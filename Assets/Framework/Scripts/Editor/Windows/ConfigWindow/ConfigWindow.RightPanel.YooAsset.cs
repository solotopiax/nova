/***************************************************************
 * (c) copyright 2026 - 2030, Solotopia
 * All Rights Reserved.
 * -------------------------------------------------------------
 * filename:  ConfigWindow.RightPanel.YooAsset.cs
 * author:    taoye
 * created:   2026/5/28
 * descrip:   ConfigWindow 右侧面板 YooAsset 配置分片（WorkingCopy 编辑与预览注入）
 ***************************************************************/

using NovaFramework.Runtime;
using IOPath = System.IO.Path;
using UnityEditor;
using UnityEngine;

namespace NovaFramework.Editor
{
    internal sealed partial class ConfigWindow : EditorWindow
    {
        /// <summary>
        /// 绘制 YooAsset 配置面板，提供 YooAssetSettings 与 BundleCollectorSetting 两条路径的编辑与浏览。
        /// <para>全部字段与维度操作只修改 WorkingCopy，点击保存后才整体写入 ConfigMaster.asset。</para>
        /// </summary>
        private void DrawRightPanelYooAsset()
        {
            if (m_Master == null) return;
            // 坐标或路径变化时重新 Resolve+Inject；缓存比较在 ReInjectYooAsset 内完成，无变化则跳过，避免每帧 ResetCache+LoadAssetAtPath 造成编辑器卡顿
            ReInjectYooAsset();

            // 内联标题行（标题 + 维度 toggle）；维度投影只修改 WorkingCopy，预览注入也读取该副本。
            DrawYooAssetTitleWithMask();

            EditorUtil.Draw.Layout.Horizontal(() =>
            {
                EditorUtil.Draw.Space(16f);
                EditorUtil.Draw.HelpBox(MessageType.Info, new[]
                {
                    "(1) YooAssetSettingsPath：编辑器期注入 YooAssetConfiguration，避免多 Sample 共存时加载到非预期的配置资产。",
                    "(2) BundleCollectorSettingPath：供 BundleBuilder 步骤显式加载指定 sample 的收集器配置。",
                    "(3) 两个路径均为项目根相对路径（以 Assets/ 开头）。",
                    "(4) 本面板的修改先进入内存副本，点击顶部保存后才写入 ConfigMaster.asset。",
                }, false, GUILayout.ExpandWidth(true));
                EditorUtil.Draw.Space(16f);
            });
            EditorUtil.Draw.Space(8f);

            DrawYooAssetSettingsPathRow();
            EditorUtil.Draw.Space(4f);
            DrawBundleCollectorSettingPathRow();

            EditorUtil.Draw.Space(12f);
            EditorUtil.Draw.Line();
            EditorUtil.Draw.Space(12f);
            DrawYooAssetSettingValueRow("Yoo Folder Name", true);
            EditorUtil.Draw.Space(4f);
            DrawYooAssetSettingValueRow("Package File Prefix", false);
            EditorUtil.Draw.Space(4f);
            EditorUtil.Draw.Layout.Horizontal(() =>
            {
                EditorUtil.Draw.Space(16f);
                EditorUtil.Draw.HelpBox(MessageType.Info, new[]
                {
                    "(1) 此处只读写 ConfigMaster，不会从 YooAssetSettings.asset 反向读取。",
                    "(2) 点击顶部「导出」时，才会把当前维度的值写入上方指定的 YooAssetSettings.asset。",
                    "(3) 支持 {Platform} / {Channel} / {Package} / {Version} / {Time} 占位符。",
                    "(4) {Platform}=当前编辑平台（导出时必须与 Unity Active BuildTarget 一致）；{Channel}=当前渠道；{Package}=YooAsset 包名；{Version}=Application.version；{Time}=导出时间。",
                }, false, GUILayout.ExpandWidth(true));
                EditorUtil.Draw.Space(16f);
            });

            EditorUtil.Draw.Space(16f);
        }

