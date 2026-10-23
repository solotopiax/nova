/***************************************************************
 * (c) copyright 2026 - 2030, Solotopia
 * All Rights Reserved.
 * -------------------------------------------------------------
 * filename:  PersistenceMaintenance.cs
 * author:    taoye
 * created:   2026/10/13
 * descrip:   存档维护的目标冻结、单写锁与持久恢复日志
 ***************************************************************/

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using System.Threading;
using Newtonsoft.Json;
using NovaFramework.Runtime;
using UnityEditor;
using UnityEngine;
using Path = System.IO.Path;

namespace NovaFramework.Editor
{
    /// <summary>复用存档检查与维护入口；默认元数据检查保持只读，固定独立范围支持三种存储的值 AES 开关维护。</summary>
    internal static partial class PersistenceMaintenance
    {
        private const string Field = "m_UseAESForFileFragment";
        private const string PlayerPrefsIndexKey = "__pp_classifies__";
        private const int MaximumIndexBytes = 1024 * 1024;
        private const int MaximumIndexEntries = 4096;
        private const long MaximumSQLiteBytes = 64L * 1024 * 1024;

        /// <summary>只含托管索引计数与整集摘要；不含索引名称、玩家值、全局草稿或落盘证明。</summary>
        [Serializable]
        internal sealed class PlayerPrefsIndexMetadata
        {
            public bool classifyIndexPresent;
            public int classifyCount;
            public int itemCount;
            public int missingIndexCount;
            public int missingItemCount;
            public int indexByteCount;
            public string metadataSha256;
            public string provider = "UnityEngine.PlayerPrefs";
            public string observationScope = "current-provider-managed-indexes";
            public string playerValues = "not_read";
            public string wholeKeySpace = "unknown";
            public string globalDirtyState = "unknown";
            public string durableState = "unknown";
            public bool writeMaintenanceSupported;
        }

        /// <summary>固定文件是否存在、大小及整体摘要；不存在时摘要为空，不解析数据库内容。</summary>
        [Serializable]
        internal sealed class SQLiteFileEntry
        {
            public string name;
            public bool present;
            public long byteCount;
            public string sha256;
        }

        /// <summary>仅观察主库和三种伴随文件；双次稳定读不等于原子快照、连接独占或逻辑完整性。</summary>
        [Serializable]
        internal sealed class SQLiteFileMetadata
        {
            public SQLiteFileEntry[] files;
            public int unexpectedEntryCount;
            public string metadataSha256;
            public string observationScope = "current-editor-fixed-sqlite-files";
            public string playerValues = "not_read";
            public string databaseIntegrity = "not_checked";
            public string logicalConsistency = "unknown";
            public string activeConnections = "unknown";
            public string durableState = "unknown";
            public bool writeMaintenanceSupported;
        }

        /// <summary>唯一自有 Prefab PersistComponent 身份；存储 root 从当前 Editor 权威路径读取。</summary>
        [Serializable]
        internal sealed class Request
        {
            [AgentActionRequired] public string targetGuid;
            [AgentActionRequired] public string globalObjectId;
            [AgentActionRequired] public string storage;
            [AgentActionRequired] public string dataScope;
            public string sourceSha256;
            public bool? enableAES;
            public bool? acknowledgeGlobalSave;
            public string operation;
            public bool? generateNewCredential;
            public string targetCredentialHandle;
            public bool? acknowledgeEditorOnly;
            public bool? acknowledgeDefaultAesConsumerScope;
        }

        /// <summary>可持久恢复的脱敏全体事实；玩家内容仅保留于原字节备份，不出现在 JSON。</summary>
        [Serializable]
        internal sealed class Journal
        {
            public int schemaVersion = 1;
            public string projectRoot;
            public string operationId;
            public string phase;
            public string storage = "FileFragment";
            public SerializedTargetEditing.Target target;
            public SerializedTargetEditing.Receipt settings;
            public SerializedTargetEditing.Receipt savedSettings;
            public bool beforeAES;
            public bool afterAES;
            public string masterGuid;
            public string masterSha256;
            public string masterMetaSha256;
            public string coordinate;
            public string sourceRoot;
            public string beforeSha256;
            public string afterSha256;
            public FileFragmentMaintenance.Entry[] entries;
        }

        /// <summary>Execute 状态只驻留当前进程，包含敏感凭据和单次计划；重载只能只读 Verify 或新建恢复计划。</summary>
        internal sealed class Plan
        {
            internal Journal Journal;
            internal FileFragmentMaintenance.Plan Files;
            internal SerializedTargetEditing.Plan Settings;
            internal string JournalHash;
            internal string RecoveryToken;
            internal string RecoveryFileHash;
            internal string RecoveryStateHash;
            internal bool CleanupOnly;
            internal int Consumed;
        }

        /// <summary>检查公开请求严格身份和存储枚举；不接受数字、路径、密码或其他后端。</summary>
        internal static bool Validate(Request request, bool migration, out string error)
        {
            if (request?.operation == "rotate-privacy-aes") return ValidatePrivacyRotation(request, migration, out error);
            if (request != null && (request.acknowledgeEditorOnly.HasValue || request.acknowledgeDefaultAesConsumerScope.HasValue))
            { error = "当前操作不接受 Privacy 全域确认字段。"; return false; }
            if (request?.operation == "rotate-sqlite-cipher")
            { if (migration) return ValidateCipher(request, out error); error = "轮换仅由迁移操作执行。"; return false; }
            if (request != null && (request.operation != null && request.operation != "toggle-value-aes" || request.generateNewCredential.HasValue || request.targetCredentialHandle != null))
            { error = "加密操作或凭据策略无效。"; return false; }
            if (request?.storage == "PlayerPrefs" || request?.storage == "SQLite")
                return ValidateValues(request, migration, out error);
            error = null;
            if (request == null || request.storage != "FileFragment" || request.dataScope != "current-editor-global-data" ||
                !SerializedTargetEditing.ValidateTarget(ToTarget(request), out error) || string.IsNullOrEmpty(request.globalObjectId) ||
                request.acknowledgeGlobalSave.HasValue || migration && (!request.enableAES.HasValue || !FileFragmentMaintenance.ValidHash(request.sourceSha256)))
            {
                error = "须提供唯一 Prefab PersistComponent、storage=FileFragment、dataScope=current-editor-global-data；迁移须显式 enableAES 与检查得到的 sourceSha256。";
                return false;
            }
            return true;
        }

        /// <summary>检查接受三种存储的默认元数据范围及 PlayerPrefs 完整索引值范围，不接受写入意图。</summary>
        internal static bool ValidateInspection(Request request, out string error)
        {
            if (request?.operation == "inspect-privacy-aes") return ValidatePrivacyRotation(request, false, out error);
            if (request != null && (request.acknowledgeEditorOnly.HasValue || request.acknowledgeDefaultAesConsumerScope.HasValue))
            { error = "普通存档检查不接受 Privacy 全域确认字段。"; return false; }
            if (request != null && (request.operation != null || request.generateNewCredential.HasValue || request.targetCredentialHandle != null))
            { error = "检查不接受轮换或凭据策略。"; return false; }
            if (request?.storage == "PlayerPrefs" && request.dataScope == "current-editor-managed-indexed-data")
                return ValidateValues(request, false, out error);
            error = null;
            if (request == null || request.dataScope != "current-editor-global-data" ||
                request.storage != "FileFragment" && request.storage != "PlayerPrefs" && request.storage != "SQLite" ||
                !SerializedTargetEditing.ValidateTarget(ToTarget(request), out error) || string.IsNullOrEmpty(request.globalObjectId) ||
                request.enableAES.HasValue || request.sourceSha256 != null || request.acknowledgeGlobalSave.HasValue)
            {
                error = "检查须提供唯一 Prefab PersistComponent、精确 storage 和 dataScope=current-editor-global-data；不接受 enableAES/sourceSha256 或写入意图。";
                return false;
            }
            return true;
        }

