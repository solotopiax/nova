/***************************************************************
 * (c) copyright 2026 - 2030, Solotopia
 * All Rights Reserved.
 * -------------------------------------------------------------
 * filename:  UIExporter.cs
 * author:    taoye
 * created:   2026/5/11
 * descrip:   UI 专用导出内部编排：校验 Excel，暂存 Luban 产物，验证后批量发布
 * input:     UISettings、UI Excel 源目录以及可选的单文件目标
 * output:    UI 注册表 JSON、Luban C# 类型与 Map 属性
 * boundary:  复用 Luban/Excel 基础设施，不承担 Runtime UI 加载和视图实例管理
 * failure:   校验、生成或发布失败时返回 false，正式产物保持或回滚到导出前状态
 ***************************************************************/

using System;
using System.Collections.Generic;
using System.IO;
using NovaFramework.Runtime;
using UnityEditor;
using IOPath = System.IO.Path;

namespace NovaFramework.Editor
{
    /// <summary>
    /// UI 导出内部编排器：负责输入校验、Luban 生成、暂存验证和正式产物发布。
    /// <para>公共调用入口是 <see cref="EditorUtil.UI.Exporter"/>；文件应用与回滚由
    /// <see cref="EditorUtil.FileSystem.OutputApplier"/> 完成。</para>
    /// </summary>
    internal static class UIExporter
    {
        private enum ExportMode
        {
            All,
            Code,
            Data,
        }

        internal sealed class ExportOperations
        {
            internal Action<string, IReadOnlyList<UIUnitSetting>> ValidateSources = UIExcelValidator.Validate;
            internal Func<EditorUtil.Luban.LubanExportContext, bool> ExportAll = EditorUtil.Luban.Pipeline.ExportAll;
            internal Func<EditorUtil.Luban.LubanExportContext, bool> ExportCode = EditorUtil.Luban.Pipeline.ExportCode;
            internal Func<EditorUtil.Luban.LubanExportContext, bool> ExportData = EditorUtil.Luban.Pipeline.ExportData;
            internal Action RefreshAssetDatabase = AssetDatabase.Refresh;
        }

        internal static class UIExcelValidator
        {
            private static readonly string[] s_RequiredColumns =
            {
                "Name",
                "AssetLocation",
                "UIGroupName",
                "PauseCoveredUIView",
                "InObjectPools",
            };

            private static readonly Dictionary<string, string> s_RequiredTypes =
                new Dictionary<string, string>(StringComparer.Ordinal)
                {
                    ["Name"] = "string",
                    ["AssetLocation"] = "string",
                    ["UIGroupName"] = "string",
                    ["PauseCoveredUIView"] = "bool",
                    ["InObjectPools"] = "bool",
                };

            internal static void Validate(string sourceDirPath, IReadOnlyList<UIUnitSetting> units)
            {
                Validate(sourceDirPath, units, EditorUtil.Excel.ReadAllSheets);
            }

            internal static void Validate(
                string sourceDirPath,
                IReadOnlyList<UIUnitSetting> units,
                Func<string, Dictionary<string, List<IReadOnlyList<string>>>> readAllSheets)
            {
                if (string.IsNullOrWhiteSpace(sourceDirPath))
                {
                    throw new InvalidDataException("UI Excel source directory cannot be empty.");
                }
                if (units == null || units.Count == 0)
                {
                    throw new InvalidDataException("UI Excel units cannot be empty.");
                }
                if (readAllSheets == null)
                {
                    throw new ArgumentNullException(nameof(readAllSheets));
                }

                var names = new Dictionary<string, string>(StringComparer.Ordinal);
                int activeSheetCount = 0;
                for (int unitIndex = 0; unitIndex < units.Count; unitIndex++)
                {
                    UIUnitSetting unit = units[unitIndex] ??
                        throw new InvalidDataException($"UI Excel unit at index {unitIndex} is null.");
                    if (string.IsNullOrWhiteSpace(unit.SourcePath))
                    {
                        throw new InvalidDataException($"UI Excel unit at index {unitIndex} has an empty SourcePath.");
                    }

                    string fullPath = IOPath.Combine(sourceDirPath, unit.SourcePath);
                    Dictionary<string, List<IReadOnlyList<string>>> sheets = readAllSheets(fullPath) ??
                        throw new InvalidDataException($"UI Excel reader returned null: {unit.SourcePath}");
                    foreach (KeyValuePair<string, List<IReadOnlyList<string>>> pair in sheets)
                    {
                        string sheetName = pair.Key;
                        if (string.IsNullOrWhiteSpace(sheetName) || sheetName.StartsWith("#", StringComparison.Ordinal))
                        {
                            continue;
                        }

                        activeSheetCount++;
                        ValidateSheet(unit.SourcePath, sheetName, pair.Value, names);
                    }
                }

                if (activeSheetCount == 0)
                {
                    throw new InvalidDataException("UI Excel sources do not contain any exportable sheets.");
                }
            }

