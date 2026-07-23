/***************************************************************
 * (c) copyright 2026 - 2030, Solotopia
 * All Rights Reserved.
 * -------------------------------------------------------------
 * filename:  SDKComponentInspector.cs
 * author:    taoye
 * created:   2026/4/28
 * descrip:   SDK 组件编辑器面板定制
 ***************************************************************/

using System.Collections.Generic;
using NovaFramework.Runtime;
using UnityEditor;

namespace NovaFramework.Editor
{
    /// <summary>
    /// SDK 组件编辑器面板定制。
    /// 负责 Manager 选择器、Plugin 条目列表（含分组 + Missing 清理）的 Inspector 绘制。
    /// Play 模式下所有控件通过 disableOnPlaying=true 自动禁用，不展示运行时数据面板。
    /// </summary>
    [CustomEditor(typeof(SDKComponent))]
    internal sealed partial class SDKComponentInspector : BaseComponentInspector
    {
        /// <summary>
        /// 启用：绑定 SerializedProperty，收集 Manager 类型名，扫描 Plugin 类型并执行初始同步。
        /// </summary>
        protected override void OnEnable()
        {
            base.OnEnable();

            m_CurManagerTypeName = serializedObject.FindProperty("m_CurManagerTypeName");
            m_PluginEntries = serializedObject.FindProperty("m_PluginEntries");

            m_ManagerTypeNames = new List<string>(EditorUtil.TypeCache.GetTypeNames(typeof(ISDKManager)));

            m_Drawer = new PluginEntriesDrawer();
            m_Drawer.SyncEntries(m_PluginEntries, serializedObject);
            EditorUtil.Config.Events.ActiveConfigMasterSaved += OnActiveConfigMasterSaved;
        }

        /// <summary>
        /// 禁用：释放 PluginEntriesDrawer（清理 Foldout 缓存）。
        /// </summary>
        private void OnDisable()
        {
            EditorUtil.Config.Events.ActiveConfigMasterSaved -= OnActiveConfigMasterSaved;
            m_Drawer?.Dispose();
            m_Drawer = null;
        }

        /// <summary>
        /// ConfigMaster 保存后刷新 SDK Plugin 可见列表。
        /// </summary>
        /// <param name="master">刚保存的 ConfigMaster。</param>
        private void OnActiveConfigMasterSaved(ConfigMasterSO master)
        {
            serializedObject.Update();
            m_Drawer?.ForceRefresh();
            m_Drawer?.SyncEntries(m_PluginEntries, serializedObject);
            Repaint();
        }

        /// <summary>
        /// 绘制 Inspector：Manager 选择器 → Plugin 条目列表 → 打点工具 → 最终刷新。
        /// </summary>
        public override void OnInspectorGUI()
        {
            base.OnInspectorGUI();
            DrawConfigs();
            DrawPluginEntries();
            DrawTrackTools();
            FinalRefreshInspectorGUI();
        }
    }
}
