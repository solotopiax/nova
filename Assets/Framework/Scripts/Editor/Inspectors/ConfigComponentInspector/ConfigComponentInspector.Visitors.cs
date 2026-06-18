/***************************************************************
 * (c) copyright 2026 - 2030, Solotopia
 * All Rights Reserved.
 * -------------------------------------------------------------
 * filename:  ConfigComponentInspector.Visitors.cs
 * author:    taoye
 * created:   2026/3/4
 * descrip:   配置组件编辑器面板定制 —— 属性与字段
 ***************************************************************/

using System.Collections.Generic;
using UnityEditor;

namespace NovaFramework.Editor
{
    internal sealed partial class ConfigComponentInspector : BaseComponentInspector
    {
        /// <summary>
        /// 当前 Config 管理器类型名称。
        /// </summary>
        private SerializedProperty m_CurManagerTypeName;

        /// <summary>
        /// Asset 地址。
        /// </summary>
        private SerializedProperty m_AssetLocationSP;

        /// <summary>
        /// Config 管理器所有类型名称。
        /// </summary>
        private List<string> m_ManagerTypeNames;

    }
}