        /// <summary>
        /// YooAsset 面板内联标题行（标题 + 维度 toggle）；维度投影与其它面板一样只作用于 WorkingCopy。
        /// </summary>
        private void DrawYooAssetTitleWithMask()
        {
            if (m_Master == null) return;

            // 修复 1：坐标取 workingSrc（m_WorkingCopy ?? m_Master），与 HybridCLR 面板对齐，避免 TopBar 切坐标写 WorkingCopy 后 YooAsset 面板坐标失步
            ConfigMasterSO workingSrc = m_WorkingCopy != null ? m_WorkingCopy : m_Master;
            PanelDimensionMask mask = workingSrc.YooAssetEditorConfigsMask;
            EditorUtil.Config.DimensionProjector.Coord curCoord = new(
                m_EditingPlatform,
                workingSrc.CurrentChannel,
                workingSrc.CurrentDevelopMode);

            // Undo 标签使用 axis.ToString() 枚举名（Platform / Channel / DevelopMode）；
            // 与原三条硬编码中文相比仅 Undo 历史菜单呈英文，行为语义完全一致，可接受
            DrawTitleWithMaskCore("YooAsset 配置", mask, titleTrailingSpace: 30f,
                onAxisToggled: (axis, enabled) =>
                {
                    GUI.FocusControl(null);
                    EditorGUIUtility.editingTextField = false;
                    try
                    {
                        if (enabled)
                            EditorUtil.Config.DimensionProjector.OnDimensionEnabled(workingSrc, m_MasterSO, EditorUtil.Config.DimensionProjector.PanelKind.YooAssetEditorConfigs, null, curCoord, axis);
                        else
                            EditorUtil.Config.DimensionProjector.OnDimensionDisabled(workingSrc, m_MasterSO, EditorUtil.Config.DimensionProjector.PanelKind.YooAssetEditorConfigs, null, curCoord, axis);
                        m_IsDirty = true;
                        ReInjectYooAsset();
                    }
                    catch (System.Exception exception)
                    {
                        EditorUtility.DisplayDialog("维度切换失败", exception.Message, "知道了");
                    }
                    Repaint();
                });

            // HelpBox：全局唯一 vs 已勾维度（与 DrawPanelTitleWithMask 文案风格对齐）
            EditorUtil.Draw.Layout.Horizontal(() =>
            {
                EditorUtil.Draw.Space(16f);
                if (mask.IsGlobal)
                {
                    EditorUtil.Draw.HelpBox(MessageType.Info, new[]
                    {
                        "(1) 当前是【全局共用一份】：本面板内容不区分平台 / 渠道 / 开发模式，整个工程共用同一份",
                        "(2) 勾选上方任一维度，可以让本面板按该维度的不同取值【分别保存】，每份独立编辑",
                        "(3) 修改会同步所有未勾选维度的组合，点击顶部保存后才落盘",
                    }, false, GUILayout.ExpandWidth(true));
                }
                else
                {
                    System.Text.StringBuilder activeAxes = new();
                    if (mask.ByPlatform) { if (activeAxes.Length > 0) activeAxes.Append(" / "); activeAxes.Append("平台类型"); }
                    if (mask.ByChannel) { if (activeAxes.Length > 0) activeAxes.Append(" / "); activeAxes.Append("渠道类型"); }
                    if (mask.ByDevelopMode) { if (activeAxes.Length > 0) activeAxes.Append(" / "); activeAxes.Append("开发模式"); }
                    string editingDesc = $"平台={m_EditingPlatform} 渠道={workingSrc.CurrentChannel} 模式={workingSrc.CurrentDevelopMode}";
                    EditorUtil.Draw.HelpBox(MessageType.Info, new[]
                    {
                        $"(1) 当前按【{activeAxes}】分别保存：勾选的每个维度，其每种取值各存一份，互不影响",
                        $"(2) 正在编辑【{editingDesc}】这一份；平台/渠道/模式均可在顶部切换",
                        "(3) 未勾选的维度仍然共用同一份",
                        "(4) ⚠ 取消任一维度的勾选 = 把当前份合并到该维全部取值，其它份内容会被永久丢弃",
                    }, false, GUILayout.ExpandWidth(true));
                }
                EditorUtil.Draw.Space(16f);
            });
            EditorUtil.Draw.Space(4f);
        }

