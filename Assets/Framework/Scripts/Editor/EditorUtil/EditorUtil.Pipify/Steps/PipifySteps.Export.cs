/***************************************************************
 * (c) copyright 2026 - 2030, Solotopia
 * All Rights Reserved.
 * -------------------------------------------------------------
 * filename:  PipifySteps.Export.cs
 * author:    taoye
 * created:   2026/5/10
 * descrip:   Pipify 内置 Step 合集 —— 导出分组（Config 导出）
 ***************************************************************/

using System;
using Cysharp.Threading.Tasks;
using NovaFramework.Runtime;
using UnityEditor;

namespace NovaFramework.Editor
{
    /// <summary>
    /// Pipify 内置 Step 合集（partial）。
    /// 本文件收录导出分组 Config 子类的原子操作：Config 模块导出。
    /// 每个方法仅做薄封装，调用 EditorUtil.Config.* 对应 public API。
    /// </summary>
    internal static partial class PipifySteps
    {
        /// <summary>
        /// Step：导出 Config 模块（将 ConfigMasterSO 当前 Platform×Channel×DevelopMode 写入 ConfigRuntimeSO.asset）。
        /// 通过 Helpers.ResolveConfigMaster() 定位 ConfigMasterSO；
        /// 若 ExportTarget 为 null 则抛出 InvalidOperationException，提示用户先在 ConfigWindow 拖入目标 ConfigRuntimeSO；
        /// 通过后调用 Exporter.Export 写入，返回 null（未找到对应矩阵行）时抛出异常中断流水线。
        /// </summary>
        /// <param name="ctx">Runner 下发的运行时上下文。</param>
        /// <returns>完成的 UniTask。</returns>
        [PipifyStep("export.config", "导出 Config 资源", "导出资源/Config")]
        internal static UniTask RunExportConfig(PipifyContext ctx)
        {
            ConfigMasterSO master = Helpers.ResolveConfigMaster();

            if (master.ExportTarget == null)
            {
                throw new InvalidOperationException("[Pipify] ConfigMasterSO.ExportTarget 未配置，请在 ConfigWindow 拖入目标 ConfigRuntimeSO 资产。");
            }

            string assetPath = AssetDatabase.GetAssetPath(master.ExportTarget);
            ConfigRuntimeSO result = EditorUtil.Config.Exporter.Export(master, master.CurrentPlatform, master.CurrentChannel, master.CurrentDevelopMode, assetPath);
            if (result == null)
            {
                throw new InvalidOperationException(string.Format("[Pipify] Config 导出失败：未找到 Platform={0} × Channel={1} 配置行，请检查 ConfigMasterSO。", master.CurrentPlatform, master.CurrentChannel));
            }

            return UniTask.CompletedTask;
        }

    }
}
