/***************************************************************
 * (c) copyright 2026 - 2030, Solotopia
 * All Rights Reserved.
 * -------------------------------------------------------------
 * filename:  PipifySteps.Export.Table.cs
 * author:    taoye
 * created:   2026/5/11
 * descrip:   Pipify 内置 Step 合集 —— 导出分组（Table 导出）
 ***************************************************************/

using System;
using System.Reflection;
using Cysharp.Threading.Tasks;
using NovaFramework.Runtime;

namespace NovaFramework.Editor
{
    /// <summary>
    /// Pipify 内置 Step 合集（partial）。
    /// 本文件收录导出分组 Table 子类的原子操作：仅导出表格数据、仅导出表格类型。
    /// 每个方法仅做薄封装，调用 EditorUtil.Table.Exporter 对应 public API。
    /// TableSettings 通过反射从 TableComponent.m_Setting 获取，与 TableComponentInspector 保持一致。
    /// </summary>
    internal static partial class PipifySteps
    {
        /// <summary>
        /// Step：仅导出表格数据（JSON）。
        /// 通过 Helpers.ResolveComponentOnNova 定位 TableComponent，
        /// 反射取 m_Setting 与 SourceDirPath，调用 EditorUtil.Table.Exporter.ExportData。
        /// </summary>
        /// <param name="ctx">Runner 下发的运行时上下文。</param>
        /// <returns>完成的 UniTask。</returns>
        [PipifyStep("export.table.data", "导出 Table 数据", "导出资源/Table")]
        internal static UniTask RunExportTableData(PipifyContext ctx)
        {
            TableComponent component = Helpers.ResolveComponentOnNova<TableComponent>();
            TableSettings settings = ResolveTableSettings(component);
            bool result = EditorUtil.Table.Exporter.ExportData(settings, settings.SourceDirPath);
            if (!result)
            {
                throw new InvalidOperationException("[Pipify] 表格数据导出失败，请检查 TableComponent 配置与表格数据源目录。");
            }
            return UniTask.CompletedTask;
        }

        /// <summary>
        /// Step：仅导出表格类型（C# 代码）。
        /// 通过 Helpers.ResolveComponentOnNova 定位 TableComponent，
        /// 反射取 m_Setting 与 SourceDirPath，调用 EditorUtil.Table.Exporter.ExportCode。
        /// </summary>
        /// <param name="ctx">Runner 下发的运行时上下文。</param>
        /// <returns>完成的 UniTask。</returns>
        [PipifyStep("export.table.code", "导出 Table 类型", "导出资源/Table")]
        internal static UniTask RunExportTableCode(PipifyContext ctx)
        {
            TableComponent component = Helpers.ResolveComponentOnNova<TableComponent>();
            TableSettings settings = ResolveTableSettings(component);
            bool result = EditorUtil.Table.Exporter.ExportCode(settings, settings.SourceDirPath);
            if (!result)
            {
                throw new InvalidOperationException("[Pipify] 表格类型导出失败，请检查 TableComponent 配置与表格数据源目录。");
            }
            return UniTask.CompletedTask;
        }

        /// <summary>
        /// 通过反射从 TableComponent 获取 TableSettings 实例；
        /// 字段缺失或值为 null 时抛出 InvalidOperationException。
        /// </summary>
        /// <param name="component">目标 TableComponent 实例。</param>
        /// <returns>TableSettings 实例。</returns>
        private static TableSettings ResolveTableSettings(TableComponent component)
        {
            FieldInfo field = typeof(TableComponent).GetField("m_Setting", BindingFlags.NonPublic | BindingFlags.Instance);
            if (field == null)
            {
                throw new InvalidOperationException("[Pipify] 无法反射 TableComponent.m_Setting，字段可能已被重命名。");
            }
            TableSettings settings = field.GetValue(component) as TableSettings;
            if (settings == null)
            {
                throw new InvalidOperationException("[Pipify] TableComponent.m_Setting 未配置，请在 Nova Prefab 的 TableComponent 上完成 Settings 配置。");
            }
            if (string.IsNullOrEmpty(settings.SourceDirPath))
            {
                throw new InvalidOperationException("[Pipify] TableSettings.SourceDirPath 未配置，请在 TableComponent Inspector 中填写表格数据源目录。");
            }
            return settings;
        }
    }
}