        /// <summary>
        /// 在编辑平台与 Active BuildTarget 一致时，按当前坐标 Resolve YooAsset 两路径并重新注入 YooAssetConfiguration；
        /// 切换坐标或路径写入后调用，缓存未变则跳过，避免每帧 ResetCache+LoadAssetAtPath 卡顿（修复 3）。
        /// </summary>
        private void ReInjectYooAsset()
        {
            if (m_Master == null) return;
            if (!IsEditingPlatformAligned())
            {
                // 不一致期间不改变当前 Unity 的 YooAsset 环境，并清空缓存以保证重新对齐后必定再次注入。
                m_CachedInjectSettingsPath = null;
                m_CachedInjectPlatform = PlatformType.None;
                return;
            }
            ConfigMasterSO workingSrc = m_WorkingCopy != null ? m_WorkingCopy : m_Master;
            PlatformType curPlatform = m_EditingPlatform;
            ChannelType curChannel = workingSrc.CurrentChannel;
            DevelopMode curMode = workingSrc.CurrentDevelopMode;
            EditorUtil.Config.DimensionalResolver.YooAssetResult result = EditorUtil.Config.DimensionalResolver.ResolveYooAsset(
                workingSrc, curPlatform, curChannel, curMode);
            // 缓存守卫：路径 + 坐标全等则跳过注入，避免每帧 ResetCache+LoadAssetAtPath 造成编辑器卡顿
            if (result.YooAssetSettingsPath == m_CachedInjectSettingsPath
                && curPlatform == m_CachedInjectPlatform
                && curChannel == m_CachedInjectChannel
                && curMode == m_CachedInjectMode)
            {
                return;
            }
            m_CachedInjectSettingsPath = result.YooAssetSettingsPath;
            m_CachedInjectPlatform = curPlatform;
            m_CachedInjectChannel = curChannel;
            m_CachedInjectMode = curMode;
            EditorUtil.Config.YooAssetInjector.InjectByPath(result.YooAssetSettingsPath);
        }

        /// <summary>
        /// 绘制由 ConfigMaster 单向导出到 YooAssetSettings.asset 的字符串字段。
        /// </summary>
        private void DrawYooAssetSettingValueRow(string label, bool isYooFolderName)
        {
            ConfigMasterSO workingSrc = m_WorkingCopy != null ? m_WorkingCopy : m_Master;
            EditorUtil.Config.DimensionProjector.Coord curCoord = new(
                m_EditingPlatform,
                workingSrc.CurrentChannel,
                workingSrc.CurrentDevelopMode);
            EditorUtil.Config.DimensionalResolver.YooAssetResult resolved =
                EditorUtil.Config.DimensionalResolver.ResolveYooAsset(
                    workingSrc,
                    curCoord.Platform,
                    curCoord.Channel,
                    curCoord.Mode);
            string committedValue = isYooFolderName
                ? resolved.YooFolderName
                : resolved.PackageFilePrefix;

            EditorUtil.Draw.Layout.Horizontal(() =>
            {
                EditorUtil.Draw.Space(16f);
                EditorUtil.Draw.Label(label, false, GUILayout.Width(160f));
                EditorGUI.BeginChangeCheck();
                string editedValue = EditorUtil.Draw.TextField(
                    committedValue ?? string.Empty,
                    false,
                    GUILayout.ExpandWidth(true));
                if (EditorGUI.EndChangeCheck() && editedValue != committedValue)
                {
                    CommitYooAssetSettingValue(isYooFolderName, editedValue, curCoord);
                    m_IsDirty = true;
                }
                EditorUtil.Draw.Space(68f);
            });
        }

