/***************************************************************
 * (c) copyright 2026 - 2030, Solotopia
 * All Rights Reserved.
 * -------------------------------------------------------------
 * filename:  PrefabManager.cs
 * author:    taoye
 * created:   2026/5/14
 * descrip:   PrefabManager 主入口
 ***************************************************************/

using System;
using System.Collections.Generic;
using UnityEngine;

namespace NovaFramework.Runtime
{
    /// <summary>
    /// PrefabManager 实现：基于 IAssetManager 的 Prefab 实例化与生命周期管理。
    /// </summary>
    /// <remarks>
    /// 每次 InstantiateSync/Async 都申请独立的 IAssetHandle，并在实例化后向 GameObject 挂载
    /// PrefabInstanceTag 钩子组件。无论通过 PrefabComponent.Destroy、业务原生 Object.Destroy
    /// 还是父节点/场景销毁链触发，最终都汇聚到 PrefabInstanceTag.OnDestroy → OnInstanceDestroyed
    /// 这一条单路径完成 handle 释放，彻底杜绝引用计数残留与 fake-null 字典 key。
    /// Shutdown 作为退出兜底，统一释放仍跟踪中的全部句柄。
    /// </remarks>
    internal sealed partial class PrefabManager : PrefabManagerBase
    {
        /// <summary>
        /// 反射创建用无参构造器，初始化内部字典与诊断列表。
        /// </summary>
        public PrefabManager()
        {
            m_InstanceToHandle = new Dictionary<GameObject, IAssetHandle<GameObject>>();
            m_RecordedInstances = new List<PrefabRecordedInstance>();
        }

        /// <summary>
        /// 用配置初始化 Manager，获取 IAssetManager 跨模块依赖。
        /// </summary>
        /// <param name="config">PrefabManager 配置。</param>
        public override void Initialize(PrefabManagerConfig config)
        {
            if (config == null)
            {
                throw new ArgumentNullException(nameof(config));
            }
            m_Config = config;
            m_AssetManager = FrameworkManagersGroup.GetManager<IAssetManager>();
            if (m_AssetManager == null)
            {
                throw new InvalidOperationException("PrefabManager.Initialize: IAssetManager not found in FrameworkManagersGroup.");
            }
        }

        /// <summary>
        /// 每帧 Tick，由 FrameworkManagersGroup 调度。PrefabManager 无周期任务。
        /// </summary>
        public override void Update()
        {
            // PrefabManager 无周期任务。
        }

        /// <summary>
        /// 关闭 Manager：释放所有仍跟踪中的 IAssetHandle，清空字典与诊断列表。
        /// </summary>
        public override void Shutdown()
        {
            foreach (var pair in m_InstanceToHandle)
            {
                pair.Value?.Release();
            }
            m_InstanceToHandle.Clear();
            m_RecordedInstances.Clear();
            m_AssetManager = null;
            m_Config = null;
        }
    }
}
