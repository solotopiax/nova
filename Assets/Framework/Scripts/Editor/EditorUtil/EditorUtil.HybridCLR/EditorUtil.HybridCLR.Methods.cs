/***************************************************************
 * (c) copyright 2026 - 2030, Solotopia
 * All Rights Reserved.
 * -------------------------------------------------------------
 * filename:  EditorUtil.HybridCLR.Methods.cs
 * author:    taoye
 * created:   2026/5/8
 * descrip:   HybridCLR Pipeline 自动化工具 —— 私有方法
 ***************************************************************/

using System;
using System.Collections.Generic;
using System.IO;
using System.Xml;
using HybridCLR.Editor;
using NovaFramework.Runtime;
using UnityEditor;
using Path = System.IO.Path;

namespace NovaFramework.Editor
{
    public static partial class EditorUtil
    {
        public static partial class HybridCLR
        {
            /// <summary>
            /// 读取当前激活 ConfigMaster；未绑定时抛异常，提示用户先切到目标 sample 或在 ConfigWindow 绑定。
            /// </summary>
            /// <returns>当前激活的 ConfigMasterSO。</returns>
            private static ConfigMasterSO ResolveActiveMasterOrThrow()
            {
                ConfigMasterSO master = Config.WorkspaceActive.Get();
                if (master != null)
                {
                    return master;
                }

                throw new InvalidOperationException("[HybridCLR] 未找到激活 ConfigMasterSO。请先打开目标 Demo 场景，或在 ConfigWindow 绑定 ConfigMaster.asset。");
            }

            /// <summary>
            /// 按当前 ConfigMaster 坐标解析最终生效的 HybridCLR 配置。
            /// </summary>
            /// <param name="master">当前激活的 ConfigMasterSO。</param>
            /// <returns>当前坐标下生效的 HybridCLR 配置。</returns>
            private static Config.DimensionalResolver.HybridCLRResult ResolveHybridCLRForCurrentCoord(ConfigMasterSO master)
            {
                return Config.DimensionalResolver.ResolveHybridCLR(master, master.CurrentPlatform, master.CurrentChannel, master.CurrentDevelopMode);
            }

            /// <summary>
            /// 读取 AOT 元数据 DLL 列表，检查 Assets/link.xml 是否包含每个条目的保留项，缺失则追加。
            /// </summary>
            /// <param name="aotEntries">AOT 元数据 DLL 条目列表（主配置视图，使用 AssetLocation 生成 link.xml 保留名）。</param>
            private static void ValidateAndPatchLinkXml(IReadOnlyList<DllMasterAssetEntry> aotEntries)
            {
                string fullPath = Path.GetFullPath(Path.Combine(SettingsUtil.ProjectDir, ResolveLinkXmlPath()));

                XmlDocument doc = new XmlDocument();
                XmlElement linker;
                bool justCreated = false;

                if (!File.Exists(fullPath))
                {
                    // 自动创建空的 link.xml 骨架：<?xml?> + 根节点 <linker>
                    Directory.CreateDirectory(Path.GetDirectoryName(fullPath));
                    doc.AppendChild(doc.CreateXmlDeclaration("1.0", "utf-8", null));
                    linker = doc.CreateElement("linker");
                    doc.AppendChild(linker);
                    justCreated = true;
                    Log.Debug(LogTag.Editor, "[HybridCLR Pipeline][link.xml] 未找到，已自动创建：{0}", fullPath);
                }
                else
                {
                    doc.Load(fullPath);
                    linker = doc.DocumentElement;
                    if (linker == null || linker.Name != "linker")
                    {
                        throw new InvalidOperationException($"link.xml 格式错误：根节点不是 <linker>，路径：{fullPath}");
                    }
                }

                // 收集已有的 fullname 集合
                HashSet<string> existing = new HashSet<string>(StringComparer.Ordinal);
                foreach (XmlNode child in linker.ChildNodes)
                {
                    if (child is XmlElement elem && elem.Name == "assembly")
                    {
                        string fullname = elem.GetAttribute("fullname");
                        if (!string.IsNullOrEmpty(fullname)) existing.Add(fullname);
                    }
                }

                // 缺失项追加。Unity link.xml 规范：fullname 是 assembly 逻辑名，严禁带 .dll 后缀。
                // 面板填写的 AssetLocation 可能已带 .dll（与磁盘文件名一致），写入前统一剥离。
                bool patched = false;
                foreach (DllMasterAssetEntry entry in aotEntries)
                {
                    string fullname = StripDllSuffix(entry.AssetLocation);
                    if (existing.Contains(fullname)) continue;

                    XmlElement newElem = doc.CreateElement("assembly");
                    newElem.SetAttribute("fullname", fullname);
                    newElem.SetAttribute("preserve", "all");
                    linker.AppendChild(newElem);
                    existing.Add(fullname);
                    patched = true;
                    Log.Debug(LogTag.Editor, "[HybridCLR Pipeline][link.xml] 追加保留项：{0}", fullname);
                }

                if (patched || justCreated)
                {
                    XmlWriterSettings writerSettings = new XmlWriterSettings { Indent = true, IndentChars = "  ", OmitXmlDeclaration = false };
                    using XmlWriter writer = XmlWriter.Create(fullPath, writerSettings);
                    doc.Save(writer);
                    Log.Debug(LogTag.Editor, "[HybridCLR Pipeline][link.xml] 已写回：{0}", fullPath);
                }
                else
                {
                    Log.Debug(LogTag.Editor, "[HybridCLR Pipeline][link.xml] 无需修改，所有 AOT 条目已存在。");
                }
            }

