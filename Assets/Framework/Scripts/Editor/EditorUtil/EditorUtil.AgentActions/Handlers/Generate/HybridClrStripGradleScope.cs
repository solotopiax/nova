/***************************************************************
 * (c) copyright 2026 - 2030, Solotopia
 * All Rights Reserved.
 * -------------------------------------------------------------
 * filename:  HybridClrStripGradleScope.cs
 * author:    taoye
 * created:   2026/10/16
 * descrip:   Generate 内部 Android 构建的真实路径观察和 Library fallback 保护
 ***************************************************************/

using System;
using System.IO;
using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEditor.Android;
using UnityEditor.Build;
using UnityEditor.Build.Reporting;

namespace NovaFramework.Editor
{
    /// <summary>只在 Generate 的同步租约内启用；未启用时不读设置、不改资产、不改变其他构建行为。</summary>
    internal sealed class HybridClrStripGradleScope : IPreprocessBuildWithReport, IPostprocessBuildWithReport, IPostGenerateGradleAndroidProject
    {
        private static Lease s_Active;

        /// <summary>保持在已审 HybridCLR Patch(order=0) 之前执行；未激活时三个真实回调均自然返回。</summary>
        /// <value>固定排序值 -1，不读取租约或任何 Editor 设置。</value>
        public int callbackOrder => -1;

        [Serializable]
        internal sealed class Observation
        {
            public string outputProject;
            public string gradleUnityLibrary;
            public int reportOptions;
            public int preprocessCount;
            public int gradleCount;
            public int postprocessCount;
            public string[] scriptingAssembliesPaths;
        }

        /// <summary>冻结精确输出；调用方先完成回调 roster 审查，不允许其他 Gradle 回调在本 guard 与 HybridCLR 之间删除工程根目录。</summary>
        /// <param name="frozenOutputProject">Action 冻结的本次临时 Android 工程根目录。</param>
        /// <returns>仅属于本次同步 GenerateAll 调用的租约；调用方必须在 finally 或 using 结束时释放。</returns>
        internal static Lease Begin(string frozenOutputProject)
        { return BeginCore(frozenOutputProject, null); }

        /// <summary>Android typed Generate额外绑定已审工具预测，原自然Gradle回调取得实际导出选择。</summary>
        /// <param name="frozenOutputProject">本次准确输出根。</param><param name="tools">原冻结完整工具输入。</param><returns>同一次同步原调用租约。</returns>
        internal static Lease BeginAndroidGenerate(string frozenOutputProject, AndroidGenerateToolchainContract.Snapshot tools)
        {
            if (tools == null) throw new ArgumentNullException(nameof(tools));
            return BeginCore(frozenOutputProject, tools);
        }

        /// <summary>沿用唯一原租约和路径校验；null保持既有纯路径guard语义。</summary>
        /// <param name="frozenOutputProject">准确输出根。</param><param name="tools">仅Android typed Generate传入的工具输入。</param><returns>本次租约。</returns>
        private static Lease BeginCore(string frozenOutputProject, AndroidGenerateToolchainContract.Snapshot tools)
        {
            if (s_Active != null) throw new InvalidOperationException("已有 StripAOT 路径观察租约。");
            if (EditorUserBuildSettings.activeBuildTarget != BuildTarget.Android)
                throw new InvalidOperationException("StripAOT 路径观察仅支持已冻结的 Android Target。");
            string root = Canonical(frozenOutputProject);
            return s_Active = new Lease(root, tools);
        }

        /// <summary>记录真实 BuildReport，验证它对应冻结输出与原实现的临时 Editor 设置；不推测 native 最终 options。</summary>
        /// <param name="report">Unity 自然调用时传入的真实构建报告；测试不得直接调用本回调制造观察。</param>
        public void OnPreprocessBuild(BuildReport report)
        {
            if (s_Active == null) return;
            BuildSummary summary = report.summary;
            if (summary.platform != BuildTarget.Android || Canonical(summary.outputPath) != s_Active.Observed.outputProject ||
                !EditorUserBuildSettings.buildScriptsOnly || !EditorUserBuildSettings.exportAsGoogleAndroidProject)
                throw new BuildFailedException("Generate 内部 Player 的真实输出、Target 或临时选项与冻结合同不符。");
            s_Active.Observed.preprocessCount++;
            s_Active.Observed.reportOptions = (int)summary.options;
        }

