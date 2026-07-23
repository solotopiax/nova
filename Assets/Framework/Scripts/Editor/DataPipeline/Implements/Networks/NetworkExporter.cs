/***************************************************************
 * (c) copyright 2026 - 2030, Solotopia
 * All Rights Reserved.
 * -------------------------------------------------------------
 * filename:  NetworkExporter.cs
 * author:    taoye
 * created:   2026/7/21
 * descrip:   Network 专用导出编排：预处理 Excel，暂存 Luban 产物，验证后批量发布
 * input:     HostKeySettings/NetCmdSettings、可选单元目标及 ConfigRuntime DevelopMode
 * output:    HostKeys/NetCmds JSON 与 Luban C# 类型文件
 * boundary:  解释 Network 导出契约；文件替换与失败回滚复用 FileSystem.OutputApplier
 * failure:   配置、预处理、生成或发布失败时返回 false，正式产物保持或回滚到导出前状态
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
    /// Network 导出内部编排器。HostKeys 与 NetCmds 共用暂存发布流程，
    /// 但分别调用各自的 Excel 预处理规则；公共入口仍由 EditorUtil.Network 下的导出器提供。
    /// </summary>
    internal static class NetworkExporter
    {
        internal enum ExportMode
        {
            All,
            Code,
            Data,
        }

        internal sealed class ExportOperations
        {
            internal Func<DevelopMode?> GetDevelopMode = GetCurrentDevelopMode;
            internal Action<string, string, DevelopMode> FilterHostKeys = NetworkExcelPreFilter.FilterHostKeys;
            internal Action<string, string> FilterNetCmds = NetworkExcelPreFilter.FilterNetCmds;
            internal Func<EditorUtil.Luban.LubanExportContext, bool> ExportAll = EditorUtil.Luban.Pipeline.ExportAll;
            internal Func<EditorUtil.Luban.LubanExportContext, bool> ExportCode = EditorUtil.Luban.Pipeline.ExportCode;
            internal Func<EditorUtil.Luban.LubanExportContext, bool> ExportData = EditorUtil.Luban.Pipeline.ExportData;
            internal Action RefreshAssetDatabase = AssetDatabase.Refresh;
        }

        internal static bool ExportHostKeys(
            HostKeySettings settings,
            ExportMode mode,
            HostKeyUnitSetting targetUnit = null,
            ExportOperations operations = null)
        {
            operations ??= new ExportOperations();
            if (!TryValidate(settings, settings?.HostKeyUnits, mode, targetUnit, "HostKey", out string classExportPath))
            {
                return false;
            }

            DevelopMode? developMode = operations.GetDevelopMode?.Invoke();
            if (!developMode.HasValue)
            {
                Log.Error(LogTag.Editor, "HostKeys 导出需要当前 ConfigRuntime。请先导出 Config，再重试 Network 导出。");
                return false;
            }

            return ExecuteHostKeys(settings, mode, targetUnit, classExportPath, developMode.Value, operations);
        }

        internal static bool ExportNetCmds(
            NetCmdSettings settings,
            ExportMode mode,
            NetCmdUnitSetting targetUnit = null,
            ExportOperations operations = null)
        {
            operations ??= new ExportOperations();
            if (!TryValidate(settings, settings?.NetCmdUnits, mode, targetUnit, "NetCmd", out string classExportPath))
            {
                return false;
            }

            return ExecuteNetCmds(settings, mode, targetUnit, classExportPath, operations);
        }

        private static bool ExecuteHostKeys(
            HostKeySettings settings,
            ExportMode mode,
            HostKeyUnitSetting targetUnit,
            string formalClassExportPath,
            DevelopMode developMode,
            ExportOperations operations)
        {
            string tempRoot = IOPath.GetFullPath(IOPath.Combine(settings.SourceDirPath, "_temp"));
            try
            {
                using IDisposable workspace = EditorUtil.FileSystem.AcquireWorkspace(tempRoot);
                DeleteTempRoot(tempRoot);
                using var applier = new EditorUtil.FileSystem.OutputApplier(tempRoot);
                string stagedCodeDir = IOPath.Combine(applier.StagingRoot, "code~");
                HostKeySettings stagedSettings = CreateStagedSettings(
                    settings,
                    applier.StagingRoot,
                    stagedCodeDir,
                    out Dictionary<HostKeyUnitSetting, HostKeyUnitSetting> stagedUnits);
                if (mode == ExportMode.Code)
                {
                    SeedStagedData(settings.HostKeyUnits, stagedUnits);
                }

                operations.FilterHostKeys(settings.SourceDirPath, tempRoot, developMode);
                HostKeyUnitSetting stagedTarget = targetUnit == null ? null : stagedUnits[targetUnit];
                var context = EditorUtil.Luban.ExportHelper.BuildExportContext(
                    settings.SourceDirPath,
                    stagedSettings,
                    EditorUtil.Luban.LubanExportProfiles.NetworkHostKey);
                context.RegionUnits = stagedSettings.HostKeyUnits;
                context.TargetUnit = stagedTarget;
                context.SchemaValueTypeScanner = CreateProjectedValueTypeScanner(tempRoot);
                if (mode != ExportMode.Data)
                {
                    context.OutputCodeDir = stagedCodeDir;
                }

                if (!RunPipeline(context, mode, operations))
                {
                    return false;
                }

                RegisterOutputs(
                    applier,
                    context,
                    settings.HostKeyUnits,
                    stagedUnits,
                    targetUnit,
                    formalClassExportPath,
                    stagedCodeDir,
                    mode);
                applier.Apply();
                return true;
            }
            catch (Exception exception)
            {
                Log.Error(LogTag.Editor, "HostKeys 导出失败，正式产物未更新：{0}", exception);
                return false;
            }
            finally
            {
                Cleanup(tempRoot, operations);
            }
        }

        private static bool ExecuteNetCmds(
            NetCmdSettings settings,
            ExportMode mode,
            NetCmdUnitSetting targetUnit,
            string formalClassExportPath,
            ExportOperations operations)
        {
            string tempRoot = IOPath.GetFullPath(IOPath.Combine(settings.SourceDirPath, "_temp"));
            try
            {
                using IDisposable workspace = EditorUtil.FileSystem.AcquireWorkspace(tempRoot);
                DeleteTempRoot(tempRoot);
                using var applier = new EditorUtil.FileSystem.OutputApplier(tempRoot);
                string stagedCodeDir = IOPath.Combine(applier.StagingRoot, "code~");
                NetCmdSettings stagedSettings = CreateStagedSettings(
                    settings,
                    applier.StagingRoot,
                    stagedCodeDir,
                    out Dictionary<NetCmdUnitSetting, NetCmdUnitSetting> stagedUnits);
                if (mode == ExportMode.Code)
                {
                    SeedStagedData(settings.NetCmdUnits, stagedUnits);
                }

                operations.FilterNetCmds(settings.SourceDirPath, tempRoot);
                NetCmdUnitSetting stagedTarget = targetUnit == null ? null : stagedUnits[targetUnit];
                var context = EditorUtil.Luban.ExportHelper.BuildExportContext(
                    settings.SourceDirPath,
                    stagedSettings,
                    EditorUtil.Luban.LubanExportProfiles.NetworkCmd);
                context.RegionUnits = stagedSettings.NetCmdUnits;
                context.TargetUnit = stagedTarget;
                context.SchemaValueTypeScanner = CreateProjectedValueTypeScanner(tempRoot);
                if (mode != ExportMode.Data)
                {
                    context.OutputCodeDir = stagedCodeDir;
                }

                if (!RunPipeline(context, mode, operations))
                {
                    return false;
                }

                RegisterOutputs(
                    applier,
                    context,
                    settings.NetCmdUnits,
                    stagedUnits,
                    targetUnit,
                    formalClassExportPath,
                    stagedCodeDir,
                    mode);
                applier.Apply();
                return true;
            }
            catch (Exception exception)
            {
                Log.Error(LogTag.Editor, "NetCmds 导出失败，正式产物未更新：{0}", exception);
                return false;
            }
            finally
            {
                Cleanup(tempRoot, operations);
            }
        }

        private static bool RunPipeline(
            EditorUtil.Luban.LubanExportContext context,
            ExportMode mode,
            ExportOperations operations)
        {
            return mode switch
            {
                ExportMode.All => operations.ExportAll(context),
                ExportMode.Code => operations.ExportCode(context),
                ExportMode.Data => operations.ExportData(context),
                _ => false,
            };
        }

        private static HostKeySettings CreateStagedSettings(
            HostKeySettings settings,
            string stagingRoot,
            string stagedCodeDir,
            out Dictionary<HostKeyUnitSetting, HostKeyUnitSetting> stagedUnits)
        {
            var result = new HostKeySettings
            {
                SourceDirPath = settings.SourceDirPath,
                DatasExportPath = settings.DatasExportPath,
                ClassesExportPath = stagedCodeDir,
            };
            stagedUnits = new Dictionary<HostKeyUnitSetting, HostKeyUnitSetting>();
            for (int i = 0; i < settings.HostKeyUnits.Count; i++)
            {
                HostKeyUnitSetting source = settings.HostKeyUnits[i];
                var staged = new HostKeyUnitSetting
                {
                    SourcePath = source.SourcePath,
                    DatasExportPath = GetStagedDataPath(stagingRoot, source.DatasExportPath, i),
                    ClassesExportPath = stagedCodeDir,
                    AssetLocation = source.AssetLocation,
                };
                result.HostKeyUnits.Add(staged);
                stagedUnits.Add(source, staged);
            }
            return result;
        }

        private static NetCmdSettings CreateStagedSettings(
            NetCmdSettings settings,
            string stagingRoot,
            string stagedCodeDir,
            out Dictionary<NetCmdUnitSetting, NetCmdUnitSetting> stagedUnits)
        {
            var result = new NetCmdSettings
            {
                SourceDirPath = settings.SourceDirPath,
                DatasExportPath = settings.DatasExportPath,
                ClassesExportPath = stagedCodeDir,
            };
            stagedUnits = new Dictionary<NetCmdUnitSetting, NetCmdUnitSetting>();
            for (int i = 0; i < settings.NetCmdUnits.Count; i++)
            {
                NetCmdUnitSetting source = settings.NetCmdUnits[i];
                var staged = new NetCmdUnitSetting
                {
                    SourcePath = source.SourcePath,
                    DatasExportPath = GetStagedDataPath(stagingRoot, source.DatasExportPath, i),
                    ClassesExportPath = stagedCodeDir,
                    AssetLocation = source.AssetLocation,
                };
                result.NetCmdUnits.Add(staged);
                stagedUnits.Add(source, staged);
            }
            return result;
        }

        private static string GetStagedDataPath(string stagingRoot, string formalPath, int index)
        {
            string fileName = string.IsNullOrWhiteSpace(formalPath)
                ? index.ToString("D4") + ".json"
                : IOPath.GetFileName(formalPath);
            return IOPath.Combine(stagingRoot, "data", index.ToString("D4") + "_" + fileName);
        }

        private static void SeedStagedData<TUnit>(
            IReadOnlyList<TUnit> formalUnits,
            IReadOnlyDictionary<TUnit, TUnit> stagedUnits)
            where TUnit : DataTableUnitSettingBase
        {
            foreach (TUnit formal in formalUnits)
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

        private static void RegisterOutputs<TUnit>(
            EditorUtil.FileSystem.OutputApplier applier,
            EditorUtil.Luban.LubanExportContext context,
            IReadOnlyList<TUnit> formalUnits,
            IReadOnlyDictionary<TUnit, TUnit> stagedUnits,
            TUnit formalTarget,
            string formalClassExportPath,
            string stagedCodeDir,
            ExportMode mode)
            where TUnit : DataTableUnitSettingBase
        {
            if (mode != ExportMode.Code)
            {
                foreach (TUnit formal in formalUnits)
                {
                    if (formalTarget != null && !ReferenceEquals(formalTarget, formal))
                    {
                        continue;
                    }
                    if (string.IsNullOrWhiteSpace(formal.DatasExportPath))
                    {
                        continue;
                    }
                    applier.AddReplacement(stagedUnits[formal].DatasExportPath, formal.DatasExportPath);
                }
            }

            if (mode != ExportMode.Data)
            {
                RegisterCodeOutputs(applier, context, stagedCodeDir, formalClassExportPath);
            }
        }

        private static void RegisterCodeOutputs(
            EditorUtil.FileSystem.OutputApplier applier,
            EditorUtil.Luban.LubanExportContext context,
            string stagedCodeDir,
            string formalCodeDir)
        {
            EditorUtil.Luban.GeneratedOutput.RegisterCodeOutputs(
                applier,
                context,
                stagedCodeDir,
                formalCodeDir,
                context.TargetUnit == null);
        }

        private static Func<string, int, IReadOnlyList<string>> CreateProjectedValueTypeScanner(string tempRoot)
        {
            return (sourceFilePath, _) =>
            {
                string inputDir = IOPath.Combine(tempRoot, IOPath.GetFileNameWithoutExtension(sourceFilePath));
                if (!Directory.Exists(inputDir))
                {
                    throw new DirectoryNotFoundException($"Network Luban 临时输入目录不存在：{inputDir}");
                }

                string[] csvPaths = Directory.GetFiles(inputDir, "*.csv", SearchOption.TopDirectoryOnly);
                Array.Sort(csvPaths, StringComparer.Ordinal);
                if (csvPaths.Length == 0)
                {
                    throw new InvalidDataException($"Network Luban 临时输入目录没有 CSV：{inputDir}");
                }

                var valueTypes = new List<string>(csvPaths.Length);
                foreach (string csvPath in csvPaths)
                {
                    valueTypes.Add(IOPath.GetFileNameWithoutExtension(csvPath));
                }
                return valueTypes;
            };
        }

        private static bool TryValidate<TSettings, TUnit>(
            TSettings settings,
            IReadOnlyList<TUnit> units,
            ExportMode mode,
            TUnit targetUnit,
            string label,
            out string classExportPath)
            where TSettings : class, IDataTableSettings
            where TUnit : DataTableUnitSettingBase
        {
            classExportPath = string.Empty;
            if (settings == null || string.IsNullOrWhiteSpace(settings.SourceDirPath) || !Directory.Exists(settings.SourceDirPath))
            {
                Log.Error(LogTag.Editor, "{0} 数据源目录不存在或配置为空。", label);
                return false;
            }
            if (units == null || units.Count == 0)
            {
                Log.Error(LogTag.Editor, "{0} 单元设置为空，无法导出。", label);
                return false;
            }
            if (targetUnit != null && !ContainsReference(units, targetUnit))
            {
                Log.Error(LogTag.Editor, "{0} 单文件导出目标不属于当前设置。", label);
                return false;
            }

            var classPaths = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            foreach (TUnit unit in units)
            {
                if (unit == null || string.IsNullOrWhiteSpace(unit.SourcePath))
                {
                    Log.Error(LogTag.Editor, "{0} 单元设置包含空项或空 SourcePath。", label);
                    return false;
                }
                if (mode != ExportMode.Code && string.IsNullOrWhiteSpace(unit.DatasExportPath))
                {
                    Log.Error(LogTag.Editor, "{0} 单元 {1} 的数据导出路径不能为空。", label, unit.SourcePath);
                    return false;
                }
                if (!string.IsNullOrWhiteSpace(unit.ClassesExportPath))
                {
                    classPaths.Add(IOPath.GetFullPath(unit.ClassesExportPath));
                    classExportPath = unit.ClassesExportPath;
                }
            }

            if (mode != ExportMode.Data && classPaths.Count != 1)
            {
                Log.Error(LogTag.Editor, "{0} 类型导出路径必须统一且不能为空。", label);
                return false;
            }
            return true;
        }

        private static bool ContainsReference<T>(IReadOnlyList<T> values, T target) where T : class
        {
            for (int i = 0; i < values.Count; i++)
            {
                if (ReferenceEquals(values[i], target))
                {
                    return true;
                }
            }
            return false;
        }

        private static DevelopMode? GetCurrentDevelopMode()
        {
            ConfigRuntimeSO runtime = EditorUtil.Config.RuntimeProvider.GetCurrent();
            return runtime == null ? (DevelopMode?)null : runtime.DevelopMode;
        }

        private static void Cleanup(string tempRoot, ExportOperations operations)
        {
            try
            {
                DeleteTempRoot(tempRoot);
            }
            catch (Exception exception)
            {
                Log.Warning(LogTag.Editor, "清理 Network 导出临时目录失败：{0}", exception.Message);
            }
            operations.RefreshAssetDatabase?.Invoke();
        }

        private static void DeleteTempRoot(string tempRoot)
        {
            EditorUtil.FileSystem.DeleteUnityTempRoot(tempRoot);
        }
    }
}
