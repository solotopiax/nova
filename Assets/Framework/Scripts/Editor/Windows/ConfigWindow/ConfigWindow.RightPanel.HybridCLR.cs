/***************************************************************
 * (c) copyright 2026 - 2030, Solotopia
 * All Rights Reserved.
 * -------------------------------------------------------------
 * filename:  ConfigWindow.RightPanel.HybridCLR.cs
 * author:    taoye
 * created:   2026/5/9
 * descrip:   ConfigWindow 右侧面板 HybridCLR 配置分片
 ***************************************************************/

using System.Collections.Generic;
using HybridCLR.Editor;
using NovaFramework.Runtime;
using UnityEditor;
using UnityEditorInternal;
using UnityEngine;

namespace NovaFramework.Editor
{
    internal sealed partial class ConfigWindow : EditorWindow
    {
        /// <summary>
        /// 绘制 HybridCLR 配置面板（业务入口 Procedure / AOT 元数据 DLL / 业务 DLL 三个 section）。
        /// 整个 HybridCLR 面板（AotMetadataDlls/GameDlls/LinkXmlTargetPath/GameEntranceProcedureName）共用一套 HybridCLRMask（同进同退）。
        /// </summary>
        private void DrawHybridCLRPanel()
        {
            m_MasterSO.Update();

            // 面板标题行（内联维度掩码三 toggle）+ HelpBox；整个 HybridCLR 字段组共用 HybridCLRMask
            ConfigMasterSO workingSrc = m_WorkingCopy != null ? m_WorkingCopy : m_Master;
            DrawPanelTitleWithMask("HybridCLR 配置", workingSrc, EditorUtil.Config.DimensionProjector.PanelKind.HybridCLR, null);

            DrawHybridCLREntranceSection();
            EditorUtil.Draw.Space(8f);
            DrawHybridCLRAotMetadataSection();
            EditorUtil.Draw.Space(8f);
            DrawHybridCLRGameDllSection();
            EditorUtil.Draw.Space(8f);
            DrawHybridCLRLinkXmlSection();

            m_MasterSO.ApplyModifiedProperties();
            // 修复 4：移除每帧无条件 BroadcastWithinGroup。
            // 字符串字段（GameEntranceProcedureName / LinkXmlTargetPath）写入时经 EnsureHybridCLROverrideAtCoord 裁剪坐标，
            // 一条 clipped 条目覆盖整组（靠 MatchesMask），无需每帧广播同步。
            // Dll 列表（AotMetadataDlls / GameDlls）当前为顶层字段（Phase 2 待处理），每帧广播对其无实际意义。
            // 每帧调用会触发 ResolveHybridCLR 深拷贝两个 List<DllMasterAssetEntry>，造成不必要的 GC 分配。
            EditorUtil.Draw.Space(16f);
        }

