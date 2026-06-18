/***************************************************************
 * (c) copyright 2026 - 2030, Solotopia
 * All Rights Reserved.
 * -------------------------------------------------------------
 * filename:  ConfigWindow.RightPanel.cs
 * author:    taoye
 * created:   2026/4/29
 * descrip:   ConfigWindow 右侧面板（Luban / Common / SDK 详情）
 ***************************************************************/

using System.Collections.Generic;
using System.Text;
using NovaFramework.Runtime;
using UnityEditor;
using UnityEngine;

namespace NovaFramework.Editor
{
    internal sealed partial class ConfigWindow : EditorWindow
    {
        /// <summary>
        /// 绘制左右分割线（1px 浅灰竖线）。
        /// </summary>
        private void DrawVerticalSeparator()
        {
            GUIStyle separatorStyle = new GUIStyle(GUIStyle.none);
            separatorStyle.normal.background = EditorGUIUtility.whiteTexture;
            Color savedColor = GUI.color;
            GUI.color = new Color(0.35f, 0.35f, 0.35f, 1f);
            GUILayout.Box(GUIContent.none, separatorStyle, GUILayout.Width(1), GUILayout.ExpandHeight(true));
            GUI.color = savedColor;
        }

        /// <summary>
        /// 按左树选中项分发到具体面板绘制方法，左侧先渲染竖分割线再渲染内容；
        /// 所有配置编辑区域统一用 ChangeCheck 检测改动以驱动"保存"按钮可用状态。
        /// </summary>
        private void DrawRightPanel()
        {
            DrawVerticalSeparator();
            EditorUtil.Draw.Layout.Vertical(EditorStyles.helpBox, () =>
            {
                m_RightPanelScrollPos = EditorGUILayout.BeginScrollView(m_RightPanelScrollPos, false, false);
                // YooAsset 面板即时写真实资产（ADR-049 C1 例外），不参与 WorkingCopy 脏检测
                if (m_Master != null && m_SelectedItem == LeftTreeItem.YooAssetConfig)
                {
                    DrawRightPanelYooAsset();
                }
                else
                {
                    EditorGUI.BeginChangeCheck();
                    if (m_Master == null)
                    {
                        DrawBindGuide();
                    }
                    else
                    {
                        switch (m_SelectedItem)
                        {
                            case LeftTreeItem.LubanEnv:
                                DrawLubanSection();
                                break;
                            case LeftTreeItem.Python3Env:
                                DrawPython3Section();
                                break;
                            case LeftTreeItem.HybridCLREnv:
                                DrawHybridCLREnvSection();
                                break;
                            case LeftTreeItem.AppConfig when m_MasterSO != null:
                                DrawCommonPanel();
                                break;
                            case LeftTreeItem.NamespaceConfig when m_MasterSO != null:
                                DrawNamespacePanel();
                                break;
                            case LeftTreeItem.SDKNode when m_SelectedPluginType != null:
                                DrawSDKPanel();
                                break;
                            case LeftTreeItem.HybridCLRConfig when m_MasterSO != null:
                                DrawHybridCLRPanel();
                                break;
                            case LeftTreeItem.KitNode when m_SelectedPluginType != null:
                                DrawKitPanel();
                                break;
                            default:
                                EditorUtil.Draw.HelpBox(MessageType.Info, new[] { "请在左侧选择一项开始编辑。" }, false);
                                break;
                        }
                    }
                    if (EditorGUI.EndChangeCheck())
                    {
                        m_IsDirty = true;
                    }
                }
                EditorGUILayout.EndScrollView();
            });
        }

        /// <summary>
        /// 将指定值提交到 Namespace 存储；IsGlobal 时写顶层字段，否则调 SetNamespaceAtCoord 写 Override。
        /// </summary>
        /// <param name="workingSrc">编辑期 ConfigMasterSO 实例（工作副本或真实资产）。</param>
        /// <param name="coord">目标坐标（Platform × Channel × DevelopMode）。</param>
        /// <param name="mask">当前 Namespace 面板的维度掩码，决定写入分支。</param>
        /// <param name="value">待写入的 Namespace 字符串。</param>
        private void CommitNamespaceValue(ConfigMasterSO workingSrc, EditorUtil.Config.DimensionProjector.Coord coord, PanelDimensionMask mask, string value)
        {
            if (mask.IsGlobal)
            {
                // 全不勾（IsGlobal）：写顶层字段，与原有 IsGlobal 分支行为完全一致（零回归）
                SerializedProperty namespaceProp = m_MasterSO.FindProperty("Namespace");
                if (namespaceProp != null)
                {
                    namespaceProp.stringValue = value;
                    m_MasterSO.ApplyModifiedProperties();
                }
            }
            else
            {
                // 已勾维度：只写目标坐标 Override，不触碰顶层字段，不广播同组
                EditorUtil.Config.DimensionProjector.SetNamespaceAtCoord(workingSrc, coord, value);
                // Override 走 C# List 直接写，需刷新 SerializedObject 避免后续 FindProperty 读到旧值
                m_MasterSO.Update();
            }
        }

