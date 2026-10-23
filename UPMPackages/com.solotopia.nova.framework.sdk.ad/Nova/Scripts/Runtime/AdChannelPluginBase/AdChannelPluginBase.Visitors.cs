/***************************************************************
 * (c) copyright 2026 - 2030, Solotopia
 * All Rights Reserved.
 * -------------------------------------------------------------
 * filename:  AdChannelPluginBase.Visitors.cs
 * author:    yingzheng
 * created:   2026/5/13
 * descrip:   AdChannelPluginBase 字段与属性
 ***************************************************************/

using System;
using System.Collections.Generic;
using System.Threading;
using Cysharp.Threading.Tasks;
using NovaFramework.Runtime;

namespace NovaFramework.SDK.AdPlugin.Runtime
{
    public abstract partial class AdChannelPluginBase
    {
        /// <summary>
        /// 缓存的变现打点插件列表；InitializeAsync 完成后由 CacheTrackPlugins 填充。
        /// </summary>
        private IReadOnlyList<ITrackPlugin> m_TrackPlugins;

        /// <summary>
        /// 初始化时缓存的文件片段管理器；存档读写前由启动流程完成 Persist.LoadAsync()。
        /// </summary>
        private IFileFragmentManager m_BannerIlrdPersistManager;

        /// <summary>
        /// 共享渠道释放结果；重复调用等待缓存、事件和原厂订阅全部清理完成。
        /// </summary>
        private UniTaskCompletionSource m_ChannelDisposeCompletion;

        /// <summary>
        /// 各广告格式对应的 AdUnit 槽位列表；派生类在 InitChannelSDKAsync 内通过 RegisterAdUnits 注入。
        /// 键为 AdFormat，值为该格式下所有槽位。
        /// </summary>
        private Dictionary<AdFormat, List<AdUnit>> m_AdUnits = new();

        /// <summary>
        /// 当前需要观察轮次的请求批次；任一观察轮成功或全部轮失败时，先移除再完成。
        /// 多个并发 RequestAsync 互不干扰，各批次独立等待。
        /// </summary>
        private List<RequestBatch> m_PendingBatches = new();

        /// <summary>
        /// 是否启用多渠道比价（eCPM 竞价）；AdPlugin 初始化时通过 ApplyGlobalConfig 写入。
        /// </summary>
        internal bool EnableBidding { get; private set; } = true;

        /// <summary>
        /// Banner ILRD 上报间隔次数；0 或负数表示不上报。AdPlugin 初始化时通过 ApplyGlobalConfig 写入。
        /// </summary>
        protected internal int BannerIlrdInterval { get; private set; } = 5;

        /// <summary>
        /// 是否全局静音所有广告；AdPlugin 初始化时通过 ApplyGlobalConfig 写入。
        /// </summary>
        protected internal bool MuteAd { get; private set; }

        /// <summary>
        /// 全部广告组共用的有效重试间隔序列；应用配置时复制，各组独立推进等待下标。
        /// </summary>
        internal IReadOnlyList<float> GroupRetryIntervalsSeconds { get; private set; } = AdChannelConfigList.CreateDefaultGroupRetryIntervalsSeconds();

        /// <summary>
        /// 初始化后的独立格式广告组，注册顺序用于同收益候选排序。
        /// </summary>
        private readonly Dictionary<AdFormat, List<AdGroup>> m_AdGroups = new();

        /// <summary>
        /// 格式配置错误；拒绝该格式请求，不影响其他有效格式。
        /// </summary>
        private readonly Dictionary<AdFormat, string> m_AdConfigFailures = new();

        /// <summary>
        /// 渠道托管生命周期；业务取消不取消已经发出的原厂加载或展示。
        /// </summary>
        private readonly CancellationTokenSource m_ChannelLifetimeCts = new();

        /// <summary>
        /// 缓存渠道令牌值，释放CTS后查询释放门仍能返回已取消令牌。
        /// </summary>
        private readonly CancellationToken m_ChannelLifetimeToken;

        /// <summary>
        /// 释放门同时供原厂后台回调安全读取。
        /// </summary>
        private volatile bool m_IsDisposed;

        /// <summary>
        /// 回调队列版本，仅用于过滤本实例已排队托管动作。
        /// </summary>
        private int m_AdCallbackGeneration;

        /// <summary>
        /// 渠道全屏展示的唯一占用。
        /// </summary>
        private ActiveAdShow m_ActiveAdShow;

        /// <summary>
        /// 派生渠道可用的释放门。
        /// </summary>
        protected bool IsDisposed => m_IsDisposed;

        /// <summary>
        /// 派生渠道可用的托管生命周期令牌，不是原厂取消API。
        /// </summary>
        protected CancellationToken ChannelLifetimeToken => m_ChannelLifetimeToken;

        /// <summary>
        /// SDK 回调转 Unity 主线程的 FIFO 队列。
        /// </summary>
        private readonly Queue<Action> m_AdCallbackQueue = new();

        /// <summary>
        /// m_AdCallbackQueue 的跨线程互斥锁。
        /// </summary>
        private readonly object m_AdCallbackQueueLock = new();

        /// <summary>
        /// 是否已有主线程 drain 任务在等待或执行。
        /// </summary>
        private bool m_AdCallbackDrainScheduled;
    }
}
