/***************************************************************
 * (c) copyright 2026 - 2030, Solotopia
 * All Rights Reserved.
 * -------------------------------------------------------------
 * filename:  SDKComponentInspector.Methods.cs
 * author:    taoye
 * created:   2026/4/28
 * descrip:   SDK 组件编辑器面板定制 —— 私有方法
 ***************************************************************/

using UnityEditor;
using UnityEngine;

namespace NovaFramework.Editor
{
    internal sealed partial class SDKComponentInspector : BaseComponentInspector
    {
        /// <summary>
        /// 绘制配置区域：SDK Manager 类型选择器 + HelpBox 说明 + 分隔线。
        /// </summary>
        private void DrawConfigs()
        {
            EditorUtil.Draw.TypesSelector("SDK 管理器", m_ManagerTypeNames, m_CurManagerTypeName, true, null, GUILayout.Width(180f));
            EditorUtil.Draw.HelpBox(MessageType.Info, new[] { "支持自定义类型，实现框架层 ISDKManager 接口后，该类型将自动出现在此列表中。" });
            EditorUtil.Draw.Line();
        }

        /// <summary>
        /// 绘制 Plugin 条目列表区域：先执行增量同步（捕获新反射到的类型），再委托 Drawer 绘制。
        /// </summary>
        private void DrawPluginEntries()
        {
            m_Drawer.SyncEntries(m_PluginEntries, serializedObject);
            m_Drawer.Draw(m_PluginEntries, serializedObject);
        }
    }
}
