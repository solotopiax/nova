/***************************************************************
 * (c) copyright 2026 - 2030, Solotopia
 * All Rights Reserved.
 * -------------------------------------------------------------
 * filename:  KitsViewWindow.Methods.cs
 * author:    taoye
 * created:   2026/4/22
 * descrip:   KitsView 窗口私有方法（数据收集与 GUI 绘制）
 ***************************************************************/

using System.Collections.Generic;
using Newtonsoft.Json.Linq;
using NovaFramework.Runtime;
using UnityEditor;
using UnityEngine;

namespace NovaFramework.Editor
{
    internal sealed partial class KitsViewWindow : EditorWindow
    {
        /// <summary>
        /// 从 packages-lock.json 读取所有已解析依赖（含间接依赖），筛选 Kit 前缀包并定位物理路径后构建条目列表。
        /// manifest.json 用于识别开发者模式（file: 本地引用），其余走消费者模式（PackageCache）。
        /// </summary>
        private void CollectKitEntries()
        {
            m_KitEntries = new List<KitEntry>();

            string projectRoot = Util.SysIO.Path.GetFullPath(Util.SysIO.Path.Combine(Application.dataPath, ".."));

            // 从 packages-lock.json 获取所有已解析的包名（含间接依赖）
            string lockPath = Util.SysIO.Path.Combine(projectRoot, c_PackagesLockPath);
            if (!Util.SysIO.File.Exists(lockPath))
            {
                return;
            }

            string lockText = Util.SysIO.File.ReadAllTextSync(lockPath);
            List<string> resolvedPackages = ParseLockPackageNames(lockText);

            // 从 manifest.json 获取直接依赖的 source 映射（用于区分 file: 开发者模式）
            Dictionary<string, string> manifestSources = new Dictionary<string, string>();
            string manifestPath = Util.SysIO.Path.Combine(projectRoot, c_ManifestPath);
            if (Util.SysIO.File.Exists(manifestPath))
            {
                string manifestText = Util.SysIO.File.ReadAllTextSync(manifestPath);
                List<KeyValuePair<string, string>> deps = new List<KeyValuePair<string, string>>();
                ParseManifestDependencies(manifestText, deps);
                for (int i = 0; i < deps.Count; i++)
                {
                    manifestSources[deps[i].Key] = deps[i].Value;
                }
            }

            string packageCacheDir = Util.SysIO.Path.Combine(projectRoot, "Library/PackageCache");

            for (int i = 0; i < resolvedPackages.Count; i++)
            {
                string packageName = resolvedPackages[i];
                if (!packageName.StartsWith(c_KitPackagePrefix))
                {
                    continue;
                }

                string source;
                manifestSources.TryGetValue(packageName, out source);
                string packageDir = ResolvePackageDir(projectRoot, packageCacheDir, packageName, source);
                if (string.IsNullOrEmpty(packageDir))
                {
                    continue;
                }

                string packageJsonPath = Util.SysIO.Path.Combine(packageDir, "package.json");
                if (!Util.SysIO.File.Exists(packageJsonPath))
                {
                    continue;
                }

                KitEntry entry = ParsePackageJson(packageJsonPath, packageDir);
                if (entry != null)
                {
                    m_KitEntries.Add(entry);
                }
            }

            m_KitEntries.Sort((a, b) => string.Compare(a.Name, b.Name, System.StringComparison.Ordinal));
        }

