/***************************************************************
 * (c) copyright 2026 - 2030, Solotopia
 * All Rights Reserved.
 * -------------------------------------------------------------
 * filename:  AdPluginEvents.cs
 * author:    yingzheng
 * created:   2026/5/14
 * descrip:   AdPlugin 事件容器，持有所有 ObservableEvent 字段
 ***************************************************************/

using NovaFramework.Runtime;

namespace NovaFramework.SDK.AdPlugin.Runtime
{
    /// <summary>
    /// AdPlugin 事件容器。
    /// 持有所有可观察事件字段，由 AdPlugin 实例创建并对外暴露。
    /// </summary>
    public sealed class AdPluginEvents
    {
        /// <summary>
        /// SDK 初始化结果。
        /// Sticky 模式：业务层订阅时若 SDK 已完成初始化则立即收到结果，无需关心订阅时序。
        /// </summary>
        public readonly StickyEvent<bool> InitResult = new StickyEvent<bool>();

        /// <summary>
        /// 广告加载成功事件。
        /// Sticky 模式：订阅时补发最近一次加载成功事件，确保业务层不遗漏已加载状态。
        /// </summary>
        public readonly StickyEvent<AdLoadResult> AdLoaded = new StickyEvent<AdLoadResult>();

        /// <summary>
        /// 广告加载失败事件。
        /// Sticky 模式：订阅时补发最近一次加载失败事件，便于业务层在订阅后立即处理失败状态。
        /// </summary>
        public readonly StickyEvent<AdLoadResult> AdLoadFailed = new StickyEvent<AdLoadResult>();

        /// <summary>
        /// 广告播放完成事件。
        /// Replay 模式（cap=32）：每条播放完成记录独立有意义，不可被后续事件覆盖。
        /// </summary>
        public readonly ReplayEvent<AdResult> ShowCompleted = new ReplayEvent<AdResult>(32);

        /// <summary>
        /// 广告播放失败事件。
        /// Replay 模式（cap=32）：每条失败记录独立有意义，业务层需逐条处理。
        /// </summary>
        public readonly ReplayEvent<AdResult> ShowFailed = new ReplayEvent<AdResult>(32);

        /// <summary>
        /// 广告收益回调事件。
        /// Replay 模式（cap=32）：每次收益均需上报，不可丢失，使用 Replay 保障无漏发。
        /// </summary>
        public readonly ReplayEvent<AdEvent> RevenuePaid = new ReplayEvent<AdEvent>(32);

        /// <summary>
        /// 广告关闭回调事件。
        /// Replay 模式（cap=32）：每条关闭记录独立有意义，业务层需逐条响应。
        /// </summary>
        public readonly ReplayEvent<AdResult> AdClosed = new ReplayEvent<AdResult>(32);
    }
}
