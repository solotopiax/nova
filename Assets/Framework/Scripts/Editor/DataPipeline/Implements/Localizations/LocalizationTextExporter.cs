/***************************************************************
 * (c) copyright 2026 - 2030, Solotopia
 * All Rights Reserved.
 * -------------------------------------------------------------
 * filename:  LocalizationTextExporter.cs
 * author:    taoye
 * created:   2026/4/19
 * descrip:   编排 Localization 的预过滤、Luban 生成、校验与正式产物应用
 * input:     源目录、Settings、代码/语言列表路径与 Luban 模板目录
 * output:    各语言数据、C# 类型、Map 属性和支持语言列表正式文件
 * reason:    保证多阶段生成全部成功后，才把同一批产物应用到正式位置
 * boundary:  不解析 Excel 细节，不实现底层文件替换、备份或回滚算法
 * failure:   应用前失败不修改正式产物；应用中失败由 OutputApplier 回滚
 ***************************************************************/

using System;
using System.Collections.Generic;
using System.IO;
using Newtonsoft.Json.Linq;
using NovaFramework.Runtime;
using UnityEditor;
using IOPath = System.IO.Path;

namespace NovaFramework.Editor
{
    /// <summary>
    /// 本地化文本导出流程编排器：一次加载配置源，暂存并验证全部产物后统一应用。
    /// <para>全量顺序：全部语言数据 → C# 类型 → Map 属性 → 支持语言列表 → 应用正式产物。</para>
    /// Excel 解析由 <see cref="LocalizationExcelPreFilter"/> 完成；文件替换与回滚由
    /// <see cref="EditorUtil.FileSystem.OutputApplier"/> 完成。
    /// </summary>
    internal static class LocalizationTextExporter
    {
        /// <summary>
        /// 导出流程调用的外部能力集合，仅作为测试替换点；不承载导出状态或业务规则。
        /// </summary>
        internal sealed class ExportOperations
        {
            internal Func<string, IReadOnlyList<IDataTableUnitSetting>, LocalizationExcelPreFilter.SourceModel>
                LoadSourceModel =
                    (sourceDirPath, units) => LocalizationExcelPreFilter.SourceModel.Load(sourceDirPath, units);

            internal Func<EditorUtil.Luban.LubanExportContext, bool> ExportData =
                EditorUtil.Luban.Pipeline.ExportData;

            internal Func<EditorUtil.Luban.LubanExportContext, bool> ExportCode =
                EditorUtil.Luban.Pipeline.ExportCode;

            internal Func<EditorUtil.Luban.LubanSchemaManifest, string, Dictionary<string, int>>
                GenerateMapProperties = EditorUtil.Luban.MapPropGen.GenerateAll;

            internal Func<string> GetTopModule = EditorUtil.Config.RuntimeProvider.GetNamespace;

            internal Action RefreshAssetDatabase = AssetDatabase.Refresh;
        }

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
        internal static bool ExportAll(string sourceDirPath, IDataTableSettings settings, string classExportPath, string[] customTemplateDirs, string supportedLanguagesExportPath)
        {
            return ExportAll(
                sourceDirPath,
                settings,
                classExportPath,
                customTemplateDirs,
                supportedLanguagesExportPath,
                new ExportOperations());
        }