        /// <summary>只读检查标准存储的元数据；不访问 Privacy/Cipher、Inspector、Manager 或 SQLite 连接。</summary>
        internal static AgentActionResult InspectMetadata(Request request, CancellationToken token)
        {
            if (!ValidateInspection(request, out string error) || request.storage == "FileFragment")
                throw new InvalidOperationException(error ?? "FileFragment 使用既有严格解码检查。");
            EnsureIdle();
            token.ThrowIfCancellationRequested();
            PersistComponent component = ReadMetadataTarget(request, out string assetPath);
            object metadata;
            bool incomplete;
            if (request.storage == "PlayerPrefs")
            {
#if NOVA_DYSDK || NOVA_WXSDK || NOVA_ALIPAYSDK
                return AgentActionResult.Create(null, "not_applicable", "索引检查只支持默认 Unity PlayerPrefs provider；未读取平台存储。");
#else
                if (component.CurPlayerPrefsManagerTypeName != "NovaFramework.Runtime.PlayerPrefsManager")
                    throw new InvalidOperationException("只支持标准 PlayerPrefsManager 的索引格式。");
                PlayerPrefsIndexMetadata indexes = InspectPlayerPrefsIndexes(UnityEngine.PlayerPrefs.HasKey,
                    key => UnityEngine.PlayerPrefs.GetString(key), token);
                metadata = indexes;
                incomplete = indexes.missingIndexCount != 0 || indexes.missingItemCount != 0;
#endif
            }
            else
            {
                if (component.CurSQLiteManagerTypeName != "NovaFramework.Runtime.SQLiteManager")
                    throw new InvalidOperationException("只支持标准 SQLiteManager 的固定文件位置。");
                SQLiteFileMetadata files = InspectSQLiteFiles(NovaFramework.Runtime.Path.Persist.SQLite.FileFullPath, token);
                metadata = files;
                incomplete = files.unexpectedEntryCount != 0 ||
                    !files.files[0].present && files.files.Skip(1).Any(file => file.present) ||
                    files.files[0].present && files.files[0].byteCount == 0;
            }
            token.ThrowIfCancellationRequested();
            EnsureIdle();
            if (ReadMetadataTarget(request, out string currentPath) != component || currentPath != assetPath)
                throw new InvalidOperationException("检查期间目标身份变化。");
            var result = AgentActionResult.Create(null, incomplete ? "partial" : "success", incomplete ?
                "元数据已只读检查，存在索引缺项或异常文件集合；未读取玩家值，未证明落盘、完整数据或迁移就绪。" :
                "限定范围元数据已只读检查；未读取玩家值，未证明落盘、完整数据或迁移就绪。");
            result.DataJson = JsonConvert.SerializeObject(new { schemaVersion = 1, request.storage, request.dataScope,
                target = ToTarget(request), assetPath, metadata, dataOrConfigWritten = false });
            result.ReceiptJson = JsonConvert.SerializeObject(request);
            result.EvidenceKinds = AgentActionEvidence.Static;
            result.Evidence.Add("仅索引/文件元数据的当前观察；不初始化 Manager、打开 Inspector、Save 或创建数据库连接。");
            return result;
        }

        /// <summary>用共享身份与路径校验定位唯一自有普通 Prefab；仅读取已核实的 Manager 类型属性，不描述凭据字段。</summary>
        private static PersistComponent ReadMetadataTarget(Request request, out string assetPath)
        {
            assetPath = AssetDatabase.GUIDToAssetPath(request.targetGuid);
            if (string.IsNullOrEmpty(assetPath) || !assetPath.StartsWith("Assets/", StringComparison.Ordinal) ||
                !assetPath.EndsWith(".prefab", StringComparison.OrdinalIgnoreCase) || assetPath.Split('/').Any(segment =>
                    new[] { "Framework", "Plugins", "ThirdParty", "Third-Party", "Third Party", "Vendor", "Vendors",
                        "Generated", "GeneratedCode", "_configs", "Tests" }.Contains(segment, StringComparer.OrdinalIgnoreCase)) ||
                !GenerateActionCommon.TryResolveProjectPath(assetPath, "target", out string absolute, out _) ||
                !File.Exists(absolute) || !File.Exists(absolute + ".meta"))
                throw new InvalidOperationException("目标须是项目自有普通 Prefab。");
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(assetPath);
            if (prefab == null || PrefabUtility.GetPrefabAssetType(prefab) != PrefabAssetType.Regular ||
                !GlobalObjectId.TryParse(request.globalObjectId, out GlobalObjectId id))
                throw new InvalidOperationException("目标 Prefab 或组件身份无效。");
            var component = GlobalObjectId.GlobalObjectIdentifierToObjectSlow(id) as PersistComponent;
            var transforms = prefab.GetComponentsInChildren<Transform>(true);
            if (component == null || !EditorUtility.IsPersistent(component) || AssetDatabase.GetAssetPath(component) != assetPath ||
                component.transform.root.gameObject != prefab || prefab.GetComponentsInChildren<PersistComponent>(true).Length != 1 ||
                transforms.Any(transform => PrefabUtility.IsAnyPrefabInstanceRoot(transform.gameObject) ||
                    EditorUtility.IsDirty(transform.gameObject) || transform.GetComponents<Component>().Any(value => value == null || EditorUtility.IsDirty(value))) ||
                UnityEditor.SceneManagement.PrefabStageUtility.GetCurrentPrefabStage()?.assetPath == assetPath)
                throw new InvalidOperationException("目标须是唯一真实 PersistComponent，不能含嵌套、Missing Script 或未保存草稿。");
            return component;
        }

        /// <summary>两次有界读取托管索引及已索引键存在性，变化即拒绝；委托只读索引，绝不读取玩家值。</summary>
        internal static PlayerPrefsIndexMetadata InspectPlayerPrefsIndexes(Func<string, bool> hasKey, Func<string, string> readIndex, CancellationToken token)
        {
            if (hasKey == null || readIndex == null) throw new ArgumentNullException();
            PlayerPrefsIndexMetadata first = ReadPlayerPrefsIndexMetadata(hasKey, readIndex, token);
            PlayerPrefsIndexMetadata second = ReadPlayerPrefsIndexMetadata(hasKey, readIndex, token);
            if (first.metadataSha256 != second.metadataSha256) throw new InvalidOperationException("托管索引检查期间变化。");
            return second;
        }

        /// <summary>读取两个既有索引格式，严格限制格式、数量和 UTF8 大小；缺失引用计数但不猜测未索引键。</summary>
        private static PlayerPrefsIndexMetadata ReadPlayerPrefsIndexMetadata(Func<string, bool> hasKey, Func<string, string> readIndex, CancellationToken token)
        {
            token.ThrowIfCancellationRequested();
            var result = new PlayerPrefsIndexMetadata();
            var proof = new List<string>();
            string classes = ReadPlayerPrefsIndex(PlayerPrefsIndexKey, hasKey, readIndex, result, proof, token);
            result.classifyIndexPresent = classes != null;
            string[] classifies = ParsePlayerPrefsIndex(classes, false);
            result.classifyCount = classifies.Length;
            foreach (string classify in classifies)
            {
                string raw = ReadPlayerPrefsIndex(classify + "::__items__", hasKey, readIndex, result, proof, token);
                if (raw == null) result.missingIndexCount++;
                string[] items = ParsePlayerPrefsIndex(raw, true);
                result.itemCount += items.Length;
                if (result.itemCount > MaximumIndexEntries) throw new InvalidOperationException("托管条目数超限。");
                foreach (string item in items)
                {
                    token.ThrowIfCancellationRequested();
                    string key = classify + "::" + item;
                    bool present = hasKey(key);
                    if (!present) result.missingItemCount++;
                    proof.Add(JsonConvert.SerializeObject(new { key, present }));
                }
            }
            result.metadataSha256 = GenerateActionCommon.ComputeTextHash(string.Join("\n", proof));
            return result;
        }

