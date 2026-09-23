/***************************************************************
 * (c) copyright 2026 - 2030, Solotopia
 * All Rights Reserved.
 * -------------------------------------------------------------
 * filename:  DemoWechatMiniGameView.Methods.cs
 * author:    nova-create-sample
 * created:   2026/09/20
 * descrip:   DemoWechatMiniGameView 演示 View — 私有方法
 ***************************************************************/

using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using Cysharp.Threading.Tasks;
using NovaFramework.Kit.Network.GameBind.Runtime;
using NovaFramework.Kit.Network.GameLogin.Runtime;
using NovaFramework.Runtime;
using NovaFramework.SDK.WeChatMiniGame.Runtime;
using TMPro;
using UnityEngine.Events;
using UnityEngine.UI;

namespace NovaFramework.Sdk.Wechat.Minigame.Samples.Runtime
{
    /// <summary>
    /// DemoWechatMiniGameView 演示 View 的私有方法。
    /// </summary>
    public sealed partial class DemoWechatMiniGameView
    {
        /// <summary>绑定按钮事件，并在按钮提示区显示对应的 Plugin API。</summary>
        /// <param name="button">需要绑定的按钮。</param>
        /// <param name="callback">按钮点击回调。</param>
        /// <param name="label">按钮显示文本。</param>
        /// <param name="apiHint">按钮对应的公开 API 提示。</param>
        private void BindButton(Button button, UnityAction callback, string label, string apiHint)
        {
            if (button == null)
            {
                return;
            }
            TMP_Text labelText = button.transform.Find("Text")?.GetComponent<TMP_Text>();
            if (labelText != null)
            {
                labelText.text = label;
            }
            button.onClick.RemoveAllListeners();
            button.onClick.AddListener(callback);
            SetButtonApiHint(button, apiHint);
        }

        private bool TryGetWechat(out WeChatMiniGamePlugin plugin)
        {
            if (Nova.SDK.TryGet(out plugin) && plugin != null && plugin.IsAvailable)
            {
                m_Wechat = plugin;
                return true;
            }
            AppendFeedback("当前不是微信小游戏运行环境，因此不会调用微信能力。请导出微信小游戏后，在微信开发者工具或真机中体验。", FeedbackLevel.Warn);
            return false;
        }

        private void BeginSession()
        {
            EndSession();
            m_SessionCancellation = new CancellationTokenSource();
            if (!TryGetWechat(out WeChatMiniGamePlugin plugin))
            {
                return;
            }
            plugin.Shown += OnWechatShown;
            plugin.Hidden += OnWechatHidden;
            plugin.MemoryWarning += OnWechatMemoryWarning;
            plugin.SetPaymentFulfillmentHandler(this);
        }

        private void EndSession()
        {
            if (m_Wechat != null)
            {
                m_Wechat.Shown -= OnWechatShown;
                m_Wechat.Hidden -= OnWechatHidden;
                m_Wechat.MemoryWarning -= OnWechatMemoryWarning;
                m_Wechat = null;
            }
            m_LastOrderId = null;
            m_WechatOpenId = null;
            m_WechatUnionId = null;
            m_SessionCancellation?.Cancel();
            m_SessionCancellation?.Dispose();
            m_SessionCancellation = null;
        }

        private CancellationToken SessionToken => m_SessionCancellation?.Token ?? default;

        private void OnWechatShown(WeChatMiniGameLaunchContext context)
        {
            AppendFeedback($"小游戏已回到前台：入口场景 {context?.Scene ?? 0}，携带启动参数 {context?.Query?.Count ?? 0} 项。", FeedbackLevel.Info);
        }

        private void OnWechatHidden()
        {
            AppendFeedback("小游戏已进入后台。", FeedbackLevel.Info);
        }

        private void OnWechatMemoryWarning(int level)
        {
            AppendFeedback($"微信提示可用内存不足（等级 {level}），游戏应释放暂时不用的资源。", FeedbackLevel.Warn);
        }

