/***************************************************************
 * (c) copyright 2026 - 2030, Solotopia
 * All Rights Reserved.
 * -------------------------------------------------------------
 * filename:  EditorUtil.UI.Exporter.cs
 * author:    taoye
 * created:   2026/5/11
 * descrip:   UI 模块 Luban 导出流水线静态入口，脱离 Inspector/SerializedProperty 依赖
 ***************************************************************/

using System.Collections.Generic;
using NovaFramework.Runtime;

namespace NovaFramework.Editor
{
    public static partial class EditorUtil
    {
        public static partial class UI
        {
            /// <summary>
            /// UI 模块 Luban 导出入口，提供全量导出、代码导出、数据导出三类操作。
            /// </summary>
            public static class Exporter
            {
                /// <summary>
                /// Luban target 名称。
                /// </summary>
                private const string c_ExportTargetName = "ui";

                /// <summary>
                /// Luban manager 类名。
                /// </summary>
                private const string c_ExportManagerName = "UITables";

                /// <summary>
                /// 全量导出：清理旧导出目录，一次性通过 Luban Pipeline 生成代码与数据并合并 JSON。
                /// <para>若 UIUnitsSettings 为空则静默返回。当多个 UIUnitSetting 指定不同的 ClassesExportPath 时，
                /// Luban 只支持统一输出到同一目录，实际使用首个非空路径并输出警告。</para>
                /// </summary>
                /// <param name="settings">UI 模块配置，含 UIUnitsSettings 列表。</param>
                /// <param name="sourceDirPath">数据源目录完整路径。</param>
                public static void ExportAll(UISettings settings, string sourceDirPath)
                {
                    if (settings == null || settings.UIUnitsSettings == null || settings.UIUnitsSettings.Count == 0)
                    {
                        return;
                    }

                    ClearExportPaths(settings);

                    string classExportPath = CollectFirstClassExportPath(settings, out bool hasMultiplePaths);
                    if (hasMultiplePaths)
                    {
                        Log.Warning(LogTag.Editor, "检测到多个不同的类型导出路径，Luban 仅支持统一输出到一个目录，将使用：{0}", classExportPath);
                    }

                    var ctx = EditorUtil.Luban.ExportHelper.BuildExportContext(sourceDirPath, settings, c_ExportTargetName, c_ExportManagerName);
                    ctx.OutputCodeDir = classExportPath;
                    EditorUtil.Luban.Pipeline.ExportAll(ctx);
                }

                /// <summary>
                /// 全量仅导出代码（类型定义）：构建导出上下文并调用 Pipeline.ExportCode，不清理旧目录。
                /// </summary>
                /// <param name="settings">UI 模块配置，含 UIUnitsSettings 列表。</param>
                /// <param name="sourceDirPath">数据源目录完整路径。</param>
                public static void ExportCode(UISettings settings, string sourceDirPath)
                {
                    if (settings == null || string.IsNullOrEmpty(sourceDirPath))
                    {
                        return;
                    }

                    string classExportPath = CollectFirstClassExportPath(settings, out _);
                    var ctx = EditorUtil.Luban.ExportHelper.BuildExportContext(sourceDirPath, settings, c_ExportTargetName, c_ExportManagerName);
                    ctx.OutputCodeDir = classExportPath;
                    EditorUtil.Luban.Pipeline.ExportCode(ctx);
                }

                /// <summary>
                /// 全量仅导出数据（JSON）：构建导出上下文并调用 Pipeline.ExportData，不清理旧目录。
                /// </summary>
                /// <param name="settings">UI 模块配置，含 UIUnitsSettings 列表。</param>
                /// <param name="sourceDirPath">数据源目录完整路径。</param>
                public static void ExportData(UISettings settings, string sourceDirPath)
                {
                    if (settings == null || string.IsNullOrEmpty(sourceDirPath))
                    {
                        return;
                    }

                    var ctx = EditorUtil.Luban.ExportHelper.BuildExportContext(sourceDirPath, settings, c_ExportTargetName, c_ExportManagerName);
                    EditorUtil.Luban.Pipeline.ExportData(ctx);
                }

