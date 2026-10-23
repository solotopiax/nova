/***************************************************************
 * (c) copyright 2026 - 2030, Solotopia
 * All Rights Reserved.
 * filename:  EditorUtil.TrackRegistry.AgentSnapshot.cs
 * author:    Codex
 * created:   2026/10/13
 * descrip:   复用登记扫描规则的 Agent 只读源快照与输出验证
 ***************************************************************/
using System;
using System.Collections.Generic;
using System.IO;
using IOPath = System.IO.Path;
using System.IO.Compression;
using System.Linq;
using System.Threading;
using System.Xml.Linq;
using NovaFramework.Runtime;
using PackageInfo = UnityEditor.PackageManager.PackageInfo;

namespace NovaFramework.Editor
{
    public static partial class EditorUtil
    {
        public static partial class TrackRegistry
        {
            [Serializable]
            internal sealed class AgentFileState
            {
                public string path;
                public bool exists;
                public string sha256;
            }

            [Serializable]
            internal sealed class AgentPackageState
            {
                public string name;
                public string version;
                public string assetPath;
                public string resolvedPath;
                public string source;
            }

            [Serializable]
            internal sealed class AgentRegistrySnapshot
            {
                public string projectRootHash;
                public AgentPackageState[] registeredPackages;
                public AgentFileState[] sources;
                public AgentFileState[] metadata;
            }

            [Serializable]
            internal sealed class AgentSheetState
            {
                public string name;
                public string rowsSha256;
                public bool eventSheet;
            }

            /// <summary>复用汇总器真实扫描规则，冻结所有工作簿、meta、包清单和解析元数据；不写任何文件。</summary>
            internal static AgentRegistrySnapshot CaptureAgentSnapshot(string projectRoot, CancellationToken token)
            {
                string root = IOPath.GetFullPath(projectRoot).TrimEnd(IOPath.DirectorySeparatorChar, IOPath.AltDirectorySeparatorChar);
                TrackWorkbookScanContext context = CreateScanContext(root);
                var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
                var paths = new List<string>();
                foreach (TrackWorkbookScanRule rule in s_WorkbookScanRules)
                foreach (string path in EnumerateTrackWorkbooks(context, rule))
                {
                    token.ThrowIfCancellationRequested();
                    if (TryRegisterWorkbookPath(seen, path)) paths.Add(NormalizeWorkbookPath(path));
                }
                if (paths.Count == 0) throw new InvalidOperationException("未找到正式 Tracks 登记源。");
                AgentPackageState[] packages = IsCurrentUnityProject(root)
                    ? PackageInfo.GetAllRegisteredPackages().OrderBy(p => p.name, StringComparer.Ordinal).Select(p => new AgentPackageState
                    {
                        name = p.name ?? "", version = p.version ?? "", assetPath = p.assetPath ?? "",
                        resolvedPath = string.IsNullOrEmpty(p.resolvedPath) ? "" : IOPath.GetFullPath(p.resolvedPath).Replace('\\', '/'),
                        source = p.source.ToString(),
                    }).ToArray()
                    : Array.Empty<AgentPackageState>();
                var metadata = new List<string>
                {
                    IOPath.Combine(root, "Packages/manifest.json"), IOPath.Combine(root, "Packages/packages-lock.json"),
                };
                metadata.AddRange(paths.Select(p => p + ".meta"));
                metadata.AddRange(context.UpmPackageRoots.Concat(context.PackageRoots)
                    .Distinct(StringComparer.Ordinal).Select(p => IOPath.Combine(p, "package.json")));
                return new AgentRegistrySnapshot
                {
                    projectRootHash = GenerateActionCommon.ComputeTextHash(root), registeredPackages = packages,
                    sources = paths.Select(p => CaptureAgentFile(p, token)).ToArray(),
                    metadata = metadata.Distinct(StringComparer.Ordinal).Select(p => CaptureAgentFile(p, token)).ToArray(),
                };
            }

