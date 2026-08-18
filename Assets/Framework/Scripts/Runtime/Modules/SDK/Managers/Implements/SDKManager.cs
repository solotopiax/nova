/***************************************************************
 * (c) copyright 2026 - 2030, Solotopia
 * All Rights Reserved.
 * -------------------------------------------------------------
 * filename:  SDKManager.cs
 * author:    taoye
 * created:   2026/3/16
 * descrip:   SDK 管理器唯一实现
 ***************************************************************/

using System;
using System.Collections.Generic;
using System.Threading;
using Cysharp.Threading.Tasks;

namespace NovaFramework.Runtime
{
    /// <summary>
    /// SDK 管理器唯一实现，负责插件生命周期编排、物料注入与类型查询。
    /// 通过 Util.TypeCreator.Create 反射创建，禁止直接 new。
    /// 主线程调用约定：所有公开 API 必须在主线程调用；InitializeAsync/DisposeAsync 内部可切线程，
    /// complete 前须 await UniTask.SwitchToMainThread 切回主线程。
    /// </summary>
    internal sealed partial class SDKManager : SDKManagerBase
    {
        /// <summary>
        /// 初始化 SDKManager 实例。
        /// 无参构造器，FrameworkManager 基类负责向 FrameworkManagersGroup 注册。
        /// </summary>
        public SDKManager() { }

        /// <summary>
        /// 同步初始化：缓存 Manager 依赖；不读取 PluginEntries 作为运行时启用或排序来源。
        /// 不在 Start 期实例化插件；插件启用与实例化统一延后到 InitializeAsync 中按 ConfigMaster.EnabledSDKs 执行。
        /// </summary>
        /// <param name="config">由 SDKComponent.Start() 构造并传入的配置 DTO，包含 PluginEntries 列表。</param>
        public override void Initialize(SDKManagerConfig config)
        {

            m_EventManager = FrameworkManagersGroup.GetManager<IEventManager>();
            m_ConfigManager = FrameworkManagersGroup.GetManager<IConfigManager>();
            Log.Debug(LogTag.SDK, "SDKManager.Initialize 完成，已缓存 Manager 依赖。");
        }
        /// <summary>
        /// 异步批量初始化 ConfigMaster.EnabledSDKs 启用的插件。
        /// 按插件自身 Priority 升序分桶，同桶内 UniTask.WhenAll 并行；桶间串行执行降低启动期 CPU 峰值。
        /// 单插件失败隔离：catch 后记录 Log.Error，不向上传播。
        /// 所有桶完成后 m_IsInitialized = true 并解锁 WaitForInitializedAsync。
        /// </summary>
        /// <param name="ct">取消令牌；传入 CancellationToken.None 时不可取消。</param>
        /// <returns>所有批次初始化完成的异步任务。</returns>
        public override async UniTask InitializeAsync(CancellationToken ct = default)
        {
            if (m_IsInitialized)
            {
                Log.Warning(LogTag.SDK, "SDKManager.InitializeAsync：已初始化，重复调用已忽略。");
                return;
            }

            // Config 已在 Procedure 加载流程就绪（Initialize 处于 Unity Start 期，早于 Config 加载）。
            // 此处以 ConfigMaster.EnabledSDKs 为唯一启用源实例化插件，排序使用 ISDKPlugin.Priority。
            InstantiateEnabledPluginsFromConfig();
            SortPluginsByPriority();

            try
            {
                List<List<ISDKPlugin>> buckets = GroupByPriority();
                for (int i = 0; i < buckets.Count; i++)
                {
                    await InitializeBucketAsync(buckets[i], ct);
                }

                CacheAssetCheckDeviceId();
                m_IsInitialized = true;
                m_InitializedTcs.TrySetResult();
                Log.Debug(LogTag.SDK, "SDKManager.InitializeAsync 完成，所有插件初始化流程已执行。");
            }
            catch (OperationCanceledException)
            {
                // 取消时唤醒所有等待者，避免 TCS 永久挂起。
                m_InitializedTcs.TrySetCanceled();
                throw;
            }
        }

