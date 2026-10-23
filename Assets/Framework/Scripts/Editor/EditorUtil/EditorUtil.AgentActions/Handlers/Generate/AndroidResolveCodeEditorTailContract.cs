/***************************************************************
 * (c) copyright 2026 - 2030, Solotopia
 * All Rights Reserved.
 * -------------------------------------------------------------
 * filename:  AndroidResolveCodeEditorTailContract.cs
 * author:    taoye
 * created:   2026/10/16
 * descrip:   当前 Cursor 工程同步尾部的自然无生成支路与既有 usage 效果合同
 ***************************************************************/

using System;
using System.Collections;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Runtime.CompilerServices;
using System.Threading;
using System.Text.RegularExpressions;
using NovaFramework.Runtime;
using UnityEditor;
using Path = System.IO.Path;

namespace NovaFramework.Editor
{
    /// <summary>只覆盖根已实核的现存 Cursor 实例；不调用 CodeEditor getter、发现、生成、同步、注册或打开方法。</summary>
    internal static class AndroidResolveCodeEditorTailContract
    {
        private const string CoreSha256 = "14a25302095cb3c648b4b83b9936ba2df10454ae254da6440a1a968395f2d581";
        private const string CoreMvid = "11f76f7e-1291-4a87-8174-9af3fa5b621a";
        private const string CursorSha256 = "aac6fe6cb7aaeca4ddaa9f81c8f446006949eeb6b8709b8370525f8648e4f6b7";
        private const string CursorMvid = "e0909c3c-418e-4d10-b418-a76b1bafec60";
        private const string CursorPrefix = "Microsoft.Unity.VisualStudio.Editor.";
        private const BindingFlags Fields = BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.DeclaredOnly;
        private static readonly ConditionalWeakTable<object, IdentityToken> Identities = new ConditionalWeakTable<object, IdentityToken>();
        private static readonly string[] VscodeNames = { "extensions.json", "settings.json", "launch.json" };
        private static readonly string[] VscodeHashes =
        {
            "99ad80dd86d99326e354f18a4a7f77a38434d39dad9c3503b3a9594e08bb01db",
            "ed52fbbd9ba0902fbc758f4c57e0a39ddc15555109287592d3471ab51fc42ddc",
            "314af30527d85081412909337f20badb462068cfd21ce03a89e406df22e38afa",
        };

        /// <summary>供主 Action knownEffects 明示既有引擎 usage 的条件 API 调用；不宣称网络送达。</summary>
        internal const string UsageKnownEffect = "Unity 导入清理在现有 EditorAnalytics.enabled 开启且实际 postProcessorCalls 非空时，可能调用既有 assetPostProcessorsUsage v2 原生发送入口（选项0）；载荷仅 importActionId 和 methodName/duration_sec/invocationCount。本回执不证明网络送达，不修改 analytics 开关。";

        /// <summary>脱敏稳定快照；既有生成器刷新安装引用、替换扩展 HashSet 和普通编译器委托缓存不进入稳定身份。</summary>
        [Serializable]
        internal sealed class Snapshot
        {
            public bool editorAnalyticsEnabled;
            public string objectIdentitySha256;
            public string selectionSha256;
            public string extensionsSha256;
            public string potentialDispatchPathsSha256;
            public string vscodeFilesSha256;
            public string sha256;
        }

        /// <summary>本类持有的域内弱身份，不持有或修改 vendor 实例。</summary>
        private sealed class IdentityToken
        {
            internal readonly string value = Guid.NewGuid().ToString("N");
        }

        /// <summary>双读当前已初始化实例与精确模板路径角色，冻结当前正规 Cursor 自然无生成支路。</summary>
        /// <param name="exactWritePaths">主刷新合同已经审查的精确写集；本方法不授予路径写权限。</param>
        /// <param name="token">只读取消标记。</param>
        /// <param name="snapshot">成功时返回仅含摘要和布尔的稳定快照。</param>
        /// <param name="error">未知实现、偏好、发现状态、扩展或文件条件的具体阻断原因。</param>
        /// <returns>当前同域对象、设置、已审配置文件和路径排除条件均稳定时为 true。</returns>
        internal static bool TryCapture(string[] exactWritePaths, CancellationToken token, out Snapshot snapshot, out string error)
        {
            snapshot = null;
            error = null;
            try
            {
                if (!TryGetPotentialDispatchPaths(exactWritePaths, out string[] potential, out error)) return false;
                token.ThrowIfCancellationRequested();
                Snapshot first = CaptureOnce(potential, token);
                Snapshot second = CaptureOnce(potential, token);
                Require(first.sha256 == second.sha256, "工程同步尾部的只读输入在读取期间漂移。");
                snapshot = second;
                return true;
            }
            catch (Exception exception) when (!(exception is OperationCanceledException))
            {
                error = exception is InvalidOperationException ? exception.Message :
                    "冻结工程同步尾部失败：" + (exception.InnerException?.GetType().Name ?? exception.GetType().Name);
                return false;
            }
        }