        internal static bool ExportAll(
            string sourceDirPath,
            IDataTableSettings settings,
            string classExportPath,
            string[] customTemplateDirs,
            string supportedLanguagesExportPath,
            ExportOperations operations)
        {
            if (!HasValidSettings(sourceDirPath, settings) || operations == null)
            {
                Log.Warning(LogTag.Localization, "文本导出参数无效，导出已跳过。");
                return false;
            }

            string tempDir = Util.SysIO.Path.Combine(sourceDirPath, c_TempDirName);
            try
            {
                using (EditorUtil.FileSystem.AcquireWorkspace(tempDir))
                {
                    LocalizationExcelPreFilter.SourceModel model = operations.LoadSourceModel(sourceDirPath, settings.Units);
                    CleanupTempDir(tempDir, true);
                    try
                    {
                        ResolveConfigPaths(sourceDirPath, out string confPath, out string tablesXmlPath);
                        string topModule = operations.GetTopModule();
                        using var outputApplier = new EditorUtil.FileSystem.OutputApplier(tempDir);
                        EditorUtil.Luban.LubanSchemaManifest dataManifest = null;

                        foreach (string language in model.Languages)
                        {
                            string inputRoot = IOPath.Combine(tempDir, language);
                            LocalizationExcelPreFilter.ProjectLanguage(model, inputRoot, language);
                            StagedSettings stagedSettings = CreateStagedDataSettings(
                                settings,
                                model,
                                language,
                                outputApplier.StagingRoot);
                            EditorUtil.Luban.LubanExportContext dataContext = CreateDataContext(
                                sourceDirPath,
                                stagedSettings,
                                confPath,
                                tablesXmlPath,
                                topModule);
                            if (!operations.ExportData(dataContext))
                            {
                                throw new InvalidOperationException(
                                    $"Localization data export failed for language '{language}'.");
                            }

                            ValidateAndRegisterStagedData(stagedSettings, outputApplier);
                            dataManifest = dataContext.SchemaManifest ??
                                throw new InvalidDataException(
                                    $"Localization data export produced no schema manifest for '{language}'.");
                        }

                        if (!string.IsNullOrEmpty(classExportPath))
                        {
                            string codegenInputRoot = IOPath.Combine(tempDir, c_CodegenTempSubDir);
                            LocalizationExcelPreFilter.ProjectCodeGen(model, codegenInputRoot);
                            // Unity ignores folders ending in '~', preventing staged .cs files from compiling
                            // before the outputApplier applies them to the formal code directory.
                            string stagedCodeRoot = IOPath.Combine(outputApplier.StagingRoot, "code~");
                            StagedSettings codeSettings = CreateStagedCodeSettings(
                                settings,
                                model,
                                stagedCodeRoot);
                            EditorUtil.Luban.LubanExportContext codeContext = CreateCodeContext(
                                sourceDirPath,
                                codeSettings,
                                confPath,
                                tablesXmlPath,
                                topModule,
                                stagedCodeRoot,
                                customTemplateDirs);
                            if (!operations.ExportCode(codeContext))
                            {
                                throw new InvalidOperationException("Localization code export failed.");
                            }

                            if (codeContext.SchemaManifest == null)
                            {
                                throw new InvalidDataException(
                                    "Localization code export produced no schema manifest.");
                            }

                            EditorUtil.Luban.LubanSchemaManifest mapManifest = CreateMapManifest(
                                dataManifest,
                                stagedCodeRoot);
                            operations.GenerateMapProperties(mapManifest, topModule);
                            ValidateMapOutputs(mapManifest);
                            RegisterStagedCode(codeContext, stagedCodeRoot, classExportPath, outputApplier);
                        }

                        if (!string.IsNullOrEmpty(supportedLanguagesExportPath))
                        {
                            StageSupportedLanguages(
                                model.Languages,
                                supportedLanguagesExportPath,
                                outputApplier);
                        }

                        RegisterObsoleteLanguageDeletes(model, outputApplier);
                        outputApplier.Apply();
                        return true;
                    }
                    finally
                    {
                        CleanupTempDir(tempDir, true);
                        operations.RefreshAssetDatabase();
                    }
                }
            }
            catch (Exception exception)
            {
                Log.Error(LogTag.Localization, "本地化文本全量导出失败：{0}", exception);
                return false;
            }
        }

        /// <summary>
        /// 仅导出 C# 类型。
        /// </summary>
        internal static bool ExportCode(string sourceDirPath, IDataTableSettings settings,
            string classExportPath, string[] customTemplateDirs)
        {
            return ExportCode(
                sourceDirPath,
                settings,
                classExportPath,
                customTemplateDirs,
                new ExportOperations());
        }

