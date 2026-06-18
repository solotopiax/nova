/***************************************************************
 * (c) copyright 2026 - 2030, Solotopia
 * All Rights Reserved.
 * -------------------------------------------------------------
 * filename:  ConfigWindow.RightPanel.YooAsset.cs
 * author:    taoye
 * created:   2026/5/28
 * descrip:   ConfigWindow 右侧面板 YooAsset 配置分片（路径字段编辑与立即注入）
 ***************************************************************/

using System.Collections.Generic;
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
        /// <para>本面板为 ADR-049 C1 即时落盘模式：路径修改不进 WorkingCopy 暂存，直接写真实资产并 Inject。</para>
        /// <para>维度 toggle（YooAssetMask）已添加，但维度操作（加维/减维/广播）作用于真实资产 m_Master（不同于矩阵类的 WorkingCopy 路径），切换坐标后按新坐标 Resolve 重新 Inject。</para>
        /// </summary>
        private void DrawRightPanelYooAsset()
        {
            if (m_Master == null) return;
            // 坐标或路径变化时重新 Resolve+Inject；缓存比较在 ReInjectYooAsset 内完成，无变化则跳过，避免每帧 ResetCache+LoadAssetAtPath 造成编辑器卡顿
            ReInjectYooAsset();

            // 内联标题行（标题 + 维度 toggle）——YooAsset 面板顶层维度，C1 即时落盘，不走 WorkingCopy 暂存
            // toggle 操作直接作用于真实资产 m_Master，改完即时 SaveAssetIfDirty + Inject 当前坐标份
            DrawYooAssetTitleWithMask();

            EditorUtil.Draw.Layout.Horizontal(() =>
            {
                EditorUtil.Draw.Space(16f);
                EditorUtil.Draw.HelpBox(MessageType.Info, new[]
                {
                    "(1) YooAssetSettingsPath：编辑器期注入 YooAssetConfiguration，避免多 Sample 共存时加载到非预期的配置资产。",
                    "(2) BundleCollectorSettingPath：供 BundleBuilder 步骤显式加载指定 sample 的收集器配置。",
                    "(3) 两个路径均为项目根相对路径（以 Assets/ 开头）。",
                    "(4) 本面板路径修改即时写盘生效，同时点亮顶部保存按钮。",
                }, false, GUILayout.ExpandWidth(true));
                EditorUtil.Draw.Space(16f);
            });
            EditorUtil.Draw.Space(8f);

            DrawYooAssetSettingsPathRow();
            EditorUtil.Draw.Space(4f);
            DrawBundleCollectorSettingPathRow();

            EditorUtil.Draw.Space(16f);
        }

        /// <summary>
        /// YooAsset 面板内联标题行（标题 + 维度 toggle，C1 即时落盘版）。
        /// <para>与矩阵类 DrawPanelTitleWithMask 的区别：toggle 回调作用于真实资产 m_Master 而非 WorkingCopy，
        /// 改完即时 SetDirty + SaveAssetIfDirty，并按新坐标 Resolve 重新 Inject。</para>
        /// </summary>
        private void DrawYooAssetTitleWithMask()
        {
            if (m_Master == null) return;

            // 修复 1：坐标取 workingSrc（m_WorkingCopy ?? m_Master），与 HybridCLR 面板对齐，避免 TopBar 切坐标写 WorkingCopy 后 YooAsset 面板坐标失步
            ConfigMasterSO workingSrc = m_WorkingCopy != null ? m_WorkingCopy : m_Master;
            PanelDimensionMask mask = m_Master.YooAssetMask;
            EditorUtil.Config.DimensionProjector.Coord curCoord = new(
                workingSrc.CurrentPlatform,
                workingSrc.CurrentChannel,
                workingSrc.CurrentDevelopMode);

            // Undo 标签使用 axis.ToString() 枚举名（Platform / Channel / DevelopMode）；
            // 与原三条硬编码中文相比仅 Undo 历史菜单呈英文，行为语义完全一致，可接受
            DrawTitleWithMaskCore("YooAsset 配置", mask, titleTrailingSpace: 30f,
                onAxisToggled: (axis, enabled) =>
                {
                    Undo.RecordObject(m_Master, $"YooAsset 维度 {axis}");
                    if (enabled)
                        EditorUtil.Config.DimensionProjector.OnDimensionEnabled(m_Master, null, EditorUtil.Config.DimensionProjector.PanelKind.YooAsset, null, curCoord, axis);
                    else
                        EditorUtil.Config.DimensionProjector.OnDimensionDisabled(m_Master, null, EditorUtil.Config.DimensionProjector.PanelKind.YooAsset, null, curCoord, axis);
                    EditorUtility.SetDirty(m_Master);
                    AssetDatabase.SaveAssetIfDirty(m_Master);
                    ReInjectYooAsset();
                    SyncYooAssetDimensionToWorkingCopy();
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
                        "(3) 路径修改即时写盘生效，同时点亮顶部保存按钮",
                    }, false, GUILayout.ExpandWidth(true));
                }
                else
                {
                    System.Text.StringBuilder activeAxes = new();
                    if (mask.ByPlatform) { if (activeAxes.Length > 0) activeAxes.Append(" / "); activeAxes.Append("平台类型"); }
                    if (mask.ByChannel) { if (activeAxes.Length > 0) activeAxes.Append(" / "); activeAxes.Append("渠道类型"); }
                    if (mask.ByDevelopMode) { if (activeAxes.Length > 0) activeAxes.Append(" / "); activeAxes.Append("开发模式"); }
                    string editingDesc = $"平台={workingSrc.CurrentPlatform} 渠道={workingSrc.CurrentChannel} 模式={workingSrc.CurrentDevelopMode}";
                    EditorUtil.Draw.HelpBox(MessageType.Info, new[]
                    {
                        $"(1) 当前按【{activeAxes}】分别保存：勾选的每个维度，其每种取值各存一份，互不影响",
                        $"(2) 正在编辑【{editingDesc}】这一份；要改其它份请先在顶部切换对应坐标，路径修改即时写盘",
                        "(3) 未勾选的维度仍然共用同一份",
                        "(4) ⚠ 取消任一维度的勾选 = 把当前份合并到该维全部取值，其它份内容会被永久丢弃",
                    }, false, GUILayout.ExpandWidth(true));
                }
                EditorUtil.Draw.Space(16f);
            });
            EditorUtil.Draw.Space(4f);
        }

        /// <summary>
        /// 按当前坐标 Resolve YooAsset 两路径并重新注入 YooAssetConfiguration；
        /// 切换坐标或路径写入后调用，缓存未变则跳过，避免每帧 ResetCache+LoadAssetAtPath 卡顿（修复 3）。
        /// </summary>
        private void ReInjectYooAsset()
        {
            if (m_Master == null) return;
            ConfigMasterSO workingSrc = m_WorkingCopy != null ? m_WorkingCopy : m_Master;
            PlatformType curPlatform = workingSrc.CurrentPlatform;
            ChannelType curChannel = workingSrc.CurrentChannel;
            DevelopMode curMode = workingSrc.CurrentDevelopMode;
            EditorUtil.Config.DimensionalResolver.YooAssetResult result = EditorUtil.Config.DimensionalResolver.ResolveYooAsset(
                m_Master, curPlatform, curChannel, curMode);
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
        /// 将 m_Master 的 YooAssetMask（三 bool）、YooAssetOverrides 列表以及顶层两路径同步到 m_WorkingCopy，
        /// 防止后续 CommitWorkingCopyToAsset 的 CopySerialized 用旧 WorkingCopy 覆写 m_Master。
        /// <para>YooAsset 维度 toggle 及路径编辑均直写 m_Master（C1 即时落盘），WorkingCopy 不感知；此方法补齐差异。</para>
        /// </summary>
        private void SyncYooAssetDimensionToWorkingCopy()
        {
            if (m_WorkingCopy == null) return;
            m_WorkingCopy.YooAssetMask.ByPlatform = m_Master.YooAssetMask.ByPlatform;
            m_WorkingCopy.YooAssetMask.ByChannel = m_Master.YooAssetMask.ByChannel;
            m_WorkingCopy.YooAssetMask.ByDevelopMode = m_Master.YooAssetMask.ByDevelopMode;
            // YooAssetOverride 字段均为不可变 string，逐元素 new 复制避免共享引用
            m_WorkingCopy.YooAssetOverrides = new List<YooAssetOverride>(m_Master.YooAssetOverrides.Count);
            foreach (YooAssetOverride o in m_Master.YooAssetOverrides)
            {
                m_WorkingCopy.YooAssetOverrides.Add(new YooAssetOverride
                {
                    Platform = o.Platform,
                    Channel = o.Channel,
                    DevelopMode = o.DevelopMode,
                    YooAssetSettingsPath = o.YooAssetSettingsPath,
                    BundleCollectorSettingPath = o.BundleCollectorSettingPath,
                });
            }
            // IsGlobal 写顶层后同步顶层两路径，防止 CopySerialized 以旧 WorkingCopy 值回退即时落盘的顶层字段
            m_WorkingCopy.YooAssetSettingsPath = m_Master.YooAssetSettingsPath;
            m_WorkingCopy.BundleCollectorSettingPath = m_Master.BundleCollectorSettingPath;
        }

        /// <summary>
        /// 绘制 YooAssetSettingsPath 路径行（文本框 + 浏览按钮）；路径变更后立即注入（ADR-049 C1 即时落盘）。
        /// 使用普通 TextField 实时提交（Bug 1 修复：DelayedTextField 在切页时丢弃 pending 缓冲；
        /// C1 即时落盘语义本身要求每次按键都写入 m_Master，实时 TextField 与该语义完全一致）。
        /// 提交时依 YooAssetMask 双分支写入。
        /// </summary>
        private void DrawYooAssetSettingsPathRow()
        {
            // 修复 1：坐标取 workingSrc，显示值解析对象仍为 m_Master（YooAsset C1 即时落盘到 m_Master）
            ConfigMasterSO workingSrc = m_WorkingCopy != null ? m_WorkingCopy : m_Master;
            EditorUtil.Config.DimensionProjector.Coord curCoord = new(workingSrc.CurrentPlatform, workingSrc.CurrentChannel, workingSrc.CurrentDevelopMode);
            // 按当前坐标通过 DimensionalResolver 取显示值（正确读取 Override 或顶层默认值）
            string committedPath = EditorUtil.Config.DimensionalResolver.ResolveYooAsset(m_Master, workingSrc.CurrentPlatform, workingSrc.CurrentChannel, workingSrc.CurrentDevelopMode).YooAssetSettingsPath;

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
        /// 绘制 BundleCollectorSettingPath 路径行（文本框 + 浏览按钮）；路径变更后即时落盘（ADR-049 C1）。
        /// 使用普通 TextField 实时提交（Bug 1 修复：同 DrawYooAssetSettingsPathRow）。
        /// 提交时依 YooAssetMask 双分支写入。
        /// </summary>
        private void DrawBundleCollectorSettingPathRow()
        {
            // 修复 1：坐标取 workingSrc，显示值解析对象仍为 m_Master（YooAsset C1 即时落盘到 m_Master）
            ConfigMasterSO workingSrc = m_WorkingCopy != null ? m_WorkingCopy : m_Master;
            EditorUtil.Config.DimensionProjector.Coord curCoord = new(workingSrc.CurrentPlatform, workingSrc.CurrentChannel, workingSrc.CurrentDevelopMode);
            // 按当前坐标通过 DimensionalResolver 取显示值（正确读取 Override 或顶层默认值）
            string committedPath = EditorUtil.Config.DimensionalResolver.ResolveYooAsset(m_Master, workingSrc.CurrentPlatform, workingSrc.CurrentChannel, workingSrc.CurrentDevelopMode).BundleCollectorSettingPath;

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
        /// 提交 YooAssetSettingsPath 值：依 YooAssetMask 双分支写入（IsGlobal 写顶层，否则写 Override 条目），
        /// 然后 SetDirty / SaveAssetIfDirty / SyncYooAssetDimensionToWorkingCopy / ReInjectYooAsset。
        /// 文本行提交与 Browse 共用同一逻辑（DRY）。
        /// </summary>
        /// <param name="value">要写入的路径值。</param>
        /// <param name="curCoord">当前坐标（由调用方从 workingSrc 构造）。</param>
        private void CommitYooAssetSettingsPath(string value, EditorUtil.Config.DimensionProjector.Coord curCoord)
        {
            Undo.RecordObject(m_Master, "修改 YooAssetSettingsPath");
            PanelDimensionMask mask = m_Master.YooAssetMask;
            if (mask.IsGlobal)
            {
                m_Master.YooAssetSettingsPath = value;
            }
            else
            {
                YooAssetOverride ov = EditorUtil.Config.DimensionProjector.EnsureYooAssetOverrideAtCoord(m_Master, curCoord);
                if (ov != null) ov.YooAssetSettingsPath = value;
            }
            EditorUtility.SetDirty(m_Master);
            AssetDatabase.SaveAssetIfDirty(m_Master);
            SyncYooAssetDimensionToWorkingCopy();
            ReInjectYooAsset();
        }

        /// <summary>
        /// 提交 BundleCollectorSettingPath 值：依 YooAssetMask 双分支写入（IsGlobal 写顶层，否则写 Override 条目），
        /// 然后 SetDirty / SaveAssetIfDirty / SyncYooAssetDimensionToWorkingCopy。
        /// 文本行提交与 Browse 共用同一逻辑（DRY）。
        /// </summary>
        /// <param name="value">要写入的路径值。</param>
        /// <param name="curCoord">当前坐标（由调用方从 workingSrc 构造）。</param>
        private void CommitBundleCollectorSettingPath(string value, EditorUtil.Config.DimensionProjector.Coord curCoord)
        {
            Undo.RecordObject(m_Master, "修改 BundleCollectorSettingPath");
            PanelDimensionMask mask = m_Master.YooAssetMask;
            if (mask.IsGlobal)
            {
                m_Master.BundleCollectorSettingPath = value;
            }
            else
            {
                YooAssetOverride ov = EditorUtil.Config.DimensionProjector.EnsureYooAssetOverrideAtCoord(m_Master, curCoord);
                if (ov != null) ov.BundleCollectorSettingPath = value;
            }
            EditorUtility.SetDirty(m_Master);
            AssetDatabase.SaveAssetIfDirty(m_Master);
            SyncYooAssetDimensionToWorkingCopy();
        }

        /// <summary>
        /// 打开文件浏览对话框，选择 YooAssetSettings.asset；选中后通过 CommitYooAssetSettingsPath 走 IsGlobal/Override 双分支写入并注入（修复 2）。
        /// </summary>
        /// <param name="curCoord">当前坐标（由调用方从 workingSrc 构造）。</param>
        private void BrowseYooAssetSettingsPath(EditorUtil.Config.DimensionProjector.Coord curCoord)
        {
            // 显示值取 Resolved 路径（m_Master 是落盘源），确保 initialFolder 指向正确目录
            string currentResolved = EditorUtil.Config.DimensionalResolver.ResolveYooAsset(m_Master, curCoord.Platform, curCoord.Channel, curCoord.Mode).YooAssetSettingsPath;
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
            string currentResolved = EditorUtil.Config.DimensionalResolver.ResolveYooAsset(m_Master, curCoord.Platform, curCoord.Channel, curCoord.Mode).BundleCollectorSettingPath;
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