            private static void ValidateSheet(
                string sourcePath,
                string sheetName,
                IReadOnlyList<IReadOnlyList<string>> rows,
                IDictionary<string, string> names)
            {
                if (rows == null || rows.Count < 5)
                {
                    throw Error(sourcePath, sheetName, 0, "有效 Sheet 至少需要 5 行表头与数据。");
                }

                IReadOnlyList<string> variableRow = rows[1];
                IReadOnlyList<string> typeRow = rows[2];
                if (!string.Equals(GetCell(variableRow, 0), "##var", StringComparison.Ordinal) ||
                    !string.Equals(GetCell(typeRow, 0), "##type", StringComparison.Ordinal))
                {
                    throw Error(sourcePath, sheetName, 0, "第 2、3 行必须分别以 ##var、##type 开头。");
                }

                var columnIndexes = new Dictionary<string, int>(StringComparer.Ordinal);
                for (int i = 1; i < variableRow.Count; i++)
                {
                    string columnName = GetCell(variableRow, i);
                    if (!string.IsNullOrWhiteSpace(columnName) && !columnIndexes.ContainsKey(columnName))
                    {
                        columnIndexes.Add(columnName, i);
                    }
                }

                foreach (string requiredColumn in s_RequiredColumns)
                {
                    if (!columnIndexes.TryGetValue(requiredColumn, out int columnIndex))
                    {
                        throw Error(sourcePath, sheetName, 0, $"缺少必需列 {requiredColumn}。");
                    }

                    string actualType = GetCell(typeRow, columnIndex);
                    string expectedType = s_RequiredTypes[requiredColumn];
                    if (!string.Equals(actualType, expectedType, StringComparison.Ordinal))
                    {
                        throw Error(
                            sourcePath,
                            sheetName,
                            3,
                            $"列 {requiredColumn} 类型必须是 {expectedType}，当前为 {actualType}。");
                    }
                }

                for (int rowIndex = 4; rowIndex < rows.Count; rowIndex++)
                {
                    IReadOnlyList<string> row = rows[rowIndex];
                    if (IsEmptyRow(row))
                    {
                        continue;
                    }

                    foreach (string requiredColumn in s_RequiredColumns)
                    {
                        string value = GetCell(row, columnIndexes[requiredColumn]);
                        if (string.IsNullOrWhiteSpace(value))
                        {
                            throw Error(
                                sourcePath,
                                sheetName,
                                rowIndex + 1,
                                $"列 {requiredColumn} 不能为空。");
                        }

                        if ((requiredColumn == "PauseCoveredUIView" || requiredColumn == "InObjectPools") &&
                            !bool.TryParse(value, out _))
                        {
                            throw Error(
                                sourcePath,
                                sheetName,
                                rowIndex + 1,
                                $"列 {requiredColumn} 必须填写 true 或 false。");
                        }
                    }

                    string name = GetCell(row, columnIndexes["Name"]);
                    string location = $"{sourcePath}/{sheetName}/第 {rowIndex + 1} 行";
                    if (names.TryGetValue(name, out string previousLocation))
                    {
                        throw Error(
                            sourcePath,
                            sheetName,
                            rowIndex + 1,
                            $"Name '{name}' 重复，首次出现于 {previousLocation}。");
                    }

                    names.Add(name, location);
                }
            }

            private static InvalidDataException Error(
                string sourcePath,
                string sheetName,
                int rowNumber,
                string message)
            {
                string row = rowNumber > 0 ? $"，第 {rowNumber} 行" : string.Empty;
                return new InvalidDataException($"UI Excel '{sourcePath}'，Sheet '{sheetName}'{row}：{message}");
            }

