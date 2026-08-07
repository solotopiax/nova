/***************************************************************
 * (c) copyright 2026 - 2030, Solotopia
 * All Rights Reserved.
 * -------------------------------------------------------------
 * filename:  NativeComponentInspector.Visitors.cs
 * author:    taoye
 * created:   2026/8/7
 * descrip:   Native 组件 Inspector 字段
 ***************************************************************/

using System.Collections.Generic;
using UnityEditor;

namespace NovaFramework.Editor
{
    internal sealed partial class NativeComponentInspector : BaseComponentInspector
    {
        /// <summary>
        /// 当前 NativeManager 类型名称。
        /// </summary>
        private SerializedProperty m_CurNativeManagerTypeName;

        /// <summary>
        /// 所有可选 INativeManager 实现类型名称。
        /// </summary>
        private List<string> m_NativeManagerTypeNames;
    }
}