        /// <summary>比较同域现存对象、设置、精确资产角色和自然无写固定点，不把已审内存缓存更新视作永久漂移。</summary>
        /// <param name="before">Plan 或写前快照。</param>
        /// <param name="after">重新捕获的当前快照。</param>
        /// <returns>两者自洽且完整稳定摘要相等时为 true。</returns>
        internal static bool Matches(Snapshot before, Snapshot after)
        {
            return IsSelfConsistent(before) && IsSelfConsistent(after) && before.sha256 == after.sha256;
        }

        /// <summary>检查脱敏快照必需字段与完整摘要，不读取任何引擎或 vendor 状态。</summary>
        /// <param name="snapshot">待核验快照。</param>
        /// <returns>全部必需字段存在且摘要自洽时为 true。</returns>
        internal static bool IsSelfConsistent(Snapshot snapshot)
        {
            return snapshot != null && new[] { snapshot.objectIdentitySha256, snapshot.selectionSha256,
                snapshot.extensionsSha256, snapshot.potentialDispatchPathsSha256, snapshot.vscodeFilesSha256, snapshot.sha256 }
                .All(value => !string.IsNullOrEmpty(value)) && snapshot.sha256 == SnapshotHash(snapshot);
        }

        /// <summary>按角色构造保守的潜在派发资产路径域；不伪造实际数组，也不按文件是否仍存在丢弃删除路径。</summary>
        /// <param name="exactWritePaths">已经由主合同冻结的精确模板写集。</param>
        /// <param name="potentialPaths">Assets 主资产和每个可能父目录的排序去重集合；.meta 只映射一次主资产。</param>
        /// <param name="error">非资产角色不明、越界字面量或未知路径格式的阻断原因。</param>
        /// <returns>只含正式模板域已知角色时为 true。</returns>
        internal static bool TryGetPotentialDispatchPaths(string[] exactWritePaths, out string[] potentialPaths, out string error)
        {
            potentialPaths = null;
            error = null;
            if (exactWritePaths == null || exactWritePaths.Length == 0)
            { error = "工程同步尾部缺少主合同精确写集。"; return false; }
            var result = new SortedSet<string>(StringComparer.Ordinal);
            foreach (string raw in exactWritePaths)
            {
                if (raw == AndroidResolveActionCommon.ResolutionStatePath) continue;
                if (string.IsNullOrEmpty(raw) || !raw.StartsWith("Assets/", StringComparison.Ordinal) ||
                    raw.Contains("..") || raw.Contains("\\") || raw.Contains("*") || raw.Contains("（"))
                { error = "工程同步尾部含未知派发角色或非精确资产路径。"; return false; }
                string path = raw.EndsWith(".meta", StringComparison.Ordinal) ? raw.Substring(0, raw.Length - 5) : raw;
                if (path.EndsWith(".meta", StringComparison.Ordinal) || path.EndsWith("/", StringComparison.Ordinal))
                { error = "工程同步尾部含重复元数据后缀或未规范目录路径。"; return false; }
                result.Add(path);
            }
            if (result.Count == 0) { error = "工程同步尾部没有任何冻结的潜在资产路径。"; return false; }
            potentialPaths = result.ToArray();
            return true;
        }

