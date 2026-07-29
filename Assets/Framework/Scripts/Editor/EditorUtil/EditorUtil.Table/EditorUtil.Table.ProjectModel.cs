/***************************************************************
 * (c) copyright 2026 - 2030, Solotopia
 * All Rights Reserved.
 * -------------------------------------------------------------
 * filename:  EditorUtil.Table.ProjectModel.cs
 * author:    taoye
 * created:   2026/7/27
 * descrip:   Luban Project 配置、Excel、Sheet 与表定义只读模型
 ***************************************************************/

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Xml.Linq;
using NovaFramework.Runtime;
using UnityEditor;
using YooAsset.Editor;
using IOPath = System.IO.Path;

namespace NovaFramework.Editor
{
    /// <summary>
    /// Luban Project 的 Inspector 只读模型。
    /// </summary>
    internal sealed class TableProjectModel
    {
        internal string ConfigPath = string.Empty;
        internal string DataDirectory = string.Empty;
        internal List<string> SchemaFiles = new List<string>();
        internal List<string> Targets = new List<string>();
        internal Dictionary<string, string> BindingTypeByTarget = new Dictionary<string, string>(StringComparer.Ordinal);
        internal List<TableProjectExcelFile> ExcelFiles = new List<TableProjectExcelFile>();
        internal List<string> Errors = new List<string>();
    }

    /// <summary>
    /// Project 内单个 Excel 文件及其 Sheet 清单。
    /// </summary>
    internal sealed class TableProjectExcelFile
    {
        internal string RelativePath = string.Empty;
        internal string AbsolutePath = string.Empty;
        internal List<TableProjectExcelSheet> Sheets = new List<TableProjectExcelSheet>();
    }

    /// <summary>
    /// Excel Sheet 与使用该输入的 Luban 表定义。
    /// </summary>
    internal sealed class TableProjectExcelSheet
    {
        internal string Name = string.Empty;
        internal List<string> TableNames = new List<string>();
    }

    /// <summary>
    /// 收集 Luban 数据目录及其现有子目录，供 Inspector 注册 Excel 文件变化监听。
    /// </summary>
    internal static class TableProjectWatchDirectoryResolver
    {
        /// <summary>
        /// 返回需要监听的全部现有目录；数据目录无效时返回空集合。
        /// </summary>
        /// <param name="dataDirectory">Luban Project 的数据根目录。</param>
        /// <returns>使用正斜杠且不重复的目录清单。</returns>
        internal static List<string> Collect(string dataDirectory)
        {
            if (string.IsNullOrWhiteSpace(dataDirectory) || !Directory.Exists(dataDirectory))
            {
                return new List<string>();
            }

            return new[] { IOPath.GetFullPath(dataDirectory) }
                .Concat(Directory.GetDirectories(dataDirectory, "*", SearchOption.AllDirectories))
                .Select(path => path.Replace('\\', '/').TrimEnd('/'))
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .OrderBy(path => path, StringComparer.OrdinalIgnoreCase)
                .ToList();
        }
    }

    /// <summary>
    /// Luban 工程配置文件在 Inspector 中的显示状态。
    /// </summary>
    internal enum TableProjectConfigState
    {
        Valid,
        Missing,
        Duplicate,
    }

    /// <summary>
    /// Luban 工程配置文件状态与用户提示。
    /// </summary>
    internal readonly struct TableProjectConfigStatus
    {
        internal TableProjectConfigStatus(TableProjectConfigState state, string message)
        {
            State = state;
            Message = message;
        }

        internal TableProjectConfigState State { get; }
        internal string Message { get; }
    }

