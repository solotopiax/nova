/***************************************************************
 * (c) copyright 2026 - 2030, Solotopia
 * All Rights Reserved.
 * -------------------------------------------------------------
 * filename:  IPrefabManager.cs
 * author:    taoye
 * created:   2026/5/14
 * descrip:   Prefab 实例化与销毁接口
 ***************************************************************/

using System.Collections.Generic;
using System.Threading;
using Cysharp.Threading.Tasks;
using UnityEngine;

namespace NovaFramework.Runtime
{
    /// <summary>
    /// Prefab 实例化接口，仅负责实例化与销毁，
    /// AB / 资源生命周期由 IAssetManager 内部接管。
    /// </summary>
    public interface IPrefabManager
    {
        /// <summary>
        /// 用配置初始化 Manager。
        /// </summary>
        /// <param name="config">PrefabManager 配置。</param>
        void Initialize(PrefabManagerConfig config);

        /// <summary>
        /// 同步实例化 Prefab。
        /// </summary>
        /// <param name="location">Prefab location。</param>
        /// <param name="parent">父节点，null 表示放置在根。</param>
        /// <returns>实例化得到的 GameObject。</returns>
        GameObject InstantiateSync(string location, Transform parent = null);

        /// <summary>
        /// 同步实例化 Prefab 并取出指定 Component。
        /// </summary>
        /// <typeparam name="T">目标组件类型。</typeparam>
        /// <param name="location">Prefab location。</param>
        /// <param name="parent">父节点，null 表示放置在根。</param>
        /// <returns>目标组件实例。</returns>
        T InstantiateSync<T>(string location, Transform parent = null) where T : Component;

        /// <summary>
        /// 异步实例化 Prefab。
        /// </summary>
        /// <param name="location">Prefab location。</param>
        /// <param name="parent">父节点，null 表示放置在根。</param>
        /// <param name="ct">取消令牌。</param>
        /// <returns>实例化得到的 GameObject。</returns>
        UniTask<GameObject> InstantiateAsync(string location, Transform parent = null, CancellationToken ct = default);

        /// <summary>
        /// 异步实例化 Prefab 并取出指定 Component。
        /// </summary>
        /// <typeparam name="T">目标组件类型。</typeparam>
        /// <param name="location">Prefab location。</param>
        /// <param name="parent">父节点，null 表示放置在根。</param>
        /// <param name="ct">取消令牌。</param>
        /// <returns>目标组件实例。</returns>
        UniTask<T> InstantiateAsync<T>(string location, Transform parent = null, CancellationToken ct = default) where T : Component;

        /// <summary>
        /// 销毁实例（便捷转发，等价 UnityEngine.Object.Destroy）。
        /// </summary>
        /// <param name="instance">待销毁的 GameObject。</param>
        void Destroy(GameObject instance);

        /// <summary>
        /// 当前已被 PrefabManager 记录的实例数量（测试与诊断用）。
        /// </summary>
        int RecordedInstanceCount { get; }

        /// <summary>
        /// 当前所有被记录实例的只读快照，供 Inspector 诊断面板展示，禁止修改。
        /// </summary>
        IReadOnlyList<PrefabRecordedInstance> RecordedInstances { get; }
    }
}