        /// <summary>
        /// 根据来源字符串解析包的物理绝对路径。
        /// file: 前缀视为开发者模式（相对 Packages/ 目录），null 或其他值在 PackageCache 中查找。
        /// </summary>
        /// <param name="projectRoot">项目根目录绝对路径。</param>
        /// <param name="packageCacheDir">Library/PackageCache 目录绝对路径。</param>
        /// <param name="packageName">包名（如 com.solotopia.nova.framework.kit.network）。</param>
        /// <param name="source">manifest.json 中的 value（file: 路径或版本号），间接依赖为 null。</param>
        /// <returns>包目录绝对路径，无法定位时返回空字符串。</returns>
        private string ResolvePackageDir(string projectRoot, string packageCacheDir, string packageName, string source)
        {
            if (source != null && source.StartsWith("file:"))
            {
                string relativePath = source.Substring(5);
                return Util.SysIO.Path.GetFullPath(Util.SysIO.Path.Combine(projectRoot, "Packages", relativePath));
            }

            if (!Util.SysIO.Directory.Exists(packageCacheDir))
            {
                return string.Empty;
            }

            string[] matches = Util.SysIO.Directory.GetDirectories(packageCacheDir, packageName + "@*", System.IO.SearchOption.TopDirectoryOnly);
            if (matches.Length > 0)
            {
                return matches[0];
            }

            return string.Empty;
        }

        /// <summary>
        /// 从 manifest.json 全文中解析 dependencies 块的所有键值对。
        /// </summary>
        /// <param name="manifestText">manifest.json 全文。</param>
        /// <param name="dependencies">输出的依赖键值对列表。</param>
        private void ParseManifestDependencies(string manifestText, List<KeyValuePair<string, string>> dependencies)
        {
            JObject root = Util.Json.Deserialize<JObject>(manifestText);
            JObject deps = root?["dependencies"] as JObject;
            if (deps == null)
            {
                return;
            }

            foreach (var prop in deps.Properties())
            {
                dependencies.Add(new KeyValuePair<string, string>(prop.Name, prop.Value.ToString()));
            }
        }

        /// <summary>
        /// 从 packages-lock.json 全文中提取 dependencies 下的所有顶层包名。
        /// </summary>
        /// <param name="lockText">packages-lock.json 全文。</param>
        /// <returns>顶层包名列表。</returns>
        private List<string> ParseLockPackageNames(string lockText)
        {
            var result = new List<string>();
            JObject root = Util.Json.Deserialize<JObject>(lockText);
            JObject deps = root?["dependencies"] as JObject;
            if (deps == null)
            {
                return result;
            }

            foreach (var prop in deps.Properties())
            {
                result.Add(prop.Name);
            }

            return result;
        }

        /// <summary>
        /// 解析 package.json 为 KitEntry。
        /// </summary>
        /// <param name="jsonPath">package.json 文件路径。</param>
        /// <param name="packageDir">包目录路径。</param>
        /// <returns>解析后的 KitEntry，失败返回 null。</returns>
        private KitEntry ParsePackageJson(string jsonPath, string packageDir)
        {
            string json = Util.SysIO.File.ReadAllTextSync(jsonPath);
            JObject root = Util.Json.Deserialize<JObject>(json);
            if (root == null)
            {
                return null;
            }

            KitEntry entry = new KitEntry
            {
                PackagePath = packageDir,
                Dependencies = new List<KeyValuePair<string, string>>(),
                ProtoFiles = new List<ProtoFileEntry>()
            };

            entry.Name = root["name"]?.ToString() ?? "";
            entry.DisplayName = root["displayName"]?.ToString() ?? "";
            entry.Version = root["version"]?.ToString() ?? "";
            entry.Description = root["description"]?.ToString() ?? "";

            JObject deps = root["dependencies"] as JObject;
            if (deps != null)
            {
                foreach (var prop in deps.Properties())
                {
                    entry.Dependencies.Add(new KeyValuePair<string, string>(prop.Name, prop.Value.ToString()));
                }
            }

            CollectProtoFiles(packageDir, entry.ProtoFiles);

            return entry;
        }

