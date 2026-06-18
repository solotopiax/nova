/***************************************************************
 * (c) copyright 2026 - 2030, Solotopia
 * All Rights Reserved.
 * -------------------------------------------------------------
 * filename:  PipifySteps.Export.Localization.cs
 * author:    taoye
 * created:   2026/5/11
 * descrip:   Pipify 内置 Step 合集 —— 导出分组（Localization 模块）
 ***************************************************************/

using System;
using Cysharp.Threading.Tasks;
using NovaFramework.Runtime;
using UnityEditor;

namespace NovaFramework.Editor
{
    /// <summary>
    /// Pipify 内置 Step 合集（partial）。
    /// 本文件收录导出分组 Localization 子类的原子操作：文本数据、文本类型、语言列表、字体数据、字体类型。
    /// 每个方法仅做薄封装，调用 EditorUtil.Localization.TextExporter / FontExporter 对应 public API。
    /// </summary>
    internal static partial class PipifySteps
    {
        /// <summary>
        /// Step：导出本地化文本数据（按所有语言分批，通过 ExcelPreFilter 切列后逐语言调 Luban）。
        /// 通过 Helpers.ResolveComponentOnNova 定位 LocalizationComponent；
        /// 从 LocalizationComponent.LocalizationSettings.TextSourceDirPath 获取数据源目录；
        /// 调用 EditorUtil.Localization.TextExporter.ExportTextData。
        /// </summary>
        /// <param name="ctx">Runner 下发的运行时上下文。</param>
        /// <returns>完成的 UniTask。</returns>
        [PipifyStep("export.localization.text.data", "导出 Localization 文本数据", "导出资源/Localization")]
        internal static UniTask RunExportLocalizationTextData(PipifyContext ctx)
        {
            LocalizationComponent component = Helpers.ResolveComponentOnNova<LocalizationComponent>();
            LocalizationSettings settings = component.LocalizationSettings;
            string sourceDirPath = settings.TextSourceDirPath;

            if (string.IsNullOrEmpty(sourceDirPath))
            {
                throw new InvalidOperationException("[Pipify] LocalizationComponent.LocalizationSettings.TextSourceDirPath 未配置，请在 Inspector 中填写文本数据源目录路径。");
            }

            bool success = EditorUtil.Localization.TextExporter.ExportTextData(settings, sourceDirPath);
            if (!success)
            {
                throw new InvalidOperationException("[Pipify] 本地化文本数据导出失败，请查看 Console 日志排查原因。");
            }

            return UniTask.CompletedTask;
        }

        /// <summary>
        /// Step：导出本地化文本 C# 类型（Phase A，PreFilter 取第一种语言列生成简化 Excel 后调 Luban ExportCode）。
        /// 从 LocalizationComponent.LocalizationSettings 取文本数据源目录与类型导出路径；
        /// classExportPath 取 TextUnitsSettings 中第一个非空 ClassesExportPath；
        /// 调用 EditorUtil.Localization.TextExporter.ExportTextCode。
        /// </summary>
        /// <param name="ctx">Runner 下发的运行时上下文。</param>
        /// <returns>完成的 UniTask。</returns>
        [PipifyStep("export.localization.text.code", "导出 Localization 文本类型", "导出资源/Localization")]
        internal static UniTask RunExportLocalizationTextCode(PipifyContext ctx)
        {
            LocalizationComponent component = Helpers.ResolveComponentOnNova<LocalizationComponent>();
            LocalizationSettings settings = component.LocalizationSettings;
            string sourceDirPath = settings.TextSourceDirPath;

            if (string.IsNullOrEmpty(sourceDirPath))
            {
                throw new InvalidOperationException("[Pipify] LocalizationComponent.LocalizationSettings.TextSourceDirPath 未配置，请在 Inspector 中填写文本数据源目录路径。");
            }

            string classExportPath = string.Empty;
            if (settings.TextUnitsSettings != null)
            {
                for (int i = 0; i < settings.TextUnitsSettings.Count; i++)
                {
                    if (!string.IsNullOrEmpty(settings.TextUnitsSettings[i].ClassesExportPath))
                    {
                        classExportPath = settings.TextUnitsSettings[i].ClassesExportPath;
                        break;
                    }
                }
            }

            if (string.IsNullOrEmpty(classExportPath))
            {
                throw new InvalidOperationException("[Pipify] 未找到有效的文本类型导出路径（ClassesExportPath），请在 Inspector 中为文本单元填写类型导出路径。");
            }

            string[] customTemplateDirs = EditorUtil.Luban.ExportHelper.GetLubanCustomTemplateDirs("localization-text");
            bool success = EditorUtil.Localization.TextExporter.ExportTextCode(settings, sourceDirPath, classExportPath, customTemplateDirs);
            if (!success)
            {
                throw new InvalidOperationException("[Pipify] 本地化文本类型导出失败，请查看 Console 日志排查原因。");
            }

            return UniTask.CompletedTask;
        }