            /// <summary>冻结正式源行内容及页序，提前发现跨源重名，不以已有输出作为登记依据。</summary>
            internal static AgentSheetState[] CaptureAgentSheets(string projectRoot)
            {
                List<TrackSheet> sheets = BuildSheets(IOPath.GetFullPath(projectRoot));
                var names = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
                return sheets.Select(sheet =>
                {
                    if (sheet.Name.Contains("$") || !names.Add(sheet.Name))
                        throw new InvalidOperationException("登记源页签含 $ 或跨源重名。");
                    return new AgentSheetState
                    {
                        name = sheet.Name, rowsSha256 = GenerateActionCommon.ComputeTextHash(Util.Json.Serialize(sheet.Rows)),
                        eventSheet = sheet.Rows.Count > 0 && sheet.Rows[0].Count > 0 && sheet.Rows[0][0] == "事件名",
                    };
                }).ToArray();
            }

            /// <summary>校验完整输出页序、源行内容和 Network 单色样式；不修改源或生成物。</summary>
            internal static void VerifyAgentOutput(string output, AgentSheetState[] expected)
            {
                Dictionary<string, List<IReadOnlyList<string>>> rows = Excel.ReadAllSheets(output);
                if (!rows.Keys.SequenceEqual(expected.Select(s => s.name), StringComparer.Ordinal))
                    throw new InvalidOperationException("汇总页序或名称与正式登记源不同。");
                foreach (AgentSheetState sheet in expected)
                    if (GenerateActionCommon.ComputeTextHash(Util.Json.Serialize(NormalizeRows(rows[sheet.name]))) != sheet.rowsSha256)
                        throw new InvalidOperationException("汇总内容与正式登记源不同。");
                using (ZipArchive zip = ZipFile.OpenRead(output))
                {
                    XDocument styles;
                    using (Stream input = zip.GetEntry("xl/styles.xml").Open()) styles = XDocument.Load(input);
                    XNamespace ns = "http://schemas.openxmlformats.org/spreadsheetml/2006/main";
                    XElement[] fills = styles.Root.Element(ns + "fills").Elements(ns + "fill").ToArray();
                    XElement[] xfs = styles.Root.Element(ns + "cellXfs").Elements(ns + "xf").ToArray();
                    for (int index = 0; index < expected.Length; index++)
                    {
                        XDocument worksheet;
                        using (Stream input = zip.GetEntry("xl/worksheets/sheet" + (index + 1) + ".xml").Open()) worksheet = XDocument.Load(input);
                        foreach (XElement cell in worksheet.Descendants(ns + "c"))
                        {
                            int style = int.Parse((string)cell.Attribute("s") ?? "0");
                            int fill = int.Parse((string)xfs[style].Attribute("fillId") ?? "0");
                            string color = (string)fills[fill].Element(ns + "patternFill")?.Element(ns + "fgColor")?.Attribute("rgb");
                            int row = int.Parse(new string(((string)cell.Attribute("r")).Where(char.IsDigit).ToArray()));
                            if (expected[index].eventSheet && color != (row == 1 ? "FF1F4E78" : "FFEAF4EC"))
                                throw new InvalidOperationException("汇总事件页未采用 Network 单色样式。");
                        }
                        if (expected[index].eventSheet && worksheet.Descendants(ns + "mergeCell").Any(m =>
                            !System.Text.RegularExpressions.Regex.IsMatch((string)m.Attribute("ref") ?? "", @"^([ABC])[0-9]+:\1[0-9]+$")))
                            throw new InvalidOperationException("汇总事件页合并超出 A-C 同列。");
                    }
                }
            }

            /// <summary>冻结文件存在状态与摘要，拒绝路径中任何符号链接以保护来源身份。</summary>
            internal static AgentFileState CaptureAgentFile(string path, CancellationToken token)
            {
                string full = IOPath.GetFullPath(path);
                for (string current = full; !string.IsNullOrEmpty(current); current = IOPath.GetDirectoryName(current))
                    if ((File.Exists(current) || Directory.Exists(current)) && (File.GetAttributes(current) & FileAttributes.ReparsePoint) != 0)
                        throw new InvalidOperationException("Tracks 来源或元数据含符号链接。");
                return new AgentFileState
                {
                    path = full.Replace('\\', '/'), exists = File.Exists(full),
                    sha256 = File.Exists(full) ? GenerateActionCommon.ComputeFileHash(full, token) : null,
                };
            }
        }
    }
}
