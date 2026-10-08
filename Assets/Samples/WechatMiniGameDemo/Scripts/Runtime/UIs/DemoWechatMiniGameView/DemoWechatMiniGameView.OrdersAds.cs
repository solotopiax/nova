/***************************************************************
 * (c) copyright 2026 - 2030, Solotopia
 * All Rights Reserved.
 * -------------------------------------------------------------
 * filename:  DemoWechatMiniGameView.OrdersAds.cs
 * author:    Codex
 * created:   2026/10/08
 * descrip:   微信订单选择、广告及原生订阅演示
 ***************************************************************/
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using Cysharp.Threading.Tasks;
using NovaFramework.SDK.WeChatMiniGame.Runtime;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.EventSystems;

namespace NovaFramework.Sdk.Wechat.Minigame.Samples.Runtime
{
    public sealed partial class DemoWechatMiniGameView
    {
        private readonly List<Button> m_OrderRows = new();
        private IReadOnlyList<WeChatMiniGamePaymentOrderInfo> m_ServerOrders = Array.Empty<WeChatMiniGamePaymentOrderInfo>();
        private int m_OrderKindFilter;
        private string m_SelectedOrderId;
        private DemoWechatFulfillmentService m_Fulfillment;
        private IDisposable m_SubscribeTouchEnd;
        private IDisposable m_SubscribeTouchStart;
        private readonly Dictionary<int, Vector2> m_SubscribeTouches = new();
        private readonly List<RaycastResult> m_SubscribeRaycasts = new();
        private WeChatMiniGameRewardedAd m_Rewarded;
        private WeChatMiniGameInterstitialAd m_Interstitial;
        private WeChatMiniGameBannerAd m_Banner;
        private bool m_RewardedLoaded;
        private bool m_InterstitialLoaded;
        private bool m_Advertising;
        private bool m_RewardedLoading;
        private bool m_InterstitialLoading;
        private bool m_BannerLoading;
        private bool m_BannerLoaded;
        private bool m_BannerShown;
        private bool m_Subscribing;

        /// <summary>从 Demo View 读取通知物料，不进入 SDK 正式配置。</summary>
        private void ApplyDemoMaterials()
        {
            SubscriptionTemplateIds = new[] { m_DemoMaterials.TemplateId };
            NoticeConfigId = m_DemoMaterials.NoticeConfigId;
            NoticeClientTaskId = m_DemoMaterials.NoticeTaskKey;
            SetDemoFormInitialValues();
        }

        /// <summary>页面仅订阅发货反馈；长期发货器不持有 View，可在关闭页面后继续补单。</summary>
        private void OnFulfillmentFeedback(string message) => AppendFeedback(message, FeedbackLevel.Info);

        /// <summary>依次选择全部、游戏币、道具，保持所选订单只来自当前筛选列表。</summary>
        private void OnFilterOrdersClick()
        {
            m_OrderKindFilter = (m_OrderKindFilter + 1) % 3;
            BindButton(m_FilterOrdersButton, OnFilterOrdersClick,
                "订单类型：" + (m_OrderKindFilter == 0 ? "全部" : m_OrderKindFilter == 1 ? "游戏币" : "道具"), "订单类型筛选");
            if (TryGetWechat(out WeChatMiniGamePlugin plugin)) RefreshOrderRows(plugin);
        }