        /// <summary>
        /// 将设计态模板值写入 ConfigMaster 当前维度，不修改目标 YooAssetSettings.asset。
        /// </summary>
        private void CommitYooAssetSettingValue(
            bool isYooFolderName,
            string value,
            EditorUtil.Config.DimensionProjector.Coord curCoord)
        {
            ConfigMasterSO workingSrc = m_WorkingCopy != null ? m_WorkingCopy : m_Master;
            YooAssetEditorConfigs target = workingSrc.YooAssetEditorConfigsMask.IsGlobal
                ? workingSrc.YooAssetEditorConfigs
                : EditorUtil.Config.DimensionProjector.EnsureYooAssetEditorConfigsOverrideAtCoord(workingSrc, curCoord);

            if (isYooFolderName)
                target.YooFolderName = value;
            else
                target.PackageFilePrefix = value;

            m_MasterSO?.Update();
        }

        /// <summary>
        /// 绘制 YooAssetSettingsPath 路径行（文本框 + 浏览按钮）；路径变更后更新 WorkingCopy 并刷新预览注入。
        /// 使用普通 TextField 实时提交，避免 DelayedTextField 在切页时丢弃 pending 缓冲。
        /// 提交时依 YooAssetEditorConfigsMask 双分支写入。
        /// </summary>
        private void DrawYooAssetSettingsPathRow()
        {
            ConfigMasterSO workingSrc = m_WorkingCopy != null ? m_WorkingCopy : m_Master;
            EditorUtil.Config.DimensionProjector.Coord curCoord = new(m_EditingPlatform, workingSrc.CurrentChannel, workingSrc.CurrentDevelopMode);
            // 按当前坐标通过 DimensionalResolver 取显示值（正确读取 Override 或顶层默认值）
            string committedPath = EditorUtil.Config.DimensionalResolver.ResolveYooAsset(workingSrc, m_EditingPlatform, workingSrc.CurrentChannel, workingSrc.CurrentDevelopMode).YooAssetSettingsPath;

            EditorUtil.Draw.Layout.Horizontal(() =>
            {
                EditorUtil.Draw.Space(16f);
                EditorUtil.Draw.Label("YooAsset Settings", false, GUILayout.Width(160f));
                // BeginChangeCheck/EndChangeCheck 是纯状态查询非绘制 API，允许裸用。
                // 改为普通 TextField 实时提交（Bug 1 修复）。
                EditorGUI.BeginChangeCheck();
                string editedPath = EditorUtil.Draw.TextField(committedPath, false, GUILayout.ExpandWidth(true));
                if (EditorGUI.EndChangeCheck() && editedPath != committedPath)
                {
                    CommitYooAssetSettingsPath(editedPath, curCoord);
                    m_IsDirty = true;
                }
                EditorUtil.Draw.Button("浏览", false, () => BrowseYooAssetSettingsPath(curCoord), GUILayout.Width(48f));
                EditorUtil.Draw.Space(16f);
            });
        }

