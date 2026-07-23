/***************************************************************
 * (c) copyright 2026 - 2030, Solotopia
 * All Rights Reserved.
 * -------------------------------------------------------------
 * filename:  EditorUtil.UI.Exporter.cs
 * author:    taoye
 * created:   2026/5/11
 * descrip:   UI 模块导出的公共门面，只负责保持稳定 API 并转发到内部编排器
 * input:     UISettings、UI Excel 源目录以及可选的单文件目标
 * output:    导出是否成功
 * boundary:  不承担 Excel 校验、Luban 编排、暂存发布或回滚
 ***************************************************************/

using NovaFramework.Runtime;

namespace NovaFramework.Editor
{
    public static partial class EditorUtil
    {
        public static partial class UI
        {
            /// <summary>
            /// UI 模块 Luban 导出公共入口；实际流程由 <see cref="UIExporter"/> 负责。
            /// </summary>
            public static class Exporter
            {
                public static bool ExportAll(UISettings settings, string sourceDirPath)
                {
                    return UIExporter.ExportAll(settings, sourceDirPath);
                }

                public static bool ExportCode(UISettings settings, string sourceDirPath)
                {
                    return UIExporter.ExportCode(settings, sourceDirPath);
                }

                public static bool ExportData(UISettings settings, string sourceDirPath)
                {
                    return UIExporter.ExportData(settings, sourceDirPath);
                }

                public static bool ExportCodeForFile(
                    UISettings settings,
                    string sourceDirPath,
                    string filePath,
                    string classExportPath)
                {
                    return UIExporter.ExportCodeForFile(
                        settings,
                        sourceDirPath,
                        filePath,
                        classExportPath);
                }

                public static bool ExportDataForFile(
                    UISettings settings,
                    string sourceDirPath,
                    string filePath)
                {
                    return UIExporter.ExportDataForFile(settings, sourceDirPath, filePath);
                }
            }
        }
    }
}