        /// <summary>合并本地未完成订单与服务端只读订单，按类型创建可选择的订单行。</summary>
        private void RefreshOrderRows(WeChatMiniGamePlugin plugin)
        {
            ClearOrderRows();
            if (m_FilterOrdersButton == null) return;
            var rows = new Dictionary<string, (WeChatMiniGamePaymentKind kind, string label)>();
            foreach (WeChatMiniGamePaymentOrderInfo order in m_ServerOrders)
                rows[order.OrderId] = (order.Kind, $"服务端 {order.OrderId}｜{order.Kind}｜{DescribeVerificationStatus(order.Status)}");
            foreach (WeChatMiniGamePaymentOrderRecord order in plugin.GetPendingPaymentOrders())
                rows[order.OrderId] = (order.Kind, $"本地待处理 {order.OrderId}｜{order.Kind}｜{order.Status}");
            int index = m_FilterOrdersButton.transform.GetSiblingIndex() + 1;
            foreach (var row in rows.OrderBy(item => item.Key))
            {
                if (m_OrderKindFilter == 1 && row.Value.kind != WeChatMiniGamePaymentKind.GameCurrency ||
                    m_OrderKindFilter == 2 && row.Value.kind != WeChatMiniGamePaymentKind.GameItem) continue;
                string orderId = row.Key;
                Button button = Instantiate(m_QueryOrderButton, m_InteractionRoot);
                button.gameObject.name = "OrderSelectionRow";
                button.transform.SetSiblingIndex(index++);
                BindButton(button, () =>
                {
                    m_SelectedOrderId = orderId;
                    AppendFeedback($"已选中订单 {orderId}，点击“验证选中订单”进行本地补单。", FeedbackLevel.Info);
                }, row.Value.label, "选择订单");
                m_OrderRows.Add(button);
            }
            AppendFeedback($"当前筛选列表 {m_OrderRows.Count} 笔，点击任意订单可选择；服务端已完成订单只读。", FeedbackLevel.Info);
        }

        /// <summary>销毁本次页面会话动态创建的订单行，并清空选中状态。</summary>
        private void ClearOrderRows()
        {
            foreach (Button row in m_OrderRows) if (row != null) Destroy(row.gameObject);
            m_OrderRows.Clear();
            m_SelectedOrderId = null;
        }

        /// <summary>记录触摸开始时实际命中按钮的触点，避免滚动结束被解释成订阅点击。</summary>
        private void OnSubscribeTouchStart(WeChatMiniGameTouchEvent touchEvent)
        {
            foreach (WeChatMiniGameTouchPoint point in touchEvent.ChangedTouches)
            {
                if (TryGetSubscribePoint(point, out Vector2 screenPoint) && IsSubscribeTopHit(screenPoint))
                    m_SubscribeTouches[point.Identifier] = screenPoint;
            }
        }

        /// <summary>原生结束回调只接受开始与结束均命中且未拖动的触点，并立即拉起订阅。</summary>
        private void OnSubscribeTouchEnd(WeChatMiniGameTouchEvent touchEvent)
        {
            if (m_Subscribing) return;
            foreach (WeChatMiniGameTouchPoint point in touchEvent.ChangedTouches)
            {
                bool started = m_SubscribeTouches.TryGetValue(point.Identifier, out Vector2 startPoint);
                m_SubscribeTouches.Remove(point.Identifier);
                if (!started || !TryGetSubscribePoint(point, out Vector2 screenPoint)) continue;
                int threshold = EventSystem.current != null ? EventSystem.current.pixelDragThreshold : 10;
                if ((screenPoint - startPoint).sqrMagnitude > threshold * threshold || !IsSubscribeTopHit(screenPoint)) continue;
                RequestNativeSubscriptionAsync().Forget();
                break;
            }
        }

        /// <summary>把微信窗口左上角客户坐标转换为 Unity 屏幕左下角坐标。</summary>
        private bool TryGetSubscribePoint(WeChatMiniGameTouchPoint point, out Vector2 screenPoint)
        {
            screenPoint = default;
            if (m_Wechat == null || point == null) return false;
            float width = (float)m_Wechat.RuntimeInfo.WindowWidth;
            float height = (float)m_Wechat.RuntimeInfo.WindowHeight;
            if (width <= 0 || height <= 0) return false;
            screenPoint = new Vector2((float)point.ClientX * Screen.width / width,
                Screen.height - (float)point.ClientY * Screen.height / height);
            return true;
        }

        /// <summary>通过 Unity UI 顶层射线判断按钮可交互且未被弹窗、CanvasGroup 或遮罩遮挡。</summary>
        private bool IsSubscribeTopHit(Vector2 screenPoint)
        {
            if (m_SubscribeMessageButton == null || !m_SubscribeMessageButton.gameObject.activeInHierarchy ||
                !m_SubscribeMessageButton.IsInteractable() || EventSystem.current == null) return false;
            m_SubscribeRaycasts.Clear();
            var pointer = new PointerEventData(EventSystem.current) { position = screenPoint };
            EventSystem.current.RaycastAll(pointer, m_SubscribeRaycasts);
            if (m_SubscribeRaycasts.Count == 0) return false;
            RaycastResult first = m_SubscribeRaycasts[0];
            if (!(first.module is GraphicRaycaster)) return false;
            Transform target = first.gameObject.transform;
            return target == m_SubscribeMessageButton.transform || target.IsChildOf(m_SubscribeMessageButton.transform);
        }

