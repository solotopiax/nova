/***************************************************************
 * (c) copyright 2026 - 2030, Solotopia
 * All Rights Reserved.
 * -------------------------------------------------------------
 * filename:  PlayerBuildAction.GenerateAdapter.cs
 * author:    taoye
 * created:   2026/10/16
 * descrip:   WebGL Generate 的真实回调适配与输入拥有域合同
 ***************************************************************/

using System;
using System.IO;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using Cysharp.Threading.Tasks;
using Newtonsoft.Json.Linq;
using HybridCLR.Editor;
using NovaFramework.Runtime;
using UnityEditor;
using UnityEditor.Build.Reporting;
using UnityEditor.Compilation;
using UnityEngine;
using Path = System.IO.Path;

namespace NovaFramework.Editor
{
    /// <summary>既有 Player 合同的 WebGL Generate 内部适配；不新增 Action，不给 Android 未审分支授权。</summary>
    internal sealed partial class PlayerBuildAction
    {
        /// <summary>Plan 保存的完整源输入、准确回调、拥有域及报告恢复位置。</summary>
        [Serializable]
        internal sealed class GeneratePlan
        {
            public HybridClrGenerateInputProjection.Spec owned;
            public PlayerInputSnapshot inputs;
            public CallbackInputSnapshot callbacks;
            public FileState[] auditorBaseline;
            public string recoveryDirectory;
            public string previousReportGuid;
            public string masterLinkXml;
            public string webglTemplate;
            public HybridClrStripBuildContract.Snapshot stripBefore;
            /// <summary>原生SVS完整前态，不用旧hash-only材料或后来观测代替。</summary>
            public GenerateSceneVisibilityContract.Snapshot sceneVisibilityBefore;
            /// <summary>同次原回调行绑定的准确安装 DTO 加载前态；不是后来 current。</summary>
            public GenerateBuildDataAssemblyContract.Snapshot buildDataBefore;
        }

        /// <summary>仅一次真实 GenerateAll 及其正式 BuildReport、恢复和全部 typed 回调后验。</summary>
        [Serializable]
        internal sealed class GenerateEvidence
        {
            public bool complete;
            public bool chainReturned;
            public bool chainEntered;
            public bool cancelled;
            public string error;
            public HybridClrStripBuildContract.Snapshot stripAfter;
            public bool failureStateValid;
            public string preCompilationCertificate;
            public int generationLogErrors;
            public GeneratePlan before;
            public PlayerInputSnapshot afterInputs;
            /// <summary>同步Generate scope释放后立即只读捕获的实际SVS，失败也保留已有材料。</summary>
            public GenerateSceneVisibilityContract.Snapshot sceneVisibilityAfter;
            /// <summary>较晚输入捕获前的完整真实SVS观测，不能替代紧邻scope后态。</summary>
            public GenerateSceneVisibilityContract.Snapshot sceneVisibilityObserved;
            /// <summary>当时SVS捕获失败；任何非null值维持partial，不在Verify补造。</summary>
            public string sceneVisibilityError;
            public CallbackInputSnapshot afterCallbacks;
            /// <summary>afterCallbacks 后立即捕获的实际安装文件/加载状态，随原 final 与证书绑定。</summary>
            public GenerateBuildDataAssemblyContract.Snapshot buildDataAfter;
            /// <summary>本次scope.Dispose已经捕获的真实TMP后态；捕获失败或scope未创建时可为null，不用before补造。</summary>
            public PlayerTmpFontBuildContract.Snapshot tmpFontAfter;
            /// <summary>本次scope已有TMP捕获/验证错误；复制不代表通过TMP门或完整生成。</summary>
            public string tmpFontError;
            public string reportGuid;
            public string reportPlatform;
            public string reportOutput;
            public int reportOptions;
            public int reportErrors;
            public string reportResult;
            public FileState[] aotDlls;
            public FileState[] ownedOutputs;
            public string[] generatedAssetIdentities;
            public ReportCopyReceipt reportCopy;
            public PlayerBuiltinUsageBuildContract.ReportMaterial builtinUsageReport;
            public PlayerBuildEventsContract.ReportMaterial buildEventsReport;
            public PlayerEmptyUsageBuildContract.ReportMaterial emptyUsageReport;
            public PlayerFiniteAdditionalBuildContract.ReportMaterial finiteAdditionalReport;
            public PlayerPackageCallbacksContract.ReportMaterial packageCallbacksReport;
        }

        /// <summary>仅本合同拥有的无回调序列化形状，用于只读证明原 Save 的准确字节早退。</summary>
        [Serializable] private sealed class GenerateRepositoryJson { public GenerateRepositoryDictionary m_Dictionary; }
        /// <summary>固定字段形状，与已审 SettingsDictionary 的唯一序列化列表相同。</summary>
        [Serializable] private sealed class GenerateRepositoryDictionary { public GenerateRepositoryEntry[] m_DictionaryValues; }
        /// <summary>只复制准确三字符串值；不调用业务反序列化或目标 repo 的 OnBeforeSerialize。</summary>
        [Serializable] private sealed class GenerateRepositoryEntry { public string type; public string key; public string value; }

        /// <summary>当前消费者只接受既存 Assets 输出父链及 meta，避免授权未声明的新目录 meta。</summary>
        /// <param name="spec">由当前设置解析的文件角色；编译派生文件不属于 Assets 输出文件。</param>
        /// <returns>必须逐字保持的全部既存父目录 meta；缺失时拒绝并要求正式准备后重新 Plan。</returns>
        private static string[] GetGenerateAssetParentMetas(HybridClrGenerateInputProjection.Spec spec)
        {
            string assets = PhysicalPath("Assets"); var result = new HashSet<string>(StringComparer.Ordinal);
            foreach (string file in spec.files.Where(path => IsWithin(Path.GetFullPath(path), assets)))
                for (string parent = Path.GetDirectoryName(file); parent != assets; parent = Path.GetDirectoryName(parent))
                {
                    if (string.IsNullOrEmpty(parent) || !IsWithin(parent, assets) || !Directory.Exists(parent) || !File.Exists(parent + ".meta"))
                        throw new InvalidOperationException("Generate Assets 输出父目录及 meta 必须既存：" + parent);
                    result.Add(parent + ".meta");
                }
            return result.OrderBy(path => path, StringComparer.Ordinal).ToArray();
        }

        /// <summary>绑定默认双仓库的有效字典与磁盘，而非只删除跨域 identity；不执行 Init、Save 或 repo 序列化。</summary>
        /// <param name="inputs">追加准确 settings-manager 包源码指纹。</param>
        /// <param name="token">源码和磁盘读取取消令牌。</param>
        /// <returns>原有效配置与磁盘完全一致时的确定性摘要；未初始化、未保存或自定义仓库具体拒绝。</returns>
        private static string CaptureGenerateRepositoryState(ICollection<string> inputs, CancellationToken token)
        {
            Type owner = FindType("UnityEditor.ProBuilder.ProBuilderSettings");
            if (owner == null) throw new InvalidOperationException("Generate ProBuilder 已审类型缺失。");
            object settings = GenerateBacking(owner, "s_Instance", null);
            if (settings?.GetType().FullName != "UnityEditor.SettingsManagement.Settings") throw new InvalidOperationException("Generate ProBuilder 自然 Settings 缺失。");
            RequirePackageSource(settings.GetType(), "com.unity.settings-manager", "Editor/Settings.cs", "196185d5c64589a2d87a3d34becd8f68d6454a1066372d0cc4856df7afd14f29", inputs, token);
            RequirePackageSource(settings.GetType(), "com.unity.settings-manager", "Editor/FileSettingsRepository.cs", "39cf702c6e2aa40e8c62831fd1760fdab014925c9f1d073ed879dddb1487c601", inputs, token);
            RequirePackageSource(settings.GetType(), "com.unity.settings-manager", "Editor/PackageSettingsRepository.cs", "ed2a08ba522b69ddfa7780722d1b1280625e679ea0ae086976fc1d369d873d69", inputs, token);
            RequirePackageSource(settings.GetType(), "com.unity.settings-manager", "Editor/UserSettingsRepository.cs", "c858e3f45099dc333d8b93012597323c9c2314059242e24db6dfe0733d793880", inputs, token);
            RequirePackageSource(settings.GetType(), "com.unity.settings-manager", "Editor/SettingsDictionary.cs", "ff6fdb07336c69d94f7b2936ad9c634691789bcd109b000114d41623ed7a7516", inputs, token);
            Array repositories = GenerateBacking(settings.GetType(), "m_SettingsRepositories", settings) as Array;
            Type package = settings.GetType().Assembly.GetType("UnityEditor.SettingsManagement.PackageSettingsRepository", true);
            Type user = settings.GetType().Assembly.GetType("UnityEditor.SettingsManagement.UserSettingsRepository", true);
            Type file = package.BaseType;
            if (repositories == null || repositories.Length != 2 || repositories.GetValue(0)?.GetType() != package || repositories.GetValue(1)?.GetType() != user ||
                user.GetFields(System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance).Length != 0 || file?.FullName != "UnityEditor.SettingsManagement.FileSettingsRepository")
                throw new InvalidOperationException("Generate ProBuilder 只接受默认 Package/User 双仓库与无字段 UserRepo。");
            object repository = repositories.GetValue(0);
            const string relative = "ProjectSettings/Packages/com.unity.probuilder/Settings.json";
            if (!Equals(GenerateBacking(file, "m_Path", repository), relative) || !Equals(GenerateBacking(file, "m_Initialized", repository), true))
                throw new InvalidOperationException("Generate ProBuilder 文件仓库必须已自然初始化且路径准确；不替用户 Init/Save。");
            var fileFields = file.GetFields(System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.DeclaredOnly);
            if (!fileFields.Select(field => field.Name).OrderBy(name => name, StringComparer.Ordinal).SequenceEqual(new[] { "m_Dictionary", "m_Initialized", "m_JsonHash", "m_Path" }) ||
                fileFields.Where(field => field.IsDefined(typeof(SerializeField), false)).Select(field => field.Name).SingleOrDefault() != "m_Dictionary" ||
                file.GetField("m_Initialized", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance)?.FieldType != typeof(bool) ||
                file.GetField("m_Path", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance)?.FieldType != typeof(string) ||
                file.GetField("m_JsonHash", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance)?.FieldType != typeof(Hash128) ||
                package.GetFields(System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.DeclaredOnly).Length != 0)
                throw new InvalidOperationException("Generate ProBuilder FileRepo 字段/序列化 roster 变化。");
            object dictionary = GenerateBacking(file, "m_Dictionary", repository);
            Type dictionaryType = settings.GetType().Assembly.GetType("UnityEditor.SettingsManagement.SettingsDictionary", true);
            var dictionaryFields = dictionaryType.GetFields(System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.DeclaredOnly);
            if (dictionaryFields.Length != 2 || dictionaryFields.Where(field => field.IsDefined(typeof(SerializeField), false)).Select(field => field.Name).SingleOrDefault() != "m_DictionaryValues")
                throw new InvalidOperationException("Generate ProBuilder SettingsDictionary 序列化 roster 变化。");
            if (dictionary?.GetType() != dictionaryType) throw new InvalidOperationException("Generate ProBuilder Dictionary backing 类型变化。");
            object raw = GenerateBacking(dictionaryType, "dictionary", dictionary);
            if (raw?.GetType() != typeof(Dictionary<Type, Dictionary<string, string>>)) throw new InvalidOperationException("Generate ProBuilder 有效字典不是准确 BCL 字典。");
            var effective = new List<string[]>();
            foreach (KeyValuePair<Type, Dictionary<string, string>> entry in (Dictionary<Type, Dictionary<string, string>>)raw)
            {
                if (ReferenceEquals(entry.Key, null) || entry.Key.GetType() != typeof(int).GetType() || entry.Value?.GetType() != typeof(Dictionary<string, string>) || !ReferenceEquals(entry.Value.Comparer, EqualityComparer<string>.Default))
                    throw new InvalidOperationException("Generate ProBuilder 有效字典含未知类型或 comparer。");
                foreach (KeyValuePair<string, string> value in entry.Value) effective.Add(new[] { entry.Key.AssemblyQualifiedName, value.Key, value.Value });
            }
            Type pair = dictionaryType.GetNestedType("SettingsKeyValuePair", System.Reflection.BindingFlags.NonPublic);
            object values = GenerateBacking(dictionaryType, "m_DictionaryValues", dictionary);
            if (pair == null || values?.GetType() != typeof(List<>).MakeGenericType(pair)) throw new InvalidOperationException("Generate ProBuilder 序列化列表 backing 类型变化。");
            var serialized = new List<string[]>();
            foreach (object value in (System.Collections.IEnumerable)values)
                serialized.Add(new[] { (string)GenerateBacking(pair, "type", value), (string)GenerateBacking(pair, "key", value), (string)GenerateBacking(pair, "value", value) });
            token.ThrowIfCancellationRequested();
            string path = PhysicalPath(relative); RejectLinks(path);
            if (!File.Exists(path)) throw new InvalidOperationException("Generate ProBuilder 原持久配置文件缺失。");
            string diskText = File.ReadAllText(path);
            if (!(GenerateBacking(file, "m_JsonHash", repository) is Hash128 jsonHash) || jsonHash != Hash128.Compute(diskText))
                throw new InvalidOperationException("Generate ProBuilder 原 JSON Hash 未绑定当前磁盘；不替用户保存。");
            JObject disk = JObject.Parse(diskText, new JsonLoadSettings { DuplicatePropertyNameHandling = DuplicatePropertyNameHandling.Error });
            if (disk.Properties().Count() != 1 || !(disk["m_Dictionary"] is JObject stored) || stored.Properties().Count() != 1 || !(stored["m_DictionaryValues"] is JArray rows))
                throw new InvalidOperationException("Generate ProBuilder 持久配置 schema 未审。");
            var persisted = new List<string[]>();
            foreach (JObject row in rows)
            {
                if (row.Properties().Count() != 3 || row["type"]?.Type != JTokenType.String || row["key"]?.Type != JTokenType.String || row["value"]?.Type != JTokenType.String)
                    throw new InvalidOperationException("Generate ProBuilder 持久配置行结构未审。");
                persisted.Add(new[] { (string)row["type"], (string)row["key"], (string)row["value"] });
            }
            string expected = CanonicalGenerateRepositoryRows(persisted);
            if (CanonicalGenerateRepositoryRows(effective) != expected || CanonicalGenerateRepositoryRows(serialized) != expected ||
                Util.Json.Serialize(effective) != Util.Json.Serialize(persisted) || Util.Json.Serialize(serialized) != Util.Json.Serialize(persisted))
                throw new InvalidOperationException("Generate ProBuilder 有未保存或不同步的有效字典；beforeAssemblyReload.Save 可能改源，不继续生成。");
            RequireGenerateRepositorySerialization(diskText, effective);
            return GenerateActionCommon.ComputeTextHash(relative + "|" + expected);
        }