        internal static bool ExportCode(
            string sourceDirPath,
            IDataTableSettings settings,
            string classExportPath,
            string[] customTemplateDirs,
            ExportOperations operations)
        {
            if (!HasValidSettings(sourceDirPath, settings) ||
                string.IsNullOrEmpty(classExportPath) ||
                operations == null)
            {
                Log.Warning(LogTag.Localization, "文本代码导出参数无效，导出已跳过。");
                return false;
            }

            string tempDir = Util.SysIO.Path.Combine(sourceDirPath, c_TempDirName);
            try
            {
                using (EditorUtil.FileSystem.AcquireWorkspace(tempDir))
                {
                    LocalizationExcelPreFilter.SourceModel model = operations.LoadSourceModel(sourceDirPath, settings.Units);
                    CleanupTempDir(tempDir, true);
                    try
                    {
                        ResolveConfigPaths(sourceDirPath, out string confPath, out string tablesXmlPath);
                        string topModule = operations.GetTopModule();
                        using var outputApplier = new EditorUtil.FileSystem.OutputApplier(tempDir);
                        string codegenInputRoot = IOPath.Combine(tempDir, c_CodegenTempSubDir);
                        LocalizationExcelPreFilter.ProjectCodeGen(model, codegenInputRoot);
                        string stagedCodeRoot = IOPath.Combine(outputApplier.StagingRoot, "code~");
                        StagedSettings codeSettings = CreateStagedCodeSettings(
                            settings,
                            model,
                            stagedCodeRoot);
                        EditorUtil.Luban.LubanExportContext context = CreateCodeContext(
                            sourceDirPath,
                            codeSettings,
                            confPath,
                            tablesXmlPath,
                            topModule,
                            stagedCodeRoot,
                            customTemplateDirs);
                        if (!operations.ExportCode(context))
                        {
                            throw new InvalidOperationException("Localization code export failed.");
                        }

                        if (context.SchemaManifest == null)
                        {
                            throw new InvalidDataException(
                                "Localization code export produced no schema manifest.");
                        }

                        ValidateGeneratedCodeFiles(context.SchemaManifest, stagedCodeRoot);
                        EditorUtil.Luban.LubanSchemaManifest mapManifest =
                            CreateStandaloneCodeMapManifest(context.SchemaManifest, model, stagedCodeRoot);
                        if (mapManifest.Units.Count > 0)
                        {
                            operations.GenerateMapProperties(mapManifest, topModule);
                            ValidateMapOutputs(mapManifest);
                        }

                        RegisterStagedCode(context, stagedCodeRoot, classExportPath, outputApplier);
                        outputApplier.Apply();
                        return true;
                    }
                    finally
                    {
                        CleanupTempDir(tempDir, true);
                        operations.RefreshAssetDatabase();
                    }
                }
            }
            catch (Exception exception)
            {
                Log.Error(LogTag.Localization, "本地化文本代码导出失败：{0}", exception);
                return false;
            }
        }

        /// <summary>
        /// 仅导出所有语言的文本数据。
        /// </summary>
        internal static bool ExportData(string sourceDirPath, IDataTableSettings settings)
        {
            return ExportData(sourceDirPath, settings, new ExportOperations());
        }

        internal static bool ExportData(
            string sourceDirPath,
            IDataTableSettings settings,
            ExportOperations operations)
        {
            if (!HasValidSettings(sourceDirPath, settings) || operations == null)
            {
                Log.Warning(LogTag.Localization, "文本数据导出参数无效，导出已跳过。");
                return false;
            }

            string tempDir = Util.SysIO.Path.Combine(sourceDirPath, c_TempDirName);
            try
            {
                using (EditorUtil.FileSystem.AcquireWorkspace(tempDir))
                {
                    LocalizationExcelPreFilter.SourceModel model = operations.LoadSourceModel(sourceDirPath, settings.Units);
                    CleanupTempDir(tempDir, true);
                    try
                    {
                        ResolveConfigPaths(sourceDirPath, out string confPath, out string tablesXmlPath);
                        string topModule = operations.GetTopModule();
                        using var outputApplier = new EditorUtil.FileSystem.OutputApplier(tempDir);
                        foreach (string language in model.Languages)
                        {
                            string inputRoot = IOPath.Combine(tempDir, language);
                            LocalizationExcelPreFilter.ProjectLanguage(model, inputRoot, language);
                            StagedSettings stagedSettings = CreateStagedDataSettings(
                                settings,
                                model,
                                language,
                                outputApplier.StagingRoot);
                            EditorUtil.Luban.LubanExportContext context = CreateDataContext(
                                sourceDirPath,
                                stagedSettings,
                                confPath,
                                tablesXmlPath,
                                topModule);
                            if (!operations.ExportData(context))
                            {
                                throw new InvalidOperationException(
                                    $"Localization data export failed for language '{language}'.");
                            }

                            ValidateAndRegisterStagedData(stagedSettings, outputApplier);
                        }

                        RegisterObsoleteLanguageDeletes(model, outputApplier);
                        outputApplier.Apply();
                        return true;
                    }
                    finally
                    {
                        CleanupTempDir(tempDir, true);
                        operations.RefreshAssetDatabase();
                    }
                }
            }
            catch (Exception exception)
            {
                Log.Error(LogTag.Localization, "本地化文本数据导出失败：{0}", exception);
                return false;
            }
        }

