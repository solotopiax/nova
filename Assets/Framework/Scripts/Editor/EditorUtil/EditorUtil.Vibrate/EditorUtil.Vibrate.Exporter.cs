/***************************************************************
 * (c) copyright 2026 - 2030, Solotopia
 * All Rights Reserved.
 * -------------------------------------------------------------
 * filename:  EditorUtil.Vibrate.Exporter.cs
 * author:    taoye
 * created:   2026/5/11
 * descrip:   Vibrate 专用导出编排：Emphasis/Custom 分轨暂存、验证并发布 Luban 产物
 * input:     VibrateSettings、目标区域及可选的单文件目标
 * output:    Vibrate JSON 与 Luban C# 类型文件
 * boundary:  两个区域保持独立；文件替换与失败回滚复用 FileSystem.OutputApplier
 * failure:   配置、生成或发布失败时记录错误，正式产物保持或回滚到导出前状态
 ***************************************************************/

using System;
using System.Collections.Generic;
using System.IO;
using NovaFramework.Runtime;
using UnityEditor;
using IOPath = System.IO.Path;

namespace NovaFramework.Editor
{
    public static partial class EditorUtil
    {
        public static partial class Vibrate
        {
            private enum ExportMode
            {
                All,
                Code,
                Data,
            }

            internal sealed class ExportOperations
            {
                internal Func<EditorUtil.Luban.LubanExportContext, bool> ExportAll = EditorUtil.Luban.Pipeline.ExportAll;
                internal Func<EditorUtil.Luban.LubanExportContext, bool> ExportCode = EditorUtil.Luban.Pipeline.ExportCode;
                internal Func<EditorUtil.Luban.LubanExportContext, bool> ExportData = EditorUtil.Luban.Pipeline.ExportData;
                internal Action RefreshAssetDatabase = AssetDatabase.Refresh;
            }

            public static void ExportEmphasisData(string filePath, string dataExportPath, VibrateSettings settings)
            {
                ExportEmphasisData(filePath, dataExportPath, settings, new ExportOperations());
            }

            internal static bool ExportEmphasisData(
                string filePath,
                string dataExportPath,
                VibrateSettings settings,
                ExportOperations operations)
            {
                return ExportData(filePath, dataExportPath, settings, true, operations);
            }

            public static void ExportEmphasisCode(string filePath, string classExportPath, VibrateSettings settings)
            {
                ExportEmphasisCode(filePath, classExportPath, settings, new ExportOperations());
            }

            internal static bool ExportEmphasisCode(
                string filePath,
                string classExportPath,
                VibrateSettings settings,
                ExportOperations operations)
            {
                return ExportCode(filePath, classExportPath, settings, true, operations);
            }

            public static void ExportEmphasisAll(VibrateSettings settings)
            {
                ExportEmphasisAll(settings, new ExportOperations());
            }

            internal static bool ExportEmphasisAll(VibrateSettings settings, ExportOperations operations)
            {
                return ExportAll(settings, true, operations);
            }

            public static void ExportCustomData(string filePath, string dataExportPath, VibrateSettings settings)
            {
                ExportCustomData(filePath, dataExportPath, settings, new ExportOperations());
            }

            internal static bool ExportCustomData(
                string filePath,
                string dataExportPath,
                VibrateSettings settings,
                ExportOperations operations)
            {
                return ExportData(filePath, dataExportPath, settings, false, operations);
            }

            public static void ExportCustomCode(string filePath, string classExportPath, VibrateSettings settings)
            {
                ExportCustomCode(filePath, classExportPath, settings, new ExportOperations());
            }

            internal static bool ExportCustomCode(
                string filePath,
                string classExportPath,
                VibrateSettings settings,
                ExportOperations operations)
            {
                return ExportCode(filePath, classExportPath, settings, false, operations);
            }

            public static void ExportCustomAll(VibrateSettings settings)
            {
                ExportCustomAll(settings, new ExportOperations());
            }

            internal static bool ExportCustomAll(VibrateSettings settings, ExportOperations operations)
            {
                return ExportAll(settings, false, operations);
            }

            internal static bool ExportEmphasisDataAll(VibrateSettings settings, ExportOperations operations = null)
            {
                return ExportArea(settings, true, ExportMode.Data, operations);
            }

