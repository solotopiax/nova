/***************************************************************
 * (c) copyright 2026 - 2030, Solotopia
 * All Rights Reserved.
 * -------------------------------------------------------------
 * filename:  SerializedTargetEditing.cs
 * author:    taoye
 * created:   2026/10/13
 * descrip:   项目自有 Prefab 组件和普通配置资产的受控序列化编辑
 ***************************************************************/

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Threading;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using NovaFramework.Runtime;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using Object = UnityEngine.Object;
using Path = System.IO.Path;
using PackageInfo = UnityEditor.PackageManager.PackageInfo;

namespace NovaFramework.Editor
{
    /// <summary>只编辑已存在的项目对象；预演只使用待应用缓存，禁止创建对象、执行方法和改写生成配置。</summary>
    internal static class SerializedTargetEditing
    {
        /// <summary>由真实资产和对象标识定位目标；Prefab 组件必须同时提供精确 GlobalObjectId。</summary>
        [Serializable]
        internal sealed class Target
        {
            [AgentActionRequired] public string targetGuid;
            public string globalObjectId;
        }

        /// <summary>精确字段路径及一个严格 JSON 值；省略的字段保留，JSON null 仅用于允许清空的对象引用。</summary>
        [Serializable]
        internal sealed class Edit
        {
            [AgentActionRequired] public string propertyPath;
            [AgentActionRequired] public string valueJson;
        }

        /// <summary>字段写集只记录真实路径和类型，不输出输入值、旧值或逐字段秘密摘要。</summary>
        [Serializable]
        internal sealed class Change
        {
            public string propertyPath;
            public string propertyType;
            public string type;
            public bool sensitive;
        }

        /// <summary>只读结果来自当前 Unity 对象；字段默认值未从声明核实时保持 unknown。</summary>
        [Serializable]
        internal sealed class Inspection
        {
            public string targetGuid;
            public string globalObjectId;
            public string assetPath;
            public string targetType;
            public string scriptPath;
            public string scriptGuid;
            public bool editable;
            public string blockedReason;
            public string fileSha256;
            public string stateSha256;
            public SerializedPropertyValues.FieldView[] fields;
        }

        /// <summary>跨重载只保留身份、整个资产摘要和字段写集；不持久化敏感请求值，也不支持重放。</summary>
        [Serializable]
        internal sealed class Receipt
        {
            public int schemaVersion = 1;
            public Target target;
            public string assetPath;
            public string targetType;
            public string scriptGuid;
            public string scriptPath;
            public string scriptSha256;
            public string metaSha256;
            public string plannedFileSha256;
            public string beforeStateSha256;
            public string expectedStateSha256;
            public string observedFileSha256;
            public bool executionStarted;
            public Change[] changes;
        }

        /// <summary>单次计划仅在当前 Editor 进程保存请求值，进入 Execute 前必须再次核对全部指纹。</summary>
        internal sealed class Plan
        {
            internal Receipt Receipt;
            internal Edit[] Edits;
            internal int Consumed;
            internal EditIntent Intent;
        }

        /// <summary>只在内部内存计划中携带领域意图；不进入请求或 Receipt，不能由外部授权开关伪造。</summary>
        internal enum EditIntent { Generic, PersistenceAES, PersistenceCipher }

        /// <summary>真实已加载资产的定位结果；不加载 Prefab 内容到临时场景，也不实例化业务对象。</summary>
        private sealed class Resolved
        {
            internal Target Identity;
            internal Object Value;
            internal Object Main;
            internal Object[] AssetObjects;
            internal string Path;
            internal string AbsolutePath;
            internal MonoScript Script;
            internal string ScriptPath;
            internal string ScriptAbsolute;
        }

        /// <summary>验证目标身份的静态格式；对象归属和精确类型在真实资产加载后再判断。</summary>
        internal static bool ValidateTarget(Target target, out string error)
        {
            error = null;
            if (target == null || !IsGuid(target.targetGuid) ||
                target.globalObjectId != null && (target.globalObjectId.Length > 256 ||
                    !GlobalObjectId.TryParse(target.globalObjectId, out _)))
            {
                error = "targetGuid 必须是 Unity GUID；globalObjectId 若提供必须是当前对象的精确标识。";
                return false;
            }
            return true;
        }

        /// <summary>校验 1 至 64 个非重叠字段和严格 JSON；语法解析不调用 Unity 赋值或任意业务代码。</summary>
        internal static bool ValidateEdits(Edit[] edits, out string error)
        {
            error = null;
            if (edits == null || edits.Length == 0 || edits.Length > 64)
            {
                error = "patches 必须包含 1 至 64 个明确字段。";
                return false;
            }
            try
            {
                var paths = new List<string>();
                foreach (Edit edit in edits)
                {
                    if (edit == null || string.IsNullOrWhiteSpace(edit.propertyPath) || edit.propertyPath.Length > 512 ||
                        edit.propertyPath != edit.propertyPath.Trim() || edit.propertyPath.IndexOf('\0') >= 0 ||
                        paths.Any(path => path == edit.propertyPath || path.StartsWith(edit.propertyPath + ".", StringComparison.Ordinal) ||
                            edit.propertyPath.StartsWith(path + ".", StringComparison.Ordinal)))
                        throw new InvalidOperationException();
                    SerializedPropertyValues.Parse(edit.valueJson);
                    paths.Add(edit.propertyPath);
                }
                return true;
            }
            catch
            {
                error = "字段路径重复、重叠或无效，或 valueJson 不是一个受支持的严格 JSON 值。";
                return false;
            }
        }

        /// <summary>读取现存对象的可见字段、真实类型和 tooltip；检查结果不表示默认值、场景或业务运行已验证。</summary>
        internal static Inspection Inspect(Target identity, CancellationToken cancellationToken)
        {
            Resolved target = Resolve(identity, false);
            using (var serialized = new SerializedObject(target.Value))
            {
                serialized.Update();
                string reason = WriteBlockReason(target);
                return new Inspection
                {
                    targetGuid = target.Identity.targetGuid, globalObjectId = target.Identity.globalObjectId,
                    assetPath = target.Path, targetType = target.Value.GetType().FullName,
                    scriptPath = target.ScriptPath, scriptGuid = AssetDatabase.AssetPathToGUID(target.ScriptPath),
                    editable = reason == null, blockedReason = reason,
                    fileSha256 = FileHash(target.AbsolutePath, cancellationToken),
                    stateSha256 = AssetStateHash(target, null), fields = Describe(serialized, reason == null),
                };
            }
        }

