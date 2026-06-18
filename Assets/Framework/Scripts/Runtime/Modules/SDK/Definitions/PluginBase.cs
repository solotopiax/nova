/***************************************************************
 * (c) copyright 2026 - 2030, Solotopia
 * All Rights Reserved.
 * -------------------------------------------------------------
 * filename:  PluginBase.cs
 * author:    taoye
 * created:   2026/4/30
 * descrip:   SDK 插件泛型基类
 ***************************************************************/

using System;
using System.Threading;
using Cysharp.Threading.Tasks;

namespace NovaFramework.Runtime
{
    /// <summary>
    /// SDK 插件强类型泛型基类。
    /// 继承此类的插件将 ConfigType 固化为 TConfig，SDKManager 在初始化时自动从
    /// IConfigManager 按 RequiredConfigType 拉取配置并注入；若未取到则跳过该插件。
    /// 派生类只需实现 OnInitializeAsync(TConfig, CancellationToken) 强类型版本，
    /// 无需关心 ISDKPluginConfig 到 TConfig 的转换细节。
    /// </summary>
    /// <typeparam name="TConfig">本插件所需的配置类型，必须实现 ISDKPluginConfig。</typeparam>
    public abstract class PluginBase<TConfig> : SDKPluginBase
        where TConfig : class, ISDKPluginConfig
    {
        /// <summary>
        /// 将 ConfigType 固化为 TConfig，阻止派生类覆盖，确保 RequiredConfigType 返回值稳定。
        /// </summary>
        protected sealed override Type ConfigType => typeof(TConfig);

        /// <summary>
        /// ISDKPluginConfig 到 TConfig 的桥接初始化方法。
        /// 执行强转校验后委托给强类型版 OnInitializeAsync(TConfig, CancellationToken)。
        /// config 类型不匹配时抛 InvalidCastException，终止初始化并由 SDKManager 统一捕获。
        /// </summary>
        /// <param name="config">由 SDKManager 注入的配置，要求运行时类型为 TConfig。</param>
        /// <param name="ct">取消令牌（由 SDKManager 串联）。</param>
        /// <returns>初始化完成的异步任务。</returns>
        protected sealed override UniTask OnInitializeAsync(ISDKPluginConfig config, CancellationToken ct)
        {
            if (config is not TConfig typed)
                throw new InvalidCastException($"PluginBase<{typeof(TConfig).Name}> 期望 {typeof(TConfig).Name} 配置，实际得到 {config?.GetType().Name ?? "<null>"}。");
            return OnInitializeAsync(typed, ct);
        }

        /// <summary>
        /// 派生类实现点：使用强类型配置执行实际初始化逻辑。
        /// 抛出异常时基类不会将 IsAvailable 置 true；SDKManager 统一捕获并记录诊断。
        /// 可后台线程执行，完成前须 await UniTask.SwitchToMainThread(ct) 切回主线程。
        /// </summary>
        /// <param name="config">已完成类型校验的强类型配置实例。</param>
        /// <param name="ct">取消令牌。</param>
        /// <returns>初始化完成的异步任务。</returns>
        protected abstract UniTask OnInitializeAsync(TConfig config, CancellationToken ct);
    }
}