    /// <summary>
    /// 统一判断 Luban 配置文件是否有效、缺失或与其他工程重复。
    /// </summary>
    internal static class TableProjectConfigStatusResolver
    {
        /// <summary>
        /// 判断单个配置路径的状态；缺失优先于重复。
        /// </summary>
        /// <param name="configPath">当前配置路径。</param>
        /// <param name="allConfigPaths">Inspector 中的全部配置路径。</param>
        /// <returns>状态和提示文字。</returns>
        internal static TableProjectConfigStatus Resolve(string configPath, IEnumerable<string> allConfigPaths)
        {
            if (!TryGetAbsolutePath(configPath, out string absolutePath) || !File.Exists(absolutePath))
            {
                return new TableProjectConfigStatus(
                    TableProjectConfigState.Missing,
                    "Luban 配置文件不存在。");
            }

            int duplicateCount = (allConfigPaths ?? Array.Empty<string>())
                .Select(path => TryGetAbsolutePath(path, out string value) ? value : string.Empty)
                .Count(path => string.Equals(path, absolutePath, StringComparison.OrdinalIgnoreCase));
            if (duplicateCount > 1)
            {
                return new TableProjectConfigStatus(
                    TableProjectConfigState.Duplicate,
                    "Luban 配置文件与其他工程重复。");
            }

            return new TableProjectConfigStatus(
                TableProjectConfigState.Valid,
                "Luban 配置文件有效。");
        }

        /// <summary>
        /// 把项目相对路径和绝对路径统一成可比较的完整路径。
        /// </summary>
        private static bool TryGetAbsolutePath(string path, out string absolutePath)
        {
            absolutePath = string.Empty;
            if (string.IsNullOrWhiteSpace(path))
            {
                return false;
            }

            try
            {
                absolutePath = IOPath.GetFullPath(path).Replace('\\', '/');
                return true;
            }
            catch
            {
                return false;
            }
        }
    }

    /// <summary>
    /// 从原生 luban.conf、schema 与 Excel 构建 Project 只读视图。
    /// </summary>
    internal static class TableProjectModelBuilder
    {
        /// <summary>
        /// 解析正式 Luban Project，并读取 Excel 的真实 Sheet 名称。
        /// </summary>
        /// <param name="configPath">项目根相对或绝对的 luban.conf 路径。</param>
        /// <returns>包含目标、Excel、Sheet、表关联和解析错误的模型。</returns>
        internal static TableProjectModel Build(string configPath)
        {
            return Build(configPath, EditorUtil.Excel.GetSheetNames);
        }

