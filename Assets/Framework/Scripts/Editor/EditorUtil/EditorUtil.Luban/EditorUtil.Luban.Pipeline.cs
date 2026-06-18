/***************************************************************
 * (c) copyright 2026 - 2030, Solotopia
 * All Rights Reserved.
 * -------------------------------------------------------------
 * filename:  EditorUtil.Luban.Pipeline.cs
 * author:    taoye
 * created:   2026/4/16
 * descrip:   Luban 导出流水线（通用版）
 ***************************************************************/

using System;
using System.Collections.Generic;
using NovaFramework.Runtime;
using UnityEditor;
using UnityEditor.Compilation;

namespace NovaFramework.Editor
{
    public static partial class EditorUtil
    {
        public static partial class Luban
        {
            /// <summary>
            /// Luban 导出上下文，封装单次导出操作所需的全部参数。
            /// </summary>
            public class LubanExportContext
            {
                /// <summary>
                /// 数据源根目录路径。
                /// </summary>
                public string SourceDirPath;

                /// <summary>
                /// luban.conf 文件路径。
                /// </summary>
                public string ConfPath;

                /// <summary>
                /// Luban target 名称（如 "table" / "config"）。
                /// </summary>
                public string TargetName;

                /// <summary>
                /// Luban manager 类名（如 "TableTables" / "ConfigTables"）。
                /// </summary>
                public string ManagerName;

                /// <summary>
                /// 顶层命名空间（如 "Game.Runtime"）。
                /// </summary>
                public string TopModule;

                /// <summary>
                /// 代码输出目录。
                /// </summary>
                public string OutputCodeDir;

                /// <summary>
                /// 数据输出目录（Luban 临时输出，非最终导出路径）。
                /// </summary>
                public string OutputDataDir;

                /// <summary>
                /// 自定义模板目录列表（可为 null，使用内置模板）。按优先级排序，Luban 依次查找。
                /// </summary>
                public string[] CustomTemplateDirs;

                /// <summary>
                /// __tables__.xml 文件路径。
                /// </summary>
                public string TablesXmlPath;

                /// <summary>
                /// 数据表设置（通过 IDataTableSettings 统一消费）。
                /// </summary>
                public IDataTableSettings Settings;

                /// <summary>
                /// 仅打印这些文件名的日志（单文件导出时使用，为 null 时打印全部）。
                /// </summary>
                public HashSet<string> RelevantFileNames;

                /// <summary>
                /// 当前地域的单元设置列表（按地域区分时由调用方填入，为 null 时回退到 Settings.Units）。
                /// </summary>
                public IReadOnlyList<IDataTableUnitSetting> RegionUnits;

                /// <summary>
                /// 目标单元设置（单文件导出时指定，为 null 时处理全部）。
                /// </summary>
                public IDataTableUnitSetting TargetUnit;

                /// <summary>
                /// 获取有效的单元设置列表（优先使用 RegionUnits，回退到 Settings.Units）。
                /// </summary>
                public IReadOnlyList<IDataTableUnitSetting> EffectiveUnits => RegionUnits ?? Settings?.Units;
            }