        /// <summary>
        /// SDK 插件全部初始化后，将可用设备提供者返回的稳定 DeviceID 交给 Asset 模块原子缓存。
        /// 任一环节不可用或异常均不影响 SDK 初始化完成语义。
        /// </summary>
        private void CacheAssetCheckDeviceId()
        {
            try
            {
                if (!TryGet<IDeviceIdProvider>(out IDeviceIdProvider provider))
                {
                    return;
                }

                string deviceId = provider.GetDeviceID();
                if (string.IsNullOrWhiteSpace(deviceId))
                {
                    return;
                }

                IAssetManager assetManager = FrameworkManagersGroup.GetManager<IAssetManager>();
                assetManager?.SaveAssetCheckDeviceId(deviceId);
            }
            catch (Exception exception)
            {
                Log.Warning(LogTag.SDK, "缓存启动白名单 DeviceID 失败，不影响 SDK 初始化。Error={0}", exception.Message);
            }
        }

        /// <summary>
        /// 异步释放所有已初始化的插件。
        /// 按 Priority 降序逐个 try/catch 调用 plugin.DisposeAsync；
        /// 单插件抛异常记 Log.Error 后继续释放其他插件（失败隔离）。
        /// 完成后清空内部集合，m_IsInitialized 置为 false，重置初始化信号源。
        /// </summary>
        /// <param name="ct">取消令牌。</param>
        /// <returns>所有插件释放完成的异步任务。</returns>
        public override async UniTask DisposeAsync(CancellationToken ct = default)
        {
            for (int i = m_SortedPlugins.Count - 1; i >= 0; i--)
            {
                ISDKPlugin plugin = m_SortedPlugins[i];
                if (plugin.IsAvailable)
                {
                    try
                    {
                        await plugin.DisposeAsync(ct);
                        Log.Debug(LogTag.SDK, Txt.Format("SDK 插件 '{0}' 已释放。", plugin.Name));
                    }
                    catch (Exception e)
                    {
                        Log.Error(LogTag.SDK, Txt.Format("SDK 插件 '{0}' 释放异常（已隔离）：{1}", plugin.Name, e));
                    }
                }
            }

            m_Plugins.Clear();
            m_SortedPlugins.Clear();
            m_IsInitialized = false;
            // 唤醒所有挂在旧 TCS 上的等待者，避免因 DisposeAsync 替换 TCS 后旧等待者永久挂起。
            m_InitializedTcs.TrySetCanceled();
            m_InitializedTcs = new UniTaskCompletionSource();
            Log.Debug(LogTag.SDK, "SDKManager.DisposeAsync 完成，所有插件已释放。");
        }

        /// <summary>
        /// 等待管理器完成 InitializeAsync。
        /// 若已完成则立即返回；若未完成则挂起直到完成或取消。
        /// </summary>
        /// <param name="ct">取消令牌。</param>
        /// <returns>等待完成的异步任务。</returns>
        public override UniTask WaitForInitializedAsync(CancellationToken ct = default)
        {
            if (m_IsInitialized)
            {
                return UniTask.CompletedTask;
            }

            return m_InitializedTcs.Task.AttachExternalCancellation(ct);
        }

        /// <summary>
        /// 按插件具体类型获取已初始化且可用的插件实例。
        /// 未找到或 IsAvailable==false 时抛 SDKUnavailableException。
        /// </summary>
        /// <typeparam name="TPlugin">插件具体类型，必须为 class 且实现 ISDKPlugin。</typeparam>
        /// <returns>对应的可用插件实例。</returns>
        public override TPlugin Get<TPlugin>()
        {
            foreach (ISDKPlugin plugin in m_Plugins.Values)
            {
                if (plugin is TPlugin typed && plugin.IsAvailable)
                {
                    return typed;
                }
            }

            throw new SDKUnavailableException(typeof(TPlugin));
        }

        /// <summary>
        /// 尝试按插件具体类型获取已初始化且可用的插件实例。
        /// </summary>
        /// <typeparam name="TPlugin">插件具体类型，必须为 class 且实现 ISDKPlugin。</typeparam>
        /// <param name="plugin">输出的插件实例；不可用时为 null。</param>
        /// <returns>插件可用返回 true，否则 false。</returns>
        public override bool TryGet<TPlugin>(out TPlugin plugin)
        {
            foreach (ISDKPlugin candidate in m_Plugins.Values)
            {
                if (candidate is TPlugin typed && candidate.IsAvailable)
                {
                    plugin = typed;
                    return true;
                }
            }

            plugin = null;
            return false;
        }

