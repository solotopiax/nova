/***************************************************************
 * (c) copyright 2026 - 2030, Solotopia
 * All Rights Reserved.
 * -------------------------------------------------------------
 * filename:  LocalizationTextExporter.cs
 * author:    taoye
 * created:   2026/4/19
 * descrip:   本地化文本导出器（PreFilter + Luban Pipeline 编排）
 ***************************************************************/

using System;
using System.Collections.Generic;
using NovaFramework.Runtime;
using Newtonsoft.Json;
using UnityEditor;

namespace NovaFramework.Editor
{
    /// <summary>
    /// 本地化文本导出器，编排 PreFilter + Luban Pipeline 完成全链路导出。
    /// <para>Phase A — C# 类型生成：PreFilter 取第一种语言 → Pipeline.ExportCode。</para>
    /// <para>Phase B — 按语言数据导出：每种语言 PreFilter → Pipeline.ExportData。</para>
    /// <para>Phase C — MapPropGen + 语言列表导出。</para>
    /// </summary>
    internal static class LocalizationTextExporter
    {
        /// <summary>
        /// Luban target 名称。
        /// </summary>
        private const string c_TargetName = "localization-text";

        /// <summary>
        /// Luban manager 类名。
        /// </summary>
        private const string c_ManagerName = "LocalizationTextTables";

        /// <summary>
        /// 代码生成临时子目录名称。
        /// </summary>
        private const string c_CodegenTempSubDir = "_codegen";

        /// <summary>
        /// 临时目录名称。
        /// </summary>
        private const string c_TempDirName = "_temp";

        /// <summary>
        /// UTF-8 无 BOM 编码。
        /// </summary>
        private static readonly System.Text.Encoding s_Utf8NoBom = new System.Text.UTF8Encoding(false);

        /// <summary>
        /// 导出全部文本数据和类型：三阶段全链路导出。
        /// </summary>
        /// <param name="sourceDirPath">文本数据源目录路径。</param>
        /// <param name="settings">文本数据表设置适配器（IDataTableSettings）。</param>
        /// <param name="classExportPath">C# 类型输出目录。</param>
        /// <param name="customTemplateDirs">自定义模板目录列表（可为 null）。</param>
        /// <param name="supportedLanguagesExportPath">语言列表 JSON 导出路径（工程相对路径，可为 null）。</param>
        /// <returns>是否导出成功。</returns>
        public static bool ExportAll(string sourceDirPath, IDataTableSettings settings, string classExportPath, string[] customTemplateDirs, string supportedLanguagesExportPath)
        {
            if (string.IsNullOrEmpty(sourceDirPath) || settings == null || settings.Units == null || settings.Units.Count == 0)
            {
                Log.Warning(LogTag.Localization, "文本导出参数无效，导出已跳过。");
                return false;
            }

            string tempDir = Util.SysIO.Path.Combine(sourceDirPath, c_TempDirName);
            string codegenTempDir = Util.SysIO.Path.Combine(tempDir, c_CodegenTempSubDir);

            try
            {
                HashSet<string> allLanguages = LocalizationExcelPreFilter.ExtractAllLanguageColumns(sourceDirPath);
                if (allLanguages == null || allLanguages.Count == 0)
                {
                    Log.Warning(LogTag.Localization, "未从数据源目录中提取到任何语言列，导出已跳过。");
                    return false;
                }

                string configDir = EditorUtil.Luban.ConfigSyncer.GetConfigDirPath(sourceDirPath);
                string confPath = Util.SysIO.Path.Combine(configDir, EditorUtil.Luban.ConfigSyncer.c_LubanConfFileName);
                string tablesXmlPath = Util.SysIO.Path.Combine(configDir, EditorUtil.Luban.ConfigSyncer.c_TablesXmlFileName);
                string topModule = EditorUtil.Config.RuntimeProvider.GetNamespace();

                bool phaseASuccess = ExportPhaseA(sourceDirPath, settings, codegenTempDir, classExportPath, customTemplateDirs, confPath, tablesXmlPath, topModule);
                if (!phaseASuccess)
                {
                    Log.Error(LogTag.Localization, "Phase A（C# 类型生成）失败，导出中止。");
                    return false;
                }

                string firstLanguage = null;
                bool phaseBSuccess = ExportPhaseB(sourceDirPath, settings, tempDir, confPath, tablesXmlPath, topModule, allLanguages, out firstLanguage);
                if (!phaseBSuccess)
                {
                    Log.Error(LogTag.Localization, "Phase B（按语言数据导出）失败，导出中止。");
                    return false;
                }

                ExportPhaseC(settings, topModule, allLanguages, firstLanguage, supportedLanguagesExportPath);

                return true;
            }
            finally
            {
                CleanupTempDir(tempDir);
                AssetDatabase.Refresh();
            }
        }