        /// <summary>仅用 Nova 自有无回调 DTO 预测原 repo 的准确 pretty 字节，拒绝语义相同但会被重载 Save 重写的文本。</summary>
        /// <param name="diskText">原持久文件完整文本。</param>
        /// <param name="effective">原 BCL 字典枚举顺序的三字符串行。</param>
        /// <returns>无返回值；原字节与预测不完全相等时拒绝，不写入或格式化原文件。</returns>
        internal static void RequireGenerateRepositorySerialization(string diskText, IEnumerable<string[]> effective)
        {
            CanonicalGenerateRepositoryRows(effective);
            var shape = new GenerateRepositoryJson { m_Dictionary = new GenerateRepositoryDictionary { m_DictionaryValues = effective.Select(row => new GenerateRepositoryEntry { type = row[0], key = row[1], value = row[2] }).ToArray() } };
            if (EditorJsonUtility.ToJson(shape, true) != diskText)
                throw new InvalidOperationException("Generate ProBuilder 当前持久文本不是已审 Save 的准确稳定序列化；不擅自改写格式。");
        }

        /// <summary>只读准确字段，不调用属性、序列化接口或初始化入口。</summary>
        /// <param name="owner">已审具体声明类型。</param>
        /// <param name="name">源码中固定的字段名。</param>
        /// <param name="instance">既有对象；静态字段为空。</param>
        /// <returns>当前字段原值；字段丢失直接拒绝。</returns>
        private static object GenerateBacking(Type owner, string name, object instance)
        {
            var field = owner.GetField(name, System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.Static | System.Reflection.BindingFlags.DeclaredOnly);
            if (field == null) throw new InvalidOperationException("Generate 已审 backing 字段缺失：" + owner.FullName + "." + name);
            return field.GetValue(instance);
        }

        /// <summary>规范原始 type/key/value 行，不解析用户值或实例化 Type；重复键和空行拒绝。</summary>
        /// <param name="rows">只从准确 BCL backing 或磁盘 schema 读取的三列行。</param>
        /// <returns>确定性文本，仅用于驻内存比较和摘要，不写入回执明文。</returns>
        internal static string CanonicalGenerateRepositoryRows(IEnumerable<string[]> rows)
        {
            if (rows == null) throw new InvalidOperationException("Generate repo 行缺失。");
            var seen = new HashSet<string>(StringComparer.Ordinal); var result = new List<string>();
            foreach (string[] row in rows)
            {
                if (row == null || row.Length != 3 || row.Any(value => value == null) || row[0].Length == 0 || row[1].Length == 0)
                    throw new InvalidOperationException("Generate repo 行结构缺失。");
                string key = row[0].Length + ":" + row[0] + row[1].Length + ":" + row[1];
                if (!seen.Add(key)) throw new InvalidOperationException("Generate repo 含重复 type/key。");
                result.Add(key + row[2].Length + ":" + row[2]);
            }
            return string.Join("\n", result.OrderBy(value => value, StringComparer.Ordinal));
        }

        /// <summary>只规整 AOTReference 实际所属程序集的模块身份；所有其他加载模块仍保留完整 MVID。</summary>
        /// <param name="inputs">已由准确回调捕获产生的输入行。</param>
        /// <param name="identities">真实回调身份集合，用来禁止投影任何回调所属程序集。</param>
        /// <returns>包含生成源码的准确模块以源闭包合同代替易变 MVID，其余行逐字保持。</returns>
        private static string[] ProjectGenerateAssemblyRoster(string[] inputs, string[] identities)
        {
            string logical = "Assets/" + SettingsUtil.HybridCLRSettings.outputAOTGenericReferenceFile;
            string owner = Path.GetFileNameWithoutExtension(CompilationPipeline.GetAssemblyNameFromScriptPath(logical));
            if (string.IsNullOrEmpty(owner) || identities.Any(row => row.Contains("|" + owner + ",")))
                throw new InvalidOperationException("不能投影缺失归属或含真实回调的 AOTReference 程序集。");
            string prefix = "LoadedAssembly=" + owner + ",";
            string[] rows = inputs.Where(row => row.StartsWith(prefix, StringComparison.Ordinal)).ToArray();
            if (rows.Length != 1) throw new InvalidOperationException("AOTReference 当前加载模块身份不唯一。");
            return inputs.Select(row => row.StartsWith(prefix, StringComparison.Ordinal) ? row.Substring(0, row.LastIndexOf('|')) + "|owned-generated-source; all other sources/defines/references frozen" : row)
                .OrderBy(row => row, StringComparer.Ordinal).ToArray();
        }

        /// <summary>保留未受生成源码影响的聚合导入摘要；准确受影响节点改绑逐文件闭包、依赖边、导入器与已加载有效对象。</summary>
        /// <param name="paths">完整递归依赖的非拥有逻辑路径。</param>
        /// <param name="projection">固定生成拥有域，不能由外部传入通配规则。</param>
        /// <param name="token">当前只读捕获取消令牌。</param>
        /// <returns>每个保留依赖的 GUID、依赖边及仍可稳定比较的有效导入材料。</returns>
        private static string[] CaptureGenerateDependencyIdentities(string[] paths, HybridClrGenerateInputProjection projection, CancellationToken token)
        {
            UnityEngine.Object[] loaded = Resources.FindObjectsOfTypeAll<UnityEngine.Object>();
            var rows = new List<string>();
            foreach (string path in paths)
            {
                token.ThrowIfCancellationRequested();
                string[] closure = AssetDatabase.GetDependencies(path, true).OrderBy(value => value, StringComparer.Ordinal).ToArray();
                bool affected = closure.Any(value => projection.Owns(PhysicalPath(value)));
                string identity = path + "|" + AssetDatabase.AssetPathToGUID(path);
                if (!affected) { rows.Add(identity + "|" + AssetDatabase.GetAssetDependencyHash(path)); continue; }
                AssetImporter importer = AssetImporter.GetAtPath(path);
                string[] retained = closure.Where(value => !projection.Owns(PhysicalPath(value))).ToArray();
                // 不加载任何新业务资产；现存有效对象正文仍必须逐个保持，未知变化继续拒绝。
                string[] objects = loaded.Where(value => value != null && EditorUtility.IsPersistent(value) && AssetDatabase.GetAssetPath(value) == path)
                    .Select(value => ObjectIdentity(value) + "|" + HashObject(value)).OrderBy(value => value, StringComparer.Ordinal).ToArray();
                rows.Add(identity + "|owned-dependency-projection|" + GenerateActionCommon.ComputeTextHash(Util.Json.Serialize(new { retained, importer = importer == null ? null : HashObject(importer), objects })));
            }
            return rows.ToArray();
        }

        /// <summary>只接受官方当前 WebGL StripAOT 的准确派生临时目录，不沿用普通 Player 的 Build/Builds 根。</summary>
        /// <param name="target">必须等于当前真实 WebGL 的目标。</param>
        /// <param name="output">本次候选输出目录。</param>
        /// <param name="projection">已验证准确输出角色并拒绝链接的拥有域。</param>
        /// <param name="error">拒绝原因，成功为空。</param>
        /// <returns>路径等于当前 SettingsUtil 派生的唯一临时目录且属于已审角色时为 true。</returns>
        private static bool TryValidateGenerateOutput(BuildTarget target, string output, HybridClrGenerateInputProjection projection, out string error)
        {
            if (target == BuildTarget.Android) return TryValidateAndroidGenerateOutput(output, projection, out error);
            error = null;
            string exact = Path.GetFullPath(Path.Combine(SettingsUtil.HybridCLRDataDir, "StrippedAOTDllsTempProj", "WebGL"));
            if (target != BuildTarget.WebGL || EditorUserBuildSettings.activeBuildTarget != target || projection == null || string.IsNullOrWhiteSpace(output) ||
                !string.Equals(Path.GetFullPath(output), exact, StringComparison.Ordinal) || !projection.Owns(exact))
            { error = "Generate 输出不是当前 WebGL 官方准确临时工程。"; return false; }
            projection.ValidateOwnedTrees(CancellationToken.None);
            return true;
        }

        /// <summary>仅描述同次正式准备失败的两份纯DTO；复用原克隆/dirty投影，不重捕获、不改变比较准入或原材料。</summary>
        /// <param name="original">该次CaptureForHybridClrGenerate在回调准备前已取得的原输入。</param>
        /// <param name="prepared">同次准备后的实际输入；只有原比较器返回false后生产调用才进入本方法。</param>
        /// <returns>最多15KiB的差异说明，为Dispatcher及通用失败前缀预留1KiB；超量明确省略整份明细，不截字符串冒充完整。</returns>
        private static string DescribeGeneratePreparationMismatch(PlayerInputSnapshot original, PlayerInputSnapshot prepared) => DescribeGeneratePreparationMismatchWithDiagnostics(original, prepared, null, null);

        /// <summary>在同一次规范化诊断内关联本次两捕获的纯guard记录；不补读或将弱path关联升级为完整Native身份。</summary>
        /// <param name="original">同次原输入。</param><param name="prepared">同次准备后输入。</param>
        /// <param name="originalDecisions">本次original捕获实际执行的Texture2D guard记录。</param><param name="preparedDecisions">本次prepared捕获实际执行的Texture2D guard记录。</param>
        /// <returns>同一15KiB总门内的纯失败说明，不改变许可。</returns>
        private static string DescribeGeneratePreparationMismatchWithDiagnostics(PlayerInputSnapshot original, PlayerInputSnapshot prepared,
            BuildImportedFontTextureCacheGuard.DecisionTrace[] originalDecisions, BuildImportedFontTextureCacheGuard.DecisionTrace[] preparedDecisions)
        {
            var data = new JObject { ["originalInputSha256"] = original?.sha256, ["preparedInputSha256"] = prepared?.sha256, ["normalizationComplete"] = false, ["detailsComplete"] = true, ["transportMaySanitizePaths"] = true };
            string stage = "clone";
            try
            {
                if (original == null || prepared == null) throw new InvalidOperationException("准备比较缺少输入DTO。");
                PlayerInputSnapshot before = Util.Json.Deserialize<PlayerInputSnapshot>(Util.Json.Serialize(original));
                PlayerInputSnapshot after = Util.Json.Deserialize<PlayerInputSnapshot>(Util.Json.Serialize(prepared));
                before.sha256 = null; after.sha256 = null; stage = "dirty-projection";
                before.effectiveIdentities = before.effectiveIdentities.Where(row => !row.StartsWith("PreservedDirtyObject=", StringComparison.Ordinal))
                    .Concat(ProjectDirtyObjectRows(before.effectiveIdentities.Where(row => row.StartsWith("PreservedDirtyObject=", StringComparison.Ordinal)),
                        after.effectiveIdentities.Where(row => row.StartsWith("TMPPreservedObject=", StringComparison.Ordinal))))
                    .OrderBy(row => row, StringComparer.Ordinal).ToArray();
                stage = "field-differences";
                string left = Util.Json.Serialize(before), right = Util.Json.Serialize(after);
                data["normalizationComplete"] = true; data["normalizedEqual"] = left == right;
                data["rawNormalizedBeforeSha256"] = GenerateActionCommon.ComputeTextHash(left); data["rawNormalizedAfterSha256"] = GenerateActionCommon.ComputeTextHash(right);
                var fields = new JArray(); JObject first = JObject.Parse(left), second = JObject.Parse(right);
                foreach (string name in first.Properties().Select(field => field.Name).Union(second.Properties().Select(field => field.Name), StringComparer.Ordinal))
                    if (!JToken.DeepEquals(first[name], second[name])) fields.Add(new JObject { ["field"] = name, ["before"] = PreparationFieldFingerprint(first[name]), ["after"] = PreparationFieldFingerprint(second[name]) });
                var rows = new JArray();
                for (int index = 0; index < Math.Max(before.effectiveIdentities.Length, after.effectiveIdentities.Length); index++)
                {
                    bool beforePresent = index < before.effectiveIdentities.Length, afterPresent = index < after.effectiveIdentities.Length;
                    string previous = beforePresent ? before.effectiveIdentities[index] : null, current = afterPresent ? after.effectiveIdentities[index] : null;
                    if (beforePresent != afterPresent || previous != current) rows.Add(new JObject { ["index"] = index, ["beforePresent"] = beforePresent, ["before"] = previous, ["afterPresent"] = afterPresent, ["after"] = current });
                }
                var previousCounts = before.effectiveIdentities.GroupBy(row => row, StringComparer.Ordinal).ToDictionary(group => group.Key, group => group.Count(), StringComparer.Ordinal);
                var currentCounts = after.effectiveIdentities.GroupBy(row => row, StringComparer.Ordinal).ToDictionary(group => group.Key, group => group.Count(), StringComparer.Ordinal);
                var changedRows = new JArray();
                foreach (string row in previousCounts.Keys.Union(currentCounts.Keys, StringComparer.Ordinal).OrderBy(row => row, StringComparer.Ordinal))
                {
                    previousCounts.TryGetValue(row, out int previousCount); currentCounts.TryGetValue(row, out int currentCount);
                    if (previousCount != currentCount) changedRows.Add(new JObject { ["row"] = row, ["beforeCount"] = previousCount, ["afterCount"] = currentCount });
                }
                data["changedFieldCount"] = fields.Count; data["fields"] = fields; data["fieldDiffComplete"] = true;
                data["effectiveBeforeCount"] = before.effectiveIdentities.Length; data["effectiveAfterCount"] = after.effectiveIdentities.Length;
                data["differentEffectiveIndexCount"] = rows.Count; data["differentEffectiveIndexRows"] = rows;
                data["differentEffectiveRowCount"] = changedRows.Count; data["differentEffectiveRows"] = changedRows; data["effectiveRowDiffComplete"] = true; data["effectiveOrderOnly"] = rows.Count > 0 && changedRows.Count == 0;
            }
            catch (Exception error) when (error is InvalidOperationException || error is Newtonsoft.Json.JsonException || error is ArgumentException)
            {
                data["normalizationComplete"] = false; data["detailsComplete"] = false; data["normalizationFailureStage"] = stage; data["normalizationErrorType"] = error.GetType().FullName; data["normalizationErrorSha256"] = GenerateActionCommon.ComputeTextHash(error.Message);
            }
            if (originalDecisions != null || preparedDecisions != null) AttachImportedFontCacheDecisions(data, originalDecisions, preparedDecisions);
            return FormatGeneratePreparationDiagnostic(data);
        }

