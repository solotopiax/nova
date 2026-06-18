/***************************************************************
 * (c) copyright 2026 - 2030, Solotopia
 * All Rights Reserved.
 * -------------------------------------------------------------
 * filename:  PlugPalsWindow.Methods.cs
 * author:    taoye
 * created:   2026/4/8
 * descrip:   PlugPals 窗口私有方法（仅 GUI 绘制与交互，业务逻辑委托 EditorUtil.PlugPals）
 ***************************************************************/

using System;
using System.Collections.Generic;
using System.Threading;
using NovaFramework.Runtime;
using UnityEditor;
using UnityEditor.PackageManager.UI;
using UnityEngine;

namespace NovaFramework.Editor
{
    public sealed partial class PlugPalsWindow : EditorWindow
    {
        /// <summary>
        /// 发起远程包列表请求入口。
        /// 取消上一个未完成的请求后新建令牌，委托 EditorUtil.PlugPals 完成网络和数据处理。
        /// </summary>
        private void FetchPackages()
        {
            if (m_IsFetching)
            {
                return;
            }

            m_IsFetching = true;
            m_ErrorMessage = null;
            m_ExternalErrorMessage = null;
            m_InternalErrorMessage = null;

            CancelFetchRequest();
            m_CancellationTokenSource = new System.Threading.CancellationTokenSource();

            FetchPackagesAsync(m_CancellationTokenSource.Token);
            Repaint();
        }

        /// <summary>
        /// 取消并释放当前远程包请求令牌。
        /// </summary>
        private void CancelFetchRequest()
        {
            CancellationTokenSource source = m_CancellationTokenSource;
            if (source == null)
            {
                return;
            }

            m_CancellationTokenSource = null;
            try
            {
                source.Cancel();
            }
            catch (ObjectDisposedException)
            {
            }

            try
            {
                source.Dispose();
            }
            catch (ObjectDisposedException)
            {
            }
        }

        /// <summary>
        /// 异步获取远程包列表并在主线程回调处理结果。
        /// </summary>
        /// <param name="token">取消令牌。</param>
        private async void FetchPackagesAsync(System.Threading.CancellationToken token)
        {
            List<EditorUtil.PlugPals.PackageDisplayEntry> externalEntries = null;
            List<EditorUtil.PlugPals.PackageDisplayEntry> internalEntries = null;

            try
            {
                try
                {
                    EditorUtil.PlugPals.VerdaccioPackageInfo[] externalPackages = await EditorUtil.PlugPals.FetchRemotePackagesAsync(m_ExternalUrl, EditorUtil.PlugPals.c_RegistryApiPath, token);
                    if (token.IsCancellationRequested)
                    {
                        return;
                    }

                    if (externalPackages != null)
                    {
                        externalEntries = EditorUtil.PlugPals.BuildDisplayEntries(externalPackages, m_ExternalUrl);
                    }
                    else
                    {
                        m_ExternalErrorMessage = "外网仓库解析远程包列表失败：返回数据为空。";
                    }
                }
                catch (OperationCanceledException)
                {
                    if (token.IsCancellationRequested)
                    {
                        return;
                    }

                    m_ExternalErrorMessage = "外网仓库请求超时，请检查网络后重试。";
                }
                catch (Exception e)
                {
                    if (!token.IsCancellationRequested)
                    {
                        m_ExternalErrorMessage = "外网仓库请求失败：" + e.Message;
                        Log.Warning(LogTag.Editor, "PlugPalsWindow.FetchPackagesAsync 外网仓库请求失败: {0}", e.Message);
                    }
                }

                if (string.IsNullOrEmpty(m_InternalUrl))
                {
                    internalEntries = new List<EditorUtil.PlugPals.PackageDisplayEntry>();
                }
                else
                {
                    try
                    {
                        EditorUtil.PlugPals.VerdaccioPackageInfo[] internalPackages = await EditorUtil.PlugPals.FetchRemotePackagesAsync(m_InternalUrl, EditorUtil.PlugPals.c_RegistryApiPath, token);
                        if (token.IsCancellationRequested)
                        {
                            return;
                        }

                        if (internalPackages != null)
                        {
                            internalEntries = EditorUtil.PlugPals.BuildDisplayEntries(internalPackages, m_InternalUrl);
                        }
                        else
                        {
                            m_InternalErrorMessage = "内部云仓库解析远程包列表失败：返回数据为空。";
                        }
                    }
                    catch (OperationCanceledException)
                    {
                        if (token.IsCancellationRequested)
                        {
                            return;
                        }

                        m_InternalErrorMessage = "内部云仓库请求超时，请检查网络后重试。";
                    }
                    catch (Exception e)
                    {
                        if (!token.IsCancellationRequested)
                        {
                            m_InternalErrorMessage = "内部云仓库请求失败：" + e.Message;
                            Log.Warning(LogTag.Editor, "PlugPalsWindow.FetchPackagesAsync 内部云仓库请求失败: {0}", e.Message);
                        }
                    }
                }

                if (!token.IsCancellationRequested)
                {
                    m_ExternalPackages = externalEntries ?? new List<EditorUtil.PlugPals.PackageDisplayEntry>();
                    m_InternalPackages = internalEntries ?? new List<EditorUtil.PlugPals.PackageDisplayEntry>();
                    ApplyFilter();

                    if (m_ExternalPackages.Count == 0 && m_InternalPackages.Count == 0)
                    {
                        m_ErrorMessage = BuildFatalRepoErrorMessage();
                    }
                }
            }
            finally
            {
                if (!token.IsCancellationRequested)
                {
                    m_IsFetching = false;
                    Repaint();
                }
            }
        }

        /// <summary>
        /// 根据搜索文本和分类标签过滤包列表，更新 m_FilteredPackages。
        /// </summary>
        private void ApplyFilter()
        {
            if (m_ExternalPackages == null && m_InternalPackages == null)
            {
                m_FilteredPackages = null;
                return;
            }

            m_FilteredPackages = BuildFilteredEntries(
                m_ExternalPackages,
                m_InternalPackages,
                m_SelectedCategory,
                m_ShowInstalledOnly,
                m_ShowInternalOnly,
                m_SearchText);
        }