        /// <summary>
        /// 绘制"业务入口 Procedure"section。
        /// 使用普通 TextField 实时提交（Bug 1 修复：DelayedTextField 在切页时会丢弃 pending 缓冲；Bug 2 修复后 masterSO.Update 已不再每帧触发，PAT-22 冲突根源消除，改为实时提交安全）。提交时依 HybridCLRMask 双分支写入。
        /// </summary>
        private void DrawHybridCLREntranceSection()
        {
            EditorUtil.Draw.Layout.Horizontal(() =>
            {
                EditorUtil.Draw.Space(16f);
                EditorUtil.Draw.Label("业务入口 Procedure", m_SectionTitleStyle, false);
                EditorUtil.Draw.Space(16f);
            });
            EditorUtil.Draw.Space(4f);

            SerializedProperty entranceProp = m_MasterSO.FindProperty("GameEntranceProcedureName");
            if (entranceProp == null)
            {
                EditorUtil.Draw.Layout.Horizontal(() =>
                {
                    EditorUtil.Draw.Space(16f);
                    EditorUtil.Draw.HelpBox(MessageType.Warning, new[] { "未找到 GameEntranceProcedureName 字段，请检查 ConfigMasterSO 结构。" }, false, GUILayout.ExpandWidth(true));
                    EditorUtil.Draw.Space(16f);
                });
                EditorUtil.Draw.Space(2f);
                return;
            }

            ConfigMasterSO workingSrc = m_WorkingCopy != null ? m_WorkingCopy : m_Master;
            EditorUtil.Config.DimensionProjector.Coord curCoord = new(workingSrc.CurrentPlatform, workingSrc.CurrentChannel, workingSrc.CurrentDevelopMode);
            // 按当前坐标通过 DimensionalResolver 取显示值（正确读取 Override 或顶层默认值）
            string committedEntrance = EditorUtil.Config.DimensionalResolver.ResolveHybridCLR(workingSrc, curCoord.Platform, curCoord.Channel, curCoord.Mode).GameEntranceProcedureName;

            EditorUtil.Draw.Layout.Horizontal(() =>
            {
                EditorUtil.Draw.Space(32f);
                EditorUtil.Draw.Label("Procedure 名", false, GUILayout.Width(100));
                // BeginChangeCheck/EndChangeCheck 是纯状态查询非绘制 API，允许裸用。
                // 改为普通 TextField 实时提交（Bug 1 修复）；Bug 2 修复后不再每帧 masterSO.Update，PAT-22 冲突根源消除。
                EditorGUI.BeginChangeCheck();
                string editedEntrance = EditorUtil.Draw.TextField(committedEntrance, false, GUILayout.ExpandWidth(true));
                if (EditorGUI.EndChangeCheck() && editedEntrance != committedEntrance)
                {
                    PanelDimensionMask mask = workingSrc.HybridCLRMask;
                    if (mask.IsGlobal)
                    {
                        // 全不勾：写顶层 SerializedProperty 字段
                        entranceProp.stringValue = editedEntrance;
                        m_MasterSO.ApplyModifiedProperties();
                    }
                    else
                    {
                        // 已勾维度：写 Override 条目
                        HybridCLROverride ov = EditorUtil.Config.DimensionProjector.EnsureHybridCLROverrideAtCoord(workingSrc, curCoord);
                        if (ov != null) ov.GameEntranceProcedureName = editedEntrance;
                        m_MasterSO.Update();
                    }
                    m_IsDirty = true;
                }
                EditorUtil.Draw.Space(16f);
            });

            EditorUtil.Draw.Layout.Horizontal(() =>
            {
                EditorUtil.Draw.Space(32f);
                EditorUtil.Draw.HelpBox(MessageType.Info, new[]
                {
                    "(1) 运行时拼接为 {Namespace}.{此字段}",
                    "(2) DLL 加载完成后切换到该 Procedure",
                    "(3) 热更红线：改名后必须确保新 Procedure 类型已存在于本轮热推的业务 DLL，否则 ProcedureLoadDll 阶段抛「入口 Procedure 未找到」直接断更",
                    "(4) 重命名业务入口建议同客户端发版，禁纯热更",
                }, false, GUILayout.ExpandWidth(true));
                EditorUtil.Draw.Space(16f);
            });
            EditorUtil.Draw.Space(2f);
        }

