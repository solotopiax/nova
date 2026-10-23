/***************************************************************
 * (c) copyright 2026 - 2030, Solotopia
 * All Rights Reserved.
 * -------------------------------------------------------------
 * filename:  PersistenceMaintenance.AesRecovery.cs
 * author:    taoye
 * created:   2026/10/13
 * descrip:   存档AES维护的完整资产来源与中断日志恢复
 ***************************************************************/

using System;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading;
using Newtonsoft.Json;
using UnityEditor;

namespace NovaFramework.Editor
{
    internal static partial class PersistenceMaintenance
    {
        /// <summary>正式提交前保护完整原Prefab文本；原token冻结的整文件SHA认证内容，不以裸日志savedSHA授权。</summary>
        private static void PrepareAesAssetOrigin(Journal journal, CancellationToken token)
        {
            string original = File.ReadAllText(System.IO.Path.Combine(GenerateActionCommon.ProjectRoot, journal.settings.assetPath), new UTF8Encoding(false, true));
            if (original.Length > 4 * 1024 * 1024 || GenerateActionCommon.ComputeTextHash(original) != journal.settings.plannedFileSha256)
                throw new InvalidOperationException("AES原资产文本超限或漂移。");
            NormalizeAesPrefab(original, journal.target, journal.storage, journal.beforeAES);
            WriteProtectedValues(System.IO.Path.Combine(OperationDirectory(journal.operationId), "before-aes-asset.bin"),
                new[] { new ValueEntry { name = "prefab", value = original, index = true } }, ReadPrivacy(journal).AESKey);
            if (!AesDiskMatchesBoundOrigin(journal, token)) throw new InvalidOperationException("AES原资产保护重读失败。");
        }

        /// <summary>磁盘只接受原token认证全文中对应存储单布尔值改变；旧日志缺保护文件时仅接受原整文件SHA。</summary>
        internal static bool AesDiskMatchesBoundOrigin(Journal journal, CancellationToken token)
        {
            token.ThrowIfCancellationRequested();
            string path = System.IO.Path.Combine(GenerateActionCommon.ProjectRoot, journal.settings.assetPath);
            FileFragmentMaintenance.CheckPath(path);
            string protectedPath = System.IO.Path.Combine(OperationDirectory(journal.operationId), "before-aes-asset.bin");
            FileFragmentMaintenance.CheckPath(protectedPath);
            if (!File.Exists(protectedPath)) return FileFragmentMaintenance.Hash(path, token) == journal.settings.plannedFileSha256;
            ValueEntry[] values = ReadProtectedValues(protectedPath, ReadPrivacy(journal).AESKey);
            if (values?.Length != 1 || values[0].name != "prefab" || !values[0].index || values[0].value == null ||
                values[0].value.Length > 4 * 1024 * 1024 || GenerateActionCommon.ComputeTextHash(values[0].value) != journal.settings.plannedFileSha256)
                throw new InvalidOperationException("AES受保护原资产未绑定原token整文件SHA。");
            string expected = NormalizeAesPrefab(values[0].value, journal.target, journal.storage, journal.beforeAES);
            string disk = File.ReadAllText(path, new UTF8Encoding(false, true));
            if (disk.Length > 4 * 1024 * 1024) throw new InvalidOperationException("AES磁盘资产超限。");
            try { if (NormalizeAesPrefab(disk, journal.target, journal.storage, journal.beforeAES) == expected) return true; } catch { }
            try { if (NormalizeAesPrefab(disk, journal.target, journal.storage, journal.afterAES) == expected) return true; } catch { }
            return false;
        }

        /// <summary>只读解析固定Unity LF文本中精确组件localId的唯一0/1布尔标量；规整该值后保留其余全文。</summary>
        private static string NormalizeAesPrefab(string text, SerializedTargetEditing.Target target, string storage, bool enabled)
        {
            if (!text.StartsWith("%YAML 1.1", StringComparison.Ordinal) || !GlobalObjectId.TryParse(target.globalObjectId, out var id))
                throw new InvalidOperationException("AES资产文本格式或身份无效。");
            string marker = "--- !u!114 &" + id.targetObjectId.ToString(System.Globalization.CultureInfo.InvariantCulture) + "\n";
            int start = text.IndexOf(marker, StringComparison.Ordinal);
            if (start < 0) throw new InvalidOperationException("AES精确组件不在原资产文本中。");
            int end = text.IndexOf("\n--- !u!", start + marker.Length, StringComparison.Ordinal); if (end < 0) end = text.Length;
            string name = storage == "FileFragment" ? "m_UseAESForFileFragment" : storage == "PlayerPrefs" ? "m_UseAESForPlayerPrefs" :
                storage == "SQLite" ? "m_UseAESForSQLite" : throw new InvalidOperationException("AES恢复存储无效。");
            string field = "  " + name + ": ";
            int position = text.IndexOf(field, start, end - start, StringComparison.Ordinal);
            if (position < 0 || text.IndexOf(field, position + field.Length, end - position - field.Length, StringComparison.Ordinal) >= 0)
                throw new InvalidOperationException("AES布尔标量不唯一。");
            int lineEnd = text.IndexOf('\n', position);
            if (lineEnd < 0 || lineEnd > end || text.Substring(position + field.Length, lineEnd - position - field.Length) != (enabled ? "1" : "0"))
                throw new InvalidOperationException("AES磁盘字段不是原/新单行布尔值。");
            return text.Substring(0, position + field.Length) + "__NOVA_AES_BOUND_SCALAR__" + text.Substring(lineEnd);
        }