            internal static bool ExportEmphasisCodeAll(VibrateSettings settings, ExportOperations operations = null)
            {
                return ExportArea(settings, true, ExportMode.Code, operations);
            }

            internal static bool ExportCustomDataAll(VibrateSettings settings, ExportOperations operations = null)
            {
                return ExportArea(settings, false, ExportMode.Data, operations);
            }

            internal static bool ExportCustomCodeAll(VibrateSettings settings, ExportOperations operations = null)
            {
                return ExportArea(settings, false, ExportMode.Code, operations);
            }

            private static bool ExportArea(
                VibrateSettings settings,
                bool isEmphasis,
                ExportMode mode,
                ExportOperations operations)
            {
                if (!TryResolveArea(settings, isEmphasis, out Area area))
                {
                    return false;
                }
                string classExportPath = mode == ExportMode.Data
                    ? null
                    : ResolveClassExportPath(area.Units, area.Label);
                if (mode != ExportMode.Data && string.IsNullOrWhiteSpace(classExportPath))
                {
                    Log.Error(LogTag.Editor, "Vibrate {0} 类型导出路径不能为空。", area.Label);
                    return false;
                }
                return Execute(area, null, classExportPath, mode, operations ?? new ExportOperations());
            }

            private static bool ExportData(
                string filePath,
                string dataExportPath,
                VibrateSettings settings,
                bool isEmphasis,
                ExportOperations operations)
            {
                if (string.IsNullOrWhiteSpace(dataExportPath) ||
                    !TryResolveArea(settings, isEmphasis, out Area area))
                {
                    return false;
                }

                string relativePath = Util.SysIO.Path.GetRelativePath(area.SourceDirPath.TrimEnd('/', '\\'), filePath);
                VibrateUnitSetting targetUnit = area.Units.Find(unit => unit.SourcePath == relativePath);
                if (targetUnit == null)
                {
                    Log.Error(LogTag.Editor, "未找到文件 {0} 对应的 VibrateUnitSetting。", relativePath);
                    return false;
                }

                return Execute(area, targetUnit, null, ExportMode.Data, operations);
            }

            private static bool ExportCode(
                string filePath,
                string classExportPath,
                VibrateSettings settings,
                bool isEmphasis,
                ExportOperations operations)
            {
                if (string.IsNullOrWhiteSpace(classExportPath) ||
                    !TryResolveArea(settings, isEmphasis, out Area area))
                {
                    return false;
                }

                string relativePath = Util.SysIO.Path.GetRelativePath(area.SourceDirPath.TrimEnd('/', '\\'), filePath);
                VibrateUnitSetting targetUnit = area.Units.Find(unit => unit.SourcePath == relativePath);
                if (targetUnit == null)
                {
                    Log.Error(LogTag.Editor, "未找到文件 {0} 对应的 VibrateUnitSetting。", relativePath);
                    return false;
                }

                return Execute(area, targetUnit, classExportPath, ExportMode.Code, operations);
            }

            private static bool ExportAll(
                VibrateSettings settings,
                bool isEmphasis,
                ExportOperations operations)
            {
                if (!TryResolveArea(settings, isEmphasis, out Area area))
                {
                    return false;
                }

                string classExportPath = ResolveClassExportPath(area.Units, area.Label);
                if (string.IsNullOrWhiteSpace(classExportPath))
                {
                    Log.Error(LogTag.Editor, "Vibrate {0} 类型导出路径不能为空。", area.Label);
                    return false;
                }

                return Execute(area, null, classExportPath, ExportMode.All, operations);
            }