        /// <summary>只从明确索引键获取文本；大小只计算索引，不读取或估算玩家值大小。</summary>
        private static string ReadPlayerPrefsIndex(string key, Func<string, bool> hasKey, Func<string, string> readIndex,
            PlayerPrefsIndexMetadata result, List<string> proof, CancellationToken token)
        {
            token.ThrowIfCancellationRequested();
            bool present = hasKey(key);
            string raw = present ? readIndex(key) : null;
            if (present && raw == null || raw != null && raw.Length > MaximumIndexBytes) throw new InvalidOperationException("托管索引文本无效。");
            result.indexByteCount += raw == null ? 0 : new UTF8Encoding(false, true).GetByteCount(raw);
            if (result.indexByteCount > MaximumIndexBytes) throw new InvalidOperationException("托管索引大小超限。");
            proof.Add(JsonConvert.SerializeObject(new { key, present, index = raw }));
            return raw;
        }

        /// <summary>复用逗号分隔和双冒号键格式；拒绝空项、重复、保留条目名及超长名称，不放宽运行时名称合同。</summary>
        private static string[] ParsePlayerPrefsIndex(string raw, bool items)
        {
            if (string.IsNullOrEmpty(raw)) return Array.Empty<string>();
            string[] names = raw.Split(',');
            if (names.Length > MaximumIndexEntries || names.Any(name => string.IsNullOrEmpty(name) || name.Length > 1024 ||
                    name.Contains("::") || name.IndexOf('\0') >= 0 || items && name == "__items__") ||
                names.Distinct(StringComparer.Ordinal).Count() != names.Length)
                throw new InvalidOperationException("托管索引名称无效或重复。");
            return names;
        }

        /// <summary>仅对固定 game.db 及三个伴随文件做双次有界摘要；不建目录、连接、checkpoint 或一致快照。</summary>
        internal static SQLiteFileMetadata InspectSQLiteFiles(string databasePath, CancellationToken token)
        {
            if (string.IsNullOrEmpty(databasePath) || Path.GetFileName(databasePath) != "game.db")
                throw new InvalidOperationException("仅检查标准 game.db 文件集合。");
            SQLiteFileMetadata first = ReadSQLiteFileMetadata(databasePath, token);
            SQLiteFileMetadata second = ReadSQLiteFileMetadata(databasePath, token);
            if (first.metadataSha256 != second.metadataSha256 || first.unexpectedEntryCount != second.unexpectedEntryCount)
                throw new InvalidOperationException("SQLite 文件元数据检查期间变化。");
            return second;
        }

        /// <summary>检查固定单层范围和容量，仅报告未知文件数量，不读取其他文件或玩家记录。</summary>
        private static SQLiteFileMetadata ReadSQLiteFileMetadata(string databasePath, CancellationToken token)
        {
            token.ThrowIfCancellationRequested();
            string root = Path.GetDirectoryName(Path.GetFullPath(databasePath));
            CheckMetadataPath(root);
            if (File.Exists(root)) throw new InvalidOperationException("SQLite 目录被普通文件占用。");
            string[] names = { "game.db", "game.db-wal", "game.db-shm", "game.db-journal" };
            string[] children = Directory.Exists(root) ? Directory.EnumerateFileSystemEntries(root).Take(33).ToArray() : Array.Empty<string>();
            if (children.Length > 32) throw new InvalidOperationException("SQLite 目录项目数超限。");
            var result = new SQLiteFileMetadata { files = names.Select(name => ReadSQLiteFile(Path.Combine(root, name), token)).ToArray(),
                unexpectedEntryCount = children.Count(child => !names.Contains(Path.GetFileName(child), StringComparer.Ordinal)) };
            if (result.files.Sum(file => file.byteCount) > MaximumSQLiteBytes) throw new InvalidOperationException("SQLite 文件集合大小超限。");
            result.metadataSha256 = GenerateActionCommon.ComputeTextHash(JsonConvert.SerializeObject(result.files));
            return result;
        }

        /// <summary>以只读句柄对冻结长度做有界 SHA256，并核对前后大小和修改时间；不声明取得 SQLite 排他锁。</summary>
        private static SQLiteFileEntry ReadSQLiteFile(string path, CancellationToken token)
        {
            token.ThrowIfCancellationRequested();
            CheckMetadataPath(path);
            if (Directory.Exists(path)) throw new InvalidOperationException("SQLite 固定文件被目录占用。");
            var result = new SQLiteFileEntry { name = Path.GetFileName(path), present = File.Exists(path) };
            if (!result.present) return result;
            var before = new FileInfo(path);
            long length = before.Length;
            DateTime modified = before.LastWriteTimeUtc;
            if (length > MaximumSQLiteBytes) throw new InvalidOperationException("SQLite 文件大小超限。");
            using (var stream = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.Read))
            using (SHA256 hash = SHA256.Create())
            {
                if (stream.Length != length) throw new InvalidOperationException("SQLite 文件长度变化。");
                byte[] buffer = new byte[65536];
                long remaining = length;
                while (remaining > 0)
                {
                    token.ThrowIfCancellationRequested();
                    int count = stream.Read(buffer, 0, (int)Math.Min(buffer.Length, remaining));
                    if (count == 0) throw new InvalidOperationException("SQLite 文件读取不完整。");
                    hash.TransformBlock(buffer, 0, count, null, 0);
                    remaining -= count;
                }
                hash.TransformFinalBlock(Array.Empty<byte>(), 0, 0);
                result.sha256 = BitConverter.ToString(hash.Hash).Replace("-", string.Empty).ToLowerInvariant();
                if (stream.Length != length) throw new InvalidOperationException("SQLite 文件长度变化。");
            }
            CheckMetadataPath(path);
            var after = new FileInfo(path);
            if (!after.Exists || after.Length != length || after.LastWriteTimeUtc != modified)
                throw new InvalidOperationException("SQLite 文件检查期间变化。");
            result.byteCount = length;
            return result;
        }

        /// <summary>复用维护路径校验并补查不存在目标的链接属性；只接受普通路径，不跟随符号链接或创建末端。</summary>
        private static void CheckMetadataPath(string path)
        {
            FileFragmentMaintenance.CheckPath(path);
            for (string current = Path.GetFullPath(path); !string.IsNullOrEmpty(current); current = Path.GetDirectoryName(current))
            {
                try
                {
                    if ((File.GetAttributes(current) & FileAttributes.ReparsePoint) != 0)
                        throw new InvalidOperationException("SQLite 元数据路径不能含符号链接。");
                }
                catch (FileNotFoundException) { }
                catch (DirectoryNotFoundException) { }
            }
        }

