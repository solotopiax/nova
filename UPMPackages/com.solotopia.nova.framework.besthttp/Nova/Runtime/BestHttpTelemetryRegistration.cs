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
using System.Threading;

using Best.HTTP.Telemetry;

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

            if (ReferenceEquals(BestHttpTelemetry.Sink, s_Sink))
                BestHttpTelemetry.Sink = null;

            s_Sink?.ClearPending();
            s_Sink = null;
        }

        /// <summary>
        /// 程序集加载后自动注册 Nova 接收器，并等待 SDK 初始化完成后派发启动期缓存。
        /// </summary>
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterAssembliesLoaded)]
        internal static void Register()
        {
            Reset();

            s_Sink = new NovaBestHttpTelemetrySink(
                IsTelemetryEnabled,
                IsSdkReady,
                GetTrackPlugins);
            BestHttpTelemetry.Sink = s_Sink;

            s_ReadinessCancellation = new CancellationTokenSource();
            WaitForSdkReadyAsync(s_Sink, s_ReadinessCancellation.Token).Forget();
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