        /// <summary>
        /// 绘制命名空间面板；按当前坐标（Platform × Channel × DevelopMode）通过 DimensionalResolver 读取显示值，
        /// 提交时依 NamespaceMask 双分支写入：全不勾写顶层字段，已勾维度调 SetNamespaceAtCoord 写 Override。
        /// 使用普通 TextField 实时提交（Bug 1 修复：DelayedTextField 在切页时会丢弃 pending 缓冲）。
        /// Namespace 面板用普通 TextField 操作顶层字段（非 SerializeReference），控件 ID 基于固定 path 不漂移，不受每帧 Update 影响；每帧 Update 仅对 SDK/Kit 的 SerializeReference 路径有问题，已在对应面板单独做编辑态跳过处理。
        /// </summary>
        private void DrawNamespacePanel()
        {
            // 面板标题行（内联维度掩码三 toggle）+ HelpBox
            ConfigMasterSO workingSrc = m_WorkingCopy != null ? m_WorkingCopy : m_Master;
            DrawPanelTitleWithMask("命名空间配置", workingSrc, EditorUtil.Config.DimensionProjector.PanelKind.Namespace, null);

            m_MasterSO.Update();

            // 按当前坐标通过 DimensionalResolver 取显示值（正确读取 Override 或顶层默认值）
            EditorUtil.Config.DimensionProjector.Coord curCoord = new(workingSrc.CurrentPlatform, workingSrc.CurrentChannel, workingSrc.CurrentDevelopMode);
            string committedNamespace = EditorUtil.Config.DimensionalResolver.ResolveNamespace(workingSrc, curCoord.Platform, curCoord.Channel, curCoord.Mode);
            PanelDimensionMask namespaceMask = workingSrc.NamespaceMask;

            EditorUtil.Draw.Layout.Horizontal(() =>
            {
                EditorUtil.Draw.Space(16f);
                EditorUtil.Draw.Label("Namespace", false, GUILayout.Width(80));
                // EditorGUI.BeginChangeCheck/EndChangeCheck 是纯状态查询非绘制 API，允许裸用。
                EditorGUI.BeginChangeCheck();
                string editedNamespace = EditorUtil.Draw.TextField(committedNamespace, false, GUILayout.ExpandWidth(true));
                if (EditorGUI.EndChangeCheck() && editedNamespace != committedNamespace)
                {
                    CommitNamespaceValue(workingSrc, curCoord, namespaceMask, editedNamespace);
                    m_IsDirty = true;
                }
                EditorUtil.Draw.Space(16f);
            });

            EditorUtil.Draw.Layout.Horizontal(() =>
            {
                EditorUtil.Draw.Space(16f);
                if (namespaceMask.IsGlobal)
                {
                    EditorUtil.Draw.HelpBox(MessageType.Info, new[]
                    {
                        "(1) 当前是【全局共用一份】：整个工程共用同一个 Namespace，不区分平台 / 渠道 / 开发模式",
                        "(2) 用于业务代码生成与资产路径前缀",
                        "(3) 热更红线：Namespace 一旦上线永不改，业务程序集名以此匹配",
                        "(4) 旧客户端业务 dll 程序集名仍是旧值，热推新配置会触发 ProcedureLoadDll 报「业务程序集未加载」直接断更",
                        "(5) 改 Namespace 必须强制更新客户端，禁纯热更",
                        "(6) 当次启动可热更：ProcedureLoadDll 顺序为 LoadManifestAsync → LoadAsync，新 manifest 含新配置时本次启动即解析新版",
                        "(7) 运行期不可刷新：LoadAsync 完成后再次调用幂等短路，期间推下来的新配置不在当前进程重读，需下次启动生效",
                    }, false, GUILayout.ExpandWidth(true));
                }
                else
                {
                    System.Text.StringBuilder activeAxes = new();
                    if (namespaceMask.ByPlatform) { if (activeAxes.Length > 0) activeAxes.Append(" / "); activeAxes.Append("平台类型"); }
                    if (namespaceMask.ByChannel) { if (activeAxes.Length > 0) activeAxes.Append(" / "); activeAxes.Append("渠道类型"); }
                    if (namespaceMask.ByDevelopMode) { if (activeAxes.Length > 0) activeAxes.Append(" / "); activeAxes.Append("开发模式"); }
                    string editingDesc = $"平台={workingSrc.CurrentPlatform} 渠道={workingSrc.CurrentChannel} 模式={workingSrc.CurrentDevelopMode}";
                    EditorUtil.Draw.HelpBox(MessageType.Info, new[]
                    {
                        $"(1) 当前按【{activeAxes}】分别保存，每种取值各存一份，互不影响",
                        $"(2) 正在编辑【{editingDesc}】这一份；要改其它份请先在顶部切换对应坐标",
                        "(3) 热更红线：Namespace 一旦上线永不改，业务程序集名以此匹配",
                        "(4) 旧客户端业务 dll 程序集名仍是旧值，热推新配置会触发 ProcedureLoadDll 报「业务程序集未加载」直接断更",
                        "(5) 改 Namespace 必须强制更新客户端，禁纯热更",
                        "(6) 当次启动可热更：ProcedureLoadDll 顺序为 LoadManifestAsync → LoadAsync，新 manifest 含新配置时本次启动即解析新版",
                        "(7) 运行期不可刷新：LoadAsync 完成后再次调用幂等短路，期间推下来的新配置不在当前进程重读，需下次启动生效",
                    }, false, GUILayout.ExpandWidth(true));
                }
                EditorUtil.Draw.Space(16f);
            });

            EditorUtil.Draw.Space(16f);
        }