        /// <summary>严格读取当前数据并冻结资产、配置与全部分片；Plan 不产生备份、阶段目录或文件写入。</summary>
        internal static Plan Build(Request request, bool migration, CancellationToken token)
        {
            if (!Validate(request, migration, out string error)) throw new InvalidOperationException(error);
            EnsureIdle();
            SerializedTargetEditing.Target target = ToTarget(request);
            SerializedTargetEditing.Inspection view = SerializedTargetEditing.Inspect(target, token);
            if (view.targetType != typeof(PersistComponent).FullName || !view.editable)
                throw new InvalidOperationException("须关闭目标 Inspector，并定位自有普通 Prefab 中真实 PersistComponent。");
            var manager = view.fields.SingleOrDefault(field => field.path == "m_CurFileFragmentManagerTypeName");
            if (manager?.valueJson != "\"NovaFramework.Runtime.FileFragmentManager\"")
                throw new InvalidOperationException("当前只支持标准 FileFragmentManager，不推断自定义存储格式。");
            bool before = CurrentAES(view);
            bool after = migration ? request.enableAES.Value : before;
            var settings = SerializedTargetEditing.BuildPersistenceAESPlan(target, after, token);
            ConfigMasterSO master = ReadMaster(out string masterPath, out string masterGuid);
            PrivacyConfigs privacy = master.GetPrivacyConfigs(master.CurrentPlatform, master.CurrentChannel, master.CurrentDevelopMode);
            string root = AesMaintenanceFileRoot;
            var files = FileFragmentMaintenance.Build(root, before, after, privacy?.AESKey, privacy?.AESIV, token);
            if (migration && request.sourceSha256 != files.SnapshotSha256) throw new InvalidOperationException("检查与迁移源集合漂移。");
            if (!migration || before == after) foreach (var entry in files.Entries) entry.afterSha256 = entry.beforeSha256;
            var journal = new Journal { projectRoot = GenerateActionCommon.ProjectRoot, operationId = Guid.NewGuid().ToString("N"),
                phase = "planned", target = target, settings = JsonConvert.DeserializeObject<SerializedTargetEditing.Receipt>(JsonConvert.SerializeObject(settings.Receipt)), beforeAES = before, afterAES = after,
                masterGuid = masterGuid, masterSha256 = AssetHash(masterPath, token), masterMetaSha256 = AssetHash(masterPath + ".meta", token),
                coordinate = Coordinate(master), sourceRoot = Path.GetFullPath(root), beforeSha256 = files.SnapshotSha256,
                entries = files.Entries };
            AssertMaster(journal, token);
            return new Plan { Journal = journal, Files = files, Settings = settings };
        }

        /// <summary>执行冻结迁移；先整集备份和阶段重读，再持久日志，最后替换数据并保存唯一字段。</summary>
        internal static AgentActionResult Execute(Plan plan, CancellationToken token)
        {
            if (plan?.Journal == null || Interlocked.Exchange(ref plan.Consumed, 1) != 0)
                return AgentActionResult.Create(null, "blocked", "维护计划无效或已消费，请重新 Plan。");
            Journal journal = plan.Journal;
            bool committing = false;
            bool prepared = false;
            string operationDirectory = OperationDirectory(journal.operationId);
            try
            {
                EnsureIdle();
                AssertMaster(journal, token);
                AssertSettingsBefore(journal, token);
                FileFragmentMaintenance.AssertBefore(plan.Files, null, token);
                if (journal.beforeAES == journal.afterAES)
                {
                    journal.phase = "unchanged";
                    journal.afterSha256 = journal.beforeSha256;
                    return Result(journal, "success", "当前加密状态已满足；数据与配置未写入。");
                }
                using (Acquire(journal))
                {
                    FileFragmentMaintenance.AssertBefore(plan.Files, journal.operationId, token);
                    if (Directory.Exists(operationDirectory)) throw new InvalidOperationException("操作目录已经存在。");
                    Directory.CreateDirectory(operationDirectory);
                    prepared = true;
                    PrepareAesAssetOrigin(journal, token);
                    FileFragmentMaintenance.Prepare(plan.Files, journal.operationId, operationDirectory, token);
                    journal.afterSha256 = FileFragmentMaintenance.Fingerprint(journal.entries, true);
                    journal.phase = "prepared";
                    SaveJournal(journal);
                    AssertMaster(journal, token);
                    AssertSettingsBefore(journal, token);
                    token.ThrowIfCancellationRequested();
                    journal.phase = "committing";
                    SaveJournal(journal);
                    committing = true;
                    // 跨数据文件与 Unity 资产不是真事务；提交点之后保留恢复日志，不依赖取消或异常自动回滚。
                    FileFragmentMaintenance.Apply(plan.Files, journal.operationId, CancellationToken.None);
                    AgentActionResult saved = SerializedTargetEditing.Execute(plan.Settings, CancellationToken.None);
                    if (saved.Status != "success") throw new InvalidOperationException("目标字段保存未通过。");
                    journal.savedSettings = JsonConvert.DeserializeObject<SerializedTargetEditing.Receipt>(saved.ReceiptJson);
                    journal.phase = "committed";
                    SaveJournal(journal);
                    FileFragmentMaintenance.AssertAfter(journal.sourceRoot, journal.entries, journal.operationId, CancellationToken.None);
                    if (SerializedTargetEditing.Verify(saved.ReceiptJson, CancellationToken.None).Status != "success")
                        throw new InvalidOperationException("配置后验失败。");
                    FileFragmentMaintenance.CleanStage(journal.sourceRoot, journal.operationId, journal.entries);
                    return Result(journal, "success", "分片全集转换、独立重读与唯一配置保存已验证；原字节备份和恢复日志保留。");
                }
            }
            catch (OperationCanceledException) when (!committing)
            {
                if (prepared && !TryCleanUncommitted(journal, operationDirectory))
                    return Result(journal, "partial", "提交前已取消；正式数据未开始写入，但临时维护材料无法安全清理，保留现场核对。");
                throw;
            }
            catch
            {
                if (!committing)
                {
                    if (prepared && !TryCleanUncommitted(journal, operationDirectory))
                        return Result(journal, "partial", "提交前失败，正式数据未开始写入；临时维护材料无法安全清理，保留现场核对。");
                    return Result(journal, "blocked", "提交前冻结、备份或阶段验证失败；正式数据与配置未开始写入。");
                }
                return Result(journal, "partial", "已进入维护提交；保留恢复日志与备份，先 Verify 再独立计划 recover-maintenance，禁止重放迁移。");
            }
        }

        /// <summary>从规范操作标识读取唯一项目恢复日志；不允许用户指定路径或改变日志目标。</summary>
        internal static Journal LoadJournal(string operationId)
        {
            string directory = OperationDirectory(operationId);
            FileFragmentMaintenance.CheckPath(directory);
            string path = Path.Combine(directory, "journal.json");
            FileFragmentMaintenance.CheckPath(path);
            if (!File.Exists(path) || new FileInfo(path).Length > 1024 * 1024) throw new InvalidOperationException("恢复日志不存在或过大。");
            Journal journal = ParseJournal(File.ReadAllText(path));
            ValidateJournal(journal, operationId);
            return journal;
        }