        /// <summary>一般字段只返回完整序列化值的摘要/类型/数量，不暴露配置正文或凭据。</summary>
        /// <param name="value">规范化DTO的准确单字段节点；null参数表示字段未出现。</param>
        /// <returns>完整值的有限指纹，数组顺序及重复内容均进入SHA。</returns>
        private static JObject PreparationFieldFingerprint(JToken value)
        {
            string raw = value?.ToString(Newtonsoft.Json.Formatting.None);
            return new JObject { ["present"] = value != null, ["type"] = value?.Type.ToString(), ["count"] = value == null || value.Type == JTokenType.Null ? 0 : value is JContainer container ? container.Count : 1, ["rawJsonSha256"] = raw == null ? null : GenerateActionCommon.ComputeTextHash(raw), ["rawJsonBytes"] = raw == null ? 0 : System.Text.Encoding.UTF8.GetByteCount(raw) };
        }

        /// <summary>只将同次已记录的纯值转换为独立JSON；notReached扣除最后已尝试未返回的点，异常位置不由最后读取猜测。</summary>
        /// <param name="trace">本次实际判定的纯记录。</param>
        /// <returns>完整记录副本，不含对象/委托或Font正文。</returns>
        private static JObject ImportedFontCacheDecisionJson(BuildImportedFontTextureCacheGuard.DecisionTrace trace)
        {
            string[] reads = { "candidateNull", "candidateType", "nativeAsset", "foreignAsset", "subAsset", "firstHideFlags", "path", "importerLookup", "importerNull", "importerType", "importerDirty", "ownerCount", "ownerDirty", "serializedOwner", "materialPropertyLookup", "texturePropertyLookup", "materialPropertyNull", "materialPropertyType", "texturePropertyNull", "texturePropertyType", "materialId", "textureId", "candidateId", "textureJson", "width", "height", "format", "readable", "mipmapCount", "dimension", "secondHideFlags" };
            return new JObject { ["classified"] = trace.classified, ["rejectedAt"] = trace.rejectedAt, ["lastRecordedAttempt"] = trace.lastAttempt,
                ["exceptionType"] = trace.exceptionType, ["exceptionLocationNotInferredFromLastAttempt"] = trace.exceptionType != null,
                ["attemptWithoutRecordedReturn"] = trace.lastAttempt != null && trace.observed.Property(trace.lastAttempt) == null ? trace.lastAttempt : null,
                ["exactDirtyRow"] = trace.exactDirtyRow, ["observed"] = trace.observed.DeepClone(),
                ["notReachedReadPoints"] = new JArray(reads.Where(name => trace.observed.Property(name) == null && name != trace.lastAttempt)) };
        }

        /// <summary>按完整dirty行精确关联false记录；true记录只能在已有path唯一时弱关联，歧义不选winner。分组计数/完整SHA与完整样本有独立有界展示标记。</summary>
        /// <param name="data">同次正规化后的纯差异树。</param><param name="original">本次原捕获记录，null表示未请求收集。</param><param name="prepared">本次准备后捕获记录，null表示未请求收集。</param>
        /// <returns>无返回值；只改变自有诊断树，不影响输入或分类。</returns>
        private static void AttachImportedFontCacheDecisions(JObject data, BuildImportedFontTextureCacheGuard.DecisionTrace[] original, BuildImportedFontTextureCacheGuard.DecisionTrace[] prepared)
        {
            var full = new JArray(); var related = new List<JObject>(); int exactMatches = 0, pathMatches = 0, missing = 0, ambiguous = 0;
            foreach (var phase in new[] { new { name = "original", traces = original }, new { name = "prepared", traces = prepared } })
            {
                var traces = phase.traces ?? Array.Empty<BuildImportedFontTextureCacheGuard.DecisionTrace>();
                foreach (var trace in traces) full.Add(new JObject { ["phase"] = phase.name, ["trace"] = ImportedFontCacheDecisionJson(trace) });
                var selected = new HashSet<BuildImportedFontTextureCacheGuard.DecisionTrace>();
                foreach (JToken row in data["differentEffectiveRows"] as JArray ?? new JArray())
                {
                    string raw = row["row"]?.Value<string>(); if (raw == null || !raw.StartsWith("PreservedDirtyObject=UnityEngine.Texture2D|", StringComparison.Ordinal)) continue;
                    int tail = raw.LastIndexOf('|'); if (tail <= "PreservedDirtyObject=".Length) { missing++; continue; } string[] identity = raw.Substring("PreservedDirtyObject=".Length, tail - "PreservedDirtyObject=".Length).Split(new[] { '|' }, 4);
                    if (identity.Length != 4) { missing++; continue; }
                    var exact = traces.Where(trace => trace.exactDirtyRow == raw).ToArray();
                    if (exact.Length == 1) { exactMatches++; if (selected.Add(exact[0])) related.Add(new JObject { ["phase"] = phase.name, ["binding"] = "exact-original-dirty-row", ["trace"] = ImportedFontCacheDecisionJson(exact[0]) }); continue; }
                    if (exact.Length > 1) { ambiguous++; continue; }
                    var byPath = traces.Where(trace => trace.classified && trace.observed["path"]?.Value<string>() == identity[3]).ToArray();
                    if (byPath.Length == 1) { pathMatches++; if (selected.Add(byPath[0])) related.Add(new JObject { ["phase"] = phase.name, ["binding"] = "unique-observed-path-only-no-guid-or-localid-proof", ["trace"] = ImportedFontCacheDecisionJson(byPath[0]) }); }
                    else if (byPath.Length > 1) ambiguous++; else missing++;
                }
            }
            var allGroups = new JArray();
            foreach (var group in related.GroupBy(row => row["phase"] + "|" + row["trace"]["classified"] + "|" + row["trace"]["rejectedAt"] + "|" + row["trace"]["exceptionType"] + "|" + (!string.IsNullOrEmpty(row["trace"]["exceptionType"]?.Value<string>()) ? row["trace"]["lastRecordedAttempt"] : "") + "|" + row["trace"]["observed"]?[row["trace"]["rejectedAt"]?.Value<string>() ?? ""], StringComparer.Ordinal)
                .OrderBy(group => group.First()["trace"]["classified"].Value<bool>()).ThenBy(group => group.Key, StringComparer.Ordinal))
            {
                JObject first = group.First(); string raw = new JArray(group.Select(row => row.DeepClone())).ToString(Newtonsoft.Json.Formatting.None);
                allGroups.Add(new JObject { ["phase"] = first["phase"], ["classified"] = first["trace"]["classified"], ["rejectedAt"] = first["trace"]["rejectedAt"], ["exceptionType"] = first["trace"]["exceptionType"],
                    ["lastRecordedAttemptIfException"] = !string.IsNullOrEmpty(first["trace"]["exceptionType"]?.Value<string>()) ? first["trace"]["lastRecordedAttempt"] : null, ["observedGateValue"] = first["trace"]["observed"]?[first["trace"]["rejectedAt"]?.Value<string>() ?? ""]?.DeepClone(), ["count"] = group.Count(), ["rawGroupSha256"] = GenerateActionCommon.ComputeTextHash(raw), ["sample"] = first.DeepClone() });
            }
            string fullRaw = full.ToString(Newtonsoft.Json.Formatting.None); var groups = new JArray();
            var output = new JObject { ["diagnosticOnly"] = true, ["notApprovalEvidence"] = true, ["countScope"] = "collected-dirty-Texture2D-only", ["originalCollectionRequested"] = original != null, ["preparedCollectionRequested"] = prepared != null,
                ["originalGuardCallCount"] = original?.Length ?? 0, ["preparedGuardCallCount"] = prepared?.Length ?? 0, ["rawAllCallsSha256"] = GenerateActionCommon.ComputeTextHash(fullRaw), ["rawAllCallsBytes"] = System.Text.Encoding.UTF8.GetByteCount(fullRaw),
                ["exactRowMatchCount"] = exactMatches, ["uniquePathOnlyMatchCount"] = pathMatches, ["unmatchedPhaseRowCount"] = missing, ["ambiguousPhaseRowCount"] = ambiguous,
                ["relatedCallCount"] = related.Count, ["groupCount"] = allGroups.Count, ["groups"] = groups, ["groupDisplayComplete"] = true, ["samplesComplete"] = related.Count == allGroups.Count };
            foreach (JObject group in allGroups)
            {
                JObject shown = (JObject)group.DeepClone(); groups.Add(shown);
                if (System.Text.Encoding.UTF8.GetByteCount(output.ToString(Newtonsoft.Json.Formatting.None)) <= 4 * 1024 - 128) continue;
                shown.Remove("sample"); shown["sampleOmitted"] = true; output["samplesComplete"] = false;
                if (System.Text.Encoding.UTF8.GetByteCount(output.ToString(Newtonsoft.Json.Formatting.None)) <= 4 * 1024 - 128) continue;
                groups.Remove(shown); output["groupDisplayComplete"] = false; break;
            }
            output["displayedGroupCount"] = groups.Count;
            output["captureDisplayComplete"] = original != null && prepared != null && missing == 0 && ambiguous == 0 && output["groupDisplayComplete"].Value<bool>() && output["samplesComplete"].Value<bool>() && related.Count == full.Count;
            if (!output["captureDisplayComplete"].Value<bool>()) data["detailsComplete"] = false;
            data["fontCacheDecisions"] = output;
        }

        /// <summary>总预算紧张时先完整省略trace样本，再省略分组展示，保留真实总数/完整SHA，优先保护实际dirty行身份。</summary>
        /// <param name="data">已附同次trace的自有诊断。</param><param name="limit">原总UTF8门，不提高15KiB。</param>
        /// <returns>无返回值；任何省略均明确非完整。</returns>
        private static void ReduceImportedFontCacheTraceDisplay(JObject data, int limit)
        {
            if (!(data["fontCacheDecisions"] is JObject trace) || System.Text.Encoding.UTF8.GetByteCount(data.ToString(Newtonsoft.Json.Formatting.None)) <= limit) return;
            foreach (JObject group in trace["groups"] as JArray ?? new JArray()) { group.Remove("sample"); group["sampleOmitted"] = true; }
            trace["samplesComplete"] = false; trace["captureDisplayComplete"] = false;
            if (System.Text.Encoding.UTF8.GetByteCount(data.ToString(Newtonsoft.Json.Formatting.None)) > limit) { trace.Remove("groups"); trace["groupDisplayComplete"] = false; trace["displayedGroupCount"] = 0; }
        }