        /// <summary>
        /// 绘制"AOT 元数据 DLL 列表"section。
        /// </summary>
        private void DrawHybridCLRAotMetadataSection()
        {
            EditorUtil.Draw.Layout.Horizontal(() =>
            {
                EditorUtil.Draw.Space(16f);
                EditorUtil.Draw.Label("AOT 元数据 DLL 列表", m_SectionTitleStyle, false);
                EditorUtil.Draw.Space(16f);
            });
            EditorUtil.Draw.Space(4f);

            SerializedProperty aotProp = m_MasterSO.FindProperty("AotMetadataDlls");
            if (aotProp == null)
            {
                EditorUtil.Draw.Layout.Horizontal(() =>
                {
                    EditorUtil.Draw.Space(16f);
                    EditorUtil.Draw.HelpBox(MessageType.Warning, new[] { "未找到 AotMetadataDlls 字段，请检查 ConfigMasterSO 结构。" }, false, GUILayout.ExpandWidth(true));
                    EditorUtil.Draw.Space(16f);
                });
                EditorUtil.Draw.Space(2f);
                return;
            }

            EnsureHybridCLRAotMetadataDllsList(aotProp);
            // 用 Horizontal + Space(32f) + Vertical 包裹，使 ReorderableList 整体缩进 32f 对齐其他子条目；右侧 Space(16f) 与面板边距对称。
            EditorUtil.Draw.Layout.Horizontal(() =>
            {
                EditorUtil.Draw.Space(32f);
                EditorUtil.Draw.Layout.Vertical(() =>
                {
                    m_HybridCLRAotMetadataDllsList.DoLayoutList();
                });
                EditorUtil.Draw.Space(16f);
            });

            EditorUtil.Draw.Layout.Horizontal(() =>
            {
                EditorUtil.Draw.Space(32f);
                EditorUtil.Draw.HelpBox(MessageType.Info, new[]
                {
                    "(1) AOT DLL 按序加载以支持 HybridCLR 泛型共享",
                    "(2) 源位置 / 目标位置为项目根相对的具体文件路径（含文件名与扩展名，如 .dll / .dll.bytes），所见即所得",
                    "(3) Asset 地址为运行期 Asset 模块加载地址",
                    "(4) 路径支持占位符 {ActiveBuildTarget}，自动替换为当前激活构建平台（如 Android / iOS / WebGL）",
                    "(5) 选择按钮仅定位到目标目录，回填后请手动追加文件名",
                    "(6) 热更红线：列表新增的 dll 字节必须同步存在于当前资源系统 manifest，否则 ProcedureLoadDll 阶段 LoadAsync<TextAsset> 会失败",
                    "(7) 改 AOT DLL 列表后必须走完整 Pipify 原子构建（ConfigRuntimeSO + dll 字节 + AB 同 manifest）",
                    "(8) 禁单独热推 ConfigRuntimeSO，必须与 dll 字节同批发布",
                }, false, GUILayout.ExpandWidth(true));
                EditorUtil.Draw.Space(16f);
            });
            EditorUtil.Draw.Space(2f);
        }

        /// <summary>
        /// 绘制"业务 DLL 列表"section。
        /// </summary>
        private void DrawHybridCLRGameDllSection()
        {
            EditorUtil.Draw.Layout.Horizontal(() =>
            {
                EditorUtil.Draw.Space(16f);
                EditorUtil.Draw.Label("业务 DLL 列表", m_SectionTitleStyle, false);
                EditorUtil.Draw.Space(16f);
            });
            EditorUtil.Draw.Space(4f);

            SerializedProperty gameProp = m_MasterSO.FindProperty("GameDlls");
            if (gameProp == null)
            {
                EditorUtil.Draw.Layout.Horizontal(() =>
                {
                    EditorUtil.Draw.Space(16f);
                    EditorUtil.Draw.HelpBox(MessageType.Warning, new[] { "未找到 GameDlls 字段，请检查 ConfigMasterSO 结构。" }, false, GUILayout.ExpandWidth(true));
                    EditorUtil.Draw.Space(16f);
                });
                EditorUtil.Draw.Space(2f);
                return;
            }

            EnsureHybridCLRGameDllsList(gameProp);
            // 用 Horizontal + Space(32f) + Vertical 包裹，使 ReorderableList 整体缩进 32f 对齐其他子条目；右侧 Space(16f) 与面板边距对称。
            EditorUtil.Draw.Layout.Horizontal(() =>
            {
                EditorUtil.Draw.Space(32f);
                EditorUtil.Draw.Layout.Vertical(() =>
                {
                    m_HybridCLRGameDllsList.DoLayoutList();
                });
                EditorUtil.Draw.Space(16f);
            });

            EditorUtil.Draw.Layout.Horizontal(() =>
            {
                EditorUtil.Draw.Space(32f);
                EditorUtil.Draw.HelpBox(MessageType.Info, new[]
                {
                    "(1) 业务 DLL 按序加载后注册程序集",
                    "(2) 源位置 / 目标位置为项目根相对的具体文件路径（含文件名与扩展名，如 .dll / .dll.bytes），所见即所得",
                    "(3) Asset 地址为运行期 Asset 模块加载地址",
                    "(4) 路径支持占位符 {ActiveBuildTarget}，自动替换为当前激活构建平台（如 Android / iOS / WebGL）",
                    "(5) 选择按钮仅定位到目标目录，回填后请手动追加文件名",
                    "(6) 热更红线：列表新增的 dll 字节必须同步存在于当前资源系统 manifest，否则 ProcedureLoadDll 阶段 LoadAsync<TextAsset> 会失败",
                    "(7) 改业务 DLL 列表后必须走完整 Pipify 原子构建（ConfigRuntimeSO + dll 字节 + AB 同 manifest）",
                    "(8) 禁单独热推 ConfigRuntimeSO 或 dll，必须三者同批发布",
                }, false, GUILayout.ExpandWidth(true));
                EditorUtil.Draw.Space(16f);
            });
            EditorUtil.Draw.Space(2f);
        }