        private void OnRuntimeInfoClick()
        {
            if (!TryGetWechat(out WeChatMiniGamePlugin plugin))
            {
                return;
            }
            WeChatMiniGameRuntimeInfo info = plugin.RuntimeInfo;
            AppendFeedback(
                $"基础库 {info?.SdkVersion ?? "未知"}，设备 {info?.Model ?? "未知"}，系统 {info?.Platform ?? "未知"}，" +
                $"屏幕 {info?.ScreenWidth ?? 0}×{info?.ScreenHeight ?? 0}。",
                FeedbackLevel.Success);
        }

        private async void OnGameLoginClick()
        {
            if (string.IsNullOrWhiteSpace(m_WechatOpenId))
            {
                AppendFeedback("请先完成微信登录校验，获取已校验的 OpenID。", FeedbackLevel.Warn);
                return;
            }

            try
            {
                Login login = Nova.Network.Kit<Login>();
                NetResponse<PbNetLoginResp> response = await login.Async(string.Empty, m_WechatOpenId, false);
                if (response.IsSuccess && response.Data != null)
                {
                    AppendFeedback($"已通过微信 OpenID 登录已绑定的游戏账号，UID={Mask(response.Data.Uid)}。", FeedbackLevel.Success);
                    return;
                }

                if (response.ErrorCode == LoginErrorCode.ErrAccountNotFound)
                {
                    await LoginGuestAndBindWechatAsync(login);
                    return;
                }

                AppendFeedback(
                    $"微信 OpenID 登录游戏服务器失败：错误码 {response.ErrorCode}，{response.ErrorMessage}",
                    FeedbackLevel.Error);
            }
            catch (Exception exception)
            {
                ReportFailure("登录游戏服务器", exception);
            }
        }

        private async void OnWechatLoginClick()
        {
            if (!TryGetWechat(out WeChatMiniGamePlugin plugin))
            {
                return;
            }
            try
            {
                WeChatMiniGameLoginVerificationResult result =
                    await plugin.LoginAndVerifyAsync(SessionToken);
                if (result == null)
                {
                    throw new InvalidOperationException("业务服务端未返回有效的微信身份。");
                }

                m_WechatOpenId = result.OpenId;
                m_WechatUnionId = result.UnionId;
                AppendFeedback(
                    $"微信 code 校验成功，OpenID={Mask(m_WechatOpenId)}" +
                    (string.IsNullOrWhiteSpace(m_WechatUnionId) ? "。" : $"，UnionID={Mask(m_WechatUnionId)}。"),
                    FeedbackLevel.Success);
            }
            catch (Exception exception)
            {
                ReportFailure("微信登录校验", exception);
            }
        }

        /// <summary>微信 OpenID 未绑定时，先按 TGA DeviceID 登录游客账号，再绑定微信身份。</summary>
        private async UniTask LoginGuestAndBindWechatAsync(Login login)
        {
            AppendFeedback("当前 OpenID 尚未绑定，正在通过 TGA DeviceID 登录游客账号。", FeedbackLevel.Info);
            NetResponse<PbNetLoginResp> guestResponse =
                await login.Async(string.Empty, string.Empty, false);
            if (!guestResponse.IsSuccess || guestResponse.Data == null)
            {
                AppendFeedback(
                    $"游客账号登录失败：错误码 {guestResponse.ErrorCode}，{guestResponse.ErrorMessage}",
                    FeedbackLevel.Error);
                return;
            }

            AppendFeedback($"游客账号登录成功，UID={Mask(guestResponse.Data.Uid)}，开始绑定微信 OpenID。", FeedbackLevel.Info);
            NetResponse<PbNetBindResp> bindResponse = await Nova.Network.Kit<Bind>()
                .BindAsync(ThirdLoginProvider.Wechat, m_WechatOpenId);
            if (bindResponse.IsSuccess)
            {
                AppendFeedback("微信 OpenID 已绑定到当前游客账号。", FeedbackLevel.Success);
                return;
            }

            if (bindResponse.ErrorCode == BindErrorCode.ErrBindConflict)
            {
                string existingUid = bindResponse.Data?.ExistingUid ?? string.Empty;
                AppendFeedback($"微信绑定冲突：existing_uid={Mask(existingUid)}。", FeedbackLevel.Warn);
                AppendFeedback("业务层应继续调用 QueryConflictAsync + ResolveAsync，由玩家选择保留游客账号或已有账号。", FeedbackLevel.Info);
                return;
            }

            AppendFeedback(
                $"微信 OpenID 绑定失败：错误码 {bindResponse.ErrorCode}，{bindResponse.ErrorMessage}",
                FeedbackLevel.Error);
        }