        /// <summary>
        /// Step：导出本地化语言列表 JSON（从数据源目录提取所有语言列，排序后写入指定路径）。
        /// 从 LocalizationComponent 通过 SerializedObject 读取 m_SupportedLanguagesJsonExportPath；
        /// 调用 EditorUtil.Localization.TextExporter.ExportSupportedLanguages。
        /// </summary>
        /// <param name="ctx">Runner 下发的运行时上下文。</param>
        /// <returns>完成的 UniTask。</returns>
        [PipifyStep("export.localization.supportedlangs", "导出 Localization 语言列表", "导出资源/Localization")]
        internal static UniTask RunExportLocalizationSupportedLanguages(PipifyContext ctx)
        {
            LocalizationComponent component = Helpers.ResolveComponentOnNova<LocalizationComponent>();
            LocalizationSettings settings = component.LocalizationSettings;
            string sourceDirPath = settings.TextSourceDirPath;

            if (string.IsNullOrEmpty(sourceDirPath))
            {
                throw new InvalidOperationException("[Pipify] LocalizationComponent.LocalizationSettings.TextSourceDirPath 未配置，请在 Inspector 中填写文本数据源目录路径。");
            }

            SerializedObject so = new SerializedObject(component);
            SerializedProperty exportPathProp = so.FindProperty("m_SupportedLanguagesJsonExportPath");
            string exportPath = exportPathProp?.stringValue ?? string.Empty;

            if (string.IsNullOrEmpty(exportPath))
            {
                throw new InvalidOperationException("[Pipify] LocalizationComponent.m_SupportedLanguagesJsonExportPath 未配置，请在 Inspector 中填写语言列表 JSON 导出路径。");
            }

            bool success = EditorUtil.Localization.TextExporter.ExportSupportedLanguages(sourceDirPath, exportPath);
            if (!success)
            {
                throw new InvalidOperationException("[Pipify] 本地化语言列表导出失败，请查看 Console 日志排查原因。");
            }

            return UniTask.CompletedTask;
        }

        /// <summary>
        /// Step：导出本地化字体数据（构建上下文后调 Luban Pipeline.ExportData）。
        /// 从 LocalizationComponent.LocalizationSettings.FontSourceDirPath 获取字体数据源目录；
        /// 调用 EditorUtil.Localization.FontExporter.ExportFontData。
        /// </summary>
        /// <param name="ctx">Runner 下发的运行时上下文。</param>
        /// <returns>完成的 UniTask。</returns>
        [PipifyStep("export.localization.font.data", "导出 Localization 字体数据", "导出资源/Localization")]
        internal static UniTask RunExportLocalizationFontData(PipifyContext ctx)
        {
            LocalizationComponent component = Helpers.ResolveComponentOnNova<LocalizationComponent>();
            LocalizationSettings settings = component.LocalizationSettings;
            string sourceDirPath = settings.FontSourceDirPath;

            if (string.IsNullOrEmpty(sourceDirPath))
            {
                throw new InvalidOperationException("[Pipify] LocalizationComponent.LocalizationSettings.FontSourceDirPath 未配置，请在 Inspector 中填写字体数据源目录路径。");
            }

            bool success = EditorUtil.Localization.FontExporter.ExportFontData(settings, sourceDirPath);
            if (!success)
            {
                throw new InvalidOperationException("[Pipify] 本地化字体数据导出失败，请查看 Console 日志排查原因。");
            }

            return UniTask.CompletedTask;
        }

        /// <summary>
        /// Step：导出本地化字体 C# 类型（构建上下文后调 Luban Pipeline.ExportCode）。
        /// 从 LocalizationComponent.LocalizationSettings 取字体数据源目录与类型导出路径；
        /// classExportPath 取 FontUnitsSettings 中第一个非空 ClassesExportPath；
        /// 调用 EditorUtil.Localization.FontExporter.ExportFontCode。
        /// </summary>
        /// <param name="ctx">Runner 下发的运行时上下文。</param>
        /// <returns>完成的 UniTask。</returns>
        [PipifyStep("export.localization.font.code", "导出 Localization 字体类型", "导出资源/Localization")]
        internal static UniTask RunExportLocalizationFontCode(PipifyContext ctx)
        {
            LocalizationComponent component = Helpers.ResolveComponentOnNova<LocalizationComponent>();
            LocalizationSettings settings = component.LocalizationSettings;
            string sourceDirPath = settings.FontSourceDirPath;

            if (string.IsNullOrEmpty(sourceDirPath))
            {
                throw new InvalidOperationException("[Pipify] LocalizationComponent.LocalizationSettings.FontSourceDirPath 未配置，请在 Inspector 中填写字体数据源目录路径。");
            }

            string classExportPath = string.Empty;
            if (settings.FontUnitsSettings != null)
            {
                for (int i = 0; i < settings.FontUnitsSettings.Count; i++)
                {
                    if (!string.IsNullOrEmpty(settings.FontUnitsSettings[i].ClassesExportPath))
                    {
                        classExportPath = settings.FontUnitsSettings[i].ClassesExportPath;
                        break;
                    }
                }
            }

            if (string.IsNullOrEmpty(classExportPath))
            {
                throw new InvalidOperationException("[Pipify] 未找到有效的字体类型导出路径（ClassesExportPath），请在 Inspector 中为字体单元填写类型导出路径。");
            }

            bool success = EditorUtil.Localization.FontExporter.ExportFontCode(settings, sourceDirPath, classExportPath);
            if (!success)
            {
                throw new InvalidOperationException("[Pipify] 本地化字体类型导出失败，请查看 Console 日志排查原因。");
            }

            return UniTask.CompletedTask;
        }
    }
}