            private static string GetCell(IReadOnlyList<string> row, int index)
            {
                return row != null && index >= 0 && index < row.Count
                    ? row[index]?.Trim() ?? string.Empty
                    : string.Empty;
            }

            private static bool IsEmptyRow(IReadOnlyList<string> row)
            {
                if (row == null)
                {
                    return true;
                }

                for (int i = 0; i < row.Count; i++)
                {
                    if (!string.IsNullOrWhiteSpace(row[i]))
                    {
                        return false;
                    }
                }

                return true;
            }
        }

        /// <summary>
        /// 全量导出：校验 UI Excel 后在 _temp 中生成代码与数据，验证通过后成批发布。
        /// <para>所有 UIUnitSetting 必须使用同一个 ClassesExportPath；任一阶段失败均返回 false。</para>
        /// </summary>
        /// <param name="settings">UI 模块配置，含 UIUnitsSettings 列表。</param>
        /// <param name="sourceDirPath">数据源目录完整路径。</param>
        public static bool ExportAll(UISettings settings, string sourceDirPath)
        {
            return ExportAll(settings, sourceDirPath, new ExportOperations());
        }

        internal static bool ExportAll(UISettings settings, string sourceDirPath, ExportOperations operations)
        {
            if (!TryValidate(settings, sourceDirPath, operations, true, true, out string classExportPath))
            {
                return false;
            }

            if (!ExecuteStaged(settings, sourceDirPath, classExportPath, null, ExportMode.All, operations))
            {
                Log.Error(LogTag.Editor, "UI 全量导出失败，正式产物未更新。");
                return false;
            }

            return true;
        }

        /// <summary>
        /// 全量仅导出代码（类型定义）：正式 JSON 只复制到暂存区供 Map 属性生成，不修改数据产物。
        /// </summary>
        /// <param name="settings">UI 模块配置，含 UIUnitsSettings 列表。</param>
        /// <param name="sourceDirPath">数据源目录完整路径。</param>
        public static bool ExportCode(UISettings settings, string sourceDirPath)
        {
            return ExportCode(settings, sourceDirPath, new ExportOperations());
        }

        internal static bool ExportCode(UISettings settings, string sourceDirPath, ExportOperations operations)
        {
            if (!TryValidate(settings, sourceDirPath, operations, true, false, out string classExportPath))
            {
                return false;
            }

            if (!ExecuteStaged(settings, sourceDirPath, classExportPath, null, ExportMode.Code, operations))
            {
                Log.Error(LogTag.Editor, "UI 类型导出失败，正式产物未更新。");
                return false;
            }

            return true;
        }

        /// <summary>
        /// 全量仅导出数据（JSON）：先生成到暂存区，完整成功后再替换正式数据文件。
        /// </summary>
        /// <param name="settings">UI 模块配置，含 UIUnitsSettings 列表。</param>
        /// <param name="sourceDirPath">数据源目录完整路径。</param>
        public static bool ExportData(UISettings settings, string sourceDirPath)
        {
            return ExportData(settings, sourceDirPath, new ExportOperations());
        }

        internal static bool ExportData(UISettings settings, string sourceDirPath, ExportOperations operations)
        {
            if (!TryValidate(settings, sourceDirPath, operations, false, true, out _))
            {
                return false;
            }

            if (!ExecuteStaged(settings, sourceDirPath, null, null, ExportMode.Data, operations))
            {
                Log.Error(LogTag.Editor, "UI 数据导出失败，正式产物未更新。");
                return false;
            }

            return true;
        }

        /// <summary>
        /// 单文件代码导出：必须精确匹配 UIUnitSetting，只发布该单元相关类型及 UITables。
        /// </summary>
        /// <param name="settings">UI 模块配置，含 UIUnitsSettings 列表。</param>
        /// <param name="sourceDirPath">数据源目录完整路径。</param>
        /// <param name="filePath">目标源文件完整路径。</param>
        /// <param name="classExportPath">代码输出目录路径。</param>
        public static bool ExportCodeForFile(UISettings settings, string sourceDirPath, string filePath, string classExportPath)
        {
            return ExportCodeForFile(settings, sourceDirPath, filePath, classExportPath, new ExportOperations());
        }

