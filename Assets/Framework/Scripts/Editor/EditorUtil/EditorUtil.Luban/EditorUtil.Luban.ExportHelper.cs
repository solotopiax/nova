/***************************************************************
 * (c) copyright 2026 - 2030, Solotopia
 * All Rights Reserved.
 * -------------------------------------------------------------
 * filename:  EditorUtil.Luban.ExportHelper.cs
 * author:    taoye
 * created:   2026/4/25
 * descrip:   Luban 导出辅助工具：构建导出上下文、生成关联文件名、查找单元设置
 ***************************************************************/

using System.Collections.Generic;
using NovaFramework.Runtime;
using UnityEditor.PackageManager;

namespace NovaFramework.Editor
{
    public static partial class EditorUtil
    {
        public static partial class Luban
        {
            /// <summary>
            /// Luban 导出辅助工具：构建导出上下文、生成关联文件名、查找单元设置。
            /// </summary>
            public static class ExportHelper
            {
                /// <summary>
                /// 构建标准 Luban 导出上下文。
                /// </summary>
                /// <param name="sourceDirPath">数据源根目录路径。</param>
                /// <param name="settings">数据表设置。</param>
                /// <param name="targetName">模块名称（如 "sound" / "ui" / "network-cmd" / "network-hostkey"），对应 Templates/Luban/ 下的子目录名。</param>
                /// <param name="managerName">Luban manager 类名（如 "TableTables" / "ConfigTables" / "SoundTables" / "UITables"）。</param>
                /// <returns>初始化完毕的导出上下文。</returns>
                public static LubanExportContext BuildExportContext(string sourceDirPath, IDataTableSettings settings, string targetName, string managerName)
                {
                    string configDir = ConfigSyncer.GetConfigDirPath(sourceDirPath);
                    string confPath = Util.SysIO.Path.Combine(configDir, ConfigSyncer.c_LubanConfFileName);
                    string tablesXmlPath = Util.SysIO.Path.Combine(configDir, ConfigSyncer.c_TablesXmlFileName);
                    string topModule = EditorUtil.Config.RuntimeProvider.GetNamespace();

                    return new LubanExportContext
                    {
                        SourceDirPath = sourceDirPath,
                        ConfPath = confPath,
                        TargetName = targetName,
                        ManagerName = managerName,
                        TopModule = topModule,
                        CustomTemplateDirs = GetLubanCustomTemplateDirs(targetName),
                        TablesXmlPath = tablesXmlPath,
                        Settings = settings,
                    };
                }

                /// <summary>
                /// 根据数据源文件构建其对应的生成代码文件名集合（用于单文件导出时过滤日志）。
                /// 遍历 units 列表找到匹配的 UnitSetting，提取 DataTypeNames 生成 SheetName.cs 和 TbSheetName.cs，并加入 managerName.cs。
                /// </summary>
                /// <param name="filePath">数据源文件的完整路径。</param>
                /// <param name="sourceDirPath">数据源根目录路径。</param>
                /// <param name="units">当前的单元设置列表。</param>
                /// <param name="managerName">Luban manager 类名。</param>
                /// <returns>该文件关联的 .cs 文件名集合。</returns>
                public static HashSet<string> BuildRelevantFileNames(string filePath, string sourceDirPath, IReadOnlyList<IDataTableUnitSetting> units, string managerName)
                {
                    HashSet<string> fileNames = new HashSet<string>();
                    string relativePath = Util.SysIO.Path.GetRelativePath(sourceDirPath.TrimEnd('/', '\\'), filePath);
                    IDataTableUnitSetting unitSetting = FindUnitSetting(units, relativePath);
                    if (unitSetting?.DataTypeNames != null)
                    {
                        foreach (string typeName in unitSetting.DataTypeNames)
                        {
                            if (string.IsNullOrEmpty(typeName) || typeName.StartsWith("#"))
                            {
                                continue;
                            }

                            string sheetName = typeName.Contains(".") ? typeName.Substring(typeName.LastIndexOf('.') + 1) : typeName;
                            fileNames.Add(sheetName + ".cs");
                            fileNames.Add("Tb" + sheetName + ".cs");
                        }
                    }

                    fileNames.Add(managerName + ".cs");
                    return fileNames;
                }

