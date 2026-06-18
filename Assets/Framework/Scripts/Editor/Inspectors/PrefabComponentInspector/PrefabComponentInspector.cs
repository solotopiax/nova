/***************************************************************
 * (c) copyright 2026 - 2030, Solotopia
 * All Rights Reserved.
 * -------------------------------------------------------------
 * filename:  PrefabComponentInspector.cs
 * author:    taoye
 * created:   2026/5/16
 * descrip:   Prefab 组件编辑器面板定制
 ***************************************************************/

using System.Collections.Generic;
using NovaFramework.Runtime;
using UnityEditor;
using UnityEngine;

namespace NovaFramework.Editor
{
    /// <summary>
    /// Prefab 组件编辑器面板定制。
    /// 负责 PrefabManager 类型选择 + 运行时实例列表展示。
    /// </summary>
    [CustomEditor(typeof(PrefabComponent))]
    internal sealed partial class PrefabComponentInspector : BaseComponentInspector
    {
        /// <summary>
        /// 启用。
        /// </summary>
        protected override void OnEnable()
        {
            base.OnEnable();

            m_CurPrefabManagerTypeName = serializedObject.FindProperty("m_CurPrefabManagerTypeName");
            m_PrefabManagerTypeNames = new List<string>(EditorUtil.TypeCache.GetTypeNames(typeof(IPrefabManager)));
        }

        /// <summary>
        /// 绘制。
        /// </summary>
        public override void OnInspectorGUI()
        {
            base.OnInspectorGUI();
            DrawConfigs();
            FinalRefreshInspectorGUI();
        }

        /// <summary>
        /// 运行时实时刷新，确保实例计数及时更新。
        /// </summary>
        public override bool RequiresConstantRepaint() => Application.isPlaying;
    }
}
