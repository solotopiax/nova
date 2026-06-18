/***************************************************************
 * (c) copyright 2026 - 2030, Solotopia
 * All Rights Reserved.
 * -------------------------------------------------------------
 * filename:  EditorUtil.Localization.FontExporter.cs
 * author:    taoye
 * created:   2026/5/11
 * descrip:   本地化字体导出工具（字体配置 Pipeline 编排）
 ***************************************************************/

using System.Collections.Generic;
using System.Linq;
using NovaFramework.Runtime;
using UnityEditor;

namespace NovaFramework.Editor
{
    public static partial class EditorUtil
    {
        public static partial class Localization
        {
            /// <summary>
            /// 本地化字体导出工具。
            /// 提供全链路导出（代码+数据）、仅代码导出和仅数据导出三条路径，
            /// 通过标准 Luban Pipeline 实现，不需要 PreFilter 多语言处理。
            /// </summary>
            public static class FontExporter
            {
                /// <summary>
                /// Luban target 名称（字体轨）。
                /// </summary>
                private const string c_TargetName = "localization-font";

                /// <summary>
                /// Luban manager 类名（字体轨）。
                /// </summary>
                private const string c_ManagerName = "LocalizationFontTables";

                /// <summary>
                /// 全链路导出字体数据和 C# 类型：刷新数据类型名称 → 构建上下文 → Pipeline.ExportAll 或 ExportData。
                /// 当 classExportPath 非空时执行 Pipeline.ExportAll（代码+数据），否则仅 Pipeline.ExportData。
                /// </summary>
                /// <param name="settings">本地化设置实例。</param>
                /// <param name="sourceDirPath">字体数据源目录路径。</param>
                /// <param name="fontUnitsSettingsProp">Inspector 序列化字体单元列表属性，用于刷新类型名称（可为 null）。</param>
                /// <param name="serializedObject">Inspector 序列化对象（可为 null）。</param>
                /// <param name="classExportPath">C# 类型输出目录（空时仅导数据）。</param>
                /// <returns>是否导出成功。</returns>
                public static bool ExportFontAll(LocalizationSettings settings, string sourceDirPath, UnityEditor.SerializedProperty fontUnitsSettingsProp, UnityEditor.SerializedObject serializedObject, string classExportPath)
                {
                    if (settings == null || string.IsNullOrEmpty(sourceDirPath))
                    {
                        Log.Warning(LogTag.Localization, "字体全量导出参数无效，导出已跳过。");
                        return false;
                    }

                    if (fontUnitsSettingsProp != null && serializedObject != null)
                    {
                        EditorUtil.Luban.DataTypeNameHelper.DoRefreshAllDataTypeNames(sourceDirPath, fontUnitsSettingsProp, serializedObject);
                    }

                    if (settings.FontUnitsSettings == null || settings.FontUnitsSettings.Count == 0)
                    {
                        Log.Warning(LogTag.Localization, "字体单元设置为空，导出已跳过。");
                        return false;
                    }

                    DataTableSettingsAdapter<LocalizationFontUnitSetting> adapter = new DataTableSettingsAdapter<LocalizationFontUnitSetting>(sourceDirPath, settings.FontUnitsSettings);
                    EditorUtil.Luban.LubanExportContext ctx = EditorUtil.Luban.ExportHelper.BuildExportContext(sourceDirPath, adapter, c_TargetName, c_ManagerName);
                    ctx.OutputCodeDir = classExportPath;

                    bool success;
                    if (!string.IsNullOrEmpty(classExportPath))
                    {
                        success = EditorUtil.Luban.Pipeline.ExportAll(ctx);
                    }
                    else
                    {
                        success = EditorUtil.Luban.Pipeline.ExportData(ctx);
                    }

                    if (success)
                    {
                        AssetDatabase.Refresh();
                    }
                    return success;
                }

                /// <summary>
                /// 仅导出字体 C# 类型：构建上下文 → Pipeline.ExportCode。
                /// </summary>
                /// <param name="settings">本地化设置实例。</param>
                /// <param name="sourceDirPath">字体数据源目录路径。</param>
                /// <param name="classExportPath">C# 类型输出目录。</param>
                /// <returns>是否导出成功。</returns>
                public static bool ExportFontCode(LocalizationSettings settings, string sourceDirPath, string classExportPath)
                {
                    if (settings == null || string.IsNullOrEmpty(sourceDirPath) || string.IsNullOrEmpty(classExportPath))
                    {
                        Log.Warning(LogTag.Localization, "字体代码导出参数无效，导出已跳过。");
                        return false;
                    }

                    if (settings.FontUnitsSettings == null || settings.FontUnitsSettings.Count == 0)
                    {
                        Log.Warning(LogTag.Localization, "字体单元设置为空，代码导出已跳过。");
                        return false;
                    }

                    DataTableSettingsAdapter<LocalizationFontUnitSetting> adapter = new DataTableSettingsAdapter<LocalizationFontUnitSetting>(sourceDirPath, settings.FontUnitsSettings);
                    EditorUtil.Luban.LubanExportContext ctx = EditorUtil.Luban.ExportHelper.BuildExportContext(sourceDirPath, adapter, c_TargetName, c_ManagerName);
                    ctx.OutputCodeDir = classExportPath;

                    bool success = EditorUtil.Luban.Pipeline.ExportCode(ctx);
                    if (success)
                    {
                        AssetDatabase.Refresh();
                    }
                    return success;
                }

                /// <summary>
                /// 仅导出字体数据：构建上下文 → Pipeline.ExportData。
                /// </summary>
                /// <param name="settings">本地化设置实例。</param>
                /// <param name="sourceDirPath">字体数据源目录路径。</param>
                /// <returns>是否导出成功。</returns>
                public static bool ExportFontData(LocalizationSettings settings, string sourceDirPath)
                {
                    if (settings == null || string.IsNullOrEmpty(sourceDirPath))
                    {
                        Log.Warning(LogTag.Localization, "字体数据导出参数无效，导出已跳过。");
                        return false;
                    }

                    if (settings.FontUnitsSettings == null || settings.FontUnitsSettings.Count == 0)
                    {
                        Log.Warning(LogTag.Localization, "字体单元设置为空，数据导出已跳过。");
                        return false;
                    }

                    DataTableSettingsAdapter<LocalizationFontUnitSetting> adapter = new DataTableSettingsAdapter<LocalizationFontUnitSetting>(sourceDirPath, settings.FontUnitsSettings);
                    EditorUtil.Luban.LubanExportContext ctx = EditorUtil.Luban.ExportHelper.BuildExportContext(sourceDirPath, adapter, c_TargetName, c_ManagerName);

                    bool success = EditorUtil.Luban.Pipeline.ExportData(ctx);
                    if (success)
                    {
                        AssetDatabase.Refresh();
                    }
                    return success;
                }
            }
        }
    }
}