        /// <summary>
        /// 独立导出支持语言列表。
        /// </summary>
        internal static bool ExportSupportedLanguages(string sourceDirPath, string exportPath)
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

            return ExportSupportedLanguages(
                sourceDirPath,
                exportPath,
                OrderLanguages(allLanguages),
                new ExportOperations());
        }

        internal static bool ExportSupportedLanguages(
            string sourceDirPath,
            string exportPath,
            IReadOnlyList<string> languages,
            ExportOperations operations)
        {
            if (string.IsNullOrEmpty(sourceDirPath) ||
                string.IsNullOrEmpty(exportPath) ||
                languages == null ||
                languages.Count == 0 ||
                operations == null)
            {
                Log.Warning(LogTag.Localization, "语言列表导出参数无效，导出已跳过。");
                return false;
            }

            string tempDir = IOPath.Combine(sourceDirPath, c_TempDirName);
            try
            {
                using (EditorUtil.FileSystem.AcquireWorkspace(tempDir))
                {
                    CleanupTempDir(tempDir, true);
                    try
                    {
                        using var outputApplier = new EditorUtil.FileSystem.OutputApplier(tempDir);
                        var ordered = new List<string>(languages);
                        ordered.Sort(StringComparer.Ordinal);
                        StageSupportedLanguages(ordered, exportPath, outputApplier);
                        outputApplier.Apply();
                        return true;
                    }
                    finally
                    {
                        CleanupTempDir(tempDir, true);
                        operations.RefreshAssetDatabase();
                    }
                }
            }
            catch (Exception exception)
            {
                Log.Error(LogTag.Localization, "本地化支持语言列表导出失败：{0}", exception);
                return false;
            }
        }

        private static StagedSettings CreateStagedDataSettings(
            IDataTableSettings original,
            LocalizationExcelPreFilter.SourceModel model,
            string language,
            string stagingRoot)
        {
            var units = new List<StagedUnitSetting>(model.Units.Count);
            foreach (LocalizationExcelPreFilter.SourceUnit sourceUnit in model.Units)
            {
                string stagedPath = IOPath.Combine(
                    stagingRoot,
                    "data",
                    language,
                    sourceUnit.RelativeStem + ".json");
                string finalPath = sourceUnit.Setting.DatasExportPath.Replace("{0}", language);
                string lubanInputPath = IOPath.Combine(
                        c_TempDirName,
                        language,
                        sourceUnit.RelativeStem)
                    .Replace('\\', '/');
                units.Add(new StagedUnitSetting(
                    sourceUnit.Setting,
                    sourceUnit.SourcePath,
                    lubanInputPath,
                    stagedPath,
                    sourceUnit.Setting.ClassesExportPath,
                    finalPath));
            }

            return new StagedSettings(original.SourceDirPath, units);
        }