        /// <summary>将脱敏前完整差异绑定SHA并严格限制UTF8；超限先移除放大的逐索引明细，保留完整多重行计数；仍超量再整体省略明细，保留总数及完整差异摘要。</summary>
        /// <param name="data">本次自有完整纯DTO差异树，SHA计算不包含自身的rawDiffSha256/rawDiffBytes字段。</param>
        /// <returns>不超过15KiB的纯JSON诊断；Gateway后续可脱敏路径，返回JSON不能据此重算脱敏前SHA。</returns>
        private static string FormatGeneratePreparationDiagnostic(JObject data)
        {
            const int limit = 15 * 1024 - 32;
            string raw = data.ToString(Newtonsoft.Json.Formatting.None); data["rawDiffSha256"] = GenerateActionCommon.ComputeTextHash(raw); data["rawDiffBytes"] = System.Text.Encoding.UTF8.GetByteCount(raw);
            if (System.Text.Encoding.UTF8.GetByteCount(data.ToString(Newtonsoft.Json.Formatting.None)) > limit)
            {
                data["detailsComplete"] = false; data["omissionReason"] = "index-details-utf8-budget"; data["indexDetailsOmitted"] = true; data.Remove("differentEffectiveIndexRows");
                ReduceImportedFontCacheTraceDisplay(data, limit);
                if (System.Text.Encoding.UTF8.GetByteCount(data.ToString(Newtonsoft.Json.Formatting.None)) > limit) { data["effectiveRowDiffComplete"] = false; data["omissionReason"] = "effective-row-details-utf8-budget"; data.Remove("differentEffectiveRows"); }
                if (System.Text.Encoding.UTF8.GetByteCount(data.ToString(Newtonsoft.Json.Formatting.None)) > limit) { data["fieldDiffComplete"] = false; data.Remove("fields"); }
            }
            string result = data.ToString(Newtonsoft.Json.Formatting.None);
            if (System.Text.Encoding.UTF8.GetByteCount(result) > limit)
                result = new JObject { ["detailsComplete"] = false, ["omissionReason"] = "diagnostic-metadata-budget", ["rawDiffSha256"] = data["rawDiffSha256"], ["rawDiffBytes"] = data["rawDiffBytes"] }.ToString(Newtonsoft.Json.Formatting.None);
            return " 准备差异=" + result;
        }

        /// <summary>由当前设置计算唯一拥有角色，其他程序集源不能因处于祖先目录而被排除。</summary>
        /// <param name="masterLinkXml">已项目内解析的 Master AOT 保留表路径。</param>
        /// <param name="protectedFiles">当前场景、Master 和直接配置文件。</param>
        /// <returns>精确拥有域；任一关键输入交叉时抛出异常。</returns>
        private static HybridClrGenerateInputProjection CreateGenerateProjection(string masterLinkXml, string[] protectedFiles)
        {
            var settings = SettingsUtil.HybridCLRSettings;
            if (settings.outputAOTGenericReferenceFile != "HybridCLRGenerate/AOTGenericReferences.cs" || settings.outputLinkFile != "HybridCLRGenerate/link.xml")
                throw new InvalidOperationException("当前 WebGL consumer 只接纳已审默认 AOTGenericReferences.cs/HybridCLRGenerate link.xml 角色；自定义源码路径须另审。");
            string generic = Path.GetFullPath(Path.Combine(Application.dataPath, settings.outputAOTGenericReferenceFile ?? string.Empty));
            var spec = new HybridClrGenerateInputProjection.Spec
            {
                projectRoot = BuildActionCommon.ProjectRoot,
                directories = new[] { SettingsUtil.GetHotUpdateDllsOutputDirByTarget(BuildTarget.WebGL),
                    SettingsUtil.GetAssembliesPostIl2CppStripDir(BuildTarget.WebGL), SettingsUtil.GeneratedCppDir,
                    Path.Combine(SettingsUtil.HybridCLRDataDir, "StrippedAOTDllsTempProj", "WebGL") }.Select(Path.GetFullPath).ToArray(),
                files = new[] { generic, Path.GetFullPath(masterLinkXml), Path.GetFullPath(Path.Combine(Application.dataPath, settings.outputLinkFile ?? string.Empty)) },
            };
            string logicalGeneric = Path.GetRelativePath(BuildActionCommon.ProjectRoot, generic).Replace('\\', '/');
            string ownerDll = CompilationPipeline.GetAssemblyNameFromScriptPath(logicalGeneric);
            if (string.IsNullOrEmpty(ownerDll)) throw new InvalidOperationException("AOTReference 生成源码没有唯一的当前编译程序集归属。");
            string ownerName = Path.GetFileNameWithoutExtension(ownerDll);
            UnityEditor.Compilation.Assembly owner = CompilationPipeline.GetAssemblies(AssembliesType.Player).SingleOrDefault(value => value.name == ownerName);
            if (owner == null || !owner.sourceFiles.Select(PhysicalPath).Contains(generic, StringComparer.Ordinal))
                throw new InvalidOperationException("AOTReference 不属于当前真实 Player 编译源集合；缺失来源需先完成正式资产导入。");
            string libraryAssembly = PhysicalPath("Library/ScriptAssemblies/" + ownerName + ".dll");
            string outputAssembly = PhysicalPath(owner.outputPath);
            if (!IsWithin(outputAssembly, PhysicalPath("Library")) || Path.GetFileName(outputAssembly) != ownerName + ".dll")
                throw new InvalidOperationException("AOTReference 编译输出不属于当前已审 Library 程序集布局。");
            spec.compiledAssemblyName = ownerName;
            spec.compiledArtifacts = new[] { libraryAssembly, Path.ChangeExtension(libraryAssembly, ".pdb"), outputAssembly, Path.ChangeExtension(outputAssembly, ".pdb") }.Distinct(StringComparer.Ordinal).ToArray();
            GetGenerateAssetParentMetas(spec);
            var projection = new HybridClrGenerateInputProjection(spec, protectedFiles);
            foreach (UnityEditor.Compilation.Assembly assembly in CompilationPipeline.GetAssemblies(AssembliesType.Player))
                foreach (string source in assembly.sourceFiles)
                    if (projection.Owns(PhysicalPath(source)) && PhysicalPath(source) != generic)
                        throw new InvalidOperationException("Generate 拥有域包含非 AOTReference 的编译源：" + source);
            return projection;
        }

        /// <summary>只读建立 WebGL Generate 计划，保留全部原脏状态和 Player 的准备前后保护。</summary>
        /// <param name="masterPath">当前真实 Master 的逻辑路径。</param>
        /// <param name="masterLinkXml">Master 保留表精确路径。</param>
        /// <param name="scenes">本次启用场景完整有序集合。</param>
        /// <param name="token">当前取消令牌。</param>
        /// <returns>完整 typed 计划；旧 C++ 开发标记不作为生成前提。</returns>
        internal static GeneratePlan CaptureForHybridClrGenerate(string masterPath, string masterLinkXml, string[] scenes, CancellationToken token)
        {
            if (EditorUserBuildSettings.activeBuildTarget != BuildTarget.WebGL || scenes == null || scenes.Length == 0)
                throw new InvalidOperationException("Generate adapter 仅支持当前真实 WebGL 与完整场景集合。");
            if ((SettingsUtil.HybridCLRSettings.externalHotUpdateAssembliyDirs?.Length ?? 0) != 0)
                throw new InvalidOperationException("当前 Generate 消费合同只接受默认 WebGL 编译目录；外部热更优先解析源须另审，不猜已消费本次编译。");
            string[] protectedFiles = scenes.Concat(new[] { masterPath, "ProjectSettings/ProjectSettings.asset", "ProjectSettings/HybridCLRSettings.asset" })
                .Select(PhysicalPath).SelectMany(path => new[] { path, path + ".meta" }).ToArray();
            HybridClrGenerateInputProjection projection = CreateGenerateProjection(masterLinkXml, protectedFiles);
            string output = Path.GetFullPath(Path.Combine(SettingsUtil.HybridCLRDataDir, "StrippedAOTDllsTempProj", "WebGL"));
            GenerateSceneVisibilityContract.ValidatedCapture svsBefore = GenerateSceneVisibilityContract.CaptureInitial();
            GenerateSceneVisibilityContract.Snapshot svs = svsBefore.baseline;
            if (!TryCaptureInputsWithDiagnostics(BuildTarget.WebGL, output, EditorUserBuildSettings.development, token, out PlayerInputSnapshot original, out string error, out var originalFontCacheDecisions, null, projection, svsBefore))
                throw new InvalidOperationException(error);
            CallbackInputSnapshot callbacks = CapturePlayerCallbacks(BuildTarget.WebGL, original.dependencyPaths, token, true);
            if (callbacks.unsupported.Length != 0) throw new InvalidOperationException("WebGL Generate 回调合同未闭合：" + string.Join("; ", callbacks.unsupported));
            GenerateBuildDataAssemblyContract.Snapshot buildData = GenerateBuildDataAssemblyContract.Capture(callbacks.packageCallbacks.domain, callbacks.inputs);
            if (!TryCaptureInputsWithDiagnostics(BuildTarget.WebGL, output, EditorUserBuildSettings.development, token, out PlayerInputSnapshot prepared, out error, out var preparedFontCacheDecisions, callbacks.tmpFonts, projection, GenerateSceneVisibilityContract.ApproveBefore(svs, GenerateSceneVisibilityContract.Capture())))
                throw new InvalidOperationException(error ?? "Generate 回调准备改变了未授权源或脏状态。");
            if (!PreparedInputsPreserveOriginal(original, prepared))
                throw new InvalidOperationException((error ?? "Generate 回调准备改变了未授权源或脏状态。") + DescribeGeneratePreparationMismatchWithDiagnostics(original, prepared, originalFontCacheDecisions, preparedFontCacheDecisions));
            if (!prepared.scenes.Select(scene => scene.path).SequenceEqual(scenes, StringComparer.Ordinal) || prepared.masterPath != masterPath)
                throw new InvalidOperationException("Generate 与 Player 共享捕获的 Master 或场景不一致。");
            foreach (string protectedPath in new[] { prepared.runtimePath, prepared.yooSettingsPath, prepared.collectorPath })
                if (projection.Owns(PhysicalPath(protectedPath))) throw new InvalidOperationException("Generate 覆盖直接配置源：" + protectedPath);
            BuildReport previous = BuildReport.GetLatestReport();
            return new GeneratePlan
            {
                owned = projection.ExportSpec(), sceneVisibilityBefore = svs, buildDataBefore = buildData,
                inputs = prepared, callbacks = callbacks, auditorBaseline = CaptureAuditorBaseline(token), masterLinkXml = masterLinkXml,
                recoveryDirectory = PhysicalPath("Library/Nova/AgentActions/HybridCLRGenerate/" + Guid.NewGuid().ToString("N") + "/report-backup"),
                previousReportGuid = previous == null ? null : previous.summary.guid.ToString(), webglTemplate = PlayerSettings.WebGL.template, stripBefore = HybridClrStripBuildContract.CaptureWebGL(),
            };
        }

        /// <summary>执行前精确比较两个真实计划，不将新脏标志或新模块当作旧输入。</summary>
        /// <param name="expected">原 Plan。</param>
        /// <param name="actual">当前重新捕获计划。</param>
        /// <returns>源、环境以外的回调不变量、全部 before、报告与模板均保持时为 true。</returns>
        internal static bool GeneratePlansEqual(GeneratePlan expected, GeneratePlan actual)
        {
            return expected != null && actual != null && expected.inputs.sha256 == actual.inputs.sha256 &&
                GenerateSceneVisibilityContract.BeforeEquals(expected.sceneVisibilityBefore, actual.sceneVisibilityBefore) &&
                GenerateBuildDataAssemblyContract.BeforeEquals(expected.buildDataBefore, actual.buildDataBefore) &&
                expected.callbacks.sha256 == actual.callbacks.sha256 && expected.previousReportGuid == actual.previousReportGuid &&
                expected.webglTemplate == actual.webglTemplate && HybridClrStripBuildContract.BeforeEqual(expected.stripBefore, actual.stripBefore) && Util.Json.Serialize(expected.owned) == Util.Json.Serialize(actual.owned) &&
                Util.Json.Serialize(expected.auditorBaseline) == Util.Json.Serialize(actual.auditorBaseline) &&
                NormalResetBaselinesEqual(expected.callbacks.normalResetStates, actual.callbacks.normalResetStates) &&
                PlayerTmpFontBuildContract.BeforeEquals(expected.callbacks.tmpFonts, actual.callbacks.tmpFonts) &&
                PlayerFacebookBuildContract.BeforeEquals(expected.callbacks.facebook, actual.callbacks.facebook) &&
                PlayerBuiltinUsageBuildContract.BeforeEquals(expected.callbacks.builtinUsage, actual.callbacks.builtinUsage) &&
                PlayerBuildEventsContract.BeforeEquals(expected.callbacks.buildEvents, actual.callbacks.buildEvents) &&
                PlayerEmptyUsageBuildContract.BeforeEquals(expected.callbacks.emptyUsage, actual.callbacks.emptyUsage) &&
                PlayerFiniteAdditionalBuildContract.BeforeEquals(expected.callbacks.finiteAdditional, actual.callbacks.finiteAdditional) &&
                PlayerPackageCallbacksContract.BeforeEquals(expected.callbacks.packageCallbacks, actual.callbacks.packageCallbacks);
        }

