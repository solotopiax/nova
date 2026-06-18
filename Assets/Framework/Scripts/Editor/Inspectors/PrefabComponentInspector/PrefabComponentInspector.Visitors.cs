/***************************************************************
 * (c) copyright 2026 - 2030, Solotopia
 * All Rights Reserved.
 * -------------------------------------------------------------
 * filename:  PrefabComponentInspector.Visitors.cs
 * author:    taoye
 * created:   2026/5/16
 * descrip:   Prefab 组件编辑器面板定制 —— 属性与字段
 ***************************************************************/

using System.Collections.Generic;
using UnityEditor;

namespace NovaFramework.Editor
{
    internal sealed partial class PrefabComponentInspector : BaseComponentInspector
    {
        /// <summary>
        /// 当前 PrefabManager 类型名称。
        /// </summary>
        private SerializedProperty m_CurPrefabManagerTypeName;

        /// <summary>
        /// PrefabManager 所有类型名称。
        /// </summary>
        private List<string> m_PrefabManagerTypeNames;
    }
}