            private static bool Execute(
                Area area,
                VibrateUnitSetting formalTargetUnit,
                string formalClassExportPath,
                ExportMode mode,
                ExportOperations operations)
            {
                operations ??= new ExportOperations();
                string tempRoot = IOPath.GetFullPath(IOPath.Combine(area.SourceDirPath, "_temp"));
                try
                {
                    using IDisposable workspace = EditorUtil.FileSystem.AcquireWorkspace(tempRoot);
                    DeleteTempRoot(tempRoot);
                    using var applier = new EditorUtil.FileSystem.OutputApplier(tempRoot);
                    string stagedCodeDir = IOPath.Combine(applier.StagingRoot, "code~");
                    StagedSettings stagedSettings = CreateStagedSettings(
                        area,
                        applier.StagingRoot,
                        stagedCodeDir,
                        out Dictionary<VibrateUnitSetting, VibrateUnitSetting> stagedUnits);
                    if (mode == ExportMode.Code)
                    {
                        SeedStagedData(area.Units, stagedUnits);
                    }

                    var context = EditorUtil.Luban.ExportHelper.BuildExportContext(
                        area.SourceDirPath,
                        stagedSettings,
                        area.Profile);
                    context.RegionUnits = stagedSettings.Units;
                    context.TargetUnit = formalTargetUnit == null ? null : stagedUnits[formalTargetUnit];
                    if (mode != ExportMode.Data)
                    {
                        context.OutputCodeDir = stagedCodeDir;
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
                        Log.Error(
                            LogTag.Editor,
                            "Vibrate {0} {1}导出失败，正式产物未更新。",
                            area.Label,
                            GetModeLabel(mode));
                        return false;
                    }

                    RegisterOutputs(
                        applier,
                        context,
                        area.Units,
                        stagedUnits,
                        formalTargetUnit,
                        stagedCodeDir,
                        formalClassExportPath,
                        mode);
                    applier.Apply();
                    return true;
                }
                catch (Exception exception)
                {
                    Log.Error(
                        LogTag.Editor,
                        "Vibrate {0} {1}导出失败，正式产物未更新：{2}",
                        area.Label,
                        GetModeLabel(mode),
                        exception);
                    return false;
                }
                finally
                {
                    Cleanup(tempRoot, area.Label, operations);
                }
            }

            private static StagedSettings CreateStagedSettings(
                Area area,
                string stagingRoot,
                string stagedCodeDir,
                out Dictionary<VibrateUnitSetting, VibrateUnitSetting> stagedUnits)
            {
                var units = new List<VibrateUnitSetting>(area.Units.Count);
                stagedUnits = new Dictionary<VibrateUnitSetting, VibrateUnitSetting>();
                for (int i = 0; i < area.Units.Count; i++)
                {
                    VibrateUnitSetting source = area.Units[i];
                    var staged = new VibrateUnitSetting
                    {
                        SourcePath = source.SourcePath,
                        DatasExportPath = GetStagedDataPath(stagingRoot, source.DatasExportPath, i),
                        ClassesExportPath = stagedCodeDir,
                        AssetLocation = source.AssetLocation,
                    };
                    units.Add(staged);
                    stagedUnits.Add(source, staged);
                }
                return new StagedSettings(area.SourceDirPath, units);
            }

            private static string GetStagedDataPath(string stagingRoot, string formalPath, int index)
            {
                string fileName = string.IsNullOrWhiteSpace(formalPath)
                    ? index.ToString("D4") + ".json"
                    : IOPath.GetFileName(formalPath);
                return IOPath.Combine(stagingRoot, "data", index.ToString("D4") + "_" + fileName);
            }

            private static void SeedStagedData(
                IReadOnlyList<VibrateUnitSetting> formalUnits,
                IReadOnlyDictionary<VibrateUnitSetting, VibrateUnitSetting> stagedUnits)
            {
                foreach (VibrateUnitSetting formal in formalUnits)
                {
                    if (string.IsNullOrWhiteSpace(formal.DatasExportPath) || !File.Exists(formal.DatasExportPath))
                    {
                        continue;
                    }
                    string stagedPath = stagedUnits[formal].DatasExportPath;
                    Directory.CreateDirectory(IOPath.GetDirectoryName(stagedPath));
                    File.Copy(formal.DatasExportPath, stagedPath, true);
                }
            }

