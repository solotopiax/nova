/***************************************************************
 * (c) copyright 2026 - 2030, Solotopia
 * All Rights Reserved.
 * -------------------------------------------------------------
 * filename:  PrefabManager.Load.cs
 * author:    taoye
 * created:   2026/5/14
 * descrip:   PrefabManager 同步/异步实例化实现
 ***************************************************************/

using System.Threading;
using Cysharp.Threading.Tasks;
using UnityEngine;

namespace NovaFramework.Runtime
{
    internal sealed partial class PrefabManager : PrefabManagerBase
    {
        /// <summary>
        /// 同步实例化 Prefab；每次申请独立句柄，实例化后挂载 PrefabInstanceTag 钩子。
        /// </summary>
        /// <param name="location">Prefab location。</param>
        /// <param name="parent">父节点，null 表示放置在根。</param>
        /// <returns>实例化得到的 GameObject。</returns>
        public override GameObject InstantiateSync(string location, Transform parent = null)
        {
            IAssetHandle<GameObject> handle = m_AssetManager.LoadSync<GameObject>(location);
            if (handle.Asset == null)
            {
                Log.Error(LogTag.Prefab, "PrefabManager: 资源加载失败，location='{0}'。", location);
                handle.Release();
                return null;
            }
            GameObject go = Object.Instantiate(handle.Asset, parent);
            RecordInstance(go, handle, location);
            return go;
        }

        /// <summary>
        /// 异步实例化 Prefab；每次申请独立句柄，实例化后挂载 PrefabInstanceTag 钩子。
        /// </summary>
        /// <param name="location">Prefab location。</param>
        /// <param name="parent">父节点，null 表示放置在根。</param>
        /// <param name="ct">取消令牌。</param>
        /// <returns>实例化得到的 GameObject。</returns>
        public override async UniTask<GameObject> InstantiateAsync(string location, Transform parent = null, CancellationToken ct = default)
        {
            IAssetHandle<GameObject> handle = await m_AssetManager.LoadAsync<GameObject>(location, ct);
            if (handle.Asset == null)
            {
                Log.Error(LogTag.Prefab, "PrefabManager: 资源加载失败，location='{0}'。", location);
                handle.Release();
                return null;
            }
            GameObject go = Object.Instantiate(handle.Asset, parent);
            RecordInstance(go, handle, location);
            return go;
        }

        /// <summary>
        /// 同步实例化 Prefab 并取出指定 Component；组件缺失时记录错误日志。
        /// </summary>
        /// <typeparam name="T">目标组件类型。</typeparam>
        /// <param name="location">Prefab location。</param>
        /// <param name="parent">父节点，null 表示放置在根。</param>
        /// <returns>目标组件实例，缺失时为 null。</returns>
        public override T InstantiateSync<T>(string location, Transform parent = null)
        {
            GameObject go = InstantiateSync(location, parent);
            T comp = go != null ? go.GetComponent<T>() : null;
            if (comp == null)
            {
                Log.Error(LogTag.Prefab, "PrefabManager: Prefab '{0}' 缺少组件 {1}。", location, typeof(T).Name);
            }
            return comp;
        }

        /// <summary>
        /// 异步实例化 Prefab 并取出指定 Component；组件缺失时记录错误日志。
        /// </summary>
        /// <typeparam name="T">目标组件类型。</typeparam>
        /// <param name="location">Prefab location。</param>
        /// <param name="parent">父节点，null 表示放置在根。</param>
        /// <param name="ct">取消令牌。</param>
        /// <returns>目标组件实例，缺失时为 null。</returns>
        public override async UniTask<T> InstantiateAsync<T>(string location, Transform parent = null, CancellationToken ct = default)
        {
            GameObject go = await InstantiateAsync(location, parent, ct);
            T comp = go != null ? go.GetComponent<T>() : null;
            if (comp == null)
            {
                Log.Error(LogTag.Prefab, "PrefabManager: Prefab '{0}' 缺少组件 {1}。", location, typeof(T).Name);
            }
            return comp;
        }

        /// <summary>
        /// 销毁实例；仅触发 UnityEngine.Object.Destroy，handle 释放由 PrefabInstanceTag.OnDestroy 钩子接管。
        /// </summary>
        /// <param name="instance">待销毁的 GameObject。</param>
        public override void Destroy(GameObject instance)
        {
            if (instance == null)
            {
                return;
            }
            Object.Destroy(instance);
        }
    }
}