        /// <summary>
        /// 绘制"link.xml 配置"section。
        /// 使用普通 TextField 实时提交（Bug 1 修复：同 DrawHybridCLREntranceSection）。提交时依 HybridCLRMask 双分支写入。
        /// </summary>
        private void DrawHybridCLRLinkXmlSection()
        {
            EditorUtil.Draw.Layout.Horizontal(() =>
            {
                EditorUtil.Draw.Space(16f);
                EditorUtil.Draw.Label("link.xml 配置", m_SectionTitleStyle, false);
                EditorUtil.Draw.Space(16f);
            });
            EditorUtil.Draw.Space(4f);

            SerializedProperty linkXmlProp = m_MasterSO.FindProperty("LinkXmlTargetPath");
            if (linkXmlProp == null)
            {
                EditorUtil.Draw.Layout.Horizontal(() =>
                {
                    EditorUtil.Draw.Space(16f);
                    EditorUtil.Draw.HelpBox(MessageType.Warning, new[] { "未找到 LinkXmlTargetPath 字段，请检查 ConfigMasterSO 结构。" }, false, GUILayout.ExpandWidth(true));
                    EditorUtil.Draw.Space(16f);
                });
                EditorUtil.Draw.Space(2f);
                return;
            }

            ConfigMasterSO workingSrc = m_WorkingCopy != null ? m_WorkingCopy : m_Master;
            EditorUtil.Config.DimensionProjector.Coord curCoord = new(workingSrc.CurrentPlatform, workingSrc.CurrentChannel, workingSrc.CurrentDevelopMode);
            // 按当前坐标通过 DimensionalResolver 取显示值（正确读取 Override 或顶层默认值）
            string committedLinkXml = EditorUtil.Config.DimensionalResolver.ResolveHybridCLR(workingSrc, curCoord.Platform, curCoord.Channel, curCoord.Mode).LinkXmlTargetPath;

            EditorUtil.Draw.Layout.Horizontal(() =>
            {
                EditorUtil.Draw.Space(32f);
                EditorUtil.Draw.Label("link.xml 目标位置", false, GUILayout.Width(120));
                // BeginChangeCheck/EndChangeCheck 是纯状态查询非绘制 API，允许裸用。
                // 改为普通 TextField 实时提交（Bug 1 修复）；Bug 2 修复后不再每帧 masterSO.Update，PAT-22 冲突根源消除。
                EditorGUI.BeginChangeCheck();
                string editedLinkXml = EditorUtil.Draw.TextField(committedLinkXml, false, GUILayout.ExpandWidth(true));
                if (EditorGUI.EndChangeCheck() && editedLinkXml != committedLinkXml)
                {
                    PanelDimensionMask mask = workingSrc.HybridCLRMask;
                    if (mask.IsGlobal)
                    {
                        // 全不勾：写顶层 SerializedProperty 字段
                        linkXmlProp.stringValue = editedLinkXml;
                        m_MasterSO.ApplyModifiedProperties();
                    }
                    else
                    {
                        // 已勾维度：写 Override 条目
                        HybridCLROverride ov = EditorUtil.Config.DimensionProjector.EnsureHybridCLROverrideAtCoord(workingSrc, curCoord);
                        if (ov != null) ov.LinkXmlTargetPath = editedLinkXml;
                        m_MasterSO.Update();
                    }
                    m_IsDirty = true;
                }
                EditorUtil.Draw.Space(4f);
                EditorUtil.Draw.Button("选择", false, () => OnPickFolderForRelativePath(linkXmlProp, "选择 link.xml 目标位置"), GUILayout.Width(c_PickButtonWidth));
                EditorUtil.Draw.Space(4f);
                EditorUtil.Draw.Button("打开文件夹", false, () => OnRevealFolderInFinder(committedLinkXml), GUILayout.Width(c_RevealButtonWidth));
                EditorUtil.Draw.Space(16f);
            });

            EditorUtil.Draw.Layout.Horizontal(() =>
            {
                EditorUtil.Draw.Space(32f);
                EditorUtil.Draw.HelpBox(MessageType.Info, new[] { "(1) 项目根相对文件路径（含文件名与 .xml 扩展名），如 Assets/link.xml", "(2) 留空时使用默认值 Assets/link.xml", "(3) 选择按钮仅定位到目标目录，回填后请手动追加文件名 link.xml" }, false, GUILayout.ExpandWidth(true));
                EditorUtil.Draw.Space(16f);
            });
            EditorUtil.Draw.Space(2f);
        }