        /// <summary>
        /// 获取所有实现指定接口且当前可用（IsAvailable == true）的插件列表。
        /// 按 m_SortedPlugins 的插件自身 Priority 升序顺序遍历，同一插件实现多个接口时在各接口查询中均出现（同一对象引用）。
        /// </summary>
        /// <typeparam name="TInterface">目标插件接口类型，必须为 class 且实现 ISDKPlugin。</typeparam>
        /// <returns>可用插件实例的只读列表，按插件自身 Priority 升序；若无可用实例返回空列表。</returns>
        public override IReadOnlyList<TInterface> GetAll<TInterface>()
        {
            List<TInterface> result = new List<TInterface>();
            for (int i = 0; i < m_SortedPlugins.Count; i++)
            {
                if (m_SortedPlugins[i] is TInterface typed && m_SortedPlugins[i].IsAvailable)
                {
                    result.Add(typed);
                }
            }

            return result;
        }

        /// <summary>
        /// 转发 OnApplicationPause 事件到所有实现 ISDKPauseListener 的插件。
        /// 由 SDKComponent 在 OnApplicationPause 回调中调用；主线程调用，无需线程保护。
        /// </summary>
        /// <param name="isPaused">true 表示进入后台，false 表示回到前台。</param>
        public override void BroadcastPause(bool isPaused)
        {
            for (int i = 0; i < m_SortedPlugins.Count; i++)
            {
                if (m_SortedPlugins[i] is ISDKPauseListener listener)
                {
                    try
                    {
                        listener.OnPause(isPaused);
                    }
                    catch (Exception e)
                    {
                        Log.Error(LogTag.SDK, Txt.Format("SDK 插件 '{0}' OnPause 异常（已隔离）：{1}", m_SortedPlugins[i].Name, e));
                    }
                }
            }
        }

        /// <summary>
        /// 转发 OnApplicationFocus 事件到所有实现 ISDKFocusListener 的插件。
        /// 由 SDKComponent 在 OnApplicationFocus 回调中调用；主线程调用，无需线程保护。
        /// </summary>
        /// <param name="hasFocus">true 表示获得焦点，false 表示失去焦点。</param>
        public override void BroadcastFocus(bool hasFocus)
        {
            for (int i = 0; i < m_SortedPlugins.Count; i++)
            {
                if (m_SortedPlugins[i] is ISDKFocusListener listener)
                {
                    try
                    {
                        listener.OnFocus(hasFocus);
                    }
                    catch (Exception e)
                    {
                        Log.Error(LogTag.SDK, Txt.Format("SDK 插件 '{0}' OnFocus 异常（已隔离）：{1}", m_SortedPlugins[i].Name, e));
                    }
                }
            }
        }

        /// <summary>
        /// 转发 OnApplicationQuit 事件到所有实现 ISDKQuitListener 的插件。
        /// 由 SDKComponent 在 OnApplicationQuit 回调中调用；主线程调用，无需线程保护。
        /// </summary>
        public override void BroadcastQuit()
        {
            for (int i = 0; i < m_SortedPlugins.Count; i++)
            {
                if (m_SortedPlugins[i] is ISDKQuitListener listener)
                {
                    try
                    {
                        listener.OnQuit();
                    }
                    catch (Exception e)
                    {
                        Log.Error(LogTag.SDK, Txt.Format("SDK 插件 '{0}' OnQuit 异常（已隔离）：{1}", m_SortedPlugins[i].Name, e));
                    }
                }
            }
        }

        /// <summary>
        /// 通知用户登录，userId 为空时忽略；有效时向 EventManager 发送 SDKEventData.UserLogin 事件。
        /// </summary>
        /// <param name="userId">已登录用户的唯一标识。</param>
        public override void Login(string userId)
        {
            if (string.IsNullOrWhiteSpace(userId))
            {
                Log.Warning(LogTag.SDK, "SDKManager.Login：userId 为空，已跳过登录事件广播。");
                return;
            }

            m_EventManager?.Fire(this, SDKEventData.UserLogin.Create(userId));
        }

        /// <summary>
        /// 框架管理器轮询（SDK Manager 无帧轮询需求，空实现）。
        /// </summary>
        public override void Update() { }

        /// <summary>
        /// 关闭并清理管理器，内部同步触发 DisposeAsync（Fire-and-Forget）。
        /// FrameworkManagersGroup 在框架关闭时调用，此时 UniTask 运行时仍可用。
        /// </summary>
        public override void Shutdown()
        {
            DisposeAsync(CancellationToken.None).Forget();
        }
    }
}
