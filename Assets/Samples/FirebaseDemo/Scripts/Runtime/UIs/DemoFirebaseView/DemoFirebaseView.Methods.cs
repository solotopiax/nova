/***************************************************************
 * (c) copyright 2026 - 2030, Solotopia
 * All Rights Reserved.
 * -------------------------------------------------------------
 * filename:  DemoFirebaseView.Methods.cs
 * author:    nova-create-sample
 * created:   2026/06/02
 * descrip:   DemoFirebaseView 演示 View — 私有方法
 ***************************************************************/

using System;
using Cysharp.Threading.Tasks;
using NovaFramework.Kit.Network.GameLogin.Runtime;
using NovaFramework.Runtime;
using NovaFramework.SDK.FirebasePlugin.Runtime;
using Newtonsoft.Json;
using TMPro;

namespace NovaFramework.Sdk.Firebase.Samples.Runtime
{
    /// <summary>
    /// DemoFirebaseView 演示 View 的私有方法。
    /// </summary>
    public sealed partial class DemoFirebaseView
    {
        /// <summary>
        /// 获取 Firebase Analytics Instance ID 并输出到反馈区。
        /// </summary>
        private void OnGetInstanceIdButtonClick()
        {
            if (!TryGetFirebasePlugin(out FirebasePlugin plugin))
            {
                return;
            }

            string instanceId = plugin.GetAnalyticsInstanceId();
            AppendFeedback($"{plugin.GetType().Name}.GetAnalyticsInstanceId() -> {instanceId}", FeedbackLevel.Success);
        }

        /// <summary>
        /// 获取 Firebase Token 并输出到反馈区。
        /// </summary>
        private void OnGetTokenButtonClick()
        {
            if (!TryGetFirebasePlugin(out FirebasePlugin plugin))
            {
                return;
            }

            string token = plugin.GetToken();
            AppendFeedback($"{plugin.GetType().Name}.GetToken() -> {token}", FeedbackLevel.Success);
        }

        /// <summary>
        /// 将输入框里的 key/value 写入当前待发送的打点参数。
        /// </summary>
        private void OnAddEventParamButtonClick()
        {
            string key = m_EventParamKeyInput != null ? m_EventParamKeyInput.text?.Trim() : string.Empty;
            string value = m_EventParamValueInput != null ? m_EventParamValueInput.text : string.Empty;
            if (string.IsNullOrEmpty(key))
            {
                AppendFeedback("添加打点属性失败：属性名不能为空。", FeedbackLevel.Warn);
                return;
            }

            m_EventParams[key] = value ?? string.Empty;
            RefreshEventParamsPreview();
            AppendFeedback($"添加打点属性：{key}={m_EventParams[key]}", FeedbackLevel.Success);
        }

        /// <summary>
        /// 清空当前打点参数缓存，并刷新界面预览。
        /// </summary>
        private void OnClearEventParamsButtonClick()
        {
            m_EventParams.Clear();
            RefreshEventParamsPreview();
            AppendFeedback("已清空当前打点属性。", FeedbackLevel.Success);
        }

        /// <summary>
        /// 校验事件名后，将当前参数发送到 Firebase。
        /// </summary>
        private void OnSendEventButtonClick()
        {
            string eventName = m_EventNameInput != null ? m_EventNameInput.text?.Trim() : string.Empty;
            if (string.IsNullOrEmpty(eventName))
            {
                AppendFeedback("发送打点失败：打点名字不能为空。", FeedbackLevel.Warn);
                return;
            }

            if (!TryGetFirebasePlugin(out FirebasePlugin plugin))
            {
                return;
            }

            plugin.TrackEvent(eventName, m_EventParams);
            AppendFeedback($"{plugin.GetType().Name}.TrackEvent(\"{eventName}\", params={FormatEventParams()})", FeedbackLevel.Success);
        }

        /// <summary>
        /// 启动示例登录流程，避免按钮回调直接阻塞。
        /// </summary>
        private void OnLoginButtonClick()
        {
            LoginAsync().Forget();
        }

