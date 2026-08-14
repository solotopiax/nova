/***************************************************************
 * (c) copyright 2026 - 2030, Solotopia
 * All Rights Reserved.
 * -------------------------------------------------------------
 * filename:  BestHttpTelemetryRegistration.cs
 * author:    taoye
 * created:   2026/8/3
 * descrip:   BestHTTP 遥测接收器自动注册
 ***************************************************************/

#if NOVA_BEST_HTTP

using System;
using System.Collections.Generic;
using System.Reflection;
using System.Threading;

using Cysharp.Threading.Tasks;

using NovaFramework.Runtime;

using UnityEngine;

namespace NovaFramework.BestHTTP.Runtime
{
    /// <summary>
    /// 在运行时自动安装 Nova Best HTTP 遥测接收器，并在 SDK 初始化完成后清空启动缓存。
    /// </summary>
    internal static class BestHttpTelemetryRegistration
    {
        private static NovaBestHttpTelemetrySink s_Sink;
        private static Action<string, IReadOnlyDictionary<string, object>> s_EventHandler;
        private static PropertyInfo s_EventHandlerProperty;
        private static CancellationTokenSource s_ReadinessCancellation;

        /// <summary>
        /// 重置本包拥有的接收器与 SDK 就绪等待任务，不覆盖其他调用方后来注册的接收器。
        /// </summary>
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        internal static void Reset()
        {
            s_ReadinessCancellation?.Cancel();
            s_ReadinessCancellation?.Dispose();
            s_ReadinessCancellation = null;

            try
            {
                if (s_EventHandlerProperty != null &&
                    ReferenceEquals(s_EventHandlerProperty.GetValue(null), s_EventHandler))
                {
                    s_EventHandlerProperty.SetValue(null, null);
                }
            }
            catch (Exception exception)
            {
                Log.Warning(LogTag.SDK, "清理 BestHTTP 网络埋点委托失败，已忽略：{0}", exception.Message);
            }

            s_Sink?.ClearPending();
            s_Sink = null;
            s_EventHandler = null;
            s_EventHandlerProperty = null;
        }

        /// <summary>
        /// 程序集加载后同步注册 Nova 接收器，为启动期事件建立缓存入口。
        /// </summary>
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterAssembliesLoaded)]
        internal static void Register()
        {
            Reset();

            s_EventHandlerProperty = ResolveEventHandlerProperty();
            if (s_EventHandlerProperty == null)
            {
                return;
            }

            s_Sink = new NovaBestHttpTelemetrySink(
                IsTelemetryEnabled,
                IsSdkReady,
                GetTrackPlugins);
            s_EventHandler = s_Sink.Track;
            try
            {
                s_EventHandlerProperty.SetValue(null, s_EventHandler);
            }
            catch (Exception exception)
            {
                Log.Warning(LogTag.SDK, "注册 BestHTTP 网络埋点委托失败，已跳过遥测：{0}", exception.Message);
                s_Sink = null;
                s_EventHandler = null;
                s_EventHandlerProperty = null;
                return;
            }

            s_ReadinessCancellation = new CancellationTokenSource();
        }

        /// <summary>
        /// 一次反射查找内部 BestHTTP 提供的标准委托属性；官方原版不存在时静默跳过遥测。
        /// </summary>
        /// <returns>签名匹配的静态属性，不支持时返回 null。</returns>
        private static PropertyInfo ResolveEventHandlerProperty()
        {
            Type telemetryType = typeof(Best.HTTP.HTTPRequest).Assembly.GetType(
                "Best.HTTP.Telemetry.BestHttpTelemetry",
                false);
            PropertyInfo property = telemetryType?.GetProperty(
                "EventHandler",
                BindingFlags.Public | BindingFlags.Static);
            return property != null &&
                   property.SetMethod?.IsPublic == true &&
                   property.PropertyType == typeof(Action<string, IReadOnlyDictionary<string, object>>)
                ? property
                : null;
        }

        /// <summary>
        /// 场景加载前启动 SDK 就绪监听，确保此时 UniTask 已完成 PlayerLoop 注入。
        /// </summary>
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        private static void StartReadinessWatch()
        {
            NovaBestHttpTelemetrySink sink = s_Sink;
            CancellationTokenSource cancellation = s_ReadinessCancellation;
            if (sink == null || cancellation == null || cancellation.IsCancellationRequested)
                return;

            if (IsSdkReady())
            {
                sink.FlushPendingIfReady();
                return;
            }

            WaitForSdkReadyAsync(sink, cancellation.Token).Forget();
        }

        /// <summary>
        /// 读取 Network Inspector 中的 BestHTTP 网络埋点开关。
        /// </summary>
        /// <returns>网络组件存在且开关开启时返回 true。</returns>
        private static bool IsTelemetryEnabled()
        {
            return Nova.Network != null &&
                   Nova.Network.HttpSettings != null &&
                   Nova.Network.HttpSettings.EnableBestHttpTelemetry;
        }

        /// <summary>
        /// 判断 SDK 插件系统是否已完成初始化。
        /// </summary>
        /// <returns>SDK 已初始化时返回 true。</returns>
        private static bool IsSdkReady()
        {
            return Nova.SDK != null && Nova.SDK.IsInitialized;
        }

        /// <summary>
        /// 获取当前所有已初始化且可用的通用埋点插件。
        /// </summary>
        /// <returns>按 SDK 插件优先级排序的只读列表；SDK 不存在时返回空列表。</returns>
        private static IReadOnlyList<ITrackPlugin> GetTrackPlugins()
        {
            return Nova.SDK != null
                ? Nova.SDK.GetAll<ITrackPlugin>()
                : Array.Empty<ITrackPlugin>();
        }

        /// <summary>
        /// 按帧等待 SDK 就绪，并让指定接收器按原始顺序清空启动期缓存。
        /// </summary>
        /// <param name="sink">本次注册拥有的 Nova 接收器。</param>
        /// <param name="cancellationToken">子系统重置或退出时的取消令牌。</param>
        private static async UniTask WaitForSdkReadyAsync(
            NovaBestHttpTelemetrySink sink,
            CancellationToken cancellationToken)
        {
            try
            {
                while (!cancellationToken.IsCancellationRequested && ReferenceEquals(sink, s_Sink))
                {
                    if (IsSdkReady())
                    {
                        sink.FlushPendingIfReady();
                        return;
                    }

                    await UniTask.Yield(PlayerLoopTiming.Update, cancellationToken);
                }
            }
            catch (OperationCanceledException)
            {
                // 子系统重置或应用退出负责触发取消，无需继续向上传播。
            }
        }
    }
}

#endif