        private async void OnPrivacyStatusClick()
        {
            if (!TryGetWechat(out WeChatMiniGamePlugin plugin))
            {
                return;
            }
            try
            {
                WeChatMiniGamePrivacyStatus status = await plugin.GetPrivacyStatusAsync(SessionToken);
                AppendFeedback(
                    status.NeedsAuthorization
                        ? $"当前需要玩家确认隐私授权，协议名称：{status.ContractName}。"
                        : "当前无需再次请求隐私授权。",
                    FeedbackLevel.Success);
            }
            catch (Exception exception)
            {
                ReportFailure("查询隐私状态", exception);
            }
        }

        private async void OnPrivacyAuthorizeClick()
        {
            if (!TryGetWechat(out WeChatMiniGamePlugin plugin))
            {
                return;
            }
            try
            {
                WeChatMiniGameUserGestureToken gesture = plugin.CaptureUserGesture();
                await plugin.RequirePrivacyAuthorizationAsync(gesture, SessionToken);
                AppendFeedback("隐私授权请求已完成。拒绝时不会阻断与隐私无关的主流程。", FeedbackLevel.Success);
            }
            catch (Exception exception)
            {
                ReportFailure("请求隐私授权", exception);
            }
        }

        private async void OnReadClipboardClick()
        {
            if (!TryGetWechat(out WeChatMiniGamePlugin plugin))
            {
                return;
            }
            try
            {
                string text = await plugin.GetClipboardTextAsync(SessionToken);
                AppendFeedback($"读取剪贴板 → {text}", FeedbackLevel.Success);
            }
            catch (Exception exception)
            {
                ReportFailure("读取剪贴板", exception);
            }
        }

        private async void OnWriteClipboardClick()
        {
            if (!TryGetWechat(out WeChatMiniGamePlugin plugin))
            {
                return;
            }
            try
            {
                WeChatMiniGameUserGestureToken gesture = plugin.CaptureUserGesture();
                await plugin.SetClipboardTextAsync("Nova WeChat Mini Game", gesture, SessionToken);
                AppendFeedback("已写入剪贴板：Nova WeChat Mini Game", FeedbackLevel.Success);
            }
            catch (Exception exception)
            {
                ReportFailure("写入剪贴板", exception);
            }
        }

        private async void OnVibrateClick()
        {
            if (!TryGetWechat(out WeChatMiniGamePlugin plugin))
            {
                return;
            }
            try
            {
                await plugin.VibrateShortAsync(WeChatMiniGameVibrationStrength.Medium, SessionToken);
                AppendFeedback("已触发中等强度短振动。", FeedbackLevel.Success);
            }
            catch (Exception exception)
            {
                ReportFailure("短振动", exception);
            }
        }

        private void OnShareClick()
        {
            if (!TryGetWechat(out WeChatMiniGamePlugin plugin))
            {
                return;
            }
            try
            {
                WeChatMiniGameUserGestureToken gesture = plugin.CaptureUserGesture();
                plugin.ShareAppMessage(new WeChatMiniGameShareRequest
                {
                    Title = "Nova 微信小游戏演示",
                    Query = "source=nova-demo"
                }, gesture);
                AppendFeedback("已拉起主动分享；平台不提供可靠完成证明，禁止据此直接发奖。", FeedbackLevel.Info);
            }
            catch (Exception exception)
            {
                ReportFailure("主动分享", exception);
            }
        }

        private async void OnPaymentSupportClick()
        {
            if (!TryGetWechat(out WeChatMiniGamePlugin plugin))
            {
                return;
            }
            try
            {
                WeChatMiniGamePaymentSupport support = await plugin.CheckPaymentSupportAsync(SessionToken);
                AppendFeedback(
                    support.IsSupported ? "当前环境支持微信虚拟支付。" : $"当前环境不支持微信虚拟支付：{support.Reason}",
                    support.IsSupported ? FeedbackLevel.Success : FeedbackLevel.Warn);
            }
            catch (Exception exception)
            {
                ReportFailure("检查支付能力", exception);
            }
        }