        /// <summary>
        /// 按当前页签语义构建可见条目列表，并统一按显示名排序。
        /// </summary>
        private static List<EditorUtil.PlugPals.PackageDisplayEntry> BuildFilteredEntries(
            IReadOnlyList<EditorUtil.PlugPals.PackageDisplayEntry> externalEntries,
            IReadOnlyList<EditorUtil.PlugPals.PackageDisplayEntry> internalEntries,
            EditorUtil.PlugPals.PackageCategory selectedCategory,
            bool showInstalledOnly,
            bool showInternalOnly,
            string searchText)
        {
            var result = new List<EditorUtil.PlugPals.PackageDisplayEntry>();

            void AppendEntries(IReadOnlyList<EditorUtil.PlugPals.PackageDisplayEntry> source, bool filterByCategory)
            {
                if (source == null)
                {
                    return;
                }

                for (int i = 0; i < source.Count; i++)
                {
                    EditorUtil.PlugPals.PackageDisplayEntry entry = source[i];
                    if (showInstalledOnly && entry.Status == EditorUtil.PlugPals.PackageStatus.NotInstalled)
                    {
                        continue;
                    }

                    if (filterByCategory && selectedCategory != EditorUtil.PlugPals.PackageCategory.All && entry.Category != selectedCategory)
                    {
                        continue;
                    }

                    if (!string.IsNullOrEmpty(searchText) &&
                        !ContainsIgnoreCase(entry.Name, searchText) &&
                        !ContainsIgnoreCase(entry.DisplayName, searchText) &&
                        !ContainsIgnoreCase(entry.Description, searchText))
                    {
                        continue;
                    }

                    result.Add(entry);
                }
            }

            if (showInstalledOnly)
            {
                AppendEntries(externalEntries, false);
                AppendEntries(internalEntries, false);
            }
            else if (showInternalOnly)
            {
                AppendEntries(internalEntries, false);
            }
            else if (selectedCategory == EditorUtil.PlugPals.PackageCategory.All)
            {
                AppendEntries(externalEntries, false);
                AppendEntries(internalEntries, false);
            }
            else
            {
                AppendEntries(externalEntries, true);
            }

            var internalEntrySet = internalEntries != null
                ? new HashSet<EditorUtil.PlugPals.PackageDisplayEntry>(internalEntries)
                : null;
            result.Sort((a, b) => ComparePackageEntries(a, b, internalEntrySet));
            return result;
        }

        /// <summary>
        /// 包条目统一排序：按显示名、包名、最新版本升序。
        /// </summary>
        private static int ComparePackageEntries(
            EditorUtil.PlugPals.PackageDisplayEntry a,
            EditorUtil.PlugPals.PackageDisplayEntry b,
            ISet<EditorUtil.PlugPals.PackageDisplayEntry> internalEntries)
        {
            int byDisplayName = string.Compare(a?.DisplayName, b?.DisplayName, StringComparison.OrdinalIgnoreCase);
            if (byDisplayName != 0)
            {
                return byDisplayName;
            }

            int byName = string.Compare(a?.Name, b?.Name, StringComparison.OrdinalIgnoreCase);
            if (byName != 0)
            {
                return byName;
            }

            int byLatestVersion = string.Compare(a?.LatestVersion, b?.LatestVersion, StringComparison.OrdinalIgnoreCase);
            if (byLatestVersion != 0)
            {
                return byLatestVersion;
            }

            bool aIsInternal = internalEntries != null && a != null && internalEntries.Contains(a);
            bool bIsInternal = internalEntries != null && b != null && internalEntries.Contains(b);
            if (aIsInternal != bIsInternal)
            {
                return aIsInternal ? 1 : -1;
            }

            return string.Compare(a?.Description, b?.Description, StringComparison.OrdinalIgnoreCase);
        }

        /// <summary>
        /// 忽略大小写的字符串包含判断。
        /// </summary>
        /// <param name="source">源字符串。</param>
        /// <param name="search">搜索文本。</param>
        /// <returns>source 是否包含 search（忽略大小写）。</returns>
        private static bool ContainsIgnoreCase(string source, string search)
        {
            if (string.IsNullOrEmpty(source))
            {
                return false;
            }

            return source.IndexOf(search, StringComparison.OrdinalIgnoreCase) >= 0;
        }

        /// <summary>
        /// 获取 manifest.json 的绝对路径。
        /// </summary>
        /// <returns>manifest.json 绝对路径。</returns>
        private string GetManifestFullPath()
        {
            return Util.SysIO.Path.GetFullPath(Util.SysIO.Path.Combine(Application.dataPath, "../", c_ManifestPath));
        }

        /// <summary>
        /// 绘制窗口主标题行（居中加粗，上下各 8px 空行，底部细分隔线）。
        /// </summary>
        private void DrawMainTitle()
        {
            EnsureStyles();
            EditorUtil.Draw.Space(8f);
            EditorUtil.Draw.Layout.Horizontal(() =>
            {
                EditorUtil.Draw.FlexibleSpace();
                EditorUtil.Draw.Label("Plugin 云插件服务中心", m_MainTitleStyle, false);
                EditorUtil.Draw.FlexibleSpace();
            });
            EditorUtil.Draw.Space(8f);
            EditorUtil.Draw.Line();
        }

        /// <summary>
        /// 绘制顶部工具栏：左侧公网/内网仓库地址输入框 + 保存；右侧搜索框（缩短）+ 刷新。
        /// </summary>
        private void DrawToolbar()
        {
            EditorUtil.Draw.Space(4f);
            EditorUtil.Draw.Layout.Horizontal(() =>
            {
                EditorUtil.Draw.Space(8f);

                EditorUtil.Draw.Label("公网仓库", EditorStyles.boldLabel, false, GUILayout.Width(64f));
                m_ExternalUrl = EditorUtil.Draw.TextField(m_ExternalUrl ?? "", false, GUILayout.Width(240f));
                EditorUtil.Draw.Space(8f);

                EditorUtil.Draw.Label("内网仓库", EditorStyles.boldLabel, false, GUILayout.Width(64f));
                m_InternalUrl = EditorUtil.Draw.TextField(m_InternalUrl ?? "", false, GUILayout.Width(240f));
                EditorUtil.Draw.Space(8f);

                EditorUtil.Draw.Button("保存", 64f, false, SaveRegistriesAndRefetch);

                GUILayout.FlexibleSpace();

                EditorUtil.Draw.Label("搜索", EditorStyles.boldLabel, false, GUILayout.Width(40f));
                string newSearch = EditorUtil.Draw.TextField(m_SearchText ?? "", false, GUILayout.Width(220f));
                if (newSearch != m_SearchText)
                {
                    m_SearchText = newSearch;
                    ApplyFilter();
                }
                EditorUtil.Draw.Space(8f);
                EditorUtil.Draw.Button("刷新", 100f, false, FetchPackages);
                EditorUtil.Draw.Space(8f);
            });
            EditorUtil.Draw.Space(4f);
            EditorUtil.Draw.Line();
        }

