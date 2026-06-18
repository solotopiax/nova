/***************************************************************
 * (c) copyright 2026 - 2030, Solotopia
 * All Rights Reserved.
 * -------------------------------------------------------------
 * filename:  PipifySteps.Export.UI.cs
 * author:    taoye
 * created:   2026/5/11
 * descrip:   Pipify 内置 Step 合集 —— 导出分组（UI 导出）
 ***************************************************************/

using System;
using System.Reflection;
using Cysharp.Threading.Tasks;
using NovaFramework.Runtime;

namespace NovaFramework.Editor
{
    /// <summary>
    /// Pipify 内置 Step 合集（partial）。
    /// 本文件收录导出分组 UI 子类的原子操作：UI 数据导出与 UI 类型导出。
    /// 每个方法仅做薄封装，调用 EditorUtil.UI.Exporter.ExportAll。
    /// </summary>
    internal static partial class PipifySteps
    {
        /// <summary>
        /// Step：全量导出 UI 数据（通过 Luban Pipeline 仅生成 JSON 数据，不执行代码生成）。
        /// 通过 Helpers.ResolveComponentOnNova 取 UIComponent；
        /// 通过反射读取 m_UISettings（与 UIComponentInspector 保持一致）；
        /// UISettings 为 null 或 sourceDirPath 为空时内部静默返回，不抛异常。
        /// </summary>
        /// <param name="ctx">Runner 下发的运行时上下文。</param>
        /// <returns>完成的 UniTask。</returns>
        [PipifyStep("export.ui.data", "导出 UI 数据", "导出资源/UI")]
        internal static UniTask RunExportUIData(PipifyContext ctx)
        {
            UISettings settings = ResolveUISettings();
            string sourceDirPath = ResolveUISourceDirPath(settings);
            EditorUtil.UI.Exporter.ExportData(settings, sourceDirPath);
            return UniTask.CompletedTask;
        }

        /// <summary>
        /// Step：全量导出 UI 类型（通过 Luban Pipeline 仅生成 C# 类型定义，不执行数据导出）。
        /// 通过 Helpers.ResolveComponentOnNova 取 UIComponent；
        /// 通过反射读取 m_UISettings（与 UIComponentInspector 保持一致）；
        /// UISettings 为 null 或 sourceDirPath 为空时内部静默返回，不抛异常。
        /// </summary>
        /// <param name="ctx">Runner 下发的运行时上下文。</param>
        /// <returns>完成的 UniTask。</returns>
        [PipifyStep("export.ui.code", "导出 UI 类型", "导出资源/UI")]
        internal static UniTask RunExportUICode(PipifyContext ctx)
        {
            UISettings settings = ResolveUISettings();
            string sourceDirPath = ResolveUISourceDirPath(settings);
            EditorUtil.UI.Exporter.ExportCode(settings, sourceDirPath);
            return UniTask.CompletedTask;
        }

        /// <summary>
        /// 从 Nova 根节点的 UIComponent 上通过反射读取 UISettings 实例；
        /// 未找到 UIComponent 或字段值为 null 时均抛出 InvalidOperationException。
        /// </summary>
        /// <returns>UIComponent 上配置的 UISettings 实例。</returns>
        private static UISettings ResolveUISettings()
        {
            UIComponent uiComponent = Helpers.ResolveComponentOnNova<UIComponent>();
            FieldInfo field = typeof(UIComponent).GetField("m_UISettings", BindingFlags.NonPublic | BindingFlags.Instance);
            UISettings settings = field?.GetValue(uiComponent) as UISettings;
            if (settings == null)
            {
                throw new InvalidOperationException("[Pipify] UIComponent.m_UISettings 未配置，请在 UIComponent Inspector 中配置 UI Settings。");
            }
            return settings;
        }

        /// <summary>
        /// 验证并返回 UISettings.SourceDirPath；路径为空时抛出 InvalidOperationException。
        /// </summary>
        /// <param name="settings">已定位的 UISettings 实例。</param>
        /// <returns>非空的数据源目录路径。</returns>
        private static string ResolveUISourceDirPath(UISettings settings)
        {
#if UNITY_EDITOR
            if (string.IsNullOrEmpty(settings.SourceDirPath))
            {
                throw new InvalidOperationException("[Pipify] UISettings.SourceDirPath 未配置，请在 UIComponent Inspector 中填写数据源目录路径。");
            }
            return settings.SourceDirPath;
#else
            throw new InvalidOperationException("[Pipify] UISettings.SourceDirPath 仅在编辑器模式下可用。");
#endif
        }
    }
}
