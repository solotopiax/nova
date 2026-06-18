/***************************************************************
 * (c) copyright 2026 - 2030, Solotopia
 * All Rights Reserved.
 * -------------------------------------------------------------
 * filename:  EditorUtil.Localization.TextExporter.cs
 * author:    taoye
 * created:   2026/5/11
 * descrip:   本地化文本导出工具（文本数据 + 语言列表双轨）
 ***************************************************************/

using System;
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
            /// 本地化文本导出工具。
            /// 提供全链路导出、仅代码导出、仅数据导出和语言列表独立导出四条路径。
            /// 文本数据使用 Map 模式，通过 LocalizationTextExporter 完成三阶段全链路。
            /// </summary>
            public static class TextExporter
            {
                /// <summary>
                /// Luban target 名称（文本轨）。
                /// </summary>
                private const string c_TargetName = "localization-text";

                /// <summary>
                /// Luban manager 类名（文本轨）。
                /// </summary>
                private const string c_ManagerName = "LocalizationTextTables";

                /// <summary>
                /// 代码生成临时子目录名称。
                /// </summary>
                private const string c_CodegenTempSubDir = "_codegen";

                /// <summary>
                /// 临时目录根名称。
                /// </summary>
                private const string c_TempDirName = "_temp";

                /// <summary>
                /// UTF-8 无 BOM 编码实例。
                /// </summary>
                private static readonly System.Text.Encoding s_Utf8NoBom = new System.Text.UTF8Encoding(false);

                /// <summary>
                /// 全链路导出文本数据和 C# 类型：刷新数据类型名称 → 三阶段 Pipeline。
                /// </summary>
                /// <param name="settings">本地化设置实例。</param>
                /// <param name="sourceDirPath">文本数据源目录路径。</param>
                /// <param name="textUnitsSettingsProp">Inspector 序列化文本单元列表属性，用于刷新类型名称（可为 null）。</param>
                /// <param name="serializedObject">Inspector 序列化对象（可为 null）。</param>
                /// <param name="classExportPath">C# 类型输出目录。</param>
                /// <param name="customTemplateDirs">自定义模板目录列表（可为 null）。</param>
                /// <param name="supportedLanguagesExportPath">语言列表 JSON 导出路径（可为 null）。</param>
                /// <returns>是否导出成功。</returns>
                public static bool ExportTextAll(LocalizationSettings settings, string sourceDirPath, UnityEditor.SerializedProperty textUnitsSettingsProp, UnityEditor.SerializedObject serializedObject, string classExportPath, string[] customTemplateDirs, string supportedLanguagesExportPath)
                {
                    if (settings == null || string.IsNullOrEmpty(sourceDirPath))
                    {
                        Log.Warning(LogTag.Localization, "文本全量导出参数无效，导出已跳过。");
                        return false;
                    }

                    if (textUnitsSettingsProp != null && serializedObject != null)
                    {
                        EditorUtil.Luban.DataTypeNameHelper.DoRefreshAllDataTypeNames(sourceDirPath, textUnitsSettingsProp, serializedObject);
                    }

                    if (settings.TextUnitsSettings == null || settings.TextUnitsSettings.Count == 0)
                    {
                        Log.Warning(LogTag.Localization, "文本单元设置为空，导出已跳过。");
                        return false;
                    }

                    DataTableSettingsAdapter<LocalizationTextUnitSetting> adapter = new DataTableSettingsAdapter<LocalizationTextUnitSetting>(sourceDirPath, settings.TextUnitsSettings);
                    return LocalizationTextExporter.ExportAll(sourceDirPath, adapter, classExportPath, customTemplateDirs, supportedLanguagesExportPath);
                }

                /// <summary>
                /// 仅导出 C# 类型（Phase A）：PreFilter 取第一种语言列生成简化 Excel，调用 Pipeline.ExportCode。
                /// </summary>
                /// <param name="settings">本地化设置实例。</param>
                /// <param name="sourceDirPath">文本数据源目录路径。</param>
                /// <param name="classExportPath">C# 类型输出目录。</param>
                /// <param name="customTemplateDirs">自定义模板目录列表（可为 null）。</param>
                /// <returns>是否导出成功。</returns>
                public static bool ExportTextCode(LocalizationSettings settings, string sourceDirPath, string classExportPath, string[] customTemplateDirs)
                {
                    if (settings == null || string.IsNullOrEmpty(sourceDirPath) || string.IsNullOrEmpty(classExportPath))
                    {
                        Log.Warning(LogTag.Localization, "文本代码导出参数无效，导出已跳过。");
                        return false;
                    }

                    if (settings.TextUnitsSettings == null || settings.TextUnitsSettings.Count == 0)
                    {
                        Log.Warning(LogTag.Localization, "文本单元设置为空，代码导出已跳过。");
                        return false;
                    }

                    string codegenTempDir = Util.SysIO.Path.Combine(sourceDirPath, c_TempDirName, c_CodegenTempSubDir);
                    try
                    {
                        LocalizationExcelPreFilter.FilterForCodeGen(sourceDirPath, codegenTempDir);

                        DataTableSettingsAdapter<LocalizationTextUnitSetting> adapter = new DataTableSettingsAdapter<LocalizationTextUnitSetting>(sourceDirPath, settings.TextUnitsSettings);
                        string configDir = EditorUtil.Luban.ConfigSyncer.GetConfigDirPath(sourceDirPath);
                        string confPath = Util.SysIO.Path.Combine(configDir, EditorUtil.Luban.ConfigSyncer.c_LubanConfFileName);
                        string tablesXmlPath = Util.SysIO.Path.Combine(configDir, EditorUtil.Luban.ConfigSyncer.c_TablesXmlFileName);
                        string topModule = EditorUtil.Config.RuntimeProvider.GetNamespace();

                        CodegenResolvedSettings codegenSettings = new CodegenResolvedSettings(adapter, c_CodegenTempSubDir);
                        var ctx = new EditorUtil.Luban.LubanExportContext
                        {
                            SourceDirPath = sourceDirPath,
                            ConfPath = confPath,
                            TargetName = c_TargetName,
                            ManagerName = c_ManagerName,
                            TopModule = topModule,
                            OutputCodeDir = classExportPath,
                            CustomTemplateDirs = customTemplateDirs,
                            TablesXmlPath = tablesXmlPath,
                            Settings = codegenSettings,
                        };

                        bool success = EditorUtil.Luban.Pipeline.ExportCode(ctx);
                        if (success)
                        {
                            AssetDatabase.Refresh();
                        }
                        return success;
                    }
                    finally
                    {
                        string tempDir = Util.SysIO.Path.Combine(sourceDirPath, c_TempDirName);
                        CleanupTempDir(tempDir);
                    }
                }

                /// <summary>
                /// 仅导出文本数据（Phase B）：按每种语言执行 PreFilter → Pipeline.ExportData。
                /// DatasExportPath 中的 {0} 占位符替换为对应语言名称。
                /// </summary>
                /// <param name="settings">本地化设置实例。</param>
                /// <param name="sourceDirPath">文本数据源目录路径。</param>
                /// <returns>是否全部成功。</returns>
                public static bool ExportTextData(LocalizationSettings settings, string sourceDirPath)
                {
                    if (settings == null || string.IsNullOrEmpty(sourceDirPath))
                    {
                        Log.Warning(LogTag.Localization, "文本数据导出参数无效，导出已跳过。");
                        return false;
                    }

                    if (settings.TextUnitsSettings == null || settings.TextUnitsSettings.Count == 0)
                    {
                        Log.Warning(LogTag.Localization, "文本单元设置为空，数据导出已跳过。");
                        return false;
                    }

                    HashSet<string> allLanguages = LocalizationExcelPreFilter.ExtractAllLanguageColumns(sourceDirPath);
                    if (allLanguages == null || allLanguages.Count == 0)
                    {
                        Log.Warning(LogTag.Localization, "未从数据源目录中提取到任何语言列，导出已跳过。");
                        return false;
                    }

                    DataTableSettingsAdapter<LocalizationTextUnitSetting> adapter = new DataTableSettingsAdapter<LocalizationTextUnitSetting>(sourceDirPath, settings.TextUnitsSettings);
                    string configDir = EditorUtil.Luban.ConfigSyncer.GetConfigDirPath(sourceDirPath);
                    string confPath = Util.SysIO.Path.Combine(configDir, EditorUtil.Luban.ConfigSyncer.c_LubanConfFileName);
                    string tablesXmlPath = Util.SysIO.Path.Combine(configDir, EditorUtil.Luban.ConfigSyncer.c_TablesXmlFileName);
                    string topModule = EditorUtil.Config.RuntimeProvider.GetNamespace();
                    string tempDir = Util.SysIO.Path.Combine(sourceDirPath, c_TempDirName);

                    try
                    {
                        bool allSuccess = true;
                        foreach (string languageName in allLanguages)
                        {
                            string langTempDir = Util.SysIO.Path.Combine(tempDir, languageName);
                            LocalizationExcelPreFilter.FilterForLanguage(sourceDirPath, langTempDir, languageName);

                            LangResolvedSettings langSettings = new LangResolvedSettings(adapter, languageName);
                            var ctx = new EditorUtil.Luban.LubanExportContext
                            {
                                SourceDirPath = sourceDirPath,
                                ConfPath = confPath,
                                TargetName = c_TargetName,
                                ManagerName = c_ManagerName,
                                TopModule = topModule,
                                TablesXmlPath = tablesXmlPath,
                                Settings = langSettings,
                            };

                            bool success = EditorUtil.Luban.Pipeline.ExportData(ctx);
                            if (!success)
                            {
                                Log.Error(LogTag.Localization, "语言 '{0}' 文本数据导出失败。", languageName);
                                allSuccess = false;
                            }
                        }

                        if (allSuccess)
                        {
                            AssetDatabase.Refresh();
                        }
                        return allSuccess;
                    }
                    finally
                    {
                        CleanupTempDir(tempDir);
                    }
                }

                /// <summary>
                /// 独立导出语言列表 JSON：从数据源目录提取所有语言列，排序后写入指定路径。
                /// </summary>
                /// <param name="sourceDirPath">文本数据源目录路径。</param>
                /// <param name="exportPath">输出文件路径（工程相对路径）。</param>
                /// <returns>是否导出成功。</returns>
                public static bool ExportSupportedLanguages(string sourceDirPath, string exportPath)
                {
                    if (string.IsNullOrEmpty(sourceDirPath) || string.IsNullOrEmpty(exportPath))
                    {
                        Log.Warning(LogTag.Localization, "语言列表导出参数无效，导出已跳过。");
                        return false;
                    }

                    HashSet<string> allLanguages = LocalizationExcelPreFilter.ExtractAllLanguageColumns(sourceDirPath);
                    if (allLanguages == null || allLanguages.Count == 0)
                    {
                        Log.Warning(LogTag.Localization, "未从数据源目录中提取到任何语言列，语言列表导出已跳过。");
                        return false;
                    }

                    List<string> sortedLanguages = new List<string>(allLanguages);
                    sortedLanguages.Sort(StringComparer.Ordinal);

                    string fullOutputPath = EditorUtil.FileSystem.GetProjectFullPath(exportPath);
                    string outputDir = Util.SysIO.Path.GetDirectoryName(fullOutputPath);
                    if (!string.IsNullOrEmpty(outputDir) && !Util.SysIO.Directory.Exists(outputDir))
                    {
                        Util.SysIO.Directory.CreateIfNotExist(outputDir);
                    }

                    string json = Util.Json.Serialize(sortedLanguages);
                    Util.SysIO.File.WriteAllTextSync(fullOutputPath, json, s_Utf8NoBom);
                    AssetDatabase.Refresh();
                    Log.Debug(LogTag.Localization, "语言列表导出完成：{0} 种语言 -> {1}", sortedLanguages.Count, exportPath);
                    return true;
                }

                /// <summary>
                /// 清理临时目录，异常时仅警告不中断。
                /// </summary>
                /// <param name="tempDir">临时目录路径。</param>
                private static void CleanupTempDir(string tempDir)
                {
                    if (string.IsNullOrEmpty(tempDir) || !Util.SysIO.Directory.Exists(tempDir))
                    {
                        return;
                    }

                    try
                    {
                        Util.SysIO.Directory.Delete(tempDir, true);
                    }
                    catch (Exception e)
                    {
                        Log.Warning(LogTag.Localization, "清理临时目录失败：{0}，异常：{1}", tempDir, e.Message);
                    }
                }

                /// <summary>
                /// 按代码生成模式解析后的数据表设置，LubanInputPath 覆盖为 _temp/_codegen/{fileName}。
                /// </summary>
                private sealed class CodegenResolvedSettings : IDataTableSettings
                {
#if UNITY_EDITOR
                    /// <inheritdoc />
                    public string SourceDirPath { get; }
#endif

                    /// <inheritdoc />
                    public IReadOnlyList<IDataTableUnitSetting> Units { get; }

                    /// <summary>
                    /// 构造代码生成解析设置。
                    /// </summary>
                    /// <param name="original">原始适配器。</param>
                    /// <param name="codegenSubDir">Luban 临时输入子目录名称。</param>
                    public CodegenResolvedSettings(IDataTableSettings original, string codegenSubDir)
                    {
#if UNITY_EDITOR
                        SourceDirPath = original.SourceDirPath;
#endif
                        var resolved = new List<IDataTableUnitSetting>();
                        for (int i = 0; i < original.Units.Count; i++)
                        {
                            resolved.Add(new LubanPathOverrideUnitSetting(original.Units[i], null, codegenSubDir));
                        }
                        Units = resolved;
                    }
                }

                /// <summary>
                /// 按语言解析后的数据表设置，DatasExportPath 中的 {0} 替换为语言名称，
                /// LubanInputPath 覆盖为 _temp/{languageName}/{fileName}。
                /// </summary>
                private sealed class LangResolvedSettings : IDataTableSettings
                {
#if UNITY_EDITOR
                    /// <inheritdoc />
                    public string SourceDirPath { get; }
#endif

                    /// <inheritdoc />
                    public IReadOnlyList<IDataTableUnitSetting> Units { get; }

                    /// <summary>
                    /// 构造语言解析设置。
                    /// </summary>
                    /// <param name="original">原始适配器。</param>
                    /// <param name="languageName">当前语言名称。</param>
                    public LangResolvedSettings(IDataTableSettings original, string languageName)
                    {
#if UNITY_EDITOR
                        SourceDirPath = original.SourceDirPath;
#endif
                        var resolved = new List<IDataTableUnitSetting>();
                        for (int i = 0; i < original.Units.Count; i++)
                        {
                            resolved.Add(new LubanPathOverrideUnitSetting(original.Units[i], languageName, languageName));
                        }
                        Units = resolved;
                    }
                }

                /// <summary>
                /// 通用单元路径覆盖适配器，DatasExportPath 替换 {0} 占位符，LubanInputPath 覆盖为临时目录路径。
                /// </summary>
                private sealed class LubanPathOverrideUnitSetting : IDataTableUnitSetting
                {
                    /// <summary>
                    /// 原始单元设置。
                    /// </summary>
                    private readonly IDataTableUnitSetting m_Original;

                    /// <summary>
                    /// 当前语言名称（null 时不替换 DatasExportPath 中的占位符）。
                    /// </summary>
                    private readonly string m_LanguageName;

                    /// <summary>
                    /// Luban 临时输入子目录名称，覆盖 LubanInputPath。
                    /// </summary>
                    private readonly string m_LubanInputSubDir;

#if UNITY_EDITOR
                    /// <inheritdoc />
                    string IDataTableUnitSetting.SourcePath => m_Original.SourcePath;

                    /// <inheritdoc />
                    string IDataTableUnitSetting.DatasExportPath => m_Original.DatasExportPath?.Replace("{0}", m_LanguageName);

                    /// <inheritdoc />
                    string IDataTableUnitSetting.ClassesExportPath => m_Original.ClassesExportPath;

                    /// <inheritdoc />
                    string IDataTableUnitSetting.LubanInputPath =>
                        string.IsNullOrEmpty(m_LubanInputSubDir)
                            ? m_Original.LubanInputPath
                            : c_TempDirName + "/" + m_LubanInputSubDir + "/" + Util.SysIO.Path.GetFileNameWithoutExtension(m_Original.SourcePath);
#endif

                    /// <inheritdoc />
                    string IDataTableUnitSetting.AssetLocation => m_Original.AssetLocation;

                    /// <inheritdoc />
                    DataTableMode IDataTableUnitSetting.Mode => m_Original.Mode;

                    /// <inheritdoc />
                    string IDataTableUnitSetting.IndexField => m_Original.IndexField;

                    /// <inheritdoc />
                    IReadOnlyList<string> IDataTableUnitSetting.DataTypeNames => m_Original.DataTypeNames;

                    /// <summary>
                    /// 构造通用单元路径覆盖适配器。
                    /// </summary>
                    /// <param name="original">原始单元设置。</param>
                    /// <param name="languageName">当前语言名称（可为 null）。</param>
                    /// <param name="lubanInputSubDir">Luban 临时输入子目录名称（可为 null）。</param>
                    public LubanPathOverrideUnitSetting(IDataTableUnitSetting original, string languageName, string lubanInputSubDir)
                    {
                        m_Original = original;
                        m_LanguageName = languageName;
                        m_LubanInputSubDir = lubanInputSubDir;
                    }
                }
            }
        }
    }
}