        private static StagedSettings CreateStagedCodeSettings(
            IDataTableSettings original,
            LocalizationExcelPreFilter.SourceModel model,
            string stagedCodeRoot)
        {
            var units = new List<StagedUnitSetting>(model.Units.Count);
            foreach (LocalizationExcelPreFilter.SourceUnit sourceUnit in model.Units)
            {
                string lubanInputPath = IOPath.Combine(
                        c_TempDirName,
                        c_CodegenTempSubDir,
                        sourceUnit.RelativeStem)
                    .Replace('\\', '/');
                units.Add(new StagedUnitSetting(
                    sourceUnit.Setting,
                    sourceUnit.SourcePath,
                    lubanInputPath,
                    string.Empty,
                    stagedCodeRoot,
                    null));
            }

            return new StagedSettings(original.SourceDirPath, units);
        }

        private static EditorUtil.Luban.LubanExportContext CreateDataContext(
            string sourceDirPath,
            IDataTableSettings settings,
            string confPath,
            string tablesXmlPath,
            string topModule)
        {
            return new EditorUtil.Luban.LubanExportContext
            {
                SourceDirPath = sourceDirPath,
                ConfPath = confPath,
                TargetName = EditorUtil.Luban.LubanExportProfiles.LocalizationText.TargetName,
                ManagerName = EditorUtil.Luban.LubanExportProfiles.LocalizationText.ManagerName,
                TopModule = topModule,
                TablesXmlPath = tablesXmlPath,
                Settings = settings,
            };
        }

        private static EditorUtil.Luban.LubanExportContext CreateCodeContext(
            string sourceDirPath,
            IDataTableSettings settings,
            string confPath,
            string tablesXmlPath,
            string topModule,
            string stagedCodeRoot,
            string[] customTemplateDirs)
        {
            return new EditorUtil.Luban.LubanExportContext
            {
                SourceDirPath = sourceDirPath,
                ConfPath = confPath,
                TargetName = EditorUtil.Luban.LubanExportProfiles.LocalizationText.TargetName,
                ManagerName = EditorUtil.Luban.LubanExportProfiles.LocalizationText.ManagerName,
                TopModule = topModule,
                OutputCodeDir = stagedCodeRoot,
                CustomTemplateDirs = customTemplateDirs,
                TablesXmlPath = tablesXmlPath,
                Settings = settings,
            };
        }

        private static void ValidateAndRegisterStagedData(
            StagedSettings stagedSettings,
            EditorUtil.FileSystem.OutputApplier outputApplier)
        {
            foreach (StagedUnitSetting unit in stagedSettings.StagedUnits)
            {
                if (!File.Exists(unit.DatasExportPath))
                {
                    throw new InvalidDataException(
                        $"Localization staged data file was not produced: {unit.DatasExportPath}");
                }

                try
                {
                    JObject.Parse(File.ReadAllText(unit.DatasExportPath, s_Utf8NoBom));
                }
                catch (Exception exception) when (
                    exception is Newtonsoft.Json.JsonException || exception is IOException)
                {
                    throw new InvalidDataException(
                        $"Localization staged data file is invalid: {unit.DatasExportPath}",
                        exception);
                }

                outputApplier.AddReplacement(unit.DatasExportPath, unit.FinalDatasExportPath);
            }
        }

        private static void RegisterObsoleteLanguageDeletes(
            LocalizationExcelPreFilter.SourceModel model,
            EditorUtil.FileSystem.OutputApplier outputApplier)
        {
            var currentLanguages = new HashSet<string>(model.Languages, StringComparer.Ordinal);
            var candidates = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            foreach (string language in Enum.GetNames(typeof(Language)))
            {
                if (currentLanguages.Contains(language))
                {
                    continue;
                }

                foreach (LocalizationExcelPreFilter.SourceUnit unit in model.Units)
                {
                    string candidate = IOPath.GetFullPath(
                        unit.Setting.DatasExportPath.Replace("{0}", language));
                    if (candidates.Add(candidate) && File.Exists(candidate))
                    {
                        outputApplier.AddDeletion(candidate);
                    }
                }
            }
        }