        internal static bool ExportCodeForFile(
            UISettings settings,
            string sourceDirPath,
            string filePath,
            string classExportPath,
            ExportOperations operations)
        {
            if (settings == null || string.IsNullOrEmpty(sourceDirPath) || string.IsNullOrEmpty(classExportPath))
            {
                Log.Error(LogTag.Editor, "UI 单文件类型导出参数无效。");
                return false;
            }

            string relativePath = NormalizePath(Util.SysIO.Path.GetRelativePath(sourceDirPath.TrimEnd('/', '\\'), filePath));
            UIUnitSetting unitSetting = FindUnit(settings.UIUnitsSettings, relativePath);
            if (unitSetting == null)
            {
                Log.Error(LogTag.Editor, "未找到文件 {0} 对应的 UIUnitSetting，已取消类型导出。", relativePath);
                return false;
            }

            if (!TryValidate(settings, sourceDirPath, operations, false, false, out _))
            {
                return false;
            }

            if (!ExecuteStaged(
                    settings,
                    sourceDirPath,
                    classExportPath,
                    unitSetting,
                    ExportMode.Code,
                    operations))
            {
                Log.Error(LogTag.Editor, "UI 单文件类型导出失败，正式产物未更新。");
                return false;
            }

            return true;
        }

        /// <summary>
        /// 单文件数据导出：为指定数据源文件执行 Luban 数据导出并合并 JSON。
        /// <para>若在 UIUnitsSettings 中未找到与 filePath 对应的 UIUnitSetting，则输出错误日志并返回。</para>
        /// </summary>
        /// <param name="settings">UI 模块配置，含 UIUnitsSettings 列表。</param>
        /// <param name="sourceDirPath">数据源目录完整路径。</param>
        /// <param name="filePath">目标源文件完整路径。</param>
        public static bool ExportDataForFile(UISettings settings, string sourceDirPath, string filePath)
        {
            return ExportDataForFile(settings, sourceDirPath, filePath, new ExportOperations());
        }

        internal static bool ExportDataForFile(
            UISettings settings,
            string sourceDirPath,
            string filePath,
            ExportOperations operations)
        {
            if (settings == null || string.IsNullOrEmpty(sourceDirPath))
            {
                Log.Error(LogTag.Editor, "UI 单文件数据导出参数无效。");
                return false;
            }

            string relativePath = NormalizePath(Util.SysIO.Path.GetRelativePath(sourceDirPath.TrimEnd('/', '\\'), filePath));
            UIUnitSetting unitSetting = FindUnit(settings.UIUnitsSettings, relativePath);
            if (unitSetting == null)
            {
                Log.Error(LogTag.Editor, "未找到文件 {0} 对应的 UIUnitSetting，已取消数据导出。", relativePath);
                return false;
            }

            if (!TryValidate(settings, sourceDirPath, operations, false, true, out _))
            {
                return false;
            }

            if (!ExecuteStaged(
                    settings,
                    sourceDirPath,
                    null,
                    unitSetting,
                    ExportMode.Data,
                    operations))
            {
                Log.Error(LogTag.Editor, "UI 单文件数据导出失败，正式产物未更新。");
                return false;
            }

            return true;
        }