        /// <summary>校验全部固定身份、全集指纹与唯一字段写集；日志不能扩展成任意数据或配置恢复器。</summary>
        private static void ValidateJournal(Journal journal, string operationId)
        {
            if (journal?.entries == null) throw new InvalidOperationException("维护条目缺失。");
            FileFragmentMaintenance.ValidateEntries(journal.entries);
            if (journal == null || journal.schemaVersion != 1 || journal.operationId != operationId || journal.storage != "FileFragment" ||
                journal.projectRoot != GenerateActionCommon.ProjectRoot || journal.sourceRoot != Path.GetFullPath(AesMaintenanceFileRoot) ||
                journal.settings == null || journal.target == null || journal.settings.target?.targetGuid != journal.target.targetGuid ||
                journal.settings.target?.globalObjectId != journal.target.globalObjectId ||
                !FileFragmentMaintenance.ValidHash(journal.beforeSha256) || !FileFragmentMaintenance.ValidHash(journal.afterSha256) ||
                journal.settings.targetType != typeof(PersistComponent).FullName || journal.settings.changes == null ||
                !SerializedTargetEditing.ValidateTarget(journal.target, out _) || string.IsNullOrEmpty(journal.target.globalObjectId) ||
                journal.settings.schemaVersion != 1 || !IsGuid(journal.masterGuid) || !IsGuid(journal.settings.scriptGuid) ||
                !FileFragmentMaintenance.ValidHash(journal.masterSha256) || !FileFragmentMaintenance.ValidHash(journal.masterMetaSha256) ||
                string.IsNullOrEmpty(journal.coordinate) || string.IsNullOrEmpty(journal.settings.assetPath) ||
                string.IsNullOrEmpty(journal.settings.scriptPath) || !FileFragmentMaintenance.ValidHash(journal.settings.scriptSha256) ||
                !FileFragmentMaintenance.ValidHash(journal.settings.metaSha256) || !FileFragmentMaintenance.ValidHash(journal.settings.plannedFileSha256) ||
                !FileFragmentMaintenance.ValidHash(journal.settings.beforeStateSha256) || !FileFragmentMaintenance.ValidHash(journal.settings.expectedStateSha256) ||
                journal.settings.executionStarted || journal.settings.observedFileSha256 != null ||
                journal.settings.changes.Any(change => change == null || change.propertyPath != Field || change.propertyType != "Boolean") ||
                journal.settings.changes.Length != (journal.beforeAES == journal.afterAES ? 0 : 1) ||
                journal.beforeAES == journal.afterAES && journal.phase != "planned" && journal.phase != "unchanged" ||
                journal.phase == "unchanged" && journal.beforeAES != journal.afterAES ||
                journal.beforeSha256 != FileFragmentMaintenance.Fingerprint(journal.entries, false) ||
                journal.afterSha256 != FileFragmentMaintenance.Fingerprint(journal.entries, true) ||
                !new[] { "planned", "unchanged", "prepared", "committing", "committed", "recovering", "recovered" }.Contains(journal.phase))
                throw new InvalidOperationException("恢复日志身份或全集摘要不匹配。");
        }

        /// <summary>日志严格按固定结构解析，拒绝未知成员、类型注入及过深嵌套。</summary>
        private static Journal ParseJournal(string json) => JsonConvert.DeserializeObject<Journal>(json,
            new JsonSerializerSettings { MissingMemberHandling = MissingMemberHandling.Error, TypeNameHandling = TypeNameHandling.None, MaxDepth = 32 });

        /// <summary>配置与脚本 GUID 只接受固定 Unity GUID 格式。</summary>
        private static bool IsGuid(string value) => value?.Length == 32 && value.All(Uri.IsHexDigit);

        /// <summary>只读重新验证完整文件、唯一配置与原始备份；未提交计划不算执行成功。</summary>
        internal static AgentActionResult Verify(string receiptJson, CancellationToken token)
        {
            try
            {
                Journal receipt = JsonConvert.DeserializeObject<Journal>(receiptJson);
                if (receipt == null) throw new InvalidOperationException();
                if (receipt.phase != "planned") ValidateJournal(receipt, receipt.operationId);
                EnsureIdle();
                if (receipt.phase == "planned") return Result(receipt, "blocked", "该计划未执行，不能计入迁移验收。");
                if (receipt.phase == "unchanged")
                {
                    AssertMaster(receipt, token);
                    AssertSettingsBefore(receipt, token);
                    if (FileFragmentMaintenance.Fingerprint(FileFragmentMaintenance.Build(receipt.sourceRoot,
                            receipt.beforeAES, receipt.beforeAES, ReadPrivacy(receipt).AESKey, ReadPrivacy(receipt).AESIV, token).Entries, false) != receipt.beforeSha256)
                        throw new InvalidOperationException();
                    return Result(receipt, "success", "状态仍已满足，未产生维护写入。");
                }
                Journal journal = LoadJournal(receipt.operationId);
                if (!SameOrigin(receipt, journal)) throw new InvalidOperationException("后验收据不属于当前持久维护日志。");
                AssertMaster(journal, token);
                if (journal.phase != "committed" && journal.phase != "recovered")
                    return Result(journal, "partial", "维护日志尚未完成；按精确 operationId 计划恢复，不重放原 Execute。");
                if (Directory.Exists(FileFragmentMaintenance.StagePath(journal.sourceRoot, journal.operationId)))
                    return Result(journal, "partial", "本操作阶段目录仍保留，维护清理尚未完成；未执行额外清理或写入。");
                FileFragmentMaintenance.AssertRecoverable(journal.sourceRoot, journal.entries, journal.operationId, OperationDirectory(journal.operationId), token);
                if (journal.phase == "committed") FileFragmentMaintenance.AssertAfter(journal.sourceRoot, journal.entries, journal.operationId, token);
                else if (FileFragmentMaintenance.Fingerprint(FileFragmentMaintenance.Build(journal.sourceRoot, journal.beforeAES, journal.beforeAES,
                        ReadPrivacy(journal).AESKey, ReadPrivacy(journal).AESIV, token).Entries, false) != journal.beforeSha256)
                    throw new InvalidOperationException();
                VerifyAesSavedSettings(journal, token);
                return Result(journal, "success", journal.phase == "recovered" ? "全集原字节与原配置已恢复验证。" : "全集新字节、配置保存与原始备份仍匹配。");
            }
            catch (OperationCanceledException) { throw; }
            catch { return AgentActionResult.Create(null, "partial", "维护后验未通过，当前数据、配置或备份可能漂移；未执行写入。"); }
        }

        /// <summary>已完成日志的保存收据只能验证原目标与对应原/新完整状态，不能替换为其它资产成功收据。</summary>
        private static bool ValidSavedSettings(Journal journal) => journal.savedSettings != null &&
            journal.savedSettings.schemaVersion == 1 && journal.savedSettings.targetType == journal.settings.targetType &&
            JsonConvert.SerializeObject(journal.savedSettings.target) == JsonConvert.SerializeObject(journal.target) &&
            journal.savedSettings.assetPath == journal.settings.assetPath && journal.savedSettings.scriptPath == journal.settings.scriptPath &&
            journal.savedSettings.scriptGuid == journal.settings.scriptGuid && journal.savedSettings.scriptSha256 == journal.settings.scriptSha256 &&
            journal.savedSettings.metaSha256 == journal.settings.metaSha256 &&
            journal.savedSettings.expectedStateSha256 == (journal.phase == "recovered" ? journal.settings.beforeStateSha256 : journal.settings.expectedStateSha256);