        /// <summary>
        /// 维度 toggle 二次确认；勾选与取消使用不同文案。返回 true 才允许调用方执行 onAxisToggled 落盘。
        /// <para>勾选语义：当前一份复制成多份，无数据丢失风险，文案侧重影响范围。</para>
        /// <para>取消语义：合并并丢弃其余份，存在数据永久丢失风险，文案显著标注。</para>
        /// </summary>
        /// <param name="axis">变化的维度轴。</param>
        /// <param name="enabling">true 表示勾选（加维分裂），false 表示取消（减维合并）。</param>
        /// <returns>用户点击确认返回 true，取消返回 false。</returns>
        private static bool ConfirmDimensionToggle(EditorUtil.Config.DimensionProjector.DimensionAxis axis, bool enabling)
        {
            string axisCN = axis switch
            {
                EditorUtil.Config.DimensionProjector.DimensionAxis.Platform => "平台类型",
                EditorUtil.Config.DimensionProjector.DimensionAxis.Channel => "渠道类型",
                _ => "开发模式",
            };
            if (enabling)
            {
                return EditorUtil.Draw.Panel.Confirm(
                    $"开启「按 {axisCN} 分别配置」？",
                    $"开启后，本面板的内容会按不同的 {axisCN} 各存一份，每份相互独立、互不影响。\n\n" +
                    $"当前这份会被复制到每个 {axisCN} 作为初始内容，之后你可以在切换坐标后分别修改。\n\n" +
                    $"未勾选的其它维度仍然共用同一份，不受影响。",
                    "开启分别配置",
                    "取消");
            }
            return EditorUtil.Draw.Panel.Confirm(
                $"⚠ 取消「按 {axisCN} 分别配置」？",
                $"取消后，本面板将合并为所有 {axisCN} 共用一份；保留的就是你当前正在编辑的这一份。\n\n" +
                $"⚠ 该维度下其它 {axisCN} 取值已经分别填过的内容会被永久丢弃，无法撤销。\n\n" +
                $"如果你只是想查看其它取值，不要点这里——切换顶部坐标即可。",
                "合并并丢弃其余",
                "返回");
        }

        /// <summary>
        /// 标题 toggle 行公共核心，供矩阵类五面板与 YooAsset 面板共用；职责仅限绘制「标题 + 按照 + 三维 toggle + 分别配置」行，不画 HelpBox。
        /// <para>titleTrailingSpace：标题后追加的 Space 间距；矩阵类传 30f，YooAsset 类传 0f（标题后无间距）。</para>
        /// <para>onAxisToggled：toggle 状态变化回调，参数为变化的轴与新的启用状态；调用方负责具体落盘语义（矩阵延迟 / YooAsset 即时）。仅在二次确认通过后触发。</para>
        /// </summary>
        /// <param name="title">面板标题文字（使用 m_SectionTitleStyle）。</param>
        /// <param name="mask">当前面板维度掩码。</param>
        /// <param name="titleTrailingSpace">标题后间距，0f 表示无间距。</param>
        /// <param name="onAxisToggled">toggle 变化回调；(axis, enabled) 两参数，enabled=true 为启用，false 为禁用。</param>
        private void DrawTitleWithMaskCore(string title, PanelDimensionMask mask, float titleTrailingSpace, System.Action<EditorUtil.Config.DimensionProjector.DimensionAxis, bool> onAxisToggled)
        {
            // 内联 toggle 为行内紧凑元素，不传固定 Width（豁免 PAT-24 顶层条目对齐约束）
            EditorUtil.Draw.Layout.Horizontal(() =>
            {
                EditorUtil.Draw.LabelInline(title, m_SectionTitleStyle, false);
                if (titleTrailingSpace > 0f)
                    EditorUtil.Draw.Space(titleTrailingSpace);
                EditorUtil.Draw.LabelInline("按照 ", false);

                bool byPlatform = EditorUtil.Draw.ToggleInline("平台类型", mask.ByPlatform, false);
                if (byPlatform != mask.ByPlatform)
                {
                    if (ConfirmDimensionToggle(EditorUtil.Config.DimensionProjector.DimensionAxis.Platform, byPlatform))
                        onAxisToggled(EditorUtil.Config.DimensionProjector.DimensionAxis.Platform, byPlatform);
                    else
                        Repaint();
                }

                bool byChannel = EditorUtil.Draw.ToggleInline("渠道类型", mask.ByChannel, false);
                if (byChannel != mask.ByChannel)
                {
                    if (ConfirmDimensionToggle(EditorUtil.Config.DimensionProjector.DimensionAxis.Channel, byChannel))
                        onAxisToggled(EditorUtil.Config.DimensionProjector.DimensionAxis.Channel, byChannel);
                    else
                        Repaint();
                }

                bool byMode = EditorUtil.Draw.ToggleInline("开发模式", mask.ByDevelopMode, false);
                if (byMode != mask.ByDevelopMode)
                {
                    if (ConfirmDimensionToggle(EditorUtil.Config.DimensionProjector.DimensionAxis.DevelopMode, byMode))
                        onAxisToggled(EditorUtil.Config.DimensionProjector.DimensionAxis.DevelopMode, byMode);
                    else
                        Repaint();
                }

                EditorUtil.Draw.LabelInline(" 分别配置", false);
                EditorUtil.Draw.FlexibleSpace();
            });
        }