        /// <summary>在真实对象的 SerializedObject 待应用缓存上预演并 Update 丢弃；全过程不 Apply、不保存、不创建 clone。</summary>
        internal static Plan BuildPlan(Target identity, Edit[] edits, CancellationToken cancellationToken)
            => BuildPlan(identity, edits, cancellationToken, EditIntent.Generic);

        /// <summary>仅供存档迁移事务构造 FileFragment AES 单字段计划，不接收任意路径或领域授权值。</summary>
        internal static Plan BuildPersistenceAESPlan(Target identity, bool enableAES, CancellationToken cancellationToken) =>
            BuildPlan(identity, new[] { new Edit { propertyPath = "m_UseAESForFileFragment", valueJson = enableAES ? "true" : "false" } },
                cancellationToken, EditIntent.PersistenceAES);

        /// <summary>仅供已冻结的 PlayerPrefs/SQLite 值维护构造对应单布尔字段计划，不开放密码或任意路径。</summary>
        /// <param name="identity">唯一 PersistComponent 身份。</param>
        /// <param name="storage">精确 PlayerPrefs 或 SQLite。</param>
        /// <param name="enableAES">完成数据维护后保存的状态。</param>
        /// <param name="cancellationToken">提交前取消。</param>
        /// <returns>对应存储 AES 单字段计划。</returns>
        internal static Plan BuildPersistenceValueAESPlan(Target identity, string storage, bool enableAES, CancellationToken cancellationToken)
        {
            string path = storage == "PlayerPrefs" ? "m_UseAESForPlayerPrefs" : storage == "SQLite" ? "m_UseAESForSQLite" :
                throw new InvalidOperationException("值维护存储无效。");
            return BuildPlan(identity, new[] { new Edit { propertyPath = path, valueJson = enableAES ? "true" : "false" } },
                cancellationToken, EditIntent.PersistenceAES);
        }

        /// <summary>仅供 Cipher 维护事务预演唯一密码字段；秘密只留在内存 Edits，不进入 Receipt。</summary>
        internal static Plan BuildPersistenceCipherPlan(Target identity, string password, CancellationToken cancellationToken) =>
            BuildPlan(identity, new[] { new Edit { propertyPath = "m_SQLiteCipherPassword", valueJson = JsonConvert.SerializeObject(password) } },
                cancellationToken, EditIntent.PersistenceCipher);

        /// <summary>仅已绑定原迁移 token 的 Cipher 服务调用：脏内存必须完整原/新状态，磁盘须本操作已知完整文件；其它草稿门保持。</summary>
        internal static string AssertCipherRecoveryTarget(Receipt before, Receipt after, Receipt saved, string oldPassword, string newPassword, Func<bool> diskMatchesOrigin,
            CancellationToken cancellationToken)
        {
            Resolved target = Resolve(before.target, false);
            AssertIdentity(target, before, cancellationToken);
            if (target.Value.GetType() != typeof(PersistComponent) || after == null ||
                WriteBlockReason(target, true) != null) throw new InvalidOperationException("Cipher 恢复目标安全条件不满足。");
            string state = AssetStateHash(target, null);
            if (state != before.beforeStateSha256 && state != after.expectedStateSha256)
                throw new InvalidOperationException("Cipher 脏状态不是本操作完整原/新状态。");
            string file = FileHash(target.AbsolutePath, cancellationToken);
            if (diskMatchesOrigin == null || !diskMatchesOrigin())
                throw new InvalidOperationException("Cipher 目标磁盘文件不是本操作完整原/新语义。");
            using var serialized = new SerializedObject(target.Value);
            serialized.Update();
            string current = serialized.FindProperty("m_SQLiteCipherPassword").stringValue;
            if (current != oldPassword && current != newPassword) throw new InvalidOperationException("Cipher 当前字段不是封存旧/新凭据。");
            try
            {
                Stage(serialized, new[] { new Edit { propertyPath = "m_SQLiteCipherPassword",
                    valueJson = JsonConvert.SerializeObject(current == oldPassword ? newPassword : oldPassword) } }, EditIntent.PersistenceCipher);
                if (AssetStateHash(target, serialized) != (current == oldPassword ? after.expectedStateSha256 : before.beforeStateSha256))
                    throw new InvalidOperationException("Cipher 字段与日志完整状态未绑定。");
            }
            finally { serialized.Update(); }
            return current;
        }

        /// <summary>仅本操作已知完整原/新内存及磁盘恢复旧密码；允许该单字段 Apply 后保存失败留下的脏状态，不放宽 Generic 门。</summary>
        internal static AgentActionResult RestoreCipherRecoveryTarget(Receipt before, Receipt after, Receipt saved, string oldPassword,
            string newPassword, Func<bool> diskMatchesOrigin, CancellationToken cancellationToken)
        {
            AssertCipherRecoveryTarget(before, after, saved, oldPassword, newPassword, diskMatchesOrigin, cancellationToken);
            Resolved target = Resolve(before.target, false);
            string file = FileHash(target.AbsolutePath, cancellationToken);
            string state = AssetStateHash(target, null);
            var receipt = new Receipt { target = before.target, assetPath = before.assetPath, targetType = before.targetType,
                scriptGuid = before.scriptGuid, scriptPath = before.scriptPath, scriptSha256 = before.scriptSha256, metaSha256 = before.metaSha256,
                plannedFileSha256 = file, beforeStateSha256 = state, expectedStateSha256 = before.beforeStateSha256,
                changes = Array.Empty<Change>() };
            using (var serialized = new SerializedObject(target.Value))
            {
                serialized.Update();
                try
                {
                    receipt.changes = Stage(serialized, new[] { new Edit { propertyPath = "m_SQLiteCipherPassword",
                        valueJson = JsonConvert.SerializeObject(oldPassword) } }, EditIntent.PersistenceCipher);
                    if (AssetStateHash(target, serialized) != before.beforeStateSha256 || WriteBlockReason(target, true) != null ||
                        FileHash(target.AbsolutePath, cancellationToken) != file || AssetStateHash(target, null) != state)
                        throw new InvalidOperationException("Cipher 恢复提交前状态变化。");
                    cancellationToken.ThrowIfCancellationRequested(); receipt.executionStarted = true;
                    if (receipt.changes.Length != 0 && !serialized.ApplyModifiedProperties()) throw new InvalidOperationException("Cipher 恢复字段未应用。");
                    if (receipt.changes.Length != 0 || target.AssetObjects.Any(EditorUtility.IsDirty))
                    {
                        EditorUtility.SetDirty(target.Value);
                        PrefabUtility.SavePrefabAsset((GameObject)target.Main, out bool succeeded);
                        if (!succeeded) throw new InvalidOperationException("Cipher 恢复 Prefab 保存失败。");
                    }
                }
                finally { serialized.Update(); }
            }
            receipt.observedFileSha256 = FileHash(target.AbsolutePath, CancellationToken.None);
            AssertCipherRecoveryTarget(before, after, receipt, oldPassword, newPassword, diskMatchesOrigin, CancellationToken.None);
            if (AssetStateHash(target, null) != before.beforeStateSha256 || target.AssetObjects.Any(EditorUtility.IsDirty))
                throw new InvalidOperationException("Cipher 恢复保存后完整状态或脏态不符。");
            // 已知本操作脏态即使字段已旧也可能需要实际保存；不复用 Generic changes=0 的未保存假设。
            return Result(receipt, "success", "Cipher 原字段与完整资产已保存并核验。");
        }

