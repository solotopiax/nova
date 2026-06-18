/***************************************************************
 * (c) copyright 2026 - 2030, Solotopia
 * All Rights Reserved.
 * -------------------------------------------------------------
 * filename:  ObjectPoolComponent.Visitors.cs
 * author:    taoye
 * created:   2026/1/21
 * descrip:   对象池组件-访问器
 ***************************************************************/

using System;
using System.Collections.Generic;
using UnityEngine;

namespace NovaFramework.Runtime
{
    /// <summary>
    /// 对象池组件。
    /// </summary>
    public sealed partial class ObjectPoolComponent : FrameworkComponent
    {
        /// <summary>
        /// 当前对象池管理器类型名称。
        /// </summary>
        [Tooltip("对象池管理器实现类全名")]
        [SerializeField]
        private string m_CurManagerTypeName = "NovaFramework.Runtime.ObjectPoolManager";
        public string CurManagerTypeName => m_CurManagerTypeName;
        
        /// <summary>
        /// 对象池管理器。
        /// </summary>
        private IObjectPoolManager m_ObjectPoolManager = null;

        /// <summary>
        /// 获取对象池数量。
        /// </summary>
        public int Count => m_ObjectPoolManager.Count;
    }
}
