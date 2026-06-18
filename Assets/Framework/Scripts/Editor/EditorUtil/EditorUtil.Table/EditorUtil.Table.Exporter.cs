/***************************************************************
 * (c) copyright 2026 - 2030, Solotopia
 * All Rights Reserved.
 * -------------------------------------------------------------
 * filename:  EditorUtil.Table.Exporter.cs
 * author:    taoye
 * created:   2026/5/11
 * descrip:   Table 模块 Luban 导出逻辑工具入口，供 Inspector 与 Pipify Step 直调
 ***************************************************************/

using System.Collections.Generic;
using NovaFramework.Runtime;

namespace NovaFramework.Editor
{
    public static partial class EditorUtil
    {
        public static partial class Table
        {
            /// <summary>
            /// Table 模块 Luban 导出工具，封装清理旧文件、构建导出上下文、调用 Pipeline 三步流程。
            /// </summary>
            public static class Exporter
            {
                /// <summary>
                /// Luban target 名称，固定为 table。
                /// </summary>
                private const string c_ExportTargetName = "table";

                /// <summary>
                /// Luban manager 类名，固定为 TableTables。
                /// </summary>
                private const string c_ExportManagerName = "TableTables";

                /// <summary>
                /// 导出全部（代码 + 数据）：清理旧导出文件 → 构建导出上下文 → 调用 Pipeline.ExportAll。
                /// </summary>
                /// <param name="settings">TableSettings 实例，包含导出路径与单元配置。</param>
                /// <param name="sourceDirPath">表格数据源根目录的完整路径。</param>
                /// <returns>Pipeline.ExportAll 的执行结果，成功时为 true。</returns>
                public static bool ExportAll(TableSettings settings, string sourceDirPath)
                {
                    if (settings == null || string.IsNullOrEmpty(sourceDirPath))
                    {
                        return false;
                    }

                    ClearExportPaths(settings);

                    string classExportPath = CollectFirstClassExportPath(settings, out bool hasMultiplePaths);
                    if (hasMultiplePaths)
                    {
                        Log.Warning(LogTag.Editor, "检测到多个不同的类型导出路径，Luban 仅支持统一输出到一个目录，将使用：{0}", classExportPath);
                    }

                    var ctx = EditorUtil.Luban.ExportHelper.BuildExportContext(sourceDirPath, settings, c_ExportTargetName, c_ExportManagerName);
                    ctx.OutputCodeDir = classExportPath;
                    return EditorUtil.Luban.Pipeline.ExportAll(ctx);
                }

                /// <summary>
                /// 仅导出代码（类型定义）：构建导出上下文并调用 Pipeline.ExportCode。
                /// </summary>
                /// <param name="settings">TableSettings 实例，包含导出路径与单元配置。</param>
                /// <param name="sourceDirPath">表格数据源根目录的完整路径。</param>
                /// <returns>Pipeline.ExportCode 的执行结果，成功时为 true。</returns>
                public static bool ExportCode(TableSettings settings, string sourceDirPath)
                {
                    if (settings == null || string.IsNullOrEmpty(sourceDirPath))
                    {
                        return false;
                    }

                    string classExportPath = CollectFirstClassExportPath(settings, out _);
                    var ctx = EditorUtil.Luban.ExportHelper.BuildExportContext(sourceDirPath, settings, c_ExportTargetName, c_ExportManagerName);
                    ctx.OutputCodeDir = classExportPath;
                    return EditorUtil.Luban.Pipeline.ExportCode(ctx);
                }

                /// <summary>
                /// 仅导出数据（JSON）：构建导出上下文并调用 Pipeline.ExportData。
                /// </summary>
                /// <param name="settings">TableSettings 实例，包含导出路径与单元配置。</param>
                /// <param name="sourceDirPath">表格数据源根目录的完整路径。</param>
                /// <returns>Pipeline.ExportData 的执行结果，成功时为 true。</returns>
                public static bool ExportData(TableSettings settings, string sourceDirPath)
                {
                    if (settings == null || string.IsNullOrEmpty(sourceDirPath))
                    {
                        return false;
                    }

                    var ctx = EditorUtil.Luban.ExportHelper.BuildExportContext(sourceDirPath, settings, c_ExportTargetName, c_ExportManagerName);
                    return EditorUtil.Luban.Pipeline.ExportData(ctx);
                }

                /// <summary>
                /// 清理 settings 中所有单元的 DatasExportPath 与 ClassesExportPath 对应目录。
                /// 相同路径只删除一次，避免重复操作。
                /// </summary>
                /// <param name="settings">TableSettings 实例。</param>
                private static void ClearExportPaths(TableSettings settings)
                {
                    var dataPathsToClear = new HashSet<string>();
                    var classPathsToClear = new HashSet<string>();
                    foreach (TableUnitSetting unit in settings.TableUnitsSettings)
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
                /// <param name="settings">TableSettings 实例。</param>
                /// <param name="hasMultiplePaths">是否存在多于一个不同的类型导出路径。</param>
                /// <returns>第一个非空 ClassesExportPath，无任何非空路径时返回空字符串。</returns>
                private static string CollectFirstClassExportPath(TableSettings settings, out bool hasMultiplePaths)
                {
                    string classExportPath = "";
                    var distinctClassPaths = new HashSet<string>();
                    foreach (TableUnitSetting unit in settings.TableUnitsSettings)
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
