/***************************************************************
 * (c) copyright 2026 - 2030, Solotopia
 * All Rights Reserved.
 * -------------------------------------------------------------
 * filename:  PrefabManagerBase.cs
 * author:    taoye
 * created:   2026/5/14
 * descrip:   PrefabManager 抽象基类
 ***************************************************************/

using System.Collections.Generic;
using System.Threading;
using Cysharp.Threading.Tasks;
using UnityEngine;

namespace NovaFramework.Runtime
{
    /// <summary>
    /// PrefabManager 抽象基类，声明 IPrefabManager 全部成员的 abstract 形式。
    /// </summary>
    /// <remarks>
    /// Priority = 10，高于 AssetManager（4）确保 PrefabManager 在资源管理器后初始化、先关闭。
    /// Update/Shutdown 由 FrameworkManager 派生强制覆盖。
    /// </remarks>
    internal abstract class PrefabManagerBase : FrameworkManager, IPrefabManager
    {
        /// <summary>
        /// FrameworkManager 调度优先级。
        /// </summary>
        public override int Priority => 10;

        /// <summary>
        /// 用配置初始化 Manager。
        /// </summary>
        /// <param name="config">PrefabManager 配置。</param>
        public abstract void Initialize(PrefabManagerConfig config);

        /// <summary>
        /// 每帧 Tick，由 FrameworkManagersGroup 调度。
        /// </summary>
        public abstract override void Update();

        /// <summary>
        /// 关闭 Manager，释放所有资源句柄并清空内部字典。
        /// </summary>
        public abstract override void Shutdown();

        /// <summary>
        /// 同步实例化 Prefab。
        /// </summary>
        /// <param name="location">Prefab location。</param>
        /// <param name="parent">父节点，null 表示放置在根。</param>
        /// <returns>实例化得到的 GameObject。</returns>
        public abstract GameObject InstantiateSync(string location, Transform parent = null);

        /// <summary>
        /// 同步实例化 Prefab 并取出指定 Component。
        /// </summary>
        /// <typeparam name="T">目标组件类型。</typeparam>
        /// <param name="location">Prefab location。</param>
        /// <param name="parent">父节点，null 表示放置在根。</param>
        /// <returns>目标组件实例。</returns>
        public abstract T InstantiateSync<T>(string location, Transform parent = null) where T : Component;

        /// <summary>
        /// 异步实例化 Prefab。
        /// </summary>
        /// <param name="location">Prefab location。</param>
        /// <param name="parent">父节点，null 表示放置在根。</param>
        /// <param name="ct">取消令牌。</param>
        /// <returns>实例化得到的 GameObject。</returns>
        public abstract UniTask<GameObject> InstantiateAsync(string location, Transform parent = null, CancellationToken ct = default);

        /// <summary>
        /// 异步实例化 Prefab 并取出指定 Component。
        /// </summary>
        /// <typeparam name="T">目标组件类型。</typeparam>
        /// <param name="location">Prefab location。</param>
        /// <param name="parent">父节点，null 表示放置在根。</param>
        /// <param name="ct">取消令牌。</param>
        /// <returns>目标组件实例。</returns>
        public abstract UniTask<T> InstantiateAsync<T>(string location, Transform parent = null, CancellationToken ct = default) where T : Component;

        /// <summary>
        /// 销毁实例（便捷转发，等价 UnityEngine.Object.Destroy）。
        /// </summary>
        /// <param name="instance">待销毁的 GameObject。</param>
        public abstract void Destroy(GameObject instance);

        /// <summary>
        /// 当前已被 PrefabManager 记录的实例数量（测试与诊断用）。
        /// </summary>
        public abstract int RecordedInstanceCount { get; }

        /// <summary>
        /// 当前所有被记录实例的只读快照，供 Inspector 诊断面板展示，禁止修改。
        /// </summary>
        public abstract IReadOnlyList<PrefabRecordedInstance> RecordedInstances { get; }
    }
}