        private static bool ExecuteStaged(
            UISettings settings,
            string sourceDirPath,
            string formalClassExportPath,
            UIUnitSetting formalTargetUnit,
            ExportMode mode,
            ExportOperations operations)
        {
            operations ??= new ExportOperations();
            string tempRoot = IOPath.GetFullPath(IOPath.Combine(sourceDirPath, "_temp"));
            try
            {
                using IDisposable workspace = EditorUtil.FileSystem.AcquireWorkspace(tempRoot);
                DeleteTempRoot(tempRoot);

                using var applier = new EditorUtil.FileSystem.OutputApplier(tempRoot);
                string stagedCodeDir = IOPath.Combine(applier.StagingRoot, "code~");
                UISettings stagedSettings = CreateStagedSettings(
                    settings,
                    applier.StagingRoot,
                    stagedCodeDir,
                    out Dictionary<UIUnitSetting, UIUnitSetting> stagedUnits);
                if (mode == ExportMode.Code)
                {
                    SeedStagedData(settings, stagedUnits);
                }

                var context = EditorUtil.Luban.ExportHelper.BuildExportContext(
                    sourceDirPath,
                    stagedSettings,
                    EditorUtil.Luban.LubanExportProfiles.UI);
                context.DataFormat = settings.DataFormat;
                if (mode != ExportMode.Data)
                {
                    context.OutputCodeDir = stagedCodeDir;
                }
                if (formalTargetUnit != null)
                {
                    context.TargetUnit = stagedUnits[formalTargetUnit];
                }

                bool pipelineSuccess = mode switch
                {
                    ExportMode.All => operations.ExportAll(context),
                    ExportMode.Code => operations.ExportCode(context),
                    ExportMode.Data => operations.ExportData(context),
                    _ => false,
                };
                if (!pipelineSuccess)
                {
                    return false;
                }

                if (mode != ExportMode.Code)
                {
                    RegisterDataOutputs(applier, settings, stagedUnits, formalTargetUnit);
                }
                if (mode != ExportMode.Data)
                {
                    RegisterCodeOutputs(
                        applier,
                        context,
                        stagedCodeDir,
                        formalClassExportPath,
                        formalTargetUnit == null);
                }

                applier.Apply();
                return true;
            }
            catch (Exception exception)
            {
                Log.Warning(LogTag.Editor, "UI 导出暂存或发布失败：{0}", exception);
                return false;
            }
            finally
            {
                try
                {
                    DeleteTempRoot(tempRoot);
                }
                catch (Exception cleanupException)
                {
                    Log.Warning(LogTag.Editor, "清理 UI 导出临时目录失败：{0}", cleanupException.Message);
                }

                operations.RefreshAssetDatabase?.Invoke();
            }
        }

        private static void DeleteTempRoot(string tempRoot)
        {
            EditorUtil.FileSystem.DeleteUnityTempRoot(tempRoot);
        }

        private static UISettings CreateStagedSettings(
            UISettings settings,
            string stagingRoot,
            string stagedCodeDir,
            out Dictionary<UIUnitSetting, UIUnitSetting> stagedUnits)
        {
            var stagedSettings = new UISettings { DataFormat = settings.DataFormat };
            stagedUnits = new Dictionary<UIUnitSetting, UIUnitSetting>();
            for (int i = 0; i < settings.UIUnitsSettings.Count; i++)
            {
                UIUnitSetting source = settings.UIUnitsSettings[i];
                string dataExtension = settings.DataFormat == LubanDataFormat.Binary ? ".bytes" : ".json";
                string dataFileName = i.ToString("D4") + dataExtension;
                var staged = new UIUnitSetting
                {
                    SourcePath = source.SourcePath,
                    DatasExportPath = IOPath.Combine(stagingRoot, "data", i.ToString("D4") + "_" + dataFileName),
                    ClassesExportPath = stagedCodeDir,
                    AssetLocation = source.AssetLocation,
                };
                stagedSettings.UIUnitsSettings.Add(staged);
                stagedUnits.Add(source, staged);
            }

            return stagedSettings;
        }

        private static void SeedStagedData(
            UISettings formalSettings,
            IReadOnlyDictionary<UIUnitSetting, UIUnitSetting> stagedUnits)
        {
            foreach (UIUnitSetting formalUnit in formalSettings.UIUnitsSettings)
            {
                if (string.IsNullOrWhiteSpace(formalUnit.DatasExportPath) ||
                    !File.Exists(formalUnit.DatasExportPath))
                {
                    continue;
                }

                string stagedPath = stagedUnits[formalUnit].DatasExportPath;
                string stagedDirectory = IOPath.GetDirectoryName(stagedPath);
                if (!string.IsNullOrEmpty(stagedDirectory))
                {
                    Directory.CreateDirectory(stagedDirectory);
                }
                File.Copy(formalUnit.DatasExportPath, stagedPath, true);
            }
        }

        private static void RegisterDataOutputs(
            EditorUtil.FileSystem.OutputApplier applier,
            UISettings formalSettings,
            IReadOnlyDictionary<UIUnitSetting, UIUnitSetting> stagedUnits,
            UIUnitSetting formalTargetUnit)
        {
            foreach (UIUnitSetting formalUnit in formalSettings.UIUnitsSettings)
            {
                if (formalTargetUnit != null && !ReferenceEquals(formalTargetUnit, formalUnit))
                {
                    continue;
                }
                if (string.IsNullOrWhiteSpace(formalUnit.DatasExportPath))
                {
                    continue;
                }

                UIUnitSetting stagedUnit = stagedUnits[formalUnit];
                applier.AddReplacement(stagedUnit.DatasExportPath, formalUnit.DatasExportPath);
                EditorUtil.Luban.DataArtifact.RegisterCounterpartDeletion(
                    applier, formalUnit.DatasExportPath, formalSettings.DataFormat);
            }
        }