        /// <summary>
        /// 收集包目录下 Protos 子目录中的所有 .proto 文件。
        /// </summary>
        /// <param name="packageDir">包目录路径。</param>
        /// <param name="protoFiles">输出的 Proto 文件列表。</param>
        private void CollectProtoFiles(string packageDir, List<ProtoFileEntry> protoFiles)
        {
            string protosDir = Util.SysIO.Path.Combine(packageDir, "Nova", "Protos");
            if (!Util.SysIO.Directory.Exists(protosDir))
            {
                return;
            }

            string[] files = Util.SysIO.Directory.GetFiles(protosDir, "*.proto", System.IO.SearchOption.TopDirectoryOnly);
            for (int i = 0; i < files.Length; i++)
            {
                protoFiles.Add(new ProtoFileEntry
                {
                    FileName = Util.SysIO.Path.GetFileName(files[i]),
                    Content = Util.SysIO.File.ReadAllTextSync(files[i]),
                    IsExpanded = false
                });
            }
        }

        /// <summary>
        /// 绘制单个 Kit 条目（可折叠）。
        /// </summary>
        /// <param name="entry">Kit 条目数据。</param>
        private void DrawKitEntry(KitEntry entry)
        {
            EditorUtil.Draw.Layout.Vertical("box", () =>
            {
                entry.IsExpanded = EditorUtil.Draw.Foldout(ref entry.IsExpanded, entry.DisplayName ?? entry.Name, true, m_TitleStyle);

                if (!entry.IsExpanded)
                {
                    return;
                }

                EditorUtil.Draw.Space(4f);

                int savedIndent = EditorUtil.Draw.SaveIndentLevel();
                EditorUtil.Draw.SetIndentLevel(1);

                DrawInfoField("Name", entry.Name);
                DrawInfoField("Version", entry.Version);
                DrawInfoField("Description", entry.Description);

                EditorUtil.Draw.Space(4f);
                DrawDependencies(entry.Dependencies);

                if (entry.ProtoFiles.Count > 0)
                {
                    EditorUtil.Draw.Space(6f);
                    DrawProtoSection(entry.ProtoFiles);
                }

                EditorUtil.Draw.RestoreIndentLevel(savedIndent);
                EditorUtil.Draw.Space(2f);
            });

            EditorUtil.Draw.Space(2f);
        }

        /// <summary>
        /// 绘制单行信息字段（标签 + 值）。
        /// </summary>
        /// <param name="label">字段标签。</param>
        /// <param name="value">字段值。</param>
        private void DrawInfoField(string label, string value)
        {
            EditorUtil.Draw.Layout.Horizontal(() =>
            {
                EditorUtil.Draw.Label(label, m_LabelStyle, false, GUILayout.Width(c_LabelWidth));
                if (label == "Description")
                {
                    EditorUtil.Draw.Label(value ?? "", m_DescStyle, false, GUILayout.ExpandWidth(true));
                }
                else
                {
                    EditorUtil.Draw.Label(value ?? "", m_ValueStyle, false, GUILayout.ExpandWidth(true));
                }
            });
        }

        /// <summary>
        /// 绘制依赖列表区域。
        /// </summary>
        /// <param name="dependencies">依赖列表。</param>
        private void DrawDependencies(List<KeyValuePair<string, string>> dependencies)
        {
            EditorUtil.Draw.Layout.Horizontal(() =>
            {
                EditorUtil.Draw.Label("Dependencies", m_LabelStyle, false, GUILayout.Width(c_LabelWidth));
                if (dependencies == null || dependencies.Count == 0)
                {
                    EditorUtil.Draw.Label("(none)", m_ValueStyle, false);
                }
                else
                {
                    EditorUtil.Draw.Layout.Vertical(() =>
                    {
                        for (int i = 0; i < dependencies.Count; i++)
                        {
                            EditorUtil.Draw.Label(dependencies[i].Key + "  " + dependencies[i].Value, m_DependencyStyle, false);
                        }
                    });
                }
            });
        }

