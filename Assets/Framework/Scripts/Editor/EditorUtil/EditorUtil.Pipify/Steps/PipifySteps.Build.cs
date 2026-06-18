/***************************************************************
 * (c) copyright 2026 - 2030, Solotopia
 * All Rights Reserved.
 * -------------------------------------------------------------
 * filename:  PipifySteps.Build.cs
 * author:    taoye
 * created:   2026/5/10
 * descrip:   Pipify 内置 Step 合集 —— 打包分组（1 个 Step）
 ***************************************************************/

using Cysharp.Threading.Tasks;
using NovaFramework.Runtime;

namespace NovaFramework.Editor
{
    /// <summary>
    /// Pipify 内置 Step 合集（partial）。
    /// 本文件收录打包分组的 1 个原子操作：一键打包 Player。
    /// 方法仅做薄封装，调用 EditorUtil.Build.BuildPackage public API。
    /// </summary>
    internal static partial class PipifySteps
    {
        /// <summary>
        /// Step：按 PackageParams 执行 Unity Player 打包。
        /// 产物文件名由 BuildPackage 按固定格式自动生成，输出到 OutputFolderPath 指定的文件夹。
        /// 打包失败时 BuildPackage 内部抛 InvalidOperationException，Runner 会捕获并中断流水线。
        /// </summary>
        /// <param name="ctx">Runner 下发的运行时上下文。</param>
        /// <param name="p">打包参数。</param>
        /// <returns>完成的 UniTask。</returns>
        [PipifyStep("build.package", "安装包|工程", "构建打包", ParamsType = typeof(PackageParams))]
        internal static UniTask RunPackage(PipifyContext ctx, PackageParams p)
        {
            ConfigRuntimeSO runtime = EditorUtil.Config.RuntimeProvider.GetCurrent();
            DevelopMode developMode = runtime != null ? runtime.DevelopMode : DevelopMode.Debug;
            if (runtime == null)
            {
                Log.Warning(LogTag.Editor, "[Pipify] 未找到激活 ConfigRuntimeSO，文件名开发模式段降级为 Debug，请先导出 Config。");
            }
            EditorUtil.Build.BuildPackage(
                p.Target, p.OutputFolderPath, p.DevelopmentBuild,
                p.BuildMode, p.BuildAppBundle, p.SplitApplicationBinary, developMode);
            return UniTask.CompletedTask;
        }
    }
}
