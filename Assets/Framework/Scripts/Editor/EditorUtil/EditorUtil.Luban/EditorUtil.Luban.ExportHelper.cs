/***************************************************************
 * (c) copyright 2026 - 2030, Solotopia
 * All Rights Reserved.
 * -------------------------------------------------------------
 * filename:  EditorUtil.Luban.ExportHelper.cs
 * author:    taoye
 * created:   2026/4/25
 * descrip:   Luban 导出辅助工具：构建导出上下文、生成关联文件名、查找单元设置
 ***************************************************************/

using System;
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
                private const string c_DevelopmentTemplateRoot = "Assets/Framework/Templates/Luban";
                private const string c_PackageTemplateRoot = "Packages/com.solotopia.nova.framework/Templates/Luban";

                /// <summary>
                /// 根据 Nova 内置 Profile 构建标准 Luban 导出上下文。
                /// </summary>
                /// <param name="sourceDirPath">数据源根目录路径。</param>
                /// <param name="settings">数据表设置。</param>
                /// <param name="profile">模块固定导出配置。</param>
                /// <returns>初始化完毕的导出上下文。</returns>
                internal static LubanExportContext BuildExportContext(string sourceDirPath, IDataTableSettings settings, LubanExportProfile profile)
                {
                    if (profile == null)
                    {
                        throw new ArgumentNullException(nameof(profile));
                    }

                    string configDir = ConfigSyncer.GetConfigDirPath(sourceDirPath);
                    string confPath = Util.SysIO.Path.Combine(configDir, ConfigSyncer.c_LubanConfFileName);
                    string tablesXmlPath = Util.SysIO.Path.Combine(configDir, ConfigSyncer.c_TablesXmlFileName);
                    string topModule = EditorUtil.Config.RuntimeProvider.GetNamespace();

                    return new LubanExportContext
                    {
                        SourceDirPath = sourceDirPath,
                        ConfPath = confPath,
                        TargetName = profile.TargetName,
                        ManagerName = profile.ManagerName,
                        TopModule = topModule,
                        CustomTemplateDirs = GetLubanCustomTemplateDirs(profile.TemplateKey),
                        TablesXmlPath = tablesXmlPath,
                        Settings = settings,
                        Profile = profile,
                        MinHeaderRowCount = profile.MinHeaderRowCount,
                    };
                }

                internal static HashSet<string> BuildRelevantFileNames(
                    LubanSchemaManifest manifest,
                    string sourcePath,
                    string managerName)
                {
                    LubanSchemaUnit unit = manifest.ResolveUnit(sourcePath);
                    var fileNames = new HashSet<string>();
                    foreach (LubanSchemaTable table in unit.Tables)
                    {
                        fileNames.Add(table.ValueType + ".cs");
                        fileNames.Add(table.Name + ".cs");
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
                /// 把 Nova Framework 模板逻辑路径解析为当前安装形态下的物理路径。
                /// 开发态使用 Assets/Framework，消费态使用 PackageInfo.resolvedPath；
                /// 项目自定义目录保持原值。
                /// </summary>
                /// <param name="configuredDirectory">Inspector 中保存的模板目录。</param>
                /// <returns>可交给 Luban CLI 的模板目录。</returns>
                internal static string ResolveCustomTemplateDirectory(string configuredDirectory)
                {
                    if (string.IsNullOrWhiteSpace(configuredDirectory))
                    {
                        return configuredDirectory;
                    }

                    string normalized = configuredDirectory.Replace('\\', '/').TrimEnd('/');
                    string relative = TryGetFrameworkTemplateRelativePath(normalized);
                    if (relative == null)
                    {
                        return configuredDirectory;
                    }

                    string templateRoot = ResolveNovaFrameworkTemplatePath();
                    if (string.IsNullOrEmpty(templateRoot))
                    {
                        return configuredDirectory;
                    }

                    return relative.Length == 0
                        ? templateRoot
                        : Util.SysIO.Path.Combine(templateRoot, relative);
                }

                private static string TryGetFrameworkTemplateRelativePath(string configuredDirectory)
                {
                    if (string.Equals(configuredDirectory, c_DevelopmentTemplateRoot, StringComparison.Ordinal) ||
                        string.Equals(configuredDirectory, c_PackageTemplateRoot, StringComparison.Ordinal))
                    {
                        return string.Empty;
                    }

                    string developmentPrefix = c_DevelopmentTemplateRoot + "/";
                    if (configuredDirectory.StartsWith(developmentPrefix, StringComparison.Ordinal))
                    {
                        return configuredDirectory.Substring(developmentPrefix.Length);
                    }

                    string packagePrefix = c_PackageTemplateRoot + "/";
                    return configuredDirectory.StartsWith(packagePrefix, StringComparison.Ordinal)
                        ? configuredDirectory.Substring(packagePrefix.Length)
                        : null;
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