        /// <summary>
        /// 使用可替换的 Sheet 读取器构建模型，便于隔离文件格式读取进行测试。
        /// </summary>
        /// <param name="configPath">项目根相对或绝对的 luban.conf 路径。</param>
        /// <param name="sheetReader">Excel 路径到 Sheet 名称清单的读取器。</param>
        /// <returns>Project 只读模型。</returns>
        internal static TableProjectModel Build(string configPath, Func<string, List<string>> sheetReader)
        {
            var model = new TableProjectModel { ConfigPath = Normalize(configPath) };
            if (string.IsNullOrWhiteSpace(configPath))
            {
                model.Errors.Add("Luban 配置文件路径为空。");
                return model;
            }

            string absoluteConfigPath = IOPath.GetFullPath(configPath);
            if (!File.Exists(absoluteConfigPath))
            {
                model.Errors.Add($"Luban 配置文件不存在：{configPath}");
                return model;
            }

            try
            {
                LubanConfigDto config = Util.Json.Deserialize<LubanConfigDto>(File.ReadAllText(absoluteConfigPath));
                string projectDirectory = IOPath.GetDirectoryName(absoluteConfigPath) ?? Directory.GetCurrentDirectory();
                string dataDirectory = IOPath.GetFullPath(IOPath.Combine(projectDirectory, config?.dataDir ?? "."));
                model.DataDirectory = Normalize(dataDirectory);
                model.SchemaFiles = (config?.schemaFiles ?? new List<LubanSchemaFileDto>())
                    .Where(schema => schema != null && !string.IsNullOrWhiteSpace(schema.fileName))
                    .Select(schema => Normalize(IOPath.GetFullPath(IOPath.Combine(projectDirectory, schema.fileName))))
                    .ToList();
                model.Targets = config?.targets?
                    .Where(target => target != null && !string.IsNullOrWhiteSpace(target.name))
                    .Select(target => target.name)
                    .Distinct(StringComparer.Ordinal)
                    .ToList() ?? new List<string>();
                foreach (LubanTargetDto target in config?.targets ?? new List<LubanTargetDto>())
                {
                    if (target == null || string.IsNullOrWhiteSpace(target.name) ||
                        string.IsNullOrWhiteSpace(target.manager))
                    {
                        continue;
                    }
                    string typeName = string.IsNullOrWhiteSpace(target.topModule)
                        ? target.manager + "Binding"
                        : target.topModule.TrimEnd('.') + "." + target.manager + "Binding";
                    model.BindingTypeByTarget[target.name] = typeName;
                }

                Dictionary<string, List<string>> tableNamesByInput = ReadTableInputs(config, projectDirectory, model.Errors);
                if (!Directory.Exists(dataDirectory))
                {
                    model.Errors.Add($"Luban 数据目录不存在：{model.DataDirectory}");
                    return model;
                }

                foreach (string excelPath in Directory.GetFiles(dataDirectory, "*", SearchOption.AllDirectories)
                             .Where(EditorUtil.Excel.IsExcelFile)
                             .Where(path => !IOPath.GetFileName(path).StartsWith(EditorUtil.Excel.c_ExcludePrefix, StringComparison.Ordinal))
                             .OrderBy(path => path, StringComparer.OrdinalIgnoreCase))
                {
                    string relativePath = Normalize(IOPath.GetRelativePath(dataDirectory, excelPath));
                    var excel = new TableProjectExcelFile
                    {
                        RelativePath = relativePath,
                        AbsolutePath = Normalize(excelPath),
                    };
                    try
                    {
                        foreach (string sheetName in sheetReader(excelPath))
                        {
                            string inputKey = Normalize(sheetName + "@" + relativePath);
                            excel.Sheets.Add(new TableProjectExcelSheet
                            {
                                Name = sheetName,
                                TableNames = tableNamesByInput.TryGetValue(inputKey, out List<string> tableNames)
                                    ? new List<string>(tableNames)
                                    : new List<string>(),
                            });
                        }
                    }
                    catch (Exception exception)
                    {
                        model.Errors.Add($"读取 Excel 失败：{relativePath}；{exception.Message}");
                    }
                    model.ExcelFiles.Add(excel);
                }
            }
            catch (Exception exception)
            {
                model.Errors.Add($"解析 Luban Project 失败：{exception.Message}");
            }
            return model;
        }

        /// <summary>
        /// 解析删除 Luban 工程引用时允许从磁盘移除的精确文件清单，只包含显式 Schema 文件和配置文件。
        /// </summary>
        /// <param name="configPath">Luban 配置文件路径。</param>
        /// <returns>Schema 文件在前、配置文件在后的绝对路径清单；不会扫描任何目录。</returns>
        internal static List<string> ResolveExplicitDeletionFiles(string configPath)
        {
            if (string.IsNullOrWhiteSpace(configPath))
            {
                throw new ArgumentException("Luban 配置文件路径为空。", nameof(configPath));
            }

            string absoluteConfigPath = IOPath.GetFullPath(configPath);
            if (!File.Exists(absoluteConfigPath))
            {
                throw new FileNotFoundException("Luban 配置文件不存在。", absoluteConfigPath);
            }

            LubanConfigDto config = Util.Json.Deserialize<LubanConfigDto>(File.ReadAllText(absoluteConfigPath));
            string projectDirectory = IOPath.GetDirectoryName(absoluteConfigPath) ?? Directory.GetCurrentDirectory();
            var files = new List<string>();
            foreach (LubanSchemaFileDto schema in config?.schemaFiles ?? new List<LubanSchemaFileDto>())
            {
                if (schema == null || string.IsNullOrWhiteSpace(schema.fileName))
                {
                    continue;
                }
                string schemaPath = Normalize(IOPath.GetFullPath(IOPath.Combine(projectDirectory, schema.fileName)));
                if (!string.Equals(schemaPath, Normalize(absoluteConfigPath), StringComparison.OrdinalIgnoreCase) &&
                    !files.Contains(schemaPath, StringComparer.OrdinalIgnoreCase))
                {
                    files.Add(schemaPath);
                }
            }
            files.Add(Normalize(absoluteConfigPath));
            return files;
        }