        private static void RegisterCodeOutputs(
            EditorUtil.FileSystem.OutputApplier applier,
            EditorUtil.Luban.LubanExportContext context,
            string stagedCodeDir,
            string formalCodeDir,
            bool deleteStaleFiles)
        {
            EditorUtil.Luban.GeneratedOutput.RegisterCodeOutputs(
                applier,
                context,
                stagedCodeDir,
                formalCodeDir,
                deleteStaleFiles);
        }

        private static bool TryValidate(
            UISettings settings,
            string sourceDirPath,
            ExportOperations operations,
            bool requireClassExportPath,
            bool requireDataExportPath,
            out string classExportPath)
        {
            classExportPath = string.Empty;
            if (settings == null || string.IsNullOrWhiteSpace(sourceDirPath) ||
                settings.UIUnitsSettings == null || settings.UIUnitsSettings.Count == 0)
            {
                Log.Error(LogTag.Editor, "UI 导出配置为空或不完整。");
                return false;
            }

            operations ??= new ExportOperations();
            var classPaths = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            var dataPaths = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            var assetLocations = new HashSet<string>(StringComparer.Ordinal);
            foreach (UIUnitSetting unit in settings.UIUnitsSettings)
            {
                if (unit == null)
                {
                    Log.Error(LogTag.Editor, "UIUnitsSettings 中存在空单元。");
                    return false;
                }

                if (string.IsNullOrWhiteSpace(unit.AssetLocation))
                {
                    Log.Error(LogTag.Editor, "UI 单元 {0} 的 Asset 地址不能为空。", unit.SourcePath);
                    return false;
                }
                if (!assetLocations.Add(unit.AssetLocation.Trim()))
                {
                    Log.Error(LogTag.Editor, "UI Asset 地址重复：{0}。", unit.AssetLocation);
                    return false;
                }

                if (!string.IsNullOrWhiteSpace(unit.DatasExportPath) &&
                    !dataPaths.Add(NormalizePath(unit.DatasExportPath)))
                {
                    Log.Error(LogTag.Editor, "UI 数据导出路径重复：{0}。", unit.DatasExportPath);
                    return false;
                }
                if (requireDataExportPath && string.IsNullOrWhiteSpace(unit.DatasExportPath))
                {
                    Log.Error(LogTag.Editor, "UI 单元 {0} 的数据导出路径不能为空。", unit.SourcePath);
                    return false;
                }

                if (!string.IsNullOrWhiteSpace(unit.ClassesExportPath))
                {
                    classPaths.Add(NormalizePath(unit.ClassesExportPath));
                    classExportPath = unit.ClassesExportPath;
                }
            }

            if (classPaths.Count > 1)
            {
                Log.Error(LogTag.Editor, "UI 所有单元的类型导出路径必须一致，当前检测到 {0} 个不同路径。", classPaths.Count);
                return false;
            }

            if (requireClassExportPath && classPaths.Count == 0)
            {
                Log.Error(LogTag.Editor, "UI 类型导出路径未配置。");
                return false;
            }

            try
            {
                operations.ValidateSources(sourceDirPath, settings.UIUnitsSettings);
            }
            catch (Exception exception)
            {
                Log.Error(LogTag.Editor, "UI Excel 校验失败：{0}", exception.Message);
                return false;
            }

            return true;
        }

        private static UIUnitSetting FindUnit(IReadOnlyList<UIUnitSetting> units, string relativePath)
        {
            if (units == null)
            {
                return null;
            }

            for (int i = 0; i < units.Count; i++)
            {
                UIUnitSetting unit = units[i];
                if (unit != null && string.Equals(
                        NormalizePath(unit.SourcePath),
                        relativePath,
                        StringComparison.OrdinalIgnoreCase))
                {
                    return unit;
                }
            }

            return null;
        }

        private static string NormalizePath(string path)
        {
            return path?.Trim().Replace('\\', '/').TrimEnd('/') ?? string.Empty;
        }
    }
}