        /// <summary>
        /// 绘制 BundleCollectorSettingPath 路径行（文本框 + 浏览按钮）；路径变更后仅更新 WorkingCopy。
        /// 使用普通 TextField 实时提交（Bug 1 修复：同 DrawYooAssetSettingsPathRow）。
        /// 提交时依 YooAssetEditorConfigsMask 双分支写入。
        /// </summary>
        private void DrawBundleCollectorSettingPathRow()
        {
            ConfigMasterSO workingSrc = m_WorkingCopy != null ? m_WorkingCopy : m_Master;
            EditorUtil.Config.DimensionProjector.Coord curCoord = new(m_EditingPlatform, workingSrc.CurrentChannel, workingSrc.CurrentDevelopMode);
            // 按当前坐标通过 DimensionalResolver 取显示值（正确读取 Override 或顶层默认值）
            string committedPath = EditorUtil.Config.DimensionalResolver.ResolveYooAsset(workingSrc, m_EditingPlatform, workingSrc.CurrentChannel, workingSrc.CurrentDevelopMode).BundleCollectorSettingPath;

            EditorUtil.Draw.Layout.Horizontal(() =>
            {
                EditorUtil.Draw.Space(16f);
                EditorUtil.Draw.Label("BundleCollector Setting", false, GUILayout.Width(160f));
                // BeginChangeCheck/EndChangeCheck 是纯状态查询非绘制 API，允许裸用。
                // 改为普通 TextField 实时提交（Bug 1 修复）。
                EditorGUI.BeginChangeCheck();
                string editedPath = EditorUtil.Draw.TextField(committedPath, false, GUILayout.ExpandWidth(true));
                if (EditorGUI.EndChangeCheck() && editedPath != committedPath)
                {
                    CommitBundleCollectorSettingPath(editedPath, curCoord);
                    m_IsDirty = true;
                }
                EditorUtil.Draw.Button("浏览", false, () => BrowseBundleCollectorSettingPath(curCoord), GUILayout.Width(48f));
                EditorUtil.Draw.Space(16f);
            });
        }

        /// <summary>
        /// 提交 YooAssetSettingsPath 值：依 YooAssetEditorConfigsMask 双分支写入 WorkingCopy
        /// （IsGlobal 写顶层，否则写 Override 条目），然后刷新当前坐标的预览注入。
        /// 文本行提交与 Browse 共用同一逻辑（DRY）。
        /// </summary>
        /// <param name="value">要写入的路径值。</param>
        /// <param name="curCoord">当前坐标（由调用方从 workingSrc 构造）。</param>
        private void CommitYooAssetSettingsPath(string value, EditorUtil.Config.DimensionProjector.Coord curCoord)
        {
            ConfigMasterSO workingSrc = m_WorkingCopy != null ? m_WorkingCopy : m_Master;
            PanelDimensionMask mask = workingSrc.YooAssetEditorConfigsMask;
            if (mask.IsGlobal)
            {
                workingSrc.YooAssetEditorConfigs.YooAssetSettingsPath = value;
            }
            else
            {
                YooAssetEditorConfigsOverride ov = EditorUtil.Config.DimensionProjector.EnsureYooAssetEditorConfigsOverrideAtCoord(workingSrc, curCoord);
                if (ov != null) ov.YooAssetSettingsPath = value;
            }
            m_MasterSO?.Update();
            ReInjectYooAsset();
        }

        /// <summary>
        /// 提交 BundleCollectorSettingPath 值：依 YooAssetEditorConfigsMask 双分支写入 WorkingCopy
        /// （IsGlobal 写顶层，否则写 Override 条目）。
        /// 文本行提交与 Browse 共用同一逻辑（DRY）。
        /// </summary>
        /// <param name="value">要写入的路径值。</param>
        /// <param name="curCoord">当前坐标（由调用方从 workingSrc 构造）。</param>
        private void CommitBundleCollectorSettingPath(string value, EditorUtil.Config.DimensionProjector.Coord curCoord)
        {
            ConfigMasterSO workingSrc = m_WorkingCopy != null ? m_WorkingCopy : m_Master;
            PanelDimensionMask mask = workingSrc.YooAssetEditorConfigsMask;
            if (mask.IsGlobal)
            {
                workingSrc.YooAssetEditorConfigs.BundleCollectorSettingPath = value;
            }
            else
            {
                YooAssetEditorConfigsOverride ov = EditorUtil.Config.DimensionProjector.EnsureYooAssetEditorConfigsOverrideAtCoord(workingSrc, curCoord);
                if (ov != null) ov.BundleCollectorSettingPath = value;
            }
            m_MasterSO?.Update();
        }

