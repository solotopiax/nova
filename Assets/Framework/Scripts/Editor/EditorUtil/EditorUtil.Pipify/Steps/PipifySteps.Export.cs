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
        /// 读取 Config 导出参数；旧条目参数为空时仅初始化一次，并立即写回条目的参数 JSON。
        /// </summary>
        /// <param name="item">当前执行的 Pipify 条目。</param>
        /// <param name="master">旧条目首次初始化时的默认值来源。</param>
        /// <param name="initialized">是否在本次调用中完成了旧条目初始化。</param>
        /// <returns>本次导出使用的三维参数。</returns>
        internal static ConfigExportParams ResolveConfigExportParams(
            BatchItem item,
            ConfigMasterSO master,
            out bool initialized)
        {
            if (item == null) throw new ArgumentNullException(nameof(item));
            if (!string.IsNullOrWhiteSpace(item.ParamsJson))
            {
                initialized = false;
                return Util.Json.Deserialize<ConfigExportParams>(item.ParamsJson);
            }

            ConfigExportParams parameters = CreateConfigExportParams(master);
            item.ParamsJson = Util.Json.Serialize(parameters);
            initialized = true;
            return parameters;
        }

        /// <summary>
        /// 解析 Config 导出参数，并在旧条目首次初始化时立即保存所属 PipifySettingsSO。
        /// </summary>
        /// <param name="settings">条目所属的 Pipify 存档；无法定位时可为空，此时仍更新内存条目。</param>
        /// <param name="item">当前执行的 Pipify 条目。</param>
        /// <param name="master">旧条目首次初始化时的默认值来源。</param>
        /// <param name="initialized">是否在本次调用中完成了旧条目初始化。</param>
        /// <returns>本次导出使用的三维参数。</returns>
        internal static ConfigExportParams ResolveAndPersistConfigExportParams(
            PipifySettingsSO settings,
            BatchItem item,
            ConfigMasterSO master,
            out bool initialized)
        {
            ConfigExportParams parameters = ResolveConfigExportParams(item, master, out initialized);
            if (initialized && settings != null)
            {
                EditorUtility.SetDirty(settings);
                AssetDatabase.SaveAssetIfDirty(settings);
            }
            return parameters;
        }

        /// <summary>
        /// 使用 Step 显式坐标导出 ConfigRuntime，不修改 ConfigMaster 当前选择。
        /// </summary>
        /// <param name="master">只读的设计态配置来源。</param>
        /// <param name="parameters">本次 Step 已固化的三维坐标。</param>
        /// <param name="assetPath">目标 ConfigRuntime 资产路径。</param>
        /// <returns>成功写入的 ConfigRuntime；未找到对应矩阵行时返回 null。</returns>
        internal static ConfigRuntimeSO ExportConfig(
            ConfigMasterSO master,
            ConfigExportParams parameters,
            string assetPath)
        {
            if (master == null) throw new ArgumentNullException(nameof(master));
            if (parameters == null) throw new ArgumentNullException(nameof(parameters));
            if (parameters.Platform == PlatformType.None)
            {
                throw new InvalidOperationException("[Pipify] Config 导出参数 Platform 不可为 None。");
            }
            if (parameters.Channel == ChannelType.None)
            {
                throw new InvalidOperationException("[Pipify] Config 导出参数 Channel 不可为 None。");
            }
            if (!Enum.IsDefined(typeof(DevelopMode), parameters.DevelopMode))
            {
                throw new InvalidOperationException($"[Pipify] Config 导出参数 DevelopMode 非法：{parameters.DevelopMode}。");
            }

            return EditorUtil.Config.Exporter.Export(
                master,
                parameters.Platform,
                parameters.Channel,
                parameters.DevelopMode,
                assetPath);
        }

        /// <summary>
        /// Step：导出 Config 模块（将 Step 参数指定的 Platform×Channel×DevelopMode 写入 ConfigRuntimeSO.asset）。
        /// 通过 Helpers.ResolveConfigMaster() 定位 ConfigMasterSO；
        /// 若 ExportTarget 为 null 则抛出 InvalidOperationException，提示用户先在 ConfigWindow 拖入目标 ConfigRuntimeSO；
        /// 通过后调用 Exporter.Export 写入，返回 null（未找到对应矩阵行）时抛出异常中断流水线。
        /// </summary>
        /// <param name="ctx">Runner 下发的运行时上下文。</param>
        /// <param name="parameters">仅对本次 Step 生效的三维导出参数。</param>
        /// <returns>完成的 UniTask。</returns>
        [PipifyStep("export.config", "导出 Config 资源", "导出资源/Config", ParamsType = typeof(ConfigExportParams))]
        internal static UniTask RunExportConfig(PipifyContext ctx, ConfigExportParams parameters)
        {
            ConfigMasterSO master = Helpers.ResolveConfigMaster();

            if (master.ExportTarget == null)
            {
                throw new InvalidOperationException("[Pipify] ConfigMasterSO.ExportTarget 未配置，请在 ConfigWindow 拖入目标 ConfigRuntimeSO 资产。");
            }

            string assetPath = AssetDatabase.GetAssetPath(master.ExportTarget);
            ConfigRuntimeSO result = ExportConfig(master, parameters, assetPath);
            if (result == null)
            {
                throw new InvalidOperationException(string.Format(
                    "[Pipify] Config 导出失败：未找到 Platform={0} × Channel={1} × DevelopMode={2} 配置，请检查 ConfigMasterSO。",
                    parameters.Platform,
                    parameters.Channel,
                    parameters.DevelopMode));
            }

            return UniTask.CompletedTask;
        }

    }
}