        /// <summary>
        /// 保存当前输入的 registry 地址到 PlugPalsRegistries.json，并用新地址重新拉取包列表。
        /// </summary>
        private void SaveRegistriesAndRefetch()
        {
            EditorUtil.PlugPals.SaveRegistries(new EditorUtil.PlugPals.RegistriesConfig
            {
                externalUrl = m_ExternalUrl,
                externalName = m_ExternalName,
                internalUrl = m_InternalUrl,
                internalName = m_InternalName,
            });

            EditorUtil.PlugPals.RegistriesConfig normalized = EditorUtil.PlugPals.LoadRegistries();
            m_ExternalUrl = normalized.externalUrl;
            m_ExternalName = normalized.externalName;
            m_InternalUrl = normalized.internalUrl;
            m_InternalName = normalized.internalName;

            FetchPackages();
        }

        /// <summary>
        /// 确保分隔线坐标数组已初始化（首次或窗口宽度变化时重算默认值）。
        /// </summary>
        private void EnsureColumnBorders()
        {
            if (m_ColBorders != null && m_ColBorders.Length == 5)
            {
                return;
            }

            float w = position.width;
            m_ColBorders = new float[]
            {
                w * 0.30f,
                w - 390f,
                w - 290f,
                w - 190f,
                w
            };
        }

        /// <summary>
        /// 绘制列表表头（手动 Rect 绘制 + 拖拽 + 黑色分隔线）。
        /// </summary>
        private void DrawHeader()
        {
            EnsureColumnBorders();
            m_ColBorders[4] = position.width;

            EditorUtil.Draw.Space(5);
            Rect row = GUILayoutUtility.GetRect(position.width, c_RowHeight + 4f);

            GUI.Box(row, GUIContent.none);

            float y = row.y;
            float h = row.height;

            GUI.Label(new Rect(row.x + 4f, y, m_ColBorders[0] - row.x - 4f, h), "名称", EditorStyles.whiteLargeLabel);
            GUI.Label(new Rect(m_ColBorders[0] + 4f, y, m_ColBorders[1] - m_ColBorders[0] - 4f, h), "描述", EditorStyles.whiteLargeLabel);
            GUI.Label(new Rect(m_ColBorders[1], y, m_ColBorders[2] - m_ColBorders[1], h), "本地版本", m_VersionHeaderStyle);
            GUI.Label(new Rect(m_ColBorders[2], y, m_ColBorders[3] - m_ColBorders[2], h), "最新版本", m_VersionHeaderStyle);
            GUI.Label(new Rect(m_ColBorders[3], y, m_ColBorders[4] - m_ColBorders[3], h), "版本操作", m_VersionHeaderStyle);

            DrawColumnSeparators(y, h);
            HandleColumnDrag(row);
        }

        /// <summary>
        /// 在指定 Y 范围内绘制四条列分隔线，并注册拖拽光标。
        /// </summary>
        /// <param name="y">行顶部 Y 坐标。</param>
        /// <param name="h">行高度。</param>
        private void DrawColumnSeparators(float y, float h)
        {
            Color prevColor = GUI.color;
            GUI.color = s_SeparatorColor;
            for (int i = 0; i < 4; i++)
            {
                GUI.DrawTexture(new Rect(m_ColBorders[i] - 0.5f, y, 1f, h), EditorGUIUtility.whiteTexture);
                Rect handle = new Rect(m_ColBorders[i] - c_DragHandleHalfWidth, y, c_DragHandleHalfWidth * 2f, h);
                EditorGUIUtility.AddCursorRect(handle, MouseCursor.SplitResizeLeftRight);
            }
            GUI.color = prevColor;
        }

        /// <summary>
        /// 处理列分隔线拖拽。拖拽时以分隔线左边缘为焦点移动。
        /// </summary>
        /// <param name="headerRect">表头行的矩形区域（用于限定鼠标点击区域）。</param>
        private void HandleColumnDrag(Rect headerRect)
        {
            Event evt = Event.current;

            if (evt.type == EventType.MouseDown && evt.button == 0)
            {
                for (int i = 0; i < 4; i++)
                {
                    Rect handle = new Rect(m_ColBorders[i] - c_DragHandleHalfWidth, headerRect.y, c_DragHandleHalfWidth * 2f, headerRect.height);
                    if (handle.Contains(evt.mousePosition))
                    {
                        m_DraggingCol = i;
                        m_DragStartMouseX = evt.mousePosition.x;
                        m_DragStartBorderX = m_ColBorders[i];
                        evt.Use();
                        break;
                    }
                }
            }

            if (m_DraggingCol >= 0 && evt.type == EventType.MouseDrag)
            {
                float delta = evt.mousePosition.x - m_DragStartMouseX;
                float minX = m_DraggingCol > 0 ? m_ColBorders[m_DraggingCol - 1] + 30f : 60f;
                float maxX = m_ColBorders[m_DraggingCol + 1] - 30f;
                m_ColBorders[m_DraggingCol] = Mathf.Clamp(m_DragStartBorderX + delta, minX, maxX);
                evt.Use();
                Repaint();
            }

            if (m_DraggingCol >= 0 && evt.type == EventType.MouseUp)
            {
                m_DraggingCol = -1;
                evt.Use();
            }
        }

