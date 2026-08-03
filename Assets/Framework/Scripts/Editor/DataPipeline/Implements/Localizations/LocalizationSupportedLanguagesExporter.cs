/***************************************************************
 * (c) copyright 2026 - 2030, Solotopia
 * All Rights Reserved.
 * -------------------------------------------------------------
 * filename:  LocalizationSupportedLanguagesExporter.cs
 * author:    taoye
 * created:   2026/7/29
 * descrip:   支持语言列表 Luban 临时投影与导出
 ***************************************************************/

using System;
using System.Collections.Generic;
using System.IO;
using System.Xml.Linq;
using Newtonsoft.Json.Linq;
using NovaFramework.Runtime;
using IOPath = System.IO.Path;

namespace NovaFramework.Editor
{
    internal static partial class LocalizationTextExporter
    {
        private const string c_SupportedLanguagesTarget = "localization-supported-languages";
        private const string c_SupportedLanguagesManager = "LocalizationSupportedLanguagesTables";
        private const string c_SupportedLanguagesTable = "TbLocalizationSupportedLanguages";
        private const string c_SupportedLanguagesValueType = "LocalizationSupportedLanguage";

        private static void ExportSupportedLanguagesThroughLuban(
            string sourceDirPath,
            string tempRoot,
            IReadOnlyList<string> languages,
            string formalDataPath,
            string formalCodeDir,
            string topModule,
            LubanDataFormat dataFormat,
            EditorUtil.FileSystem.OutputApplier outputApplier)
        {
            string workspace = IOPath.Combine(tempRoot, "_supported_languages");
            string projectionDir = IOPath.Combine(workspace, "input");
            string configDir = IOPath.Combine(workspace, "config");
            string nativeDataDir = IOPath.Combine(workspace, "native-data");
            string stagedDataPath = IOPath.Combine(
                outputApplier.StagingRoot,
                "metadata",
                dataFormat == LubanDataFormat.Binary ? "supported-languages.bytes" : "supported-languages.json");
            string stagedCodeDir = string.IsNullOrWhiteSpace(formalCodeDir)
                ? null
                : IOPath.Combine(outputApplier.StagingRoot, "supported-code~");

            Directory.CreateDirectory(projectionDir);
            Directory.CreateDirectory(configDir);
            Directory.CreateDirectory(nativeDataDir);
            Directory.CreateDirectory(IOPath.GetDirectoryName(stagedDataPath));
            if (stagedCodeDir != null)
            {
                Directory.CreateDirectory(stagedCodeDir);
            }

            WriteSupportedLanguagesProjection(projectionDir, languages);
            string confPath = WriteSupportedLanguagesConfig(configDir, topModule);
            string tablesXmlPath = WriteSupportedLanguagesSchema(configDir);
            EditorUtil.Luban.LubanSchemaManifest manifest = CreateSupportedLanguagesManifest(
                stagedDataPath,
                stagedCodeDir);

            string[] customTemplateDirs = EditorUtil.Luban.ExportHelper.GetLubanCustomTemplateDirs(
                c_SupportedLanguagesTarget);
            bool exported = stagedCodeDir == null
                ? EditorUtil.Luban.CliRunner.RunDataExport(
                    confPath,
                    c_SupportedLanguagesTarget,
                    nativeDataDir,
                    dataFormat)
                : EditorUtil.Luban.CliRunner.RunAll(
                    confPath,
                    c_SupportedLanguagesTarget,
                    stagedCodeDir,
                    nativeDataDir,
                    customTemplateDirs,
                    dataFormat);
            if (!exported)
            {
                throw new InvalidOperationException("Localization supported languages Luban export failed.");
            }

            bool packaged = dataFormat == LubanDataFormat.Binary
                ? EditorUtil.Luban.BinaryPackager.PackageAll(nativeDataDir, manifest)
                : EditorUtil.Luban.JsonMerger.MergeAll(nativeDataDir, tablesXmlPath, manifest);
            if (!packaged || !File.Exists(stagedDataPath))
            {
                throw new InvalidDataException("Localization supported languages data was not produced.");
            }
            if (!string.IsNullOrWhiteSpace(formalDataPath))
            {
                outputApplier.AddReplacement(
                    stagedDataPath,
                    EditorUtil.FileSystem.GetProjectFullPath(formalDataPath));
            }

            if (stagedCodeDir == null)
            {
                return;
            }

            var context = new EditorUtil.Luban.LubanExportContext
            {
                SourceDirPath = sourceDirPath,
                TargetName = c_SupportedLanguagesTarget,
                ManagerName = c_SupportedLanguagesManager,
                TopModule = topModule,
                OutputCodeDir = stagedCodeDir,
                DataFormat = dataFormat,
                SchemaManifest = manifest,
            };
            EditorUtil.Luban.GeneratedOutput.RegisterCodeOutputs(
                outputApplier,
                context,
                stagedCodeDir,
                formalCodeDir,
                false);
        }