        /// <summary>
        /// 按需构建 AOT 元数据 DLL 条目的 ReorderableList；SerializedProperty 引用变化时重建。
        /// </summary>
        /// <param name="aotProp">AotMetadataDlls 对应的 SerializedProperty。</param>
        private void EnsureHybridCLRAotMetadataDllsList(SerializedProperty aotProp)
        {
            if (m_HybridCLRAotMetadataDllsList != null && m_HybridCLRAotMetadataDllsList.serializedProperty.propertyPath == aotProp.propertyPath)
            {
                return;
            }

            m_HybridCLRAotMetadataDllsList = new ReorderableList(m_MasterSO, aotProp, true, true, true, true);
            m_HybridCLRAotMetadataDllsList.drawHeaderCallback = rect => EditorUtil.Draw.Label(rect, $"AOT 元数据 DLL 列表 ({aotProp.arraySize})");
            // lambda 闭包捕获 aotProp 与 m_AotDllFoldouts，确保与 m_HybridCLRGameDllsList 使用各自独立的 SerializedProperty 和折叠状态
            m_HybridCLRAotMetadataDllsList.drawElementCallback = (rect, index, isActive, isFocused) => DrawHybridCLRDllEntryElementCore(aotProp, m_AotDllFoldouts, rect, index);
            m_HybridCLRAotMetadataDllsList.elementHeightCallback = index =>
            {
                // 折叠状态 index 越界时默认收缩，返回单行 header 高度
                bool expanded = index < m_AotDllFoldouts.Count && m_AotDllFoldouts[index];
                return expanded ? EditorGUIUtility.singleLineHeight * 4 + 10f : EditorGUIUtility.singleLineHeight + 4f;
            };
            m_HybridCLRAotMetadataDllsList.onAddCallback = list => OnAddHybridCLRAotMetadataDllEntry(aotProp);
        }

