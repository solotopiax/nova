/***************************************************************
 * (c) copyright 2026 - 2030, Solotopia
 * All Rights Reserved.
 * -------------------------------------------------------------
 * filename:  PrefabComponent.Visitors.cs
 * author:    taoye
 * created:   2026/5/15
 * descrip:   PrefabComponent 字段定义
 ***************************************************************/

using System;
using System.Collections.Generic;
using UnityEngine;

namespace NovaFramework.Runtime
{
    /// <summary>
    /// Prefab 实例化组件。
    /// </summary>
    public sealed partial class PrefabComponent : FrameworkComponent
    {
        /// <summary>
        /// 当前 PrefabManager 类型名称。
        /// </summary>
        [Tooltip("PrefabManager 的实现类全名")]
        [SerializeField]
        private string m_CurPrefabManagerTypeName = "NovaFramework.Runtime.PrefabManager";
        public string CurPrefabManagerTypeName => m_CurPrefabManagerTypeName;

        /// <summary>
        /// PrefabManager 启动配置，由 Inspector 配置后传入 Initialize。
        /// </summary>
        [Tooltip("PrefabManager 初始化配置")]
        [SerializeField]
        private PrefabManagerConfig m_PrefabManagerConfig;
        public PrefabManagerConfig PrefabManagerConfig => m_PrefabManagerConfig;

        /// <summary>
        /// PrefabManager 实例，由 Awake 通过 Util.TypeCreator 反射创建。
        /// Manager 未就绪（null）时，RecordedInstanceCount 返回 0，RecordedInstances 返回空集合。
        /// </summary>
        private IPrefabManager m_PrefabManager;
        public int RecordedInstanceCount => m_PrefabManager != null ? m_PrefabManager.RecordedInstanceCount : 0;
        public IReadOnlyList<PrefabRecordedInstance> RecordedInstances => m_PrefabManager?.RecordedInstances ?? Array.Empty<PrefabRecordedInstance>();
    }
}