        /// <summary>
        /// 绘制单个包条目行（手动 Rect 绘制，后列覆盖前列内容）。
        /// </summary>
        /// <param name="entry">包条目。</param>
        private void DrawPackageCard(EditorUtil.PlugPals.PackageDisplayEntry entry)
        {
            string foldoutKey = GetFoldoutKey(entry);
            if (!m_FoldoutStates.ContainsKey(foldoutKey))
            {
                m_FoldoutStates[foldoutKey] = false;
            }

            bool foldout = m_FoldoutStates[foldoutKey];

            Rect row = GUILayoutUtility.GetRect(position.width, c_RowHeight);
            float y = row.y;
            float h = row.height;

            Rect nameClip = new Rect(row.x, y, m_ColBorders[0] - row.x, h);
            GUI.BeginGroup(nameClip);
            Color prevNameColor = GUI.contentColor;
            GUI.contentColor = GetPackageNameColor(entry);
            EditorUtil.Draw.Foldout(new Rect(0, 0, nameClip.width, nameClip.height), ref foldout, entry.DisplayName ?? entry.Name, true, m_TitleStyle);
            GUI.contentColor = prevNameColor;
            GUI.EndGroup();
            m_FoldoutStates[foldoutKey] = foldout;

            GUI.Label(new Rect(m_ColBorders[0] + 4f, y, m_ColBorders[1] - m_ColBorders[0] - 4f, h), entry.Description ?? "", m_RowDescStyle);
            GUI.Label(new Rect(m_ColBorders[1], y, m_ColBorders[2] - m_ColBorders[1], h), entry.LocalVersion ?? "-----", m_RowVersionStyle);

            bool highlightLatestVersion = ShouldHighlightLatestVersion(entry);
            Color prevContent = GUI.contentColor;
            if (highlightLatestVersion) { GUI.contentColor = Color.green; }
            GUI.Label(new Rect(m_ColBorders[2], y, m_ColBorders[3] - m_ColBorders[2], h), entry.LatestVersion, m_RowVersionStyle);
            GUI.contentColor = prevContent;

            DrawRowActionArea(entry, new Rect(m_ColBorders[3], y, m_ColBorders[4] - m_ColBorders[3], h));

            DrawColumnSeparators(y, h);

            DrawRowHorizontalLine(row);

            if (foldout)
            {
                EditorUtil.Draw.Layout.Vertical("box", () =>
                {
                    EditorUtil.Draw.Space(4f);

                    int savedIndent = EditorUtil.Draw.SaveIndentLevel();
                    EditorUtil.Draw.SetIndentLevel(1);

                    DrawInfoField("Name", entry.Name, false);
                    DrawInfoField("Local Version", entry.LocalVersion ?? "-----", false);
                    DrawInfoField("Latest Version", entry.LatestVersion, ShouldHighlightLatestVersion(entry));
                    if (!string.IsNullOrEmpty(entry.CoreVersion))
                    {
                        DrawInfoField("Core Version", entry.CoreVersion, false);
                    }
                    DrawInfoField("Description", entry.Description, false);
                    EditorUtil.Draw.Space(4f);
                    DrawRowActionButtons(entry);

                    EditorUtil.Draw.RestoreIndentLevel(savedIndent);
                    EditorUtil.Draw.Space(4f);
                });
            }
        }

        /// <summary>
        /// 绘制卡片内单行信息字段（标签 + 值）。
        /// </summary>
        /// <param name="label">字段标签。</param>
        /// <param name="value">字段值。</param>
        /// <param name="highlight">是否以绿色高亮显示值。</param>
        private void DrawInfoField(string label, string value, bool highlight)
        {
            EditorUtil.Draw.Layout.Horizontal(() =>
            {
                EditorUtil.Draw.Label(label, m_LabelStyle, false, GUILayout.Width(c_LabelWidth));
                if (highlight) { GUI.contentColor = Color.green; }
                GUIStyle style = label == "Description" ? m_DescStyle : m_ValueStyle;
                EditorUtil.Draw.Label(value ?? "", style, false, GUILayout.ExpandWidth(true));
                if (highlight) { GUI.contentColor = Color.white; }
            });
        }

        /// <summary>
        /// 判断包是否归属 Nova Framework 体系（以 com.solotopia.nova.framework 为前缀），
        /// 仅此前缀的包在已安装时才尝试读取并展示 Samples 导入入口。
        /// </summary>
        /// <param name="packageName">UPM 包名。</param>
        /// <returns>属于 Nova Framework 体系返回 true。</returns>
        private bool IsNovaFrameworkPackage(string packageName)
        {
            return !string.IsNullOrEmpty(packageName) && packageName.StartsWith(c_NovaFrameworkPackagePrefix, StringComparison.Ordinal);
        }

        /// <summary>
        /// 弹出当前包的 Samples 下拉菜单，每个菜单项点击后将对应 Sample 导入到 Assets/Samples。
        /// 已导入项的 isImported 标记为 true，菜单项依然可用以支持覆盖导入。
        /// </summary>
        /// <param name="entry">包条目（仅用于日志）。</param>
        /// <param name="samples">从 Sample.FindByPackage 拿到的 Samples 列表。</param>
        private void ShowSamplesMenu(EditorUtil.PlugPals.PackageDisplayEntry entry, IReadOnlyList<Sample> samples)
        {
            GenericMenu menu = new GenericMenu();
            for (int i = 0; i < samples.Count; i++)
            {
                // 必须用局部变量捕获，防止 GenericMenu 延迟回调时 i 已变化
                Sample captured = samples[i];
                string label = string.IsNullOrEmpty(captured.displayName) ? "Sample " + (i + 1) : captured.displayName;
                if (captured.isImported)
                {
                    label += "  (已导入，点击覆盖)";
                }
                menu.AddItem(new GUIContent(label), false, () => ImportSample(entry, captured));
            }
            menu.ShowAsContext();
        }