        /// <summary>
        /// 按需构建业务 DLL 条目的 ReorderableList；SerializedProperty 引用变化时重建。
        /// </summary>
        /// <param name="gameProp">GameDlls 对应的 SerializedProperty。</param>
        private void EnsureHybridCLRGameDllsList(SerializedProperty gameProp)
        {
            if (m_HybridCLRGameDllsList != null && m_HybridCLRGameDllsList.serializedProperty.propertyPath == gameProp.propertyPath)
            {
                return;
            }

            m_HybridCLRGameDllsList = new ReorderableList(m_MasterSO, gameProp, true, true, true, true);
            m_HybridCLRGameDllsList.drawHeaderCallback = rect => EditorUtil.Draw.Label(rect, $"业务 DLL 列表 ({gameProp.arraySize})");
            // lambda 闭包捕获 gameProp 与 m_GameDllFoldouts，确保与 m_HybridCLRAotMetadataDllsList 使用各自独立的 SerializedProperty 和折叠状态
            m_HybridCLRGameDllsList.drawElementCallback = (rect, index, isActive, isFocused) => DrawHybridCLRDllEntryElementCore(gameProp, m_GameDllFoldouts, rect, index);
            m_HybridCLRGameDllsList.elementHeightCallback = index =>
            {
                // 折叠状态 index 越界时默认收缩，返回单行 header 高度
                bool expanded = index < m_GameDllFoldouts.Count && m_GameDllFoldouts[index];
                return expanded ? EditorGUIUtility.singleLineHeight * 4 + 10f : EditorGUIUtility.singleLineHeight + 4f;
            };
            m_HybridCLRGameDllsList.onAddCallback = list => OnAddHybridCLRGameDllEntry(gameProp);
        }

        /// <summary>
        /// 绘制单条 DllMasterAssetEntry 的实际逻辑。
        /// 第一行始终显示 Foldout header（名称取 Asset 地址值，空时显示"(未命名)"）；
        /// 展开时额外绘制三行字段：源位置 / 目标位置 / Asset 地址。
        /// 由 lambda 封装后分别绑定到两个 ReorderableList，通过 listProp / foldouts 区分来源。
        /// 源位置与目标位置均为项目根相对路径，所见即所得，不追加任何扩展名。
        /// 源位置 / 目标位置行末附带"选择"和"打开文件夹"两个按钮；Asset 地址行保持原样。
        /// </summary>
        /// <param name="listProp">所属列表的 SerializedProperty。</param>
        /// <param name="foldouts">该列表对应的折叠状态集合（按 index，自动扩容）。</param>
        /// <param name="rect">绘制区域。</param>
        /// <param name="index">条目索引。</param>
        private void DrawHybridCLRDllEntryElementCore(SerializedProperty listProp, List<bool> foldouts, Rect rect, int index)
        {
            // 同步 foldouts 容量，index 越界时补 false（默认收缩）
            while (foldouts.Count <= index)
            {
                foldouts.Add(false);
            }

            SerializedProperty element = listProp.GetArrayElementAtIndex(index);
            SerializedProperty sourceLocationProp = element.FindPropertyRelative("m_SourceLocation");
            SerializedProperty targetLocationProp = element.FindPropertyRelative("m_TargetLocation");
            SerializedProperty assetLocationProp = element.FindPropertyRelative("m_AssetLocation");

            float h = EditorGUIUtility.singleLineHeight;
            const float c_Gap = 3f;

            // 第零行：Foldout header，名称取 Asset 地址，空时用占位名
            string headerName = string.IsNullOrEmpty(assetLocationProp.stringValue) ? "(未命名)" : assetLocationProp.stringValue;
            Rect headerRect = new Rect(rect.x, rect.y + 2f, rect.width, h);
            bool currentFoldout = foldouts[index];
            // Foldout 处于 Rect 上下文，必须经 EditorUtil.Draw.Foldout(Rect, ref bool, ...) 封装（PAT-35 / feedback_editor_draw_only）
            foldouts[index] = EditorUtil.Draw.Foldout(headerRect, ref currentFoldout, headerName, true, EditorStyles.foldout);

            // 折叠时仅画 header，不渲染字段行
            if (!foldouts[index])
            {
                return;
            }

            // 展开：从 header 下方偏移一行开始绘制三行字段
            float contentY = rect.y + 2f + h + c_Gap;
            const float c_LabelWidth = 80f;
            const float c_BtnGap = 4f;
            // 源/目标位置行：label + textfield + "选择" + "打开文件夹"
            float btnTotalWidth = c_PickButtonWidth + c_BtnGap + c_RevealButtonWidth;
            float fieldX = rect.x + c_LabelWidth + 4f;
            float fieldWidth = rect.width - c_LabelWidth - 4f - btnTotalWidth - c_BtnGap;
            // Asset 地址行：label + textfield（无按钮，原始宽度）
            float fieldWidthAsset = rect.width - c_LabelWidth - 4f;

            // 第一行：源位置（项目根相对路径）
            Rect labelRect0 = new Rect(rect.x, contentY, c_LabelWidth, h);
            Rect fieldRect0 = new Rect(fieldX, contentY, fieldWidth, h);
            Rect pickRect0 = new Rect(fieldX + fieldWidth + c_BtnGap, contentY, c_PickButtonWidth, h);
            Rect revealRect0 = new Rect(fieldX + fieldWidth + c_BtnGap + c_PickButtonWidth + c_BtnGap, contentY, c_RevealButtonWidth, h);
            // 第二行：目标位置（项目根相对路径，所见即所得）
            Rect labelRect1 = new Rect(rect.x, contentY + h + c_Gap, c_LabelWidth, h);
            Rect fieldRect1 = new Rect(fieldX, contentY + h + c_Gap, fieldWidth, h);
            Rect pickRect1 = new Rect(fieldX + fieldWidth + c_BtnGap, contentY + h + c_Gap, c_PickButtonWidth, h);
            Rect revealRect1 = new Rect(fieldX + fieldWidth + c_BtnGap + c_PickButtonWidth + c_BtnGap, contentY + h + c_Gap, c_RevealButtonWidth, h);
            // 第三行：Asset 地址（无按钮）
            Rect labelRect2 = new Rect(rect.x, contentY + (h + c_Gap) * 2f, c_LabelWidth, h);
            Rect fieldRect2 = new Rect(fieldX, contentY + (h + c_Gap) * 2f, fieldWidthAsset, h);

            EditorUtil.Draw.Label(labelRect0, "源位置");
            EditorUtil.Draw.TextField(fieldRect0, sourceLocationProp);
            // "选择"按钮不依赖 ref/out，但处于 Rect 绘制上下文中无法改用 EditorUtil.Draw.Button（需 GUILayout 流），保留 GUI.Button。
            if (GUI.Button(pickRect0, "选择")) { OnPickFolderForRelativePath(sourceLocationProp, "选择源位置"); }
            if (GUI.Button(revealRect0, "打开文件夹")) { OnRevealFolderInFinder(sourceLocationProp.stringValue); }

            EditorUtil.Draw.Label(labelRect1, "目标位置");
            EditorUtil.Draw.TextField(fieldRect1, targetLocationProp);
            if (GUI.Button(pickRect1, "选择")) { OnPickFolderForRelativePath(targetLocationProp, "选择目标位置"); }
            if (GUI.Button(revealRect1, "打开文件夹")) { OnRevealFolderInFinder(targetLocationProp.stringValue); }

            EditorUtil.Draw.Label(labelRect2, "Asset 地址");
            EditorUtil.Draw.TextField(fieldRect2, assetLocationProp);
        }

