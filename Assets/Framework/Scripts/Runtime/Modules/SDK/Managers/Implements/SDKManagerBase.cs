/***************************************************************
 * (c) copyright 2026 - 2030, Solotopia
 * All Rights Reserved.
 * -------------------------------------------------------------
 * filename:  SDKManagerBase.cs
 * author:    taoye
 * created:   2026/3/16
 * descrip:   SDK 管理器抽象基类
 ***************************************************************/

using System.Collections.Generic;
using System.Threading;
using Cysharp.Threading.Tasks;

namespace NovaFramework.Runtime
{
    /// <summary>
    /// SDK 管理器抽象基类，继承 FrameworkManager 并声明 ISDKManager 全部契约为 abstract。
    /// Priority = 16，SDK 模块在大部分核心模块（Event=7、ObjectPool=6 等）之后轮询，最后 Shutdown。
    /// 唯一实现类为 SDKManager（internal sealed partial），通过 Util.TypeCreator.Create 注入。
    /// </summary>
    internal abstract class SDKManagerBase : FrameworkManager, ISDKManager
    {
        /// <summary>
        /// 管理器优先级，值越小越先 Update、越后 Shutdown。SDK 模块优先级为 16。
        /// </summary>
        public override int Priority => 16;

        /// <summary>
        /// 同步初始化：按 PluginEntries 反射实例化启用的插件，建立 Type→Plugin 索引。
        /// 不执行 Plugin.InitializeAsync；Missing / Disabled 的条目跳过实例化并记录日志。
        /// </summary>
        /// <param name="config">由 SDKComponent.Start() 构造并传入的配置 DTO，包含 PluginEntries 列表。</param>
        public abstract void Initialize(SDKManagerConfig config);

        /// <summary>
        /// 异步批量初始化所有已实例化的插件。
        /// 按 Priority 升序分桶，同桶内 UniTask.WhenAll 并行；单插件失败隔离（不影响其他插件与主流程）。
        /// 完成后 IsInitialized 置为 true。
        /// </summary>
        /// <param name="ct">取消令牌；传入 CancellationToken.None 时不可取消。</param>
        /// <returns>所有批次初始化完成的异步任务。</returns>
        public abstract UniTask InitializeAsync(CancellationToken ct = default);

        /// <summary>
        /// 异步释放所有已初始化的插件。
        /// 按 Priority 降序逐个 try/catch，单插件抛异常记 Log.Error 后继续释放其他插件。
        /// 完成后清空内部集合，IsInitialized 置为 false。
        /// </summary>
        /// <param name="ct">取消令牌。</param>
        /// <returns>所有插件释放完成的异步任务。</returns>
        public abstract UniTask DisposeAsync(CancellationToken ct = default);

        /// <summary>
        /// 管理器是否已完成 InitializeAsync（包含失败隔离后的最终状态）。
        /// </summary>
        public abstract bool IsInitialized { get; }

        /// <summary>
        /// 等待管理器完成 InitializeAsync。
        /// 若已完成则立即返回；若未完成则挂起直到完成或取消。
        /// </summary>
        /// <param name="ct">取消令牌。</param>
        /// <returns>等待完成的异步任务。</returns>
        public abstract UniTask WaitForInitializedAsync(CancellationToken ct = default);

        /// <summary>
        /// 按插件具体类型获取已初始化且可用的插件实例。
        /// 不可用时抛 SDKUnavailableException。
        /// </summary>
        /// <typeparam name="TPlugin">插件具体类型，必须为 class 且实现 ISDKPlugin。</typeparam>
        /// <returns>对应的插件实例。</returns>
        public abstract TPlugin Get<TPlugin>() where TPlugin : class, ISDKPlugin;

        /// <summary>
        /// 尝试按插件具体类型获取已初始化且可用的插件实例。
        /// </summary>
        /// <typeparam name="TPlugin">插件具体类型，必须为 class 且实现 ISDKPlugin。</typeparam>
        /// <param name="plugin">输出的插件实例；不可用时为 null。</param>
        /// <returns>插件可用返回 true，否则 false。</returns>
        public abstract bool TryGet<TPlugin>(out TPlugin plugin) where TPlugin : class, ISDKPlugin;

        /// <summary>
        /// 获取所有实现指定接口且当前可用（IsAvailable == true）的插件列表。
        /// 同一插件实现多个接口时，在各接口的查询结果中均出现（同一对象引用）。
        /// </summary>
        /// <typeparam name="TInterface">目标插件接口类型，必须为 class 且实现 ISDKPlugin。</typeparam>
        /// <returns>可用插件实例的只读列表，按 Priority 升序；若无可用实例返回空列表。</returns>
        public abstract IReadOnlyList<TInterface> GetAll<TInterface>() where TInterface : class, ISDKPlugin;

        /// <summary>
        /// 转发 OnApplicationPause 事件到所有实现 ISDKPauseListener 的插件。
        /// 由 SDKComponent 在 OnApplicationPause 回调中调用。
        /// </summary>
        /// <param name="isPaused">true 表示进入后台，false 表示回到前台。</param>
        public abstract void BroadcastPause(bool isPaused);

        /// <summary>
        /// 转发 OnApplicationFocus 事件到所有实现 ISDKFocusListener 的插件。
        /// 由 SDKComponent 在 OnApplicationFocus 回调中调用。
        /// </summary>
        /// <param name="hasFocus">true 表示获得焦点，false 表示失去焦点。</param>
        public abstract void BroadcastFocus(bool hasFocus);

        /// <summary>
        /// 转发 OnApplicationQuit 事件到所有实现 ISDKQuitListener 的插件。
        /// 由 SDKComponent 在 OnApplicationQuit 回调中调用。
        /// </summary>
        public abstract void BroadcastQuit();

        /// <summary>
        /// 通知用户登录，向 EventManager 发送 SDKEventData.UserLogin 事件。
        /// </summary>
        /// <param name="userId">已登录用户的唯一标识。</param>
        public abstract void Login(string userId);

        /// <summary>
        /// 框架管理器轮询（SDK Manager 无帧轮询需求，空实现）。
        /// </summary>
        public abstract override void Update();

        /// <summary>
        /// 关闭并清理管理器（内部 await DisposeAsync 完成释放）。
        /// </summary>
        public abstract override void Shutdown();
    }
}
