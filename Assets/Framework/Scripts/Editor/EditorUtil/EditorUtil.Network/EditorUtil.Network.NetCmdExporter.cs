/***************************************************************
 * (c) copyright 2026 - 2030, Solotopia
 * All Rights Reserved.
 * -------------------------------------------------------------
 * filename:  EditorUtil.Network.NetCmdExporter.cs
 * author:    taoye
 * created:   2026/5/11
 * descrip:   指令表（NetCmd）导出工具，封装数据和类型的 Luban 全量导出逻辑
 ***************************************************************/

using System.Collections.Generic;
using NovaFramework.Runtime;

namespace NovaFramework.Editor
{
    public static partial class EditorUtil
    {
        public static partial class Network
        {
            /// <summary>
            /// 指令表（NetCmd）导出工具，封装数据导出和类型代码生成的一键全量导出入口。
            /// </summary>
            public static class NetCmdExporter
            {
                /// <summary>
                /// Luban target 名称。
                /// </summary>
                private const string c_TargetName = "network-cmd";

                /// <summary>
                /// Luban manager 类名。
                /// </summary>
                private const string c_ManagerName = "NetworkTables";

                /// <summary>
                /// 导出指令表所有单元的数据和类型代码（等价于 Inspector "导出所有指令表数据和类型" 按钮）。
                /// </summary>
                /// <param name="settings">指令表设置。</param>
                /// <returns>导出是否成功。</returns>
                public static bool ExportNetCmdAll(NetCmdSettings settings)
                {
                    if (settings == null)
                    {
                        Log.Error(LogTag.Editor, "NetCmdSettings 为 null，无法导出。");
                        return false;
                    }

                    return ExportAll(settings.SourceDirPath, settings, settings.NetCmdUnits);
                }

                /// <summary>
                /// 仅导出指令表类型代码（跳过数据，重新生成 C# 枚举/常量类）。
                /// </summary>
                /// <param name="settings">指令表设置。</param>
                /// <returns>导出是否成功。</returns>
                public static bool ExportNetCmdCode(NetCmdSettings settings)
                {
                    if (settings == null)
                    {
                        Log.Error(LogTag.Editor, "NetCmdSettings 为 null，无法导出类型代码。");
                        return false;
                    }

                    return ExportCode(settings.SourceDirPath, settings, settings.NetCmdUnits);
                }

                /// <summary>
                /// 仅导出指令表数据（跳过类型代码，重新生成 JSON 数据文件）。
                /// </summary>
                /// <param name="settings">指令表设置。</param>
                /// <returns>导出是否成功。</returns>
                public static bool ExportNetCmdData(NetCmdSettings settings)
                {
                    if (settings == null)
                    {
                        Log.Error(LogTag.Editor, "NetCmdSettings 为 null，无法导出数据。");
                        return false;
                    }

                    return ExportData(settings.SourceDirPath, settings, settings.NetCmdUnits);
                }

                /// <summary>
                /// 全量导出（数据 + 类型代码）。
                /// </summary>
                /// <param name="regionDirPath">数据源目录路径。</param>
                /// <param name="settings">指令表设置（IDataTableSettings）。</param>
                /// <param name="units">单元设置列表。</param>
                /// <returns>导出是否成功。</returns>
                private static bool ExportAll(string regionDirPath, IDataTableSettings settings, IReadOnlyList<IDataTableUnitSetting> units)
                {
                    if (string.IsNullOrEmpty(regionDirPath) || !Util.SysIO.Directory.Exists(regionDirPath))
                    {
                        Log.Error(LogTag.Editor, "NetCmd 数据源目录不存在：{0}", regionDirPath);
                        return false;
                    }

                    if (units == null || units.Count == 0)
                    {
                        Log.Warning(LogTag.Editor, "NetCmd 单元设置为空，跳过导出。");
                        return true;
                    }

                    string classExportPath = ResolveClassExportPath(units);
                    string tempDir = EditorUtil.Luban.ExportHelper.GetPreFilterTempDirPath(regionDirPath);
                    EditorUtil.Luban.ConfigSyncer.CleanTempDir(tempDir);

                    try
                    {
                        NetworkExcelPreFilter.FilterAll(regionDirPath, tempDir);
                        var ctx = EditorUtil.Luban.ExportHelper.BuildExportContext(regionDirPath, settings, c_TargetName, c_ManagerName);
                        ctx.RegionUnits = units;
                        ctx.OutputCodeDir = classExportPath;
                        EditorUtil.Luban.Pipeline.ExportAll(ctx);
                        return true;
                    }
                    finally
                    {
                        EditorUtil.Luban.ConfigSyncer.CleanTempDir(tempDir);
                    }
                }