        /// <summary>
        /// 打开文件浏览对话框，选择 YooAssetSettings.asset；选中后通过 CommitYooAssetSettingsPath 走 IsGlobal/Override 双分支写入并注入（修复 2）。
        /// </summary>
        /// <param name="curCoord">当前坐标（由调用方从 workingSrc 构造）。</param>
        private void BrowseYooAssetSettingsPath(EditorUtil.Config.DimensionProjector.Coord curCoord)
        {
            // 显示值取 WorkingCopy 的已解析路径，确保未保存编辑也能作为浏览起始目录。
            ConfigMasterSO workingSrc = m_WorkingCopy != null ? m_WorkingCopy : m_Master;
            string currentResolved = EditorUtil.Config.DimensionalResolver.ResolveYooAsset(workingSrc, curCoord.Platform, curCoord.Channel, curCoord.Mode).YooAssetSettingsPath;
            string initial = string.IsNullOrEmpty(currentResolved)
                ? Application.dataPath
                : IOPath.GetFullPath(IOPath.Combine(Application.dataPath, "..", currentResolved));
            string absolute = EditorUtility.OpenFilePanel("选择 YooAssetSettings.asset", IOPath.GetDirectoryName(initial), "asset");
            if (string.IsNullOrEmpty(absolute)) return;

            string relative = ToProjectRelativePath(absolute);
            if (string.IsNullOrEmpty(relative)) return;

            CommitYooAssetSettingsPath(relative, curCoord);
            m_IsDirty = true;
            Repaint();
        }

        /// <summary>
        /// 打开文件浏览对话框，选择 BundleCollectorSetting.asset；选中后通过 CommitBundleCollectorSettingPath 走 IsGlobal/Override 双分支写入（修复 2）。
        /// </summary>
        /// <param name="curCoord">当前坐标（由调用方从 workingSrc 构造）。</param>
        private void BrowseBundleCollectorSettingPath(EditorUtil.Config.DimensionProjector.Coord curCoord)
        {
            // 显示值取 Resolved 路径，确保 initialFolder 指向正确目录
            ConfigMasterSO workingSrc = m_WorkingCopy != null ? m_WorkingCopy : m_Master;
            string currentResolved = EditorUtil.Config.DimensionalResolver.ResolveYooAsset(workingSrc, curCoord.Platform, curCoord.Channel, curCoord.Mode).BundleCollectorSettingPath;
            string initial = string.IsNullOrEmpty(currentResolved)
                ? Application.dataPath
                : IOPath.GetFullPath(IOPath.Combine(Application.dataPath, "..", currentResolved));
            string absolute = EditorUtility.OpenFilePanel("选择 BundleCollectorSetting.asset", IOPath.GetDirectoryName(initial), "asset");
            if (string.IsNullOrEmpty(absolute)) return;

            string relative = ToProjectRelativePath(absolute);
            if (string.IsNullOrEmpty(relative)) return;

            CommitBundleCollectorSettingPath(relative, curCoord);
            m_IsDirty = true;
            Repaint();
        }

        /// <summary>
        /// 将绝对路径转换为项目根相对路径（以 Assets/ 或 ProjectSettings/ 开头）；
        /// 路径不在项目内时返回 null 并打印警告。
        /// </summary>
        /// <param name="absolutePath">文件系统绝对路径。</param>
        /// <returns>项目根相对路径；不在项目内时返回 null。</returns>
        private static string ToProjectRelativePath(string absolutePath)
        {
            string projectRoot = IOPath.GetDirectoryName(Application.dataPath);
            if (string.IsNullOrEmpty(projectRoot)) return null;
            // 统一路径分隔符以便比较
            string normalRoot = projectRoot.Replace('\\', '/').TrimEnd('/') + "/";
            string normalAbs = absolutePath.Replace('\\', '/');
            if (!normalAbs.StartsWith(normalRoot))
            {
                Log.Warning(LogTag.Editor, "[ConfigWindow.YooAsset] 选中路径不在项目目录内：{0}", absolutePath);
                return null;
            }
            return normalAbs.Substring(normalRoot.Length);
        }
    }
}