        /// <summary>对应存储单布尔恢复的完整内存及磁盘认证；dirty豁免仅在内部领域接口中生效。</summary>
        private static void AssertAesSettingsRecoverable(Journal journal, CancellationToken token) =>
            SerializedTargetEditing.AssertAesRecoveryTarget(journal.settings, journal.storage, journal.beforeAES, journal.afterAES,
                () => AesDiskMatchesBoundOrigin(journal, token), token);

        /// <summary>只核真实已保存收据、完整目标、对应布尔状态和clean后验；不复用Generic零变化未保存假设。</summary>
        private static void VerifyAesSavedSettings(Journal journal, CancellationToken token)
        {
            AssertAesSettingsRecoverable(journal, token);
            var view = SerializedTargetEditing.Inspect(journal.target, token);
            bool expected = journal.phase == "recovered" ? journal.beforeAES : journal.afterAES;
            var field = view.fields.Single(value => value.path == (journal.storage == "FileFragment" ? "m_UseAESForFileFragment" :
                journal.storage == "PlayerPrefs" ? "m_UseAESForPlayerPrefs" : "m_UseAESForSQLite"));
            if (!ValidSavedSettings(journal) || !journal.savedSettings.executionStarted || !view.editable ||
                view.stateSha256 != journal.savedSettings.expectedStateSha256 || view.fileSha256 != journal.savedSettings.observedFileSha256 ||
                field.valueJson != (expected ? "true" : "false")) throw new InvalidOperationException("AES保存收据或完整clean后验不符。");
        }

        /// <summary>有界检查固定临时日志；半写内容仅原token已绑定的独立恢复可隔离保留，完整异域日志始终拒绝。</summary>
        private static void CheckAesJournalTemporary(string temporary, Func<string, bool> matchesOrigin, bool authorizedRecovery, bool preserveInterrupted)
        {
            FileFragmentMaintenance.CheckPath(temporary);
            if (!File.Exists(temporary)) return;
            if (new FileInfo(temporary).Length > 1024 * 1024) throw new InvalidOperationException("AES临时日志超限，保留现场。");
            string text = File.ReadAllText(temporary); bool malformed = false;
            try
            {
                var parsed = Newtonsoft.Json.Linq.JToken.Parse(text);
                if (parsed.Type == Newtonsoft.Json.Linq.JTokenType.Null) malformed = true;
                else if (!matchesOrigin(text)) throw new InvalidOperationException("AES临时日志不属于本操作，保留现场。");
            }
            catch (JsonException) { malformed = true; }
            if (!malformed) return;
            if (!authorizedRecovery) throw new InvalidOperationException("半写AES日志需原token绑定后独立恢复。");
            if (preserveInterrupted)
            {
                string preserved = temporary.Substring(0, temporary.Length - 4) + ".interrupted-" + Guid.NewGuid().ToString("N") + ".bin";
                FileFragmentMaintenance.CheckPath(preserved); File.Move(temporary, preserved);
            }
        }