        /// <summary>
        /// 登录流程。
        /// </summary>
        private async UniTaskVoid LoginAsync()
        {
            string openId = string.Empty;
            bool forceNewAccount = false;
            try
            {
                NetResponse<PbNetLoginResp> resp = await Nova.Network.Kit<Login>().Async(string.Empty, openId, forceNewAccount);
                if (resp.IsSuccess)
                {
                    string uid = resp.Data != null ? resp.Data.Uid : string.Empty;
                    if (string.IsNullOrEmpty(uid))
                    {
                        m_HasLoggedIn = false;
                        SetPushTaskSendButtonInteractable(false);
                        AppendFeedback("登录成功但 UID 为空，Push Task 发送按钮保持禁用。", FeedbackLevel.Warn);
                    }
                    else if (Nova.SDK == null)
                    {
                        m_HasLoggedIn = false;
                        SetPushTaskSendButtonInteractable(false);
                        AppendFeedback("登录成功但 Nova.SDK 不可用，Push Task 发送按钮保持禁用。", FeedbackLevel.Warn);
                    }
                    else
                    {
                        Nova.SDK.Login(uid);
                        m_HasLoggedIn = true;
                        SetPushTaskSendButtonInteractable(true);
                        AppendFeedback("登录成功，Push Task 发送按钮已启用。", FeedbackLevel.Info);
                    }

                    AppendFeedback($"Nova.Network.Kit<Login>().Async(string.Empty, \"{openId}\", {forceNewAccount}) -> IsSuccess=true, UID={uid}", FeedbackLevel.Success);
                }
                else
                {
                    AppendFeedback($"Nova.Network.Kit<Login>().Async(string.Empty, \"{openId}\", {forceNewAccount}) -> IsSuccess=false, ErrorCode={resp.ErrorCode}, ErrorMessage={resp.ErrorMessage}", FeedbackLevel.Error);
                }
            }
            catch (Exception ex)
            {
                AppendFeedback($"登录异常：{ex.Message}", FeedbackLevel.Error);
            }
        }

        /// <summary>
        /// 启动 push task 缓存流程，避免按钮回调直接阻塞。
        /// </summary>
        private void OnSendPushTaskButtonClick()
        {
            if (!m_HasLoggedIn)
            {
                SetPushTaskSendButtonInteractable(false);
                AppendFeedback("请先登录，PushTask 协议发送依赖登录 UID。", FeedbackLevel.Warn);
                return;
            }

            SendPushTaskAsync().Forget();
        }

        /// <summary>
        /// 构造 FirebasePushTask 并写入 FirebasePlugin 的本地缓存。
        /// </summary>
        private async UniTaskVoid SendPushTaskAsync()
        {
            if (!TryGetFirebasePlugin(out FirebasePlugin plugin))
            {
                return;
            }

            FirebasePushTask task = BuildSelectedPushTask();
            try
            {
                IFirebasePushTaskPlugin pushTaskPlugin = plugin;
                bool queued = await pushTaskPlugin.QueuePushTaskAsync(task);
                if (queued)
                {
                    AppendFeedback($"{plugin.GetType().Name}.QueuePushTaskAsync(task) -> task_key={task.TaskKey}, trigger_time={task.TriggerTime}, cancel={task.Cancel}, template_id={task.TemplateId}", FeedbackLevel.Success);
                    AppendFeedback("PushTask 已写入本地缓存；协议发送需等待 Firebase 初始化、登录 SetUserId，以及数量或时间阈值。", FeedbackLevel.Info);
                }
                else
                {
                    AppendFeedback($"{plugin.GetType().Name}.QueuePushTaskAsync(task) -> false", FeedbackLevel.Warn);
                }
            }
            catch (Exception ex)
            {
                AppendFeedback($"QueuePushTaskAsync 异常：{ex.Message}", FeedbackLevel.Error);
            }
        }