            /// <summary>
            /// Luban 导出流水线，统一编排 Sync → CLI → Merge → MapPropGen 流程。
            /// <para>Table/Config 模块共用，通过 LubanExportContext 参数化差异。</para>
            /// </summary>
            public static class Pipeline
            {
                /// <summary>
                /// 导出数据（单文件或全部）：同步配置 → 调用 Luban CLI 导出数据 → 合并 JSON。
                /// </summary>
                /// <param name="ctx">导出上下文。</param>
                /// <returns>是否成功。</returns>
                public static bool ExportData(LubanExportContext ctx)
                {
                    EditorUtil.Environment.LubanChecker.EnvironmentCheckResult envResult = EditorUtil.Environment.LubanChecker.Check();
                    if (!envResult.IsReady)
                    {
                        ConfigWindow.OpenLubanSection(envResult);
                        return false;
                    }

                    ConfigSyncer.SyncFromInspector(ctx.SourceDirPath, ctx.Settings, ctx.TargetName, ctx.ManagerName, ctx.EffectiveUnits);

                    string tempDir = Util.SysIO.Path.GetFullPath(Util.SysIO.Path.Combine(ctx.SourceDirPath, "_luban_temp_" + Guid.NewGuid().ToString("N").Substring(0, 8)));
                    try
                    {
                        if (!CliRunner.RunDataExport(ctx.ConfPath, ctx.TargetName, tempDir))
                        {
                            return false;
                        }

                        if (ctx.TargetUnit != null)
                        {
                            JsonMerger.MergeForUnit(tempDir, ctx.TablesXmlPath, ctx.TargetUnit, ctx.TopModule);
                        }
                        else
                        {
                            JsonMerger.MergeAll(tempDir, ctx.TablesXmlPath, ctx.EffectiveUnits, ctx.TopModule);
                        }

                        return true;
                    }
                    finally
                    {
                        if (Util.SysIO.Directory.Exists(tempDir))
                        {
                            Util.SysIO.Directory.Delete(tempDir, true);
                        }
                        AssetDatabase.Refresh();
                    }
                }

                /// <summary>
                /// 导出代码（单文件或全部）：同步配置 → 调用 Luban CLI 生成代码 → 生成 Map 属性。
                /// </summary>
                /// <param name="ctx">导出上下文。</param>
                /// <returns>是否成功。</returns>
                public static bool ExportCode(LubanExportContext ctx)
                {
                    EditorUtil.Environment.LubanChecker.EnvironmentCheckResult envResult = EditorUtil.Environment.LubanChecker.Check();
                    if (!envResult.IsReady)
                    {
                        ConfigWindow.OpenLubanSection(envResult);
                        return false;
                    }

                    ConfigSyncer.SyncFromInspector(ctx.SourceDirPath, ctx.Settings, ctx.TargetName, ctx.ManagerName, ctx.EffectiveUnits);

                    if (!CliRunner.RunCodeGen(ctx.ConfPath, ctx.TargetName, ctx.OutputCodeDir, ctx.CustomTemplateDirs))
                    {
                        return false;
                    }

                    Dictionary<string, string> codeFiles = CliRunner.GetGeneratedCodeFiles(ctx.OutputCodeDir, ctx.RelevantFileNames);
                    Dictionary<string, int> mapPropResults;
                    if (ctx.TargetUnit != null)
                    {
                        mapPropResults = MapPropGen.GenerateForUnit(ctx.TargetUnit, ctx.TopModule);
                    }
                    else
                    {
                        mapPropResults = MapPropGen.GenerateAll(ctx.EffectiveUnits, ctx.TopModule);
                    }

                    FlushLogsAfterRefresh(() => LogCodeExportResults(codeFiles, mapPropResults));
                    return true;
                }