                /// <summary>
                /// 仅导出类型代码。
                /// </summary>
                /// <param name="regionDirPath">数据源目录路径。</param>
                /// <param name="settings">指令表设置（IDataTableSettings）。</param>
                /// <param name="units">单元设置列表。</param>
                /// <returns>导出是否成功。</returns>
                private static bool ExportCode(string regionDirPath, IDataTableSettings settings, IReadOnlyList<IDataTableUnitSetting> units)
                {
                    if (string.IsNullOrEmpty(regionDirPath) || !Util.SysIO.Directory.Exists(regionDirPath))
                    {
                        Log.Error(LogTag.Editor, "NetCmd 数据源目录不存在：{0}", regionDirPath);
                        return false;
                    }

                    string classExportPath = ResolveClassExportPath(units);
                    string tempDir = EditorUtil.Luban.ExportHelper.GetPreFilterTempDirPath(regionDirPath);
                    EditorUtil.Luban.ConfigSyncer.CleanTempDir(tempDir);

                    try
                    {
                        NetworkExcelPreFilter.FilterAll(regionDirPath, tempDir);
                        var ctx = EditorUtil.Luban.ExportHelper.BuildExportContext(regionDirPath, settings, c_TargetName, c_ManagerName);
                        ctx.RegionUnits = units;
                        ctx.OutputCodeDir = classExportPath;
                        EditorUtil.Luban.Pipeline.ExportCode(ctx);
                        return true;
                    }
                    finally
                    {
                        EditorUtil.Luban.ConfigSyncer.CleanTempDir(tempDir);
                    }
                }

                /// <summary>
                /// 仅导出数据。
                /// </summary>
                /// <param name="regionDirPath">数据源目录路径。</param>
                /// <param name="settings">指令表设置（IDataTableSettings）。</param>
                /// <param name="units">单元设置列表。</param>
                /// <returns>导出是否成功。</returns>
                private static bool ExportData(string regionDirPath, IDataTableSettings settings, IReadOnlyList<IDataTableUnitSetting> units)
                {
                    if (string.IsNullOrEmpty(regionDirPath) || !Util.SysIO.Directory.Exists(regionDirPath))
                    {
                        Log.Error(LogTag.Editor, "NetCmd 数据源目录不存在：{0}", regionDirPath);
                        return false;
                    }

                    string tempDir = EditorUtil.Luban.ExportHelper.GetPreFilterTempDirPath(regionDirPath);
                    EditorUtil.Luban.ConfigSyncer.CleanTempDir(tempDir);

                    try
                    {
                        NetworkExcelPreFilter.FilterAll(regionDirPath, tempDir);
                        var ctx = EditorUtil.Luban.ExportHelper.BuildExportContext(regionDirPath, settings, c_TargetName, c_ManagerName);
                        ctx.RegionUnits = units;
                        EditorUtil.Luban.Pipeline.ExportData(ctx);
                        return true;
                    }
                    finally
                    {
                        EditorUtil.Luban.ConfigSyncer.CleanTempDir(tempDir);
                    }
                }

                /// <summary>
                /// 从单元设置列表中取第一个非空的 ClassesExportPath 作为统一类型导出目录。
                /// </summary>
                /// <param name="units">单元设置列表。</param>
                /// <returns>类型导出目录路径，无可用路径时返回空字符串。</returns>
                private static string ResolveClassExportPath(IReadOnlyList<IDataTableUnitSetting> units)
                {
                    if (units == null)
                    {
                        return string.Empty;
                    }

                    for (int i = 0; i < units.Count; i++)
                    {
                        if (!string.IsNullOrEmpty(units[i].ClassesExportPath))
                        {
                            return units[i].ClassesExportPath;
                        }
                    }

                    return string.Empty;
                }
            }
        }
    }
}