        /// <summary>恰好一次调用正式 GenerateAll，复用全部真实回调后验与异步报告恢复，不调用任何回调方法补证据。</summary>
        /// <param name="plan">本次写前已经精确重核的计划。</param>
        /// <param name="expectedOptions">官方 StripAOT 显式 options；与普通 Player BuildMode 分开。</param>
        /// <param name="token">本次取消令牌；失败和取消也先完成准确恢复及正式报告等待。</param>
        /// <returns>真实执行材料；Dispose成功/失败均保存已有TMP后态和错误，任何未闭合状态complete=false并保留全部异常及恢复位置。</returns>
        internal static async UniTask<GenerateEvidence> ExecuteHybridClrGenerateAsync(GeneratePlan plan, int expectedOptions, CancellationToken token)
        {
            var evidence = new GenerateEvidence { before = plan };
            PlayerCallbackExecutionScope scope = null;
            ProjectAuditorReportScope auditor = null;
            BuildReport report = null;
            Exception failure = null;
            try
            {
                token.ThrowIfCancellationRequested();
                if (!GenerateBuildDataAssemblyContract.BeforeEquals(plan.buildDataBefore, GenerateBuildDataAssemblyContract.Capture(plan.callbacks.packageCallbacks.domain, plan.callbacks.inputs)))
                    throw new InvalidOperationException("Generate DTO 程序集在正式写前漂移。");
                if (!HybridClrStripBuildContract.BeforeEqual(plan.stripBefore, HybridClrStripBuildContract.CaptureWebGL()))
                    throw new InvalidOperationException("Generate StripAOT 类型化前态在执行前漂移。");
                var executionProjection = new HybridClrGenerateInputProjection(plan.owned, plan.inputs.scenes.Select(scene => PhysicalPath(scene.path)));
                executionProjection.ValidateOwnedTrees(token);
                GenerateSceneVisibilityContract.ApproveBefore(plan.sceneVisibilityBefore, GenerateSceneVisibilityContract.Capture());
                auditor = new ProjectAuditorReportScope(plan.auditorBaseline, plan.recoveryDirectory);
                scope = new PlayerCallbackExecutionScope(plan.callbacks, token, false);
                GenerateSceneVisibilityContract.ApproveBefore(plan.sceneVisibilityBefore, GenerateSceneVisibilityContract.Capture());
                Application.LogCallback observer = (message, trace, kind) => { if (kind == LogType.Error || kind == LogType.Exception || kind == LogType.Assert) Interlocked.Increment(ref evidence.generationLogErrors); };
                Application.logMessageReceivedThreaded += observer;
                try { evidence.chainEntered = true; EditorUtil.HybridCLR.GenerateAll(); }
                finally { Application.logMessageReceivedThreaded -= observer; }
                evidence.chainReturned = true;
                if (evidence.generationLogErrors != 0) throw new InvalidOperationException("Generate 全链出现真实错误日志；不将旧 DLL 或 CPP 作为本次成功证据。");
                report = BuildReport.GetLatestReport();
                string reportError = null;
                if (report == null || !ValidateGenerateReport(plan.previousReportGuid, report.summary.guid.ToString(), report.summary.platform.ToString(),
                        report.summary.outputPath, (int)report.summary.options, report.summary.totalErrors, report.summary.result.ToString(),
                        Path.Combine(SettingsUtil.HybridCLRDataDir, "StrippedAOTDllsTempProj", "WebGL"), expectedOptions, out reportError))
                    throw new InvalidOperationException(report == null ? "Generate 未产生新的正式 BuildReport。" : reportError);
                scope.Complete(report);
                evidence.reportGuid = report.summary.guid.ToString(); evidence.reportPlatform = report.summary.platform.ToString();
                evidence.reportOutput = Path.GetFullPath(report.summary.outputPath); evidence.reportOptions = (int)report.summary.options;
                evidence.reportErrors = report.summary.totalErrors; evidence.reportResult = report.summary.result.ToString();
                evidence.builtinUsageReport = scope.BuiltinUsageReport; evidence.buildEventsReport = scope.BuildEventsReport;
                evidence.emptyUsageReport = scope.EmptyUsageReport; evidence.finiteAdditionalReport = scope.FiniteAdditionalReport;
                evidence.packageCallbacksReport = scope.PackageCallbacksReport;
            }
            catch (Exception error) { failure = error; }
            finally
            {
                failure = DisposeGenerateScopePreservingTmp(evidence, scope, () => scope.TmpFontAfter, () => scope.TmpFontError, failure);
                // 紧邻同步链和真实scope释放采集；即使失败也保存实际观测，不用后来current补造。
                if (scope != null)
                {
                    try { evidence.sceneVisibilityAfter = GenerateSceneVisibilityContract.Capture(); }
                    catch (Exception error) { evidence.sceneVisibilityError = error.ToString(); failure = failure == null ? error : new AggregateException(failure, error); }
                }
                try
                {
                    evidence.stripAfter = HybridClrStripBuildContract.CaptureWebGL();
                    evidence.failureStateValid = HybridClrStripBuildContract.FailureEqual(plan.stripBefore, evidence.stripAfter) && PlayerSettings.WebGL.template == plan.webglTemplate;
                    if (!evidence.failureStateValid) throw new InvalidOperationException("Generate 失败/取消后的设置、模板或环境不属于原值/官方阶段转换；保留真实状态供归属核查。");
                    if (failure == null && !HybridClrStripBuildContract.AfterEqual(plan.stripBefore, evidence.stripAfter)) throw new InvalidOperationException("Generate 正常后态未满足精确官方环境转换。");
                }
                catch (Exception error) { failure = failure == null ? error : new AggregateException(failure, error); }
            }
            if (failure == null)
            {
                try
                {
                    EditorUtil.HybridCLR.ValidateLinkXml();
                    evidence.reportCopy = auditor == null ? null : await auditor.CompleteAsync(report);
                    if (evidence.reportCopy != null && !evidence.reportCopy.complete) throw new InvalidOperationException(evidence.reportCopy.error);
                    HybridClrGenerateInputProjection projection = CreateGenerateProjection(plan.masterLinkXml, plan.inputs.scenes.Select(scene => PhysicalPath(scene.path)).Concat(new[] { PhysicalPath(plan.inputs.masterPath), PhysicalPath(plan.inputs.runtimePath), PhysicalPath(plan.inputs.yooSettingsPath), PhysicalPath(plan.inputs.collectorPath) }).ToArray());
                    if (!TryCaptureInputs(BuildTarget.WebGL, Path.Combine(SettingsUtil.HybridCLRDataDir, "StrippedAOTDllsTempProj", "WebGL"), EditorUserBuildSettings.development, token,
                            out evidence.afterInputs, out string inputError, plan.callbacks.tmpFonts, projection,
                            GenerateSceneVisibilityContract.ApproveAfter(plan.sceneVisibilityBefore, evidence.sceneVisibilityAfter,
                                evidence.sceneVisibilityObserved = GenerateSceneVisibilityContract.Capture(), evidence.sceneVisibilityError)) || evidence.afterInputs.sha256 != plan.inputs.sha256)
                        throw new InvalidOperationException(inputError ?? "Generate 改变了拥有域之外的源输入或脏状态。");
                    ValidateStagingClean(plan.inputs);
                    evidence.afterCallbacks = CapturePlayerCallbacksObserved(BuildTarget.WebGL, evidence.afterInputs.dependencyPaths, token, true, plan.callbacks.tmpFonts, evidence.tmpFontAfter);
                    evidence.buildDataAfter = GenerateBuildDataAssemblyContract.Capture(evidence.afterCallbacks.packageCallbacks.domain, evidence.afterCallbacks.inputs);
                    evidence.aotDlls = CaptureAotDlls(plan.inputs, token);
                    evidence.ownedOutputs = CapturePaths(plan.owned.directories.Concat(plan.owned.files).Concat(plan.owned.files.Select(path => path + ".meta")), token);
                    evidence.generatedAssetIdentities = CaptureGeneratedAssetIdentities(plan.owned);
                    if (!ValidateGenerateCallbackEvidence(evidence, evidence.afterCallbacks, out string callbackError)) throw new InvalidOperationException(callbackError);
                    token.ThrowIfCancellationRequested();
                    auditor?.Commit();
                    evidence.complete = true;
                    evidence.preCompilationCertificate = ComputeGenerateCertificate(evidence);
                }
                catch (Exception error) { failure = error; }
            }
            if (failure != null)
            {
                evidence.complete = false;
                evidence.preCompilationCertificate = null;
                evidence.cancelled = failure is OperationCanceledException || failure is AggregateException aggregate && aggregate.Flatten().InnerExceptions.Any(error => error is OperationCanceledException);
                evidence.error = failure.ToString();
                if (auditor != null) evidence.reportCopy = await auditor.RecoverAsync(report, failure.Message);
            }
            return evidence;
        }

        /// <summary>恰一次释放当前真实scope，并在finally保存其已有TMP字段；所有清理/reader异常合并，不重捕或升级完成状态。</summary>
        /// <param name="evidence">本次尚未完成的材料载体；只写两个TMP观察字段，不改变complete或证书。</param>
        /// <param name="scope">生产仅传本次真实PlayerCallbackExecutionScope；未创建时为null且不读字段。</param>
        /// <param name="readStoredAfter">生产仅直接读取scope.TmpFontAfter的函数，不调用Capture或从before补值。</param>
        /// <param name="readStoredError">生产仅直接读取scope.TmpFontError的函数，不生成替代错误。</param>
        /// <param name="priorFailure">本次已有执行异常；后续异常只能合并，不能覆写或吞掉。</param>
        /// <returns>原异常或保留全部清理/reader异常的聚合；两个reader独立尝试，已取得的真实字段保持。</returns>
        internal static Exception DisposeGenerateScopePreservingTmp(GenerateEvidence evidence, IDisposable scope,
            Func<PlayerTmpFontBuildContract.Snapshot> readStoredAfter, Func<string> readStoredError, Exception priorFailure)
        {
            if (evidence == null) throw new ArgumentNullException(nameof(evidence));
            Exception failure = priorFailure;
            try { scope?.Dispose(); }
            catch (Exception error) { failure = failure == null ? error : new AggregateException(failure, error); }
            finally
            {
                if (scope != null)
                {
                    try { evidence.tmpFontAfter = readStoredAfter(); }
                    catch (Exception error) { failure = failure == null ? error : new AggregateException(failure, error); }
                    try { evidence.tmpFontError = readStoredError(); }
                    catch (Exception error) { failure = failure == null ? error : new AggregateException(failure, error); }
                }
            }
            return failure;
        }

        /// <summary>纯验证正式报告身份；DTO测试不构造 BuildReport，不制造回调观察。</summary>
        /// <param name="previousGuid">写前最近报告 GUID，允许空。</param>
        /// <param name="guid">自然 Generate 返回后报告 GUID。</param>
        /// <param name="platform">真实报告平台。</param>
        /// <param name="output">真实报告输出。</param>
        /// <param name="options">真实报告 options。</param>
        /// <param name="errors">真实报告错误总数。</param>
        /// <param name="result">真实报告结果。</param>
        /// <param name="expectedOutput">本次由设置计算的准确临时工程。</param>
        /// <param name="expectedOptions">已冻结的官方显式选项。</param>
        /// <param name="error">不符合之处。</param>
        /// <returns>新报告、平台、输出、选项及成功状态完全匹配时为 true。</returns>
        internal static bool ValidateGenerateReport(string previousGuid, string guid, string platform, string output, int options, int errors, string result, string expectedOutput, int expectedOptions, out string error)
        {
            error = null;
            if (!Guid.TryParse(guid, out Guid parsed) || parsed == Guid.Empty || string.Equals(previousGuid, guid, StringComparison.OrdinalIgnoreCase)) error = "Generate 没有新的唯一报告身份。";
            else if (platform != BuildTarget.WebGL.ToString() || options != expectedOptions || errors != 0 || result != BuildResult.Succeeded.ToString()) error = "Generate 正式报告平台、选项、结果或错误数不匹配。";
            else
            {
                try { if (string.IsNullOrWhiteSpace(output) || string.IsNullOrWhiteSpace(expectedOutput) || Path.GetFullPath(output).TrimEnd(Path.DirectorySeparatorChar) != Path.GetFullPath(expectedOutput).TrimEnd(Path.DirectorySeparatorChar)) error = "Generate 正式报告输出并非本次准确临时工程。"; }
                catch (Exception exception) when (exception is ArgumentException || exception is NotSupportedException || exception is PathTooLongException) { error = exception.Message; }
            }
            return error == null;
        }

        /// <summary>复用所有既有 typed after 与真实 usage/report 验证，不把只有字符串 hash 当作完整回调链。</summary>
        /// <param name="evidence">本次真实材料。</param>
        /// <param name="current">当前实际回调完整捕获。</param>
        /// <param name="error">缺失或漂移。</param>
        /// <returns>全部回调不变量、正常转换和报告材料均满足时为 true。</returns>
        private static bool ValidateGenerateCallbackEvidence(GenerateEvidence evidence, CallbackInputSnapshot current, out string error)
        {
            return ValidateGenerateCallbackEvidenceWithBuildData(evidence, current, ReferenceEquals(current, evidence?.afterCallbacks) ? evidence?.buildDataAfter : null, out error);
        }

