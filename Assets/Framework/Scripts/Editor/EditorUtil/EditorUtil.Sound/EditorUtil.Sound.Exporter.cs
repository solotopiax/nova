/***************************************************************
 * (c) copyright 2026 - 2030, Solotopia
 * All Rights Reserved.
 * -------------------------------------------------------------
 * filename:  EditorUtil.Sound.Exporter.cs
 * author:    taoye
 * created:   2026/5/11
 * descrip:   Sound 模块 Luban 导出流水线薄封装
 ***************************************************************/

using System.Collections.Generic;
using NovaFramework.Runtime;

namespace NovaFramework.Editor
{
    public static partial class EditorUtil
    {
        public static partial class Sound
        {
            /// <summary>
            /// Sound 模块 Luban 导出流水线薄封装。
            /// 封装 ExportAll / ExportCode / ExportData 三种导出操作，调用 EditorUtil.Luban 通用管道。
            /// Inspector 通过此类下沉业务导出逻辑，自身仅负责参数组装与序列化对象读取。
            /// </summary>
            public static class Exporter
            {
                /// <summary>
                /// 导出所有数据与类型：清理旧导出路径后通过 Luban 管道一次性生成代码与数据。
                /// 若多个单元指向不同的类型导出路径，记录警告并使用首个非空路径。
                /// </summary>
                /// <param name="sourceDirPath">数据源根目录完整路径。</param>
                /// <param name="settings">Sound 模块设置，包含所有单元配置。</param>
                /// <param name="targetName">Luban target 名称（如 "sound"）。</param>
                /// <param name="managerName">Luban manager 类名（如 "SoundTables"）。</param>
                public static void ExportAll(string sourceDirPath, SoundSettings settings, string targetName, string managerName)
                {
                    if (settings == null || string.IsNullOrEmpty(sourceDirPath) || settings.SoundUnitsSettings == null)
                    {
                        return;
                    }

                    var dataPathsToClear = new HashSet<string>();
                    var classPathsToClear = new HashSet<string>();
                    foreach (SoundUnitSetting unit in settings.SoundUnitsSettings)
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

                    string classExportPath = "";
                    var distinctClassPaths = new HashSet<string>();
                    foreach (SoundUnitSetting unit in settings.SoundUnitsSettings)
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
                    if (distinctClassPaths.Count > 1)
                    {
                        Log.Warning(LogTag.Editor, "检测到多个不同的类型导出路径，Luban 仅支持统一输出到一个目录，将使用：{0}", classExportPath);
                    }

                    var ctx = EditorUtil.Luban.ExportHelper.BuildExportContext(sourceDirPath, settings, targetName, managerName);
                    ctx.OutputCodeDir = classExportPath;
                    EditorUtil.Luban.Pipeline.ExportAll(ctx);
                }

                /// <summary>
                /// 导出单个文件的数据：通过 Luban 管道导出指定单元的 JSON 数据。
                /// </summary>
                /// <param name="sourceDirPath">数据源根目录完整路径。</param>
                /// <param name="settings">Sound 模块设置。</param>
                /// <param name="unitSetting">目标单元设置；为 null 时静默返回。</param>
                /// <param name="targetName">Luban target 名称。</param>
                /// <param name="managerName">Luban manager 类名。</param>
                public static void ExportData(string sourceDirPath, SoundSettings settings, SoundUnitSetting unitSetting, string targetName, string managerName)
                {
                    if (unitSetting == null)
                    {
                        return;
                    }

                    var ctx = EditorUtil.Luban.ExportHelper.BuildExportContext(sourceDirPath, settings, targetName, managerName);
                    ctx.TargetUnit = unitSetting;
                    EditorUtil.Luban.Pipeline.ExportData(ctx);
                }

                /// <summary>
                /// 导出单个文件的类型代码：通过 Luban 管道为指定文件生成 C# 类型。
                /// </summary>
                /// <param name="sourceDirPath">数据源根目录完整路径。</param>
                /// <param name="settings">Sound 模块设置。</param>
                /// <param name="unitSetting">目标单元设置；为 null 时仅按文件名过滤生成。</param>
                /// <param name="classExportPath">类型代码输出目录路径。</param>
                /// <param name="relevantFileNames">相关文件名集合，用于过滤 Luban 输出日志。</param>
                /// <param name="targetName">Luban target 名称。</param>
                /// <param name="managerName">Luban manager 类名。</param>
                public static void ExportCode(string sourceDirPath, SoundSettings settings, SoundUnitSetting unitSetting, string classExportPath, HashSet<string> relevantFileNames, string targetName, string managerName)
                {
                    var ctx = EditorUtil.Luban.ExportHelper.BuildExportContext(sourceDirPath, settings, targetName, managerName);
                    ctx.OutputCodeDir = classExportPath;
                    ctx.RelevantFileNames = relevantFileNames;
                    ctx.TargetUnit = unitSetting;
                    EditorUtil.Luban.Pipeline.ExportCode(ctx);
                }
            }
        }
    }
}
