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
        /// 加载推进由所属广告组控制；组末项失败才等待统一间隔，业务观察结束不停止后台重试。
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
            /// 广告正在展示中；非 Banner 关闭或展示失败后自动回到 Idle 并触发续杯。
            /// </summary>
            Showing,
        }

        /// <summary>
        /// 单广告位运行时槽位，持有 placementId 的状态、收益与请求及展示属性。
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
            /// 最近一次加载失败的错误描述文本。
            /// </summary>
            public string LastErrorMessage;

            /// <summary>
            /// 本次 RequestAsync 传入的自定义属性；fill/fill_fail 打点时合并；续杯时置 null。
            /// </summary>
            public Dictionary<string, object> RequestCustomProps;

            /// <summary>
            /// 本次 ShowAsync 传入的自定义属性；show/show_result 成功与失败分支/hidden 打点时合并；续杯前置 null。
            /// </summary>
            public Dictionary<string, object> ShowCustomProps;

            /// <summary>
            /// 本次 RequestAsync 传入的请求原因；仅 nova_ad_request 事件使用。
            /// </summary>
            public AdRequestReason LastReason;

            /// <summary>
            /// 收益到达标记，由 RaiseRevenue 置为 true；当前关闭入口不读取或重置此字段。
            /// 对外 AdResult.RewardGranted 由渠道回调直接设置，不从本字段推导。
            /// </summary>
            public bool RewardGranted;

            /// <summary>
            /// 当前主动加载是否已收到终态，防止Ready复查期间接受重复终态。
            /// </summary>
            public bool LoadTerminalReceived;

            /// <summary>
            /// 槽位所属的初始化广告组；同一ID在渠道内只归属一个组。
            /// </summary>
            public AdGroup Group;
        }

        /// <summary>
        /// 单次 RequestAsync 调用代表的批次上下文。
        /// 持有独立完成源与待观察广告组轮次；任一轮成功或全部轮失败后先摘除再完成。
        /// </summary>
        protected sealed class RequestBatch
        {
            /// <summary>
            /// 本批次完成源；任一观察轮成功，或全部观察轮失败后返回最近实际失败。
            /// </summary>
            public UniTaskCompletionSource<AdLoadResult> Tcs;

            /// <summary>
            /// 本批次最近实际失败结果；全部观察轮失败后作为业务返回值。
            /// </summary>
            public AdLoadResult LastFailure;

            /// <summary>
            /// 本批次观察的广告组与轮次，不因组内切换广告位而丢失等待目标。
            /// </summary>
            public Dictionary<AdGroup, long> PendingRounds = new();

            /// <summary>
            /// ct 取消回调注册句柄；批次完成或取消时须调用 Dispose 防止 CancellationTokenRegistration 泄漏。
            /// ct 不可取消时为 default（值类型，无需 null 判断，直接 Dispose 安全）。
            /// </summary>
            public CancellationTokenRegistration Registration;
        }


        /// <summary>
        /// 广告组状态；等待与展示均不会产生无终点的业务观察。
        /// </summary>
        protected enum AdGroupState
        {
            Idle,
            Loading,
            Ready,
            Showing,
            WaitingRetry
        }

        /// <summary>
        /// 单渠道、单格式的有序加载组；轮次仅标识托管动作，不是原厂请求ID。
        /// </summary>
        protected sealed class AdGroup
        {
            /// <summary>
            /// 当前广告格式。
            /// </summary>
            public AdFormat Format;
            /// <summary>
            /// 注册顺序固定的广告位列表。
            /// </summary>
            public readonly List<AdUnit> Units = new();
            /// <summary>
            /// 组当前状态。
            /// </summary>
            public AdGroupState State;
            /// <summary>
            /// 正在尝试、缓存或展示的广告位下标。
            /// </summary>
            public int ActiveUnitIndex;
            /// <summary>
            /// 当前托管轮次；用于过滤旧timer与排队动作。
            /// </summary>
            public long RoundId;
            /// <summary>
            /// 最近一轮的实际加载失败结果。
            /// </summary>
            public AdLoadResult LastFailure;
            /// <summary>
            /// 下一次整组失败使用的间隔下标；整组失败推进，任意条目成功归零。
            /// </summary>
            public int RetryIntervalIndex;

            /// <summary>
            /// 当前广告组重试等待的取消源，与其他组相互独立。
            /// </summary>
            public CancellationTokenSource RetryCancellation;
            /// <summary>
            /// 新轮启动者的自定义属性快照；加入轮次不会覆盖。
            /// </summary>
            public Dictionary<string, object> RequestCustomProps;
            /// <summary>
            /// 本轮请求原因，组内后项沿用。
            /// </summary>
            public AdRequestReason Reason;
        }

        /// <summary>
        /// 全屏展示占用只由匹配原厂终态或释放解除。
        /// </summary>
        private sealed class ActiveAdShow
        {
            /// <summary>
            /// 占用的格式。
            /// </summary>
            public AdFormat Format;
            /// <summary>
            /// 占用的广告位。
            /// </summary>
            public string PlacementId;
            /// <summary>
            /// 是否已经收到displayed，重复displayed不重复派发。
            /// </summary>
            public bool Displayed;
        }

        /// <summary>
        /// 配置注入数据，由派生渠道在 RegisterAdUnits 时传入。
        /// 每个真实ID作为独立单ID组累加注册；AdUnitOptions 构造签名保持兼容。
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
