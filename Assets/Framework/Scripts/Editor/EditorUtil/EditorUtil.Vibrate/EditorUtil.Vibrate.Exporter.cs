/***************************************************************
 * (c) copyright 2026 - 2030, Solotopia
 * All Rights Reserved.
 * -------------------------------------------------------------
 * filename:  EditorUtil.Vibrate.Exporter.cs
 * author:    taoye
 * created:   2026/5/11
 * descrip:   EditorUtil.Vibrate —— Luban 导出静态工具（Emphasis + Custom 双轨）
 ***************************************************************/

using System.Collections.Generic;
using NovaFramework.Runtime;

namespace NovaFramework.Editor
{
    public static partial class EditorUtil
    {
        public static partial class Vibrate
        {
            /// <summary>
            /// Emphasis 区域的 Luban target 名称。
            /// </summary>
            private const string c_EmphasisTargetName = "vibrate-emphasis";

            /// <summary>
            /// Custom 区域的 Luban target 名称。
            /// </summary>
            private const string c_CustomTargetName = "vibrate-custom";

            /// <summary>
            /// Emphasis 区域的 Luban manager 类名。
            /// </summary>
            private const string c_EmphasisManagerName = "VibrateEmphasisTables";

            /// <summary>
            /// Custom 区域的 Luban manager 类名。
            /// </summary>
            private const string c_CustomManagerName = "VibrateCustomTables";

            /// <summary>
            /// 导出 Emphasis 区域单个文件的数据。
            /// </summary>
            /// <param name="filePath">文件完整路径。</param>
            /// <param name="dataExportPath">数据导出目标路径。</param>
            /// <param name="settings">当前 VibrateSettings 实例。</param>
            public static void ExportEmphasisData(string filePath, string dataExportPath, VibrateSettings settings)
            {
                ExportData(filePath, dataExportPath, settings, isEmphasis: true);
            }

            /// <summary>
            /// 导出 Emphasis 区域单个文件的类型。
            /// </summary>
            /// <param name="filePath">文件完整路径。</param>
            /// <param name="classExportPath">类型导出目标路径。</param>
            /// <param name="settings">当前 VibrateSettings 实例。</param>
            public static void ExportEmphasisCode(string filePath, string classExportPath, VibrateSettings settings)
            {
                ExportCode(filePath, classExportPath, settings, isEmphasis: true);
            }

            /// <summary>
            /// 导出 Emphasis 区域的全部数据和类型。
            /// </summary>
            /// <param name="settings">当前 VibrateSettings 实例。</param>
            public static void ExportEmphasisAll(VibrateSettings settings)
            {
                ExportAll(settings, isEmphasis: true);
            }

            /// <summary>
            /// 导出 Custom 区域单个文件的数据。
            /// </summary>
            /// <param name="filePath">文件完整路径。</param>
            /// <param name="dataExportPath">数据导出目标路径。</param>
            /// <param name="settings">当前 VibrateSettings 实例。</param>
            public static void ExportCustomData(string filePath, string dataExportPath, VibrateSettings settings)
            {
                ExportData(filePath, dataExportPath, settings, isEmphasis: false);
            }

            /// <summary>
            /// 导出 Custom 区域单个文件的类型。
            /// </summary>
            /// <param name="filePath">文件完整路径。</param>
            /// <param name="classExportPath">类型导出目标路径。</param>
            /// <param name="settings">当前 VibrateSettings 实例。</param>
            public static void ExportCustomCode(string filePath, string classExportPath, VibrateSettings settings)
            {
                ExportCode(filePath, classExportPath, settings, isEmphasis: false);
            }

            /// <summary>
            /// 导出 Custom 区域的全部数据和类型。
            /// </summary>
            /// <param name="settings">当前 VibrateSettings 实例。</param>
            public static void ExportCustomAll(VibrateSettings settings)
            {
                ExportAll(settings, isEmphasis: false);
            }