        /// <summary>保留原生回调调用栈直到微信授权已拉起，异步返回后解除并发保护。</summary>
        private async UniTask RequestNativeSubscriptionAsync()
        {
            m_Subscribing = true;
            var session = m_SessionCancellation;
            try { await RequestSubscribeMessageAsync(); }
            finally { if (ReferenceEquals(session, m_SessionCancellation)) m_Subscribing = false; }
        }

        /// <summary>预加载激励视频，完成后由另一次点击触发 Show，避免跨帧用户手势。</summary>
        private async void OnLoadRewardedClick()
        {
            if (!TryGetWechat(out WeChatMiniGamePlugin plugin) || m_Advertising || m_RewardedLoading) return;
            m_RewardedLoading = true;
            CancellationToken ct = SessionToken;
            var session = m_SessionCancellation;
            try
            {
                if (m_Rewarded != null) { m_Rewarded.Shown -= OnRewardedShown; m_Rewarded.Dispose(); }
                m_RewardedLoaded = false;
                m_Rewarded = plugin.CreateRewardedVideoAd(ResolveDemoAdUnit(plugin, WeChatMiniGameAdKind.RewardedVideo, m_DemoMaterials.RewardedPlacement));
                m_Rewarded.Shown += OnRewardedShown;
                var ad = m_Rewarded;
                await ad.LoadAsync(ct);
                if (ct.IsCancellationRequested || !ReferenceEquals(session, m_SessionCancellation) || !ReferenceEquals(ad, m_Rewarded)) return;
                m_RewardedLoaded = true;
                AppendFeedback("激励视频已加载，请点击显示；完整播放才满足演示奖励条件。", FeedbackLevel.Success);
            }
            catch (Exception exception) { if (ReferenceEquals(session, m_SessionCancellation)) ReportFailure("加载激励视频，请检查 ConfigWindow 的广告位", exception); }
            finally { if (ReferenceEquals(session, m_SessionCancellation)) m_RewardedLoading = false; }
        }

        /// <summary>用户点击后显示激励视频，区分完整播放与提前关闭，不伪造广告点击。</summary>
        private async void OnShowRewardedClick()
        {
            if (!TryGetWechat(out WeChatMiniGamePlugin plugin) || m_Advertising) return;
            if (!m_RewardedLoaded) { AppendFeedback("请先加载激励视频。", FeedbackLevel.Warn); return; }
            m_Advertising = true;
            var ad = m_Rewarded;
            var session = m_SessionCancellation;
            CancellationToken ct = SessionToken;
            try
            {
                TrackDemoAdIntent(plugin, "reward", WeChatMiniGameAdKind.RewardedVideo, m_DemoMaterials.RewardedPlacement);
                WeChatMiniGameRewardedAdResult result = await ad.ShowAsync(plugin.CaptureUserGesture(), ct);
                if (ct.IsCancellationRequested || !ReferenceEquals(session, m_SessionCancellation) || !ReferenceEquals(ad, m_Rewarded)) return;
                AppendFeedback(result.Completed ? "激励视频完整播放，可进入业务奖励校验。" : "激励视频提前关闭，不发奖励。", FeedbackLevel.Info);
            }
            catch (Exception exception) { if (ReferenceEquals(session, m_SessionCancellation)) ReportFailure("显示激励视频", exception); }
            finally { if (ReferenceEquals(session, m_SessionCancellation)) { m_Advertising = false; m_RewardedLoaded = false; } }
        }