        /// <summary>
        /// 弹出原生文件夹选择面板，将用户选中目录转为项目根相对路径后写回 prop。
        /// 字段现为具体文件路径，initialFolder 取字段值解析占位符后的所在目录；用户选完后写入相对目录路径，文件名由用户手动追加。
        /// 选中项目根之外的目录则 Log.Warning 并不写入；用户取消则不写入。
        /// </summary>
        /// <param name="prop">要写入的 SerializedProperty（string 类型）。</param>
        /// <param name="title">文件夹选择面板标题。</param>
        private void OnPickFolderForRelativePath(SerializedProperty prop, string title)
        {
            string projectDir = SettingsUtil.ProjectDir;
            string currentValue = prop.stringValue;
            string initialFolder = projectDir;
            if (!string.IsNullOrEmpty(currentValue))
            {
                string resolved = EditorUtil.HybridCLR.ResolvePathPlaceholders(currentValue);
                string abs = Util.SysIO.Path.GetFullPath(Util.SysIO.Path.Combine(projectDir, resolved));
                // 字段是文件路径 → 取其所在目录作为 initialFolder
                string absDir = Util.SysIO.Path.GetDirectoryName(abs);
                if (!string.IsNullOrEmpty(absDir) && Util.SysIO.Directory.Exists(absDir))
                {
                    initialFolder = absDir;
                }
            }

            string picked = EditorUtil.Draw.OpenFolderPanel(title, initialFolder);
            if (string.IsNullOrEmpty(picked))
            {
                return;
            }

            // Util.SysIO.Path.GetRelativePath 已在内部归一化为正斜杠，不需要人工追加 /
            string relative = Util.SysIO.Path.GetRelativePath(projectDir, picked);
            if (relative.StartsWith("..") || Util.SysIO.Path.IsPathRooted(relative))
            {
                Log.Warning(LogTag.Editor, "选择的目录不在项目根之内：{0}", picked);
                return;
            }

            // 仅写入相对目录路径，文件名由用户手动追加
            prop.stringValue = relative;
            m_MasterSO.ApplyModifiedProperties();
        }