        /// <summary>已绑定原token的存档AES恢复只接受指定存储单布尔字段，以及完整原/新内存和受保护原磁盘来源；其它安全门保持。</summary>
        internal static bool AssertAesRecoveryTarget(Receipt origin, string storage, bool beforeAES, bool afterAES,
            Func<bool> diskMatchesOrigin, CancellationToken cancellationToken)
        {
            Resolved target = Resolve(origin.target, false);
            AssertIdentity(target, origin, cancellationToken);
            if (target.Value.GetType() != typeof(PersistComponent) || WriteBlockReason(target, true) != null)
                throw new InvalidOperationException("AES恢复目标安全条件不满足。");
            string state = AssetStateHash(target, null);
            if (state != origin.beforeStateSha256 && state != origin.expectedStateSha256 || diskMatchesOrigin == null || !diskMatchesOrigin())
                throw new InvalidOperationException("AES恢复不是本操作完整原/新内存及磁盘来源。");
            string field = AesRecoveryField(storage);
            using var serialized = new SerializedObject(target.Value);
            serialized.Update();
            bool current = serialized.FindProperty(field).boolValue;
            if (state != (current == beforeAES ? origin.beforeStateSha256 : origin.expectedStateSha256) ||
                current != beforeAES && current != afterAES)
                throw new InvalidOperationException("AES字段与完整状态不符。");
            try
            {
                Stage(serialized, new[] { new Edit { propertyPath = field,
                    valueJson = current == beforeAES ? (afterAES ? "true" : "false") : (beforeAES ? "true" : "false") } }, EditIntent.PersistenceAES);
                if (AssetStateHash(target, serialized) != (current == beforeAES ? origin.expectedStateSha256 : origin.beforeStateSha256))
                    throw new InvalidOperationException("AES恢复单字段与原token完整状态未绑定。");
            }
            finally { serialized.Update(); }
            return current;
        }

        /// <summary>仅固定三种存储映射其唯一AES布尔字段，不接受任意propertyPath。</summary>
        private static string AesRecoveryField(string storage) => storage == "FileFragment" ? "m_UseAESForFileFragment" :
            storage == "PlayerPrefs" ? "m_UseAESForPlayerPrefs" : storage == "SQLite" ? "m_UseAESForSQLite" :
            throw new InvalidOperationException("AES恢复存储无效。");

        /// <summary>本操作完整原/新状态通过后恢复对应存储旧布尔值；字段已旧但owned dirty也实际保存，Generic规则不变。</summary>
        internal static AgentActionResult RestoreAesRecoveryTarget(Receipt origin, string storage, bool beforeAES, bool afterAES,
            Func<bool> diskMatchesOrigin, CancellationToken cancellationToken)
        {
            AssertAesRecoveryTarget(origin, storage, beforeAES, afterAES, diskMatchesOrigin, cancellationToken);
            Resolved target = Resolve(origin.target, false);
            string file = FileHash(target.AbsolutePath, cancellationToken), state = AssetStateHash(target, null);
            var receipt = new Receipt { target = origin.target, assetPath = origin.assetPath, targetType = origin.targetType,
                scriptGuid = origin.scriptGuid, scriptPath = origin.scriptPath, scriptSha256 = origin.scriptSha256, metaSha256 = origin.metaSha256,
                plannedFileSha256 = file, beforeStateSha256 = state, expectedStateSha256 = origin.beforeStateSha256, changes = Array.Empty<Change>() };
            using (var serialized = new SerializedObject(target.Value))
            {
                serialized.Update();
                try
                {
                    receipt.changes = Stage(serialized, new[] { new Edit { propertyPath = AesRecoveryField(storage),
                        valueJson = beforeAES ? "true" : "false" } }, EditIntent.PersistenceAES);
                    if (AssetStateHash(target, serialized) != origin.beforeStateSha256 || WriteBlockReason(target, true) != null ||
                        FileHash(target.AbsolutePath, cancellationToken) != file || AssetStateHash(target, null) != state || !diskMatchesOrigin())
                        throw new InvalidOperationException("AES恢复提交前来源变化。");
                    cancellationToken.ThrowIfCancellationRequested(); receipt.executionStarted = true;
                    if (receipt.changes.Length != 0 && !serialized.ApplyModifiedProperties()) throw new InvalidOperationException("AES恢复字段未应用。");
                    if (receipt.changes.Length != 0 || target.AssetObjects.Any(EditorUtility.IsDirty))
                    {
                        EditorUtility.SetDirty(target.Value);
#if UNITY_INCLUDE_TESTS
                        PersistenceMaintenance.ProbeAesFieldSaveFixture(origin.target);
#endif
                        PrefabUtility.SavePrefabAsset((GameObject)target.Main, out bool succeeded);
                        if (!succeeded) throw new InvalidOperationException("AES恢复Prefab保存失败。");
                    }
                }
                finally { serialized.Update(); }
            }
            receipt.observedFileSha256 = FileHash(target.AbsolutePath, CancellationToken.None);
            AssertAesRecoveryTarget(origin, storage, beforeAES, afterAES, diskMatchesOrigin, CancellationToken.None);
            if (AssetStateHash(target, null) != origin.beforeStateSha256 || target.AssetObjects.Any(EditorUtility.IsDirty))
                throw new InvalidOperationException("AES恢复保存后完整状态或脏态不符。");
            return Result(receipt, "success", "原AES字段与完整资产已保存并核验。");
        }

