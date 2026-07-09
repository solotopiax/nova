/***************************************************************
 * (c) copyright 2026 - 2030, Solotopia
 * All Rights Reserved.
 * -------------------------------------------------------------
 * filename:  DataMasterPluginBuildProcessor.cs
 * author:    taoye
 * created:   2026/7/8
 * descrip:   构建期按 ConfigRuntimeSO.DevelopMode 临时注入 / 移除 PRODUCTION_PACKAGE 宏，
 *            构建完成后精确复原为编译前状态（不污染工程持久 PlayerSettings）。
 ***************************************************************/

using NovaFramework.Editor;
using NovaFramework.Runtime;
using NovaFramework.SDK.StarlusDataMaster.ABTest.Runtime;
using UnityEditor;
using UnityEditor.Build;
using UnityEditor.Build.Reporting;

namespace NovaFramework.SDK.StarlusDataMaster.ABTest.Editor
{
    /// <summary>
    /// DataMaster 插件构建处理器。
    /// 依据 ConfigRuntimeSO.DevelopMode 决定构建产物是否携带编译宏 PRODUCTION_PACKAGE：
    /// Release 注入、Debug 移除。该宏由厂商 DataMaster SDK（<c>com.starlus.sdk.datamaster</c>）
    /// 用于区分正式 / 测试环境域名（未定义 → dev 域名，定义 → 正式域名，详见 Doc/官方SDK技术文档.md）。
    /// 为避免污染工程持久设置：<b>编译前</b>记录该平台原本是否已有该宏并存入 SessionState，
    /// <b>构建完成后</b>精确复原（原有则保留、原无则移除）。
    /// 仅当 ConfigRuntimeSO 中启用了 DataMaster 插件（存在 DataMasterPluginConfig）时才处理，
    /// 未启用的工程不会被注入该宏。
    /// </summary>
    public sealed class DataMasterPluginBuildProcessor : NovaSDKBuildProcessor
    {
        /// <summary>
        /// 厂商 DataMaster SDK 用于区分正式 / 测试环境的编译宏名。
        /// </summary>
        private const string c_ProductionDefine = "PRODUCTION_PACKAGE";

        /// <summary>
        /// SessionState 记录键前缀，用于跨预处理 / 后处理（乃至域重载）保存编译前的宏状态。
        /// </summary>
        private const string c_SessionKeyPrefix = "Nova.DataMaster.ProductionDefine.";

        /// <summary>
        /// Android 平台构建前：按 DevelopMode 同步 PRODUCTION_PACKAGE 宏。
        /// </summary>
        /// <param name="report">构建报告。</param>
        /// <param name="context">Nova 构建上下文。</param>
        public override void OnPreprocessBuildOnAndroid(BuildReport report, NovaBuildContext context) => ApplyProductionDefine(NamedBuildTarget.Android);

        /// <summary>
        /// iOS 平台构建前：按 DevelopMode 同步 PRODUCTION_PACKAGE 宏。
        /// </summary>
        /// <param name="report">构建报告。</param>
        /// <param name="context">Nova 构建上下文。</param>
        public override void OnPreprocessBuildOniOS(BuildReport report, NovaBuildContext context) => ApplyProductionDefine(NamedBuildTarget.iOS);

        /// <summary>
        /// WebGL 平台构建前：按 DevelopMode 同步 PRODUCTION_PACKAGE 宏。
        /// </summary>
        /// <param name="report">构建报告。</param>
        /// <param name="context">Nova 构建上下文。</param>
        public override void OnPreprocessBuildOnWebGL(BuildReport report, NovaBuildContext context) => ApplyProductionDefine(NamedBuildTarget.WebGL);

        /// <summary>
        /// Android 平台构建后：复原 PRODUCTION_PACKAGE 宏至编译前状态。
        /// </summary>
        /// <param name="report">构建报告。</param>
        /// <param name="context">Nova 构建上下文。</param>
        public override void OnPostprocessBuildOnAndroid(BuildReport report, NovaBuildContext context) => RestoreProductionDefine(NamedBuildTarget.Android);

