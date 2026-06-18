/***************************************************************
 * (c) copyright 2026 - 2030, Solotopia
 * All Rights Reserved.
 * -------------------------------------------------------------
 * filename:  PrefabComponent.cs
 * author:    taoye
 * created:   2026/5/15
 * descrip:   Prefab 实例化组件
 ***************************************************************/

using System;
using System.Threading;
using Cysharp.Threading.Tasks;
using UnityEngine;

namespace NovaFramework.Runtime
{
    /// <summary>
    /// Prefab 实例化组件，对外提供同步/异步实例化与销毁接口。
    /// </summary>
    [DisallowMultipleComponent]
    public sealed partial class PrefabComponent : FrameworkComponent
    {
        /// <summary>
        /// 唤醒：通过 TypeCreator 创建 IPrefabManager。
        /// </summary>
        protected override void Awake()
        {
            base.Awake();
            m_PrefabManager = Util.TypeCreator.Create<IPrefabManager>(m_CurPrefabManagerTypeName);
            if (m_PrefabManager == null)
            {
                throw new InvalidOperationException("PrefabManager 无效。");
            }
        }

        /// <summary>
        /// 开始：调用 PrefabManager.Initialize，内部会通过 FrameworkManagersGroup 取 IAssetManager。
        /// </summary>
        private void Start()
        {
            m_PrefabManager.Initialize(m_PrefabManagerConfig);
        }

        /// <summary>
        /// 销毁：清空 Manager 引用。
        /// </summary>
        private void OnDestroy()
        {
            m_PrefabManager = null;
        }

        /// <summary>
        /// 同步实例化 Prefab。
        /// </summary>
        /// <param name="location">Prefab location。</param>
        /// <param name="parent">父节点，null 表示放置在根。</param>
        /// <returns>实例化得到的 GameObject。</returns>
        public GameObject InstantiateSync(string location, Transform parent = null)
        {
            return m_PrefabManager.InstantiateSync(location, parent);
        }

        /// <summary>
        /// 同步实例化 Prefab 并取出指定 Component。
        /// </summary>
        /// <typeparam name="T">目标组件类型。</typeparam>
        /// <param name="location">Prefab location。</param>
        /// <param name="parent">父节点，null 表示放置在根。</param>
        /// <returns>目标组件实例。</returns>
        public T InstantiateSync<T>(string location, Transform parent = null) where T : Component
        {
            return m_PrefabManager.InstantiateSync<T>(location, parent);
        }

        /// <summary>
        /// 异步实例化 Prefab。
        /// </summary>
        /// <param name="location">Prefab location。</param>
        /// <param name="parent">父节点，null 表示放置在根。</param>
        /// <param name="ct">取消令牌。</param>
        /// <returns>实例化得到的 GameObject。</returns>
        public UniTask<GameObject> InstantiateAsync(string location, Transform parent = null, CancellationToken ct = default)
        {
            return m_PrefabManager.InstantiateAsync(location, parent, ct);
        }

        /// <summary>
        /// 异步实例化 Prefab 并取出指定 Component。
        /// </summary>
        /// <typeparam name="T">目标组件类型。</typeparam>
        /// <param name="location">Prefab location。</param>
        /// <param name="parent">父节点，null 表示放置在根。</param>
        /// <param name="ct">取消令牌。</param>
        /// <returns>目标组件实例。</returns>
        public UniTask<T> InstantiateAsync<T>(string location, Transform parent = null, CancellationToken ct = default) where T : Component
        {
            return m_PrefabManager.InstantiateAsync<T>(location, parent, ct);
        }

        /// <summary>
        /// 销毁 Prefab 实例，触发引用计数归零。
        /// </summary>
        /// <param name="instance">待销毁的 GameObject。</param>
        public void Destroy(GameObject instance)
        {
            m_PrefabManager.Destroy(instance);
        }
    }
}