        /// <summary>复用完整冻结与待应用预演；领域意图从内部入口冻结，不参与外部 Receipt 恢复权限。</summary>
        private static Plan BuildPlan(Target identity, Edit[] edits, CancellationToken cancellationToken, EditIntent intent)
        {
            if (!ValidateEdits(edits, out string error)) throw new InvalidOperationException(error);
            Resolved target = Resolve(identity, true);
            string fileHash = FileHash(target.AbsolutePath, cancellationToken);
            string before = AssetStateHash(target, null);
            Change[] changes;
            string expected;
            using (var serialized = new SerializedObject(target.Value))
            {
                serialized.Update();
                try
                {
                    changes = Stage(serialized, edits, intent);
                    expected = AssetStateHash(target, serialized);
                }
                finally { serialized.Update(); }
            }
            if (AssetStateHash(target, null) != before || EditorUtility.IsDirty(target.Value) ||
                FileHash(target.AbsolutePath, cancellationToken) != fileHash)
                throw new InvalidOperationException("预演期间目标资产发生变化，请重新计划。");
            return new Plan
            {
                Edits = edits.Select(edit => new Edit { propertyPath = edit.propertyPath, valueJson = edit.valueJson }).ToArray(),
                Intent = intent,
                Receipt = new Receipt
                {
                    target = target.Identity, assetPath = target.Path, targetType = target.Value.GetType().FullName,
                    scriptGuid = AssetDatabase.AssetPathToGUID(target.ScriptPath), scriptPath = target.ScriptPath,
                    scriptSha256 = FileHash(target.ScriptAbsolute, cancellationToken),
                    metaSha256 = FileHash(target.AbsolutePath + ".meta", cancellationToken),
                    plannedFileSha256 = fileHash, beforeStateSha256 = before,
                    expectedStateSha256 = expected, changes = changes,
                },
            };
        }

        /// <summary>单次消费后重建同一预演，复核身份、文件、草稿和精确写集；只保存所属 Prefab 或单个 SO。</summary>
        internal static AgentActionResult Execute(Plan plan, CancellationToken cancellationToken)
        {
            if (plan?.Receipt == null || plan.Edits == null || Interlocked.Exchange(ref plan.Consumed, 1) != 0)
                return AgentActionResult.Create(null, "blocked", "字段编辑计划已失效或已使用，请重新 Plan。");
            Receipt receipt = plan.Receipt;
            bool writeStarted = false;
            try
            {
                Resolved target = Resolve(receipt.target, true);
                AssertIdentity(target, receipt, cancellationToken);
                if (FileHash(target.AbsolutePath, cancellationToken) != receipt.plannedFileSha256 ||
                    AssetStateHash(target, null) != receipt.beforeStateSha256)
                    throw new InvalidOperationException("资产文件或完整序列化状态已变化。");
                using (var serialized = new SerializedObject(target.Value))
                {
                    serialized.Update();
                    try
                    {
                        Change[] staged = Stage(serialized, plan.Edits, plan.Intent);
                        if (AssetStateHash(target, serialized) != receipt.expectedStateSha256 ||
                            JsonConvert.SerializeObject(staged) != JsonConvert.SerializeObject(receipt.changes) ||
                            WriteBlockReason(target) != null || AssetStateHash(target, null) != receipt.beforeStateSha256 ||
                            FileHash(target.AbsolutePath, cancellationToken) != receipt.plannedFileSha256)
                            throw new InvalidOperationException("提交前目标、草稿或写集发生变化。");
                        cancellationToken.ThrowIfCancellationRequested();
                        receipt.executionStarted = true;
                        if (staged.Length > 0)
                        {
                            // Apply 之后发生的失败保留 partial，不回滚用户资产，不自动重放。
                            writeStarted = true;
                            if (!serialized.ApplyModifiedProperties()) throw new InvalidOperationException("未应用计划字段。");
                            EditorUtility.SetDirty(target.Value);
#if UNITY_INCLUDE_TESTS
                            if (plan.Intent == EditIntent.PersistenceCipher) PersistenceMaintenance.ProbeCipherFieldSaveFixture();
                            if (plan.Intent == EditIntent.PersistenceAES) PersistenceMaintenance.ProbeAesFieldSaveFixture(receipt.target);
#endif
                            if (target.Main is GameObject prefab)
                            {
                                PrefabUtility.SavePrefabAsset(prefab, out bool saved);
                                if (!saved) throw new InvalidOperationException("Prefab 保存失败。");
                            }
                            else AssetDatabase.SaveAssetIfDirty(target.Value);
                        }
                    }
                    finally { serialized.Update(); }
                }
                receipt.observedFileSha256 = FileHash(target.AbsolutePath, CancellationToken.None);
                return VerifyReceipt(receipt, CancellationToken.None);
            }
            catch (OperationCanceledException) when (!writeStarted) { throw; }
            catch
            {
                return Result(receipt, writeStarted ? "partial" : "blocked", writeStarted ?
                    "已开始字段写入，但保存或完整状态未通过复核；请只读 Verify，不要重放 Execute。" :
                    "目标身份、文件、草稿或字段预演未通过复核，未执行资产写入。");
            }
        }

        /// <summary>重新解析 Receipt 并只读核对保存后的整个资产状态；没有实际执行证据时拒绝验证成功。</summary>
        internal static AgentActionResult Verify(string receiptJson, CancellationToken cancellationToken)
        {
            try { return VerifyReceipt(JsonConvert.DeserializeObject<Receipt>(receiptJson), cancellationToken); }
            catch (OperationCanceledException) { throw; }
            catch { return AgentActionResult.Create(null, "blocked", "字段编辑 Receipt 无法解析或读取，未执行写入。"); }
        }