        /// <summary>
        /// 绘制面板标题行（标题 Label 内联维度掩码三 toggle）+ 下方 HelpBox；
        /// Common / SDK / Kit / Namespace / HybridCLR 五个面板统一入口。
        /// 同行布局：标题 → "按照 " → ToggleLeft(平台类型) → ToggleLeft(渠道类型) → ToggleLeft(开发模式) → " 分别配置"。
        /// HelpBox 保留在标题行下方，文案与逻辑不变。
        /// </summary>
        /// <param name="title">面板标题文字（保持 m_SectionTitleStyle 大字蓝色）。</param>
        /// <param name="workingSrc">编辑期 ConfigMasterSO 实例（工作副本或真实资产）。</param>
        /// <param name="panelKind">面板种类，决定从哪个 Mask 字段读写。</param>
        /// <param name="typeName">SDK 或 Kit 的配置类型全名；其余 panelKind 传 null 即可。</param>
        private void DrawPanelTitleWithMask(string title, ConfigMasterSO workingSrc, EditorUtil.Config.DimensionProjector.PanelKind panelKind, string typeName)
        {
            if (workingSrc == null)
            {
                EditorUtil.Draw.Layout.Horizontal(() =>
                {
                    EditorUtil.Draw.Label(title, m_SectionTitleStyle, false);
                });
                EditorUtil.Draw.Space(8f);
                return;
            }

            PanelDimensionMask mask;
            switch (panelKind)
            {
                case EditorUtil.Config.DimensionProjector.PanelKind.Common: mask = workingSrc.CommonMask; break;
                case EditorUtil.Config.DimensionProjector.PanelKind.Namespace: mask = workingSrc.NamespaceMask; break;
                case EditorUtil.Config.DimensionProjector.PanelKind.HybridCLR: mask = workingSrc.HybridCLRMask; break;
                case EditorUtil.Config.DimensionProjector.PanelKind.SDK: mask = workingSrc.GetSDKMask(typeName); break;
                default: mask = workingSrc.GetKitMask(typeName); break;
            }

            EditorUtil.Config.DimensionProjector.Coord curCoord = new(
                workingSrc.CurrentPlatform,
                workingSrc.CurrentChannel,
                workingSrc.CurrentDevelopMode);

            DrawTitleWithMaskCore(title, mask, titleTrailingSpace: 30f,
                onAxisToggled: (axis, enabled) =>
                {
                    if (enabled)
                        EditorUtil.Config.DimensionProjector.OnDimensionEnabled(workingSrc, m_MasterSO, panelKind, typeName, curCoord, axis);
                    else
                        EditorUtil.Config.DimensionProjector.OnDimensionDisabled(workingSrc, m_MasterSO, panelKind, typeName, curCoord, axis);
                    m_IsDirty = true;
                    Repaint();
                });

            EditorUtil.Draw.Space(8f);

            // HelpBox：全局唯一 vs 已勾维度（文案与逻辑原样保留）
            EditorUtil.Draw.Layout.Horizontal(() =>
            {
                EditorUtil.Draw.Space(16f);
                if (mask.IsGlobal)
                {
                    EditorUtil.Draw.HelpBox(MessageType.Info, new[]
                    {
                        "(1) 当前是【全局共用一份】：本面板内容不区分平台 / 渠道 / 开发模式，整个工程共用同一份",
                        "(2) 勾选上方任一维度，可以让本面板按该维度的不同取值【分别保存】，每份独立编辑",
                        "(3) 例：勾「平台类型」后，Android / iOS 各一份；切换顶部坐标可分别编辑",
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
                        $"(2) 正在编辑【{editingDesc}】这一份；要改其它份请先在顶部切换对应坐标",
                        "(3) 未勾选的维度仍然共用同一份",
                        "(4) ⚠ 取消任一维度的勾选 = 把当前份合并到该维全部取值，其它份内容会被永久丢弃",
                    }, false, GUILayout.ExpandWidth(true));
                }
                EditorUtil.Draw.Space(16f);
            });
            EditorUtil.Draw.Space(4f);
        }