            /// <summary>
            /// 导出单个文件的数据：构建导出上下文、设置目标单元后调用 Pipeline。
            /// </summary>
            /// <param name="filePath">文件完整路径。</param>
            /// <param name="dataExportPath">数据导出目标路径。</param>
            /// <param name="settings">VibrateSettings 实例。</param>
            /// <param name="isEmphasis">是否为 Emphasis 区域。</param>
            private static void ExportData(string filePath, string dataExportPath, VibrateSettings settings, bool isEmphasis)
            {
                if (string.IsNullOrEmpty(dataExportPath) || settings == null)
                {
                    return;
                }

                string sourceDirPath = isEmphasis ? settings.EmphasisSourceDirPath : settings.CustomSourceDirPath;
                List<VibrateUnitSetting> units = isEmphasis ? settings.EmphasisUnitsSettings : settings.CustomUnitsSettings;
                string managerName = isEmphasis ? c_EmphasisManagerName : c_CustomManagerName;
                string targetName = isEmphasis ? c_EmphasisTargetName : c_CustomTargetName;

                string relativePath = Util.SysIO.Path.GetRelativePath(sourceDirPath.TrimEnd('/', '\\'), filePath);
                VibrateUnitSetting unitSetting = units?.Find(u => u.SourcePath == relativePath);
                if (unitSetting == null)
                {
                    Log.Error(LogTag.Editor, "未找到文件 {0} 对应的 VibrateUnitSetting。", relativePath);
                    return;
                }

                IDataTableSettings areaSettings = isEmphasis ? settings.GetEmphasisAsSettings() : settings.GetCustomAsSettings();
                var ctx = EditorUtil.Luban.ExportHelper.BuildExportContext(sourceDirPath, areaSettings, targetName, managerName);
                ctx.TargetUnit = unitSetting;
                EditorUtil.Luban.Pipeline.ExportData(ctx);
            }

            /// <summary>
            /// 导出单个文件的类型：构建导出上下文、设置相关文件列表后调用 Pipeline。
            /// </summary>
            /// <param name="filePath">文件完整路径。</param>
            /// <param name="classExportPath">类型导出目标路径。</param>
            /// <param name="settings">VibrateSettings 实例。</param>
            /// <param name="isEmphasis">是否为 Emphasis 区域。</param>
            private static void ExportCode(string filePath, string classExportPath, VibrateSettings settings, bool isEmphasis)
            {
                if (string.IsNullOrEmpty(classExportPath) || settings == null)
                {
                    return;
                }

                string sourceDirPath = isEmphasis ? settings.EmphasisSourceDirPath : settings.CustomSourceDirPath;
                List<VibrateUnitSetting> units = isEmphasis ? settings.EmphasisUnitsSettings : settings.CustomUnitsSettings;
                string managerName = isEmphasis ? c_EmphasisManagerName : c_CustomManagerName;
                string targetName = isEmphasis ? c_EmphasisTargetName : c_CustomTargetName;

                string relativePath = Util.SysIO.Path.GetRelativePath(sourceDirPath.TrimEnd('/', '\\'), filePath);
                VibrateUnitSetting unitSetting = units?.Find(u => u.SourcePath == relativePath);

                IDataTableSettings areaSettings = isEmphasis ? settings.GetEmphasisAsSettings() : settings.GetCustomAsSettings();
                var ctx = EditorUtil.Luban.ExportHelper.BuildExportContext(sourceDirPath, areaSettings, targetName, managerName);
                ctx.OutputCodeDir = classExportPath;
                ctx.RelevantFileNames = EditorUtil.Luban.ExportHelper.BuildRelevantFileNames(filePath, sourceDirPath, areaSettings.Units, managerName);
                ctx.TargetUnit = unitSetting;
                EditorUtil.Luban.Pipeline.ExportCode(ctx);
            }

            /// <summary>
            /// 导出整个区域的全部数据和类型：清理旧导出目录、构建上下文后调用 Pipeline。
            /// </summary>
            /// <param name="settings">VibrateSettings 实例。</param>
            /// <param name="isEmphasis">是否为 Emphasis 区域。</param>
            private static void ExportAll(VibrateSettings settings, bool isEmphasis)
            {
                if (settings == null)
                {
                    return;
                }

                string sourceDirPath = isEmphasis ? settings.EmphasisSourceDirPath : settings.CustomSourceDirPath;
                List<VibrateUnitSetting> units = isEmphasis ? settings.EmphasisUnitsSettings : settings.CustomUnitsSettings;
                string managerName = isEmphasis ? c_EmphasisManagerName : c_CustomManagerName;
                string targetName = isEmphasis ? c_EmphasisTargetName : c_CustomTargetName;

                var dataPathsToClear = new HashSet<string>();
                var classPathsToClear = new HashSet<string>();
                foreach (VibrateUnitSetting unit in units)
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

                string classExportPath = string.Empty;
                var distinctClassPaths = new HashSet<string>();
                foreach (VibrateUnitSetting unit in units)
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

                IDataTableSettings areaSettings = isEmphasis ? settings.GetEmphasisAsSettings() : settings.GetCustomAsSettings();
                var ctx = EditorUtil.Luban.ExportHelper.BuildExportContext(sourceDirPath, areaSettings, targetName, managerName);
                ctx.OutputCodeDir = classExportPath;
                EditorUtil.Luban.Pipeline.ExportAll(ctx);
            }
        }
    }
}
