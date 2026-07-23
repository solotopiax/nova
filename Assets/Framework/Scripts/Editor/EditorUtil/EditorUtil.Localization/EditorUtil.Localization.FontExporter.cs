/***************************************************************
 * (c) copyright 2026 - 2030, Solotopia
 * All Rights Reserved.
 * -------------------------------------------------------------
 * filename:  EditorUtil.Localization.FontExporter.cs
 * author:    taoye
 * created:   2026/5/11
 * descrip:   本地化字体导出工具（字体配置 Pipeline 编排）
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
        public static partial class Localization
        {
            /// <summary>
            /// 本地化字体导出工具。
            /// 提供全链路导出（代码+数据）、仅代码导出和仅数据导出三条路径，
            /// 通过标准 Luban Pipeline 实现，不需要 PreFilter 多语言处理。
            /// </summary>
            public static class FontExporter
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
                public static bool ExportFontAll(LocalizationSettings settings, string sourceDirPath, string classExportPath)
                {
                    return ExportFontAll(settings, sourceDirPath, classExportPath, new ExportOperations());
                }

                internal static bool ExportFontAll(
                    LocalizationSettings settings,
                    string sourceDirPath,
                    string classExportPath,
                    ExportOperations operations)
                {
                    ExportMode mode = string.IsNullOrWhiteSpace(classExportPath) ? ExportMode.Data : ExportMode.All;
                    return Execute(settings, sourceDirPath, classExportPath, mode, operations);
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
                    return ExportFontCode(settings, sourceDirPath, classExportPath, new ExportOperations());
                }

                internal static bool ExportFontCode(
                    LocalizationSettings settings,
                    string sourceDirPath,
                    string classExportPath,
                    ExportOperations operations)
                {
                    return Execute(settings, sourceDirPath, classExportPath, ExportMode.Code, operations);
                }

                /// <summary>
                /// 仅导出字体数据：构建上下文 → Pipeline.ExportData。
                /// </summary>
                /// <param name="settings">本地化设置实例。</param>
                /// <param name="sourceDirPath">字体数据源目录路径。</param>
                /// <returns>是否导出成功。</returns>
                public static bool ExportFontData(LocalizationSettings settings, string sourceDirPath)
                {
                    return ExportFontData(settings, sourceDirPath, new ExportOperations());
                }

                internal static bool ExportFontData(
                    LocalizationSettings settings,
                    string sourceDirPath,
                    ExportOperations operations)
                {
                    return Execute(settings, sourceDirPath, null, ExportMode.Data, operations);
                }

                private static bool Execute(
                    LocalizationSettings settings,
                    string sourceDirPath,
                    string formalCodeDir,
                    ExportMode mode,
                    ExportOperations operations)
                {
                    if (!TryValidate(settings, sourceDirPath, formalCodeDir, mode) || operations == null)
                    {
                        return false;
                    }

                    string tempRoot = IOPath.GetFullPath(IOPath.Combine(sourceDirPath, "_temp"));
                    try
                    {
                        using IDisposable workspace = EditorUtil.FileSystem.AcquireWorkspace(tempRoot);
                        EditorUtil.FileSystem.DeleteUnityTempRoot(tempRoot);
                        using var applier = new EditorUtil.FileSystem.OutputApplier(tempRoot);
                        string stagedCodeDir = IOPath.Combine(applier.StagingRoot, "code~");
                        List<LocalizationFontUnitSetting> stagedUnits = CreateStagedUnits(
                            settings.FontUnitsSettings,
                            applier.StagingRoot,
                            stagedCodeDir);
                        if (mode == ExportMode.Code)
                        {
                            SeedStagedData(settings.FontUnitsSettings, stagedUnits);
                        }

                        var adapter = new DataTableSettingsAdapter<LocalizationFontUnitSetting>(sourceDirPath, stagedUnits);
                        EditorUtil.Luban.LubanExportContext context = EditorUtil.Luban.ExportHelper.BuildExportContext(
                            sourceDirPath,
                            adapter,
                            EditorUtil.Luban.LubanExportProfiles.LocalizationFont);
                        if (mode != ExportMode.Data)
                        {
                            context.OutputCodeDir = stagedCodeDir;
                        }

                        bool success = mode switch
                        {
                            ExportMode.All => operations.ExportAll(context),
                            ExportMode.Code => operations.ExportCode(context),
                            ExportMode.Data => operations.ExportData(context),
                            _ => false,
                        };
                        if (!success)
                        {
                            return false;
                        }

                        if (mode != ExportMode.Code)
                        {
                            for (int i = 0; i < settings.FontUnitsSettings.Count; i++)
                            {
                                applier.AddReplacement(
                                    stagedUnits[i].DatasExportPath,
                                    settings.FontUnitsSettings[i].DatasExportPath);
                            }
                        }
                        if (mode != ExportMode.Data)
                        {
                            EditorUtil.Luban.GeneratedOutput.RegisterCodeOutputs(
                                applier,
                                context,
                                stagedCodeDir,
                                formalCodeDir,
                                true);
                        }
                        applier.Apply();
                        return true;
                    }
                    catch (Exception exception)
                    {
                        Log.Error(LogTag.Localization, "本地化字体导出失败，正式产物未更新：{0}", exception);
                        return false;
                    }
                    finally
                    {
                        try
                        {
                            EditorUtil.FileSystem.DeleteUnityTempRoot(tempRoot);
                        }
                        catch (Exception exception)
                        {
                            Log.Warning(LogTag.Localization, "清理本地化字体临时目录失败：{0}", exception.Message);
                        }
                        operations?.RefreshAssetDatabase?.Invoke();
                    }
                }

                private static List<LocalizationFontUnitSetting> CreateStagedUnits(
                    IReadOnlyList<LocalizationFontUnitSetting> formalUnits,
                    string stagingRoot,
                    string stagedCodeDir)
                {
                    var result = new List<LocalizationFontUnitSetting>(formalUnits.Count);
                    for (int i = 0; i < formalUnits.Count; i++)
                    {
                        LocalizationFontUnitSetting formal = formalUnits[i];
                        string fileName = string.IsNullOrWhiteSpace(formal.DatasExportPath)
                            ? i.ToString("D4") + ".json"
                            : IOPath.GetFileName(formal.DatasExportPath);
                        result.Add(new LocalizationFontUnitSetting
                        {
                            SourcePath = formal.SourcePath,
                            DatasExportPath = IOPath.Combine(stagingRoot, "data", i.ToString("D4") + "_" + fileName),
                            ClassesExportPath = stagedCodeDir,
                            AssetLocation = formal.AssetLocation,
                        });
                    }
                    return result;
                }

                private static void SeedStagedData(
                    IReadOnlyList<LocalizationFontUnitSetting> formalUnits,
                    IReadOnlyList<LocalizationFontUnitSetting> stagedUnits)
                {
                    for (int i = 0; i < formalUnits.Count; i++)
                    {
                        if (string.IsNullOrWhiteSpace(formalUnits[i].DatasExportPath) ||
                            !File.Exists(formalUnits[i].DatasExportPath))
                        {
                            continue;
                        }
                        Directory.CreateDirectory(IOPath.GetDirectoryName(stagedUnits[i].DatasExportPath));
                        File.Copy(formalUnits[i].DatasExportPath, stagedUnits[i].DatasExportPath, true);
                    }
                }

                private static bool TryValidate(
                    LocalizationSettings settings,
                    string sourceDirPath,
                    string classExportPath,
                    ExportMode mode)
                {
                    if (settings?.FontUnitsSettings == null || settings.FontUnitsSettings.Count == 0 ||
                        string.IsNullOrWhiteSpace(sourceDirPath) || !Directory.Exists(sourceDirPath))
                    {
                        Log.Warning(LogTag.Localization, "字体导出配置为空或数据源目录不存在，导出已跳过。");
                        return false;
                    }
                    if (mode != ExportMode.Data && string.IsNullOrWhiteSpace(classExportPath))
                    {
                        Log.Warning(LogTag.Localization, "字体类型导出路径为空，导出已跳过。");
                        return false;
                    }
                    foreach (LocalizationFontUnitSetting unit in settings.FontUnitsSettings)
                    {
                        if (unit == null || string.IsNullOrWhiteSpace(unit.SourcePath) ||
                            (mode != ExportMode.Code && string.IsNullOrWhiteSpace(unit.DatasExportPath)))
                        {
                            Log.Warning(LogTag.Localization, "字体单元设置不完整，导出已跳过。");
                            return false;
                        }
                    }
                    return true;
                }
            }
        }
    }
}