        /// <summary>验证已执行 Receipt 的身份、源脚本、meta、保存文件及全部对象状态，不读取请求值或再次 Apply。</summary>
        private static AgentActionResult VerifyReceipt(Receipt receipt, CancellationToken cancellationToken)
        {
            if (receipt == null || receipt.schemaVersion != 1 || !receipt.executionStarted || receipt.changes == null ||
                !IsHash(receipt.expectedStateSha256) || !IsHash(receipt.observedFileSha256))
                return Result(receipt, "blocked", "Receipt 缺少有效执行或保存证据；不支持恢复写入。");
            Resolved target = Resolve(receipt.target, true);
            AssertIdentity(target, receipt, cancellationToken);
            bool matches = AssetStateHash(target, null) == receipt.expectedStateSha256 &&
                FileHash(target.AbsolutePath, cancellationToken) == receipt.observedFileSha256 &&
                (receipt.changes.Length != 0 || receipt.observedFileSha256 == receipt.plannedFileSha256);
            return Result(receipt, matches ? "success" : "partial", matches ?
                (receipt.changes.Length == 0 ? "目标字段已经满足，未保存资产。" : "目标字段已保存，完整资产状态与 Receipt 一致。") :
                "当前文件或完整序列化状态与保存收据不同，请查明变化后重新 Plan。");
        }

        /// <summary>定位 Assets 内现存 Prefab 资产组件或主 ScriptableObject，拒绝场景、生成配置及包内资产。</summary>
        private static Resolved Resolve(Target identity, bool forWrite)
        {
            if (!ValidateTarget(identity, out string error)) throw new InvalidOperationException(error);
            string path = AssetDatabase.GUIDToAssetPath(identity.targetGuid);
            if (!OwnedPath(path) || !GenerateActionCommon.TryResolveProjectPath(path, "target", out string absolute, out _) ||
                !File.Exists(absolute) || !File.Exists(absolute + ".meta"))
                throw new InvalidOperationException("目标必须是 Assets 内真实项目自有文件，不能是包、vendor、生成物或符号链接。");
            Object main = AssetDatabase.LoadMainAssetAtPath(path);
            Object value;
            Object[] objects;
            if (main is GameObject prefab && path.EndsWith(".prefab", StringComparison.OrdinalIgnoreCase))
            {
                if (PrefabUtility.GetPrefabAssetType(prefab) != PrefabAssetType.Regular ||
                    string.IsNullOrEmpty(identity.globalObjectId) || !GlobalObjectId.TryParse(identity.globalObjectId, out GlobalObjectId id))
                    throw new InvalidOperationException("只支持普通 Prefab 资产中提供精确 GlobalObjectId 的组件，不支持变体或场景对象。");
                value = GlobalObjectId.GlobalObjectIdentifierToObjectSlow(id);
                if (!(value is MonoBehaviour component) || !EditorUtility.IsPersistent(component) ||
                    AssetDatabase.GetAssetPath(component) != path || component.transform.root.gameObject != prefab)
                    throw new InvalidOperationException("GlobalObjectId 未指向该 Prefab 内精确的自有 MonoBehaviour。");
                objects = prefab.GetComponentsInChildren<Transform>(true).SelectMany(transform =>
                    new[] { (Object)transform.gameObject }.Concat(transform.GetComponents<Component>().Cast<Object>())).ToArray();
                if (objects.Any(item => item == null)) throw new InvalidOperationException("Prefab 含 Missing Script，无法冻结完整状态。");
                if (prefab.GetComponentsInChildren<Transform>(true).Any(transform => PrefabUtility.IsAnyPrefabInstanceRoot(transform.gameObject)))
                    throw new InvalidOperationException("嵌套 Prefab 尚无完整保存契约，请使用专用 Prefab 编辑能力。");
            }
            else if (main is ScriptableObject && path.EndsWith(".asset", StringComparison.OrdinalIgnoreCase))
            {
                value = main;
                objects = AssetDatabase.LoadAllAssetsAtPath(path);
                if (objects.Length != 1) throw new InvalidOperationException("只支持单对象普通 SO，不编辑子资产容器。");
                if (!string.IsNullOrEmpty(identity.globalObjectId) &&
                    GlobalObjectId.GetGlobalObjectIdSlow(value).ToString() != identity.globalObjectId)
                    throw new InvalidOperationException("SO 对象身份与请求不一致。");
            }
            else throw new InvalidOperationException("场景、内置组件和其它资产类型尚无此 Action 的安全保存契约。");
            if (value is ConfigMasterSO || value is ConfigRuntimeSO || value is PipifySettingsSO)
                throw new InvalidOperationException("ConfigMasterSO、ConfigRuntimeSO 与 PipifySettingsSO 必须走各自专用编辑链。");
            MonoScript script = value is MonoBehaviour behaviour ? MonoScript.FromMonoBehaviour(behaviour) :
                MonoScript.FromScriptableObject((ScriptableObject)value);
            string scriptPath = script == null ? null : AssetDatabase.GetAssetPath(script);
            if (script == null || script.GetClass() != value.GetType() || !TryResolveOwnedScript(scriptPath, out string scriptAbsolute))
                throw new InvalidOperationException("目标实际 MonoScript 必须来自项目或已解析 Nova 包的自有 Scripts，不能通过类型名推断归属。");
            var target = new Resolved
            {
                Identity = new Target { targetGuid = identity.targetGuid.ToLowerInvariant(), globalObjectId = GlobalObjectId.GetGlobalObjectIdSlow(value).ToString() },
                Value = value, Main = main, AssetObjects = objects, Path = path, AbsolutePath = absolute,
                Script = script, ScriptPath = scriptPath, ScriptAbsolute = scriptAbsolute,
            };
            if (forWrite)
            {
                string reason = WriteBlockReason(target);
                if (reason != null) throw new InvalidOperationException(reason);
            }
            return target;
        }