        /// <summary>预加载插屏，显示在单独的用户点击中触发。</summary>
        private async void OnLoadInterstitialClick()
        {
            if (!TryGetWechat(out WeChatMiniGamePlugin plugin) || m_Advertising || m_InterstitialLoading) return;
            m_InterstitialLoading = true;
            CancellationToken ct = SessionToken;
            var session = m_SessionCancellation;
            try
            {
                if (m_Interstitial != null) { m_Interstitial.Shown -= OnInterstitialShown; m_Interstitial.Dispose(); }
                m_InterstitialLoaded = false;
                m_Interstitial = plugin.CreateInterstitialAd(ResolveDemoAdUnit(plugin, WeChatMiniGameAdKind.Interstitial, m_DemoMaterials.InterstitialPlacement));
                m_Interstitial.Shown += OnInterstitialShown;
                var ad = m_Interstitial;
                await ad.LoadAsync(ct);
                if (ct.IsCancellationRequested || !ReferenceEquals(session, m_SessionCancellation) || !ReferenceEquals(ad, m_Interstitial)) return;
                m_InterstitialLoaded = true;
                AppendFeedback("插屏已加载，请点击显示。", FeedbackLevel.Success);
            }
            catch (Exception exception) { if (ReferenceEquals(session, m_SessionCancellation)) ReportFailure("加载插屏，请检查 ConfigWindow 的广告位", exception); }
            finally { if (ReferenceEquals(session, m_SessionCancellation)) m_InterstitialLoading = false; }
        }

        /// <summary>显示插屏并等待真实关闭回调，关闭不等于奖励。</summary>
        private async void OnShowInterstitialClick()
        {
            if (!TryGetWechat(out WeChatMiniGamePlugin plugin) || m_Advertising) return;
            if (!m_InterstitialLoaded) { AppendFeedback("请先加载插屏。", FeedbackLevel.Warn); return; }
            m_Advertising = true;
            var ad = m_Interstitial;
            var session = m_SessionCancellation;
            CancellationToken ct = SessionToken;
            try
            {
                TrackDemoAdIntent(plugin, "interstitial", WeChatMiniGameAdKind.Interstitial, m_DemoMaterials.InterstitialPlacement);
                await ad.ShowAsync(plugin.CaptureUserGesture(), ct);
                if (ct.IsCancellationRequested || !ReferenceEquals(session, m_SessionCancellation) || !ReferenceEquals(ad, m_Interstitial)) return;
                AppendFeedback("插屏已收到微信关闭回调。", FeedbackLevel.Success);
            }
            catch (Exception exception) { if (ReferenceEquals(session, m_SessionCancellation)) ReportFailure("显示插屏", exception); }
            finally { if (ReferenceEquals(session, m_SessionCancellation)) { m_Advertising = false; m_InterstitialLoaded = false; } }
        }

        /// <summary>创建底部居中 Banner，报告首次加载、尺寸变化与成功显示。</summary>
        private async void OnShowBannerClick()
        {
            if (!TryGetWechat(out WeChatMiniGamePlugin plugin) || m_BannerLoading || m_BannerShown) return;
            m_BannerLoading = true;
            var session = m_SessionCancellation;
            CancellationToken ct = SessionToken;
            try
            {
                if (m_Banner == null)
                {
                    m_Banner = plugin.CreateBottomCenteredBannerAd(ResolveDemoAdUnit(plugin, WeChatMiniGameAdKind.Banner, m_DemoMaterials.BannerPlacement));
                    m_Banner.Resized += OnBannerResized;
                }
                var ad = m_Banner;
                if (!m_BannerLoaded)
                {
                    await ad.LoadAsync(ct);
                    if (ct.IsCancellationRequested || !ReferenceEquals(session, m_SessionCancellation) || !ReferenceEquals(ad, m_Banner)) return;
                    m_BannerLoaded = true;
                    AppendFeedback("Banner 已完成首次加载。", FeedbackLevel.Info);
                }
                await ad.ShowAsync(ct);
                if (ct.IsCancellationRequested || !ReferenceEquals(session, m_SessionCancellation) || !ReferenceEquals(ad, m_Banner)) return;
                m_BannerShown = true;
                TrackDemoAdIntent(plugin, "banner", WeChatMiniGameAdKind.Banner, m_DemoMaterials.BannerPlacement);
                AppendFeedback("Banner 已显示于底部居中。", FeedbackLevel.Success);
            }
            catch (Exception exception) { if (ReferenceEquals(session, m_SessionCancellation)) ReportFailure("显示 Banner，请检查 ConfigWindow 的广告位", exception); }
            finally { if (ReferenceEquals(session, m_SessionCancellation)) m_BannerLoading = false; }
        }

        /// <summary>回显原厂返回的 Banner 实际尺寸。</summary>
        private void OnBannerResized(WeChatMiniGameBannerAdSize size) =>
            AppendFeedback($"Banner 实际尺寸 {size.Width}×{size.Height}。", FeedbackLevel.Info);

