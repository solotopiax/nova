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
using UnityEngine;
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

        /// <summary>创建订阅和文本安全演示表单，并紧邻对应操作按钮排列。</summary>
        private void CreateDemoFormControls()
        {
            if (m_SubscriptionTemplateIdInput != null && m_NoticeConfigIdInput != null &&
                m_NoticeTriggerTimeInput != null && m_NoticeClientTaskIdInput != null &&
                m_TextSecurityContentInput != null && m_CheckTextSecurityButton != null)
            {
                SetDemoFormInitialValues();
                BindButton(m_CheckTextSecurityButton, OnCheckTextSecurityClick,
                    "检查文本安全", "WeChatMiniGamePlugin.CheckTextContentAsync()");
                return;
            }

            if (m_InteractionRoot == null || m_SubscribeMessageButton == null || m_TitleText == null)
            {
                AppendFeedback("演示表单缺少 Prefab 绑定，无法创建输入框。", FeedbackLevel.Error);
                return;
            }

            int insertIndex = m_SubscribeMessageButton.transform.GetSiblingIndex();
            m_SubscriptionTemplateIdInput = CreateFormInput(
                "订阅模板 ID", "微信公众平台申请的模板 ID", FirstTemplateId(), ref insertIndex);
            m_NoticeConfigIdInput = CreateFormInput(
                "服务端通知配置 ID", "服务端已配置的通知配置 ID", NoticeConfigId, ref insertIndex);
            m_NoticeTriggerTimeInput = CreateFormInput(
                "通知触发时间（Unix 秒）", "输入 0 表示立即发送", "0", ref insertIndex);
            m_NoticeClientTaskIdInput = CreateFormInput(
                "通知任务幂等键", "重试时保持相同的任务键", NoticeClientTaskId, ref insertIndex);

            insertIndex = m_SubscribeMessageButton.transform.GetSiblingIndex() + 1;
            m_TextSecurityContentInput = CreateFormInput(
                "待检查文本", "输入需要检查的文本内容", string.Empty, ref insertIndex);
            m_CheckTextSecurityButton = Instantiate(m_SubscribeMessageButton, m_InteractionRoot);
            m_CheckTextSecurityButton.gameObject.name = "CheckTextSecurityButton";
            m_CheckTextSecurityButton.transform.SetSiblingIndex(insertIndex);
            BindButton(m_CheckTextSecurityButton, OnCheckTextSecurityClick,
                "检查文本安全", "WeChatMiniGamePlugin.CheckTextContentAsync()");
        }

        private void SetDemoFormInitialValues()
        {
            if (string.IsNullOrEmpty(m_SubscriptionTemplateIdInput.text))
            {
                m_SubscriptionTemplateIdInput.text = FirstTemplateId();
            }
            if (string.IsNullOrEmpty(m_NoticeConfigIdInput.text))
            {
                m_NoticeConfigIdInput.text = NoticeConfigId;
            }
            if (string.IsNullOrEmpty(m_NoticeTriggerTimeInput.text))
            {
                m_NoticeTriggerTimeInput.text = "0";
            }
            if (string.IsNullOrEmpty(m_NoticeClientTaskIdInput.text))
            {
                m_NoticeClientTaskIdInput.text = NoticeClientTaskId;
            }
        }

        private string FirstTemplateId()
        {
            return SubscriptionTemplateIds != null && SubscriptionTemplateIds.Length > 0
                ? SubscriptionTemplateIds[0]
                : string.Empty;
        }

        private TMP_InputField CreateFormInput(
            string title, string hint, string value, ref int siblingIndex)
        {
            var fieldObject = new GameObject(title, typeof(RectTransform));
            fieldObject.transform.SetParent(m_InteractionRoot, false);
            fieldObject.transform.SetSiblingIndex(siblingIndex++);
            fieldObject.AddComponent<LayoutElement>().minHeight = 132f;

            TextMeshProUGUI label = CreateFormText(fieldObject.transform, "Title", title, 25f, Color.white);
            SetAnchors(label.rectTransform, new Vector2(0f, 0.62f), Vector2.one, 8f, -8f);

            var inputObject = new GameObject("Input", typeof(RectTransform));
            inputObject.transform.SetParent(fieldObject.transform, false);
            var inputRect = (RectTransform)inputObject.transform;
            SetAnchors(inputRect, Vector2.zero, new Vector2(1f, 0.6f), 8f, -8f);
            Image background = inputObject.AddComponent<Image>();
            background.color = Color.white;
            TMP_InputField input = inputObject.AddComponent<TMP_InputField>();
            input.targetGraphic = background;
            input.lineType = TMP_InputField.LineType.SingleLine;

            TextMeshProUGUI placeholder = CreateFormText(inputRect, "Placeholder", hint, 23f,
                new Color(0.42f, 0.42f, 0.42f));
            SetAnchors(placeholder.rectTransform, Vector2.zero, Vector2.one, 12f, -12f);
            TextMeshProUGUI text = CreateFormText(inputRect, "Text", string.Empty, 23f, Color.black);
            SetAnchors(text.rectTransform, Vector2.zero, Vector2.one, 12f, -12f);
            input.textComponent = text;
            input.placeholder = placeholder;
            input.text = value ?? string.Empty;
            inputObject.AddComponent<WeChatMiniGameTmpInputBridge>();
            return input;
        }

        private TextMeshProUGUI CreateFormText(
            Transform parent, string name, string value, float fontSize, Color color)
        {
            var textObject = new GameObject(name, typeof(RectTransform));
            textObject.transform.SetParent(parent, false);
            TextMeshProUGUI result = textObject.AddComponent<TextMeshProUGUI>();
            result.font = m_TitleText.font;
            result.fontSize = fontSize;
            result.color = color;
            result.alignment = TextAlignmentOptions.MidlineLeft;
            result.raycastTarget = false;
            result.text = value;
            return result;
        }

        private static void SetAnchors(
            RectTransform rect, Vector2 min, Vector2 max, float leftInset, float rightInset)
        {
            rect.anchorMin = min;
            rect.anchorMax = max;
            rect.offsetMin = new Vector2(leftInset, 0f);
            rect.offsetMax = new Vector2(rightInset, 0f);
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

        /// <summary>使用已校验 OpenID 登录游戏账号；未绑定时登录游客并绑定微信。</summary>
        private async UniTask LoginGameServerAsync()
        {
            Login login = Nova.Network.Kit<Login>();
            NetResponse<PbNetLoginResp> response = await login.Async(string.Empty, m_WechatOpenId, false);
            if (response.IsSuccess && response.Data != null)
            {
                // 游戏服登录成功后同步设置本地支付与通知任务所属 UID。
                m_Wechat?.SetPaymentUserId(response.Data.Uid);
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
                if (plugin.IsGravityInitialized)
                {
                    AppendFeedback("引力引擎已使用校验后的微信 OpenID 启动。", FeedbackLevel.Info);
                }
                await LoginGameServerAsync();
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
                // 绑定完成后才允许当前游客 UID 发起支付和个人通知任务。
                m_Wechat?.SetPaymentUserId(guestResponse.Data.Uid);
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
                AppendFeedback($"{label}支付窗口结果：{DescribeLaunchStatus(result.LaunchResult.Status)}。", FeedbackLevel.Info);
                AppendVerificationResult($"{label}支付验单", result.VerificationResult, result.Delivered);
                if (result.Delivered && result.VerificationResult != null &&
                    intent.Kind == WeChatMiniGamePaymentKind.GameItem &&
                    plugin.IsGravityInitialized)
                {
                    try
                    {
                        plugin.TrackGravityPayment(
                            checked(intent.UnitPriceCents * intent.Quantity),
                            result.VerificationResult.OrderId,
                            intent.ProductId);
                        AppendFeedback("已向引力引擎上报本次已验单道具支付。", FeedbackLevel.Info);
                    }
                    catch (Exception gravityException)
                    {
                        AppendFeedback($"引力支付埋点失败，不影响订单发货：{gravityException.Message}", FeedbackLevel.Warn);
                    }
                }
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
                if (orders != null)
                {
                    foreach (WeChatMiniGamePaymentOrderInfo order in orders)
                    {
                        AppendFeedback(
                            $"订单 {order.OrderId}｜{order.Kind}｜商品 {order.ProductId} × {order.Quantity}｜" +
                            $"单价 {order.UnitPriceCents} 分｜状态 {DescribeVerificationStatus(order.Status)}｜" +
                            $"可发货 {(order.CanDeliver ? "是" : "否")}｜说明 {order.Message}｜" +
                            $"环境 {order.Environment}｜OfferId {order.OfferId}｜分区 {order.ZoneId}｜" +
                            $"透传 {order.Payload}｜创建 {order.CreatedAtUnixMilliseconds}｜" +
                            $"更新 {order.UpdatedAtUnixMilliseconds}。",
                            FeedbackLevel.Info);
                    }
                }
            }
            catch (Exception exception)
            {
                ReportFailure("订单查询", exception);
            }
        }

        private async void OnVerifyOrderClick()
        {
            if (!TryGetWechat(out WeChatMiniGamePlugin plugin))
            {
                return;
            }
            try
            {
                WeChatMiniGamePaymentOrderRecord order = plugin.GetPendingPaymentOrders()
                    .OrderByDescending(item => item.CreatedAtUnixMilliseconds)
                    .FirstOrDefault();
                if (order == null)
                {
                    AppendFeedback("当前账号没有本地待处理漏单。", FeedbackLevel.Warn);
                    return;
                }
                AppendFeedback($"手动补单选取最近创建的本地订单 {Mask(order.OrderId)}，不依赖本次页面会话。", FeedbackLevel.Info);
                WeChatMiniGamePaymentVerificationResult result =
                    await plugin.VerifyPaymentOrderAsync(order.OrderId, SessionToken);
                AppendVerificationResult(
                    "单笔验单",
                    result,
                    plugin.GetPendingPaymentOrder(order.OrderId) == null && result.CanDeliver);
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
            string templateId = m_SubscriptionTemplateIdInput?.text?.Trim();
            string configId = m_NoticeConfigIdInput?.text?.Trim();
            string clientTaskId = m_NoticeClientTaskIdInput?.text?.Trim();
            string triggerText = m_NoticeTriggerTimeInput?.text?.Trim();
            if (string.IsNullOrWhiteSpace(templateId) || string.IsNullOrWhiteSpace(configId) ||
                string.IsNullOrWhiteSpace(clientTaskId) || !long.TryParse(triggerText, out long triggerTime) ||
                triggerTime < 0)
            {
                AppendFeedback("请填写模板 ID、通知配置 ID、幂等键和非负 Unix 秒时间戳；0 表示立即。", FeedbackLevel.Warn);
                return;
            }
            try
            {
                WeChatMiniGameUserGestureToken gesture = plugin.CaptureUserGesture();
                WeChatMiniGameNoticeTaskResult task = await plugin.RequestSubscribeAndCreateNoticeAsync(
                    templateId, configId, triggerTime, clientTaskId, gesture, SessionToken);
                AppendFeedback(task == null
                    ? "微信未接受该模板授权，未创建通知任务。"
                    : $"服务端通知任务 {Mask(task.TaskId)} 已创建，当前状态 {task.Status}；实际送达以服务端发送结果为准。",
                    task == null ? FeedbackLevel.Warn : FeedbackLevel.Success);
            }
            catch (Exception exception)
            {
                ReportFailure("请求消息订阅", exception);
            }
        }

        private async void OnCheckTextSecurityClick()
        {
            if (!TryGetWechat(out WeChatMiniGamePlugin plugin))
            {
                return;
            }
            string content = m_TextSecurityContentInput?.text;
            if (string.IsNullOrWhiteSpace(content))
            {
                AppendFeedback("请输入需要检查的文本。", FeedbackLevel.Warn);
                return;
            }
            try
            {
                WeChatMiniGameTextSecurityCheckResult result = await plugin.CheckTextContentAsync(
                    content, WeChatMiniGameContentSecurityScene.Comment, SessionToken);
                AppendFeedback(
                    $"文本安全检查：{(result.IsAllowed ? "允许" : "不允许直接发布")}，" +
                    $"建议 {result.Suggestion}，标签 {result.Label}。",
                    result.IsAllowed ? FeedbackLevel.Success : FeedbackLevel.Warn);
            }
            catch (Exception exception)
            {
                ReportFailure("文本安全检查", exception);
            }
        }

        private void AppendVerificationResult(string action, WeChatMiniGamePaymentVerificationResult result, bool delivered)
        {
            if (result == null)
            {
                AppendFeedback($"{action}未返回订单。", FeedbackLevel.Warn);
                return;
            }
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

        /// <summary>Demo 以醒目的日志模拟增加资产并确认发货；真实项目仍须按订单号幂等落盘。</summary>
        public UniTask<bool> DeliverAsync(WeChatMiniGamePaymentDelivery delivery, CancellationToken ct = default)
        {
            AppendFeedback(
                $"【客户端已发货／模拟增加资产】{delivery.Order.Kind} 商品 {delivery.Order.ProductId} × {delivery.Order.Quantity}，" +
                $"订单 {Mask(delivery.Order.OrderId)}。Demo 不写真实资产；正式项目须按订单号幂等落盘。",
                FeedbackLevel.Success);
            return UniTask.FromResult(true);
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