        /// <summary>
        /// 将指定 Sample 导入到 Assets/Samples，导入完成后刷新 AssetDatabase 让 Project 视图立即显示。
        /// 失败仅写日志 + 弹通知，不污染 m_ErrorMessage（否则窗口列表会被错误分支整页接管）。
        /// 开发源工程下 Samples~ 由发布脚本临时生成，平时不在磁盘 → 命中即提示"开发源工程，导入无效"，避免误导用户。
        /// </summary>
        /// <param name="entry">包条目（仅用于失败日志定位）。</param>
        /// <param name="sample">待导入的 Sample。</param>
        private void ImportSample(EditorUtil.PlugPals.PackageDisplayEntry entry, Sample sample)
        {
            try
            {
                bool resolvedExists = !string.IsNullOrEmpty(sample.resolvedPath) && Util.SysIO.Directory.Exists(sample.resolvedPath);
                bool isDevSource = !resolvedExists && IsDevSourceResolvedPath(sample.resolvedPath);
                if (isDevSource)
                {
                    Log.Warning(LogTag.Editor, "PlugPalsWindow.ImportSample {0}@{1} sample={2} 命中开发源工程（resolvedPath={3}）", entry.Name, entry.LocalVersion, sample.displayName, sample.resolvedPath);
                    ShowNotification(new GUIContent("当前为开发源工程，导入无效"));
                    return;
                }

                bool ok = sample.Import(Sample.ImportOptions.OverridePreviousImports);
                if (!ok)
                {
                    string reason = resolvedExists ? "Sample.Import 返回 false（请检查 Editor 控制台堆栈）" : "源目录不存在：" + sample.resolvedPath;
                    Log.Warning(LogTag.Editor, "PlugPalsWindow.ImportSample {0}@{1} sample={2} 失败 - {3}", entry.Name, entry.LocalVersion, sample.displayName, reason);
                    ShowNotification(new GUIContent("Sample 导入失败：" + (resolvedExists ? sample.displayName : "包内缺少 Samples~ 目录")));
                    return;
                }
                AssetDatabase.Refresh();
                ShowNotification(new GUIContent($"已导入 Sample：{sample.displayName}"));
            }
            catch (Exception e)
            {
                Log.Warning(LogTag.Editor, "PlugPalsWindow.ImportSample {0}@{1} sample={2} 抛出异常: {3}", entry.Name, entry.LocalVersion, sample.displayName, e.Message);
                ShowNotification(new GUIContent("Sample 导入异常：" + e.Message));
            }
            finally
            {
                Repaint();
            }
        }

        /// <summary>
        /// 判断远端版本是否严格高于本地版本，仅此时才视为真正可升级。
        /// </summary>
        /// <param name="entry">包条目。</param>
        /// <returns>远端版本更高返回 true。</returns>
        private static bool HasRemoteUpgrade(EditorUtil.PlugPals.PackageDisplayEntry entry)
        {
            if (entry == null || string.IsNullOrEmpty(entry.LocalVersion) || string.IsNullOrEmpty(entry.LatestVersion))
            {
                return false;
            }

            return EditorUtil.PlugPals.CompareSemVer(entry.LatestVersion, entry.LocalVersion) > 0;
        }

        /// <summary>
        /// 判断"最新版本"字段是否需要高亮。
        /// 未安装时始终高亮；已安装时仅在远端版本更高时高亮。
        /// </summary>
        /// <param name="entry">包条目。</param>
        /// <returns>需要高亮返回 true。</returns>
        private static bool ShouldHighlightLatestVersion(EditorUtil.PlugPals.PackageDisplayEntry entry)
        {
            return entry != null && (string.IsNullOrEmpty(entry.LocalVersion) || HasRemoteUpgrade(entry));
        }

        /// <summary>
        /// 判断当前包是否允许点击安装/升级。
        /// 非仓库引用（file/git/http）不开放切换为仓库版本。
        /// </summary>
        /// <param name="entry">包条目。</param>
        /// <returns>允许点击安装/升级返回 true。</returns>
        private static bool CanInstallOrUpgrade(EditorUtil.PlugPals.PackageDisplayEntry entry)
        {
            if (entry == null || entry.Status == EditorUtil.PlugPals.PackageStatus.NonRegistry)
            {
                return false;
            }

            if (entry.Status == EditorUtil.PlugPals.PackageStatus.NotInstalled)
            {
                return true;
            }

            return HasRemoteUpgrade(entry);
        }

        /// <summary>
        /// 计算安装按钮文案。
        /// 未安装显示"安装"；仅远端版本更高时显示"升级"；其余一律显示"已安装"。
        /// </summary>
        /// <param name="entry">包条目。</param>
        /// <returns>按钮文案。</returns>
        private static string GetInstallActionLabel(EditorUtil.PlugPals.PackageDisplayEntry entry)
        {
            if (entry == null)
            {
                return "安装";
            }

            if (entry.Status == EditorUtil.PlugPals.PackageStatus.NotInstalled)
            {
                return "安装";
            }

            return HasRemoteUpgrade(entry) && entry.Status != EditorUtil.PlugPals.PackageStatus.NonRegistry ? "升级" : "已安装";
        }

        /// <summary>
        /// 安装/升级前弹确认框。返回 true 表示用户确认继续，取消则不执行。
        /// </summary>
        /// <param name="entry">包条目。</param>
        /// <returns>用户点确定返回 true，取消返回 false。</returns>
        private bool ConfirmInstall(EditorUtil.PlugPals.PackageDisplayEntry entry)
        {
            string displayName = string.IsNullOrEmpty(entry.DisplayName) ? entry.Name : entry.DisplayName;
            bool isUpgrade = entry.Status == EditorUtil.PlugPals.PackageStatus.Upgradeable;
            string title = isUpgrade ? "确认升级" : "确认安装";
            string message = isUpgrade
                ? string.Format("确认将 {0} 从 v{1} 升级到 v{2}？", displayName, entry.LocalVersion, entry.LatestVersion)
                : string.Format("确认安装 {0} v{1}？", displayName, entry.LatestVersion);
            return EditorUtility.DisplayDialog(title, message, "确定", "取消");
        }

        /// <summary>
        /// 卸载前弹确认框。返回 true 表示用户确认继续，取消则不执行。
        /// </summary>
        /// <param name="entry">包条目。</param>
        /// <returns>用户点确定返回 true，取消返回 false。</returns>
        private bool ConfirmUninstall(EditorUtil.PlugPals.PackageDisplayEntry entry)
        {
            string displayName = string.IsNullOrEmpty(entry.DisplayName) ? entry.Name : entry.DisplayName;
            return EditorUtility.DisplayDialog("确认卸载", string.Format("确认卸载 {0}？", displayName), "确定", "取消");
        }

        /// <summary>
        /// 判定 Sample.resolvedPath 是否指向开发源工程（即包源码直接位于本工程内，非 UPM 安装态）。
        /// 凡未落在 Library/PackageCache/ 下的 resolvedPath 一律视为开发源；该路径下 Samples~ 由发布脚本临时生成，
        /// 平时不存在是正常状态，因此区别于"包内缺少 Samples~ 目录"的真实异常。
        /// </summary>
        /// <param name="resolvedPath">Sample.resolvedPath（绝对路径）。</param>
        /// <returns>命中开发源工程返回 true，否则 false。</returns>
        private bool IsDevSourceResolvedPath(string resolvedPath)
        {
            if (string.IsNullOrEmpty(resolvedPath))
            {
                return false;
            }
            string normalized = resolvedPath.Replace('\\', '/');
            return normalized.IndexOf("/Library/PackageCache/", StringComparison.Ordinal) < 0;
        }