        /// <summary>默认拒绝只读、全资产脏态、PrefabStage和回调草稿；存档AES/Cipher专用恢复仅在外层完整原/新状态与文件核验后允许本操作脏态。</summary>
        private static string WriteBlockReason(Resolved target, bool allowPersistenceOwnedDirty = false)
        {
            if ((File.GetAttributes(target.AbsolutePath) & FileAttributes.ReadOnly) != 0 ||
                !AssetDatabase.IsOpenForEdit(target.Path)) return "目标资产不可写。";
            if (!allowPersistenceOwnedDirty && target.AssetObjects.Any(EditorUtility.IsDirty)) return "目标所属资产存在未保存改动，请先保存或处理草稿。";
            var stage = PrefabStageUtility.GetCurrentPrefabStage();
            if (stage != null && stage.assetPath == target.Path) return "目标正在 PrefabStage 编辑，请先完成并关闭该草稿。";
            Type type = target.Value.GetType();
            if (typeof(ISerializationCallbackReceiver).IsAssignableFrom(type) ||
                type.IsDefined(typeof(ExecuteAlways), true) || type.IsDefined(typeof(ExecuteInEditMode), true))
                return "目标具有编辑期生命周期或序列化回调，通用编辑不能证明精确写集，请使用领域能力。";
            for (Type current = type; current != null && current != typeof(MonoBehaviour) && current != typeof(ScriptableObject); current = current.BaseType)
                if (current.GetMethods(BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.DeclaredOnly)
                    .Any(method => method.Name == "OnValidate")) return "目标具有 OnValidate 回调，通用编辑不能安全预演。";
            // 只检查现存 Editor，不创建 Inspector；自定义 Editor 的隐藏工作副本没有统一草稿契约。
            foreach (UnityEditor.Editor editor in Resources.FindObjectsOfTypeAll<UnityEditor.Editor>())
            {
                if (editor == null || editor.targets == null || !editor.targets.Any(item => target.AssetObjects.Contains(item))) continue;
                if (editor.GetType().Assembly != typeof(UnityEditor.Editor).Assembly)
                    return "目标存在打开的自定义 Inspector，无法核实其私有草稿，请先关闭目标 Inspector。";
                if (editor.serializedObject != null && editor.serializedObject.hasModifiedProperties)
                    return "目标 Inspector 存在尚未应用的字段草稿。";
            }
            return null;
        }

        /// <summary>用真实可见全路径建立字段描述；父结构包含敏感子字段时也抑制聚合值输出。</summary>
        private static SerializedPropertyValues.FieldView[] Describe(SerializedObject serialized, bool writable)
        {
            var fields = new List<SerializedPropertyValues.FieldView>();
            SerializedProperty cursor = serialized.GetIterator();
            while (cursor.NextVisible(true))
            {
                SerializedPropertyValues.FieldView field = SerializedPropertyValues.Describe(cursor)[0];
                field.path = cursor.propertyPath;
                field.editable &= writable && !EventPath(cursor) && !SystemProperty(cursor) &&
                    !cursor.propertyPath.EndsWith(".Array.size", StringComparison.Ordinal);
                if (PersistenceEncryptionPath(cursor))
                {
                    field.editable = false;
                    field.tooltip = (field.tooltip ?? string.Empty) + " 存储 AES 必须通过配套存档迁移事务维护；通用字段编辑不会迁移存量数据。";
                }
                JToken value = ReadTree(cursor);
                field.hasValue = value != null && value.Type != JTokenType.Null &&
                    !(value.Type == JTokenType.String && string.IsNullOrEmpty(value.Value<string>()));
                field.valueJson = field.sensitive || value == null ? null : value.ToString(Formatting.None);
                fields.Add(field);
                if (fields.Count > 4096) throw new InvalidOperationException("目标字段过多，需缩小领域检查范围。");
            }
            foreach (SerializedPropertyValues.FieldView field in fields)
                if (fields.Any(child => child.sensitive && child.path.StartsWith(field.path + ".", StringComparison.Ordinal)))
                {
                    field.sensitive = true;
                    field.valueJson = null;
                }
            return fields.ToArray();
        }

        /// <summary>递归读取已有列表和直接结构子字段；遇到敏感路径不返回聚合对象值，动态管理引用只读已存在字段。</summary>
        private static JToken ReadTree(SerializedProperty property)
        {
            if (property.isArray && property.propertyType != SerializedPropertyType.String)
            {
                if (property.arraySize > 1024) return null;
                var array = new JArray();
                for (int index = 0; index < property.arraySize; index++)
                {
                    SerializedProperty element = property.GetArrayElementAtIndex(index);
                    if (SerializedPropertyValues.IsSensitive(element.propertyPath)) return null;
                    JToken value = ReadTree(element);
                    if (value == null) return null;
                    array.Add(value);
                }
                return array;
            }
            if (property.propertyType == SerializedPropertyType.Generic || property.propertyType == SerializedPropertyType.ManagedReference)
            {
                var obj = new JObject();
                SerializedProperty cursor = property.Copy();
                SerializedProperty end = property.GetEndProperty();
                if (cursor.NextVisible(true)) do
                {
                    if (SerializedProperty.EqualContents(cursor, end)) break;
                    if (cursor.depth != property.depth + 1) continue;
                    if (SerializedPropertyValues.IsSensitive(cursor.propertyPath)) return null;
                    JToken value = ReadTree(cursor);
                    if (value == null) return null;
                    obj[cursor.name] = value;
                }
                while (cursor.NextVisible(false));
                return obj;
            }
            return SerializedPropertyValues.Read(property);
        }

        /// <summary>只允许此前可见的精确路径，强类型赋值后记录真正改变的字段；数组/结构由共享转换器递归校验。</summary>
        private static Change[] Stage(SerializedObject serialized, Edit[] edits, EditIntent intent)
        {
            ValidateIntent(serialized, edits, intent);
            var visible = new HashSet<string>(Describe(serialized, true).Select(field => field.path), StringComparer.Ordinal);
            var changes = new List<Change>();
            foreach (Edit edit in edits)
            {
                if (!visible.Contains(edit.propertyPath)) throw new InvalidOperationException("字段不在真实可见 Inspector 路径中。");
                SerializedProperty property = serialized.FindProperty(edit.propertyPath);
                if (intent == EditIntent.Generic && PersistenceEncryptionPath(property))
                    throw new InvalidOperationException("存储 AES 字段及包含它的结构必须走存档迁移领域入口。");
                if (property == null || !SerializedPropertyValues.CanWrite(property) || EventPath(property) ||
                    SystemProperty(property) || property.propertyPath.EndsWith(".Array.size", StringComparison.Ordinal))
                    throw new InvalidOperationException("字段没有受控写入转换，或属于 UnityEvent 持久回调。");
                string before = property.contentHash.ToString();
                SerializedPropertyValues.Set(property, SerializedPropertyValues.Parse(edit.valueJson));
                if (property.contentHash.ToString() != before)
                    changes.Add(new Change { propertyPath = edit.propertyPath, propertyType = property.propertyType.ToString(),
                        type = property.type, sensitive = SerializedPropertyValues.IsSensitive(edit.propertyPath) });
            }
            return changes.OrderBy(change => change.propertyPath, StringComparer.Ordinal).ToArray();
        }