            /// <summary>
            /// 自动补齐 sample ConfigMaster 对应的 DLL 副本。
            /// 仅当 master 位于 Assets/Samples/ 下且目标文件缺失/过期时执行；失败只记 Warning。
            /// </summary>
            /// <param name="master">当前激活的 ConfigMasterSO。</param>
            private static void TryAutoSyncSampleDlls(ConfigMasterSO master)
            {
                string masterPath = AssetDatabase.GetAssetPath(master);
                if (string.IsNullOrEmpty(masterPath) || !masterPath.StartsWith("Assets/Samples/", StringComparison.Ordinal))
                {
                    return;
                }

                var hybrid = ResolveHybridCLRForCurrentCoord(master);
                bool needsAot = NeedsCopy(hybrid.AotMetadataDlls);
                bool needsGame = NeedsCopy(hybrid.GameDlls);
                if (!needsAot && !needsGame)
                {
                    return;
                }

                try
                {
                    if (needsAot)
                    {
                        CopyDllEntries(hybrid.AotMetadataDlls, "AOT 元数据");
                    }

                    if (needsGame)
                    {
                        CopyDllEntries(hybrid.GameDlls, "业务 DLL");
                    }

                    Log.Debug(LogTag.Editor, "[HybridCLR AutoSync] 已补齐 sample DLL 副本：{0}", masterPath);
                }
                catch (Exception e)
                {
                    Log.Warning(LogTag.Editor, "[HybridCLR AutoSync] sample DLL 自动同步失败：{0}\n{1}", masterPath, e.Message);
                }
            }

            /// <summary>
            /// 通用 DLL 条目拷贝逻辑。
            /// 源路径 = SettingsUtil.ProjectDir + 解析占位符(entry.SourceLocation)（项目根相对的具体文件路径，含文件名与扩展名；支持 {ActiveBuildTarget} 占位符）。
            /// 目标路径 = SettingsUtil.ProjectDir + 解析占位符(entry.TargetLocation)（项目根相对的具体文件路径，含文件名与扩展名，如 .dll.bytes）。
            /// 源/目标字段为空字符串时，视为配置缺失，收集到 missing 列表后统一抛 FileNotFoundException。
            /// </summary>
            /// <param name="entries">DLL 主配置条目列表（含源/目标路径）。</param>
            /// <param name="tag">日志标识（如"AOT 元数据"）。</param>
            private static void CopyDllEntries(IReadOnlyList<DllMasterAssetEntry> entries, string tag)
            {
                string projectRoot = SettingsUtil.ProjectDir;
                List<string> missing = new List<string>();

                foreach (DllMasterAssetEntry entry in entries)
                {
                    if (string.IsNullOrEmpty(entry.SourceLocation) || string.IsNullOrEmpty(entry.TargetLocation))
                    {
                        missing.Add($"[配置缺失] SourceLocation='{entry.SourceLocation}' TargetLocation='{entry.TargetLocation}'");
                        continue;
                    }
                    string srcFile = Path.GetFullPath(Path.Combine(projectRoot, ResolvePathPlaceholders(entry.SourceLocation)));
                    if (!File.Exists(srcFile)) missing.Add(srcFile);
                }

                if (missing.Count > 0)
                {
                    string list = string.Join("\n  ", missing);
                    throw new FileNotFoundException($"[HybridCLR Pipeline][{tag}] 以下源文件不存在或配置缺失，请检查配置并执行编译步骤：\n  {list}");
                }

                foreach (DllMasterAssetEntry entry in entries)
                {
                    string srcFile = Path.GetFullPath(Path.Combine(projectRoot, ResolvePathPlaceholders(entry.SourceLocation)));
                    string dstFile = Path.GetFullPath(Path.Combine(projectRoot, ResolvePathPlaceholders(entry.TargetLocation)));
                    string dstDir = Path.GetDirectoryName(dstFile);
                    Directory.CreateDirectory(dstDir);
                    File.Copy(srcFile, dstFile, overwrite: true);
                    Log.Debug(LogTag.Editor, "[HybridCLR Pipeline][{0}] 已拷贝：{1} → {2}", tag, srcFile, dstFile);

                    // 逐文件同步重导：路径分隔符统一成 /，仅 Assets/ 下的目标路径触发 ImportAsset
                    string assetRelative = ResolvePathPlaceholders(entry.TargetLocation).Replace('\\', '/');
                    if (assetRelative.StartsWith("Assets/", System.StringComparison.Ordinal))
                    {
                        AssetDatabase.ImportAsset(assetRelative, ImportAssetOptions.ForceSynchronousImport);
                        Log.Debug(LogTag.Editor, "[HybridCLR Pipeline][{0}] 已同步重导：{1}", tag, assetRelative);
                    }
                }
            }