        /// <summary>隐藏 Banner，保留对象供再次显示。</summary>
        private void OnHideBannerClick()
        {
            if (m_BannerLoading) return;
            m_Banner?.Hide();
            m_BannerShown = false;
            AppendFeedback("Banner 已隐藏。", FeedbackLevel.Info);
        }

        /// <summary>微信激励广告显示成功后上报真实展示，不将提前关闭或失败当作展示。</summary>
        private void OnRewardedShown() => TrackDemoAdShown("reward", WeChatMiniGameAdKind.RewardedVideo, m_DemoMaterials.RewardedPlacement);

        /// <summary>微信插屏显示成功后上报真实展示。</summary>
        private void OnInterstitialShown() => TrackDemoAdShown("interstitial", WeChatMiniGameAdKind.Interstitial, m_DemoMaterials.InterstitialPlacement);

        /// <summary>展示成功时接入引力广告展示；统计异常不影响广告结果。</summary>
        private void TrackDemoAdShown(string type, WeChatMiniGameAdKind kind, string placement)
        {
            if (m_Wechat == null || !m_Wechat.IsGravityInitialized) return;
            try { m_Wechat.TrackGravityAdShow(type, ResolveDemoAdUnit(m_Wechat, kind, placement)); }
            catch (Exception exception) { AppendFeedback("广告展示统计失败：" + exception.Message, FeedbackLevel.Warn); }
        }

        /// <summary>上报演示入口操作，不能伪造原厂未提供回调的素材点击事件。</summary>
        private void TrackDemoAdIntent(WeChatMiniGamePlugin plugin, string type, WeChatMiniGameAdKind kind, string placement)
        {
            if (!plugin.IsGravityInitialized) { AppendFeedback("引力未初始化，本次广告统计跳过。", FeedbackLevel.Warn); return; }
            try
            {
                plugin.TrackGravityEvent("nova_demo_wx_ad_request", new Dictionary<string, object>
                { ["ad_type"] = type, ["placement"] = placement, ["ad_unit_id"] = ResolveDemoAdUnit(plugin, kind, placement) });
                if (kind == WeChatMiniGameAdKind.Banner)
                    plugin.TrackGravityAdShow(type, ResolveDemoAdUnit(plugin, kind, placement));
                AppendFeedback("已上报广告入口操作；素材点击只能来自微信真实回调。", FeedbackLevel.Info);
            }
            catch (Exception exception) { AppendFeedback("广告统计失败，不影响显示：" + exception.Message, FeedbackLevel.Warn); }
        }

        /// <summary>优先读取仅演示广告 ID，其次查正式位置配置，未配置时给出可操作提示。</summary>
        private string ResolveDemoAdUnit(WeChatMiniGamePlugin plugin, WeChatMiniGameAdKind kind, string placement)
        {
            string id = kind == WeChatMiniGameAdKind.RewardedVideo ? m_DemoMaterials.RewardedAdUnitId
                : kind == WeChatMiniGameAdKind.Interstitial ? m_DemoMaterials.InterstitialAdUnitId
                : m_DemoMaterials.BannerAdUnitId;
            if (!string.IsNullOrWhiteSpace(id)) return id;
            if (plugin.TryGetAdUnitId(kind, placement, out id)) return id;
            throw new InvalidOperationException("请在 DemoWechatMiniGameView Inspector 的 Demo Materials 中填写 " + kind + " 广告位 ID，或配置正式业务位置 " + placement + "。");
        }

        /// <summary>关闭页面时注销广告事件并释放本页面全部广告对象。</summary>
        private void DisposeDemoAds()
        {
            if (m_Rewarded != null) { m_Rewarded.Shown -= OnRewardedShown; m_Rewarded.Dispose(); m_Rewarded = null; }
            if (m_Interstitial != null) { m_Interstitial.Shown -= OnInterstitialShown; m_Interstitial.Dispose(); m_Interstitial = null; }
            if (m_Banner != null) { m_Banner.Resized -= OnBannerResized; m_Banner.Dispose(); m_Banner = null; }
            m_RewardedLoaded = m_InterstitialLoaded = m_Advertising = false;
            m_RewardedLoading = m_InterstitialLoading = m_BannerLoading = m_BannerLoaded = m_BannerShown = false;
        }
    }
}