        /// <summary>按当前 Cursor 的真实扩展算法排除工程重建、dll/asmdef 重导和 pdb 检查；保留无扩展父目录的完整 key。</summary>
        /// <param name="potentialPaths">只含主合同角色映射后的精确潜在资产路径。</param>
        /// <param name="userExtensions">当前公开项目自定义扩展值，不改写或规范化这些设置。</param>
        /// <param name="builtinExtensions">当前公开内建扩展值。</param>
        /// <param name="error">确实可能进入生成或额外导入分支的路径原因。</param>
        /// <returns>所有可能资产角色均被正规路径过滤排除时为 true。</returns>
        internal static bool ArePathsExcluded(string[] potentialPaths, string[] userExtensions, string[] builtinExtensions, out string error)
        {
            error = null;
            if (potentialPaths == null || userExtensions == null || builtinExtensions == null)
            { error = "工程同步尾部缺少完整路径或扩展设置。"; return false; }
            var supported = new HashSet<string>(userExtensions.Concat(builtinExtensions));
            foreach (string path in potentialPaths)
            {
                if (string.IsNullOrEmpty(path)) { error = "工程同步尾部含空资产路径。"; return false; }
                string key = Path.HasExtension(path) ? Path.GetExtension(path).TrimStart('.').ToLower() : path;
                string extension = Path.GetExtension(path);
                if (key == "dll" || key == "asmdef" || supported.Contains(key) || extension == ".pdb")
                { error = "当前 Cursor 扩展设置会处理真实潜在资产路径，不能保证无工程生成：" + path; return false; }
            }
            return true;
        }

