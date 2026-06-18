/***************************************************************
 * (c) copyright 2026 - 2030, Solotopia
 * All Rights Reserved.
 * -------------------------------------------------------------
 * filename:  PrefabManager.Visitors.cs
 * author:    taoye
 * created:   2026/5/14
 * descrip:   PrefabManager 字段定义
 ***************************************************************/

using System.Collections.Generic;
using UnityEngine;

namespace NovaFramework.Runtime
{
    internal sealed partial class PrefabManager : PrefabManagerBase
    {
        /// <summary>
        /// PrefabManager 启动配置（来自 PrefabComponent.Initialize 注入）。
        /// </summary>
        private PrefabManagerConfig m_Config;

        /// <summary>
        /// AssetManager 依赖，用于加载 GameObject 资源句柄。
        /// </summary>
        private IAssetManager m_AssetManager;

        /// <summary>
        /// 实例化实例到资源句柄的反查表，<GameObject 实例, 资源句柄>，OnDestroy 钩子触发时用于查找并释放句柄。
        /// </summary>
        private Dictionary<GameObject, IAssetHandle<GameObject>> m_InstanceToHandle;
        public override int RecordedInstanceCount => m_InstanceToHandle.Count;

        /// <summary>
        /// 实例诊断记录列表，与 m_InstanceToHandle 同步增删，供 Inspector 诊断面板只读展示。
        /// </summary>
        private List<PrefabRecordedInstance> m_RecordedInstances;
        public override IReadOnlyList<PrefabRecordedInstance> RecordedInstances => m_RecordedInstances;
    }
}