        /// <summary>
        /// 读取全部 schema 文件中的 table input，并按 Sheet@Excel 建立表名索引。
        /// </summary>
        /// <param name="config">Luban 配置数据。</param>
        /// <param name="projectDirectory">配置文件所在目录。</param>
        /// <param name="errors">解析错误收集列表。</param>
        /// <returns>输入声明到 Luban 表全名列表的映射。</returns>
        private static Dictionary<string, List<string>> ReadTableInputs(
            LubanConfigDto config,
            string projectDirectory,
            List<string> errors)
        {
            var result = new Dictionary<string, List<string>>(StringComparer.OrdinalIgnoreCase);
            foreach (LubanSchemaFileDto schema in config?.schemaFiles ?? new List<LubanSchemaFileDto>())
            {
                if (schema == null || string.IsNullOrWhiteSpace(schema.fileName))
                {
                    continue;
                }

                string schemaPath = IOPath.GetFullPath(IOPath.Combine(projectDirectory, schema.fileName));
                if (!File.Exists(schemaPath))
                {
                    errors.Add($"Luban schema 文件不存在：{Normalize(schemaPath)}");
                    continue;
                }

                try
                {
                    foreach (XElement table in XDocument.Load(schemaPath).Descendants("table"))
                    {
                        string tableName = ResolveFullTableName(table);
                        string input = Normalize((string)table.Attribute("input"));
                        if (string.IsNullOrWhiteSpace(tableName) || string.IsNullOrWhiteSpace(input))
                        {
                            continue;
                        }
                        if (!result.TryGetValue(input, out List<string> tableNames))
                        {
                            tableNames = new List<string>();
                            result.Add(input, tableNames);
                        }
                        if (!tableNames.Contains(tableName))
                        {
                            tableNames.Add(tableName);
                        }
                    }
                }
                catch (Exception exception)
                {
                    errors.Add($"解析 Luban schema 失败：{Normalize(schemaPath)}；{exception.Message}");
                }
            }
            return result;
        }

        /// <summary>
        /// 将嵌套 module 名称与 table name 组合成 Luban 可用于 -o 的完整表名。
        /// </summary>
        /// <param name="table">schema 中的 table 节点。</param>
        /// <returns>Luban 表完整名。</returns>
        private static string ResolveFullTableName(XElement table)
        {
            string tableName = (string)table.Attribute("name") ?? string.Empty;
            if (tableName.Contains('.'))
            {
                return tableName;
            }

            string[] modules = table.Ancestors("module")
                .Reverse()
                .Select(module => (string)module.Attribute("name"))
                .Where(name => !string.IsNullOrWhiteSpace(name))
                .ToArray();
            return modules.Length == 0 ? tableName : string.Join(".", modules) + "." + tableName;
        }

        /// <summary>
        /// 把路径分隔符统一为正斜杠，保留 Luban input 的 Sheet@Excel 结构。
        /// </summary>
        /// <param name="value">待规范化文本。</param>
        /// <returns>规范化结果。</returns>
        private static string Normalize(string value)
        {
            return (value ?? string.Empty).Replace('\\', '/');
        }

        [Serializable]
        private sealed class LubanConfigDto
        {
            public string dataDir = ".";
            public List<LubanSchemaFileDto> schemaFiles = new List<LubanSchemaFileDto>();
            public List<LubanTargetDto> targets = new List<LubanTargetDto>();
        }

        [Serializable]
        private sealed class LubanSchemaFileDto
        {
            public string fileName = string.Empty;
        }

        [Serializable]
        private sealed class LubanTargetDto
        {
            public string name = string.Empty;
            public string manager = string.Empty;
            public string topModule = string.Empty;
        }
    }