        /// <summary>
        /// 绘制展开卡片内的操作按钮组：安装/升级 + 卸载 + UPM。
        /// </summary>
        /// <param name="entry">包条目。</param>
        private void DrawRowActionButtons(EditorUtil.PlugPals.PackageDisplayEntry entry)
        {
            bool canInstallOrUpgrade = CanInstallOrUpgrade(entry);
            bool canUninstall = entry.Status != EditorUtil.PlugPals.PackageStatus.NotInstalled;
            string installLabel = GetInstallActionLabel(entry);
            string registryUrl = GetRegistryUrl(entry);
            string registryName = GetRegistryName(entry);

            EditorUtil.Draw.DisabledGroup(m_IsOperating, () =>
            {
                EditorUtil.Draw.Layout.Horizontal(() =>
                {
                    EditorUtil.Draw.Space(c_LabelWidth + 4f);
                    EditorUtil.Draw.DisabledGroup(!canInstallOrUpgrade, () =>
                    {
                        EditorUtil.Draw.SuccessButton(installLabel, false, () =>
                        {
                            if (!ConfirmInstall(entry))
                            {
                                return;
                            }
                            m_IsOperating = true;
                            try
                            {
                                EditorUtil.PlugPals.InstallPackage(GetManifestFullPath(), registryUrl, registryName, entry, BuildKnownRegistryPackages());
                            }
                            catch (Exception e)
                            {
                                m_ErrorMessage = "安装失败: " + e.Message;
                            }
                            finally { m_IsOperating = false; }
                            Repaint();
                        }, GUILayout.Width(c_ColBtnWidth), GUILayout.Height(c_RowBtnHeight));
                    });
                    EditorUtil.Draw.DisabledGroup(!canUninstall, () =>
                    {
                        EditorUtil.Draw.DangerButton("卸载", false, () =>
                        {
                            if (!ConfirmUninstall(entry))
                            {
                                return;
                            }
                            m_IsOperating = true;
                            try
                            {
                                EditorUtil.PlugPals.UninstallPackage(GetManifestFullPath(), registryUrl, entry,
                                    EditorUtil.PlugPals.CollectRegistryUrlsDeclaredByOtherInstalled(EnumerateAllRemoteEntries(), entry.Name));
                            }
                            catch (Exception e)
                            {
                                m_ErrorMessage = "卸载失败: " + e.Message;
                            }
                            finally { m_IsOperating = false; }
                            Repaint();
                        }, GUILayout.Width(c_ColBtnWidth), GUILayout.Height(c_RowBtnHeight));
                    });
                    EditorUtil.Draw.WarningButton("UPM", false, () => OpenPackageManager(entry.Name), GUILayout.Width(c_ColBtnUpmWidth), GUILayout.Height(c_RowBtnHeight));

                    if (IsNovaFrameworkPackage(entry.Name) && !string.IsNullOrEmpty(entry.LocalVersion))
                    {
                        IReadOnlyList<Sample> samples = EditorUtil.PlugPals.GetPackageSamples(entry.Name, entry.LocalVersion);
                        if (samples != null && samples.Count > 0)
                        {
                            string samplesBtnLabel = "导入 Samples (v" + entry.LocalVersion + ")";
                            EditorUtil.Draw.Button(samplesBtnLabel, c_ColSamplesBtnWidth, false, () => ShowSamplesMenu(entry, samples), GUILayout.Height(c_RowBtnHeight));
                        }
                    }

                    EditorUtil.Draw.Button("查看日志", c_ColChangelogBtnWidth, false, () => OpenChangelogAsync(entry), GUILayout.Height(c_RowBtnHeight));
                });
            });
        }

        /// <summary>
        /// 异步获取并用系统默认程序打开指定包的更新日志。
        /// version 优先取本地已安装版本，未安装时取最新版本。
        /// 失败或包内无 CHANGELOG.md 时显示气泡通知，不污染 m_ErrorMessage。
        /// </summary>
        /// <param name="entry">包条目。</param>
        private async void OpenChangelogAsync(EditorUtil.PlugPals.PackageDisplayEntry entry)
        {
            if (m_IsFetchingChangelog) return;
            m_IsFetchingChangelog = true;
            try
            {
                string version = !string.IsNullOrEmpty(entry.LocalVersion) ? entry.LocalVersion : entry.LatestVersion;
                string path = await EditorUtil.PlugPals.FetchChangelogAsync(GetRegistryUrl(entry), entry.Name, version, CancellationToken.None);
                if (path == null)
                {
                    ShowNotification(new GUIContent("该包未发布更新日志"));
                    return;
                }
                EditorUtility.OpenWithDefaultApp(path);
            }
            finally
            {
                m_IsFetchingChangelog = false;
            }
        }

