/***************************************************************
 * (c) copyright 2026 - 2030, Solotopia
 * All Rights Reserved.
 * -------------------------------------------------------------
 * filename:  AdPlugin.Visitors.cs
 * author:    yingzheng
 * created:   2026/5/13
 * descrip:   AdPlugin 字段与属性
 ***************************************************************/

using System;
using System.Collections.Generic;
using System.Threading;
using Cysharp.Threading.Tasks;
using NovaFramework.Runtime;

namespace NovaFramework.SDK.AdPlugin.Runtime
{
    public sealed partial class AdPlugin
    {
        /// <summary>
        /// 插件友好名。
        /// </summary>
        public override string Name => "AdPlugin";

        /// <summary>
        /// 稳定遍历与释放排序值为 80；初始化先后由 InitializationDependencies 的能力依赖决定。
        /// </summary>
        public override int Priority => 80;

        /// <summary>
        /// 广告渠道初始化时缓存已启用的埋点能力；没有埋点插件时仍可初始化广告。
        /// </summary>
        public IReadOnlyList<SDKInitializationDependency> InitializationDependencies { get; } =
            new[] { new SDKInitializationDependency(typeof(ITrackPlugin), false) };

        /// <summary>
        /// 未注入配置时等待广告国家码的默认超时时间。
        /// </summary>
        private const float c_DefaultCountryCodeWaitTimeoutSeconds = 5f;

        /// <summary>
        /// SDKManager 注入的广告运行时配置。
        /// </summary>
        private AdPluginConfig m_RuntimeConfig;

        /// <summary>
        /// 已成功准备的业务渠道列表；配置创建的全部实例由渠道记录独立持有并负责释放。
        /// </summary>
        private List<IAdInternalPlugin> m_ChannelPlugins = new();

        /// <summary>
        /// 本聚合持有的渠道记录；失败实例保留在此处，直到释放收回资源。
        /// 渠道由字典键提供，配置与令牌只在准备和初始化时传递。
        /// </summary>
        private readonly Dictionary<IAdInternalPlugin, ChannelRegistration> m_ChannelRegistrations = new();

        /// <summary>
        /// 聚合层自有生命周期，控制渠道初始化与挂起业务请求。
        /// </summary>
        private readonly CancellationTokenSource m_ChannelLifetimeCts = new();

        /// <summary>
        /// 已释放后拒绝新请求与展示。
        /// </summary>
        private bool m_ChannelsDisposed;

        /// <summary>
        /// 共享聚合释放结果；重入或重复调用等待同一轮实际清理完成。
        /// </summary>
        private UniTaskCompletionSource m_ChannelDisposalCompletion;

        /// <summary>
        /// 渠道准备、初始化与退订的最小持有记录；不重复保存渠道、配置或生命周期令牌。
        /// </summary>
        private sealed class ChannelRegistration
        {
            /// <summary>
            /// 准备失败、取消或释放后失效，阻止已捕获的渠道委托回写业务事件。
            /// </summary>
            public volatile bool Active = true;

            /// <summary>
            /// 第一次事件绑定前保存的准确退订动作，部分绑定失败也能完成清理。
            /// </summary>
            public Action Unsubscribe;

            /// <summary>
            /// 准备与实际初始化方法全部退出后的完成信号；释放必须等待此信号。
            /// </summary>
            public readonly UniTaskCompletionSource InitializationCompletion = new();
        }

        /// <summary>
        /// 观察一次跨渠道请求的汇聚状态；结果完成后仍消费其余渠道结果。
        /// </summary>
        private sealed class BroadcastBatch
        {
            public readonly object Sync = new();
            public readonly UniTaskCompletionSource<AdLoadResult> Completion = new();
            public int Remaining;
            public AdLoadResult LastFailure;
        }

        /// <summary>
        /// 最近一次 RequestAsync(AdFormat.Banner) 成功后记录的活跃 Banner 渠道。
        /// ShowBanner / HideBanner 等 Banner 控制方法委托给此渠道。
        /// </summary>
        private IAdInternalPlugin m_ActiveBannerChannel;


        /// <summary>
        /// AdPlugin 所有可观察事件的容器；字段全部 readonly，禁止外部替换实例。
        /// 在 OnDisposeAsync 中统一调用 Clear() 释放缓冲。
        /// </summary>
        public AdPluginEvents Events { get; } = new AdPluginEvents();
    }
}
