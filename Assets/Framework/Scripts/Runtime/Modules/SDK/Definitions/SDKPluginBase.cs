/***************************************************************
 * (c) copyright 2026 - 2030, Solotopia
 * All Rights Reserved.
 * -------------------------------------------------------------
 * filename:  SDKPluginBase.cs
 * author:    taoye
 * created:   2026/3/31
 * descrip:   SDK 插件纯 C# 抽象基类
 ***************************************************************/

using System;
using System.Collections.Generic;
using System.Threading;
using Cysharp.Threading.Tasks;

namespace NovaFramework.Runtime
{
    /// <summary>
    /// SDK 插件通用抽象基类（纯 C#，非 MonoBehaviour）。
    /// 派生类只需重写 OnInitializeAsync / OnDisposeAsync，以及 Name / Priority 属性。
    /// IsAvailable 由基类管理：OnInitializeAsync 成功后置 true，抛出异常后保持 false。
    /// </summary>
    public abstract class SDKPluginBase : ISDKPlugin
    {
        /// <summary>
        /// m_IsAvailable 字段，记录当前插件是否可用。
        /// </summary>
        private bool m_IsAvailable;
        /// <summary>
        /// 当前插件是否可用（初始化成功后为 true，失败后永久为 false）。
        /// </summary>
        public bool IsAvailable => m_IsAvailable;

        /// <summary>
        /// 已就绪的数据槽位，键为业务约定的数据 key，值为对应的 object 数据。
        /// 读写均须持有 m_DataLock。
        /// </summary>
        private readonly Dictionary<string, object> m_DataStore = new();
        /// <summary>
        /// 挂起的等待者，键为数据 key，值为正在等待该 key 的 UniTaskCompletionSource。
        /// 同一 key 同一时刻只有一个 TCS；多个并发调用方共享同一 TCS。
        /// 读写均须持有 m_DataLock。
        /// </summary>
        private readonly Dictionary<string, UniTaskCompletionSource<object>> m_PendingAwaiters = new();
        /// <summary>
        /// 用于保护 m_DataStore 与 m_PendingAwaiters 的同步锁对象。
        /// </summary>
        private readonly object m_DataLock = new();

        /// <summary>
        /// 插件友好名。派生类必须实现，用于诊断日志与 Inspector 显示。
        /// </summary>
        public abstract string Name { get; }

        /// <summary>
        /// 初始化优先级（值越小越先提交；默认 100）。
        /// </summary>
        public virtual int Priority => 100;

        /// <summary>
        /// 声明本插件所需的配置类型。
        /// 若派生类需要配置，返回具体 ISDKPluginConfig 子类型；
        /// 返回 null 表示无需配置（InitializeAsync 时 config 参数可为 null）。
        /// SDKManager 按此类型从 IConfigManager.GetSDKPluginConfig 自动拉取配置注入；取不到则跳过该插件初始化。
        /// </summary>
        protected virtual Type ConfigType => null;

        /// <summary>
        /// 暴露给 Manager 的配置类型，与 ConfigType 等价，供 Manager 层读取。
        /// 主线程只读。
        /// </summary>
        public Type RequiredConfigType => ConfigType;

        /// <summary>
        /// 异步初始化入口（模板方法）。
        /// 基类负责 IsAvailable 管理，派生类实现 OnInitializeAsync。
        /// 主线程调用；内部实现可切线程，完成前须 await UniTask.SwitchToMainThread。
        /// </summary>
        /// <param name="config">由 SDKManager 按 RequiredConfigType 从 IConfigManager 拉取并注入的配置；无需 config 时为 null。</param>
        /// <param name="ct">取消令牌（由 Manager 串联）。</param>
        /// <returns>初始化完成的异步任务。</returns>
        public async UniTask InitializeAsync(ISDKPluginConfig config, CancellationToken ct)
        {
            await OnInitializeAsync(config, ct);
            m_IsAvailable = true;
        }

        /// <summary>
        /// 异步释放入口（模板方法）。
        /// 无论 OnDisposeAsync 是否抛出异常，IsAvailable 均置 false，防止释放失败后 Plugin 被复用。
        /// 主线程调用；内部实现可切线程，完成前须 await UniTask.SwitchToMainThread。
        /// </summary>
        /// <param name="ct">取消令牌。</param>
        /// <returns>释放完成的异步任务。</returns>
        public async UniTask DisposeAsync(CancellationToken ct)
        {
            try
            {
                await OnDisposeAsync(ct);
            }
            finally
            {
                m_IsAvailable = false;
                CancelAllPendingAwaiters();
            }
        }