        /// <summary>
        /// 在折叠行的版本操作列区域绘制操作按钮（安装/升级 + 卸载 + UPM）。
        /// 使用 GUI.Button 手动 Rect 绘制，与行内其他列风格一致。
        /// </summary>
        /// <param name="entry">包条目。</param>
        /// <param name="area">版本操作列矩形区域。</param>
        private void DrawRowActionArea(EditorUtil.PlugPals.PackageDisplayEntry entry, Rect area)
        {
            bool canInstallOrUpgrade = CanInstallOrUpgrade(entry);
            bool canUninstall = entry.Status != EditorUtil.PlugPals.PackageStatus.NotInstalled;
            string installLabel = GetInstallActionLabel(entry);
            string registryUrl = GetRegistryUrl(entry);
            string registryName = GetRegistryName(entry);

            float x = area.x + 4f;
            float btnH = area.height - 2f;
            float btnY = area.y + 1f;

            bool prevEnabled = GUI.enabled;
            Color prevColor = GUI.color;

            try
            {
                GUI.enabled = !m_IsOperating && canInstallOrUpgrade;
                GUI.color = GUI.enabled ? new Color(0.3f, 0.7f, 0.35f) : new Color(0.2f, 0.4f, 0.22f);
                if (GUI.Button(new Rect(x, btnY, c_ColBtnWidth, btnH), installLabel) && ConfirmInstall(entry))
                {
                    m_IsOperating = true;
                    try
                    {
                        EditorUtil.PlugPals.InstallPackage(GetManifestFullPath(), registryUrl, registryName, entry, BuildKnownRegistryPackages());
                    }
                    catch (Exception e)
                    {
                        m_ErrorMessage = "安装失败: " + e.Message;
                    }
                    finally { m_IsOperating = false; }
                    Repaint();
                }

                x += c_ColBtnWidth + 2f;
                GUI.enabled = !m_IsOperating && canUninstall;
                GUI.color = GUI.enabled ? new Color(0.75f, 0.25f, 0.25f) : new Color(0.45f, 0.2f, 0.2f);
                if (GUI.Button(new Rect(x, btnY, c_ColBtnWidth, btnH), "卸载") && ConfirmUninstall(entry))
                {
                    m_IsOperating = true;
                    try
                    {
                        EditorUtil.PlugPals.UninstallPackage(GetManifestFullPath(), registryUrl, entry,
                            EditorUtil.PlugPals.CollectRegistryUrlsDeclaredByOtherInstalled(EnumerateAllRemoteEntries(), entry.Name));
                    }
                    catch (Exception e)
                    {
                        m_ErrorMessage = "卸载失败: " + e.Message;
                    }
                    finally { m_IsOperating = false; }
                    Repaint();
                }

                x += c_ColBtnWidth + 2f;
                GUI.enabled = !m_IsOperating;
                GUI.color = GUI.enabled ? new Color(0.8f, 0.7f, 0.25f) : new Color(0.5f, 0.43f, 0.18f);
                if (GUI.Button(new Rect(x, btnY, c_ColBtnUpmWidth, btnH), "UPM"))
                {
                    OpenPackageManager(entry.Name);
                }
            }
            finally
            {
                GUI.color = prevColor;
                GUI.enabled = prevEnabled;
            }
        }

        /// <summary>
        /// 绘制行底部水平分隔线。
        /// </summary>
        /// <param name="row">行矩形。</param>
        private void DrawRowHorizontalLine(Rect row)
        {
            Color prev = GUI.color;
            GUI.color = s_SeparatorColor;
            GUI.DrawTexture(new Rect(row.x, row.yMax, position.width, 1f), EditorGUIUtility.whiteTexture);
            GUI.color = prev;
        }

        /// <summary>
        /// 打开 Unity Package Manager 窗口并尝试选中指定包。
        /// </summary>
        /// <param name="packageName">要选中的包名。</param>
        private void OpenPackageManager(string packageName)
        {
            UnityEditor.PackageManager.UI.Window.Open(packageName);
        }

        /// <summary>
        /// 延迟初始化自定义 GUIStyle（避免 EditorStyles 未就绪时创建）。
        /// </summary>
        private void EnsureStyles()
        {
            if (m_MainTitleStyle == null)
            {
                m_MainTitleStyle = new GUIStyle(EditorStyles.boldLabel)
                {
                    fontSize = 18,
                    fontStyle = FontStyle.Bold,
                    alignment = TextAnchor.MiddleCenter,
                };
            }

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

            m_VersionHeaderStyle = new GUIStyle(EditorStyles.whiteLargeLabel)
            {
                alignment = TextAnchor.MiddleLeft,
                padding = new RectOffset(6, 0, 0, 0)
            };

            m_RowDescStyle = new GUIStyle(EditorStyles.miniLabel)
            {
                alignment = TextAnchor.MiddleLeft,
                normal = { textColor = new Color(0.6f, 0.6f, 0.6f) },
                wordWrap = false,
                clipping = TextClipping.Clip
            };

            m_RowVersionStyle = new GUIStyle(EditorStyles.label)
            {
                alignment = TextAnchor.MiddleLeft,
                padding = new RectOffset(6, 0, 0, 0)
            };

            m_CategoryActiveStyle = new GUIStyle(EditorStyles.toolbarButton)
            {
                fontStyle = FontStyle.Bold,
                normal = { textColor = new Color(0.25f, 0.6f, 1f) },
                onNormal = { textColor = new Color(0.25f, 0.6f, 1f) }
            };

            m_CategoryNormalStyle = new GUIStyle(EditorStyles.toolbarButton);
        }

        /// <summary>
        /// 绘制分类标签栏（全部 / SDK / 框架核心 / 业务核心 / 其他）。
        /// 点击标签时更新 m_SelectedCategory 并重新执行过滤。
        /// </summary>
        private void DrawCategoryTabs()
        {
            EditorUtil.Draw.Layout.Horizontal(() =>
            {
                EditorUtil.Draw.Space(8f);
                DrawCategoryButton(EditorUtil.PlugPals.PackageCategory.All, "全部");
                DrawCategoryButton(EditorUtil.PlugPals.PackageCategory.SDK, "SDK");
                DrawCategoryButton(EditorUtil.PlugPals.PackageCategory.Kit, "业务核心");
                DrawCategoryButton(EditorUtil.PlugPals.PackageCategory.Framework, "框架核心");
                DrawCategoryButton(EditorUtil.PlugPals.PackageCategory.Other, "其他");
                DrawInternalCategoryButton();
                GUILayout.FlexibleSpace();
                DrawInstalledButton();
                EditorUtil.Draw.Space(8f);
            });
            EditorUtil.Draw.Space(2f);
        }

        /// <summary>
        /// 绘制单个分类标签按钮。
        /// 选中态使用 m_CategoryActiveStyle，未选中使用 m_CategoryNormalStyle。
        /// </summary>
        /// <param name="category">该按钮对应的分类。</param>
        /// <param name="label">按钮显示文本。</param>
        private void DrawCategoryButton(EditorUtil.PlugPals.PackageCategory category, string label)
        {
            // DrawCategoryButton 需要通过 ref 型按下状态更新 m_SelectedCategory，
            // EditorUtil.Draw.Button 无法捕获枚举选中态变化，保留 GUILayout.Button 直接调用。
            GUIStyle style = !m_ShowInstalledOnly && !m_ShowInternalOnly && m_SelectedCategory == category ? m_CategoryActiveStyle : m_CategoryNormalStyle;
            if (GUILayout.Button(label, style, GUILayout.Width(80f)))
            {
                m_SelectedCategory = category;
                m_ShowInstalledOnly = false;
                m_ShowInternalOnly = false;
                ApplyFilter();
            }
        }