        /// <summary>
        /// 基于当前 UI 选项创建一份 push task。
        /// </summary>
        private FirebasePushTask BuildSelectedPushTask()
        {
            DateTimeOffset triggerTime = DateTimeOffset.UtcNow.Add(GetSelectedPushTaskDelay());
            return new FirebasePushTask
            {
                TaskKey = GetSelectedPushTaskKey(),
                TriggerTime = triggerTime.ToUnixTimeSeconds(),
                Cancel = m_PushTaskCancelToggle != null && m_PushTaskCancelToggle.isOn,
                TemplateId = GetSelectedPushTaskTemplateId(),
            };
        }

        /// <summary>
        /// 刷新 push task 参数预览。
        /// </summary>
        private void RefreshPushTaskPreview()
        {
            if (m_PushTaskPreviewText == null)
            {
                return;
            }

            FirebasePushTask task = BuildSelectedPushTask();
            DateTimeOffset triggerTime = DateTimeOffset.FromUnixTimeSeconds(task.TriggerTime);
            m_PushTaskPreviewText.text = $"PushTask: task_key={task.TaskKey}, utc0={triggerTime:yyyy-MM-dd HH:mm:ss}, cancel={task.Cancel}, template_id={task.TemplateId}";
        }

        private string GetSelectedPushTaskKey()
        {
            string selected = GetSelectedDropdownText(m_PushTaskKeyDropdown);
            return string.IsNullOrEmpty(selected) ? "demo_push_task_1" : selected;
        }

        private TimeSpan GetSelectedPushTaskDelay()
        {
            int index = m_PushTaskTriggerTimeDropdown != null ? m_PushTaskTriggerTimeDropdown.value : 0;
            switch (index)
            {
                case 1:
                    return TimeSpan.FromMinutes(1);
                case 2:
                    return TimeSpan.FromMinutes(5);
                case 3:
                    return TimeSpan.FromMinutes(10);
                case 4:
                    return TimeSpan.FromHours(1);
                case 5:
                    return TimeSpan.FromHours(3);
                case 6:
                    return TimeSpan.FromHours(12);
                case 7:
                    return TimeSpan.FromHours(24);
                default:
                    return TimeSpan.Zero;
            }
        }

        private long GetSelectedPushTaskTemplateId()
        {
            int index = m_PushTaskTemplateIdDropdown != null ? m_PushTaskTemplateIdDropdown.value : 0;
            return index >= 0 && index < 4 ? index + 1 : 1;
        }

        private static string GetSelectedDropdownText(TMP_Dropdown dropdown)
        {
            if (dropdown == null || dropdown.options == null || dropdown.options.Count == 0)
            {
                return string.Empty;
            }

            int index = dropdown.value;
            if (index < 0 || index >= dropdown.options.Count)
            {
                return string.Empty;
            }

            return dropdown.options[index]?.text ?? string.Empty;
        }

        /// <summary>
        /// 从 Nova.SDK 中获取 FirebasePlugin，并在不可用时给出反馈。
        /// </summary>
        private bool TryGetFirebasePlugin(out FirebasePlugin plugin)
        {
            plugin = null;
            if (Nova.SDK == null)
            {
                AppendFeedback("Nova.SDK 不可用。", FeedbackLevel.Error);
                return false;
            }

            if (!Nova.SDK.TryGet(out plugin) || plugin == null)
            {
                AppendFeedback("FirebasePlugin 不可用，请确认 Firebase SDK 配置已启用并初始化完成。", FeedbackLevel.Error);
                return false;
            }

            return true;
        }

        /// <summary>
        /// 根据当前打点参数刷新界面上的参数预览文本。
        /// </summary>
        private void RefreshEventParamsPreview()
        {
            if (m_EventParamsPreviewText == null)
            {
                return;
            }

            m_EventParamsPreviewText.text = m_EventParams.Count == 0
                ? "当前属性：空"
                : "当前属性：" + JsonConvert.SerializeObject(m_EventParams, Formatting.Indented);
        }

        /// <summary>
        /// 将当前打点参数格式化为反馈区使用的 JSON 字符串。
        /// </summary>
        private string FormatEventParams()
        {
            return m_EventParams.Count == 0
                ? "{}"
                : JsonConvert.SerializeObject(m_EventParams, Formatting.Indented);
        }
    }
}