                /// <summary>
                /// 在单元设置列表中查找与指定相对路径匹配的 UnitSetting。
                /// </summary>
                /// <param name="units">单元设置列表。</param>
                /// <param name="relativePath">相对路径。</param>
                /// <returns>匹配的 UnitSetting，未找到时返回 null。</returns>
                public static IDataTableUnitSetting FindUnitSetting(IReadOnlyList<IDataTableUnitSetting> units, string relativePath)
                {
                    if (units == null)
                    {
                        return null;
                    }

                    for (int i = 0; i < units.Count; i++)
                    {
                        if (units[i].SourcePath == relativePath)
                        {
                            return units[i];
                        }
                    }

                    return null;
                }

                /// <summary>
                /// 获取预过滤器临时目录路径（_temp/ 子目录）。
                /// Config / Network 模块在导出前使用 PreFilter 将过滤后的文件写入此目录。
                /// </summary>
                /// <param name="regionDirPath">地域数据源目录路径。</param>
                /// <returns>_temp/ 目录完整路径。</returns>
                public static string GetPreFilterTempDirPath(string regionDirPath)
                {
                    return Util.SysIO.Path.Combine(regionDirPath, "_temp");
                }

                /// <summary>
                /// 获取 Luban 自定义模板目录列表：default 在前（低优先级），per-module 在后（高优先级）。
                /// Luban 对多个 --customTemplateDir 按后注册优先查找，因此 per-module 必须排在数组末尾。
                /// 每个目录须符合 Luban 期望格式：目录下含 {code-target}/ 子目录（如 cs-newtonsoft-json/）。
                /// 路径解析优先级：UPM PackageCache 物理路径 > Assets/Framework/ 本地开发路径。
                /// </summary>
                /// <param name="targetName">模块名称（如 "sound" / "ui"），为空时只返回 default 目录。</param>
                /// <returns>自定义模板目录列表，均不存在时返回 null。</returns>
                public static string[] GetLubanCustomTemplateDirs(string targetName)
                {
                    List<string> dirs = new List<string>();
                    string templateRoot = ResolveNovaFrameworkTemplatePath();

                    string defaultDir = GetExistingDir(templateRoot, "default");
                    if (defaultDir != null)
                    {
                        dirs.Add(defaultDir);
                    }

                    if (!string.IsNullOrEmpty(targetName))
                    {
                        string moduleDir = GetExistingDir(templateRoot, targetName);
                        if (moduleDir != null)
                        {
                            dirs.Add(moduleDir);
                        }
                    }

                    return dirs.Count > 0 ? dirs.ToArray() : null;
                }

                /// <summary>
                /// 解析 Nova Framework 包的 Templates/Luban 物理根路径。
                /// 优先通过 PackageInfo.FindForAssetPath 获取 UPM 包的 resolvedPath（正确处理 Library/PackageCache/ 等场景），
                /// 回退到 Assets/Framework/Templates/Luban 本地开发路径。
                /// </summary>
                /// <returns>Templates/Luban 物理目录路径，找不到可用路径时返回 null。</returns>
                private static string ResolveNovaFrameworkTemplatePath()
                {
                    PackageInfo packageInfo = PackageInfo.FindForAssetPath("Packages/com.solotopia.nova.framework");
                    if (packageInfo != null)
                    {
                        string upmTemplatePath = Util.SysIO.Path.Combine(packageInfo.resolvedPath, "Templates/Luban");
                        if (Util.SysIO.Directory.Exists(upmTemplatePath))
                        {
                            return upmTemplatePath;
                        }
                    }

                    string assetsTemplatePath = Util.SysIO.Path.GetFullPath("Assets/Framework/Templates/Luban");
                    if (Util.SysIO.Directory.Exists(assetsTemplatePath))
                    {
                        return assetsTemplatePath;
                    }

                    return null;
                }

                /// <summary>
                /// 拼接模板根路径与子目录名，存在时返回完整路径，否则返回 null。
                /// </summary>
                /// <param name="templateRoot">Templates/Luban 根路径，为 null 时直接返回 null。</param>
                /// <param name="subDir">子目录名称（如 "default" / "sound"）。</param>
                /// <returns>存在的完整目录路径，或 null。</returns>
                private static string GetExistingDir(string templateRoot, string subDir)
                {
                    if (templateRoot == null)
                    {
                        return null;
                    }

                    string fullPath = Util.SysIO.Path.Combine(templateRoot, subDir);
                    return Util.SysIO.Directory.Exists(fullPath) ? fullPath : null;
                }
            }
        }
    }
}
