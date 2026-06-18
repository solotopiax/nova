/***************************************************************
 * (c) copyright 2026 - 2030, Solotopia
 * All Rights Reserved.
 * -------------------------------------------------------------
 * filename:  AdChannelPluginBase.Definitions.cs
 * author:    yingzheng
 * created:   2026/5/15
 * descrip:   AdChannelPluginBase 嵌套类型定义：状态枚举、AdUnit 槽位类、AdUnitOptions 配置结构
 ***************************************************************/

using System.Collections.Generic;
using System.Threading;
using Cysharp.Threading.Tasks;
using NovaFramework.Runtime;

namespace NovaFramework.SDK.AdPlugin.Runtime
{
    public abstract partial class AdChannelPluginBase
    {
        /// <summary>
        /// 广告位槽位状态枚举，描述单个 placementId 的完整生命周期。
        /// 重试由全局 RetryLoadAdMaxNum / RetryLoadAdInterv 控制：未达上限立即重试，达上限等待间隔后重置计数继续重试，
        /// 因此不存在永久失败的终止态。
        /// </summary>
        protected enum AdUnitState
        {
            /// <summary>
            /// 初始状态或重试就绪，可重新发起加载请求。
            /// </summary>
            Idle,

            /// <summary>
            /// 已发起加载请求，等待 SDK 回调；此状态下禁止重复请求同一槽位。
            /// </summary>
            Loading,

            /// <summary>
            /// 广告已加载完毕，可参与 ShowAsync 候选筛选。
            /// </summary>
            Ready,

            /// <summary>
            /// 广告正在展示中；非 Banner 展示结束后自动回到 Idle 并触发续杯。
            /// </summary>
            Showing,
        }

        /// <summary>
        /// 单广告位运行时槽位，持有 placementId 的全部状态与重试元数据。
        /// </summary>
        protected sealed class AdUnit
        {
            /// <summary>
            /// 广告位唯一标识，来自派生渠道配置注入。
            /// </summary>
            public string PlacementId;

            /// <summary>
            /// 该槽位对应的广告格式。
            /// </summary>
            public AdFormat Format;

            /// <summary>
            /// 当前状态机状态。
            /// </summary>
            public AdUnitState State;

            /// <summary>
            /// 广告加载成功后 SDK 报告的 eCPM 收益（USD）；Ready 状态下有效，用于 ShowAsync eCPM 优先排序。
            /// </summary>
            public double Revenue;

            /// <summary>
            /// 当前已重试次数；加载成功后重置为 0。
            /// 触达全局 RetryLoadAdMaxNum 后，等待 RetryLoadAdInterv 秒由 ScheduleRetry 在重试前重置回 0。
            /// </summary>
            public int RetryCount;

            /// <summary>
            /// 最近一次加载失败的错误描述文本。
            /// </summary>
            public string LastErrorMessage;

            /// <summary>
            /// 延时重试任务的取消令牌源；DisposeChannelSDKAsync 时统一 Cancel 以防泄露。
            /// </summary>
            public CancellationTokenSource RetryCts;

            /// <summary>
            /// 本次 RequestAsync 传入的自定义属性；fill/fill_fail 打点时合并；续杯时置 null。
            /// </summary>
            public Dictionary<string, object> RequestCustomProps;

            /// <summary>
            /// 本次 ShowAsync 传入的自定义属性；show/show_result/hidden 打点时合并；续杯前置 null。
            /// </summary>
            public Dictionary<string, object> ShowCustomProps;

            /// <summary>
            /// 本次 RequestAsync 传入的请求原因；仅 nova_ad_request 事件使用。
            /// </summary>
            public AdRequestReason LastReason;

            /// <summary>
            /// 本次展示期间是否已收到收益回调（RaiseRevenue 已触发）。
            /// RaiseAdClosed 写入 AdResult.RewardGranted 后随 MarkShown 一起重置为 false。
            /// </summary>
            public bool RewardGranted;
        }

        /// <summary>
        /// 单次 RequestAsync 调用代表的批次上下文。
        /// 持有本批次的完成源和等待中的广告位集合；任一广告位加载成功即 SetResult 并出队。
        /// </summary>
        protected sealed class RequestBatch
        {
            /// <summary>
            /// 本批次的完成源；首个成功 RaiseAdLoaded 时调用 TrySetResult，全部失败时 TrySetResult(default)。
            /// </summary>
            public UniTaskCompletionSource<AdLoadResult> Tcs;

            /// <summary>
            /// 本批次最近一次失败结果；全部广告位都失败时作为 RequestAsync 返回值。
            /// </summary>
            public AdLoadResult LastFailure;

            /// <summary>
            /// 本批次仍在等待结果的广告位 ID 集合；加载成功或重试耗尽时从中移除对应 ID。
            /// </summary>
            public HashSet<string> PendingPlacementIds;

            /// <summary>
            /// ct 取消回调注册句柄；批次完成或取消时须调用 Dispose 防止 CancellationTokenRegistration 泄漏。
            /// ct 不可取消时为 default（值类型，无需 null 判断，直接 Dispose 安全）。
            /// </summary>
            public CancellationTokenRegistration Registration;
        }

        /// <summary>
        /// 配置注入数据，由派生渠道在 RegisterAdUnits 时传入。
        /// 重试参数已由全局 RetryLoadAdMaxNum / RetryLoadAdInterv 统一控制，AdUnitOptions 只承载 PlacementId。
        /// </summary>
        public readonly struct AdUnitOptions
        {
            /// <summary>
            /// 广告位唯一标识。
            /// </summary>
            public readonly string PlacementId;

            /// <summary>
            /// 构造 AdUnitOptions 配置值。
            /// </summary>
            /// <param name="placementId">广告位唯一标识。</param>
            public AdUnitOptions(string placementId)
            {
                PlacementId = placementId;
            }
        }
    }
}