        private static EditorUtil.Luban.LubanSchemaManifest CreateMapManifest(
            EditorUtil.Luban.LubanSchemaManifest dataManifest,
            string stagedCodeRoot)
        {
            if (dataManifest == null)
            {
                throw new InvalidDataException("Localization map generation requires a data manifest.");
            }

            var result = new EditorUtil.Luban.LubanSchemaManifest
            {
                SchemaVersion = dataManifest.SchemaVersion,
                ProfileId = dataManifest.ProfileId,
            };
            foreach (EditorUtil.Luban.LubanSchemaUnit sourceUnit in dataManifest.Units)
            {
                var targetUnit = new EditorUtil.Luban.LubanSchemaUnit
                {
                    SourcePath = sourceUnit.SourcePath,
                    LubanInputPath = sourceUnit.LubanInputPath,
                    DatasExportPath = sourceUnit.DatasExportPath,
                    ClassesExportPath = stagedCodeRoot,
                    Mode = sourceUnit.Mode,
                    IndexField = sourceUnit.IndexField,
                };
                foreach (EditorUtil.Luban.LubanSchemaTable sourceTable in sourceUnit.Tables)
                {
                    targetUnit.Tables.Add(new EditorUtil.Luban.LubanSchemaTable
                    {
                        Name = sourceTable.Name,
                        ValueType = sourceTable.ValueType,
                    });
                }

                result.Units.Add(targetUnit);
            }

            EditorUtil.Luban.LubanSchemaManifestValidator.ValidateAndNormalize(result);
            return result;
        }

        private static EditorUtil.Luban.LubanSchemaManifest CreateStandaloneCodeMapManifest(
            EditorUtil.Luban.LubanSchemaManifest codeManifest,
            LocalizationExcelPreFilter.SourceModel model,
            string stagedCodeRoot)
        {
            var result = new EditorUtil.Luban.LubanSchemaManifest
            {
                SchemaVersion = codeManifest.SchemaVersion,
                ProfileId = codeManifest.ProfileId,
            };
            string language = model.Languages[0];
            foreach (EditorUtil.Luban.LubanSchemaUnit schemaUnit in codeManifest.Units)
            {
                LocalizationExcelPreFilter.SourceUnit sourceUnit = null;
                foreach (LocalizationExcelPreFilter.SourceUnit candidate in model.Units)
                {
                    if (string.Equals(
                            candidate.SourcePath,
                            schemaUnit.SourcePath,
                            StringComparison.OrdinalIgnoreCase))
                    {
                        sourceUnit = candidate;
                        break;
                    }
                }

                if (sourceUnit == null)
                {
                    throw new InvalidDataException(
                        $"Localization source model does not contain '{schemaUnit.SourcePath}'.");
                }

                string dataPath = IOPath.GetFullPath(
                    sourceUnit.Setting.DatasExportPath.Replace("{0}", language));
                if (!File.Exists(dataPath))
                {
                    continue;
                }

                var targetUnit = new EditorUtil.Luban.LubanSchemaUnit
                {
                    SourcePath = schemaUnit.SourcePath,
                    LubanInputPath = schemaUnit.LubanInputPath,
                    DatasExportPath = dataPath,
                    ClassesExportPath = stagedCodeRoot,
                    Mode = schemaUnit.Mode,
                    IndexField = schemaUnit.IndexField,
                };
                foreach (EditorUtil.Luban.LubanSchemaTable table in schemaUnit.Tables)
                {
                    targetUnit.Tables.Add(new EditorUtil.Luban.LubanSchemaTable
                    {
                        Name = table.Name,
                        ValueType = table.ValueType,
                    });
                }

                result.Units.Add(targetUnit);
            }

            if (result.Units.Count > 0)
            {
                EditorUtil.Luban.LubanSchemaManifestValidator.ValidateAndNormalize(result);
            }

            return result;
        }

        private static void ValidateGeneratedCodeFiles(
            EditorUtil.Luban.LubanSchemaManifest manifest,
            string stagedCodeRoot)
        {
            foreach (EditorUtil.Luban.LubanSchemaUnit unit in manifest.Units)
            {
                foreach (EditorUtil.Luban.LubanSchemaTable table in unit.Tables)
                {
                    string codePath = IOPath.Combine(stagedCodeRoot, table.Name + ".cs");
                    if (!File.Exists(codePath))
                    {
                        throw new InvalidDataException(
                            $"Localization generated code file does not exist: {codePath}");
                    }
                }
            }
        }