        /// <summary>
        /// 绘制应用配置（CommonConfig）面板；按当前 Platform × Channel × DevelopMode 三维定位
        /// 到对应 Entry.CommonByMode 条目后直接展开子字段。
        /// </summary>
        private void DrawCommonPanel()
        {
            // 面板标题行（内联维度掩码三 toggle）+ HelpBox
            ConfigMasterSO workingSrc = m_WorkingCopy != null ? m_WorkingCopy : m_Master;
            DrawPanelTitleWithMask("应用配置", workingSrc, EditorUtil.Config.DimensionProjector.PanelKind.Common, null);
            if (!workingSrc.TryGetEntry(workingSrc.CurrentPlatform, workingSrc.CurrentChannel, out PlatformChannelEntry entry))
            {
                EditorUtil.Draw.Layout.Horizontal(() =>
                {
                    EditorUtil.Draw.Space(16f);
                    EditorUtil.Draw.HelpBox(MessageType.Warning, new[] { "未找到对应 Platform×Channel 行。" }, false, GUILayout.ExpandWidth(true));
                    EditorUtil.Draw.Space(16f);
                });
                return;
            }

            m_MasterSO.Update();

            int entryIndex = workingSrc.EditorEntries.IndexOf(entry);
            if (entryIndex < 0)
            {
                EditorUtil.Draw.Layout.Horizontal(() =>
                {
                    EditorUtil.Draw.Space(16f);
                    EditorUtil.Draw.HelpBox(MessageType.Warning, new[] { "Entry 在 EditorEntries 列表中未找到，请检查 ConfigMasterSO 序列化状态。" }, false, GUILayout.ExpandWidth(true));
                    EditorUtil.Draw.Space(16f);
                });
                return;
            }

            SerializedProperty entries = m_MasterSO.FindProperty("m_Entries");
            if (entries == null)
            {
                EditorUtil.Draw.Layout.Horizontal(() =>
                {
                    EditorUtil.Draw.Space(16f);
                    EditorUtil.Draw.HelpBox(MessageType.Warning, new[] { "序列化字段 m_Entries 未找到，请检查 ConfigMasterSO 结构。" }, false, GUILayout.ExpandWidth(true));
                    EditorUtil.Draw.Space(16f);
                });
                return;
            }
            SerializedProperty entryProp = entries.GetArrayElementAtIndex(entryIndex);
            SerializedProperty commonByMode = entryProp.FindPropertyRelative("CommonByMode");
            if (commonByMode == null)
            {
                EditorUtil.Draw.Layout.Horizontal(() =>
                {
                    EditorUtil.Draw.Space(16f);
                    EditorUtil.Draw.HelpBox(MessageType.Warning, new[] { "序列化字段 CommonByMode 未找到，请检查 PlatformChannelEntry 结构。" }, false, GUILayout.ExpandWidth(true));
                    EditorUtil.Draw.Space(16f);
                });
                return;
            }

            DevelopMode mode = workingSrc.CurrentDevelopMode;
            for (int i = 0; i < commonByMode.arraySize; i++)
            {
                SerializedProperty modeEntry = commonByMode.GetArrayElementAtIndex(i);
                SerializedProperty modeProp = modeEntry.FindPropertyRelative("Mode");
                if (modeProp == null || (DevelopMode)modeProp.enumValueIndex != mode) continue;
                SerializedProperty configProp = modeEntry.FindPropertyRelative("Config");
                if (configProp != null)
                {
                    SerializedProperty child = configProp.Copy();
                    SerializedProperty end = configProp.GetEndProperty();
                    bool enterChildren = true;
                    while (child.NextVisible(enterChildren) && !SerializedProperty.EqualContents(child, end))
                    {
                        // TODO: EditorUtil.Draw 未覆盖 SerializedProperty 逐字段遍历绘制场景，待 EditorUtil 扩展后替换
                        // 用 Horizontal + Space(16f) 包裹实现整体左缩进；右侧 Space(16f) 与面板边距对称
                        EditorUtil.Draw.Layout.Horizontal(() =>
                        {
                            EditorUtil.Draw.Space(16f);
                            EditorGUILayout.PropertyField(child, new GUIContent(child.displayName), true);
                            EditorUtil.Draw.Space(16f);
                        });
                        if (!string.IsNullOrEmpty(child.tooltip))
                        {
                            EditorUtil.Draw.Layout.Horizontal(() =>
                            {
                                EditorUtil.Draw.Space(16f);
                                EditorUtil.Draw.HelpBox(MessageType.Info, new[] { child.tooltip }, false, GUILayout.ExpandWidth(true));
                                EditorUtil.Draw.Space(16f);
                            });
                        }
                        enterChildren = false;
                    }
                }
                break;
            }
            m_MasterSO.ApplyModifiedProperties();
            // 字段编辑完成后广播同组格，确保组内数据一致（ChangeCheck 覆盖后追加）
            EditorUtil.Config.DimensionProjector.BroadcastWithinGroup(workingSrc, m_MasterSO, EditorUtil.Config.DimensionProjector.PanelKind.Common, null, new EditorUtil.Config.DimensionProjector.Coord(workingSrc.CurrentPlatform, workingSrc.CurrentChannel, workingSrc.CurrentDevelopMode));
            EditorUtil.Draw.Space(16f);
        }

