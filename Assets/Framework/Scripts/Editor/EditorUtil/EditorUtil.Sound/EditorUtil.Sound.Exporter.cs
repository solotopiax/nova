/***************************************************************
 * (c) copyright 2026 - 2030, Solotopia
 * All Rights Reserved.
 * -------------------------------------------------------------
 * filename:  EditorUtil.Sound.Exporter.cs
 * author:    taoye
 * created:   2026/5/11
 * descrip:   Sound 专用导出编排：暂存 Luban 数据与类型，验证后批量发布
 * input:     SoundSettings、源目录及可选的单文件目标
 * output:    Sound JSON 与 Luban C# 类型文件
 * boundary:  保持 Sound 公共 API；文件替换与失败回滚复用 FileSystem.OutputApplier
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
        public static partial class Sound
        {
            /// <summary>
            /// Sound 模块公共导出入口。公开签名保持不变，所有导出先写入源目录下的
            /// <c>_temp/_publish</c>，验证完整后再一次性应用到正式路径。
            /// </summary>
            public static class Exporter
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

                public static void ExportAll(string sourceDirPath, SoundSettings settings)
                {
                    ExportAll(sourceDirPath, settings, new ExportOperations());
                }

                internal static bool ExportAll(
                    string sourceDirPath,
                    SoundSettings settings,
                    ExportOperations operations)
                {
                    if (!TryValidate(sourceDirPath, settings, ExportMode.All, null, out string classExportPath))
                    {
                        return false;
                    }
                    return Execute(
                        sourceDirPath,
                        settings,
                        null,
                        classExportPath,
                        null,
                        ExportMode.All,
                        operations);
                }

                public static void ExportData(
                    string sourceDirPath,
                    SoundSettings settings,
                    SoundUnitSetting unitSetting)
                {
                    ExportData(sourceDirPath, settings, unitSetting, new ExportOperations());
                }

                internal static bool ExportData(
                    string sourceDirPath,
                    SoundSettings settings,
                    SoundUnitSetting unitSetting,
                    ExportOperations operations)
                {
                    if (!TryValidate(sourceDirPath, settings, ExportMode.Data, unitSetting, out _))
                    {
                        return false;
                    }
                    return Execute(
                        sourceDirPath,
                        settings,
                        unitSetting,
                        null,
                        null,
                        ExportMode.Data,
                        operations);
                }

                internal static bool ExportAllData(
                    string sourceDirPath,
                    SoundSettings settings,
                    ExportOperations operations = null)
                {
                    if (!TryValidate(sourceDirPath, settings, ExportMode.Data, null, out _))
                    {
                        return false;
                    }
                    return Execute(
                        sourceDirPath,
                        settings,
                        null,
                        null,
                        null,
                        ExportMode.Data,
                        operations ?? new ExportOperations());
                }

                internal static bool ExportAllCode(
                    string sourceDirPath,
                    SoundSettings settings,
                    ExportOperations operations = null)
                {
                    if (!TryValidate(sourceDirPath, settings, ExportMode.Code, null, out string classExportPath))
                    {
                        return false;
                    }
                    return Execute(
                        sourceDirPath,
                        settings,
                        null,
                        classExportPath,
                        null,
                        ExportMode.Code,
                        operations ?? new ExportOperations());
                }

                public static void ExportCode(
                    string sourceDirPath,
                    SoundSettings settings,
                    SoundUnitSetting unitSetting,
                    string classExportPath,
                    HashSet<string> relevantFileNames)
                {
                    ExportCode(
                        sourceDirPath,
                        settings,
                        unitSetting,
                        classExportPath,
                        relevantFileNames,
                        new ExportOperations());
                }

                internal static bool ExportCode(
                    string sourceDirPath,
                    SoundSettings settings,
                    SoundUnitSetting unitSetting,
                    string classExportPath,
                    HashSet<string> relevantFileNames,
                    ExportOperations operations)
                {
                    if (!TryValidate(sourceDirPath, settings, ExportMode.Code, unitSetting, out string configuredClassPath))
                    {
                        return false;
                    }

                    string formalClassPath = string.IsNullOrWhiteSpace(classExportPath)
                        ? configuredClassPath
                        : classExportPath;
                    if (string.IsNullOrWhiteSpace(formalClassPath))
                    {
                        Log.Error(LogTag.Editor, "Sound 类型导出路径不能为空。");
                        return false;
                    }

                    return Execute(
                        sourceDirPath,
                        settings,
                        unitSetting,
                        formalClassPath,
                        relevantFileNames,
                        ExportMode.Code,
                        operations);
                }

                private static bool Execute(
                    string sourceDirPath,
                    SoundSettings settings,
                    SoundUnitSetting formalTargetUnit,
                    string formalClassExportPath,
                    HashSet<string> relevantFileNames,
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
                        SoundSettings stagedSettings = CreateStagedSettings(
                            settings,
                            sourceDirPath,
                            applier.StagingRoot,
                            stagedCodeDir,
                            out Dictionary<SoundUnitSetting, SoundUnitSetting> stagedUnits);
                        if (mode == ExportMode.Code)
                        {
                            SeedStagedData(settings.SoundUnitsSettings, stagedUnits);
                        }

                        var context = EditorUtil.Luban.ExportHelper.BuildExportContext(
                            sourceDirPath,
                            stagedSettings,
                            EditorUtil.Luban.LubanExportProfiles.Sound);
                        context.DataFormat = settings.DataFormat;
                        context.RegionUnits = stagedSettings.SoundUnitsSettings;
                        context.TargetUnit = formalTargetUnit == null ? null : stagedUnits[formalTargetUnit];
                        context.RelevantFileNames = relevantFileNames;
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
                            Log.Error(LogTag.Editor, "Sound {0}导出失败，正式产物未更新。", GetModeLabel(mode));
                            return false;
                        }

                        RegisterOutputs(
                            applier,
                            context,
                            settings.SoundUnitsSettings,
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
                        Log.Error(LogTag.Editor, "Sound {0}导出失败，正式产物未更新：{1}", GetModeLabel(mode), exception);
                        return false;
                    }
                    finally
                    {
                        Cleanup(tempRoot, operations);
                    }
                }

                private static SoundSettings CreateStagedSettings(
                    SoundSettings settings,
                    string sourceDirPath,
                    string stagingRoot,
                    string stagedCodeDir,
                    out Dictionary<SoundUnitSetting, SoundUnitSetting> stagedUnits)
                {
                    var result = new SoundSettings
                    {
                        DataFormat = settings.DataFormat,
                        SourceDirPath = sourceDirPath,
                        TemplatePath = settings.TemplatePath,
                    };
                    stagedUnits = new Dictionary<SoundUnitSetting, SoundUnitSetting>();
                    for (int i = 0; i < settings.SoundUnitsSettings.Count; i++)
                    {
                        SoundUnitSetting source = settings.SoundUnitsSettings[i];
                        var staged = new SoundUnitSetting
                        {
                            SourcePath = source.SourcePath,
                            DatasExportPath = GetStagedDataPath(stagingRoot, i, settings.DataFormat),
                            ClassesExportPath = stagedCodeDir,
                            AssetLocation = source.AssetLocation,
                        };
                        result.SoundUnitsSettings.Add(staged);
                        stagedUnits.Add(source, staged);
                    }
                    return result;
                }

                /// <summary>
                /// 为当前格式构造声音数据暂存路径。
                /// </summary>
                /// <param name="stagingRoot">输出事务暂存根目录。</param>
                /// <param name="index">单元序号。</param>
                /// <param name="dataFormat">Luban 数据格式。</param>
                /// <returns>带格式对应后缀的暂存路径。</returns>
                private static string GetStagedDataPath(
                    string stagingRoot,
                    int index,
                    LubanDataFormat dataFormat)
                {
                    string fileName = index.ToString("D4") +
                                      (dataFormat == LubanDataFormat.Binary ? ".bytes" : ".json");
                    return IOPath.Combine(stagingRoot, "data", index.ToString("D4") + "_" + fileName);
                }

                private static void SeedStagedData(
                    IReadOnlyList<SoundUnitSetting> formalUnits,
                    IReadOnlyDictionary<SoundUnitSetting, SoundUnitSetting> stagedUnits)
                {
                    foreach (SoundUnitSetting formal in formalUnits)
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
                    IReadOnlyList<SoundUnitSetting> formalUnits,
                    IReadOnlyDictionary<SoundUnitSetting, SoundUnitSetting> stagedUnits,
                    SoundUnitSetting formalTargetUnit,
                    string stagedCodeDir,
                    string formalCodeDir,
                    ExportMode mode)
                {
                    if (mode != ExportMode.Code)
                    {
                        foreach (SoundUnitSetting formal in formalUnits)
                        {
                            if (formalTargetUnit != null && !ReferenceEquals(formalTargetUnit, formal))
                            {
                                continue;
                            }
                            if (!string.IsNullOrWhiteSpace(formal.DatasExportPath))
                            {
                                applier.AddReplacement(stagedUnits[formal].DatasExportPath, formal.DatasExportPath);
                                EditorUtil.Luban.DataArtifact.RegisterCounterpartDeletion(
                                    applier, formal.DatasExportPath, context.DataFormat);
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

                private static bool TryValidate(
                    string sourceDirPath,
                    SoundSettings settings,
                    ExportMode mode,
                    SoundUnitSetting targetUnit,
                    out string classExportPath)
                {
                    classExportPath = string.Empty;
                    if (settings == null || string.IsNullOrWhiteSpace(sourceDirPath) || !Directory.Exists(sourceDirPath) ||
                        settings.SoundUnitsSettings == null || settings.SoundUnitsSettings.Count == 0)
                    {
                        Log.Error(LogTag.Editor, "Sound 导出配置为空或数据源目录不存在。");
                        return false;
                    }
                    if (targetUnit != null && !ContainsReference(settings.SoundUnitsSettings, targetUnit))
                    {
                        Log.Error(LogTag.Editor, "Sound 单文件导出目标不属于当前设置。");
                        return false;
                    }
                    var classPaths = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
                    foreach (SoundUnitSetting unit in settings.SoundUnitsSettings)
                    {
                        if (unit == null || string.IsNullOrWhiteSpace(unit.SourcePath))
                        {
                            Log.Error(LogTag.Editor, "SoundUnitsSettings 包含空单元或空 SourcePath。");
                            return false;
                        }
                        if (!string.IsNullOrWhiteSpace(unit.ClassesExportPath))
                        {
                            classPaths.Add(IOPath.GetFullPath(unit.ClassesExportPath));
                            if (string.IsNullOrEmpty(classExportPath))
                            {
                                classExportPath = unit.ClassesExportPath;
                            }
                        }
                    }

                    if (mode != ExportMode.Data && string.IsNullOrWhiteSpace(classExportPath))
                    {
                        Log.Error(LogTag.Editor, "Sound 类型导出路径不能为空。");
                        return false;
                    }
                    if (classPaths.Count > 1)
                    {
                        Log.Warning(LogTag.Editor, "检测到多个不同的 Sound 类型导出路径，将沿用首个路径：{0}", classExportPath);
                    }
                    return true;
                }

                private static bool ContainsReference(
                    IReadOnlyList<SoundUnitSetting> units,
                    SoundUnitSetting target)
                {
                    for (int i = 0; i < units.Count; i++)
                    {
                        if (ReferenceEquals(units[i], target))
                        {
                            return true;
                        }
                    }
                    return false;
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

                private static void Cleanup(string tempRoot, ExportOperations operations)
                {
                    try
                    {
                        DeleteTempRoot(tempRoot);
                    }
                    catch (Exception exception)
                    {
                        Log.Warning(LogTag.Editor, "清理 Sound 导出临时目录失败：{0}", exception.Message);
                    }
                    operations.RefreshAssetDatabase?.Invoke();
                }

                private static void DeleteTempRoot(string tempRoot)
                {
                    EditorUtil.FileSystem.DeleteUnityTempRoot(tempRoot);
                }
            }
        }
    }
}
