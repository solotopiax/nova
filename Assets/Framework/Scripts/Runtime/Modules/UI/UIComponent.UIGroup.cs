/***************************************************************
 * (c) copyright 2026 - 2030, Solotopia
 * All Rights Reserved.
 * -------------------------------------------------------------
 * filename:  UIComponent.UIGroup.cs
 * author:    taoye
 * created:   2026/02/27
 * descrip:   UI 组件 - 视图分组配置
 ***************************************************************/

using System;
using UnityEngine;

namespace NovaFramework.Runtime
{
    /// <summary>
    /// UI 组件。
    /// </summary>
    public sealed partial class UIComponent : FrameworkComponent
    {
        /// <summary>
        /// Inspector 中配置的视图分组信息。
        /// </summary>
        [Serializable]
        private sealed class UIGroup
        {
            /// <summary>
            /// 视图分组名称。
            /// </summary>
            [SerializeField]
            private string m_Name = null;
            public string Name => m_Name;

            /// <summary>
            /// 视图分组深度。
            /// </summary>
            [SerializeField]
            private int m_Depth = 0;
            public int Depth => m_Depth;

        }
    }
}