        /// <summary>
        /// 绘制"内部云仓库"独立页签，仅展示内部仓库条目。
        /// </summary>
        private void DrawInternalCategoryButton()
        {
            GUIStyle style = m_ShowInternalOnly ? m_CategoryActiveStyle : m_CategoryNormalStyle;
            if (GUILayout.Button("内部云仓库", style, GUILayout.Width(100f)))
            {
                m_ShowInternalOnly = true;
                m_ShowInstalledOnly = false;
                ApplyFilter();
            }
        }

        /// <summary>
        /// 绘制右侧“已安装”独立页签，开启后忽略分类过滤，只显示本地已安装模块。
        /// </summary>
        private void DrawInstalledButton()
        {
            GUIStyle style = m_ShowInstalledOnly ? m_CategoryActiveStyle : m_CategoryNormalStyle;
            if (GUILayout.Button("已安装", style, GUILayout.Width(80f)))
            {
                m_ShowInstalledOnly = true;
                m_ShowInternalOnly = false;
                ApplyFilter();
            }
        }

        /// <summary>
        /// 判定条目是否来自内部云仓库列表。
        /// </summary>
        private bool IsInternalRepoEntry(EditorUtil.PlugPals.PackageDisplayEntry entry)
        {
            if (entry == null || m_InternalPackages == null)
            {
                return false;
            }

            for (int i = 0; i < m_InternalPackages.Count; i++)
            {
                if (ReferenceEquals(m_InternalPackages[i], entry))
                {
                    return true;
                }
            }

            return false;
        }

        /// <summary>
        /// 获取条目所属仓库地址。
        /// </summary>
        private string GetRegistryUrl(EditorUtil.PlugPals.PackageDisplayEntry entry)
        {
            return IsInternalRepoEntry(entry) ? m_InternalUrl : m_ExternalUrl;
        }

        /// <summary>
        /// 获取条目所属仓库名称。
        /// </summary>
        private string GetRegistryName(EditorUtil.PlugPals.PackageDisplayEntry entry)
        {
            return IsInternalRepoEntry(entry) ? m_InternalName : m_ExternalName;
        }

        /// <summary>
        /// 由内存中已 fetch 的外网 + 内部云包列表构造「包名 -> registry 来源」映射，供安装时判命中。
        /// 同名时外网优先（公网可达性更高）。
        /// </summary>
        /// <summary>
        /// 遍历内存中已 fetch 的外网 + 内部云全部包条目（含安装态与 nova 声明），供卸载时判断私有仓库是否仍被其它已装包共用。
        /// </summary>
        private IEnumerable<EditorUtil.PlugPals.PackageDisplayEntry> EnumerateAllRemoteEntries()
        {
            if (m_ExternalPackages != null)
            {
                foreach (EditorUtil.PlugPals.PackageDisplayEntry entry in m_ExternalPackages)
                {
                    yield return entry;
                }
            }

            if (m_InternalPackages != null)
            {
                foreach (EditorUtil.PlugPals.PackageDisplayEntry entry in m_InternalPackages)
                {
                    yield return entry;
                }
            }
        }

        private Dictionary<string, EditorUtil.PlugPals.RegistrySource> BuildKnownRegistryPackages()
        {
            var map = new Dictionary<string, EditorUtil.PlugPals.RegistrySource>(StringComparer.Ordinal);
            if (!string.IsNullOrEmpty(m_InternalUrl))
            {
                AddRegistryPackages(map, m_InternalPackages, m_InternalUrl, m_InternalName);
            }
            AddRegistryPackages(map, m_ExternalPackages, m_ExternalUrl, m_ExternalName);
            return map;
        }

        private static void AddRegistryPackages(
            Dictionary<string, EditorUtil.PlugPals.RegistrySource> map,
            System.Collections.Generic.List<EditorUtil.PlugPals.PackageDisplayEntry> packages,
            string registryUrl,
            string registryName)
        {
            if (packages == null)
            {
                return;
            }

            for (int i = 0; i < packages.Count; i++)
            {
                EditorUtil.PlugPals.PackageDisplayEntry entry = packages[i];
                if (entry == null || string.IsNullOrEmpty(entry.Name))
                {
                    continue;
                }

                map[entry.Name] = new EditorUtil.PlugPals.RegistrySource { Url = registryUrl, Name = registryName };
            }
        }

        /// <summary>
        /// 生成折叠状态字典 key，避免未来同名包共用同一折叠状态。
        /// </summary>
        private string GetFoldoutKey(EditorUtil.PlugPals.PackageDisplayEntry entry)
        {
            return (IsInternalRepoEntry(entry) ? "internal:" : "external:") + entry.Name;
        }

        /// <summary>
        /// 获取包名文本颜色。
        /// </summary>
        private Color GetPackageNameColor(EditorUtil.PlugPals.PackageDisplayEntry entry)
        {
            return IsInternalRepoEntry(entry) ? s_InternalPackageNameColor : Color.white;
        }

        /// <summary>
        /// 构建双仓库全失败时的兜底错误文案。
        /// </summary>
        private string BuildFatalRepoErrorMessage()
        {
            if (string.IsNullOrEmpty(m_ExternalErrorMessage) && string.IsNullOrEmpty(m_InternalErrorMessage))
            {
                return "包列表请求失败。";
            }

            if (string.IsNullOrEmpty(m_ExternalErrorMessage))
            {
                return m_InternalErrorMessage;
            }

            if (string.IsNullOrEmpty(m_InternalErrorMessage))
            {
                return m_ExternalErrorMessage;
            }

            return m_ExternalErrorMessage + "\n" + m_InternalErrorMessage;
        }

        /// <summary>
        /// 绘制单边仓库失败提示，但不阻断另一侧仓库已成功的列表展示。
        /// </summary>
        private void DrawRepoWarnings()
        {
            if (!string.IsNullOrEmpty(m_ExternalErrorMessage) && m_ExternalPackages != null && m_ExternalPackages.Count == 0 && m_InternalPackages != null && m_InternalPackages.Count > 0)
            {
                EditorUtil.Draw.Space(8f);
                EditorUtil.Draw.HelpBox(MessageType.Warning, new[] { m_ExternalErrorMessage }, false);
            }

            if (!string.IsNullOrEmpty(m_InternalErrorMessage) && m_InternalPackages != null && m_InternalPackages.Count == 0 && m_ExternalPackages != null && m_ExternalPackages.Count > 0)
            {
                EditorUtil.Draw.Space(8f);
                EditorUtil.Draw.HelpBox(MessageType.Warning, new[] { m_InternalErrorMessage }, false);
            }
        }
    }
}