        /// <summary>恢复前独立预演原字段，并核对全集只含本操作原/新字节；原迁移不会在此重放。</summary>
        internal static Plan BuildRecovery(string operationId, string recoveryToken, CancellationToken token)
        {
            EnsureIdle();
            Journal journal = LoadJournal(operationId);
            AssertOperationBinding(journal, recoveryToken);
            CheckJournalTemporary(journal, true);
            if (!new[] { "prepared", "committing", "committed", "recovering", "recovered" }.Contains(journal.phase))
                throw new InvalidOperationException("操作未处于可恢复阶段。");
            AssertMaster(journal, token);
            AssertAesSettingsRecoverable(journal, token);
            FileFragmentMaintenance.AssertRecoverable(journal.sourceRoot, journal.entries, journal.operationId, OperationDirectory(operationId), token);
            PrivacyConfigs privacy = ReadPrivacy(journal);
            FileFragmentMaintenance.AssertRecoveryContent(journal.sourceRoot, journal.entries, OperationDirectory(operationId),
                journal.beforeAES, journal.afterAES, privacy?.AESKey, privacy?.AESIV, token);
            if (journal.phase == "recovered")
            {
                AssertRecoveredFragmentContent(journal, token);
                VerifyAesSavedSettings(journal, token);
            }
            var view = SerializedTargetEditing.Inspect(journal.target, token);
            return new Plan { Journal = journal, CleanupOnly = journal.phase == "recovered", RecoveryToken = recoveryToken, RecoveryFileHash = view.fileSha256, RecoveryStateHash = view.stateSha256,
                JournalHash = FileFragmentMaintenance.Hash(Path.Combine(OperationDirectory(operationId), "journal.json"), token) };
        }

        /// <summary>recovered清场重试要求全集仍为原字节；仅再次核验和清本操作阶段，不重写数据或字段。</summary>
        private static void AssertRecoveredFragmentContent(Journal journal, CancellationToken token)
        {
            FileFragmentMaintenance.AssertRecoverable(journal.sourceRoot, journal.entries, journal.operationId, OperationDirectory(journal.operationId), token);
            foreach (var entry in journal.entries)
                if (FileFragmentMaintenance.Hash(Path.Combine(journal.sourceRoot, entry.name), token) != entry.beforeSha256)
                    throw new InvalidOperationException("已恢复全集出现变化，拒绝清场重试。");
        }

        /// <summary>恢复日志必须绑定原迁移的受控 Operation 收据，不能仅凭用户修改的本地日志恢复任意配置。</summary>
        private static void AssertOperationBinding(Journal journal, string recoveryToken)
        {
            if (!AgentActionRuntime.OperationStore.TryLoad(recoveryToken, "nova.project.persistence.migrate-encryption", 1, out var operation))
                throw new InvalidOperationException("原迁移恢复凭据无效或已经丢失。");
            var envelope = JsonConvert.DeserializeObject<AgentActionReceiptEnvelope>(operation.ReceiptJson);
            if (envelope?.ActionId != "nova.project.persistence.migrate-encryption" || envelope.ContractMajor != 1)
                throw new InvalidOperationException("原迁移收据契约不匹配。");
            var original = ParseJournal(envelope.PayloadJson);
            if (!SameOrigin(original, journal))
                throw new InvalidOperationException("本地恢复日志与原迁移冻结范围不匹配。");
            if (FileFragmentMaintenance.ValidHash(original.afterSha256) && (original.afterSha256 != journal.afterSha256 ||
                !original.entries.Select(entry => entry?.afterSha256).SequenceEqual(journal.entries.Select(entry => entry?.afterSha256))))
                throw new InvalidOperationException("本地恢复日志的新字节摘要与执行收据不匹配。");
        }

        /// <summary>比较全部不可变原计划身份、原字段收据及原文件集合，不将执行阶段当作身份。</summary>
        internal static bool SameOrigin(Journal original, Journal journal) => original != null && journal != null &&
            original.operationId == journal.operationId && original.schemaVersion == journal.schemaVersion &&
            original.projectRoot == journal.projectRoot && original.storage == journal.storage && original.sourceRoot == journal.sourceRoot &&
            original.masterGuid == journal.masterGuid && original.masterSha256 == journal.masterSha256 &&
            original.masterMetaSha256 == journal.masterMetaSha256 && original.coordinate == journal.coordinate &&
            original.beforeAES == journal.beforeAES && original.afterAES == journal.afterAES && original.beforeSha256 == journal.beforeSha256 &&
            JsonConvert.SerializeObject(original.target) == JsonConvert.SerializeObject(journal.target) &&
            JsonConvert.SerializeObject(original.settings) == JsonConvert.SerializeObject(journal.settings) &&
            original.entries != null && journal.entries != null && original.entries.Length == journal.entries.Length &&
            original.entries.Select(entry => entry == null ? null : entry.name + ":" + entry.beforeSha256 + ":" + entry.itemCount + ":" + entry.byteCount)
                .SequenceEqual(journal.entries.Select(entry => entry == null ? null : entry.name + ":" + entry.beforeSha256 + ":" + entry.itemCount + ":" + entry.byteCount));

        /// <summary>单次恢复只覆盖已确认本操作字节，先持久恢复阶段、再恢复全集与原字段，失败保留可再次核对的日志。</summary>
        internal static AgentActionResult Recover(Plan plan, CancellationToken token)
        {
            if (plan?.Journal == null || Interlocked.Exchange(ref plan.Consumed, 1) != 0)
                return AgentActionResult.Create(null, "blocked", "恢复计划无效或已消费。");
            Journal journal = plan.Journal;
            bool started = false;
            try
            {
                EnsureIdle();
                if (FileFragmentMaintenance.Hash(Path.Combine(OperationDirectory(journal.operationId), "journal.json"), token) != plan.JournalHash)
                    throw new InvalidOperationException("恢复日志在预演后漂移。");
                AssertOperationBinding(journal, plan.RecoveryToken);
                AssertMaster(journal, token);
                AssertAesSettingsRecoverable(journal, token);
                var current = SerializedTargetEditing.Inspect(journal.target, token);
                if (current.fileSha256 != plan.RecoveryFileHash || current.stateSha256 != plan.RecoveryStateHash)
                    throw new InvalidOperationException("恢复预演后的目标资产漂移。");
                using (Acquire(journal))
                {
                    if (plan.CleanupOnly)
                    {
                        if (journal.phase != "recovered") throw new InvalidOperationException("清场恢复阶段漂移。");
                        AssertRecoveredFragmentContent(journal, token); VerifyAesSavedSettings(journal, token);
                        token.ThrowIfCancellationRequested(); started = true;
                        CheckJournalTemporary(journal, true, true);
                        FileFragmentMaintenance.CleanStage(journal.sourceRoot, journal.operationId, journal.entries);
                        AssertRecoveredFragmentContent(journal, CancellationToken.None); VerifyAesSavedSettings(journal, CancellationToken.None);
                        return Result(journal, "success", "已恢复全集和字段重核通过，本操作阶段与中断日志已清场或隔离保留；未重写数据。");
                    }
                    FileFragmentMaintenance.AssertRecoverable(journal.sourceRoot, journal.entries, journal.operationId, OperationDirectory(journal.operationId), token);
                    PrivacyConfigs privacy = ReadPrivacy(journal);
                    FileFragmentMaintenance.AssertRecoveryContent(journal.sourceRoot, journal.entries, OperationDirectory(journal.operationId),
                        journal.beforeAES, journal.afterAES, privacy?.AESKey, privacy?.AESIV, token);
                    token.ThrowIfCancellationRequested();
                    journal.phase = "recovering";
                    SaveJournal(journal, true);
                    started = true;
                    FileFragmentMaintenance.Restore(journal.sourceRoot, journal.entries, journal.operationId, OperationDirectory(journal.operationId), CancellationToken.None, true);
                    AgentActionResult saved = SerializedTargetEditing.RestoreAesRecoveryTarget(journal.settings, journal.storage, journal.beforeAES, journal.afterAES,
                        () => AesDiskMatchesBoundOrigin(journal, CancellationToken.None), CancellationToken.None);
                    if (saved.Status != "success") throw new InvalidOperationException("原配置恢复失败。");
                    journal.savedSettings = JsonConvert.DeserializeObject<SerializedTargetEditing.Receipt>(saved.ReceiptJson);
                    journal.phase = "recovered";
                    SaveJournal(journal, true);
                    FileFragmentMaintenance.CleanStage(journal.sourceRoot, journal.operationId, journal.entries);
                    VerifyAesSavedSettings(journal, CancellationToken.None);
                    return Result(journal, "success", "原始全集字节与原配置已恢复；保留恢复材料，不删玩家数据。");
                }
            }
            catch (OperationCanceledException) when (!started) { throw; }
            catch { return Result(journal, started ? "partial" : "blocked", started ?
                "恢复已开始但未完成；保留日志，先核对现场再新建恢复计划。" : "恢复前数据、配置或备份漂移，未开始恢复。"); }
        }