        /// <summary>
        /// 绘制选中 SDK Plugin 在当前 Platform × Channel × DevelopMode 下的 Config（SerializeReference 条目）。
        /// </summary>
        private void DrawSDKPanel()
        {
            ConfigMasterSO workingSrc = m_WorkingCopy != null ? m_WorkingCopy : m_Master;
            if (!workingSrc.TryGetEntry(workingSrc.CurrentPlatform, workingSrc.CurrentChannel, out PlatformChannelEntry entry))
            {
                // 入口校验失败时仍需渲染占位标题，用类型名兜底
                string fallbackName = m_SelectedPluginType != null ? m_SelectedPluginType.Name : "SDK Plugin";
                EditorUtil.Draw.Layout.Horizontal(() =>
                {
                    EditorUtil.Draw.Label(fallbackName, m_SectionTitleStyle, false);
                });
                EditorUtil.Draw.Space(8f);
                EditorUtil.Draw.Layout.Horizontal(() =>
                {
                    EditorUtil.Draw.Space(16f);
                    EditorUtil.Draw.HelpBox(MessageType.Warning, new[] { "未找到对应 Platform×Channel 行。" }, false, GUILayout.ExpandWidth(true));
                    EditorUtil.Draw.Space(16f);
                });
                return;
            }

            DevelopMode mode = workingSrc.CurrentDevelopMode;
            List<ISDKPluginConfig> sdkConfigs = entry.GetSDKConfigs(mode);
            int idx = sdkConfigs.FindIndex(c => c != null && c.GetType() == m_SelectedPluginType);

            // 优先取 ISDKPluginConfig.DisplayName 作为面板标题，空时回退到类型名
            string pluginName;
            if (idx >= 0 && sdkConfigs[idx] is ISDKPluginConfig cfg && !string.IsNullOrEmpty(cfg.DisplayName))
            {
                pluginName = cfg.DisplayName;
            }
            else
            {
                pluginName = m_SelectedPluginType != null ? m_SelectedPluginType.Name : "SDK Plugin";
            }

            // 面板标题行（内联维度掩码三 toggle）+ HelpBox
            string sdkTypeName = m_SelectedPluginType?.FullName;
            DrawPanelTitleWithMask(pluginName, workingSrc, EditorUtil.Config.DimensionProjector.PanelKind.SDK, sdkTypeName);

            EditorUtil.Draw.Layout.Horizontal(() =>
            {
                EditorUtil.Draw.Space(16f);
                EditorUtil.Draw.HelpBox(MessageType.Info, new[]
                {
                    "(1) 热更红线：ISDKPluginConfig 实现类必须落在 NovaFramework.Runtime（AOT 程序集），禁定义在业务 DLL（Game.Runtime）",
                    "(2) ConfigRuntimeSO 通过 SerializeReference 反序列化 EnabledSDKConfigs",
                    "(3) ProcedureLoadDll 加载顺序为 Manifest → Config → AOT metadata → 业务 DLL",
                    "(4) SO 加载时业务 DLL 类型尚未注册，引用业务 DLL 类型的 SDK 配置会反序列化失败或元素丢失",
                    "(5) 新增 SDK 配置类型须同客户端发版（更新 AOT 程序集），不能纯热更 ConfigRuntimeSO",
                }, false, GUILayout.ExpandWidth(true));
                EditorUtil.Draw.Space(16f);
            });
            EditorUtil.Draw.Space(4f);

            if (idx < 0)
            {
                EditorUtil.Draw.Layout.Horizontal(() =>
                {
                    EditorUtil.Draw.Space(16f);
                    EditorUtil.Draw.HelpBox(MessageType.Info, new[] { "当前组合未启用该 Plugin；勾选左树节点后会自动补 Instance。" }, false, GUILayout.ExpandWidth(true));
                    EditorUtil.Draw.Space(16f);
                });
                return;
            }

            // 正在文本编辑时跳过 SO 重载：每帧 Update() 会重建 SerializeReference 的 managedReference 属性树，
            // 令正在编辑的控件 ID 漂移、recycledEditor 编辑缓冲失配，导致光标卡死且输入无法 flush 回 prop（Bug2 根因）。
            // 切 Platform/Channel/DevelopMode 时 TryApply* 已置 editingTextField=false，下一帧此处正常 Update 读取新坐标格，零回归。
            if (!EditorGUIUtility.editingTextField)
                m_MasterSO.Update();

            int entryIndex = workingSrc.EditorEntries.IndexOf(entry);
            if (entryIndex < 0)
            {
                EditorUtil.Draw.Layout.Horizontal(() =>
                {
                    EditorUtil.Draw.Space(16f);
                    EditorUtil.Draw.HelpBox(MessageType.Warning, new[] { "Entry 在 EditorEntries 列表中未找到，请检查 ConfigMasterSO 序列化状态。" }, false, GUILayout.ExpandWidth(true));
                    EditorUtil.Draw.Space(16f);
                });
                return;
            }

            SerializedProperty entries = m_MasterSO.FindProperty("m_Entries");
            if (entries == null)
            {
                EditorUtil.Draw.Layout.Horizontal(() =>
                {
                    EditorUtil.Draw.Space(16f);
                    EditorUtil.Draw.HelpBox(MessageType.Warning, new[] { "序列化字段 m_Entries 未找到，请检查 ConfigMasterSO 结构。" }, false, GUILayout.ExpandWidth(true));
                    EditorUtil.Draw.Space(16f);
                });
                return;
            }
            SerializedProperty entryProp = entries.GetArrayElementAtIndex(entryIndex);
            SerializedProperty sdkByModeProp = entryProp.FindPropertyRelative("SDKConfigsByMode");
            if (sdkByModeProp == null)
            {
                EditorUtil.Draw.Layout.Horizontal(() =>
                {
                    EditorUtil.Draw.Space(16f);
                    EditorUtil.Draw.HelpBox(MessageType.Warning, new[] { "序列化字段 SDKConfigsByMode 未找到，请检查 PlatformChannelEntry 结构。" }, false, GUILayout.ExpandWidth(true));
                    EditorUtil.Draw.Space(16f);
                });
                return;
            }

            int modeIndex = -1;
            for (int m = 0; m < sdkByModeProp.arraySize; m++)
            {
                SerializedProperty modeEntryProp = sdkByModeProp.GetArrayElementAtIndex(m);
                SerializedProperty modeProp = modeEntryProp.FindPropertyRelative("Mode");
                if (modeProp != null && (DevelopMode)modeProp.enumValueIndex == mode)
                {
                    modeIndex = m;
                    break;
                }
            }

            if (modeIndex < 0)
            {
                EditorUtil.Draw.Layout.Horizontal(() =>
                {
                    EditorUtil.Draw.Space(16f);
                    EditorUtil.Draw.HelpBox(MessageType.Warning, new[] { "未找到对应 DevelopMode 的 SDKConfigs 分组。" }, false, GUILayout.ExpandWidth(true));
                    EditorUtil.Draw.Space(16f);
                });
                return;
            }

            SerializedProperty configsProp = sdkByModeProp.GetArrayElementAtIndex(modeIndex).FindPropertyRelative("SDKConfigs");
            if (configsProp == null)
            {
                EditorUtil.Draw.Layout.Horizontal(() =>
                {
                    EditorUtil.Draw.Space(16f);
                    EditorUtil.Draw.HelpBox(MessageType.Warning, new[] { "序列化字段 SDKConfigs 未找到，请检查 SDKConfigsByModeEntry 结构。" }, false, GUILayout.ExpandWidth(true));
                    EditorUtil.Draw.Space(16f);
                });
                return;
            }
            SerializedProperty target = configsProp.GetArrayElementAtIndex(idx);
            SerializedProperty child = target.Copy();
            SerializedProperty end = target.GetEndProperty();
            bool enterChildren = true;
            // 门控：仅本帧有实际编辑时才广播同组格，避免无意义同组写入
            EditorGUI.BeginChangeCheck();
            while (child.NextVisible(enterChildren) && !SerializedProperty.EqualContents(child, end))
            {
                // TODO: EditorUtil.Draw 未覆盖 SerializedProperty 逐字段遍历绘制场景，待 EditorUtil 扩展后替换
                // 用 Horizontal + Space(16f) 包裹实现整体左缩进；右侧 Space(16f) 与面板边距对称
                EditorUtil.Draw.Layout.Horizontal(() =>
                {
                    EditorUtil.Draw.Space(16f);
                    EditorGUILayout.PropertyField(child, new GUIContent(child.displayName), true);
                    EditorUtil.Draw.Space(16f);
                });
                if (!string.IsNullOrEmpty(child.tooltip))
                {
                    EditorUtil.Draw.Layout.Horizontal(() =>
                    {
                        EditorUtil.Draw.Space(16f);
                        EditorUtil.Draw.HelpBox(MessageType.Info, new[] { child.tooltip }, false, GUILayout.ExpandWidth(true));
                        EditorUtil.Draw.Space(16f);
                    });
                }
                enterChildren = false;
            }
            bool sdkFieldChanged = EditorGUI.EndChangeCheck();

            m_MasterSO.ApplyModifiedProperties();
            // 字段确实发生变化时才广播同组格，保证 ADR-055 同组一致性
            if (sdkFieldChanged)
            {
                EditorUtil.Config.DimensionProjector.BroadcastWithinGroup(workingSrc, m_MasterSO, EditorUtil.Config.DimensionProjector.PanelKind.SDK, sdkTypeName, new EditorUtil.Config.DimensionProjector.Coord(workingSrc.CurrentPlatform, workingSrc.CurrentChannel, workingSrc.CurrentDevelopMode));
            }
            EditorUtil.Draw.Space(16f);
        }