        /// <summary>
        /// 将项目根相对文件路径解析为绝对路径后通过 RevealInFinder 在系统文件管理器中高亮或定位。
        /// 字段为具体文件路径：文件存在时高亮该文件；文件不存在但所在目录存在时回退打开目录；两者均不存在则 Log.Warning。
        /// </summary>
        /// <param name="relativePath">项目根相对文件路径字段值（含文件名与扩展名）。</param>
        private void OnRevealFolderInFinder(string relativePath)
        {
            if (string.IsNullOrEmpty(relativePath))
            {
                Log.Warning(LogTag.Editor, "目标位置无效或不存在：（字段为空）");
                return;
            }

            string resolved = EditorUtil.HybridCLR.ResolvePathPlaceholders(relativePath);
            string absolutePath = Util.SysIO.Path.GetFullPath(Util.SysIO.Path.Combine(SettingsUtil.ProjectDir, resolved));

            // 字段为文件路径：优先按文件高亮；文件不存在时回退到所在目录
            if (Util.SysIO.File.Exists(absolutePath))
            {
                EditorUtil.Draw.RevealInFinder(absolutePath);
                return;
            }

            string absDir = Util.SysIO.Path.GetDirectoryName(absolutePath);
            if (!string.IsNullOrEmpty(absDir) && Util.SysIO.Directory.Exists(absDir))
            {
                EditorUtil.Draw.RevealInFinder(absDir);
                return;
            }

            Log.Warning(LogTag.Editor, "目标位置无效或不存在：{0}（原始：{1}）", absolutePath, relativePath);
        }

        /// <summary>
        /// AOT 元数据 DLL 列表新增条目回调，三字段（源位置 / 目标位置 / Asset 地址）均置空字符串。
        /// </summary>
        /// <param name="aotProp">AotMetadataDlls 对应的 SerializedProperty。</param>
        private void OnAddHybridCLRAotMetadataDllEntry(SerializedProperty aotProp)
        {
            aotProp.arraySize++;
            SerializedProperty newEl = aotProp.GetArrayElementAtIndex(aotProp.arraySize - 1);
            newEl.FindPropertyRelative("m_SourceLocation").stringValue = "";
            newEl.FindPropertyRelative("m_TargetLocation").stringValue = "";
            newEl.FindPropertyRelative("m_AssetLocation").stringValue = "";
            m_MasterSO.ApplyModifiedProperties();
        }

        /// <summary>
        /// 业务 DLL 列表新增条目回调，三字段（源位置 / 目标位置 / Asset 地址）均置空字符串。
        /// </summary>
        /// <param name="gameProp">GameDlls 对应的 SerializedProperty。</param>
        private void OnAddHybridCLRGameDllEntry(SerializedProperty gameProp)
        {
            gameProp.arraySize++;
            SerializedProperty newEl = gameProp.GetArrayElementAtIndex(gameProp.arraySize - 1);
            newEl.FindPropertyRelative("m_SourceLocation").stringValue = "";
            newEl.FindPropertyRelative("m_TargetLocation").stringValue = "";
            newEl.FindPropertyRelative("m_AssetLocation").stringValue = "";
            m_MasterSO.ApplyModifiedProperties();
        }
    }
}
