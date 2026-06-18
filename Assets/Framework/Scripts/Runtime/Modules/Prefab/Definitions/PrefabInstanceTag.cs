/***************************************************************
 * (c) copyright 2026 - 2030, Solotopia
 * All Rights Reserved.
 * -------------------------------------------------------------
 * filename:  PrefabInstanceTag.cs
 * author:    taoye
 * created:   2026/5/15
 * descrip:   PrefabManager 实例化时自动挂载的钩子组件，OnDestroy 单路径回收 handle
 ***************************************************************/

using System;
using UnityEngine;

namespace NovaFramework.Runtime
{
    /// <summary>
    /// PrefabManager 实例化时挂在每个 GameObject 上的钩子组件。
    /// 唯一职责：OnDestroy 时通知 PrefabManager 释放对应 IAssetHandle。
    /// </summary>
    /// <remarks>
    /// 单路径回收：无论 PrefabComponent.Destroy / 业务原生 Object.Destroy / 父节点销毁链 / 场景切换，
    /// 最终都汇聚到本组件的 OnDestroy，确保 handle 不泄漏。
    /// </remarks>
    [DisallowMultipleComponent]
    public sealed class PrefabInstanceTag : MonoBehaviour
    {
        /// <summary>
        /// 本实例对应的强类型资源句柄，由 PrefabManager.RecordInstance 注入。
        /// </summary>
        internal IAssetHandle<GameObject> Handle;
        /// <summary>
        /// OnDestroy 触发时的回调，由 PrefabManager.RecordInstance 注入。
        /// 参数为本组件实例，PrefabManager 用于反查字典并释放 Handle。
        /// </summary>
        internal Action<PrefabInstanceTag> OnDestroyed;

        /// <summary>
        /// Unity Message：GameObject 被销毁时触发，通知 PrefabManager 释放 Handle。
        /// </summary>
        private void OnDestroy()
        {
            Action<PrefabInstanceTag> cb = OnDestroyed;
            OnDestroyed = null;
            Handle = null;
            cb?.Invoke(this);
        }
    }
}
