/***************************************************************
 * (c) copyright 2026 - 2030, Solotopia
 * All Rights Reserved.
 * -------------------------------------------------------------
 * filename:  ISDKPlugin.cs
 * author:    taoye
 * created:   2026/3/18
 * descrip:   SDK 插件基础接口及可选生命周期/账号监听接口
 ***************************************************************/

using System.Threading;
using Cysharp.Threading.Tasks;

namespace NovaFramework.Runtime
{
    /// <summary>
    /// SDK 插件可选生命周期钩子：OnApplicationFocus 代理。
    /// 实现此接口的 Plugin 会在 SDKComponent.OnApplicationFocus 触发时收到回调。
    /// 主线程调用（Unity 生命周期保证）。
    /// </summary>
    public interface ISDKFocusListener
    {
        /// <summary>
        /// 应用获得/失去焦点时由 SDKManager.BroadcastFocus 调用。
        /// </summary>
        /// <param name="hasFocus">true 表示获得焦点，false 表示失去焦点。</param>
        void OnFocus(bool hasFocus);
    }

    /// <summary>
    /// SDK 插件可选生命周期钩子：OnApplicationPause 代理。
    /// 实现此接口的 Plugin 会在 SDKComponent.OnApplicationPause 触发时收到回调。
    /// 主线程调用（Unity 生命周期保证）。
    /// </summary>
    public interface ISDKPauseListener
    {
        /// <summary>
        /// 应用暂停/恢复时由 SDKManager.BroadcastPause 调用。
        /// </summary>
        /// <param name="isPaused">true 表示进入后台暂停，false 表示恢复前台。</param>
        void OnPause(bool isPaused);
    }

    /// <summary>
    /// SDK 插件可选生命周期钩子：OnApplicationQuit 代理。
    /// 实现此接口的 Plugin 会在 SDKComponent.OnApplicationQuit 触发时收到回调。
    /// 主线程调用（Unity 生命周期保证）。
    /// </summary>
    public interface ISDKQuitListener
    {
        /// <summary>
        /// 应用退出时由 SDKManager.BroadcastQuit 调用。
        /// 在此方法中执行同步清理逻辑（异步清理走 DisposeAsync）。
        /// </summary>
        void OnQuit();
    }

    /// <summary>
    /// SDK 插件基础接口。
    /// 所有业务子接口必须继承此接口。
    /// Plugin 为纯 C# 类，通过 Inspector 启用列表 + 反射实例化，非 MonoBehaviour。
    /// </summary>
    public interface ISDKPlugin
    {
        /// <summary>
        /// 插件友好名（诊断日志 + Inspector 显示用，不参与查询）。
        /// </summary>
        string Name { get; }

        /// <summary>
        /// 初始化优先级（值越小越先提交；同批内并行执行）。
        /// </summary>
        int Priority { get; }

        /// <summary>
        /// 当前是否可用（初始化成功后为 true，失败后永久为 false）。
        /// </summary>
        bool IsAvailable { get; }

        /// <summary>
        /// 异步初始化。
        /// 实现方必须：1) 完成前切回主线程；2) 异常以 throw 形式抛出（上层统一捕获）。
        /// 主线程调用，实现内部可切线程，完成前须 await UniTask.SwitchToMainThread。
        /// </summary>
        /// <param name="config">由 SDKManager 按 RequiredConfigType 从 IConfigManager 拉取并注入的配置；无需 config 时传 null。</param>
        /// <param name="ct">取消令牌（由 Manager 串联）。</param>
        /// <returns>初始化完成的异步任务。</returns>
        UniTask InitializeAsync(ISDKPluginConfig config, CancellationToken ct);

        /// <summary>
        /// 异步释放资源。
        /// 逆序 Priority 调度，异常被上层捕获不中断其他 Plugin。
        /// 主线程调用。
        /// </summary>
        /// <param name="ct">取消令牌。</param>
        /// <returns>释放完成的异步任务。</returns>
        UniTask DisposeAsync(CancellationToken ct);

        /// <summary>
        /// 按 key 异步获取 Plugin 内部数据槽位中的值。
        /// 值已就绪时立即返回；值未就绪时挂起，直到对应 Plugin 调用 PublishData 写入或 ct 被取消。
        /// 取消时 await 以 OperationCanceledException 结束，调用方须自行处理。
        /// 注意：每个 key 对应一种确定的值类型，写方（PublishData）与读方（FetchDataAsync）
        /// 必须在业务层约定一致；返回类型为 object，编译期不做类型检查，运行时强转由调用方负责。
        /// 注意：再次 PublishData 会覆盖槽位并唤醒当前所有等待者；
        /// 已经 await 返回的调用方不会自动感知新值，如需获取最新值须再次调用 FetchDataAsync。
        /// </summary>
        /// <param name="key">数据键，不得为 null 或空字符串；同一 Plugin 内唯一标识一种数据。</param>
        /// <param name="ct">取消令牌，调用方可随时取消等待。</param>
        /// <returns>槽位中的数据值（object 类型，调用方按约定强转）。</returns>
        UniTask<object> FetchDataAsync(string key, CancellationToken ct = default);
    }
}