        /// <summary>专用意图仅接受真实 PersistComponent 的一个存储 AES 布尔字段，提交前重新核验。</summary>
        private static void ValidateIntent(SerializedObject serialized, Edit[] edits, EditIntent intent)
        {
            if (intent == EditIntent.Generic) return;
            if (intent == EditIntent.PersistenceCipher)
            {
                if (serialized.targetObject == null || serialized.targetObject.GetType() != typeof(PersistComponent) ||
                    edits == null || edits.Length != 1 || edits[0]?.propertyPath != "m_SQLiteCipherPassword" ||
                    serialized.FindProperty(edits[0].propertyPath)?.propertyType != SerializedPropertyType.String ||
                    SerializedPropertyValues.Parse(edits[0].valueJson).Type != JTokenType.String)
                    throw new InvalidOperationException("Cipher 领域编辑仅允许唯一实际密码字段。");
                return;
            }
            if (intent != EditIntent.PersistenceAES || serialized.targetObject == null || serialized.targetObject.GetType() != typeof(PersistComponent) ||
                edits == null || edits.Length != 1 || edits[0] == null || !new[] { "m_UseAESForFileFragment", "m_UseAESForPlayerPrefs", "m_UseAESForSQLite" }.Contains(edits[0].propertyPath))
                throw new InvalidOperationException("存档领域编辑必须是实际 PersistComponent 的唯一存储 AES 字段。");
            SerializedProperty property = serialized.FindProperty(edits[0].propertyPath);
            if (property == null || property.propertyType != SerializedPropertyType.Boolean ||
                SerializedPropertyValues.Parse(edits[0].valueJson).Type != JTokenType.Boolean)
                throw new InvalidOperationException("存档 AES 领域字段必须是明确布尔值。");
        }

        /// <summary>识别真实存储 AES、SQLite Cipher 字段及其父聚合结构；普通同名业务字段不属于该领域。</summary>
        internal static bool PersistenceEncryptionPath(SerializedProperty property)
        {
            if (property == null || property.serializedObject.targetObject == null ||
                property.serializedObject.targetObject.GetType() != typeof(PersistComponent)) return false;
            string[] paths = { "m_UseAESForPlayerPrefs", "m_UseAESForFileFragment", "m_UseAESForSQLite", "m_SQLiteCipherPassword" };
            return paths.Any(path => property.propertyPath == path || path.StartsWith(property.propertyPath + ".", StringComparison.Ordinal) ||
                property.propertyPath.StartsWith(path + ".", StringComparison.Ordinal));
        }

        /// <summary>禁止整个 UnityEvent 持久调用树和其父聚合对象被通用 JSON 写入，避免把方法名配置变成通用执行面。</summary>
        private static bool EventPath(SerializedProperty property)
        {
            if (property.propertyPath.Split('.').Contains("m_PersistentCalls", StringComparer.Ordinal)) return true;
            SerializedProperty cursor = property.Copy();
            SerializedProperty end = property.GetEndProperty();
            if (cursor.Next(true)) do
            {
                if (SerializedProperty.EqualContents(cursor, end)) break;
                if (cursor.name == "m_PersistentCalls") return true;
            }
            while (cursor.Next(true));
            return false;
        }

        /// <summary>Unity 对象身份和 Prefab 归属属于引擎字段，不能通过通用属性修改；保留正常组件启用开关。</summary>
        private static bool SystemProperty(SerializedProperty property)
        {
            string root = property.propertyPath.Split('.')[0];
            return new[] { "m_ObjectHideFlags", "m_CorrespondingSourceObject", "m_PrefabInstance", "m_PrefabAsset",
                "m_GameObject", "m_EditorHideFlags", "m_Script", "m_Name", "m_EditorClassIdentifier" }.Contains(root, StringComparer.Ordinal);
        }

        /// <summary>冻结整个 Prefab 或单 SO 的全部序列化字段；预演目标可使用尚未 Apply 的同一缓存。</summary>
        private static string AssetStateHash(Resolved target, SerializedObject staged)
        {
            var parts = new List<string>();
            foreach (Object item in target.AssetObjects)
            {
                if (staged != null && item == target.Value) parts.Add(ObjectStateHash(item, staged));
                else using (var serialized = new SerializedObject(item))
                {
                    serialized.Update();
                    parts.Add(ObjectStateHash(item, serialized));
                }
            }
            return GenerateActionCommon.ComputeTextHash(string.Join("\n", parts.OrderBy(part => part, StringComparer.Ordinal)));
        }

        /// <summary>以 GlobalObjectId、真实类型、所有字段 contentHash 汇总对象指纹，包含隐藏字段但不公开单项摘要。</summary>
        private static string ObjectStateHash(Object value, SerializedObject serialized)
        {
            var parts = new List<string> { GlobalObjectId.GetGlobalObjectIdSlow(value).ToString(), value.GetType().AssemblyQualifiedName };
            SerializedProperty property = serialized.GetIterator();
            while (property.Next(true)) parts.Add(property.propertyPath + ":" + property.propertyType + ":" + property.contentHash);
            return string.Join("\n", parts);
        }

        /// <summary>执行与验证均固定资产路径、真实类型、脚本身份及文件内容，不因同名类或 GUID 重定向自动接受漂移。</summary>
        private static void AssertIdentity(Resolved target, Receipt receipt, CancellationToken cancellationToken)
        {
            if (target.Path != receipt.assetPath || target.Value.GetType().FullName != receipt.targetType ||
                target.Identity.globalObjectId != receipt.target.globalObjectId || target.ScriptPath != receipt.scriptPath ||
                AssetDatabase.AssetPathToGUID(target.ScriptPath) != receipt.scriptGuid ||
                FileHash(target.ScriptAbsolute, cancellationToken) != receipt.scriptSha256 ||
                FileHash(target.AbsolutePath + ".meta", cancellationToken) != receipt.metaSha256)
                throw new InvalidOperationException("资产或源脚本身份已发生变化。");
        }

