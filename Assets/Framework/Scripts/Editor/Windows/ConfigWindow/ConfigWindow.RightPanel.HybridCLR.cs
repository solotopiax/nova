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
        /// 整个 HybridCLR 面板（AotMetadataDlls/GameDlls/LinkXmlTargetPath/GameEntranceProcedureName）共用一套 HybridEditorConfigsMask（同进同退）。
        /// </summary>
        private void DrawHybridCLRPanel()
        {
            m_MasterSO.Update();

            // 面板标题行（内联维度掩码三 toggle）+ HelpBox；整个 HybridCLR 字段组共用 HybridEditorConfigsMask
            ConfigMasterSO workingSrc = m_WorkingCopy != null ? m_WorkingCopy : m_Master;
            DrawPanelTitleWithMask("HybridCLR 配置", workingSrc, EditorUtil.Config.DimensionProjector.PanelKind.HybridEditorConfigs, null);

            DrawHybridCLREntranceSection();
            EditorUtil.Draw.Space(8f);
            DrawHybridCLRAotMetadataSection();
            EditorUtil.Draw.Space(8f);
            DrawHybridCLRGameDllSection();
            EditorUtil.Draw.Space(8f);
            DrawHybridCLRLinkXmlSection();

            m_MasterSO.ApplyModifiedProperties();
            // 修复 4：移除每帧无条件 BroadcastWithinGroup。
            // 全部四个字段（GameEntranceProcedureName / LinkXmlTargetPath / AotMetadataDlls / GameDlls）写入时
            // 均经 EnsureHybridEditorConfigsOverride(AtCoord/IndexAtCoord) 裁剪坐标，一条 clipped 条目覆盖整组（靠 MatchesMask），
            // 无需每帧广播同步。每帧调用会触发 ResolveHybridCLR 深拷贝两个 List<DllMasterAssetEntry>，造成不必要的 GC 分配。
            EditorUtil.Draw.Space(16f);
        }

        /// <summary>
        /// 绘制"业务入口 Procedure"section。
        /// 使用普通 TextField 实时提交（Bug 1 修复：DelayedTextField 在切页时会丢弃 pending 缓冲；Bug 2 修复后 masterSO.Update 已不再每帧触发，PAT-22 冲突根源消除，改为实时提交安全）。提交时依 HybridEditorConfigsMask 双分支写入。
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

            SerializedProperty entranceProp = m_MasterSO.FindProperty("HybridEditorConfigs")
                ?.FindPropertyRelative("GameEntranceProcedureName");
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
                    PanelDimensionMask mask = workingSrc.HybridEditorConfigsMask;
                    if (mask.IsGlobal)
                    {
                        // 全不勾：写顶层 SerializedProperty 字段
                        entranceProp.stringValue = editedEntrance;
                        m_MasterSO.ApplyModifiedProperties();
                    }
                    else
                    {
                        // 已勾维度：写 Override 条目
                        HybridEditorConfigsOverride ov = EditorUtil.Config.DimensionProjector.EnsureHybridEditorConfigsOverrideAtCoord(workingSrc, curCoord);
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

            ConfigMasterSO workingSrc = m_WorkingCopy != null ? m_WorkingCopy : m_Master;
            EditorUtil.Config.DimensionProjector.Coord curCoord = new(workingSrc.CurrentPlatform, workingSrc.CurrentChannel, workingSrc.CurrentDevelopMode);
            // 按当前坐标 + HybridEditorConfigsMask 解析目标 SerializedProperty：IsGlobal 回落顶层；mask 非全局时进入坐标即建份（含顶层快照），list 绑 Override 内嵌列表。
            SerializedProperty aotProp = ResolveHybridCLRDllListProp(workingSrc, curCoord, "AotMetadataDlls");
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

            EnsureHybridCLRAotMetadataDllsList(workingSrc, curCoord, aotProp);
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

            ConfigMasterSO workingSrc = m_WorkingCopy != null ? m_WorkingCopy : m_Master;
            EditorUtil.Config.DimensionProjector.Coord curCoord = new(workingSrc.CurrentPlatform, workingSrc.CurrentChannel, workingSrc.CurrentDevelopMode);
            // 按当前坐标 + HybridEditorConfigsMask 解析目标 SerializedProperty：IsGlobal 回落顶层；mask 非全局时进入坐标即建份（含顶层快照），list 绑 Override 内嵌列表。
            SerializedProperty gameProp = ResolveHybridCLRDllListProp(workingSrc, curCoord, "GameDlls");
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

            EnsureHybridCLRGameDllsList(workingSrc, curCoord, gameProp);
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
        /// 使用普通 TextField 实时提交（Bug 1 修复：同 DrawHybridCLREntranceSection）。提交时依 HybridEditorConfigsMask 双分支写入。
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

            SerializedProperty linkXmlProp = m_MasterSO.FindProperty("HybridEditorConfigs")
                ?.FindPropertyRelative("LinkXmlTargetPath");
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
                    PanelDimensionMask mask = workingSrc.HybridEditorConfigsMask;
                    if (mask.IsGlobal)
                    {
                        // 全不勾：写顶层 SerializedProperty 字段
                        linkXmlProp.stringValue = editedLinkXml;
                        m_MasterSO.ApplyModifiedProperties();
                    }
                    else
                    {
                        // 已勾维度：写 Override 条目
                        HybridEditorConfigsOverride ov = EditorUtil.Config.DimensionProjector.EnsureHybridEditorConfigsOverrideAtCoord(workingSrc, curCoord);
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
        /// 按需构建 AOT 元数据 DLL 条目的 ReorderableList；SerializedProperty 路径变化时重建。
        /// 路径随 HybridEditorConfigsMask 与当前坐标动态切换（顶层 AotMetadataDlls ↔ HybridEditorConfigsOverrides[i].AotMetadataDlls），
        /// propertyPath 不同即触发重建，确保 list 始终绑定当前坐标对应的那份列表。
        /// </summary>
        /// <param name="workingSrc">编辑期 ConfigMasterSO 实例（工作副本）。</param>
        /// <param name="curCoord">当前坐标格。</param>
        /// <param name="aotProp">已按当前坐标解析出的 AotMetadataDlls SerializedProperty。</param>
        private void EnsureHybridCLRAotMetadataDllsList(ConfigMasterSO workingSrc, EditorUtil.Config.DimensionProjector.Coord curCoord, SerializedProperty aotProp)
        {
            if (m_HybridCLRAotMetadataDllsList != null && m_HybridCLRAotMetadataDllsList.serializedProperty.propertyPath == aotProp.propertyPath)
            {
                SyncFoldoutCapacity(m_AotDllFoldouts, aotProp.arraySize);
                return;
            }

            m_HybridCLRAotMetadataDllsList = new ReorderableList(m_MasterSO, aotProp, true, true, true, true);
            m_HybridCLRAotMetadataDllsList.drawHeaderCallback = rect => EditorUtil.Draw.Label(rect, $"AOT 元数据 DLL 列表 ({aotProp.arraySize})");
            // lambda 闭包捕获 workingSrc/curCoord/field/aotProp/m_AotDllFoldouts，确保与 m_HybridCLRGameDllsList 使用各自独立的坐标上下文、SerializedProperty 和折叠状态
            m_HybridCLRAotMetadataDllsList.drawElementCallback = (rect, index, isActive, isFocused) => DrawHybridCLRDllEntryElementCore(workingSrc, curCoord, "AotMetadataDlls", aotProp, m_AotDllFoldouts, rect, index);
            m_HybridCLRAotMetadataDllsList.elementHeightCallback = index =>
            {
                // 折叠状态 index 越界时默认收缩，返回单行 header 高度
                bool expanded = index < m_AotDllFoldouts.Count && m_AotDllFoldouts[index];
                return expanded ? EditorGUIUtility.singleLineHeight * 4 + 10f : EditorGUIUtility.singleLineHeight + 4f;
            };
            m_HybridCLRAotMetadataDllsList.onAddCallback = list => OnAddHybridCLRAotMetadataDllEntry(workingSrc, curCoord, "AotMetadataDlls");
            SyncFoldoutCapacity(m_AotDllFoldouts, aotProp.arraySize);
        }

        /// <summary>
        /// 按需构建业务 DLL 条目的 ReorderableList；SerializedProperty 路径变化时重建。
        /// 路径随 HybridEditorConfigsMask 与当前坐标动态切换（顶层 GameDlls ↔ HybridEditorConfigsOverrides[i].GameDlls）。
        /// </summary>
        /// <param name="workingSrc">编辑期 ConfigMasterSO 实例（工作副本）。</param>
        /// <param name="curCoord">当前坐标格。</param>
        /// <param name="gameProp">已按当前坐标解析出的 GameDlls SerializedProperty。</param>
        private void EnsureHybridCLRGameDllsList(ConfigMasterSO workingSrc, EditorUtil.Config.DimensionProjector.Coord curCoord, SerializedProperty gameProp)
        {
            if (m_HybridCLRGameDllsList != null && m_HybridCLRGameDllsList.serializedProperty.propertyPath == gameProp.propertyPath)
            {
                SyncFoldoutCapacity(m_GameDllFoldouts, gameProp.arraySize);
                return;
            }

            m_HybridCLRGameDllsList = new ReorderableList(m_MasterSO, gameProp, true, true, true, true);
            m_HybridCLRGameDllsList.drawHeaderCallback = rect => EditorUtil.Draw.Label(rect, $"业务 DLL 列表 ({gameProp.arraySize})");
            // lambda 闭包捕获 workingSrc/curCoord/field/gameProp/m_GameDllFoldouts，确保与 m_HybridCLRAotMetadataDllsList 使用各自独立的坐标上下文、SerializedProperty 和折叠状态
            m_HybridCLRGameDllsList.drawElementCallback = (rect, index, isActive, isFocused) => DrawHybridCLRDllEntryElementCore(workingSrc, curCoord, "GameDlls", gameProp, m_GameDllFoldouts, rect, index);
            m_HybridCLRGameDllsList.elementHeightCallback = index =>
            {
                // 折叠状态 index 越界时默认收缩，返回单行 header 高度
                bool expanded = index < m_GameDllFoldouts.Count && m_GameDllFoldouts[index];
                return expanded ? EditorGUIUtility.singleLineHeight * 4 + 10f : EditorGUIUtility.singleLineHeight + 4f;
            };
            m_HybridCLRGameDllsList.onAddCallback = list => OnAddHybridCLRGameDllEntry(workingSrc, curCoord, "GameDlls");
            SyncFoldoutCapacity(m_GameDllFoldouts, gameProp.arraySize);
        }

        /// <summary>
        /// 解析当前坐标下 Dll 列表对应的 SerializedProperty，供 ReorderableList 显示与写入绑定。
        /// IsGlobal 时回落顶层字段；mask 非全局时进入坐标即建份（EnsureHybridEditorConfigsOverrideIndexAtCoord
        /// 新建条目时已以顶层快照预填 Dll 列表），故 list 始终绑 Override 内嵌列表，所有写入天然落该坐标份。
        /// </summary>
        /// <param name="workingSrc">编辑期 ConfigMasterSO 实例（工作副本）。</param>
        /// <param name="curCoord">当前坐标格。</param>
        /// <param name="field">字段名（"AotMetadataDlls" 或 "GameDlls"）。</param>
        /// <returns>对应 SerializedProperty；m_MasterSO 为 null 或字段不存在时返回 null。</returns>
        private SerializedProperty ResolveHybridCLRDllListProp(ConfigMasterSO workingSrc, EditorUtil.Config.DimensionProjector.Coord curCoord, string field)
        {
            if (m_MasterSO == null) return null;
            // 进入坐标即建份：mask 非全局时，若当前坐标尚无 Override 条目则新建（含顶层快照）。
            // 先 Find 只读探查：命中则无需 Update（DrawHybridCLRPanel 顶部已 Update），避免每帧 Update 破坏 ReorderableList 文本编辑态；
            // 无命中才 Ensure 建份 + Update 刷新 SO 缓存让 SerializedProperty 看到新条目。
            int idx = EditorUtil.Config.DimensionProjector.FindHybridEditorConfigsOverrideIndexAtCoord(workingSrc, curCoord);
            if (idx < 0)
            {
                // IsGlobal 或无命中：IsGlobal 时 Ensure 也返回 -1，回落顶层字段
                idx = EditorUtil.Config.DimensionProjector.EnsureHybridEditorConfigsOverrideIndexAtCoord(workingSrc, curCoord);
                if (idx >= 0)
                {
                    m_MasterSO.Update();
                }
            }
            if (idx < 0)
            {
                return m_MasterSO.FindProperty("HybridEditorConfigs")?.FindPropertyRelative(field);
            }
            SerializedProperty overridesProp = m_MasterSO.FindProperty("HybridEditorConfigsOverrides");
            if (overridesProp == null || idx >= overridesProp.arraySize)
                return m_MasterSO.FindProperty("HybridEditorConfigs")?.FindPropertyRelative(field);
            return overridesProp.GetArrayElementAtIndex(idx).FindPropertyRelative(field);
        }

        /// <summary>
        /// 同步折叠状态集合容量到当前列表条目数；列表变短时截断尾部，避免遗留状态错位。
        /// </summary>
        /// <param name="foldouts">折叠状态集合。</param>
        /// <param name="count">当前列表条目数。</param>
        private static void SyncFoldoutCapacity(List<bool> foldouts, int count)
        {
            if (foldouts == null) return;
            while (foldouts.Count > count)
            {
                foldouts.RemoveAt(foldouts.Count - 1);
            }
        }

        /// <summary>
        /// 绘制单条 DllMasterAssetEntry 的实际逻辑。
        /// 第一行始终显示 Foldout header（名称取 Asset 地址值，空时显示"(未命名)"）；
        /// 展开时额外绘制三行字段：源位置 / 目标位置 / Asset 地址。
        /// 由 lambda 封装后分别绑定到两个 ReorderableList，通过 workingSrc/curCoord/field/listProp/foldouts 区分来源。
        /// 源位置与目标位置均为项目根相对路径，所见即所得，不追加任何扩展名。
        /// 源位置 / 目标位置行末附带"选择"和"打开文件夹"两个按钮；Asset 地址行保持原样。
        /// 显示值取自 listProp 元素（listProp 已按当前坐标经 ResolveHybridCLRDllListProp 解析，
        /// IsGlobal 或无命中 Override 时回落顶层，故显示正确）；写入统一经 CommitHybridCLRDllEntryField
        /// 懒创建 Override 条目后落盘，确保勾选维度后编辑只影响当前坐标份而不污染全局顶层。
        /// </summary>
        /// <param name="workingSrc">编辑期 ConfigMasterSO 实例（工作副本）。</param>
        /// <param name="curCoord">当前坐标格。</param>
        /// <param name="field">字段名（"AotMetadataDlls" 或 "GameDlls"）。</param>
        /// <param name="listProp">所属列表的 SerializedProperty（已按当前坐标解析）。</param>
        /// <param name="foldouts">该列表对应的折叠状态集合（按 index，自动扩容）。</param>
        /// <param name="rect">绘制区域。</param>
        /// <param name="index">条目索引。</param>
        private void DrawHybridCLRDllEntryElementCore(ConfigMasterSO workingSrc, EditorUtil.Config.DimensionProjector.Coord curCoord, string field, SerializedProperty listProp, List<bool> foldouts, Rect rect, int index)
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

            // 显示值取 listProp 元素（已按坐标解析）；写入经 CommitHybridCLRDllEntryField 懒创建落 Override 份
            // 三字段 subField 名与 DllMasterAssetEntry 私有序列化字段一致
            EditorUtil.Draw.Label(labelRect0, "源位置");
            EditorUtil.Draw.TextField(fieldRect0, sourceLocationProp.stringValue, v => CommitHybridCLRDllEntryField(workingSrc, curCoord, field, index, "m_SourceLocation", v));
            // "选择"/"打开文件夹"按钮不依赖 ref/out，但处于 Rect 绘制上下文中无法改用 EditorUtil.Draw.Button（需 GUILayout 流），保留 GUI.Button。
            if (GUI.Button(pickRect0, "选择")) { OnPickFolderForRelativePathForDllEntry(workingSrc, curCoord, field, index, "m_SourceLocation", sourceLocationProp.stringValue, "选择源位置"); }
            if (GUI.Button(revealRect0, "打开文件夹")) { OnRevealFolderInFinder(sourceLocationProp.stringValue); }

            EditorUtil.Draw.Label(labelRect1, "目标位置");
            EditorUtil.Draw.TextField(fieldRect1, targetLocationProp.stringValue, v => CommitHybridCLRDllEntryField(workingSrc, curCoord, field, index, "m_TargetLocation", v));
            if (GUI.Button(pickRect1, "选择")) { OnPickFolderForRelativePathForDllEntry(workingSrc, curCoord, field, index, "m_TargetLocation", targetLocationProp.stringValue, "选择目标位置"); }
            if (GUI.Button(revealRect1, "打开文件夹")) { OnRevealFolderInFinder(targetLocationProp.stringValue); }

            EditorUtil.Draw.Label(labelRect2, "Asset 地址");
            EditorUtil.Draw.TextField(fieldRect2, assetLocationProp.stringValue, v => CommitHybridCLRDllEntryField(workingSrc, curCoord, field, index, "m_AssetLocation", v));
        }

        /// <summary>
        /// 按当前坐标将 Dll 列表单字段写入落盘。
        /// ResolveHybridCLRDllListProp 内部已 EnsureHybridEditorConfigsOverrideIndexAtCoord + Update，
        /// mask 非全局时 listProp 绑定当前坐标 Override 内嵌列表，写入落该坐标份；
        /// IsGlobal 时绑顶层字段。与字符串字段 EnsureHybridEditorConfigsOverrideAtCoord + ov.Xxx = value 的双分支语义对称。
        /// </summary>
        /// <param name="workingSrc">编辑期 ConfigMasterSO 实例（工作副本）。</param>
        /// <param name="curCoord">当前坐标格。</param>
        /// <param name="field">列表字段名（"AotMetadataDlls" 或 "GameDlls"）。</param>
        /// <param name="index">条目索引。</param>
        /// <param name="subField">条目内子字段名（m_SourceLocation / m_TargetLocation / m_AssetLocation）。</param>
        /// <param name="value">要写入的字符串值。</param>
        private void CommitHybridCLRDllEntryField(ConfigMasterSO workingSrc, EditorUtil.Config.DimensionProjector.Coord curCoord, string field, int index, string subField, string value)
        {
            SerializedProperty listProp = ResolveHybridCLRDllListProp(workingSrc, curCoord, field);
            if (listProp == null || index >= listProp.arraySize) return;
            listProp.GetArrayElementAtIndex(index).FindPropertyRelative(subField).stringValue = value;
            m_MasterSO.ApplyModifiedProperties();
            m_IsDirty = true;
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
            string relative = PickRelativeFolder(title, prop.stringValue);
            if (relative == null) return;
            // 仅写入相对目录路径，文件名由用户手动追加
            prop.stringValue = relative;
            m_MasterSO.ApplyModifiedProperties();
        }

        /// <summary>
        /// Dll 列表元素专用文件夹选择：弹面板选目录后经 CommitHybridCLRDllEntryField 懒创建 Override 落盘，
        /// 确保勾选维度后"选择"按钮只写当前坐标份而不污染全局顶层（与文本字段写入对称）。
        /// </summary>
        /// <param name="workingSrc">编辑期 ConfigMasterSO 实例（工作副本）。</param>
        /// <param name="curCoord">当前坐标格。</param>
        /// <param name="field">列表字段名（"AotMetadataDlls" 或 "GameDlls"）。</param>
        /// <param name="index">条目索引。</param>
        /// <param name="subField">条目内子字段名（m_SourceLocation / m_TargetLocation）。</param>
        /// <param name="currentValue">当前字段值（用于解析 initialFolder）。</param>
        /// <param name="title">文件夹选择面板标题。</param>
        private void OnPickFolderForRelativePathForDllEntry(ConfigMasterSO workingSrc, EditorUtil.Config.DimensionProjector.Coord curCoord, string field, int index, string subField, string currentValue, string title)
        {
            string relative = PickRelativeFolder(title, currentValue);
            if (relative == null) return;
            CommitHybridCLRDllEntryField(workingSrc, curCoord, field, index, subField, relative);
        }

        /// <summary>
        /// 弹出原生文件夹选择面板并把选中目录转为项目根相对路径。
        /// initialFolder 取字段值解析占位符后的所在目录；选中项目根之外的目录则 Log.Warning 返回 null；用户取消返回 null。
        /// </summary>
        /// <param name="title">文件夹选择面板标题。</param>
        /// <param name="currentValue">当前字段值（文件路径，取其所在目录作为 initialFolder）。</param>
        /// <returns>项目根相对目录路径；取消或越界时返回 null。</returns>
        private string PickRelativeFolder(string title, string currentValue)
        {
            string projectDir = SettingsUtil.ProjectDir;
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
                return null;
            }

            // Util.SysIO.Path.GetRelativePath 已在内部归一化为正斜杠，不需要人工追加 /
            string relative = Util.SysIO.Path.GetRelativePath(projectDir, picked);
            if (relative.StartsWith("..") || Util.SysIO.Path.IsPathRooted(relative))
            {
                Log.Warning(LogTag.Editor, "选择的目录不在项目根之内：{0}", picked);
                return null;
            }
            return relative;
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
        /// AOT 元数据 DLL 列表新增条目回调；委托 OnAddHybridCLRDllEntry 处理懒创建与写入。
        /// </summary>
        /// <param name="workingSrc">编辑期 ConfigMasterSO 实例（工作副本）。</param>
        /// <param name="curCoord">当前坐标格。</param>
        /// <param name="field">字段名（"AotMetadataDlls"）。</param>
        private void OnAddHybridCLRAotMetadataDllEntry(ConfigMasterSO workingSrc, EditorUtil.Config.DimensionProjector.Coord curCoord, string field)
        {
            OnAddHybridCLRDllEntry(workingSrc, curCoord, field);
        }

        /// <summary>
        /// 业务 DLL 列表新增条目回调；委托 OnAddHybridCLRDllEntry 处理懒创建与写入。
        /// </summary>
        /// <param name="workingSrc">编辑期 ConfigMasterSO 实例（工作副本）。</param>
        /// <param name="curCoord">当前坐标格。</param>
        /// <param name="field">字段名（"GameDlls"）。</param>
        private void OnAddHybridCLRGameDllEntry(ConfigMasterSO workingSrc, EditorUtil.Config.DimensionProjector.Coord curCoord, string field)
        {
            OnAddHybridCLRDllEntry(workingSrc, curCoord, field);
        }

        /// <summary>
        /// Dll 列表新增条目核心逻辑：三字段（源位置 / 目标位置 / Asset 地址）均置空字符串。
        /// ResolveHybridCLRDllListProp 内部已 EnsureHybridEditorConfigsOverrideIndexAtCoord + Update，
        /// mask 非全局时 listProp 绑定当前坐标 Override 内嵌列表，arraySize++ 落该坐标份；
        /// IsGlobal 时绑顶层字段，行为同原实现。
        /// </summary>
        /// <param name="workingSrc">编辑期 ConfigMasterSO 实例（工作副本）。</param>
        /// <param name="curCoord">当前坐标格。</param>
        /// <param name="field">字段名（"AotMetadataDlls" 或 "GameDlls"）。</param>
        private void OnAddHybridCLRDllEntry(ConfigMasterSO workingSrc, EditorUtil.Config.DimensionProjector.Coord curCoord, string field)
        {
            SerializedProperty listProp = ResolveHybridCLRDllListProp(workingSrc, curCoord, field);
            if (listProp == null) return;
            listProp.arraySize++;
            SerializedProperty newEl = listProp.GetArrayElementAtIndex(listProp.arraySize - 1);
            newEl.FindPropertyRelative("m_SourceLocation").stringValue = "";
            newEl.FindPropertyRelative("m_TargetLocation").stringValue = "";
            newEl.FindPropertyRelative("m_AssetLocation").stringValue = "";
            m_MasterSO.ApplyModifiedProperties();
            m_IsDirty = true;
        }
    }
}
