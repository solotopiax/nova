/***************************************************************
 * (c) copyright 2026 - 2030, Solotopia
 * All Rights Reserved.
 * -------------------------------------------------------------
 * filename:  PipifySteps.Export.Vibrate.cs
 * author:    taoye
 * created:   2026/5/11
 * descrip:   Pipify 内置 Step 合集 —— 导出分组（Vibrate 导出）
 ***************************************************************/

using System;
using System.Reflection;
using Cysharp.Threading.Tasks;
using NovaFramework.Runtime;

namespace NovaFramework.Editor
{
    /// <summary>
    /// Pipify 内置 Step 合集（partial）。
    /// 本文件收录导出分组 Vibrate 子类的原子操作：Emphasis 与 Custom 双轨的数据/类型独立导出。
    /// 每个方法仅做薄封装，调用 EditorUtil.Vibrate.Export* 对应 public API。
    /// </summary>
    internal static partial class PipifySteps
    {
        /// <summary>
        /// Step：导出 Emphasis 震动数据（遍历所有 EmphasisUnitsSettings，逐单元导出数据文件）。
        /// 通过 Helpers.ResolveComponentOnNova 定位 VibrateComponent；
        /// 反射读取私有 m_Settings 字段取得 VibrateSettings；
        /// 未找到组件或 Settings 为 null 时抛出 InvalidOperationException 中断流水线。
        /// </summary>
        /// <param name="ctx">Runner 下发的运行时上下文。</param>
        /// <returns>完成的 UniTask。</returns>
        [PipifyStep("export.vibrate.emphasis.data", "导出 Vibrate Emphasis 数据", "导出资源/Vibrate")]
        internal static UniTask RunExportVibrateEmphasisData(PipifyContext ctx)
        {
            VibrateComponent component = Helpers.ResolveComponentOnNova<VibrateComponent>();
            VibrateSettings settings = ResolveVibrateSettings(component);

            string sourceDirPath = settings.EmphasisSourceDirPath;
            foreach (VibrateUnitSetting unit in settings.EmphasisUnitsSettings)
            {
                string filePath = sourceDirPath.TrimEnd('/', '\\') + "/" + unit.SourcePath;
                EditorUtil.Vibrate.ExportEmphasisData(filePath, unit.DatasExportPath, settings);
            }

            return UniTask.CompletedTask;
        }

        /// <summary>
        /// Step：导出 Emphasis 震动类型（遍历所有 EmphasisUnitsSettings，逐单元导出类型代码文件）。
        /// 通过 Helpers.ResolveComponentOnNova 定位 VibrateComponent；
        /// 反射读取私有 m_Settings 字段取得 VibrateSettings；
        /// 未找到组件或 Settings 为 null 时抛出 InvalidOperationException 中断流水线。
        /// </summary>
        /// <param name="ctx">Runner 下发的运行时上下文。</param>
        /// <returns>完成的 UniTask。</returns>
        [PipifyStep("export.vibrate.emphasis.code", "导出 Vibrate Emphasis 类型", "导出资源/Vibrate")]
        internal static UniTask RunExportVibrateEmphasisCode(PipifyContext ctx)
        {
            VibrateComponent component = Helpers.ResolveComponentOnNova<VibrateComponent>();
            VibrateSettings settings = ResolveVibrateSettings(component);

            string sourceDirPath = settings.EmphasisSourceDirPath;
            foreach (VibrateUnitSetting unit in settings.EmphasisUnitsSettings)
            {
                string filePath = sourceDirPath.TrimEnd('/', '\\') + "/" + unit.SourcePath;
                EditorUtil.Vibrate.ExportEmphasisCode(filePath, unit.ClassesExportPath, settings);
            }

            return UniTask.CompletedTask;
        }

        /// <summary>
        /// Step：导出 Custom 震动数据（遍历所有 CustomUnitsSettings，逐单元导出数据文件）。
        /// 通过 Helpers.ResolveComponentOnNova 定位 VibrateComponent；
        /// 反射读取私有 m_Settings 字段取得 VibrateSettings；
        /// 未找到组件或 Settings 为 null 时抛出 InvalidOperationException 中断流水线。
        /// </summary>
        /// <param name="ctx">Runner 下发的运行时上下文。</param>
        /// <returns>完成的 UniTask。</returns>
        [PipifyStep("export.vibrate.custom.data", "导出 Vibrate Custom 数据", "导出资源/Vibrate")]
        internal static UniTask RunExportVibrateCustomData(PipifyContext ctx)
        {
            VibrateComponent component = Helpers.ResolveComponentOnNova<VibrateComponent>();
            VibrateSettings settings = ResolveVibrateSettings(component);

            string sourceDirPath = settings.CustomSourceDirPath;
            foreach (VibrateUnitSetting unit in settings.CustomUnitsSettings)
            {
                string filePath = sourceDirPath.TrimEnd('/', '\\') + "/" + unit.SourcePath;
                EditorUtil.Vibrate.ExportCustomData(filePath, unit.DatasExportPath, settings);
            }

            return UniTask.CompletedTask;
        }

        /// <summary>
        /// Step：导出 Custom 震动类型（遍历所有 CustomUnitsSettings，逐单元导出类型代码文件）。
        /// 通过 Helpers.ResolveComponentOnNova 定位 VibrateComponent；
        /// 反射读取私有 m_Settings 字段取得 VibrateSettings；
        /// 未找到组件或 Settings 为 null 时抛出 InvalidOperationException 中断流水线。
        /// </summary>
        /// <param name="ctx">Runner 下发的运行时上下文。</param>
        /// <returns>完成的 UniTask。</returns>
        [PipifyStep("export.vibrate.custom.code", "导出 Vibrate Custom 类型", "导出资源/Vibrate")]
        internal static UniTask RunExportVibrateCustomCode(PipifyContext ctx)
        {
            VibrateComponent component = Helpers.ResolveComponentOnNova<VibrateComponent>();
            VibrateSettings settings = ResolveVibrateSettings(component);

            string sourceDirPath = settings.CustomSourceDirPath;
            foreach (VibrateUnitSetting unit in settings.CustomUnitsSettings)
            {
                string filePath = sourceDirPath.TrimEnd('/', '\\') + "/" + unit.SourcePath;
                EditorUtil.Vibrate.ExportCustomCode(filePath, unit.ClassesExportPath, settings);
            }

            return UniTask.CompletedTask;
        }

        /// <summary>
        /// 通过反射从 VibrateComponent 取得私有字段 m_Settings；未配置时抛出 InvalidOperationException。
        /// </summary>
        /// <param name="component">已定位的 VibrateComponent 实例。</param>
        /// <returns>VibrateSettings 实例。</returns>
        private static VibrateSettings ResolveVibrateSettings(VibrateComponent component)
        {
            FieldInfo field = typeof(VibrateComponent).GetField("m_Settings", BindingFlags.NonPublic | BindingFlags.Instance);
            VibrateSettings settings = field?.GetValue(component) as VibrateSettings;
            if (settings == null)
            {
                throw new InvalidOperationException("[Pipify] VibrateComponent.m_Settings 未配置，请在 Nova Prefab 的 VibrateComponent Inspector 中设置振动参数后重试。");
            }
            return settings;
        }
    }
}
