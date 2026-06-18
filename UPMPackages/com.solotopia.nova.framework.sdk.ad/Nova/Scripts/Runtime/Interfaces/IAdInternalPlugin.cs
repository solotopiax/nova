/***************************************************************
 * (c) copyright 2026 - 2030, Solotopia
 * All Rights Reserved.
 * -------------------------------------------------------------
 * filename:  IAdInternalPlugin.cs
 * author:    yingzheng
 * created:   2026/5/13
 * descrip:   广告渠道内部插件接口，MaxAdPlugin 等渠道实现此接口，不对外公开
 ***************************************************************/

using System;
using System.Collections.Generic;
using System.Threading;
using Cysharp.Threading.Tasks;
using NovaFramework.Runtime;

namespace NovaFramework.SDK.AdPlugin.Runtime
{
    /// <summary>
    /// 广告渠道内部插件接口，由各具体渠道实现（MaxAdPlugin / AdMobAdPlugin 等）。
    /// 仅供 AdPlugin 聚合调度层内部使用，不对业务层公开。
    /// AdPlugin 通过 Nova.SDK.GetAll<IAdInternalPlugin>() 发现所有渠道。
    /// 每个渠道实例内部持有广告位 ID 等 SDK 配置；调度层不感知具体 ID。
    /// Banner 格式的展示控制通过 IBannerControl 提供的方法执行。
    /// </summary>
    public interface IAdInternalPlugin : IBannerControl
    {
        /// <summary>
        /// 当前渠道的枚举类型标识；派生类返回对应的 AdChannelType 值。
        /// </summary>
        AdChannelType Channel { get; }

        /// <summary>
        /// 同步用户登录的 userId 到当前渠道 SDK；AdPlugin 收到 SDKEventData.UserLogin 后 fanout 调用本方法。
        /// 渠道无对应原生 API 时保留基类空实现即可。
        /// </summary>
        /// <param name="userId">已登录用户的唯一标识。</param>
        void SetUserId(string userId);

        /// <summary>
        /// 查询指定格式当前已加载广告的最高预期收益（USD）。
        /// 无就绪广告时返回 0；AdPlugin 用此值在多渠道中选 Revenue 最高的执行。
        /// </summary>
        /// <param name="format">广告格式。</param>
        /// <returns>已加载广告的最高预期收益（USD），无就绪广告时为 0。</returns>
        float GetMaxRevenue(AdFormat format);

        /// <summary>
        /// 广告收入回调事件，每次广告展示产生收入时在主线程触发。
        /// AdPlugin 订阅此事件后聚合转发给业务层。
        /// </summary>
        event Action<AdEvent> OnAdRevenuePaid;

        /// <summary>
        /// 广告加载成功事件，主线程触发。
        /// </summary>
        event Action<AdLoadResult> OnAdLoaded;

        /// <summary>
        /// 广告加载失败事件，主线程触发。
        /// </summary>
        event Action<AdLoadResult> OnAdLoadFailed;

        /// <summary>
        /// SDK 初始化完成事件，参数为初始化是否成功。
        /// </summary>
        event Action<bool> OnInitResult;

        /// <summary>
        /// 广告播放完成事件，携带展示结果。
        /// </summary>
        event Action<AdResult> OnShowCompleted;

        /// <summary>
        /// 广告播放失败事件，携带失败原因。
        /// </summary>
        event Action<AdResult> OnShowFailed;

        /// <summary>
        /// 广告关闭事件，携带关闭结果。
        /// </summary>
        event Action<AdResult> OnAdClosed;

        /// <summary>
        /// 异步请求（预加载）指定格式广告。
        /// 返回统一的 AdLoadResult；Success=true 表示加载成功，Success=false 时携带错误码和错误描述。
        /// </summary>
        /// <param name="format">广告格式。</param>
        /// <param name="reason">请求原因，默认 Auto。</param>
        /// <param name="customProps">自定义属性字典，将合并到 fill/fill_fail 打点；可为 null。</param>
        /// <param name="ct">取消令牌。</param>
        /// <returns>首个完成的加载结果；全部失败时返回最后一次失败结果。</returns>
        UniTask<AdLoadResult> RequestAsync(AdFormat format, AdRequestReason reason = AdRequestReason.Auto, Dictionary<string, object> customProps = null, CancellationToken ct = default);

        /// <summary>
        /// 查询指定格式广告是否已加载完毕可立即展示。
        /// </summary>
        /// <param name="format">广告格式。</param>
        /// <returns>已加载完毕返回 true，否则 false。</returns>
        bool IsReady(AdFormat format);

        /// <summary>
        /// 异步展示指定格式广告。
        /// AdFormat.Banner 不适用此方法，Banner 展示请使用 IBannerControl.ShowBanner()。
        /// </summary>
        /// <param name="format">广告格式。</param>
        /// <param name="customProps">自定义属性字典，将合并到 show/show_result/hidden 打点；可为 null。</param>
        /// <param name="ct">取消令牌。</param>
        /// <returns>广告展示结果。</returns>
        UniTask<AdResult> ShowAsync(AdFormat format, Dictionary<string, object> customProps = null, CancellationToken ct = default);
    }
}
