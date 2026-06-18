/***************************************************************
 * (c) copyright 2026 - 2030, Solotopia
 * All Rights Reserved.
 * -------------------------------------------------------------
 * filename:  IAdPlugin.cs
 * author:    yingzheng
 * created:   2026/5/14
 * descrip:   IAA 广告聚合调度层唯一业务接口，继承 IBannerControl
 ***************************************************************/

using System.Collections.Generic;
using System.Threading;
using Cysharp.Threading.Tasks;

namespace NovaFramework.Runtime
{
    /// <summary>
    /// IAA 广告聚合调度层唯一业务接口。
    /// 内部持有多个 IAdInternalPlugin，RequestAsync 并行广播，ShowAsync 选 Revenue 最高渠道执行。
    /// 继承 IBannerControl 聚合 Banner 专属展示控制（ShowBanner/HideBanner 等）；所有方法主线程调用。
    /// Banner 格式的加载通过 RequestAsync(AdFormat.Banner) / IsReady(AdFormat.Banner) 走通用路径，
    /// 展示与控制由 IBannerControl 方法负责。
    /// 事件订阅请通过 AdPlugin 实例的 Events 属性访问（接口层不暴露具体事件容器类型）。
    /// </summary>
    public interface IAdPlugin : ISDKPlugin, IBannerControl
    {
        /// <summary>
        /// 查询指定广告格式是否正在播放中（插屏/RV 播放期间为 true）。
        /// </summary>
        /// <param name="format">广告格式。</param>
        /// <returns>正在播放中返回 true，否则 false。</returns>
        bool IsAdPlaying(AdFormat format);

        /// <summary>
        /// 向所有支持指定格式的渠道并行发起预加载请求。
        /// 返回统一的 AdLoadResult；Success=true 表示加载成功，Success=false 时携带错误码和错误描述。
        /// 业务层可 await 此方法后直接判断 Success，无需单独订阅加载事件才能取得失败详情。
        /// </summary>
        /// <param name="format">广告格式。</param>
        /// <param name="customProps">自定义属性字典，透传到渠道层 fill/fill_fail 打点；可为 null。</param>
        /// <param name="ct">取消令牌。</param>
        /// <returns>首个完成的加载结果；全部失败时返回最后一次失败结果。</returns>
        UniTask<AdLoadResult> RequestAsync(AdFormat format, Dictionary<string, object> customProps = null, CancellationToken ct = default);

        /// <summary>
        /// 查询是否有任意渠道的指定格式广告已就绪可立即展示。
        /// </summary>
        /// <param name="format">广告格式。</param>
        /// <returns>至少一个渠道就绪返回 true，否则 false。</returns>
        bool IsReady(AdFormat format);

        /// <summary>
        /// 展示指定格式广告，选 Revenue 最高的就绪渠道执行。
        /// AdFormat.Banner 不适用此方法，Banner 展示请使用 IBannerControl.ShowBanner()。
        /// </summary>
        /// <param name="format">广告格式。</param>
        /// <param name="customProps">自定义属性字典，透传到渠道层 show/show_result/hidden 打点；可为 null。</param>
        /// <param name="ct">取消令牌。</param>
        UniTask ShowAsync(AdFormat format, Dictionary<string, object> customProps = null, CancellationToken ct = default);
    }
}