        private void OnPayCurrencyClick()
        {
            PurchaseAndVerifyAsync(
                new WeChatMiniGamePaymentIntent(WeChatMiniGamePaymentKind.GameCurrency, CurrencyProductId, 60),
                "游戏币").Forget();
        }

        private void OnPayItemClick()
        {
            PurchaseAndVerifyAsync(
                new WeChatMiniGamePaymentIntent(WeChatMiniGamePaymentKind.GameItem, ItemProductId, 1, 100),
                "道具").Forget();
        }

        private async UniTask PurchaseAndVerifyAsync(WeChatMiniGamePaymentIntent intent, string label)
        {
            if (!TryGetWechat(out WeChatMiniGamePlugin plugin))
            {
                return;
            }
            try
            {
                WeChatMiniGamePurchaseResult result = await plugin.PurchaseAndVerifyAsync(intent, SessionToken);
                m_LastOrderId = result.LaunchResult.OrderId;
                AppendFeedback($"{label}支付窗口结果：{DescribeLaunchStatus(result.LaunchResult.Status)}。", FeedbackLevel.Info);
                AppendVerificationResult($"{label}支付验单", result.VerificationResult, result.Delivered);
            }
            catch (Exception exception)
            {
                ReportFailure($"{label}支付", exception);
            }
        }

        private async void OnQueryOrderClick()
        {
            if (!TryGetWechat(out WeChatMiniGamePlugin plugin))
            {
                return;
            }
            try
            {
                IReadOnlyList<WeChatMiniGamePaymentOrderInfo> orders =
                    await plugin.QueryCurrentUserPaymentOrdersAsync(SessionToken);
                AppendFeedback($"服务端返回当前用户 {orders?.Count ?? 0} 笔支付订单。", FeedbackLevel.Success);
            }
            catch (Exception exception)
            {
                ReportFailure("订单查询", exception);
            }
        }

        private async void OnVerifyOrderClick()
        {
            if (!TryGetOrderOperation(out WeChatMiniGamePlugin plugin))
            {
                return;
            }
            try
            {
                WeChatMiniGamePaymentVerificationResult result =
                    await plugin.VerifyPaymentOrderAsync(m_LastOrderId, SessionToken);
                AppendVerificationResult(
                    "单笔验单",
                    result,
                    plugin.GetPendingPaymentOrder(m_LastOrderId) == null && result.CanDeliver);
            }
            catch (Exception exception)
            {
                ReportFailure("单笔验单", exception);
            }
        }

        private async void OnVerifyAllOrdersClick()
        {
            if (!TryGetWechat(out WeChatMiniGamePlugin plugin))
            {
                return;
            }
            try
            {
                IReadOnlyList<WeChatMiniGamePaymentVerificationResult> results =
                    await plugin.VerifyAllPaymentOrdersAsync(SessionToken);
                int deliverable = results?.Count(item => item != null && item.CanDeliver) ?? 0;
                AppendFeedback($"本地补单完成：验证 {results?.Count ?? 0} 笔，可发货 {deliverable} 笔。", FeedbackLevel.Success);
            }
            catch (Exception exception)
            {
                ReportFailure("全部订单验证", exception);
            }
        }

        private async void OnSubscribeMessageClick()
        {
            if (!TryGetWechat(out WeChatMiniGamePlugin plugin))
            {
                return;
            }
            if (SubscriptionTemplateIds == null || SubscriptionTemplateIds.Length == 0)
            {
                AppendFeedback("尚未配置消息模板，暂时无法请求订阅。请先按照本示例 README 完成消息提醒配置。", FeedbackLevel.Warn);
                return;
            }
            try
            {
                WeChatMiniGameUserGestureToken gesture = plugin.CaptureUserGesture();
                IReadOnlyDictionary<string, string> results = await plugin.RequestSubscribeMessagesAsync(
                    SubscriptionTemplateIds,
                    gesture,
                    SessionToken);
                string summary = string.Join("，", results.Select(item => $"模板 {Mask(item.Key)}：{DescribeSubscriptionResult(item.Value)}"));
                AppendFeedback($"消息订阅结果已保存到游戏服务器：{summary}", FeedbackLevel.Success);
            }
            catch (Exception exception)
            {
                ReportFailure("请求消息订阅", exception);
            }
        }

