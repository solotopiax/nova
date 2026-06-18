/***************************************************************
 * (c) copyright 2026 - 2030, Solotopia
 * All Rights Reserved.
 * -------------------------------------------------------------
 * filename:  ObjectPoolComponentInspector.cs
 * author:    taoye
 * created:   2026/1/21
 * descrip:   对象池组件编辑器面板定制
 ***************************************************************/

using System.Collections.Generic;
using NovaFramework.Runtime;
using UnityEditor;

namespace NovaFramework.Editor
{
    /// <summary>
    /// 对象池组件编辑器面板定制。
    /// </summary>
    [CustomEditor(typeof(ObjectPoolComponent))]
    internal sealed partial class ObjectPoolComponentInspector : BaseComponentInspector
    {
        /// <summary>
        /// 启用。
        /// </summary>
        protected override void OnEnable()
        {
            base.OnEnable();
            // m_FeishuDocumentUrl = EditorPath.Readme.Uri.SolarVibrate;

            m_CurManagerTypeName = serializedObject.FindProperty("m_CurManagerTypeName");
            m_ManagerTypeNames = new List<string>(EditorUtil.TypeCache.GetTypeNames(typeof(IObjectPoolManager)));
            m_PageIndices = new Dictionary<string, int>();
        }

        /// <summary>
        /// 绘制。
        /// </summary>
        public override void OnInspectorGUI()
        {
            base.OnInspectorGUI();
            DrawConfigs();
            DrawRuntimeInfos();
            FinalRefreshInspectorGUI();
        }

    }
}
