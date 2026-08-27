/***************************************************************
 * (c) copyright 2026 - 2030, Solotopia
 * All Rights Reserved.
 * -------------------------------------------------------------
 * filename:  PipifySteps.BundleBuilder.cs
 * author:    taoye
 * created:   2026/5/19
 * descrip:   Pipify 内置 Step 合集 —— BundleBuilder 分组
 ***************************************************************/

using Cysharp.Threading.Tasks;
using UnityEditor;

namespace NovaFramework.Editor
{
    /// <summary>
    /// Pipify 内置 Step 合集（partial）。
    /// 本文件收录 BundleBuilder 分组的标准资源构建与可选 RawFile 构建原子操作。
    /// 方法仅做薄封装，调用 EditorUtil.BundleBuilder public API。
    /// </summary>
    internal static partial class PipifySteps
    {
        /// <summary>
        /// Step：按 AssetBundleBuildArgs 执行 YooAsset 资源包构建。
        /// 构建失败时 EditorUtil.BundleBuilder 内部抛 InvalidOperationException，Runner 会捕获并中断流水线。
        /// </summary>
        /// <param name="ctx">Runner 下发的运行时上下文。</param>
        /// <param name="p">资源包构建参数（直接复用 EditorUtil.BundleBuilder 参数类，11 项与 Bundle Builder 窗口一一对齐）。</param>
        /// <returns>完成的 UniTask。</returns>
        [PipifyStep("bundlebuilder.build", "Bundles", "构建资源", ParamsType = typeof(AssetBundleBuildArgs))]
        internal static UniTask RunBuildAssetBundle(PipifyContext ctx, AssetBundleBuildArgs p)
        {
            EditorUtil.Config.ActivePlatform.RequireCurrent("[Pipify] Bundle 构建");
            p.Target = EditorUserBuildSettings.activeBuildTarget;
            EditorUtil.BundleBuilder.BuildAssetBundle(p);
            return UniTask.CompletedTask;
        }

        /// <summary>
        /// Step：按 RawFileBuildArgs 执行 YooAsset 原生文件构建。
        /// 该入口与标准资源构建相互独立，不改变 bundlebuilder.build 的参数和行为。
        /// </summary>
        /// <param name="ctx">Runner 下发的运行时上下文。</param>
        /// <param name="p">RawFile 构建参数。</param>
        /// <returns>完成的 UniTask。</returns>
        [PipifyStep("bundlebuilder.build_raw_file", "Raw Files", "构建资源", ParamsType = typeof(RawFileBuildArgs))]
        internal static UniTask RunBuildRawFileBundle(PipifyContext ctx, RawFileBuildArgs p)
        {
            EditorUtil.Config.ActivePlatform.RequireCurrent("[Pipify] RawFile 构建");
            p.Target = EditorUserBuildSettings.activeBuildTarget;
            EditorUtil.BundleBuilder.BuildRawFileBundle(p);
            return UniTask.CompletedTask;
        }
    }
}