        private static void ValidateMapOutputs(EditorUtil.Luban.LubanSchemaManifest manifest)
        {
            const string regionBegin = "// --- AUTO-GENERATED MAP PROPERTIES BEGIN ---";
            const string regionEnd = "// --- AUTO-GENERATED MAP PROPERTIES END ---";

            foreach (EditorUtil.Luban.LubanSchemaUnit unit in manifest.Units)
            {
                JObject root = null;
                if (unit.Mode == "map")
                {
                    if (!File.Exists(unit.DatasExportPath))
                    {
                        throw new InvalidDataException(
                            $"Localization map data file does not exist: {unit.DatasExportPath}");
                    }

                    root = JObject.Parse(File.ReadAllText(unit.DatasExportPath, s_Utf8NoBom));
                }

                foreach (EditorUtil.Luban.LubanSchemaTable table in unit.Tables)
                {
                    string codePath = IOPath.Combine(unit.ClassesExportPath, table.Name + ".cs");
                    if (!File.Exists(codePath))
                    {
                        throw new InvalidDataException(
                            $"Localization generated code file does not exist: {codePath}");
                    }

                    if (unit.Mode != "map")
                    {
                        continue;
                    }

                    string indexField = string.IsNullOrEmpty(unit.IndexField) ? "ID" : unit.IndexField;
                    var expectedKeys = new HashSet<string>(StringComparer.Ordinal);
                    if (root[table.ValueType] is JArray rows)
                    {
                        foreach (JToken token in rows)
                        {
                            string key = token[indexField]?.ToString();
                            if (IsValidIdentifier(key))
                            {
                                expectedKeys.Add(key);
                            }
                        }
                    }

                    string content = File.ReadAllText(codePath, s_Utf8NoBom);
                    if (expectedKeys.Count == 0)
                    {
                        continue;
                    }

                    int beginIndex = content.IndexOf(regionBegin, StringComparison.Ordinal);
                    int endIndex = content.IndexOf(regionEnd, StringComparison.Ordinal);
                    if (beginIndex < 0 || endIndex <= beginIndex)
                    {
                        throw new InvalidDataException(
                            $"Localization generated map region is missing: {codePath}");
                    }

                    string region = content.Substring(beginIndex, endIndex - beginIndex);
                    foreach (string key in expectedKeys)
                    {
                        string declaration =
                            $"public {table.ValueType} {key} => GetOrDefault(\"{key}\");";
                        if (region.IndexOf(declaration, StringComparison.Ordinal) < 0)
                        {
                            throw new InvalidDataException(
                                $"Localization generated map property '{key}' is missing: {codePath}");
                        }
                    }

                    if (CountOccurrences(region, "=> GetOrDefault(") != expectedKeys.Count)
                    {
                        throw new InvalidDataException(
                            $"Localization generated map property count is invalid: {codePath}");
                    }
                }
            }
        }

        private static void RegisterStagedCode(
            EditorUtil.Luban.LubanExportContext context,
            string stagedCodeRoot,
            string classExportPath,
            EditorUtil.FileSystem.OutputApplier outputApplier)
        {
            EditorUtil.Luban.GeneratedOutput.RegisterCodeOutputs(
                outputApplier,
                context,
                stagedCodeRoot,
                classExportPath,
                true);
        }

        private static void StageSupportedLanguages(
            IReadOnlyList<string> languages,
            string exportPath,
            EditorUtil.FileSystem.OutputApplier outputApplier)
        {
            string stagedPath = IOPath.Combine(
                outputApplier.StagingRoot,
                "metadata",
                "supported-languages.json");
            Directory.CreateDirectory(IOPath.GetDirectoryName(stagedPath));
            File.WriteAllText(stagedPath, Util.Json.Serialize(languages), s_Utf8NoBom);
            outputApplier.AddReplacement(stagedPath, EditorUtil.FileSystem.GetProjectFullPath(exportPath));
        }