        /// <summary>
        /// Phase A — C# 类型生成：PreFilter 取第一种语言生成简化 Excel，通过 Pipeline.ExportCode 生成 C# 类型。
        /// </summary>
        /// <param name="sourceDirPath">文本数据源目录路径。</param>
        /// <param name="settings">文本数据表设置适配器。</param>
        /// <param name="codegenTempDir">代码生成临时 Excel 输出目录。</param>
        /// <param name="classExportPath">C# 类型输出目录。</param>
        /// <param name="customTemplateDirs">自定义模板目录列表。</param>
        /// <param name="confPath">luban.conf 路径。</param>
        /// <param name="tablesXmlPath">__tables__.xml 路径。</param>
        /// <param name="topModule">顶层命名空间。</param>
        /// <returns>是否成功。</returns>
        private static bool ExportPhaseA(string sourceDirPath, IDataTableSettings settings, string codegenTempDir, string classExportPath, string[] customTemplateDirs, string confPath, string tablesXmlPath, string topModule)
        {
            if (string.IsNullOrEmpty(classExportPath))
            {
                Log.Debug(LogTag.Localization, "C# 类型导出路径为空，跳过 Phase A。");
                return true;
            }

            LocalizationExcelPreFilter.FilterForCodeGen(sourceDirPath, codegenTempDir);

            LanguageResolvedSettings codegenSettings = new LanguageResolvedSettings(settings, null, c_CodegenTempSubDir);

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

            return EditorUtil.Luban.Pipeline.ExportCode(ctx);
        }

        /// <summary>
        /// Phase B — 按语言数据导出：对每种语言执行 PreFilter → Pipeline.ExportData。
        /// DatasExportPath 中的 {0} 占位符替换为当前语言名称，确保每种语言输出到不同文件。
        /// </summary>
        /// <param name="sourceDirPath">文本数据源目录路径。</param>
        /// <param name="settings">文本数据表设置适配器。</param>
        /// <param name="tempDir">临时根目录。</param>
        /// <param name="confPath">luban.conf 路径。</param>
        /// <param name="tablesXmlPath">__tables__.xml 路径。</param>
        /// <param name="topModule">顶层命名空间。</param>
        /// <param name="allLanguages">所有语言名称集合。</param>
        /// <param name="firstLanguage">输出第一种语言名称（供 Phase C MapPropGen 使用）。</param>
        /// <returns>是否全部成功。</returns>
        private static bool ExportPhaseB(string sourceDirPath, IDataTableSettings settings, string tempDir, string confPath, string tablesXmlPath, string topModule, HashSet<string> allLanguages, out string firstLanguage)
        {
            firstLanguage = null;
            bool allSuccess = true;

            foreach (string languageName in allLanguages)
            {
                if (firstLanguage == null)
                {
                    firstLanguage = languageName;
                }

                string langTempDir = Util.SysIO.Path.Combine(tempDir, languageName);
                LocalizationExcelPreFilter.FilterForLanguage(sourceDirPath, langTempDir, languageName);

                LanguageResolvedSettings langSettings = new LanguageResolvedSettings(settings, languageName, languageName);

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
                    Log.Error(LogTag.Localization, "语言 '{0}' 数据导出失败。", languageName);
                    allSuccess = false;
                }
            }

            return allSuccess;
        }

        /// <summary>
        /// Phase C — MapPropGen 生成 Map 属性 + 导出语言列表。
        /// </summary>
        /// <param name="settings">文本数据表设置适配器。</param>
        /// <param name="topModule">顶层命名空间。</param>
        /// <param name="allLanguages">所有语言名称集合。</param>
        /// <param name="firstLanguage">Phase B 中第一种语言名称。</param>
        /// <param name="supportedLanguagesExportPath">语言列表 JSON 导出路径（可为 null）。</param>
        private static void ExportPhaseC(IDataTableSettings settings, string topModule, HashSet<string> allLanguages, string firstLanguage, string supportedLanguagesExportPath)
        {
            LanguageResolvedSettings resolvedSettings = new LanguageResolvedSettings(settings, firstLanguage, null);
            EditorUtil.Luban.MapPropGen.GenerateAll(resolvedSettings.Units, topModule);

            if (!string.IsNullOrEmpty(supportedLanguagesExportPath))
            {
                ExportSupportedLanguagesJson(allLanguages, supportedLanguagesExportPath);
            }
        }

