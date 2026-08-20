/***************************************************************
 * (c) copyright 2026 - 2030, Solotopia
 * All Rights Reserved.
 * -------------------------------------------------------------
 * filename:  AgentActionOperationStore.cs
 * author:    taoye
 * created:   2026/8/20
 * descrip:   可跨 domain reload 恢复只读 Verify 的 Action Operation 存储
 ***************************************************************/

using System;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using Newtonsoft.Json;
using UnityEngine;

namespace NovaFramework.Editor
{
    [Serializable]
    internal sealed class AgentActionOperationRecord
    {
        public int RecordVersion = 1;
        public string OperationId;
        public string ActionId;
        public int ContractMajor;
        public long RegistryGeneration;
        public string Status;
        public string OutcomeStatus;
        public string CreatedAtUtc;
        public string UpdatedAtUtc;
        public string RequestSha256;
        public string[] WriteSet = Array.Empty<string>();
        public string ReceiptJson;
        public string Message;
    }

    internal sealed class AgentActionOperationStore
    {
        internal const string RecoveryTokenPrefix = "nova-action-operation-v1:";
        private const int c_MaxRecordBytes = 256 * 1024;
        private readonly object m_Gate = new object();
        private readonly IAgentActionClock m_Clock;
        private readonly IAgentActionIdGenerator m_IdGenerator;
        private readonly string m_Directory;

        /// <summary>
        /// 建立持久化 Operation 存储；测试可传入隔离目录。
        /// </summary>
        public AgentActionOperationStore(
            IAgentActionClock clock,
            IAgentActionIdGenerator idGenerator,
            string directory = null)
        {
            m_Clock = clock ?? throw new ArgumentNullException(nameof(clock));
            m_IdGenerator = idGenerator ?? throw new ArgumentNullException(nameof(idGenerator));
            m_Directory = directory ?? GetDefaultDirectory();
        }

        /// <summary>
        /// 在 Plan ready 时先建立持久化 Operation；记录不包含可执行 HandlerState。
        /// </summary>
        public bool TryCreate(
            string actionId,
            int contractMajor,
            long registryGeneration,
            string requestJson,
            string[] writeSet,
            string recoveryReceiptJson,
            out AgentActionOperationRecord record,
            out string recoveryToken)
        {
            lock (m_Gate)
            {
                try
                {
                    string operationId = CreateOperationId();
                    string now = m_Clock.UtcNow.ToString("O");
                    record = new AgentActionOperationRecord
                    {
                        OperationId = operationId,
                        ActionId = actionId,
                        ContractMajor = contractMajor,
                        RegistryGeneration = registryGeneration,
                        Status = "planned",
                        CreatedAtUtc = now,
                        UpdatedAtUtc = now,
                        RequestSha256 = ComputeSha256(requestJson ?? "{}"),
                        WriteSet = writeSet ?? Array.Empty<string>(),
                        ReceiptJson = recoveryReceiptJson,
                    };
                    recoveryToken = RecoveryTokenPrefix + operationId;
                    if (TryWrite(record)) return true;
                }
                catch (Exception)
                {
                    // 任何持久化准备失败都必须在 Execute 前 fail-closed。
                }
                record = null;
                recoveryToken = null;
                return false;
            }
        }

        /// <summary>
        /// Execute 调用领域 Handler 前持久化 executing；失败时不得开始写操作。
        /// </summary>
        public bool TryMarkExecuting(string operationId)
        {
            return TryUpdate(operationId, record =>
            {
                record.Status = "executing";
                record.OutcomeStatus = null;
                record.Message = null;
            });
        }

        /// <summary>
        /// 记录 Execute 返回结果。存在 Receipt 时状态为 submitted，等待只读 Verify 收口。
        /// </summary>
        public bool TryCompleteExecution(string operationId, AgentActionResult result)
        {
            return TryUpdate(operationId, record =>
            {
                record.Status = result?.Status == "success" || !string.IsNullOrWhiteSpace(result?.ReceiptJson)
                    ? "submitted"
                    : result?.Status ?? "partial";
                record.OutcomeStatus = result?.Status ?? "partial";
                record.ReceiptJson = result?.ReceiptJson ?? record.ReceiptJson;
                record.Message = result?.Message;
            });
        }

        /// <summary>
        /// 记录执行异常；跨重载恢复入口仍只能尝试 Verify，绝不重放 Execute。
        /// </summary>
        public bool TryMarkInterrupted(string operationId, string message)
        {
            return TryUpdate(operationId, record =>
            {
                record.Status = "partial";
                record.OutcomeStatus = "partial";
                record.Message = message;
            });
        }

        /// <summary>
        /// 计划已消费但在进入领域 Execute 前被安全拒绝。
        /// </summary>
        public bool TryMarkBlocked(string operationId, string message)
        {
            return TryUpdate(operationId, record =>
            {
                record.Status = "blocked";
                record.OutcomeStatus = "blocked";
                record.Message = message;
            });
        }

        /// <summary>
        /// 持久化最近一次只读 Verify 的结果，不触发任何领域写操作。
        /// </summary>
        public bool TryRecordVerification(string operationId, AgentActionResult result)
        {
            return TryUpdate(operationId, record =>
            {
                record.Status = "verified";
                record.OutcomeStatus = result?.Status;
                record.Message = result?.Message;
            });
        }