                /// <summary>
                /// 单文件代码导出：为指定数据源文件生成 Luban 代码，若 unitSetting 为 null 则不限定目标单元。
                /// </summary>
                /// <param name="settings">UI 模块配置，含 UIUnitsSettings 列表。</param>
                /// <param name="sourceDirPath">数据源目录完整路径。</param>
                /// <param name="filePath">目标源文件完整路径。</param>
                /// <param name="classExportPath">代码输出目录路径。</param>
                public static void ExportCodeForFile(UISettings settings, string sourceDirPath, string filePath, string classExportPath)
                {
                    if (settings == null || string.IsNullOrEmpty(sourceDirPath) || string.IsNullOrEmpty(classExportPath))
                    {
                        return;
                    }

                    string relativePath = Util.SysIO.Path.GetRelativePath(sourceDirPath.TrimEnd('/', '\\'), filePath);
                    UIUnitSetting unitSetting = settings.UIUnitsSettings?.Find(u => u.SourcePath == relativePath);

                    var ctx = EditorUtil.Luban.ExportHelper.BuildExportContext(sourceDirPath, settings, c_ExportTargetName, c_ExportManagerName);
                    ctx.OutputCodeDir = classExportPath;
                    ctx.RelevantFileNames = EditorUtil.Luban.ExportHelper.BuildRelevantFileNames(filePath, sourceDirPath, ((IDataTableSettings)settings).Units, ctx.ManagerName);
                    ctx.TargetUnit = unitSetting;
                    EditorUtil.Luban.Pipeline.ExportCode(ctx);
                }

                /// <summary>
                /// 单文件数据导出：为指定数据源文件执行 Luban 数据导出并合并 JSON。
                /// <para>若在 UIUnitsSettings 中未找到与 filePath 对应的 UIUnitSetting，则输出错误日志并返回。</para>
                /// </summary>
                /// <param name="settings">UI 模块配置，含 UIUnitsSettings 列表。</param>
                /// <param name="sourceDirPath">数据源目录完整路径。</param>
                /// <param name="filePath">目标源文件完整路径。</param>
                public static void ExportDataForFile(UISettings settings, string sourceDirPath, string filePath)
                {
                    if (settings == null || string.IsNullOrEmpty(sourceDirPath))
                    {
                        return;
                    }

                    string relativePath = Util.SysIO.Path.GetRelativePath(sourceDirPath.TrimEnd('/', '\\'), filePath);
                    UIUnitSetting unitSetting = settings.UIUnitsSettings?.Find(u => u.SourcePath == relativePath);
                    if (unitSetting == null)
                    {
                        Log.Error(LogTag.Editor, "未找到文件 {0} 对应的 UIUnitSetting。", relativePath);
                        return;
                    }

                    var ctx = EditorUtil.Luban.ExportHelper.BuildExportContext(sourceDirPath, settings, c_ExportTargetName, c_ExportManagerName);
                    ctx.TargetUnit = unitSetting;
                    EditorUtil.Luban.Pipeline.ExportData(ctx);
                }

                /// <summary>
                /// 清理 settings 中所有单元的 DatasExportPath 与 ClassesExportPath 对应目录。
                /// 相同路径只删除一次，避免重复操作。
                /// </summary>
                /// <param name="settings">UISettings 实例。</param>
                private static void ClearExportPaths(UISettings settings)
                {
                    var dataPathsToClear = new HashSet<string>();
                    var classPathsToClear = new HashSet<string>();
                    foreach (UIUnitSetting unit in settings.UIUnitsSettings)
                    {
                        if (!string.IsNullOrEmpty(unit.DatasExportPath))
                        {
                            dataPathsToClear.Add(unit.DatasExportPath);
                        }
                        if (!string.IsNullOrEmpty(unit.ClassesExportPath))
                        {
                            classPathsToClear.Add(unit.ClassesExportPath);
                        }
                    }
                    foreach (string path in dataPathsToClear)
                    {
                        EditorUtil.FileSystem.DeletePath(path);
                    }
                    foreach (string path in classPathsToClear)
                    {
                        EditorUtil.FileSystem.DeletePath(path);
                    }
                }

                /// <summary>
                /// 从 settings 中收集第一个非空 ClassesExportPath，并通过 out 参数标识是否存在多个不同路径。
                /// </summary>
                /// <param name="settings">UISettings 实例。</param>
                /// <param name="hasMultiplePaths">是否存在多于一个不同的类型导出路径。</param>
                /// <returns>第一个非空 ClassesExportPath，无任何非空路径时返回空字符串。</returns>
                private static string CollectFirstClassExportPath(UISettings settings, out bool hasMultiplePaths)
                {
                    string classExportPath = "";
                    var distinctClassPaths = new HashSet<string>();
                    foreach (UIUnitSetting unit in settings.UIUnitsSettings)
                    {
                        if (!string.IsNullOrEmpty(unit.ClassesExportPath))
                        {
                            distinctClassPaths.Add(unit.ClassesExportPath);
                            if (string.IsNullOrEmpty(classExportPath))
                            {
                                classExportPath = unit.ClassesExportPath;
                            }
                        }
                    }
                    hasMultiplePaths = distinctClassPaths.Count > 1;
                    return classExportPath;
                }
            }
        }
    }
}