                /// <summary>
                /// 导出全部（代码 + 数据）：同步配置 → 调用 Luban CLI → 合并 JSON → 生成 Map 属性。
                /// </summary>
                /// <param name="ctx">导出上下文。</param>
                /// <returns>是否成功。</returns>
                public static bool ExportAll(LubanExportContext ctx)
                {
                    EditorUtil.Environment.LubanChecker.EnvironmentCheckResult envResult = EditorUtil.Environment.LubanChecker.Check();
                    if (!envResult.IsReady)
                    {
                        ConfigWindow.OpenLubanSection(envResult);
                        return false;
                    }

                    ConfigSyncer.SyncFromInspector(ctx.SourceDirPath, ctx.Settings, ctx.TargetName, ctx.ManagerName, ctx.EffectiveUnits);

                    string tempDir = Util.SysIO.Path.GetFullPath(Util.SysIO.Path.Combine(ctx.SourceDirPath, "_luban_temp_" + Guid.NewGuid().ToString("N").Substring(0, 8)));
                    try
                    {
                        bool hasCodeDir = !string.IsNullOrEmpty(ctx.OutputCodeDir);
                        bool success;
                        if (hasCodeDir)
                        {
                            success = CliRunner.RunAll(ctx.ConfPath, ctx.TargetName, ctx.OutputCodeDir, tempDir, ctx.CustomTemplateDirs);
                        }
                        else
                        {
                            success = CliRunner.RunDataExport(ctx.ConfPath, ctx.TargetName, tempDir);
                        }

                        if (!success)
                        {
                            return false;
                        }

                        Dictionary<string, int> dataResults = new Dictionary<string, int>();
                        JsonMerger.MergeAll(tempDir, ctx.TablesXmlPath, ctx.EffectiveUnits, ctx.TopModule, dataResults);
                        Dictionary<string, int> mapPropResults = MapPropGen.GenerateAll(ctx.EffectiveUnits, ctx.TopModule);

                        Dictionary<string, string> codeFiles = hasCodeDir ? CliRunner.GetGeneratedCodeFiles(ctx.OutputCodeDir, ctx.RelevantFileNames) : null;

                        FlushLogsAfterRefresh(() =>
                        {
                            LogDataExportResults(dataResults);
                            if (codeFiles != null)
                            {
                                LogCodeExportResults(codeFiles, mapPropResults);
                            }
                            Log.Debug(LogTag.Editor, "导出全部完成。");
                        });

                        return true;
                    }
                    finally
                    {
                        if (Util.SysIO.Directory.Exists(tempDir))
                        {
                            Util.SysIO.Directory.Delete(tempDir, true);
                        }
                    }
                }

                /// <summary>
                /// 先执行 AssetDatabase.Refresh()，若触发了重编译则将日志延迟到编译完成后输出，
                /// 避免 "Clear on Recompile" 清掉导出日志。
                /// </summary>
                /// <param name="logAction">日志输出回调。</param>
                private static void FlushLogsAfterRefresh(Action logAction)
                {
                    AssetDatabase.Refresh();

                    if (EditorApplication.isCompiling)
                    {
                        void OnCompilationFinished(object _)
                        {
                            CompilationPipeline.compilationFinished -= OnCompilationFinished;
                            logAction?.Invoke();
                        }
                        CompilationPipeline.compilationFinished += OnCompilationFinished;
                    }
                    else
                    {
                        logAction?.Invoke();
                    }
                }

                /// <summary>
                /// 输出数据导出日志：逐文件打印输出路径与表数量。
                /// </summary>
                /// <param name="dataResults">数据合并结果（输出路径 → 表数量）。</param>
                private static void LogDataExportResults(Dictionary<string, int> dataResults)
                {
                    foreach (var kvp in dataResults)
                    {
                        Log.Debug(LogTag.Editor, "数据导出完成：{0}（{1} 个表）", kvp.Key, kvp.Value);
                    }
                }

                /// <summary>
                /// 输出类型导出日志：合并代码文件与 Map 属性生成结果。
                /// <para>同一文件既有代码生成又有 Map 属性时，合并为一条日志。</para>
                /// </summary>
                /// <param name="codeFiles">代码文件（文件名 → 相对路径）。</param>
                /// <param name="mapPropResults">Map 属性生成结果（文件名 → 属性数量）。</param>
                private static void LogCodeExportResults(Dictionary<string, string> codeFiles, Dictionary<string, int> mapPropResults)
                {
                    foreach (var kvp in codeFiles)
                    {
                        if (mapPropResults.TryGetValue(kvp.Key, out int propCount) && propCount > 0)
                        {
                            Log.Debug(LogTag.Editor, "类型导出完成：{0}（{1}个属性）", kvp.Value, propCount);
                        }
                        else
                        {
                            Log.Debug(LogTag.Editor, "类型导出完成：{0}", kvp.Value);
                        }
                    }
                }
            }
        }
    }
}