        /// <summary>只读一次当前 Cursor 的实际选择、已完成安装缓存、既有生成器和本机已审配置字节。</summary>
        /// <param name="potential">已角色化的精确潜在派发路径。</param>
        /// <param name="token">读取取消标记。</param>
        /// <returns>稳定、脱敏快照；不包含普通缓存替换的对象身份。</returns>
        private static Snapshot CaptureOnce(string[] potential, CancellationToken token)
        {
            token.ThrowIfCancellationRequested();
            Assembly core = typeof(AssetDatabase).Assembly;
            Require(core.ManifestModule.ModuleVersionId.ToString("D") == CoreMvid &&
                GenerateActionCommon.ComputeFileHash(core.Location, token) == CoreSha256, "工程同步尾部 CoreModule 身份漂移。");
            Type codeEditor = core.GetType("Unity.CodeEditor.CodeEditor", true);
            // 根在当前会话已实核该类型与实例完成初始化；不得将本方法用作尚未初始化类型的初始化探针。
            object editor = Read(codeEditor, "<Editor>k__BackingField");
            object current = Read(codeEditor, "m_CurrentEditor", editor);
            object fallback = Read(codeEditor, "m_DefaultEditor", editor);
            Require(editor?.GetType() == codeEditor && current != null && !ReferenceEquals(current, fallback),
                "当前 CodeEditor 为默认/空实例，可能进入自动选择与初始化，当前 Cursor 合同不接纳。");
            Type currentType = current.GetType();
            Assembly cursor = currentType.Assembly;
            Require(currentType.FullName == CursorPrefix + "VisualStudioEditor" && cursor.GetName().Name == "Unity.Cursor.Editor" &&
                cursor.ManifestModule.ModuleVersionId.ToString("D") == CursorMvid &&
                Path.GetFullPath(cursor.Location) == Path.Combine(GenerateActionCommon.ProjectRoot, "Library/ScriptAssemblies/Unity.Cursor.Editor.dll") &&
                GenerateActionCommon.ComputeFileHash(cursor.Location, token) == CursorSha256, "当前外部编辑器不是已审的实际 Cursor 实现。");
            bool explicitPath = EditorPrefs.HasKey("kScriptsDefaultApp");
            string preference = EditorPrefs.GetString("kScriptsDefaultApp", "");
            Require(explicitPath && !string.IsNullOrEmpty(preference), "当前外部编辑器偏好不是已审的现存显式路径。");
            string fullPath = Path.GetFullPath(preference);
            object binding = Read(codeEditor, "m_CurrentInstallation", editor);
            string bindingPath = Read(binding.GetType(), "Path", binding) as string;
            string bindingName = Read(binding.GetType(), "Name", binding) as string;
            Require(bindingPath == preference, "CodeEditor 现存安装绑定与偏好不一致。");

            object operation = Read(currentType, "_discoverInstallations");
            Require(operation != null && operation.GetType().GetGenericTypeDefinition() == cursor.GetType(CursorPrefix + "AsyncOperation`1", true) &&
                Read(operation.GetType(), "_resetEvent", operation) is ManualResetEventSlim signal && signal.IsSet &&
                Read(operation.GetType(), "_exception", operation) == null && Read(operation.GetType(), "_exceptionHandler", operation) == null &&
                Read(operation.GetType(), "_finalHandler", operation) == null, "Cursor 安装发现未完成或含未知处理器，禁止启动、等待或重跑发现。");
            Delegate producer = Read(operation.GetType(), "_producer", operation) as Delegate;
            Require(producer != null && producer.GetInvocationList().Length == 1 && producer.Target == null && producer.Method.IsStatic &&
                producer.Method.DeclaringType == currentType && producer.Method.Name == "DiscoverInstallations", "Cursor 安装发现生产者身份未知。");
            object cached = Read(operation.GetType(), "_result", operation);
            Type dictionaryType = typeof(Dictionary<,>).MakeGenericType(typeof(string), cursor.GetType(CursorPrefix + "IVisualStudioInstallation", true));
            Require(cached?.GetType() == dictionaryType, "Cursor 安装缓存不是已审正规字典。");
            // 只读标准 BCL 字典的比较器字段，先排除自定义比较器，再允许普通键查找。
            FieldInfo[] comparers = dictionaryType.GetFields(BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public)
                .Where(field => typeof(IEqualityComparer<string>).IsAssignableFrom(field.FieldType)).ToArray();
            Require(comparers.Length == 1, "Cursor 安装字典比较器字段签名未知。");
            object comparer = comparers[0].GetValue(cached);
            Require(comparer == null || ReferenceEquals(comparer, EqualityComparer<string>.Default), "Cursor 安装字典含未知比较器。");
            var dictionary = (IDictionary)cached;
            Require(dictionary.Contains(fullPath), "当前 Cursor 路径不在既有安装结果，正规尾部会重新发现安装。");
            object installation = dictionary[fullPath];
            Type installationType = cursor.GetType(CursorPrefix + "VisualStudioCursorInstallation", true);
            Require(installation?.GetType() == installationType && Equals(Read(installationType, "<Path>k__BackingField", installation), fullPath),
                "Cursor 现存安装类型或路径归属未知。");
            // 现存安装实例已经证明该类静态初始化完成；此处只读取既有 _generator，不调用惰性 getter。
            object generator = Read(installationType, "_generator");
            Type generation = cursor.GetType(CursorPrefix + "ProjectGeneration", true);
            Require(generator?.GetType() == cursor.GetType(CursorPrefix + "SdkStyleProjectGeneration", true) &&
                Equals(Read(generation, "<ProjectDirectory>k__BackingField", generator), GenerateActionCommon.ProjectRoot) &&
                Equals(Read(generation, "m_ProjectName", generator), "Nova") && Path.GetFileName(GenerateActionCommon.ProjectRoot) == "Nova",
                "Cursor 现存生成器类型、项目根或项目名漂移。");
            object provider = Read(generation, "m_AssemblyNameProvider", generator);
            object io = Read(generation, "m_FileIOProvider", generator);
            object guid = Read(generation, "m_GUIDGenerator", generator);
            object generatorInstallation = Read(generation, "m_CurrentInstallation", generator);
            Require(provider?.GetType() == cursor.GetType(CursorPrefix + "SdkStyleProjectGeneration+SdkStyleAssemblyNameProvider", true) &&
                io?.GetType() == cursor.GetType(CursorPrefix + "FileIOProvider", true) && guid?.GetType() == cursor.GetType(CursorPrefix + "GUIDProvider", true) &&
                (generatorInstallation == null || ReferenceEquals(generatorInstallation, installation)), "Cursor 生成器含未知提供器或安装引用。");
            Require(Read(generation, "m_ProjectSupportedExtensions", generator) is HashSet<string> &&
                Read(generation, "m_BuiltinSupportedExtensions", generator) is HashSet<string>, "Cursor 扩展缓存类型未知。");
            Regex solutionNameRule = Read(generation, "InvalidCharactersRegexPattern") as Regex;
            Require(solutionNameRule?.GetType() == typeof(Regex) && solutionNameRule.ToString() == @"\?|&|\*|""|<|>|\||#|%|\^|;|:" &&
                solutionNameRule.Options == RegexOptions.Compiled, "Cursor 解决方案名称规则未知，无法证明配置文件固定点。");
            string[] user = EditorSettings.projectGenerationUserExtensions;
            string[] builtin = EditorSettings.projectGenerationBuiltinExtensions;
            Require(ArePathsExcluded(potential, user, builtin, out string pathError), pathError);
            var snapshot = new Snapshot
            {
                editorAnalyticsEnabled = EditorAnalytics.enabled,
                objectIdentitySha256 = Hash(new[] { Identity(editor), Identity(current), Identity(operation), Identity(cached),
                    Identity(installation), Identity(generator), Identity(provider), Identity(io), Identity(guid) }),
                selectionSha256 = Hash(new { explicitPath, preference, fullPath, bindingPath, bindingName }),
                extensionsSha256 = Hash(new { user = user.OrderBy(value => value, StringComparer.Ordinal).ToArray(),
                    builtin = builtin.OrderBy(value => value, StringComparer.Ordinal).ToArray(), culture = CultureInfo.CurrentCulture.Name }),
                potentialDispatchPathsSha256 = Hash(potential), vscodeFilesSha256 = CaptureVscodeFiles(token),
            };
            snapshot.sha256 = SnapshotHash(snapshot);
            return snapshot;
        }