        /// <summary>只在完整原回调门内接纳准确 DTO 的有限首次加载；所有 typed 后验仍执行原链。</summary>
        /// <param name="evidence">原 before 与真实 after，含独立文件/加载材料。</param><param name="current">当前完整真实回调。</param><param name="buildDataCurrent">与 current 同次的真实加载材料；历史复验仅使用原 after。</param><param name="error">具体失败，不补造缺失的旧材料。</param>
        /// <returns>原全部门及独立有限加载证明均通过时为 true。</returns>
        private static bool ValidateGenerateCallbackEvidenceWithBuildData(GenerateEvidence evidence, CallbackInputSnapshot current, GenerateBuildDataAssemblyContract.Snapshot buildDataCurrent, out string error)
        {
            error = null;
            CallbackInputSnapshot before = evidence?.before?.callbacks;
            CallbackInputSnapshot after = evidence?.afterCallbacks;
            if (before == null || after == null || current == null || !evidence.chainReturned || current.unsupported.Length != 0 || !GenerateBuildDataCallbackHashesEqual(evidence, current, buildDataCurrent) || after.sha256 != current.sha256 || evidence.tmpFontAfter == null || !string.IsNullOrEmpty(evidence.tmpFontError))
            { error = DescribeGenerateCallbackMismatch(evidence, before, after, current); return false; }
            return TryValidateNormalResetReceipt(before.normalResetStates, after.normalResetStates, current.normalResetStates, out error) &&
                PlayerTmpFontBuildContract.TryValidateAfter(before.tmpFonts, evidence.tmpFontAfter, after.tmpFonts, out error) &&
                PlayerTmpFontBuildContract.TryValidateAfter(before.tmpFonts, evidence.tmpFontAfter, current.tmpFonts, out error) &&
                PlayerFacebookBuildContract.TryValidateAfter(before.facebook, after.facebook, current.facebook, out error) &&
                PlayerBuiltinUsageBuildContract.TryValidateAfter(before.builtinUsage, after.builtinUsage, current.builtinUsage, out error) &&
                PlayerBuiltinUsageBuildContract.TryValidateReport(before.builtinUsage, evidence.builtinUsageReport, evidence.reportGuid, out error) &&
                PlayerBuildEventsContract.TryValidateAfter(before.buildEvents, after.buildEvents, current.buildEvents, out error) &&
                PlayerBuildEventsContract.TryValidateReport(before.buildEvents, evidence.buildEventsReport, evidence.reportGuid, out error) &&
                PlayerEmptyUsageBuildContract.TryValidateAfter(before.emptyUsage, after.emptyUsage, current.emptyUsage, out error) &&
                PlayerEmptyUsageBuildContract.TryValidateReport(before.emptyUsage, evidence.emptyUsageReport, evidence.reportGuid, out error) &&
                PlayerFiniteAdditionalBuildContract.TryValidateAfter(before.finiteAdditional, after.finiteAdditional, current.finiteAdditional, out error) &&
                PlayerFiniteAdditionalBuildContract.TryValidateReport(before.finiteAdditional, evidence.finiteAdditionalReport, evidence.reportGuid, out error) &&
                PlayerPackageCallbacksContract.TryValidateAfter(before.packageCallbacks, after.packageCallbacks, current.packageCallbacks, out error) &&
                PlayerPackageCallbacksContract.TryValidateReport(before.packageCallbacks, after.packageCallbacks, evidence.packageCallbacksReport, evidence.reportGuid, out error);
        }

        /// <summary>原总哈希算法全文不变；只有准确加载行可在独立 before 副本中成为预计 after。</summary>
        /// <param name="evidence">真实原始三态关联及文件绑定。</param><param name="current">本次完整实际回调。</param><param name="buildDataCurrent">同次当前文件/元数据。</param>
        /// <returns>旧无 typed 材料仅保原严格摘要相等；新材料需完整文件、行、域及全部十五部分摘要匹配。</returns>
        private static bool GenerateBuildDataCallbackHashesEqual(GenerateEvidence evidence, CallbackInputSnapshot current, GenerateBuildDataAssemblyContract.Snapshot buildDataCurrent)
        {
            CallbackInputSnapshot before = evidence.before.callbacks, after = evidence.afterCallbacks;
            if (evidence.before.buildDataBefore == null && evidence.buildDataAfter == null && buildDataCurrent == null) return before.sha256 == after.sha256;
            if (evidence.before.buildDataBefore?.domain != before.packageCallbacks?.domain || evidence.buildDataAfter?.domain != after.packageCallbacks?.domain || buildDataCurrent?.domain != current.packageCallbacks?.domain) return false;
            if (!GenerateBuildDataAssemblyContract.TryValidateAfter(evidence.before.buildDataBefore, evidence.buildDataAfter, buildDataCurrent, before.inputs, after.inputs, current.inputs, out _)) return false;
            // 新增许可只改变自有副本的 inputs；原捕获行、原 DTO 与原 ComputeCallbackContractHash 均不改写。
            var expected = new CallbackInputSnapshot
            {
                identities = before.identities, inputs = after.inputs.ToArray(), writes = before.writes,
                temporaryFiles = before.temporaryFiles, temporaryBaseline = before.temporaryBaseline,
                temporaryDirectoryIdentities = before.temporaryDirectoryIdentities, unsupported = before.unsupported, normalResetStates = before.normalResetStates,
                tmpFonts = before.tmpFonts, facebook = before.facebook, builtinUsage = before.builtinUsage, buildEvents = before.buildEvents,
                emptyUsage = before.emptyUsage, finiteAdditional = before.finiteAdditional, packageCallbacks = before.packageCallbacks,
            };
            return before.sha256 == ComputeCallbackContractHash(before) && after.sha256 == ComputeCallbackContractHash(after) &&
                current.sha256 == ComputeCallbackContractHash(current) && after.sha256 == ComputeCallbackContractHash(expected);
        }

        /// <summary>只解释初始回调门已经拒绝的同调用 DTO；不重捕、不调用总合同摘要、不输出任何原始行。</summary>
        /// <param name="evidence">当前调用已有的执行材料。</param>
        /// <param name="before">当前调用已经取出的原回调材料。</param>
        /// <param name="after">当前调用已经取出的执行后材料。</param>
        /// <param name="current">原校验入口收到的当前材料。</param>
        /// <returns>包含原失败前缀且最多 8KiB UTF8 的纯诊断；诊断异常时只返回原前缀。</returns>
        private static string DescribeGenerateCallbackMismatch(GenerateEvidence evidence, CallbackInputSnapshot before, CallbackInputSnapshot after, CallbackInputSnapshot current)
        {
            const string generic = "Generate 回调链、身份或不变量不完整。";
            try
            {
                var parts = new JArray(); var rows = new JArray(); int changedRows = 0;
                var result = new JObject
                {
                    ["beforePresent"] = before != null, ["afterPresent"] = after != null, ["currentPresent"] = current != null,
                    ["chainReturned"] = evidence != null && evidence.chainReturned,
                    ["unsupportedPresent"] = current?.unsupported != null, ["unsupportedCount"] = current?.unsupported?.Length ?? -1,
                    ["tmpAfterPresent"] = evidence?.tmpFontAfter != null, ["tmpErrorPresent"] = !string.IsNullOrEmpty(evidence?.tmpFontError),
                    ["beforeAfterHashEqual"] = before != null && after != null && before.sha256 == after.sha256,
                    ["afterCurrentHashEqual"] = after != null && current != null && after.sha256 == current.sha256,
                    ["storedHashes"] = new JArray(CallbackDiagnosticStoredHash(before), CallbackDiagnosticStoredHash(after), CallbackDiagnosticStoredHash(current)),
                };
                bool mismatch = before != null && after != null && current != null && (before.sha256 != after.sha256 || after.sha256 != current.sha256);
                result["partsCompared"] = mismatch;
                if (mismatch)
                {
                    // 顺序对应原 ComputeCallbackContractHash 的参与部分；只序列化准确 DTO，不触碰 ignored 引用。
                    AppendCallbackDiagnosticArray(parts, rows, "identities", () => before.identities, () => after.identities, () => current.identities, ref changedRows);
                    AppendCallbackDiagnosticArray(parts, rows, "inputs", () => before.inputs, () => after.inputs, () => current.inputs, ref changedRows);
                    AppendCallbackDiagnosticArray(parts, rows, "writes", () => before.writes, () => after.writes, () => current.writes, ref changedRows);
                    AppendCallbackDiagnosticArray(parts, rows, "temporaryFiles", () => before.temporaryFiles, () => after.temporaryFiles, () => current.temporaryFiles, ref changedRows);
                    AppendCallbackDiagnosticArray(parts, rows, "temporaryBaseline", () => before.temporaryBaseline, () => after.temporaryBaseline, () => current.temporaryBaseline, ref changedRows);
                    AppendCallbackDiagnosticArray(parts, rows, "temporaryDirectoryIdentities", () => before.temporaryDirectoryIdentities, () => after.temporaryDirectoryIdentities, () => current.temporaryDirectoryIdentities, ref changedRows);
                    AppendCallbackDiagnosticArray(parts, rows, "unsupported", () => before.unsupported, () => after.unsupported, () => current.unsupported, ref changedRows);
                    AppendCallbackDiagnosticArray(parts, rows, "normalResetStates", () => NormalizeNormalResetStates(before.normalResetStates), () => NormalizeNormalResetStates(after.normalResetStates), () => NormalizeNormalResetStates(current.normalResetStates), ref changedRows);
                    AppendCallbackDiagnosticTyped(parts, "TMP", () => PlayerTmpFontBuildContract.InvariantHash(before.tmpFonts), () => PlayerTmpFontBuildContract.InvariantHash(after.tmpFonts), () => PlayerTmpFontBuildContract.InvariantHash(current.tmpFonts));
                    AppendCallbackDiagnosticTyped(parts, "Facebook", () => PlayerFacebookBuildContract.InvariantHash(before.facebook), () => PlayerFacebookBuildContract.InvariantHash(after.facebook), () => PlayerFacebookBuildContract.InvariantHash(current.facebook));
                    AppendCallbackDiagnosticTyped(parts, "BuiltinUsage", () => PlayerBuiltinUsageBuildContract.InvariantHash(before.builtinUsage), () => PlayerBuiltinUsageBuildContract.InvariantHash(after.builtinUsage), () => PlayerBuiltinUsageBuildContract.InvariantHash(current.builtinUsage));
                    AppendCallbackDiagnosticTyped(parts, "BuildEvents", () => PlayerBuildEventsContract.InvariantHash(before.buildEvents), () => PlayerBuildEventsContract.InvariantHash(after.buildEvents), () => PlayerBuildEventsContract.InvariantHash(current.buildEvents));
                    AppendCallbackDiagnosticTyped(parts, "EmptyUsage", () => PlayerEmptyUsageBuildContract.InvariantHash(before.emptyUsage), () => PlayerEmptyUsageBuildContract.InvariantHash(after.emptyUsage), () => PlayerEmptyUsageBuildContract.InvariantHash(current.emptyUsage));
                    AppendCallbackDiagnosticTyped(parts, "FiniteAdditional", () => PlayerFiniteAdditionalBuildContract.InvariantHash(before.finiteAdditional), () => PlayerFiniteAdditionalBuildContract.InvariantHash(after.finiteAdditional), () => PlayerFiniteAdditionalBuildContract.InvariantHash(current.finiteAdditional));
                    AppendCallbackDiagnosticTyped(parts, "PackageCallbacks", () => PlayerPackageCallbacksContract.InvariantHash(before.packageCallbacks), () => PlayerPackageCallbacksContract.InvariantHash(after.packageCallbacks), () => PlayerPackageCallbacksContract.InvariantHash(current.packageCallbacks));
                }
                result["parts"] = parts; result["rows"] = rows; result["changedRows"] = changedRows;
                result["rowsComplete"] = changedRows == rows.Count; result["partial"] = parts.OfType<JObject>().Any(part => (bool)part["partial"]);
                string json = result.ToString(Newtonsoft.Json.Formatting.None);
                string message = generic + " callbackDiagnostic=" + json;
                if (System.Text.Encoding.UTF8.GetByteCount(message) <= 8 * 1024) return message;
                // 极端数字长度等超预算时整段省略；原失败前缀、完整诊断长度和摘要仍保留，不截断 JSON/UTF8。
                return generic + " callbackDiagnostic=" + new JObject
                {
                    ["partial"] = true, ["omitted"] = true, ["partCount"] = parts.Count, ["changedRows"] = changedRows,
                    ["diagnosticUtf8Bytes"] = System.Text.Encoding.UTF8.GetByteCount(json), ["diagnosticSha256"] = GenerateActionCommon.ComputeTextHash(json),
                }.ToString(Newtonsoft.Json.Formatting.None);
            }
            catch { return generic; }
        }

        /// <summary>只在原摘要为准确十六进制 SHA 时原样显示；其它任意值只显示完整摘要，不泄露字符串。</summary>
        /// <param name="snapshot">已有回调 DTO，可为空。</param>
        /// <returns>缺失标记、原值是否为 SHA 以及安全完整摘要。</returns>
        private static JObject CallbackDiagnosticStoredHash(CallbackInputSnapshot snapshot)
        {
            string value = snapshot?.sha256;
            bool valid = value != null && value.Length == 64 && value.All(c => c >= '0' && c <= '9' || c >= 'a' && c <= 'f' || c >= 'A' && c <= 'F');
            return new JObject { ["present"] = value != null, ["isSha256"] = valid, ["sha256"] = valid ? value : GenerateActionCommon.ComputeTextHash(value) };
        }