        /// <summary>在已审 PatchScriptingAssemblyList(order=0) 前要求路径存在且精确位于本次临时工程，阻止其 Library 全域 fallback。</summary>
        /// <param name="path">Unity 自然调用时传入的真实 Gradle unityLibrary 路径；无租约时直接返回。</param>
        public void OnPostGenerateGradleAndroidProject(string path)
        {
            if (s_Active == null) return;
            string actual = ValidateGradleDirectory(s_Active.Observed.outputProject, path);
            s_Active.Observed.gradleUnityLibrary = actual;
            s_Active.Observed.gradleCount++;
            s_Active.Observed.scriptingAssembliesPaths = CollectScriptingAssembliesWithoutLinks(actual);
            if (s_Active.tools != null)
                s_Active.ActualToolSelection = AndroidGenerateToolchainContract.CaptureExport(s_Active.tools, s_Active.Observed.outputProject, System.Threading.CancellationToken.None);
        }

        /// <summary>记录真实正常后处理发生，不把收到回调直接升级为最终构建或 Generate 成功证据。</summary>
        /// <param name="report">Unity 自然后处理流程传入的真实报告；测试不得直接调用本回调制造观察。</param>
        public void OnPostprocessBuild(BuildReport report)
        {
            if (s_Active == null) return;
            if (report.summary.platform != BuildTarget.Android || Canonical(report.summary.outputPath) != s_Active.Observed.outputProject)
                throw new BuildFailedException("StripAOT 后处理输出与冻结临时工程不一致。");
            s_Active.Observed.postprocessCount++;
        }

        /// <summary>只读核对 Receipt 记录的一次构建与精确项目内 Gradle 路径；不补造缺失观察或重放构建。</summary>
        /// <param name="observed">只允许来自一次自然构建流程的已序列化观察；空值或计数缺失均不完整。</param>
        /// <param name="outputProject">当前 Receipt 绑定的精确临时工程根目录。</param>
        /// <returns>观察计数、路径和唯一文件集合都满足该 Receipt 时返回 true，否则返回 false。</returns>
        internal static bool IsComplete(Observation observed, string outputProject)
        {
            if (observed == null || observed.preprocessCount != 1 || observed.gradleCount != 1 || observed.postprocessCount != 1 ||
                observed.scriptingAssembliesPaths == null) return false;
            string root = Canonical(outputProject);
            string gradle = Canonical(Path.Combine(root, "unityLibrary"));
            return observed.outputProject == root && observed.gradleUnityLibrary == gradle &&
                observed.scriptingAssembliesPaths.Distinct(StringComparer.Ordinal).Count() == observed.scriptingAssembliesPaths.Length &&
                observed.scriptingAssembliesPaths.All(path => Canonical(path).StartsWith(gradle + Path.DirectorySeparatorChar, StringComparison.Ordinal));
        }

        /// <summary>只读验证精确 Gradle 工程目录；该函数不激活租约、不调用回调、不产生任何构建观察记录。</summary>
        /// <param name="frozenOutputProject">Action 已冻结并规范化的本次临时 Android 工程根目录。</param>
        /// <param name="path">由原生 Gradle 回调传入的目录；测试只能使用本函数验证自有临时目录边界。</param>
        /// <returns>通过精确路径、存在性和祖先符号链接检查的规范化 unityLibrary 路径。</returns>
        internal static string ValidateGradleDirectory(string frozenOutputProject, string path)
        {
            string actual = Canonical(path);
            string expected = Canonical(Path.Combine(frozenOutputProject, "unityLibrary"));
            if (actual != expected || !Directory.Exists(actual))
                throw new BuildFailedException("StripAOT Gradle 路径不符合本次精确输出或不存在；已在 HybridCLR Library fallback 前中止。Actual=" + actual);
            RejectReparseAncestors(actual);
            return actual;
        }

