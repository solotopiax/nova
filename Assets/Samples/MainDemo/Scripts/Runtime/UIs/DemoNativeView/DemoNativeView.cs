/***************************************************************
 * (c) copyright 2026 - 2030, Solotopia
 * All Rights Reserved.
 * -------------------------------------------------------------
 * filename:  DemoNativeView.cs
 * author:    taoye
 * created:   2026/8/7
 * descrip:   Modules 2.17 — Native 通知权限演示 View
 ***************************************************************/

using System;
using System.Threading;
using Cysharp.Threading.Tasks;
using NovaFramework.Runtime;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace NovaFramework.Samples.Runtime
{
    /// <summary>
    /// Native 模块完整演示页：查询和请求通知权限、打开系统设置，以及测试 Android / iOS 应用内评价。
    /// 所有系统权限和应用内评价请求均由用户点击显式触发，页面打开时不会自动弹窗。
    /// </summary>
    public sealed class DemoNativeView : BaseDemoView
    {
        [SerializeField] private TextMeshProUGUI m_StatusText;
        [SerializeField] private TextMeshProUGUI m_InAppReviewStatusText;
        [SerializeField] private Button m_QueryButton;
        [SerializeField] private Button m_RequestStandardButton;
        [SerializeField] private Button m_RequestProvisionalButton;
        [SerializeField] private Button m_OpenSettingsButton;
        [SerializeField] private Button m_OpenNotificationSettingsButton;
        [SerializeField] private Button m_RequestAndroidInAppReviewButton;
        [SerializeField] private Button m_RequestIosInAppReviewButton;

        private CancellationTokenSource m_Cts;

        /// <summary>
        /// 注册七个显式操作按钮，并就近显示对应公开 API。
        /// </summary>
        /// <param name="userData">用户自定义数据，本 View 不使用。</param>
        protected override void OnInit(object userData)
        {
            base.OnInit(userData);
            SetTitle("Native");

            if (m_QueryButton != null)
            {
                m_QueryButton.onClick.AddListener(OnQueryButtonClick);
                SetButtonApiHint(
                    m_QueryButton,
                    "Nova.Native.GetNotificationPermissionStatusAsync()");
            }

            if (m_RequestStandardButton != null)
            {
                m_RequestStandardButton.onClick.AddListener(OnRequestStandardButtonClick);
                SetButtonApiHint(
                    m_RequestStandardButton,
                    "Nova.Native.RequestNotificationPermissionAsync(Alert | Sound | Badge)");
            }

            if (m_RequestProvisionalButton != null)
            {
                m_RequestProvisionalButton.onClick.AddListener(OnRequestProvisionalButtonClick);
                SetButtonApiHint(
                    m_RequestProvisionalButton,
                    "Nova.Native.RequestNotificationPermissionAsync(Provisional)");
            }

            if (m_OpenSettingsButton != null)
            {
                m_OpenSettingsButton.onClick.AddListener(OnOpenSettingsButtonClick);
                SetButtonApiHint(m_OpenSettingsButton, "Nova.Native.OpenAppSettingsAsync()");
            }

            if (m_OpenNotificationSettingsButton != null)
            {
                m_OpenNotificationSettingsButton.onClick.AddListener(OnOpenNotificationSettingsButtonClick);
                SetButtonApiHint(
                    m_OpenNotificationSettingsButton,
                    "Nova.Native.OpenNotificationSettingsAsync()");
            }

            if (m_RequestAndroidInAppReviewButton != null)
            {
                m_RequestAndroidInAppReviewButton.onClick.AddListener(
                    OnRequestAndroidInAppReviewButtonClick);
                SetButtonApiHint(
                    m_RequestAndroidInAppReviewButton,
                    "Nova.Native.RequestInAppReviewAsync()");
            }

            if (m_RequestIosInAppReviewButton != null)
            {
                m_RequestIosInAppReviewButton.onClick.AddListener(OnRequestIosInAppReviewButtonClick);
                SetButtonApiHint(
                    m_RequestIosInAppReviewButton,
                    "Nova.Native.RequestInAppReviewAsync()");
            }
        }

        /// <summary>
        /// 页面每次打开时创建独立等待生命周期，状态保持为未查询，不自动访问系统权限。
        /// </summary>
        /// <param name="userData">用户自定义数据，本 View 不使用。</param>
        public override void OnOpen(object userData)
        {
            base.OnOpen(userData);
            ResetCancellationSource();
            SetStatusText("未查询");
            SetInAppReviewStatusText("未请求");
            RefreshInAppReviewButtonAvailability();
        }

        /// <summary>
        /// 页面关闭只取消本页面等待，不取消操作系统权限弹窗或共享底层请求。
        /// </summary>
        /// <param name="isShutdown">是否由框架关闭触发。</param>
        /// <param name="userData">用户自定义数据。</param>
        public override void OnClose(bool isShutdown, object userData)
        {
            m_Cts?.Cancel();
            m_Cts?.Dispose();
            m_Cts = null;
            base.OnClose(isShutdown, userData);
        }

        private void OnQueryButtonClick()
        {
            QueryStatusAsync().Forget();
        }

        private void OnRequestStandardButtonClick()
        {
            RequestStandardAsync().Forget();
        }

        private void OnRequestProvisionalButtonClick()
        {
            RequestProvisionalAsync().Forget();
        }

        private void OnOpenSettingsButtonClick()
        {
            OpenAppSettingsAsync().Forget();
        }

        private void OnOpenNotificationSettingsButtonClick()
        {
            OpenNotificationSettingsAsync().Forget();
        }

        /// <summary>
        /// Android 应用内评价测试按钮回调；实际平台分发仍由统一 Native 门面负责。
        /// </summary>
        private void OnRequestAndroidInAppReviewButtonClick()
        {
            RequestInAppReviewAsync("Android").Forget();
        }

        /// <summary>
        /// iOS 应用内评价测试按钮回调；实际平台分发仍由统一 Native 门面负责。
        /// </summary>
        private void OnRequestIosInAppReviewButtonClick()
        {
            RequestInAppReviewAsync("iOS").Forget();
        }

        /// <summary>
        /// 查询系统当前通知权限并刷新状态卡。
        /// </summary>
        private async UniTaskVoid QueryStatusAsync()
        {
            if (!TryGetNative(out CancellationToken token))
            {
                return;
            }

            try
            {
                NotificationPermissionStatus status =
                    await Nova.Native.GetNotificationPermissionStatusAsync(token);
                SetStatusText(status.ToString());
                AppendFeedback(
                    "Nova.Native.GetNotificationPermissionStatusAsync() -> status=" + status,
                    GetStatusFeedbackLevel(status));
            }
            catch (OperationCanceledException)
            {
                // View 关闭后的正常结束，不追加误导性失败反馈。
            }
            catch (Exception exception)
            {
                AppendException("Nova.Native.GetNotificationPermissionStatusAsync()", exception);
            }
        }

        /// <summary>
        /// 请求 Alert、Sound 与 Badge 常规通知权限。
        /// </summary>
        private async UniTaskVoid RequestStandardAsync()
        {
            const NotificationAuthorizationOptions c_Options =
                NotificationAuthorizationOptions.Alert |
                NotificationAuthorizationOptions.Sound |
                NotificationAuthorizationOptions.Badge;
            await RequestPermissionAsync(c_Options, "Alert | Sound | Badge");
        }

        /// <summary>
        /// 请求 iOS Provisional 临时静默授权；Android 仍按系统通知权限流程处理。
        /// </summary>
        private async UniTaskVoid RequestProvisionalAsync()
        {
            await RequestPermissionAsync(
                NotificationAuthorizationOptions.Provisional,
                "Provisional");
        }

        /// <summary>
        /// 执行指定选项的权限请求，并分别展示流程成功与最终系统状态。
        /// </summary>
        private async UniTask RequestPermissionAsync(
            NotificationAuthorizationOptions options,
            string optionsText)
        {
            if (!TryGetNative(out CancellationToken token))
            {
                return;
            }

            try
            {
                NotificationPermissionResult result =
                    await Nova.Native.RequestNotificationPermissionAsync(options, token);
                SetStatusText(result.Status.ToString());

                FeedbackLevel level = result.IsOperationSuccessful
                    ? GetStatusFeedbackLevel(result.Status)
                    : FeedbackLevel.Error;
                AppendFeedback(
                    "Nova.Native.RequestNotificationPermissionAsync(" + optionsText + ")" +
                    " -> operationSuccessful=" + result.IsOperationSuccessful +
                    ", status=" + result.Status +
                    ", errorCode=" + result.ErrorCode +
                    ", errorDomain=" + FormatOptional(result.ErrorDomain) +
                    ", errorMessage=" + FormatOptional(result.ErrorMessage),
                    level);
            }
            catch (OperationCanceledException)
            {
                // View 关闭后的正常结束，不追加误导性失败反馈。
            }
            catch (Exception exception)
            {
                AppendException(
                    "Nova.Native.RequestNotificationPermissionAsync(" + optionsText + ")",
                    exception);
            }
        }

        /// <summary>
        /// 打开当前应用设置根页；返回值只代表成功发起跳转，不代表用户修改了权限。
        /// </summary>
        private async UniTaskVoid OpenAppSettingsAsync()
        {
            if (!TryGetNative(out _))
            {
                return;
            }

            try
            {
                bool opened = await Nova.Native.OpenAppSettingsAsync();
                AppendFeedback(
                    "Nova.Native.OpenAppSettingsAsync() -> opened=" + opened +
                    "（仅表示已发起当前应用设置根页跳转，不代表权限已修改）",
                    opened ? FeedbackLevel.Success : FeedbackLevel.Warn);
            }
            catch (Exception exception)
            {
                AppendException("Nova.Native.OpenAppSettingsAsync()", exception);
            }
        }

        /// <summary>
        /// 精准打开当前应用通知设置；不支持时返回 false，绝不改为打开应用设置根页。
        /// </summary>
        private async UniTaskVoid OpenNotificationSettingsAsync()
        {
            if (!TryGetNative(out _))
            {
                return;
            }

            try
            {
                bool opened = await Nova.Native.OpenNotificationSettingsAsync();
                AppendFeedback(
                    "Nova.Native.OpenNotificationSettingsAsync() -> opened=" + opened +
                    "（true 仅表示已发起精准通知设置跳转；false 表示不支持或启动失败，且不会回退到应用设置）",
                    opened ? FeedbackLevel.Success : FeedbackLevel.Warn);
            }
            catch (Exception exception)
            {
                AppendException("Nova.Native.OpenNotificationSettingsAsync()", exception);
            }
        }

        /// <summary>
        /// 显式请求应用内评价流程，并只展示 Native 已将请求交给系统的技术状态。
        /// </summary>
        /// <param name="platformLabel">当前按钮对应的平台标签，仅用于 Demo 反馈说明。</param>
        private async UniTaskVoid RequestInAppReviewAsync(string platformLabel)
        {
            if (!TryGetNative(out CancellationToken token))
            {
                return;
            }

            try
            {
                InAppReviewRequestResult result = await Nova.Native.RequestInAppReviewAsync(token);
                SetInAppReviewStatusText(result.Status.ToString());
                AppendFeedback(
                    "Nova.Native.RequestInAppReviewAsync() [" + platformLabel + "]" +
                    " -> status=" + result.Status +
                    ", requestDispatched=" + result.IsRequestDispatched +
                    ", errorMessage=" + FormatOptional(result.ErrorMessage) +
                    "（RequestDispatched 仅表示已交给系统流程，不表示系统提示已展示、用户已评价或提交成功）",
                    GetInAppReviewFeedbackLevel(result.Status));
            }
            catch (OperationCanceledException)
            {
                // View 关闭后的正常结束，不追加误导性失败反馈。
            }
            catch (Exception exception)
            {
                AppendException("Nova.Native.RequestInAppReviewAsync()", exception);
            }
        }

        private bool TryGetNative(out CancellationToken token)
        {
            token = m_Cts?.Token ?? default;
            if (Nova.Native != null)
            {
                return true;
            }

            AppendFeedback("Nova.Native -> NativeComponent 未初始化", FeedbackLevel.Error);
            return false;
        }

        private void ResetCancellationSource()
        {
            m_Cts?.Cancel();
            m_Cts?.Dispose();
            m_Cts = new CancellationTokenSource();
        }

        private void SetStatusText(string status)
        {
            if (m_StatusText != null)
            {
                m_StatusText.text = "当前状态：" + status;
            }
        }

        /// <summary>
        /// 刷新应用内评价状态卡，不混用通知权限状态的展示语义。
        /// </summary>
        /// <param name="status">应用内评价请求状态文本。</param>
        private void SetInAppReviewStatusText(string status)
        {
            if (m_InAppReviewStatusText != null)
            {
                m_InAppReviewStatusText.text = "应用内评价状态：" + status;
            }
        }

        /// <summary>
        /// 在移动真机只启用对应平台的测试按钮；Editor 与其他平台保留两个入口用于验证统一回退结果。
        /// </summary>
        private void RefreshInAppReviewButtonAvailability()
        {
            if (Application.platform == RuntimePlatform.Android)
            {
                SetButtonInteractable(m_RequestAndroidInAppReviewButton, true);
                SetButtonInteractable(m_RequestIosInAppReviewButton, false);
                return;
            }

            if (Application.platform == RuntimePlatform.IPhonePlayer)
            {
                SetButtonInteractable(m_RequestAndroidInAppReviewButton, false);
                SetButtonInteractable(m_RequestIosInAppReviewButton, true);
                return;
            }

            SetButtonInteractable(m_RequestAndroidInAppReviewButton, true);
            SetButtonInteractable(m_RequestIosInAppReviewButton, true);
        }

        /// <summary>
        /// 安全设置可选 Demo 按钮的交互状态。
        /// </summary>
        /// <param name="button">待更新的按钮。</param>
        /// <param name="interactable">是否允许点击。</param>
        private static void SetButtonInteractable(Button button, bool interactable)
        {
            if (button != null)
            {
                button.interactable = interactable;
            }
        }

        private void AppendException(string api, Exception exception)
        {
            AppendFeedback(
                api + " -> exception=" + exception.GetType().Name +
                ", message=" + exception.Message,
                FeedbackLevel.Error);
        }

        private static string FormatOptional(string value)
        {
            return string.IsNullOrEmpty(value) ? "<empty>" : value;
        }

        private static FeedbackLevel GetStatusFeedbackLevel(NotificationPermissionStatus status)
        {
            return status switch
            {
                NotificationPermissionStatus.Authorized => FeedbackLevel.Success,
                NotificationPermissionStatus.Provisional => FeedbackLevel.Success,
                NotificationPermissionStatus.Ephemeral => FeedbackLevel.Success,
                NotificationPermissionStatus.Denied => FeedbackLevel.Warn,
                NotificationPermissionStatus.Unsupported => FeedbackLevel.Warn,
                NotificationPermissionStatus.NotDetermined => FeedbackLevel.Info,
                _ => FeedbackLevel.Error,
            };
        }

        /// <summary>
        /// 将应用内评价技术状态映射为 Demo 反馈等级。
        /// </summary>
        /// <param name="status">平台原生请求状态。</param>
        /// <returns>对应的反馈等级。</returns>
        private static FeedbackLevel GetInAppReviewFeedbackLevel(InAppReviewRequestStatus status)
        {
            return status switch
            {
                InAppReviewRequestStatus.RequestDispatched => FeedbackLevel.Success,
                InAppReviewRequestStatus.Unsupported => FeedbackLevel.Warn,
                InAppReviewRequestStatus.Unavailable => FeedbackLevel.Warn,
                _ => FeedbackLevel.Error,
            };
        }
    }
}