        /// <summary>
        /// 绘制 Proto 协议文件区域。
        /// </summary>
        /// <param name="protoFiles">Proto 文件列表。</param>
        private void DrawProtoSection(List<ProtoFileEntry> protoFiles)
        {
            EditorUtil.Draw.Label("Proto Files", m_SectionTitleStyle, false);
            EditorUtil.Draw.Space(2f);

            int savedIndent = EditorUtil.Draw.SaveIndentLevel();
            EditorUtil.Draw.SetIndentLevel(2);

            for (int i = 0; i < protoFiles.Count; i++)
            {
                DrawProtoFile(protoFiles[i]);
            }

            EditorUtil.Draw.RestoreIndentLevel(savedIndent);
        }

        /// <summary>
        /// 绘制单个 Proto 文件（可折叠展示内容）。
        /// </summary>
        /// <param name="proto">Proto 文件条目。</param>
        private void DrawProtoFile(ProtoFileEntry proto)
        {
            proto.IsExpanded = EditorUtil.Draw.Foldout(ref proto.IsExpanded, proto.FileName, true, m_ProtoFileNameStyle);

            if (!proto.IsExpanded)
            {
                return;
            }

            EditorUtil.Draw.Layout.Vertical("HelpBox", () =>
            {
                EditorUtil.Draw.Space(4f);
                EditorGUILayout.SelectableLabel(proto.Content, m_ProtoContentStyle, GUILayout.Height(CalculateProtoHeight(proto.Content)));
                EditorUtil.Draw.Space(4f);
            });

            EditorUtil.Draw.Space(2f);
        }

        /// <summary>
        /// 根据内容行数计算 Proto 展示区域高度。
        /// </summary>
        /// <param name="content">Proto 文件内容。</param>
        /// <returns>计算后的高度像素值。</returns>
        private float CalculateProtoHeight(string content)
        {
            if (string.IsNullOrEmpty(content))
            {
                return 40f;
            }

            int lineCount = 1;
            for (int i = 0; i < content.Length; i++)
            {
                if (content[i] == '\n')
                {
                    lineCount++;
                }
            }

            return Mathf.Max(40f, lineCount * 15f + 10f);
        }

        /// <summary>
        /// 延迟初始化自定义 GUIStyle。
        /// </summary>
        private void EnsureStyles()
        {
            if (m_TitleStyle != null)
            {
                return;
            }

            m_TitleStyle = new GUIStyle(EditorStyles.foldout)
            {
                fontSize = 13,
                fontStyle = FontStyle.Bold,
                fixedHeight = 24f
            };

            m_LabelStyle = new GUIStyle(EditorStyles.boldLabel)
            {
                alignment = TextAnchor.MiddleLeft,
                normal = { textColor = new Color(0.7f, 0.85f, 1f) }
            };

            m_ValueStyle = new GUIStyle(EditorStyles.label)
            {
                alignment = TextAnchor.MiddleLeft,
                wordWrap = true
            };

            m_DescStyle = new GUIStyle(EditorStyles.label)
            {
                alignment = TextAnchor.MiddleLeft,
                wordWrap = true,
                normal = { textColor = new Color(0.75f, 0.75f, 0.75f) }
            };

            m_SectionTitleStyle = new GUIStyle(EditorStyles.boldLabel)
            {
                fontSize = 12,
                normal = { textColor = new Color(0.9f, 0.75f, 0.4f) }
            };

            m_ProtoFileNameStyle = new GUIStyle(EditorStyles.foldout)
            {
                fontStyle = FontStyle.Italic
            };

            m_ProtoContentStyle = new GUIStyle(EditorStyles.label)
            {
                fontSize = 11,
                wordWrap = false,
                richText = false,
                font = Font.CreateDynamicFontFromOSFont("Menlo", 11),
                normal = { textColor = new Color(0.8f, 0.9f, 0.8f) }
            };

            m_DependencyStyle = new GUIStyle(EditorStyles.miniLabel)
            {
                fontSize = 11,
                normal = { textColor = new Color(0.65f, 0.65f, 0.65f) }
            };
        }
    }
}
