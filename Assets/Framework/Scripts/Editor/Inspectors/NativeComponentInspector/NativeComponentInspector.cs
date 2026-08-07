/***************************************************************
 * (c) copyright 2026 - 2030, Solotopia
 * All Rights Reserved.
 * -------------------------------------------------------------
 * filename:  NativeComponentInspector.cs
 * author:    taoye
 * created:   2026/8/7
 * descrip:   Native 组件编辑器面板定制
 ***************************************************************/

using System.Collections.Generic;
using NovaFramework.Runtime;
using UnityEditor;

namespace NovaFramework.Editor
{
    /// <summary>
    /// Native 组件编辑器面板，负责 Manager 类型选择与模块边界说明。
    /// </summary>
    [CustomEditor(typeof(NativeComponent))]
    internal sealed partial class NativeComponentInspector : BaseComponentInspector
    {
        /// <summary>
        /// 绑定 Manager 类型字段并收集 INativeManager 实现。
        /// </summary>
        protected override void OnEnable()
        {
            base.OnEnable();
            m_CurNativeManagerTypeName = serializedObject.FindProperty("m_CurNativeManagerTypeName");
            m_NativeManagerTypeNames = new List<string>(EditorUtil.TypeCache.GetTypeNames(typeof(INativeManager)));
        }

        /// <summary>
        /// 绘制 Native 模块配置。
        /// </summary>
        public override void OnInspectorGUI()
        {
            base.OnInspectorGUI();
            DrawConfigs();
            FinalRefreshInspectorGUI();
        }
    }
}