        private static bool IsValidIdentifier(string value)
        {
            if (string.IsNullOrEmpty(value) || (!char.IsLetter(value[0]) && value[0] != '_'))
            {
                return false;
            }

            for (int i = 1; i < value.Length; i++)
            {
                if (!char.IsLetterOrDigit(value[i]) && value[i] != '_')
                {
                    return false;
                }
            }

            return true;
        }

        private static int CountOccurrences(string value, string search)
        {
            int count = 0;
            int offset = 0;
            while ((offset = value.IndexOf(search, offset, StringComparison.Ordinal)) >= 0)
            {
                count++;
                offset += search.Length;
            }

            return count;
        }

        internal static IReadOnlyList<string> OrderLanguages(IEnumerable<string> languages)
        {
            var orderedLanguages = new List<string>(languages);
            orderedLanguages.Sort(StringComparer.Ordinal);
            return orderedLanguages;
        }

        private static bool HasValidSettings(string sourceDirPath, IDataTableSettings settings)
        {
            return !string.IsNullOrEmpty(sourceDirPath) &&
                   settings != null &&
                   settings.Units != null &&
                   settings.Units.Count > 0;
        }

        private static void ResolveConfigPaths(string sourceDirPath, out string confPath,
            out string tablesXmlPath)
        {
            string configDir = EditorUtil.Luban.ConfigSyncer.GetConfigDirPath(sourceDirPath);
            confPath = Util.SysIO.Path.Combine(configDir, EditorUtil.Luban.ConfigSyncer.c_LubanConfFileName);
            tablesXmlPath = Util.SysIO.Path.Combine(configDir, EditorUtil.Luban.ConfigSyncer.c_TablesXmlFileName);
        }

        /// <summary>
        /// 清理临时目录。
        /// </summary>
        /// <param name="tempDir">临时目录路径。</param>
        private static void CleanupTempDir(string tempDir)
        {
            CleanupTempDir(tempDir, false);
        }

        private static void CleanupTempDir(string tempDir, bool throwOnFailure)
        {
            if (!string.IsNullOrEmpty(tempDir) && Util.SysIO.Directory.Exists(tempDir))
            {
                try
                {
                    EditorUtil.FileSystem.DeleteUnityTempRoot(tempDir);
                }
                catch (Exception e)
                {
                    if (throwOnFailure)
                    {
                        throw new IOException($"Failed to clean Localization temp directory: {tempDir}", e);
                    }

                    Log.Warning(LogTag.Localization, "清理临时目录失败：{0}，异常：{1}", tempDir, e.Message);
                }
            }
        }

        private sealed class StagedSettings : IDataTableSettings
        {
            internal StagedSettings(string sourceDirPath, IReadOnlyList<StagedUnitSetting> units)
            {
                SourceDirPath = sourceDirPath;
                StagedUnits = units;
                Units = units;
            }

            public string SourceDirPath { get; }
            public IReadOnlyList<IDataTableUnitSetting> Units { get; }
            internal IReadOnlyList<StagedUnitSetting> StagedUnits { get; }
        }

        private sealed class StagedUnitSetting : IDataTableUnitSetting
        {
            private readonly IDataTableUnitSetting m_Original;

            internal StagedUnitSetting(
                IDataTableUnitSetting original,
                string sourcePath,
                string lubanInputPath,
                string datasExportPath,
                string classesExportPath,
                string finalDatasExportPath)
            {
                m_Original = original;
                SourcePath = sourcePath;
                LubanInputPath = lubanInputPath;
                DatasExportPath = datasExportPath;
                ClassesExportPath = classesExportPath;
                FinalDatasExportPath = finalDatasExportPath;
            }

            public string SourcePath { get; }
            public string DatasExportPath { get; }
            public string ClassesExportPath { get; }
            public string LubanInputPath { get; }
            public string AssetLocation => m_Original.AssetLocation;
            public DataTableMode Mode => m_Original.Mode;
            public string IndexField => m_Original.IndexField;
            internal string FinalDatasExportPath { get; }
        }

    }
}