        /// <summary>标准存档分片权威路径；测试只在唯一隔离fixture中覆写，不接受请求路径。</summary>
        private static string AesMaintenanceFileRoot
        {
            get
            {
#if UNITY_INCLUDE_TESTS
                if (s_AesFixtureRoot != null) return System.IO.Path.Combine(s_AesFixtureRoot, "FileFragment");
#endif
                return NovaFramework.Runtime.Path.Persist.FileFragment.FolderFullPath;
            }
        }
        /// <summary>标准值维护SQLite权威路径；测试覆写只限固定game.db，不影响Cipher领域路径。</summary>
        private static string AesMaintenanceSqlitePath
        {
            get
            {
#if UNITY_INCLUDE_TESTS
                if (s_AesFixtureRoot != null) return System.IO.Path.Combine(s_AesFixtureRoot, "SQLite/game.db");
#endif
                return NovaFramework.Runtime.Path.Persist.SQLite.FileFullPath;
            }
        }
        /// <summary>默认PlayerPrefs键存在检查；只有内部隔离fixture走内存字典。</summary>
        private static bool HasMaintenancePlayerPrefsKey(string key)
        {
#if UNITY_INCLUDE_TESTS
            if (s_AesFixturePrefs != null) return s_AesFixturePrefs.ContainsKey(key);
#endif
            return UnityEngine.PlayerPrefs.HasKey(key);
        }
        /// <summary>维护只写完整已知字符串键；测试不触碰真实PlayerPrefs。</summary>
        private static void SetMaintenancePlayerPrefsString(string key, string value)
        {
#if UNITY_INCLUDE_TESTS
            if (s_AesFixturePrefs != null) { s_AesFixturePrefs[key] = value; return; }
#endif
            UnityEngine.PlayerPrefs.SetString(key, value);
        }
        /// <summary>默认provider明确进程全局Save；隔离fixture只保留内存观测，不宣称平台落盘。</summary>
        private static void SaveMaintenancePlayerPrefs()
        {
#if UNITY_INCLUDE_TESTS
            if (s_AesFixturePrefs != null) return;
#endif
            UnityEngine.PlayerPrefs.Save();
        }

#if UNITY_INCLUDE_TESTS
        private static SerializedTargetEditing.Target s_AesFixtureTarget;
        private static string s_AesFixtureRoot;
        private static ConfigMasterSO s_AesFixtureMaster;
        private static System.Collections.Generic.Dictionary<string, string> s_AesFixturePrefs;
        private static Action s_AesFieldSaveFault;
        private static Action<string> s_AesJournalWriteFault;
        /// <summary>仅测试创建的唯一普通Prefab可注册故障范围，不修改真实存档或公开Action请求。</summary>
        internal static IDisposable UseAesFieldFixture(SerializedTargetEditing.Target target)
        {
            string path = AssetDatabase.GUIDToAssetPath(target.targetGuid);
            if (s_AesFixtureTarget != null || !path.StartsWith("Assets/NovaPersistFieldGateTest_", StringComparison.Ordinal) ||
                !path.EndsWith("/Target.prefab", StringComparison.Ordinal)) throw new InvalidOperationException("AES测试目标范围无效。");
            s_AesFixtureTarget = target; return new AesFieldFixtureScope();
        }
        /// <summary>仅测试独立Master、普通Prefab、唯一tmp路径和内存Prefs可覆盖维护来源，不改变Globals或真实存档。</summary>
        internal static IDisposable UseAesMaintenanceFixture(ConfigMasterSO master, SerializedTargetEditing.Target target,
            string root, System.Collections.Generic.Dictionary<string, string> prefs)
        {
            FileFragmentMaintenance.CheckPath(root);
            string full = System.IO.Path.GetFullPath(root);
            if (master == null || !UnityEditor.EditorUtility.IsPersistent(master) || System.IO.Path.GetDirectoryName(full) != "/private/tmp" ||
                !System.IO.Path.GetFileName(full).StartsWith("NovaAesMaintenanceFixture_", StringComparison.Ordinal) || prefs == null)
                throw new InvalidOperationException("AES完整测试来源无效。");
            IDisposable scope = UseAesFieldFixture(target);
            s_AesFixtureRoot = full; s_AesFixtureMaster = master; s_AesFixturePrefs = prefs; return scope;
        }
        /// <summary>设置本隔离测试的字段保存前故障，null取消。</summary>
        internal static void SetAesFieldSaveFixtureFault(Action fault) { if (s_AesFixtureTarget == null) throw new InvalidOperationException(); s_AesFieldSaveFault = fault; }
        /// <summary>设置隔离测试日志半写故障，null取消。</summary>
        internal static void SetAesJournalFixtureFault(Action<string> fault) { if (s_AesFixtureTarget == null) throw new InvalidOperationException(); s_AesJournalWriteFault = fault; }
        /// <summary>只有当前隔离Prefab的AES字段Apply后触发保存故障，其它字段和目标不受影响。</summary>
        internal static void ProbeAesFieldSaveFixture(SerializedTargetEditing.Target target)
        {
            if (s_AesFixtureTarget != null && JsonConvert.SerializeObject(target) == JsonConvert.SerializeObject(s_AesFixtureTarget)) s_AesFieldSaveFault?.Invoke();
        }
        private sealed class AesFieldFixtureScope : IDisposable
        {
            /// <summary>结束隔离测试范围，清全部目标和故障回调；不删除正式恢复材料。</summary>
            public void Dispose() { s_AesFixtureTarget = null; s_AesFixtureRoot = null; s_AesFixtureMaster = null; s_AesFixturePrefs = null; s_AesFieldSaveFault = null; s_AesJournalWriteFault = null; }
        }
#endif
    }
}