        /// <summary>只接纳本机已逐分支审查的现存 .vscode 三份原字节，无需复制 vendor JSON 修补算法。</summary>
        /// <param name="token">文件读取取消标记。</param>
        /// <returns>现存目录、三文件和已有 patch-disable 标记状态的摘要；缺失或变更时阻断。</returns>
        private static string CaptureVscodeFiles(CancellationToken token)
        {
            string directory = Path.Combine(GenerateActionCommon.ProjectRoot, ".vscode");
            Require(Directory.Exists(directory) && (File.GetAttributes(directory) & FileAttributes.ReparsePoint) == 0,
                "Cursor 会创建 .vscode 或目录为未知重解析点，当前无写固定点不成立。");
            var rows = new List<string>();
            for (int index = 0; index < VscodeNames.Length; index++)
            {
                string path = Path.Combine(directory, VscodeNames[index]);
                Require(File.Exists(path) && (File.GetAttributes(path) & FileAttributes.ReparsePoint) == 0,
                    "Cursor 辅助配置缺失或为重解析点，正规尾部可能创建/修补文件。");
                string sha = GenerateActionCommon.ComputeFileHash(path, token);
                Require(sha == VscodeHashes[index], "Cursor 辅助配置原字节未在当前无修补合同内：" + VscodeNames[index]);
                rows.Add(sha);
            }
            string marker = Path.Combine(directory, ".vstupatchdisable");
            Require(!Directory.Exists(marker), "Cursor patch 标记是未知目录。");
            rows.Add(File.Exists(marker) ? GenerateActionCommon.ComputeFileHash(marker, token) : "absent");
            return Hash(rows);
        }

        /// <summary>沿声明链只读固定字段，不回退 getter、初始化或任意方法。</summary>
        /// <param name="type">已经存在或已完成初始化的实际类型。</param>
        /// <param name="name">固定已审字段名。</param>
        /// <param name="target">现存对象；null 表示静态字段。</param>
        /// <returns>字段原值，仅在方法内用于核对或摘要。</returns>
        private static object Read(Type type, string name, object target = null)
        {
            for (Type cursor = type; cursor != null; cursor = cursor.BaseType)
            {
                FieldInfo field = cursor.GetField(name, Fields | (target == null ? BindingFlags.Static : BindingFlags.Instance));
                if (field != null) return field.GetValue(target);
            }
            throw new InvalidOperationException("工程同步尾部字段缺失：" + name);
        }

        /// <summary>为现存对象分配本域弱身份，不修改其字段或执行其方法。</summary>
        /// <param name="value">现存对象。</param>
        /// <returns>不透明域内身份。</returns>
        private static string Identity(object value)
        {
            return value == null ? "null" : Identities.GetValue(value, _ => new IdentityToken()).value;
        }

        /// <summary>只序列化本类创建的普通 DTO/标量；不序列化 vendor 实例。</summary>
        /// <param name="value">瞬时证据。</param>
        /// <returns>SHA-256。</returns>
        private static string Hash(object value)
        {
            return GenerateActionCommon.ComputeTextHash(Util.Json.Serialize(value));
        }

        /// <summary>计算完整稳定摘要，排除 sha256 自身和正规可替换缓存。</summary>
        /// <param name="snapshot">脱敏快照。</param>
        /// <returns>稳定输入摘要。</returns>
        private static string SnapshotHash(Snapshot snapshot)
        {
            return Hash(new { CoreSha256, CoreMvid, CursorSha256, CursorMvid, snapshot.editorAnalyticsEnabled,
                snapshot.objectIdentitySha256, snapshot.selectionSha256, snapshot.extensionsSha256,
                snapshot.potentialDispatchPathsSha256, snapshot.vscodeFilesSha256 });
        }

        /// <summary>以具体原因拒绝未知输入；不改偏好、标记、安装缓存或队列来满足条件。</summary>
        /// <param name="condition">必须成立的已审条件。</param>
        /// <param name="error">阻断原因。</param>
        private static void Require(bool condition, string error)
        {
            if (!condition) throw new InvalidOperationException(error);
        }
    }
}