        /// <summary>
        /// 从不透明 token 加载 Operation；只返回持久化 Receipt 供 Verify 使用。
        /// </summary>
        public bool TryLoad(string recoveryToken, string actionId, int contractMajor, out AgentActionOperationRecord record)
        {
            record = null;
            if (!TryParseToken(recoveryToken, out string operationId)) return false;
            lock (m_Gate)
            {
                try
                {
                    string path = GetPath(operationId);
                    if (!File.Exists(path) || new FileInfo(path).Length > c_MaxRecordBytes) return false;
                    var settings = new JsonSerializerSettings
                    {
                        MissingMemberHandling = MissingMemberHandling.Error,
                        TypeNameHandling = TypeNameHandling.None,
                        MaxDepth = 32,
                    };
                    record = JsonConvert.DeserializeObject<AgentActionOperationRecord>(File.ReadAllText(path), settings);
                    return IsValid(record, operationId, actionId, contractMajor);
                }
                catch (Exception)
                {
                    record = null;
                    return false;
                }
            }
        }

        /// <summary>
        /// 在存储锁内读取、更新并原子替换一条 Operation 记录。
        /// </summary>
        private bool TryUpdate(string operationId, Action<AgentActionOperationRecord> update)
        {
            lock (m_Gate)
            {
                if (!TryReadById(operationId, out AgentActionOperationRecord record)) return false;
                update(record);
                record.UpdatedAtUtc = m_Clock.UtcNow.ToString("O");
                return TryWrite(record);
            }
        }

        /// <summary>
        /// 按已验证格式的 Operation ID 读取内部记录。
        /// </summary>
        private bool TryReadById(string operationId, out AgentActionOperationRecord record)
        {
            record = null;
            if (!IsOperationId(operationId)) return false;
            try
            {
                string path = GetPath(operationId);
                if (!File.Exists(path) || new FileInfo(path).Length > c_MaxRecordBytes) return false;
                record = JsonConvert.DeserializeObject<AgentActionOperationRecord>(File.ReadAllText(path));
                return record != null && record.RecordVersion == 1 && record.OperationId == operationId;
            }
            catch (Exception)
            {
                record = null;
                return false;
            }
        }

        /// <summary>
        /// 通过同目录临时文件原子写入 Operation 记录。
        /// </summary>
        private bool TryWrite(AgentActionOperationRecord record)
        {
            string temporaryPath = null;
            try
            {
                Directory.CreateDirectory(m_Directory);
                string path = GetPath(record.OperationId);
                temporaryPath = path + ".tmp";
                string json = JsonConvert.SerializeObject(record, Formatting.None);
                if (Encoding.UTF8.GetByteCount(json) > c_MaxRecordBytes) return false;
                File.WriteAllText(temporaryPath, json, new UTF8Encoding(false));
                if (File.Exists(path)) File.Replace(temporaryPath, path, null);
                else File.Move(temporaryPath, path);
                return true;
            }
            catch (Exception)
            {
                return false;
            }
            finally
            {
                if (!string.IsNullOrEmpty(temporaryPath) && File.Exists(temporaryPath))
                {
                    try { File.Delete(temporaryPath); } catch { }
                }
            }
        }

        /// <summary>
        /// 生成当前 Operation 目录内唯一的 32 位十六进制 ID。
        /// </summary>
        private string CreateOperationId()
        {
            for (int attempt = 0; attempt < 16; attempt++)
            {
                string id = m_IdGenerator.NewId();
                if (IsOperationId(id) && !File.Exists(GetPath(id))) return id;
            }
            throw new InvalidOperationException("无法生成唯一的 Action Operation ID。");
        }

        /// <summary>
        /// 解析不透明恢复令牌，并拒绝非固定格式的文件标识。
        /// </summary>
        private static bool TryParseToken(string token, out string operationId)
        {
            operationId = null;
            if (string.IsNullOrWhiteSpace(token) || !token.StartsWith(RecoveryTokenPrefix, StringComparison.Ordinal)) return false;
            operationId = token.Substring(RecoveryTokenPrefix.Length);
            return IsOperationId(operationId);
        }

        /// <summary>
        /// 判断字符串是否为安全的 Operation 文件标识。
        /// </summary>
        private static bool IsOperationId(string value)
        {
            return value?.Length == 32 && value.All(Uri.IsHexDigit);
        }

        /// <summary>
        /// 核对持久化记录与恢复请求的 Action、契约和 Operation 身份。
        /// </summary>
        private static bool IsValid(AgentActionOperationRecord record, string operationId, string actionId, int contractMajor)
        {
            return record != null && record.RecordVersion == 1 && record.OperationId == operationId &&
                   record.ActionId == actionId && record.ContractMajor == contractMajor;
        }

        /// <summary>
        /// 把安全 Operation ID 映射为存储文件路径。
        /// </summary>
        private string GetPath(string operationId) => Path.Combine(m_Directory, operationId + ".json");

        /// <summary>
        /// 获取不进入版本控制的项目级 Operation 目录。
        /// </summary>
        private static string GetDefaultDirectory()
        {
            string projectRoot = Directory.GetParent(Application.dataPath)?.FullName ?? string.Empty;
            return Path.Combine(projectRoot, "Library", "Nova", "AgentActions", "Operations");
        }

        /// <summary>
        /// 计算请求快照摘要，避免把原始请求持久化到 Operation 记录。
        /// </summary>
        private static string ComputeSha256(string value)
        {
            using SHA256 sha256 = SHA256.Create();
            return string.Concat(sha256.ComputeHash(Encoding.UTF8.GetBytes(value)).Select(item => item.ToString("x2")));
        }
    }
}