        /// <summary>
        /// 绘制选中 Kit Config 在当前 Platform × Channel × DevelopMode 下的 Config（SerializeReference 条目）。
        /// SerializedProperty 路径：m_Entries[entryIndex].KitConfigsByMode[modeIndex].KitConfigs[idx]
        /// </summary>
        private void DrawKitPanel()
        {
            ConfigMasterSO workingSrc = m_WorkingCopy != null ? m_WorkingCopy : m_Master;
            if (!workingSrc.TryGetEntry(workingSrc.CurrentPlatform, workingSrc.CurrentChannel, out PlatformChannelEntry entry))
            {
                // 入口校验失败时仍需渲染占位标题，用类型名兜底
                string fallbackName = m_SelectedPluginType != null ? m_SelectedPluginType.Name : "Kit Config";
                EditorUtil.Draw.Layout.Horizontal(() =>
                {
                    EditorUtil.Draw.Label(fallbackName, m_SectionTitleStyle, false);
                });
                EditorUtil.Draw.Space(8f);
                EditorUtil.Draw.Layout.Horizontal(() =>
                {
                    EditorUtil.Draw.Space(16f);
                    EditorUtil.Draw.HelpBox(MessageType.Warning, new[] { "未找到对应 Platform×Channel 行。" }, false, GUILayout.ExpandWidth(true));
                    EditorUtil.Draw.Space(16f);
                });
                return;
            }

            DevelopMode mode = workingSrc.CurrentDevelopMode;
            List<IKitConfig> kitConfigs = entry.GetKitConfigs(mode);
            int idx = kitConfigs.FindIndex(c => c != null && c.GetType() == m_SelectedPluginType);

            // 优先取 IKitConfig.DisplayName 作为面板标题，空时回退到类型名
            string kitName;
            if (idx >= 0 && kitConfigs[idx] is IKitConfig kitCfg && !string.IsNullOrEmpty(kitCfg.DisplayName))
            {
                kitName = kitCfg.DisplayName;
            }
            else
            {
                kitName = m_SelectedPluginType != null ? m_SelectedPluginType.Name : "Kit Config";
            }

            // 面板标题行（内联维度掩码三 toggle）+ HelpBox
            string kitTypeName = m_SelectedPluginType?.FullName;
            DrawPanelTitleWithMask(kitName, workingSrc, EditorUtil.Config.DimensionProjector.PanelKind.Kit, kitTypeName);

            EditorUtil.Draw.Layout.Horizontal(() =>
            {
                EditorUtil.Draw.Space(16f);
                EditorUtil.Draw.HelpBox(MessageType.Info, new[]
                {
                    "(1) 热更红线：IKitConfig 实现类必须落在 NovaFramework.Runtime（AOT 程序集），禁定义在业务 DLL（Game.Runtime）",
                    "(2) Kit 配置按 Platform×Channel×DevelopMode 三维存储，当前显示的是当前坐标格的配置",
                    "(3) 运行时通过 Nova.Config.GetKitConfig<T>() 按类型取用当前格导出的配置",
                    "(4) 新增 Kit 配置类型须同客户端发版（更新 AOT 程序集），不能纯热更 ConfigRuntimeSO",
                }, false, GUILayout.ExpandWidth(true));
                EditorUtil.Draw.Space(16f);
            });
            EditorUtil.Draw.Space(4f);

            if (idx < 0)
            {
                EditorUtil.Draw.Layout.Horizontal(() =>
                {
                    EditorUtil.Draw.Space(16f);
                    EditorUtil.Draw.HelpBox(MessageType.Info, new[] { "当前组合未启用该 Kit Config；勾选左树节点后会自动补 Instance。" }, false, GUILayout.ExpandWidth(true));
                    EditorUtil.Draw.Space(16f);
                });
                return;
            }

            // 正在文本编辑时跳过 SO 重载：每帧 Update() 会重建 SerializeReference 的 managedReference 属性树，
            // 令正在编辑的控件 ID 漂移、recycledEditor 编辑缓冲失配，导致光标卡死且输入无法 flush 回 prop（Bug2 根因）。
            // 切 Platform/Channel/DevelopMode 时 TryApply* 已置 editingTextField=false，下一帧此处正常 Update 读取新坐标格，零回归。
            if (!EditorGUIUtility.editingTextField)
                m_MasterSO.Update();

            int entryIndex = workingSrc.EditorEntries.IndexOf(entry);
            if (entryIndex < 0)
            {
                EditorUtil.Draw.Layout.Horizontal(() =>
                {
                    EditorUtil.Draw.Space(16f);
                    EditorUtil.Draw.HelpBox(MessageType.Warning, new[] { "Entry 在 EditorEntries 列表中未找到，请检查 ConfigMasterSO 序列化状态。" }, false, GUILayout.ExpandWidth(true));
                    EditorUtil.Draw.Space(16f);
                });
                return;
            }

            SerializedProperty entries = m_MasterSO.FindProperty("m_Entries");
            if (entries == null)
            {
                EditorUtil.Draw.Layout.Horizontal(() =>
                {
                    EditorUtil.Draw.Space(16f);
                    EditorUtil.Draw.HelpBox(MessageType.Warning, new[] { "序列化字段 m_Entries 未找到，请检查 ConfigMasterSO 结构。" }, false, GUILayout.ExpandWidth(true));
                    EditorUtil.Draw.Space(16f);
                });
                return;
            }

            SerializedProperty entryProp = entries.GetArrayElementAtIndex(entryIndex);
            SerializedProperty kitByModeProp = entryProp.FindPropertyRelative("KitConfigsByMode");
            if (kitByModeProp == null)
            {
                EditorUtil.Draw.Layout.Horizontal(() =>
                {
                    EditorUtil.Draw.Space(16f);
                    EditorUtil.Draw.HelpBox(MessageType.Warning, new[] { "序列化字段 KitConfigsByMode 未找到，请检查 PlatformChannelEntry 结构。" }, false, GUILayout.ExpandWidth(true));
                    EditorUtil.Draw.Space(16f);
                });
                return;
            }

            int modeIndex = -1;
            for (int m = 0; m < kitByModeProp.arraySize; m++)
            {
                SerializedProperty modeEntryProp = kitByModeProp.GetArrayElementAtIndex(m);
                SerializedProperty modeProp = modeEntryProp.FindPropertyRelative("Mode");
                if (modeProp != null && (DevelopMode)modeProp.enumValueIndex == mode)
                {
                    modeIndex = m;
                    break;
                }
            }

            if (modeIndex < 0)
            {
                EditorUtil.Draw.Layout.Horizontal(() =>
                {
                    EditorUtil.Draw.Space(16f);
                    EditorUtil.Draw.HelpBox(MessageType.Warning, new[] { "未找到对应 DevelopMode 的 KitConfigs 分组。" }, false, GUILayout.ExpandWidth(true));
                    EditorUtil.Draw.Space(16f);
                });
                return;
            }

            SerializedProperty configsProp = kitByModeProp.GetArrayElementAtIndex(modeIndex).FindPropertyRelative("KitConfigs");
            if (configsProp == null)
            {
                EditorUtil.Draw.Layout.Horizontal(() =>
                {
                    EditorUtil.Draw.Space(16f);
                    EditorUtil.Draw.HelpBox(MessageType.Warning, new[] { "序列化字段 KitConfigs 未找到，请检查 DevelopModeKitEntry 结构。" }, false, GUILayout.ExpandWidth(true));
                    EditorUtil.Draw.Space(16f);
                });
                return;
            }

            SerializedProperty target = configsProp.GetArrayElementAtIndex(idx);
            SerializedProperty child = target.Copy();
            SerializedProperty end = target.GetEndProperty();
            bool enterChildren = true;
            // 门控：仅本帧有实际编辑时才广播同组格，避免无意义同组写入
            EditorGUI.BeginChangeCheck();
            while (child.NextVisible(enterChildren) && !SerializedProperty.EqualContents(child, end))
            {
                // TODO: EditorUtil.Draw 未覆盖 SerializedProperty 逐字段遍历绘制场景，待 EditorUtil 扩展后替换
                // 用 Horizontal + Space(16f) 包裹实现整体左缩进；右侧 Space(16f) 与面板边距对称
                EditorUtil.Draw.Layout.Horizontal(() =>
                {
                    EditorUtil.Draw.Space(16f);
                    EditorGUILayout.PropertyField(child, new GUIContent(child.displayName), true);
                    EditorUtil.Draw.Space(16f);
                });
                if (!string.IsNullOrEmpty(child.tooltip))
                {
                    EditorUtil.Draw.Layout.Horizontal(() =>
                    {
                        EditorUtil.Draw.Space(16f);
                        EditorUtil.Draw.HelpBox(MessageType.Info, new[] { child.tooltip }, false, GUILayout.ExpandWidth(true));
                        EditorUtil.Draw.Space(16f);
                    });
                }
                enterChildren = false;
            }
            bool kitFieldChanged = EditorGUI.EndChangeCheck();

            m_MasterSO.ApplyModifiedProperties();
            // 字段确实发生变化时才广播同组格，保证 ADR-055 同组一致性
            if (kitFieldChanged)
            {
                EditorUtil.Config.DimensionProjector.BroadcastWithinGroup(workingSrc, m_MasterSO, EditorUtil.Config.DimensionProjector.PanelKind.Kit, kitTypeName, new EditorUtil.Config.DimensionProjector.Coord(workingSrc.CurrentPlatform, workingSrc.CurrentChannel, workingSrc.CurrentDevelopMode));
            }
            EditorUtil.Draw.Space(16f);
        }
    }
}