        /// <summary>
        /// WebGL 平台构建后：复原 PRODUCTION_PACKAGE 宏至编译前状态。
        /// </summary>
        /// <param name="report">构建报告。</param>
        /// <param name="context">Nova 构建上下文。</param>
        public override void OnPostprocessBuildOnWebGL(BuildReport report, NovaBuildContext context) => RestoreProductionDefine(NamedBuildTarget.WebGL);

#if UNITY_IOS
        /// <summary>
        /// iOS 平台构建后：先执行基类默认 Embed 逻辑（本包无需 Embed，空操作），再复原 PRODUCTION_PACKAGE 宏。
        /// 基类 OnPostprocessBuildOniOS 仅在 UNITY_IOS 下定义，故本 override 同样以 UNITY_IOS 条件编译。
        /// </summary>
        /// <param name="report">构建报告。</param>
        /// <param name="context">Nova 构建上下文。</param>
        public override void OnPostprocessBuildOniOS(BuildReport report, NovaBuildContext context)
        {
            base.OnPostprocessBuildOniOS(report, context);
            RestoreProductionDefine(NamedBuildTarget.iOS);
        }
#endif

        /// <summary>
        /// 编译前：记录目标平台当前是否已有 PRODUCTION_PACKAGE，再按 DevelopMode 注入 / 移除。
        /// 仅在 ConfigRuntimeSO 存在且启用了 DataMaster 插件时执行。
        /// </summary>
        /// <param name="target">当前构建目标平台。</param>
        private void ApplyProductionDefine(NamedBuildTarget target)
        {
            ConfigRuntimeSO runtime = EditorUtil.Config.RuntimeProvider.GetCurrent();
            if (runtime == null)
            {
                Log.Warning(LogTag.Editor, "[DataMasterPluginBuildProcessor] 未找到 ConfigRuntimeSO，跳过 PRODUCTION_PACKAGE 宏注入。请先在 ConfigWindow 导出配置。");
                return;
            }

            if (runtime.GetSDKPluginConfig<DataMasterPluginConfig>() == null)
            {
                Log.Debug(LogTag.Editor, "[DataMasterPluginBuildProcessor] 未启用 DataMaster 插件，跳过 PRODUCTION_PACKAGE 宏注入。");
                return;
            }

            bool hadBefore = EditorUtil.ScriptingDefineSymbols.HasScriptingDefineSymbol(target, c_ProductionDefine);
            SessionState.SetBool(ActiveKey(target), true);
            SessionState.SetBool(HadBeforeKey(target), hadBefore);

            bool wantProduction = runtime.DevelopMode == DevelopMode.Release;
            if (wantProduction)
            {
                EditorUtil.ScriptingDefineSymbols.AddScriptingDefineSymbol(target, c_ProductionDefine);
            }
            else
            {
                EditorUtil.ScriptingDefineSymbols.RemoveScriptingDefineSymbol(target, c_ProductionDefine);
            }

            Log.Debug(LogTag.Editor, $"[DataMasterPluginBuildProcessor] 编译前记录 {target.TargetName} 原状态={hadBefore}；按 DevelopMode={runtime.DevelopMode} {(wantProduction ? "注入" : "移除")} {c_ProductionDefine}（构建后将复原）。");
        }

        /// <summary>
        /// 编译后：从 SessionState 读回编译前状态，精确复原 PRODUCTION_PACKAGE 宏（原有则保留、原无则移除）。
        /// 若预处理未处理该平台（未启用 / 无配置），不做任何复原。
        /// </summary>
        /// <param name="target">当前构建目标平台。</param>
        private void RestoreProductionDefine(NamedBuildTarget target)
        {
            if (!SessionState.GetBool(ActiveKey(target), false))
            {
                return;
            }

            bool hadBefore = SessionState.GetBool(HadBeforeKey(target), false);
            if (hadBefore)
            {
                EditorUtil.ScriptingDefineSymbols.AddScriptingDefineSymbol(target, c_ProductionDefine);
            }
            else
            {
                EditorUtil.ScriptingDefineSymbols.RemoveScriptingDefineSymbol(target, c_ProductionDefine);
            }

            SessionState.EraseBool(ActiveKey(target));
            SessionState.EraseBool(HadBeforeKey(target));

            Log.Debug(LogTag.Editor, $"[DataMasterPluginBuildProcessor] 构建后已复原 {target.TargetName} 的 {c_ProductionDefine} 至编译前状态={hadBefore}。");
        }

        /// <summary>
        /// 构造"本次构建是否已处理该平台"的 SessionState 键。
        /// </summary>
        /// <param name="target">目标平台。</param>
        /// <returns>SessionState 键。</returns>
        private static string ActiveKey(NamedBuildTarget target) => $"{c_SessionKeyPrefix}Active.{target.TargetName}";

        /// <summary>
        /// 构造"编译前该平台是否已有该宏"的 SessionState 键。
        /// </summary>
        /// <param name="target">目标平台。</param>
        /// <returns>SessionState 键。</returns>
        private static string HadBeforeKey(NamedBuildTarget target) => $"{c_SessionKeyPrefix}HadBefore.{target.TargetName}";
    }
}
