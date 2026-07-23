/***************************************************************
 * (c) copyright 2026 - 2030, Solotopia
 * All Rights Reserved.
 * -------------------------------------------------------------
 * filename:  EditorUtil.Localization.TextExporter.cs
 * author:    taoye
 * created:   2026/5/11
 * descrip:   本地化文本导出公共门面
 ***************************************************************/

using NovaFramework.Runtime;

namespace NovaFramework.Editor
{
    public static partial class EditorUtil
    {
        public static partial class Localization
        {
            /// <summary>
            /// 本地化文本导出公共门面。
            /// 阶段编排、语言排序和临时目录生命周期统一由 LocalizationTextExporter 负责。
            /// </summary>
            public static class TextExporter
            {
                /// <summary>
                /// 全链路导出文本数据、C# 类型和支持语言列表。
                /// </summary>
                public static bool ExportTextAll(LocalizationSettings settings, string sourceDirPath,
                    string classExportPath, string[] customTemplateDirs, string supportedLanguagesExportPath)
                {
                    if (!TryCreateSettings(settings, sourceDirPath, out IDataTableSettings adapter))
                    {
                        return false;
                    }

                    return LocalizationTextExporter.ExportAll(
                        sourceDirPath,
                        adapter,
                        classExportPath,
                        customTemplateDirs,
                        supportedLanguagesExportPath);
                }

                /// <summary>
                /// 仅导出 C# 类型。
                /// </summary>
                public static bool ExportTextCode(LocalizationSettings settings, string sourceDirPath,
                    string classExportPath, string[] customTemplateDirs)
                {
                    if (string.IsNullOrEmpty(classExportPath) ||
                        !TryCreateSettings(settings, sourceDirPath, out IDataTableSettings adapter))
                    {
                        return false;
                    }

                    return LocalizationTextExporter.ExportCode(
                        sourceDirPath,
                        adapter,
                        classExportPath,
                        customTemplateDirs);
                }

                /// <summary>
                /// 仅导出所有语言的文本数据。
                /// </summary>
                public static bool ExportTextData(LocalizationSettings settings, string sourceDirPath)
                {
                    if (!TryCreateSettings(settings, sourceDirPath, out IDataTableSettings adapter))
                    {
                        return false;
                    }

                    return LocalizationTextExporter.ExportData(sourceDirPath, adapter);
                }

                /// <summary>
                /// 独立导出支持语言列表。
                /// </summary>
                public static bool ExportSupportedLanguages(string sourceDirPath, string exportPath)
                {
                    return LocalizationTextExporter.ExportSupportedLanguages(sourceDirPath, exportPath);
                }

                private static bool TryCreateSettings(LocalizationSettings settings, string sourceDirPath,
                    out IDataTableSettings adapter)
                {
                    adapter = null;
                    if (settings == null || string.IsNullOrEmpty(sourceDirPath))
                    {
                        Log.Warning(LogTag.Localization, "文本导出参数无效，导出已跳过。");
                        return false;
                    }

                    if (settings.TextUnitsSettings == null || settings.TextUnitsSettings.Count == 0)
                    {
                        Log.Warning(LogTag.Localization, "文本单元设置为空，导出已跳过。");
                        return false;
                    }

                    adapter = new DataTableSettingsAdapter<LocalizationTextUnitSetting>(
                        sourceDirPath,
                        settings.TextUnitsSettings);
                    return true;
                }
            }
        }
    }
}