        /// <summary>
        /// 派生类实现点：执行实际初始化逻辑。
        /// 抛出异常时基类不会将 IsAvailable 置 true；Manager 统一捕获并记录诊断。
        /// 可后台线程执行，完成前须 await UniTask.SwitchToMainThread(ct) 切回主线程。
        /// </summary>
        /// <param name="config">注入的配置，可能为 null（Plugin 无需 config 时）。</param>
        /// <param name="ct">取消令牌。</param>
        /// <returns>初始化完成的异步任务。</returns>
        protected abstract UniTask OnInitializeAsync(ISDKPluginConfig config, CancellationToken ct);

        /// <summary>
        /// 派生类实现点：执行实际资源释放逻辑。
        /// 无需释放时返回 UniTask.CompletedTask 即可。
        /// 可后台线程执行，完成前须 await UniTask.SwitchToMainThread(ct) 切回主线程。
        /// </summary>
        /// <param name="ct">取消令牌。</param>
        /// <returns>释放完成的异步任务。</returns>
        protected abstract UniTask OnDisposeAsync(CancellationToken ct);

        /// <summary>
        /// 按 key 异步获取数据槽位中的值。
        /// 值已就绪时立即返回；值未就绪时挂起，直到 PublishData 写入对应 key 或 ct 被取消。
        /// 取消语义：多个调用方可能共享同一个等待槽位；单个调用方取消只让该调用方抛
        /// OperationCanceledException，不影响其他等待者。若所有等待者都取消且此 key 从此
        /// 不再 Publish，槽位会保留至 DisposeAsync 统一清理。
        /// </summary>
        /// <param name="key">数据键，不得为 null 或空字符串。</param>
        /// <param name="ct">取消令牌，调用方可随时取消等待。</param>
        /// <returns>槽位中的数据值（object 类型，调用方按约定强转）。</returns>
        public UniTask<object> FetchDataAsync(string key, CancellationToken ct = default)
        {
            if (key == null)
                throw new ArgumentNullException(nameof(key));
            if (key.Length == 0)
                throw new ArgumentException("数据键不得为空字符串。", nameof(key));

            ct.ThrowIfCancellationRequested();

            UniTaskCompletionSource<object> tcs;
            lock (m_DataLock)
            {
                if (m_DataStore.TryGetValue(key, out object value))
                    return UniTask.FromResult(value);

                if (!m_PendingAwaiters.TryGetValue(key, out tcs))
                {
                    tcs = new UniTaskCompletionSource<object>();
                    m_PendingAwaiters[key] = tcs;
                }
            }

            return tcs.Task.AttachExternalCancellation(ct);
        }

        /// <summary>
        /// 向指定 key 的数据槽位写入值，并唤醒所有正在等待该 key 的调用方。
        /// 若槽位已有值则覆盖；已完成 await 的调用方不会自动感知新值，需再次调用 FetchDataAsync。
        /// 派生类在数据就绪时调用此方法（如 OnInitializeAsync 完成后、回调返回后等）。
        /// </summary>
        /// <param name="key">数据键，不得为 null 或空字符串。</param>
        /// <param name="value">要写入的数据值。</param>
        protected void PublishData(string key, object value)
        {
            if (key == null)
                throw new ArgumentNullException(nameof(key));
            if (key.Length == 0)
                throw new ArgumentException("数据键不得为空字符串。", nameof(key));

            UniTaskCompletionSource<object> tcs;
            lock (m_DataLock)
            {
                m_DataStore[key] = value;
                m_PendingAwaiters.TryGetValue(key, out tcs);
                if (tcs != null)
                    m_PendingAwaiters.Remove(key);
            }

            tcs?.TrySetResult(value);
        }

        /// <summary>
        /// 取消所有仍在挂起的等待者，并清空两个数据字典。
        /// 由 DisposeAsync 的 finally 块调用，防止 awaiter 在 Plugin 销毁后永久悬挂造成泄露。
        /// TrySetCanceled 在锁外触发，避免 continuation 同步回调时重入锁产生死锁。
        /// </summary>
        private void CancelAllPendingAwaiters()
        {
            List<UniTaskCompletionSource<object>> pendingList;
            lock (m_DataLock)
            {
                pendingList = new List<UniTaskCompletionSource<object>>(m_PendingAwaiters.Values);
                m_PendingAwaiters.Clear();
                m_DataStore.Clear();
            }

            for (int i = 0; i < pendingList.Count; i++)
                pendingList[i].TrySetCanceled();
        }
    }
}