        /// <summary>
        /// 将语言名称集合序列化为 JSON 数组并输出到文件。
        /// </summary>
        /// <param name="allLanguages">语言名称集合。</param>
        /// <param name="exportPath">输出文件路径（工程相对路径）。</param>
        private static void ExportSupportedLanguagesJson(HashSet<string> allLanguages, string exportPath)
        {
            if (allLanguages == null || allLanguages.Count == 0 || string.IsNullOrEmpty(exportPath))
            {
                return;
            }

            List<string> sortedLanguages = new List<string>(allLanguages);
            sortedLanguages.Sort(StringComparer.Ordinal);

            string fullOutputPath = EditorUtil.FileSystem.GetProjectFullPath(exportPath);
            string outputDir = Util.SysIO.Path.GetDirectoryName(fullOutputPath);
            if (!string.IsNullOrEmpty(outputDir) && !Util.SysIO.Directory.Exists(outputDir))
            {
                Util.SysIO.Directory.CreateIfNotExist(outputDir);
            }

            string json = JsonConvert.SerializeObject(sortedLanguages, Formatting.Indented);
            Util.SysIO.File.WriteAllTextSync(fullOutputPath, json, s_Utf8NoBom);
            Log.Debug(LogTag.Localization, "语言列表导出完成：{0} 种语言 -> {1}", sortedLanguages.Count, exportPath);
        }

        /// <summary>
        /// 清理临时目录。
        /// </summary>
        /// <param name="tempDir">临时目录路径。</param>
        private static void CleanupTempDir(string tempDir)
        {
            if (!string.IsNullOrEmpty(tempDir) && Util.SysIO.Directory.Exists(tempDir))
            {
                try
                {
                    Util.SysIO.Directory.Delete(tempDir, true);
                }
                catch (Exception e)
                {
                    Log.Warning(LogTag.Localization, "清理临时目录失败：{0}，异常：{1}", tempDir, e.Message);
                }
            }
        }

        /// <summary>
        /// 按语言解析后的数据表设置，将原始 Settings 中 DatasExportPath 的 {0} 替换为语言名称，
        /// 并将 LubanInputPath 覆盖为 _temp/{lubanInputSubDir}/{fileName}。
        /// </summary>
        private class LanguageResolvedSettings : IDataTableSettings
        {
#if UNITY_EDITOR
            /// <inheritdoc />
            public string SourceDirPath { get; }
#endif

            /// <inheritdoc />
            public IReadOnlyList<IDataTableUnitSetting> Units { get; }

            /// <summary>
            /// 构造按语言解析后的数据表设置。
            /// </summary>
            /// <param name="original">原始设置。</param>
            /// <param name="languageName">当前语言名称（可为 null，不替换 DatasExportPath 占位符）。</param>
            /// <param name="lubanInputSubDir">Luban 临时输入子目录名称（如 _codegen 或语言名），覆盖 LubanInputPath。</param>
            public LanguageResolvedSettings(IDataTableSettings original, string languageName, string lubanInputSubDir)
            {
#if UNITY_EDITOR
                SourceDirPath = original.SourceDirPath;
#endif
                var resolvedUnits = new List<IDataTableUnitSetting>();
                for (int i = 0; i < original.Units.Count; i++)
                {
                    resolvedUnits.Add(new LanguageResolvedUnitSetting(original.Units[i], languageName, lubanInputSubDir));
                }
                Units = resolvedUnits;
            }
        }

        /// <summary>
        /// 按语言解析后的单元设置，DatasExportPath 中的 {0} 已替换为语言名称，
        /// LubanInputPath 已覆盖为 _temp/{lubanInputSubDir}/{fileName}。
        /// </summary>
        private class LanguageResolvedUnitSetting : IDataTableUnitSetting
        {
            /// <summary>
            /// 原始单元设置。
            /// </summary>
            private readonly IDataTableUnitSetting m_Original;

            /// <summary>
            /// 当前语言名称。
            /// </summary>
            private readonly string m_LanguageName;

            /// <summary>
            /// Luban 临时输入子目录名称，用于覆盖 LubanInputPath。
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
            /// 构造按语言解析后的单元设置。
            /// </summary>
            /// <param name="original">原始单元设置。</param>
            /// <param name="languageName">当前语言名称（可为 null）。</param>
            /// <param name="lubanInputSubDir">Luban 临时输入子目录名称，覆盖 LubanInputPath。</param>
            public LanguageResolvedUnitSetting(IDataTableUnitSetting original, string languageName, string lubanInputSubDir)
            {
                m_Original = original;
                m_LanguageName = languageName;
                m_LubanInputSubDir = lubanInputSubDir;
            }
        }
    }
}