    /// <summary>
    /// 使用 AssetComponent 默认资源包的 YooAsset 收集规则解析 Asset 地址。
    /// </summary>
    internal static class TableAssetAddressResolver
    {
        /// <summary>
        /// 批量把 Unity AssetPath 映射为默认资源包最终生成的 YooAsset Asset 地址。
        /// </summary>
        /// <param name="assetComponent">提供默认资源包名的 AssetComponent。</param>
        /// <param name="assetPaths">待解析的 Unity AssetPath。</param>
        /// <returns>AssetPath 到 Asset 地址的映射；未被收集的路径不会出现在结果中。</returns>
        internal static Dictionary<string, string> Resolve(
            AssetComponent assetComponent,
            IEnumerable<string> assetPaths)
        {
            var result = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            if (assetComponent == null || assetPaths == null)
            {
                return result;
            }

            string packageName = ResolveDefaultPackageName(assetComponent);
            if (string.IsNullOrWhiteSpace(packageName))
            {
                return result;
            }

            var requested = new HashSet<string>(
                assetPaths.Where(path => !string.IsNullOrWhiteSpace(path)).Select(NormalizeAssetPath),
                StringComparer.OrdinalIgnoreCase);
            CollectResult collectResult = CollectRequestedAssets(packageName, requested);
            foreach (CollectAssetInfo collectAsset in collectResult.CollectAssets)
            {
                string assetPath = NormalizeAssetPath(collectAsset?.AssetInfo?.AssetPath);
                if (requested.Contains(assetPath) && !string.IsNullOrWhiteSpace(collectAsset.Address))
                {
                    result[assetPath] = collectAsset.Address;
                }
            }
            return result;
        }

        /// <summary>
        /// 使用临时配置副本收集地址，避免尚未生成且与当前请求无关的 HybridCLR 目录阻断解析。
        /// </summary>
        private static CollectResult CollectRequestedAssets(
            string packageName,
            HashSet<string> requested)
        {
            BundleCollectorSetting source = BundleCollectorSettingData.Setting;
            BundleCollectorSetting working = UnityEngine.Object.Instantiate(source);
            working.hideFlags = UnityEngine.HideFlags.HideAndDontSave;
            try
            {
                BundleCollectorPackage package = working.GetPackage(packageName);
                if (package != null)
                {
                    foreach (BundleCollectorGroup group in package.Groups)
                    {
                        group.Collectors.RemoveAll(collector =>
                            IsMissingUnrelatedCollector(collector, requested));
                    }
                }

                return working.BeginCollect(packageName, false, false);
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(working);
            }
        }

        /// <summary>
        /// 判断收集器路径是否不存在，且不可能包含本次需要解析的资产。
        /// </summary>
        private static bool IsMissingUnrelatedCollector(
            BundleCollector collector,
            HashSet<string> requested)
        {
            string collectPath = NormalizeAssetPath(collector?.CollectPath).TrimEnd('/');
            if (string.IsNullOrWhiteSpace(collectPath) ||
                !string.IsNullOrEmpty(AssetDatabase.AssetPathToGUID(collectPath)))
            {
                return false;
            }

            string directoryPrefix = collectPath + "/";
            return !requested.Any(path =>
                string.Equals(path, collectPath, StringComparison.OrdinalIgnoreCase) ||
                path.StartsWith(directoryPrefix, StringComparison.OrdinalIgnoreCase));
        }

        /// <summary>
        /// 读取 AssetComponent 的默认资源包名；空值时回退到资源包清单第一项。
        /// </summary>
        /// <param name="assetComponent">目标资源组件。</param>
        /// <returns>默认资源包名，无法解析时为空。</returns>
        private static string ResolveDefaultPackageName(AssetComponent assetComponent)
        {
            using var serializedComponent = new SerializedObject(assetComponent);
            string packageName = serializedComponent.FindProperty("m_DefaultPackageName")?.stringValue;
            if (!string.IsNullOrWhiteSpace(packageName))
            {
                return packageName;
            }

            SerializedProperty packages = serializedComponent.FindProperty("m_Packages");
            return packages != null && packages.isArray && packages.arraySize > 0
                ? packages.GetArrayElementAtIndex(0).stringValue
                : string.Empty;
        }

        /// <summary>
        /// 统一 Unity AssetPath 的分隔符。
        /// </summary>
        /// <param name="assetPath">待规范化路径。</param>
        /// <returns>正斜杠路径。</returns>
        private static string NormalizeAssetPath(string assetPath)
        {
            return (assetPath ?? string.Empty).Replace('\\', '/');
        }
    }
}