            /// <summary>
            /// 判断给定 DLL 条目集合是否存在缺失或过期目标文件。
            /// 任一条目源/目标路径缺失、源文件不存在、目标文件不存在或源时间晚于目标时间，都视为需要重拷贝。
            /// </summary>
            /// <param name="entries">待检查的 DLL 条目集合。</param>
            /// <returns>存在任一待同步条目时返回 true。</returns>
            private static bool NeedsCopy(IReadOnlyList<DllMasterAssetEntry> entries)
            {
                if (entries == null || entries.Count == 0)
                {
                    return false;
                }

                string projectRoot = SettingsUtil.ProjectDir;
                foreach (DllMasterAssetEntry entry in entries)
                {
                    if (string.IsNullOrEmpty(entry.SourceLocation) || string.IsNullOrEmpty(entry.TargetLocation))
                    {
                        return true;
                    }

                    string srcFile = Path.GetFullPath(Path.Combine(projectRoot, ResolvePathPlaceholders(entry.SourceLocation)));
                    string dstFile = Path.GetFullPath(Path.Combine(projectRoot, ResolvePathPlaceholders(entry.TargetLocation)));

                    if (!File.Exists(srcFile) || !File.Exists(dstFile))
                    {
                        return true;
                    }

                    if (File.GetLastWriteTimeUtc(srcFile) > File.GetLastWriteTimeUtc(dstFile))
                    {
                        return true;
                    }
                }

                return false;
            }

            /// <summary>
            /// 解析路径占位符。
            /// 当前支持的占位符：
            /// {ActiveBuildTarget} → EditorUserBuildSettings.activeBuildTarget 的字符串名称（如 Android、iOS、StandaloneOSX）。
            /// 输入为 null/空时原样返回。
            /// </summary>
            /// <param name="raw">原始相对路径（可能含占位符）。</param>
            /// <returns>占位符替换后的相对路径。</returns>
            internal static string ResolvePathPlaceholders(string raw)
            {
                if (string.IsNullOrEmpty(raw))
                {
                    return raw;
                }

                string activeBuildTarget = EditorUserBuildSettings.activeBuildTarget.ToString();
                return raw.Replace("{ActiveBuildTarget}", activeBuildTarget);
            }

            /// <summary>
            /// 剥离 AssetLocation 末尾的 ".dll" 后缀（大小写不敏感）。
            /// Unity link.xml 规范要求 fullname 是 assembly 逻辑名，不能带文件扩展名。
            /// </summary>
            /// <param name="assetLocation">原始 AssetLocation。</param>
            /// <returns>去除 .dll 后缀后的逻辑名。</returns>
            private static string StripDllSuffix(string assetLocation)
            {
                if (string.IsNullOrEmpty(assetLocation)) return assetLocation;
                if (assetLocation.EndsWith(".dll", StringComparison.OrdinalIgnoreCase))
                {
                    return assetLocation.Substring(0, assetLocation.Length - ".dll".Length);
                }
                return assetLocation;
            }

            /// <summary>
            /// 解析 link.xml 目标路径：优先读取 ConfigMasterSO.LinkXmlTargetPath，
            /// 字段为空或找不到 ConfigMasterSO 时回退默认值 "Assets/link.xml"。
            /// </summary>
            /// <returns>项目根相对的 link.xml 文件路径。</returns>
            private static string ResolveLinkXmlPath()
            {
                ConfigMasterSO master = Config.WorkspaceActive.Get();
                if (master != null)
                {
                    var hybrid = ResolveHybridCLRForCurrentCoord(master);
                    if (!string.IsNullOrEmpty(hybrid.LinkXmlTargetPath))
                    {
                        return hybrid.LinkXmlTargetPath;
                    }
                }
                return "Assets/link.xml";
            }

        }
    }
}