        /// <summary>返回不含请求值的统一收据；成功仅证明本次字段与文件保存，不证明业务功能或设备运行。</summary>
        private static AgentActionResult Result(Receipt receipt, string status, string message)
        {
            AgentActionResult result = AgentActionResult.Create(null, status, message);
            result.ReceiptJson = receipt == null ? null : JsonConvert.SerializeObject(receipt);
            result.DataJson = result.ReceiptJson;
            if (status == "success") result.EvidenceKinds = AgentActionEvidence.Static | AgentActionEvidence.Artifact;
            result.Evidence.Add("只核对目标资产的精确字段写集、整体状态和保存文件；未执行游戏、场景、导出或外部服务。");
            return result;
        }

        /// <summary>仅允许项目 Assets 自有区域，排除框架、插件、vendor、测试和生成目录；脚本和目标均执行同一限制。</summary>
        private static bool OwnedPath(string path)
        {
            if (string.IsNullOrEmpty(path) || !path.StartsWith("Assets/", StringComparison.Ordinal)) return false;
            string[] segments = path.Split('/');
            return !segments.Any(segment => new[] { "Framework", "Plugins", "ThirdParty", "Third-Party", "Third Party", "Vendor", "Vendors",
                "Generated", "GeneratedCode", "_configs", "Tests" }.Contains(segment, StringComparer.OrdinalIgnoreCase));
        }

        /// <summary>项目自有源码及真实解析的 Framework/Nova SDK/Kit 自有 Scripts 可作为字段类型来源，vendor 和平台插件仍排除。</summary>
        private static bool TryResolveOwnedScript(string path, out string absolute)
        {
            absolute = null;
            if (string.IsNullOrEmpty(path) || !path.EndsWith(".cs", StringComparison.OrdinalIgnoreCase)) return false;
            if (OwnedPath(path))
            {
                return GenerateActionCommon.TryResolveProjectPath(path, "script", out absolute, out _) && File.Exists(absolute);
            }
            // 开发仓 Framework 也是发布包真源，只允许其 Scripts，不允许插件或其它目录。
            if (path.StartsWith("Assets/Framework/Scripts/", StringComparison.Ordinal) && SafeScriptSegments(path))
                return GenerateActionCommon.TryResolveProjectPath(path, "script", out absolute, out _) && File.Exists(absolute);
            // file: 消费项目和开发仓的 UPM 源也必须由实际 package.json 归属确认，不能只匹配目录名称。
            if (path.StartsWith("UPMPackages/", StringComparison.Ordinal))
            {
                string[] pieces = path.Split('/');
                if (pieces.Length >= 5 && pieces[2] == "Nova" && pieces[3] == "Scripts" && SafeScriptSegments(path) &&
                    GenerateActionCommon.TryResolveProjectPath("UPMPackages/" + pieces[1] + "/package.json", "package", out string manifest, out _) && File.Exists(manifest))
                {
                    JObject descriptor = JObject.Parse(File.ReadAllText(manifest));
                    string name = descriptor["name"]?.Value<string>();
                    if (NovaPackageName(name) && name == pieces[1])
                        return GenerateActionCommon.TryResolveProjectPath(path, "script", out absolute, out _) && File.Exists(absolute);
                }
                return false;
            }
            PackageInfo package = PackageInfo.FindForAssetPath(path);
            if (package == null || string.IsNullOrEmpty(package.resolvedPath) ||
                !NovaPackageName(package.name)) return false;
            string prefix = "Packages/" + package.name + "/";
            if (!path.StartsWith(prefix, StringComparison.Ordinal)) return false;
            string relative = path.Substring(prefix.Length);
            bool frameworkScript = package.name == "com.solotopia.nova.framework" && relative.StartsWith("Scripts/", StringComparison.Ordinal);
            if ((!frameworkScript && !relative.StartsWith("Nova/Scripts/", StringComparison.Ordinal)) || !SafeScriptSegments(relative)) return false;
            string root = Path.GetFullPath(package.resolvedPath).TrimEnd(Path.DirectorySeparatorChar);
            absolute = Path.GetFullPath(Path.Combine(root, relative));
            if (!absolute.StartsWith(root + Path.DirectorySeparatorChar, StringComparison.Ordinal) || !File.Exists(absolute)) return false;
            for (string current = absolute; current != null && current.StartsWith(root, StringComparison.Ordinal); current = Path.GetDirectoryName(current))
                if ((File.GetAttributes(current) & FileAttributes.ReparsePoint) != 0) return false;
            return true;
        }

        /// <summary>脚本归属只接受 Framework 和 Nova SDK/Kit 自有发布包，不把其它第三方包纳入通用编辑类型来源。</summary>
        private static bool NovaPackageName(string name) => name != null && (name == "com.solotopia.nova.framework" ||
            name.StartsWith("com.solotopia.nova.framework.sdk.", StringComparison.Ordinal) ||
            name.StartsWith("com.solotopia.nova.framework.kit.", StringComparison.Ordinal));

        /// <summary>自有包 Scripts 仍排除明确 vendor、生成和插件目录，防止仅凭 Nova 包名扩大第三方源码授权。</summary>
        private static bool SafeScriptSegments(string path) => !path.Split('/').Any(segment =>
            new[] { "Plugins", "ThirdParty", "Third-Party", "Third Party", "Vendor", "Vendors", "Generated", "GeneratedCode", "Tests", ".." }
                .Contains(segment, StringComparer.OrdinalIgnoreCase));

        /// <summary>规范化当前项目内文件路径；拒绝符号链接或项目外路径，不依赖宿主当前工作目录。</summary>
        private static string Absolute(string path)
        {
            if (!GenerateActionCommon.TryResolveProjectPath(path, "asset", out string absolute, out string error))
                throw new InvalidOperationException(error);
            return absolute;
        }

        /// <summary>使用既有带取消检查的完整文件 SHA256，不在诊断中输出文件内容。</summary>
        private static string FileHash(string absolute, CancellationToken cancellationToken) =>
            GenerateActionCommon.ComputeFileHash(absolute, cancellationToken);

        /// <summary>GUID 只接受 32 位十六进制标识，不接受路径、类型名或实例编号替代。</summary>
        private static bool IsGuid(string value) => value != null && value.Length == 32 && value.All(Uri.IsHexDigit);

        /// <summary>收据摘要必须是 64 位十六进制字符串，空摘要不能作为保存证据。</summary>
        private static bool IsHash(string value) => value != null && value.Length == 64 && value.All(Uri.IsHexDigit);
    }
}