        /// <summary>摘要一个准确数组部分；比较原有序完整 JSON，仅前四条变化行公开索引和行摘要。</summary>
        /// <param name="parts">仅保存字段名、计数、摘要和完整性标志的诊断列表。</param>
        /// <param name="rows">跨全部部分共享最多四条行差异。</param>
        /// <param name="field">代码内固定的参与字段名。</param>
        /// <param name="before">原数组或原纯规范化的读取委托。</param>
        /// <param name="after">执行后数组或原纯规范化的读取委托。</param>
        /// <param name="current">当前数组或原纯规范化的读取委托。</param>
        /// <param name="changedRows">两个相邻阶段全部不同索引的累计数；不去重、不排序。</param>
        /// <returns>无返回值；单部分失败只标记 partial，不替换原失败。</returns>
        private static void AppendCallbackDiagnosticArray(JArray parts, JArray rows, string field, Func<object[]> before, Func<object[]> after, Func<object[]> current, ref int changedRows)
        {
            var getters = new[] { before, after, current }; var values = new object[3][]; var ok = new bool[3]; var hashes = new string[3];
            var states = new JArray();
            for (int phase = 0; phase < 3; phase++)
            {
                try
                {
                    values[phase] = getters[phase](); string json = Util.Json.Serialize(values[phase]); hashes[phase] = GenerateActionCommon.ComputeTextHash(json); ok[phase] = true;
                    states.Add(new JObject { ["present"] = values[phase] != null, ["count"] = values[phase]?.Length ?? -1, ["sha256"] = hashes[phase] });
                }
                catch { states.Add(new JObject { ["error"] = true }); }
            }
            var part = new JObject { ["field"] = field, ["states"] = states, ["partial"] = ok.Any(value => !value) };
            for (int pair = 0; pair < 2; pair++)
            {
                string label = pair == 0 ? "beforeAfter" : "afterCurrent";
                if (!ok[pair] || !ok[pair + 1]) continue;
                part[label + "Equal"] = string.Equals(hashes[pair], hashes[pair + 1], StringComparison.Ordinal);
                int count = 0; object[] left = values[pair], right = values[pair + 1];
                if (hashes[pair] != hashes[pair + 1])
                    for (int index = 0; index < Math.Max(left?.Length ?? 0, right?.Length ?? 0); index++)
                    {
                        bool leftPresent = left != null && index < left.Length, rightPresent = right != null && index < right.Length;
                        string leftJson = leftPresent ? Util.Json.Serialize(left[index]) : null, rightJson = rightPresent ? Util.Json.Serialize(right[index]) : null;
                        if (leftPresent == rightPresent && string.Equals(leftJson, rightJson, StringComparison.Ordinal)) continue;
                        count++;
                        if (rows.Count < 4) rows.Add(new JObject { ["field"] = field, ["pair"] = pair, ["index"] = index, ["leftPresent"] = leftPresent, ["rightPresent"] = rightPresent, ["leftSha256"] = GenerateActionCommon.ComputeTextHash(leftJson), ["rightSha256"] = GenerateActionCommon.ComputeTextHash(rightJson) });
                    }
                part[label + "ChangedRows"] = count; changedRows = checked(changedRows + count);
            }
            parts.Add(part);
        }

        /// <summary>逐阶段复用原七组纯 InvariantHash；任何异常仅记缺失，不输出异常正文或调用原总摘要。</summary>
        /// <param name="parts">仅安全摘要组成的列表。</param>
        /// <param name="field">代码内固定的 typed 组名。</param>
        /// <param name="before">原组的纯摘要。</param>
        /// <param name="after">执行后组的纯摘要。</param>
        /// <param name="current">当前组的纯摘要。</param>
        /// <returns>无返回值；不执行 typed after、getter、Capture 或 weak.Target。</returns>
        private static void AppendCallbackDiagnosticTyped(JArray parts, string field, Func<string> before, Func<string> after, Func<string> current)
        {
            var getters = new[] { before, after, current }; var states = new JArray(); var hashes = new string[3]; var ok = new bool[3];
            for (int phase = 0; phase < 3; phase++)
            {
                try { hashes[phase] = getters[phase](); ok[phase] = true; states.Add(new JObject { ["sha256"] = hashes[phase] }); }
                catch { states.Add(new JObject { ["error"] = true }); }
            }
            var part = new JObject { ["field"] = field, ["states"] = states, ["partial"] = ok.Any(value => !value) };
            if (ok[0] && ok[1]) part["beforeAfterEqual"] = hashes[0] == hashes[1];
            if (ok[1] && ok[2]) part["afterCurrentEqual"] = hashes[1] == hashes[2];
            parts.Add(part);
        }

        /// <summary>绑定请求正式编译之前已完成的真实输入、输出、环境、回调后验和报告；自身字段不参与递归摘要。</summary>
        /// <param name="evidence">Execute 完成全部后验后的真实材料；不是由 Verify 补造。</param>
        /// <returns>由既有 compilationPayloadHash 再整体绑定的本地完整材料摘要。</returns>
        internal static string ComputeGenerateCertificate(GenerateEvidence evidence) => GenerateActionCommon.ComputeTextHash(Util.Json.Serialize(new
        {
            evidence.complete, evidence.chainEntered, evidence.chainReturned, evidence.cancelled, evidence.error, evidence.generationLogErrors, evidence.before, evidence.afterInputs, evidence.afterCallbacks,
            evidence.stripAfter, evidence.failureStateValid, evidence.tmpFontAfter, evidence.tmpFontError, evidence.buildDataAfter,
            evidence.sceneVisibilityAfter, evidence.sceneVisibilityObserved, evidence.sceneVisibilityError, evidence.reportGuid, evidence.reportPlatform, evidence.reportOutput, evidence.reportOptions,
            evidence.reportErrors, evidence.reportResult, evidence.aotDlls, evidence.ownedOutputs, evidence.generatedAssetIdentities, evidence.reportCopy,
            evidence.builtinUsageReport, evidence.buildEventsReport, evidence.emptyUsageReport, evidence.finiteAdditionalReport, evidence.packageCallbacksReport,
        }));

        /// <summary>验证已绑定历史的真实后验，再区分原域复查与完成编译后的新域持久义务。</summary>
        /// <param name="evidence">Execute 在请求编译前完整核对并写入编译载荷的材料。</param>
        /// <param name="current">Verify 新捕获的全部当前回调。</param>
        /// <param name="buildDataCurrent">同次当前域准确文件/元数据。</param><param name="firstReloadApproved">原正式 SVS 入口已验证原 token/payload 的真实首次域，不由域差推断。</param>
        /// <param name="error">历史不自洽、身份变化或当前持久配置漂移。</param>
        /// <returns>历史真实证据完整，且当前域满足准确配置投影时为 true。</returns>
        private static bool ValidateGenerateReloadEvidence(GenerateEvidence evidence, CallbackInputSnapshot current, GenerateBuildDataAssemblyContract.Snapshot buildDataCurrent, bool firstReloadApproved, out string error)
        {
            if (!ValidateGenerateCallbackEvidence(evidence, evidence?.afterCallbacks, out error)) return false;
            CallbackInputSnapshot after = evidence.afterCallbacks;
            if (after.packageCallbacks?.domain == current?.packageCallbacks?.domain)
                return ValidateGenerateCallbackEvidenceWithBuildData(evidence, current, buildDataCurrent, out error);
            if (string.IsNullOrEmpty(after.packageCallbacks?.domain) || string.IsNullOrEmpty(current?.packageCallbacks?.domain) || current.unsupported.Length != 0)
            { error = "Generate 重载域身份或当前自然准入不完整。"; return false; }
            if (!ValidateGenerateFreshDomainCaches(after, current, out error)) return false;
            string[] persistentCurrentInputs = current.inputs;
            if (evidence.before.buildDataBefore != null || evidence.buildDataAfter != null || buildDataCurrent != null)
            {
                if (evidence.buildDataAfter?.domain != after.packageCallbacks.domain || buildDataCurrent?.domain != current.packageCallbacks.domain)
                { error = "Generate DTO 文件加载域与同次实际回调域不一致。"; return false; }
                if (!GenerateBuildDataAssemblyContract.TryGetReloadComparison(evidence.buildDataAfter, buildDataCurrent, after.inputs, current.inputs, firstReloadApproved, out persistentCurrentInputs, out error)) return false;
            }
            // 当前 Capture 已重新执行每一项源码/DLL/自然分支门；只比较持久义务，不把旧域状态冒充新域 observed。
            string oldCore = Util.Json.Serialize(new { after.identities, after.inputs, after.writes, after.temporaryFiles, after.temporaryBaseline, after.temporaryDirectoryIdentities, after.unsupported, normal = NormalizeNormalResetStates(after.normalResetStates) });
            string newCore = Util.Json.Serialize(new { current.identities, inputs = persistentCurrentInputs, current.writes, current.temporaryFiles, current.temporaryBaseline, current.temporaryDirectoryIdentities, current.unsupported, normal = NormalizeNormalResetStates(current.normalResetStates) });
            if (oldCore != newCore || GenerateReloadPersistentHash(after) != GenerateReloadPersistentHash(current))
            { error = "Generate 重载后源码、回调集合、临时文件或持久配置漂移。"; return false; }
            error = null;
            return true;
        }

        /// <summary>新域静态缓存必须仍属准确初始或已审自然值，不能只删旧 hash 后接纳任意新业务缓存。</summary>
        /// <param name="after">Execute 已认证的原域实际后态。</param>
        /// <param name="current">当前完整 Capture 通过后的新域状态。</param>
        /// <param name="error">未知缓存、额外构建或活跃借用的具体原因。</param>
        /// <returns>Shader/UGUI/IAP 与空池均满足准确新域范围时为 true。</returns>
        private static bool ValidateGenerateFreshDomainCaches(CallbackInputSnapshot after, CallbackInputSnapshot current, out string error)
        {
            error = null;
            PlayerPackageCallbacksContract.Snapshot package = current.packageCallbacks;
            if (package.shaderRows?.Length > 0 || package.computeRows?.Length > 0 || package.ugui != null)
                error = "Generate 新域存在后续 Shader/UGUI 构建缓存，不能将它当作本次旧域 after。";
            else if (package.iapConfig != null && package.iapConfig != after.packageCallbacks.iapConfig && package.iapConfig != package.iapExpectedConfig)
                error = "Generate 新域 IAP 配置缓存不是空值或已认证的准确配置。";
            PlayerBuiltinUsageBuildContract.Snapshot builtin = current.builtinUsage;
            if (error == null && builtin.insights && !(!builtin.hasCheckedCreateProject && !builtin.isCreateProject && (builtin.predicate == null || builtin.predicate == "null") ||
                builtin.hasCheckedCreateProject && builtin.isCreateProject == builtin.commandCreateProject && builtin.predicate == after.builtinUsage.predicate))
                error = "Generate 新域 Insights 缓存不是准确初态或已审命令行正常后态。";
            PlayerBuildEventsContract.Snapshot events = current.buildEvents;
            if (error == null && events.applicable && !(!events.eventSent && events.cachedSceneCount == 0 && events.cached2dCount == 0 || events.eventSent && events.cachedSceneCount == events.sceneCount && events.cached2dCount == events.scene2dCount))
                error = "Generate 新域 BuildEvents 统计不是准确初态或当前场景正常后态。";
            PlayerEmptyUsageBuildContract.Snapshot empty = current.emptyUsage;
            if (error == null && empty.srp && (empty.rows == null || empty.rows.Length != 0 || empty.countAll != 0))
                error = "Generate 新域 SRP 空池有未知容量、活跃借用或多个列表。";
            if (error == null && empty.adaptive)
            {
                Type store = FindType("UnityEditor.AdaptivePerformance.Editor.Metadata.AdaptivePerformancePackageMetadataStore");
                object cached = GenerateBacking(store, "s_CachedMDStoreInformation", null);
                Type type = store.GetNestedType("CachedMDStoreInformation", System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.NonPublic);
                Type known = store.GetNestedType("KnownPackageInfo", System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.NonPublic);
                if (cached?.GetType() != type || type == null || !type.IsValueType || known == null || type.GetFields(System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance).Length != 4 ||
                    type.GetField("hasAlreadyRequestedData")?.FieldType != typeof(bool) || type.GetField("knownPackageInfos")?.FieldType != known.MakeArrayType() || type.GetField("installedPackages")?.FieldType != typeof(string[]) || type.GetField("installablePackages")?.FieldType != typeof(string[]) ||
                    !Equals(GenerateBacking(type, "hasAlreadyRequestedData", cached), false) || GenerateBacking(type, "knownPackageInfos", cached) != null || GenerateBacking(type, "installedPackages", cached) != null || GenerateBacking(type, "installablePackages", cached) != null)
                    error = "Generate 新域 Adaptive metadata 缓存不是已审四字段默认 struct；不读取 SessionState 或补初始化。";
            }
            PlayerFiniteAdditionalBuildContract.Snapshot finite = current.finiteAdditional;
            if (error == null && finite.stripper && (finite.stripperGlobalDirty || finite.stripperGraphicsDirty || finite.poolRows == null || finite.poolRows.Length != 0 || finite.fresh != null || finite.poolCountAll != 0))
                error = "Generate 新域 GlobalSettingsStripper 池不是自然初始空池。";
            return error == null;
        }

