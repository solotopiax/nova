/***************************************************************
 * (c) copyright 2026 - 2030, Solotopia
 * All Rights Reserved.
 * -------------------------------------------------------------
 * filename:  ConfigWindow.AgentEditing.cs
 * author:    taoye
 * created:   2026/10/12
 * descrip:   ConfigWindow 草稿保护与共享保存刷新适配器
 ***************************************************************/

using UnityEditor;
using UnityEngine;

namespace NovaFramework.Editor
{
    internal sealed partial class ConfigWindow
    {
        /// <summary>调用窗口自行维护 WorkingCopy 重建语义，保存通知只刷新其它干净窗口。</summary>
        private bool m_IsPublishingSaveNotification;
        /// <summary>以适配器注册草稿保护与保存后刷新，工具层不反向依赖 Window。</summary>
        [InitializeOnLoadMethod]
        private static void RegisterAgentEditingAdapter()
        {
            EditorUtil.Config.DesignTimeEditing.RegisterDraftGuard(HasUnsavedMasterDraft);
            EditorUtil.Config.DesignTimeEditing.RegisterSavedListener(RefreshSavedMasterWindows);
        }

        /// <summary>检测所有实际打开的窗口草稿，包括尚未提交的 SerializedProperty 与待执行 UI 请求。</summary>
        private static bool HasUnsavedMasterDraft(ConfigMasterSO master)
        {
            foreach (ConfigWindow window in Resources.FindObjectsOfTypeAll<ConfigWindow>())
            {
                if (window == null || window.m_Master != master) continue;
                if (window.m_IsDirty || window.m_HasPendingSave || window.m_HasPendingCoordSwitch ||
                    (window.m_MasterSO != null && window.m_MasterSO.hasModifiedProperties)) return true;
            }
            return false;
        }

        /// <summary>只刷新无未保存草稿的匹配窗口；保留坐标与待导出提示，不扫描、不注入、不写资产。</summary>
        private static void RefreshSavedMasterWindows(ConfigMasterSO master)
        {
            foreach (ConfigWindow window in Resources.FindObjectsOfTypeAll<ConfigWindow>())
            {
                if (window == null || window.m_Master != master || window.m_IsPublishingSaveNotification || window.m_IsDirty ||
                    window.m_HasPendingSave || window.m_HasPendingCoordSwitch ||
                    (window.m_MasterSO != null && window.m_MasterSO.hasModifiedProperties)) continue;
                window.DestroyWorkingCopy();
                window.RebuildWorkingCopy();
                window.m_HasSavedChangesPendingExport = true;
                window.Repaint();
            }
        }
    }
}