        /// <summary>取得微信 Plugin，并确认当前会话存在可手动验单的最近订单号。</summary>
        /// <param name="plugin">当前已初始化的微信小游戏 Plugin。</param>
        /// <returns>Plugin 可用且存在最近订单号时返回 true。</returns>
        private bool TryGetOrderOperation(out WeChatMiniGamePlugin plugin)
        {
            if (!TryGetWechat(out plugin))
            {
                return false;
            }
            if (!string.IsNullOrWhiteSpace(m_LastOrderId))
            {
                return true;
            }
            AppendFeedback("当前还没有可查询的订单，请先购买一次游戏币或道具。", FeedbackLevel.Warn);
            return false;
        }

        private void AppendVerificationResult(string action, WeChatMiniGamePaymentVerificationResult result, bool delivered)
        {
            if (result == null)
            {
                AppendFeedback($"{action}未返回订单。", FeedbackLevel.Warn);
                return;
            }
            m_LastOrderId = result.OrderId;
            AppendFeedback(
                $"{action}：订单 {Mask(result.OrderId)}，服务端状态为“{DescribeVerificationStatus(result.Status)}”，" +
                $"{(delivered ? "客户端发货已完成" : result.CanDeliver ? "等待客户端发货重试" : "尚不可发货")}。",
                delivered ? FeedbackLevel.Success : FeedbackLevel.Info);
        }

        private static string DescribeLaunchStatus(WeChatMiniGamePaymentLaunchStatus status)
        {
            return status switch
            {
                WeChatMiniGamePaymentLaunchStatus.Accepted => "支付操作已提交",
                WeChatMiniGamePaymentLaunchStatus.Cancelled => "玩家已取消",
                WeChatMiniGamePaymentLaunchStatus.Unsupported => "当前设备不支持",
                _ => "结果暂未确认"
            };
        }

        private static string DescribeVerificationStatus(WeChatMiniGamePaymentVerificationStatus status)
        {
            return status switch
            {
                WeChatMiniGamePaymentVerificationStatus.Pending => "处理中",
                WeChatMiniGamePaymentVerificationStatus.Paid => "已支付",
                WeChatMiniGamePaymentVerificationStatus.Closed => "已关闭",
                WeChatMiniGamePaymentVerificationStatus.Refunded => "已退款",
                WeChatMiniGamePaymentVerificationStatus.Failed => "支付失败",
                _ => "未知"
            };
        }

        /// <summary>Demo 不增加真实资产，仅展示项目侧按订单号幂等发货的接入位置。</summary>
        public UniTask<bool> DeliverAsync(WeChatMiniGamePaymentDelivery delivery, CancellationToken ct = default)
        {
            AppendFeedback($"Demo 发货处理器收到订单 {Mask(delivery.Order.OrderId)}，实际项目必须在资产存档中按订单号幂等发货。", FeedbackLevel.Success);
            return UniTask.FromResult(true);
        }

        private static string DescribeSubscriptionResult(string result)
        {
            return result switch
            {
                "accept" => "已允许",
                "reject" => "已拒绝",
                "ban" => "已被系统禁止",
                "filter" => "模板受限",
                _ => "结果未知"
            };
        }

        private static string Mask(string value)
        {
            if (string.IsNullOrEmpty(value))
            {
                return "?";
            }
            return value.Length <= 6 ? "***" : $"{value.Substring(0, 3)}***{value.Substring(value.Length - 3)}";
        }

        /// <summary>把取消与真实失败按不同等级回显。</summary>
        private void ReportFailure(string actionName, Exception exception)
        {
            if (exception is OperationCanceledException)
            {
                AppendFeedback($"{actionName}已取消。", FeedbackLevel.Warn);
                return;
            }
            AppendFeedback($"{actionName}失败：{exception.Message}", FeedbackLevel.Error);
        }
    }
}