        /// <summary>规范化路径并移除尾部分隔符；路径必须非空，不能把空值解释成项目根。</summary>
        /// <param name="path">必须非空的输入路径。</param>
        /// <returns>绝对且无尾部分隔符的文本路径；本函数不解析符号链接，调用方必须另行执行链接检查。</returns>
        private static string Canonical(string path)
        {
            if (string.IsNullOrWhiteSpace(path)) throw new InvalidOperationException("StripAOT 观察路径为空。");
            return Path.GetFullPath(path).TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
        }

        /// <summary>拒绝现存路径的符号链接祖先，避免精确文本路径指向临时工程域之外。</summary>
        /// <param name="path">已经规范化、需要逐层检查的现存 Gradle 目录。</param>
        private static void RejectReparseAncestors(string path)
        {
            for (DirectoryInfo item = new DirectoryInfo(path); item != null; item = item.Parent)
                if (item.Exists && (item.Attributes & FileAttributes.ReparsePoint) != 0)
                    throw new BuildFailedException("StripAOT Gradle 路径包含符号链接祖先：" + item.FullName);
        }

        /// <summary>先检查每层目录再下降，拒绝子目录或目标文件符号链接，避免 vendor 的递归枚举越过冻结工程。</summary>
        /// <param name="root">已经通过精确目录检查的 unityLibrary 根目录；测试使用独占系统临时目录。</param>
        /// <returns>按 Ordinal 排序的全部实际 ScriptingAssemblies.json 路径；遇到符号链接抛出构建异常。</returns>
        internal static string[] CollectScriptingAssembliesWithoutLinks(string root)
        {
            var pending = new Stack<string>();
            var files = new List<string>();
            pending.Push(root);
            while (pending.Count != 0)
            {
                string directory = pending.Pop();
                foreach (string child in Directory.GetDirectories(directory, "*", SearchOption.TopDirectoryOnly))
                {
                    if ((File.GetAttributes(child) & FileAttributes.ReparsePoint) != 0)
                        throw new BuildFailedException("StripAOT Gradle 子目录是符号链接：" + child);
                    pending.Push(child);
                }
                foreach (string file in Directory.GetFiles(directory, "ScriptingAssemblies.json", SearchOption.TopDirectoryOnly))
                {
                    if ((File.GetAttributes(file) & FileAttributes.ReparsePoint) != 0)
                        throw new BuildFailedException("StripAOT ScriptingAssemblies 文件是符号链接：" + file);
                    files.Add(Canonical(file));
                }
            }
            return files.OrderBy(value => value, StringComparer.Ordinal).ToArray();
        }

        /// <summary>限定一次 Generate 调用的内存观察租约；不注册延迟任务、不写 marker，不跨重载恢复。</summary>
        internal sealed class Lease : IDisposable
        {
            internal readonly Observation Observed;
            internal readonly AndroidGenerateToolchainContract.Snapshot tools;
            internal AndroidGenerateToolchainContract.ExportSelection ActualToolSelection;

            /// <summary>保存本次冻结输出路径，构造阶段不触碰 Unity 或磁盘。</summary>
            /// <param name="outputProject">已规范化的本次临时 Android 工程根目录。</param>
            internal Lease(string outputProject, AndroidGenerateToolchainContract.Snapshot expectedTools = null)
            {
                tools = expectedTools;
                Observed = new Observation { outputProject = outputProject, scriptingAssembliesPaths = Array.Empty<string>() };
            }

            /// <summary>GenerateAll 返回后要求一次完整真实构建观察；缺少 Gradle 或后处理均不能假造完整路径证据。</summary>
            internal void RequireComplete()
            {
                if (Observed.preprocessCount != 1 || Observed.gradleCount != 1 || Observed.postprocessCount != 1)
                    throw new InvalidOperationException("StripAOT 真实观察不完整：pre=" + Observed.preprocessCount + ", gradle=" + Observed.gradleCount + ", post=" + Observed.postprocessCount);
                if (tools != null && ActualToolSelection == null) throw new InvalidOperationException("Android Generate缺本次自然Gradle工具选择。");
            }

            /// <summary>只释放本次租约；失败时保留已产生的正式输出，不回放构建或恢复官方进程状态。</summary>
            public void Dispose()
            {
                if (!ReferenceEquals(s_Active, this)) throw new InvalidOperationException("StripAOT 观察租约归属不符。");
                s_Active = null;
            }
        }
    }
}