        /// <summary>逐字段排除已审域瞬态；未列字段全部保留，新增字段也不会被泛化 identity 规则删除。</summary>
        /// <param name="snapshot">完整真实旧域 after 或新域当前材料。</param>
        /// <returns>保留原资产、JSON、文件、GUID、配置和完整分支集合的摘要。</returns>
        internal static string GenerateReloadPersistentHash(CallbackInputSnapshot snapshot)
        {
            if (snapshot == null) throw new InvalidOperationException("Generate 重载当前材料缺失。");
            JObject tmp = JObject.FromObject(snapshot.tmpFonts, new Newtonsoft.Json.JsonSerializer { NullValueHandling = Newtonsoft.Json.NullValueHandling.Include, DefaultValueHandling = Newtonsoft.Json.DefaultValueHandling.Include });
            if (tmp["runtime"] is JObject runtime)
            {
                RemoveGenerateTransient(runtime, "managerPresent", "managerIdentity", "ownCameraCallbacks", "ownCanvasCallbacks", "ownRenderCallbacks", "updateQueue", "resourceOwnRegistered", "weakOwnRegistered");

            }
            JObject facebook = JObject.FromObject(snapshot.facebook, new Newtonsoft.Json.JsonSerializer { NullValueHandling = Newtonsoft.Json.NullValueHandling.Include, DefaultValueHandling = Newtonsoft.Json.DefaultValueHandling.Include });
            RemoveGenerateTransient(facebook, "cachedPresent", "cachedIdentity", "cachedInstanceId");
            JObject builtin = JObject.FromObject(snapshot.builtinUsage, new Newtonsoft.Json.JsonSerializer { NullValueHandling = Newtonsoft.Json.NullValueHandling.Include, DefaultValueHandling = Newtonsoft.Json.DefaultValueHandling.Include });
            RemoveGenerateTransient(builtin, "hasCheckedCreateProject", "isCreateProject", "predicate", "predicateOwner");
            JObject events = JObject.FromObject(snapshot.buildEvents, new Newtonsoft.Json.JsonSerializer { NullValueHandling = Newtonsoft.Json.NullValueHandling.Include, DefaultValueHandling = Newtonsoft.Json.DefaultValueHandling.Include });
            RemoveGenerateTransient(events, "moduleIdentity", "targetIdentity", "derivedProviderIdentity", "moduleMapIdentity", "derivedMapIdentity", "eventSent", "cachedSceneCount", "cached2dCount");
            JObject empty = JObject.FromObject(snapshot.emptyUsage, new Newtonsoft.Json.JsonSerializer { NullValueHandling = Newtonsoft.Json.NullValueHandling.Include, DefaultValueHandling = Newtonsoft.Json.DefaultValueHandling.Include });
            RemoveGenerateTransient(empty, "poolIdentity", "stackIdentity", "countAll", "rows", "metadataIdentity", "addRequestsIdentity", "metadataCacheSha256", "comparison", "comparisonOwner");
            JObject finite = JObject.FromObject(snapshot.finiteAdditional, new Newtonsoft.Json.JsonSerializer { NullValueHandling = Newtonsoft.Json.NullValueHandling.Include, DefaultValueHandling = Newtonsoft.Json.DefaultValueHandling.Include });
            RemoveGenerateTransient(finite, "stripperGlobalDirty", "stripperGraphicsDirty", "poolIdentity", "listIdentity", "poolCountAll", "listCapacity", "fresh", "poolRows", "inputManagerIdentity", "inputRuntimeIdentity");
            JObject package = JObject.FromObject(snapshot.packageCallbacks, new Newtonsoft.Json.JsonSerializer { NullValueHandling = Newtonsoft.Json.NullValueHandling.Include, DefaultValueHandling = Newtonsoft.Json.DefaultValueHandling.Include });
            RemoveGenerateTransient(package, "domain", "proBuilderSettingsIdentity", "auditorPreferenceIdentity", "proBuilderRepositoriesSha256", "features", "featureListIdentity", "shaderDictionaryIdentity", "computeDictionaryIdentity", "shaderRows", "computeRows", "iapConfig", "ugui");
            return GenerateActionCommon.ComputeTextHash(Util.Json.Serialize(new { tmp, facebook, builtin, events, empty, finite, package }));
        }

        /// <summary>删除本调用点明确命名的域瞬态字段；字段不存在视作结构漂移并拒绝。</summary>
        /// <param name="value">独立 JSON 副本，不修改真实快照。</param>
        /// <param name="fields">源码中逐字列举并审查的域瞬态字段。</param>
        /// <returns>无返回值；不接受前缀、后缀、类型名或反射通配删除。</returns>
        private static void RemoveGenerateTransient(JObject value, params string[] fields)
        {
            foreach (string field in fields)
                if (value.Property(field) == null) throw new InvalidOperationException("Generate 重载字段结构变化：" + field);
                else value.Remove(field);
        }

        /// <summary>执行后及编译稳定后的 Verify 复用相同源投影和 typed 材料，不重新生成或调用报告处理器。</summary>
        /// <param name="evidence">已完成的本次回执。</param>
        /// <param name="expectedOptions">本次官方显式选项。</param>
        /// <param name="token">当前只读验证取消令牌。</param>
        /// <param name="error">缺失、漂移或官方复制未完成的信息。</param>
        /// <returns>所有源、输出、报告和回调均保持时为 true。</returns>
        internal static bool VerifyHybridClrGenerate(GenerateEvidence evidence, int expectedOptions, CancellationToken token, out string error) => VerifyHybridClrGenerateObserved(evidence, expectedOptions, token, out error, null, null, out _);

        /// <summary>正式新域Verify同时返回真实SVS完整后验，原四参数调用仍默认无跨域许可。</summary>
        /// <param name="evidence">原Store读取的完整历史。</param><param name="expectedOptions">原正式build选项。</param><param name="token">本次取消令牌。</param><param name="error">准确失败。</param><param name="compilationToken">原Receipt编译令牌。</param><param name="compilationPayloadHash">原Receipt完整载荷SHA。</param><param name="observation">完整首次/当前SVS后验；成功之前不对外授予结果。</param>
        /// <returns>全部原生成门与首次域SVS门通过时为真。</returns>
        internal static bool VerifyHybridClrGenerateObserved(GenerateEvidence evidence, int expectedOptions, CancellationToken token, out string error, string compilationToken, string compilationPayloadHash, out GenerateSceneVisibilityContract.CompilationObservation observation)
        {
            error = null; observation = null;
            try
            {
                if (evidence?.complete != true || evidence.error != null || evidence.before == null) { error = "Generate adapter 未完整完成。"; return false; }
                if (string.IsNullOrEmpty(evidence.preCompilationCertificate) || evidence.preCompilationCertificate != ComputeGenerateCertificate(evidence))
                { error = "Generate 编译前真实后验证书缺失或漂移。"; return false; }
                GeneratePlan before = evidence.before;
                if (evidence.sceneVisibilityError != null) { error = "Generate SVS 原scope捕获失败：" + evidence.sceneVisibilityError; return false; }
                if (!GenerateSceneVisibilityContract.TryValidateAfter(before.sceneVisibilityBefore, evidence.sceneVisibilityAfter, evidence.sceneVisibilityObserved, out error)) return false;
                HybridClrGenerateInputProjection projection = CreateGenerateProjection(before.masterLinkXml, before.inputs.scenes.Select(scene => PhysicalPath(scene.path)).Concat(new[] { PhysicalPath(before.inputs.masterPath), PhysicalPath(before.inputs.runtimePath), PhysicalPath(before.inputs.yooSettingsPath), PhysicalPath(before.inputs.collectorPath) }).ToArray());
                var frozenProjection = new HybridClrGenerateInputProjection(before.owned, before.inputs.scenes.Select(scene => PhysicalPath(scene.path)));
                if (projection.Identity() != frozenProjection.Identity()) { error = "Generate 输出角色设置漂移。"; return false; }
                GenerateSceneVisibilityContract.ValidatedCapture svs = GenerateSceneVisibilityContract.ApproveAfterCompilationObserved(before.sceneVisibilityBefore, evidence.sceneVisibilityAfter, evidence.sceneVisibilityObserved,
                    GenerateSceneVisibilityContract.Capture(), evidence.sceneVisibilityError, compilationToken, compilationPayloadHash);
                observation = svs.compilationObservation;
                if (!TryCaptureInputs(BuildTarget.WebGL, Path.Combine(SettingsUtil.HybridCLRDataDir, "StrippedAOTDllsTempProj", "WebGL"), EditorUserBuildSettings.development, token,
                        out PlayerInputSnapshot inputs, out error, before.callbacks.tmpFonts, projection, svs) || inputs.sha256 != before.inputs.sha256) { error = error ?? "Generate 原始输入在执行后或编译后漂移。"; return false; }
                if (!HybridClrStripBuildContract.AfterEqual(before.stripBefore, evidence.stripAfter) || !HybridClrStripBuildContract.BeforeEqual(evidence.stripAfter, HybridClrStripBuildContract.CaptureWebGL()) || PlayerSettings.WebGL.template != before.webglTemplate)
                { error = "Generate 官方后态在编译后漂移。"; return false; }
                ValidateStagingClean(before.inputs);
                CallbackInputSnapshot callbacks = CapturePlayerCallbacksObserved(BuildTarget.WebGL, inputs.dependencyPaths, token, true, before.callbacks.tmpFonts, evidence.tmpFontAfter);
                GenerateBuildDataAssemblyContract.Snapshot buildDataCurrent = before.buildDataBefore == null && evidence.buildDataAfter == null ? null : GenerateBuildDataAssemblyContract.Capture(callbacks.packageCallbacks.domain, callbacks.inputs);
                if (!ValidateGenerateReloadEvidence(evidence, callbacks, buildDataCurrent, observation?.compilation != null, out error)) return false;
                if (!ValidateGenerateReport(before.previousReportGuid, evidence.reportGuid, evidence.reportPlatform, evidence.reportOutput, evidence.reportOptions, evidence.reportErrors, evidence.reportResult,
                        Path.Combine(SettingsUtil.HybridCLRDataDir, "StrippedAOTDllsTempProj", "WebGL"), expectedOptions, out error)) return false;
                if (Util.Json.Serialize(evidence.aotDlls) != Util.Json.Serialize(CaptureAotDlls(before.inputs, token)) ||
                    Util.Json.Serialize(evidence.ownedOutputs) != Util.Json.Serialize(CapturePaths(before.owned.directories.Concat(before.owned.files).Concat(before.owned.files.Select(path => path + ".meta")), token)) ||
                    !evidence.generatedAssetIdentities.SequenceEqual(CaptureGeneratedAssetIdentities(before.owned), StringComparer.Ordinal))
                { error = "Generate 本次输出、AOT复制或生成源码/meta/GUID已漂移。"; return false; }
                if (evidence.reportCopy != null && evidence.reportCopy.reportGuid != evidence.reportGuid) { error = "Generate 报告复制身份不一致。"; return false; }
                if (!VerifyAuditorReceipt(evidence.reportCopy, out error)) return false;
                if (observation != null) observation.adapterVerified = true; return true;
            }
            catch (Exception exception) when (!(exception is OperationCanceledException)) { error = exception.Message; return false; }
            finally { if (observation != null) observation.adapterError = error; }
        }

        /// <summary>记录生成源码与双保留表的实际逻辑路径和 GUID，独立于保留输入的依赖投影。</summary>
        /// <param name="owned">本次精确角色。</param>
        /// <returns>排序后的实际身份；文件与 meta 字节另由 ownedOutputs 核对。</returns>
        private static string[] CaptureGeneratedAssetIdentities(HybridClrGenerateInputProjection.Spec owned) => owned.files.Select(path => Path.GetRelativePath(BuildActionCommon.ProjectRoot, path).Replace('\\', '/'))
            .OrderBy(path => path, StringComparer.Ordinal).Select(path => path + "|" + AssetDatabase.AssetPathToGUID(path)).ToArray();

        /// <summary>合并已有 Player 回调的准确写域与本次正式报告/备份位置。</summary>
        /// <param name="plan">已冻结计划。</param>
        /// <returns>只描述本次允许域；不表示生成已发生。</returns>
        internal static string[] GetGenerateCallbackWriteSet(GeneratePlan plan) => plan.callbacks.writes.Concat(plan.owned.files.Select(path => path + ".meta")).Concat(plan.owned.compiledArtifacts ?? Array.Empty<string>())
            .Concat(new[] { plan.inputs.stagingTarget, plan.inputs.stagingTarget + ".meta", YooAssetRuntimeSettingsStaging.GetMarkerFilePath(), "Library/LastBuild.buildreport", "Library/LastCleanBuild.buildreport", plan.recoveryDirectory, Path.GetDirectoryName(plan.recoveryDirectory), Path.Combine(Path.GetDirectoryName(plan.recoveryDirectory), "before.json"), Path.Combine(Path.GetDirectoryName(plan.recoveryDirectory), "final.json"),
                plan.sceneVisibilityBefore.cache.path, "SVS full before/query-span/scope/current native evidence; finite empty cache rows and first formal reload owner recreation only", "SVS natural dirty true->false and exact Library cache writes only during Generate; reload may append current Preview Picking empty rows; public flag queries may append only Visibility empty rows; no setter/clear/Save/overwrite",
                "Temp/BurstOutput", "Temp/StagingArea", "Library/BuildPlayerData", "PlayerSettings.WebGL.template (only existing Generate wrapper exact temporary override, restored)", "UNITY_IL2CPP_PATH + _CL_ (official typed persistent transition)" }).Distinct(StringComparer.Ordinal).ToArray();
    }
}