        private static void WriteSupportedLanguagesProjection(
            string projectionDir,
            IReadOnlyList<string> languages)
        {
            var rows = new List<IReadOnlyList<string>>
            {
                new List<string> { "##comment", "Localization supported languages" },
                new List<string> { "##var", "Name" },
                new List<string> { "##type", "string" },
                new List<string> { "##comment", "Language enum name" },
            };
            for (int i = 0; i < languages.Count; i++)
            {
                rows.Add(new List<string> { string.Empty, languages[i] });
            }
            EditorUtil.Excel.Write(projectionDir, "LocalizationSupportedLanguages", rows);
        }

        private static string WriteSupportedLanguagesConfig(string configDir, string topModule)
        {
            var root = new JObject
            {
                ["dataDir"] = "..",
                ["groups"] = new JArray
                {
                    new JObject
                    {
                        ["names"] = new JArray("c"),
                        ["default"] = true,
                    },
                },
                ["schemaFiles"] = new JArray
                {
                    new JObject
                    {
                        ["fileName"] = "__tables__.xml",
                        ["type"] = string.Empty,
                    },
                },
                ["targets"] = new JArray
                {
                    new JObject
                    {
                        ["name"] = c_SupportedLanguagesTarget,
                        ["manager"] = c_SupportedLanguagesManager,
                        ["groups"] = new JArray("c"),
                        ["topModule"] = topModule,
                    },
                },
            };
            string path = IOPath.Combine(configDir, "luban.conf");
            File.WriteAllText(path, root.ToString(), s_Utf8NoBom);
            return path;
        }

        private static string WriteSupportedLanguagesSchema(string configDir)
        {
            var document = new XDocument(
                new XDeclaration("1.0", "utf-8", null),
                new XElement("module",
                    new XElement("table",
                        new XAttribute("name", c_SupportedLanguagesTable),
                        new XAttribute("value", c_SupportedLanguagesValueType),
                        new XAttribute("input", "input/LocalizationSupportedLanguages.csv"),
                        new XAttribute("mode", "list"),
                        new XAttribute("readSchemaFromFile", "true"),
                        new XAttribute("comment", "Localization supported languages"))));
            string path = IOPath.Combine(configDir, "__tables__.xml");
            document.Save(path);
            return path;
        }

        private static EditorUtil.Luban.LubanSchemaManifest CreateSupportedLanguagesManifest(
            string dataPath,
            string codeDir)
        {
            return new EditorUtil.Luban.LubanSchemaManifest
            {
                ProfileId = c_SupportedLanguagesTarget,
                Units = new List<EditorUtil.Luban.LubanSchemaUnit>
                {
                    new EditorUtil.Luban.LubanSchemaUnit
                    {
                        SourcePath = "LocalizationSupportedLanguages.csv",
                        LubanInputPath = "input/LocalizationSupportedLanguages.csv",
                        DatasExportPath = dataPath,
                        ClassesExportPath = codeDir,
                        Mode = "list",
                        IndexField = string.Empty,
                        Tables = new List<EditorUtil.Luban.LubanSchemaTable>
                        {
                            new EditorUtil.Luban.LubanSchemaTable
                            {
                                Name = c_SupportedLanguagesTable,
                                ValueType = c_SupportedLanguagesValueType,
                            },
                        },
                    },
                },
            };
        }
    }
}