            private static void RegisterOutputs(
                EditorUtil.FileSystem.OutputApplier applier,
                EditorUtil.Luban.LubanExportContext context,
                IReadOnlyList<VibrateUnitSetting> formalUnits,
                IReadOnlyDictionary<VibrateUnitSetting, VibrateUnitSetting> stagedUnits,
                VibrateUnitSetting formalTargetUnit,
                string stagedCodeDir,
                string formalCodeDir,
                ExportMode mode)
            {
                if (mode != ExportMode.Code)
                {
                    foreach (VibrateUnitSetting formal in formalUnits)
                    {
                        if (formalTargetUnit != null && !ReferenceEquals(formalTargetUnit, formal))
                        {
                            continue;
                        }
                        if (!string.IsNullOrWhiteSpace(formal.DatasExportPath))
                        {
                            applier.AddReplacement(stagedUnits[formal].DatasExportPath, formal.DatasExportPath);
                        }
                    }
                }

                if (mode != ExportMode.Data)
                {
                    RegisterCodeOutputs(
                        applier,
                        context,
                        stagedCodeDir,
                        formalCodeDir,
                        formalTargetUnit == null);
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

            private static bool TryResolveArea(VibrateSettings settings, bool isEmphasis, out Area area)
            {
                area = default;
                if (settings == null)
                {
                    return false;
                }

                string sourceDirPath = isEmphasis ? settings.EmphasisSourceDirPath : settings.CustomSourceDirPath;
                List<VibrateUnitSetting> units = isEmphasis
                    ? settings.EmphasisUnitsSettings
                    : settings.CustomUnitsSettings;
                string label = isEmphasis ? "Emphasis" : "Custom";
                if (string.IsNullOrWhiteSpace(sourceDirPath) || !Directory.Exists(sourceDirPath) ||
                    units == null || units.Count == 0)
                {
                    Log.Error(LogTag.Editor, "Vibrate {0} 导出配置为空或数据源目录不存在。", label);
                    return false;
                }
                foreach (VibrateUnitSetting unit in units)
                {
                    if (unit == null || string.IsNullOrWhiteSpace(unit.SourcePath))
                    {
                        Log.Error(LogTag.Editor, "Vibrate {0} 单元设置包含空项或空 SourcePath。", label);
                        return false;
                    }
                }

                area = new Area(
                    sourceDirPath,
                    units,
                    isEmphasis
                        ? EditorUtil.Luban.LubanExportProfiles.VibrateEmphasis
                        : EditorUtil.Luban.LubanExportProfiles.VibrateCustom,
                    label);
                return true;
            }

            private static string ResolveClassExportPath(
                IReadOnlyList<VibrateUnitSetting> units,
                string label)
            {
                string result = string.Empty;
                var distinctPaths = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
                foreach (VibrateUnitSetting unit in units)
                {
                    if (string.IsNullOrWhiteSpace(unit.ClassesExportPath))
                    {
                        continue;
                    }
                    distinctPaths.Add(IOPath.GetFullPath(unit.ClassesExportPath));
                    if (string.IsNullOrEmpty(result))
                    {
                        result = unit.ClassesExportPath;
                    }
                }
                if (distinctPaths.Count > 1)
                {
                    Log.Warning(LogTag.Editor, "检测到多个不同的 Vibrate {0} 类型导出路径，将沿用首个路径：{1}", label, result);
                }
                return result;
            }

            private static string GetModeLabel(ExportMode mode)
            {
                return mode switch
                {
                    ExportMode.All => "全量",
                    ExportMode.Code => "类型",
                    ExportMode.Data => "数据",
                    _ => string.Empty,
                };
            }

            private static void Cleanup(string tempRoot, string label, ExportOperations operations)
            {
                try
                {
                    DeleteTempRoot(tempRoot);
                }
                catch (Exception exception)
                {
                    Log.Warning(LogTag.Editor, "清理 Vibrate {0} 导出临时目录失败：{1}", label, exception.Message);
                }
                operations.RefreshAssetDatabase?.Invoke();
            }

            private static void DeleteTempRoot(string tempRoot)
            {
                EditorUtil.FileSystem.DeleteUnityTempRoot(tempRoot);
            }

            private readonly struct Area
            {
                internal Area(
                    string sourceDirPath,
                    List<VibrateUnitSetting> units,
                    EditorUtil.Luban.LubanExportProfile profile,
                    string label)
                {
                    SourceDirPath = sourceDirPath;
                    Units = units;
                    Profile = profile;
                    Label = label;
                }

                internal string SourceDirPath { get; }
                internal List<VibrateUnitSetting> Units { get; }
                internal EditorUtil.Luban.LubanExportProfile Profile { get; }
                internal string Label { get; }
            }

            private sealed class StagedSettings : IDataTableSettings
            {
                internal StagedSettings(string sourceDirPath, List<VibrateUnitSetting> units)
                {
                    SourceDirPath = sourceDirPath;
                    Units = units;
                }

                public string SourceDirPath { get; }
                public IReadOnlyList<IDataTableUnitSetting> Units { get; }
            }
        }
    }
}
