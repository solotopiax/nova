/***************************************************************
 * (c) copyright 2026 - 2030, Solotopia
 * All Rights Reserved.
 * -------------------------------------------------------------
 * filename:  SDKComponentInspector.Visitors.cs
 * author:    taoye
 * created:   2026/4/28
 * descrip:   SDK 组件编辑器面板定制 —— 属性与字段
 ***************************************************************/

using System.Collections.Generic;
using UnityEditor;

namespace NovaFramework.Editor
{
    internal sealed partial class SDKComponentInspector : BaseComponentInspector
    {
        /// <summary>
        /// 当前管理器类型名称（绑定 SDKComponent.m_CurManagerTypeName）。
        /// </summary>
        private SerializedProperty m_CurManagerTypeName;

        /// <summary>
        /// 插件条目列表属性（绑定 SDKComponent.m_PluginEntries）。
        /// </summary>
        private SerializedProperty m_PluginEntries;

        /// <summary>
        /// 管理器所有可用类型名称列表，供 TypesSelector 下拉使用。
        /// </summary>
        private List<string> m_ManagerTypeNames;

        /// <summary>
        /// Plugin 条目列表绘制器，封装反射扫描、分组展示与 Missing 清理逻辑。
        /// </summary>
        private PluginEntriesDrawer m_Drawer;
    }
}