        /// <summary>禁止 Play、待编译、Runtime 实例与任何 Persist Inspector 连接并存；不擅自关闭它们。</summary>
        private static void EnsureIdle()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode || EditorApplication.isCompiling || Nova.Self != null ||
                Resources.FindObjectsOfTypeAll<UnityEditor.Editor>().Any(editor => editor != null && editor.targets != null &&
                    editor.targets.Any(target => target is PersistComponent)))
                throw new InvalidOperationException("维护需要稳定 EditMode 且没有 Persist Inspector 或运行中 Manager。");
        }

        /// <summary>精确绑定唯一持久激活 Master，不调用可能切换场景或产生写入的 WorkspaceActive.Get。</summary>
        private static ConfigMasterSO ReadMaster(out string path, out string guid)
        {
#if UNITY_INCLUDE_TESTS
            if (s_AesFixtureMaster != null)
            {
                path = AssetDatabase.GetAssetPath(s_AesFixtureMaster); guid = AssetDatabase.AssetPathToGUID(path);
                if (EditorUtility.IsDirty(s_AesFixtureMaster) || EditorUtil.Config.DesignTimeEditing.HasUnsavedDraft(s_AesFixtureMaster))
                    throw new InvalidOperationException("AES测试Master存在未保存草稿。");
                return s_AesFixtureMaster;
            }
#endif
            if (!EditorUtil.Config.WorkspaceActive.TryGetPersistedConfigMaster(out ConfigMasterSO master, out guid, out path, out _) || master == null ||
                EditorUtility.IsDirty(master) || EditorUtil.Config.DesignTimeEditing.HasUnsavedDraft(master) ||
                master.CurrentPlatform == PlatformType.None || !master.TryGetEntry(master.CurrentPlatform, master.CurrentChannel, out _))
                throw new InvalidOperationException("当前 Master、坐标或草稿无效。");
            return master;
        }

        /// <summary>整 Master 与 meta 指纹冻结，阻止途中替换凭据、维度或配置。</summary>
        private static void AssertMaster(Journal journal, CancellationToken token)
        {
            ConfigMasterSO master = ReadMaster(out string path, out string guid);
            if (guid != journal.masterGuid || Coordinate(master) != journal.coordinate ||
                AssetHash(path, token) != journal.masterSha256 || AssetHash(path + ".meta", token) != journal.masterMetaSha256)
                throw new InvalidOperationException("激活配置或凭据来源漂移。");
        }

        /// <summary>只在内部取得当前隐私 AES，密钥不进入外部协议或恢复 JSON。</summary>
        private static PrivacyConfigs ReadPrivacy(Journal journal)
        {
            AssertMaster(journal, CancellationToken.None);
            ConfigMasterSO master = ReadMaster(out _, out _);
            return master.GetPrivacyConfigs(master.CurrentPlatform, master.CurrentChannel, master.CurrentDevelopMode);
        }

        /// <summary>数据提交前完整复核既有编辑计划，不只核对 AES 单字段。</summary>
        private static void AssertSettingsBefore(Journal journal, CancellationToken token)
        {
            var view = SerializedTargetEditing.Inspect(journal.target, token);
            if (!view.editable || view.targetType != typeof(PersistComponent).FullName || view.assetPath != journal.settings.assetPath ||
                view.scriptPath != journal.settings.scriptPath || view.scriptGuid != journal.settings.scriptGuid ||
                AssetHash(view.assetPath + ".meta", token) != journal.settings.metaSha256 ||
                AssetHash(view.scriptPath, token) != journal.settings.scriptSha256 ||
                view.stateSha256 != journal.settings.beforeStateSha256 || view.fileSha256 != journal.settings.plannedFileSha256)
                throw new InvalidOperationException("目标资产或草稿漂移。");
        }

        /// <summary>恢复只接受本操作完整原/新内存和原token认证磁盘；单布尔领域接口处理owned dirty。</summary>
        private static void AssertSettingsRecoverable(Journal journal, CancellationToken token) => AssertAesSettingsRecoverable(journal, token);

        /// <summary>独占全局数据目录锁，标记操作身份；异常退出遗留锁须先确认无其他进程占用，不自动抢锁。</summary>
        private static IDisposable Acquire(Journal journal)
        {
            string path = Path.Combine(journal.sourceRoot, FileFragmentMaintenance.LockName);
            FileFragmentMaintenance.CheckPath(path);
            FileStream stream;
            if (File.Exists(path))
            {
                // 只有无其它写句柄占用且标记属于本次恢复的遗留锁才可接管；活跃维护不会被抢占。
                stream = new FileStream(path, FileMode.Open, FileAccess.ReadWrite, FileShare.Read);
                using var reader = new StreamReader(stream, System.Text.Encoding.UTF8, false, 1024, true);
                string marker = reader.ReadToEnd();
                if (marker != journal.operationId && marker != FileFragmentMaintenance.IdleLockMarker)
                { stream.Dispose(); throw new InvalidOperationException("锁属于其他维护操作。"); }
                stream.SetLength(0);
                stream.Position = 0;
            }
            else stream = new FileStream(path, FileMode.CreateNew, FileAccess.ReadWrite, FileShare.Read);
            try
            {
                byte[] bytes = System.Text.Encoding.UTF8.GetBytes(journal.operationId);
                stream.Write(bytes, 0, bytes.Length);
                stream.Flush(true);
                return new LockScope(stream);
            }
            catch { stream.Dispose(); throw; }
        }

        /// <summary>只释放本次成功取得的锁；不会删除其他进程或用户留下的锁。</summary>
        private sealed class LockScope : IDisposable
        {
            private readonly FileStream m_Stream;
            /// <summary>保存当前独占句柄及规范锁路径。</summary>
            internal LockScope(FileStream stream) { m_Stream = stream; }
            /// <summary>持有同一独占句柄时写回空闲标记再关闭；保留稳定锁文件，避免解锁删除与下一写者接手竞态。</summary>
            public void Dispose()
            {
                try
                {
                    m_Stream.SetLength(0);
                    m_Stream.Position = 0;
                    byte[] bytes = System.Text.Encoding.UTF8.GetBytes(FileFragmentMaintenance.IdleLockMarker);
                    m_Stream.Write(bytes, 0, bytes.Length);
                    m_Stream.Flush(true);
                }
                finally { m_Stream.Dispose(); }
            }
        }

        /// <summary>只构造当前项目受控操作目录；恢复材料不能被指定到任意工作区或系统路径。</summary>
        private static string OperationDirectory(string operationId)
        {
            if (!Guid.TryParseExact(operationId, "N", out _)) throw new InvalidOperationException("operationId 无效。");
            string path = Path.Combine(GenerateActionCommon.ProjectRoot, NovaFramework.Runtime.PrivacyAesMaintenanceGuard.RelativeDirectory, operationId);
            FileFragmentMaintenance.CheckPath(path);
            return path;
        }

        /// <summary>有界日志先flush再原子替换；当前半写异常清本轮tmp，断电半写只原token独立恢复隔离保留。</summary>
        private static void SaveJournal(Journal journal, bool authorizedRecovery = false)
        {
            string path = Path.Combine(OperationDirectory(journal.operationId), "journal.json"), temporary = path + ".tmp";
            FileFragmentMaintenance.CheckPath(path); FileFragmentMaintenance.CheckPath(temporary);
            if (authorizedRecovery && !SameOrigin(LoadJournal(journal.operationId), journal)) throw new InvalidOperationException("恢复正式日志来源不符。");
            CheckJournalTemporary(journal, authorizedRecovery, true);
            if (File.Exists(temporary)) File.Delete(temporary);
            byte[] bytes = Encoding.UTF8.GetBytes(JsonConvert.SerializeObject(journal)); bool created = false;
            try
            {
                using (var stream = new FileStream(temporary, FileMode.CreateNew, FileAccess.Write, FileShare.None))
                {
                    created = true; int first = bytes.Length / 2; stream.Write(bytes, 0, first);
#if UNITY_INCLUDE_TESTS
                    if (s_AesFixtureTarget != null && JsonConvert.SerializeObject(journal.target) == JsonConvert.SerializeObject(s_AesFixtureTarget)) s_AesJournalWriteFault?.Invoke(journal.phase);
#endif
                    stream.Write(bytes, first, bytes.Length - first); stream.Flush(true);
                }
                if (File.Exists(path)) File.Replace(temporary, path, null); else File.Move(temporary, path);
            }
            catch { if (created && File.Exists(temporary)) File.Delete(temporary); throw; }
        }

        /// <summary>完整临时日志须属于同一原/新集合；原token恢复预检查不写，执行时隔离保留半写材料。</summary>
        private static void CheckJournalTemporary(Journal journal, bool authorizedRecovery = false, bool preserveInterrupted = false) =>
            CheckAesJournalTemporary(Path.Combine(OperationDirectory(journal.operationId), "journal.json.tmp"), text =>
            {
                Journal candidate = ParseJournal(text);
                return SameOrigin(candidate, journal) && candidate.afterSha256 == journal.afterSha256;
            }, authorizedRecovery, preserveInterrupted);

        /// <summary>提交前失败只清本轮阶段/备份，没有改写正式数据；无法清理则保留现场供人工核对。</summary>
        private static bool TryCleanUncommitted(Journal journal, string directory)
        {
            try
            {
                FileFragmentMaintenance.CheckPath(directory);
                string backup = Path.Combine(directory, "backup");
                string stage = FileFragmentMaintenance.StagePath(journal.sourceRoot, journal.operationId);
                foreach (string folder in new[] { directory, backup, stage })
                {
                    FileFragmentMaintenance.CheckPath(folder);
                    if (!Directory.Exists(folder)) continue;
                    foreach (string child in Directory.GetFileSystemEntries(folder))
                    {
                        FileFragmentMaintenance.CheckPath(child);
                        string name = Path.GetFileName(child);
                        if (folder == directory)
                        {
                            if (child == backup && Directory.Exists(child)) continue;
                            if (Directory.Exists(child)) return false;
                            if (name == "before-aes-asset.bin") continue;
                            if (name != "journal.json" && name != "journal.json.tmp") return false;
                            var existing = ParseJournal(File.ReadAllText(child));
                            if (!SameOrigin(existing, journal)) return false;
                        }
                        else
                        {
                            var entry = journal.entries.SingleOrDefault(item => item.name == name);
                            if (Directory.Exists(child) || entry == null || folder == backup &&
                                FileFragmentMaintenance.Hash(child, CancellationToken.None) != entry.beforeSha256) return false;
                        }
                    }
                }
                FileFragmentMaintenance.CleanStage(journal.sourceRoot, journal.operationId, journal.entries);
                if (Directory.Exists(backup)) { foreach (string path in Directory.GetFiles(backup)) File.Delete(path); Directory.Delete(backup); }
                if (Directory.Exists(directory)) { foreach (string path in Directory.GetFiles(directory)) File.Delete(path); Directory.Delete(directory); }
                return true;
            }
            catch { return false; }
        }

        /// <summary>从检查返回的真实字段读取布尔状态，不调用 Inspector 或反射 Getter。</summary>
        private static bool CurrentAES(SerializedTargetEditing.Inspection view)
        {
            var field = view.fields.SingleOrDefault(field => field.path == Field);
            if (field == null || field.valueJson != "true" && field.valueJson != "false") throw new InvalidOperationException("AES 字段不可读。");
            return field.valueJson == "true";
        }

        /// <summary>只传规范资产身份，不传组件类型或任意执行方法。</summary>
        private static SerializedTargetEditing.Target ToTarget(Request request) => request == null ? null :
            new SerializedTargetEditing.Target { targetGuid = request.targetGuid, globalObjectId = request.globalObjectId };

        /// <summary>坐标只记录精确枚举名字，不包含隐私字段。</summary>
        private static string Coordinate(ConfigMasterSO master) => master.CurrentPlatform + "/" + master.CurrentChannel + "/" + master.CurrentDevelopMode;

        /// <summary>项目或已解析脚本的完整文件摘要复用已有安全路径解析。</summary>
        private static string AssetHash(string path, CancellationToken token)
        {
            string absolute;
            if (path.StartsWith("Packages/", StringComparison.Ordinal))
            {
                var package = UnityEditor.PackageManager.PackageInfo.FindForAssetPath(path);
                if (package == null || package.name != "com.solotopia.nova.framework" ||
                    !path.StartsWith("Packages/" + package.name + "/Scripts/", StringComparison.Ordinal))
                    throw new InvalidOperationException("恢复源码不属于已解析 Framework。");
                absolute = Path.GetFullPath(Path.Combine(package.resolvedPath, path.Substring(("Packages/" + package.name + "/").Length)));
                if (!absolute.StartsWith(Path.GetFullPath(package.resolvedPath) + Path.DirectorySeparatorChar, StringComparison.Ordinal))
                    throw new InvalidOperationException("恢复源码路径越界。");
                FileFragmentMaintenance.CheckPath(absolute);
            }
            else if (!GenerateActionCommon.TryResolveProjectPath(path, "asset", out absolute, out _)) throw new InvalidOperationException("资产路径无效。");
            return FileFragmentMaintenance.Hash(absolute, token);
        }

        /// <summary>对外只报告完整集合与保存证据，成功范围为本地维护，设备耐久性仍独立验收。</summary>
        internal static AgentActionResult Result(Journal journal, string status, string message)
        {
            AgentActionResult result = AgentActionResult.Create(null, status, message);
            result.ReceiptJson = JsonConvert.SerializeObject(journal);
            result.DataJson = JsonConvert.SerializeObject(new { journal.operationId, journal.phase, journal.storage, journal.target,
                journal.beforeAES, journal.afterAES, journal.beforeSha256, journal.afterSha256,
                fileCount = journal.entries?.Length ?? 0, itemCount = journal.entries?.Where(entry => entry.name.EndsWith(".dat", StringComparison.Ordinal)).Sum(entry => entry.itemCount) ?? 0,
                retainedRecoveryDirectory = Directory.Exists(OperationDirectory(journal.operationId)) ?
                    "Library/Nova/AgentActions/PersistenceMaintenance/" + journal.operationId : null });
            result.EvidenceKinds = AgentActionEvidence.Static | (status == "success" ? AgentActionEvidence.Artifact : AgentActionEvidence.None);
            result.Evidence.Add("严格复用 FileFragment 编码，全体文件/备份与唯一资产状态独立核对；不代表 Player/设备重启耐久性。");
            return result;
        }
    }
}
