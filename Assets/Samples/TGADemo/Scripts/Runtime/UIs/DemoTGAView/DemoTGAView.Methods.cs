/***************************************************************
 * (c) copyright 2026 - 2030, Solotopia
 * All Rights Reserved.
 * -------------------------------------------------------------
 * filename:  DemoTGAView.Methods.cs
 * author:    nova-create-sample
 * created:   2026/06/01
 * descrip:   DemoTGAView 演示 View - 私有方法
 ***************************************************************/

using System;
using System.Collections.Generic;
using System.Globalization;
using Cysharp.Threading.Tasks;
using NovaFramework.Kit.Network.GameLogin.Runtime;
using NovaFramework.Runtime;
using NovaFramework.SDK.TGAPlugin.Runtime;
using Newtonsoft.Json;
using TMPro;

namespace NovaFramework.Sdk.Tga.Samples.Runtime
{
    /// <summary>
    /// DemoTGAView 演示 View 的私有方法。
    /// </summary>
    public sealed partial class DemoTGAView
    {
        /// <summary>
        /// 启动示例登录流程，避免按钮回调直接阻塞。
        /// </summary>
        private void OnLoginButtonClick()
        {
            LoginAsync().Forget();
        }

        /// <summary>
        /// 获取 TGA 设备 ID 并输出到反馈区。
        /// </summary>
        private void OnGetDeviceIdButtonClick()
        {
            if (!TryGetTGAPlugin(out TGAPlugin plugin))
            {
                return;
            }

            AppendFeedback($"{plugin.GetType().Name}.GetDeviceId() -> {plugin.GetDeviceId()}", FeedbackLevel.Success);
        }

        /// <summary>
        /// 获取 TGA 访客 ID 并输出到反馈区。
        /// </summary>
        private void OnGetDistinctIdButtonClick()
        {
            if (!TryGetTGAPlugin(out TGAPlugin plugin))
            {
                return;
            }

            AppendFeedback($"{plugin.GetType().Name}.GetDistinctId() -> {plugin.GetDistinctId()}", FeedbackLevel.Success);
        }

        /// <summary>
        /// 获取 TGA 预置属性并格式化展示。
        /// </summary>
        private void OnGetPresetPropertiesButtonClick()
        {
            if (!TryGetTGAPlugin(out TGAPlugin plugin))
            {
                return;
            }

            AppendFeedback($"{plugin.GetType().Name}.GetPresetProperties() -> {FormatKeyValueData(plugin.GetPresetProperties())}", FeedbackLevel.Success);
        }

        /// <summary>
        /// 将输入框里的 key/value 写入当前待发送的打点参数。
        /// </summary>
        private void OnAddEventParamButtonClick()
        {
            string key = GetTrimmedText(m_EventParamKeyInput);
            string value = GetText(m_EventParamValueInput);
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
        /// 校验事件名后，将当前参数发送到 TGA。
        /// </summary>
        private void OnSendEventButtonClick()
        {
            string eventName = GetTrimmedText(m_EventNameInput);
            if (string.IsNullOrEmpty(eventName))
            {
                AppendFeedback("发送打点失败：打点名字不能为空。", FeedbackLevel.Warn);
                return;
            }

            if (!TryGetTGAPlugin(out TGAPlugin plugin))
            {
                return;
            }

            plugin.TrackEvent(eventName, m_EventParams);
            AppendFeedback($"{plugin.GetType().Name}.TrackEvent(\"{eventName}\", params={FormatEventParams()})", FeedbackLevel.Success);
        }

        /// <summary>
        /// 对当前事件名调用 TGA 事件计时。
        /// </summary>
        private void OnTimeEventButtonClick()
        {
            string eventName = GetTrimmedText(m_EventNameInput);
            if (string.IsNullOrEmpty(eventName))
            {
                AppendFeedback("事件计时失败：打点名字不能为空。", FeedbackLevel.Warn);
                return;
            }

            if (!TryGetTGAPlugin(out TGAPlugin plugin))
            {
                return;
            }

            plugin.TimeEvent(eventName);
            AppendFeedback($"{plugin.GetType().Name}.TimeEvent(\"{eventName}\")", FeedbackLevel.Success);
        }

        /// <summary>
        /// 立即 Flush TGA 本地缓存。
        /// </summary>
        private void OnFlushButtonClick()
        {
            if (!TryGetTGAPlugin(out TGAPlugin plugin))
            {
                return;
            }

            plugin.Flush();
            AppendFeedback($"{plugin.GetType().Name}.Flush()", FeedbackLevel.Success);
        }

        /// <summary>
        /// 设置可覆盖的用户属性。
        /// </summary>
        private void OnUserSetButtonClick()
        {
            if (!TryGetUserPropertyInput(out string key, out string value))
            {
                return;
            }

            if (!TryGetTGAPlugin(out TGAPlugin plugin))
            {
                return;
            }

            plugin.UserSet(key, value);
            AppendFeedback($"{plugin.GetType().Name}.UserSet(\"{key}\", \"{value}\")", FeedbackLevel.Success);
        }

        /// <summary>
        /// 设置只写一次的用户属性。
        /// </summary>
        private void OnUserSetOnceButtonClick()
        {
            if (!TryGetUserPropertyInput(out string key, out string value))
            {
                return;
            }

            if (!TryGetTGAPlugin(out TGAPlugin plugin))
            {
                return;
            }

            plugin.UserSetOnce(key, value);
            AppendFeedback($"{plugin.GetType().Name}.UserSetOnce(\"{key}\", \"{value}\")", FeedbackLevel.Success);
        }

        /// <summary>
        /// 将用户属性按数字累加。
        /// </summary>
        private void OnUserAddButtonClick()
        {
            if (!TryGetUserPropertyInput(out string key, out string value))
            {
                return;
            }

            if (!double.TryParse(value, NumberStyles.Float, CultureInfo.InvariantCulture, out double numericValue))
            {
                AppendFeedback("UserAdd 失败：value 必须是数字。", FeedbackLevel.Warn);
                return;
            }

            if (!TryGetTGAPlugin(out TGAPlugin plugin))
            {
                return;
            }

            plugin.UserAdd(key, numericValue);
            AppendFeedback($"{plugin.GetType().Name}.UserAdd(\"{key}\", {numericValue.ToString(CultureInfo.InvariantCulture)})", FeedbackLevel.Success);
        }

        /// <summary>
        /// 将逗号分隔的输入追加到列表型用户属性。
        /// </summary>
        private void OnUserAppendButtonClick()
        {
            if (!TryGetUserPropertyInput(out string key, out string value))
            {
                return;
            }

            if (!TryGetTGAPlugin(out TGAPlugin plugin))
            {
                return;
            }

            List<object> values = new List<object>();
            foreach (string item in value.Split(','))
            {
                string trimmed = item.Trim();
                if (!string.IsNullOrEmpty(trimmed))
                {
                    values.Add(trimmed);
                }
            }

            plugin.UserAppend(key, values);
            AppendFeedback($"{plugin.GetType().Name}.UserAppend(\"{key}\", {JsonConvert.SerializeObject(values, Formatting.Indented)})", FeedbackLevel.Success);
        }

        /// <summary>
        /// 删除指定用户属性。
        /// </summary>
        private void OnUserUnsetButtonClick()
        {
            string key = GetTrimmedText(m_UserPropertyKeyInput);
            if (string.IsNullOrEmpty(key))
            {
                AppendFeedback("UserUnset 失败：属性名不能为空。", FeedbackLevel.Warn);
                return;
            }

            if (!TryGetTGAPlugin(out TGAPlugin plugin))
            {
                return;
            }

            plugin.UserUnset(key);
            AppendFeedback($"{plugin.GetType().Name}.UserUnset(\"{key}\")", FeedbackLevel.Success);
        }

        /// <summary>
        /// 删除当前用户的全部属性数据。
        /// </summary>
        private void OnUserDeleteButtonClick()
        {
            if (!TryGetTGAPlugin(out TGAPlugin plugin))
            {
                return;
            }

            plugin.UserDelete();
            AppendFeedback($"{plugin.GetType().Name}.UserDelete()", FeedbackLevel.Success);
        }

        /// <summary>
        /// 设置静态公共事件属性。
        /// </summary>
        private void OnSetSuperPropertyButtonClick()
        {
            if (!TryGetSuperPropertyInput(out string key, out string value))
            {
                return;
            }

            if (!TryGetTGAPlugin(out TGAPlugin plugin))
            {
                return;
            }

            plugin.SetSuperProperty(key, value);
            AppendFeedback($"{plugin.GetType().Name}.SetSuperProperty(\"{key}\", \"{value}\")", FeedbackLevel.Success);
        }

        /// <summary>
        /// 清空静态公共事件属性。
        /// </summary>
        private void OnClearSuperPropertiesButtonClick()
        {
            if (!TryGetTGAPlugin(out TGAPlugin plugin))
            {
                return;
            }

            plugin.ClearSuperProperties();
            AppendFeedback($"{plugin.GetType().Name}.ClearSuperProperties()", FeedbackLevel.Success);
        }

        /// <summary>
        /// 设置动态公共事件属性。
        /// </summary>
        private void OnSetDynamicSuperPropertyButtonClick()
        {
            if (!TryGetSuperPropertyInput(out string key, out string value))
            {
                return;
            }

            if (!TryGetTGAPlugin(out TGAPlugin plugin))
            {
                return;
            }

            plugin.SetDynamicSuperProperty(key, value);
            AppendFeedback($"{plugin.GetType().Name}.SetDynamicSuperProperty(\"{key}\", \"{value}\")", FeedbackLevel.Success);
        }

        /// <summary>
        /// 获取当前动态公共事件属性快照并格式化展示。
        /// </summary>
        private void OnGetDynamicSuperPropertiesButtonClick()
        {
            if (!TryGetTGAPlugin(out TGAPlugin plugin))
            {
                return;
            }

            AppendFeedback($"{plugin.GetType().Name}.GetDynamicSuperProperties() -> {FormatKeyValueData(plugin.GetDynamicSuperProperties())}", FeedbackLevel.Success);
        }

        /// <summary>
        /// 清空动态公共事件属性。
        /// </summary>
        private void OnClearDynamicSuperPropertiesButtonClick()
        {
            if (!TryGetTGAPlugin(out TGAPlugin plugin))
            {
                return;
            }

            plugin.ClearDynamicSuperProperties();
            AppendFeedback($"{plugin.GetType().Name}.ClearDynamicSuperProperties()", FeedbackLevel.Success);
        }

        /// <summary>
        /// 登录流程。
        /// </summary>
        private async UniTaskVoid LoginAsync()
        {
            string openId = "test_openid_guest";
            bool forceNewAccount = false;
            try
            {
                NetResponse<PbNetLoginResp> resp = await Nova.Network.Kit<Login>().Async(string.Empty, openId, forceNewAccount);
                if (resp.IsSuccess)
                {
                    string uid = resp.Data != null ? resp.Data.Uid : string.Empty;
                    if (!string.IsNullOrEmpty(uid) && Nova.SDK != null)
                    {
                        Nova.SDK.Login(uid);
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
        /// 从 Nova.SDK 中获取 TGAPlugin，并在不可用时给出反馈。
        /// </summary>
        private bool TryGetTGAPlugin(out TGAPlugin plugin)
        {
            plugin = null;
            if (Nova.SDK == null)
            {
                AppendFeedback("Nova.SDK 不可用。", FeedbackLevel.Error);
                return false;
            }

            if (!Nova.SDK.TryGet(out plugin) || plugin == null)
            {
                AppendFeedback("TGAPlugin 不可用，请确认 TGA SDK 配置已启用并初始化完成。", FeedbackLevel.Error);
                return false;
            }

            return true;
        }

        /// <summary>
        /// 读取并校验用户属性输入框。
        /// </summary>
        private bool TryGetUserPropertyInput(out string key, out string value)
        {
            key = GetTrimmedText(m_UserPropertyKeyInput);
            value = GetText(m_UserPropertyValueInput) ?? string.Empty;
            if (string.IsNullOrEmpty(key))
            {
                AppendFeedback("用户属性操作失败：属性名不能为空。", FeedbackLevel.Warn);
                return false;
            }

            return true;
        }

        /// <summary>
        /// 读取并校验公共属性输入框。
        /// </summary>
        private bool TryGetSuperPropertyInput(out string key, out string value)
        {
            key = GetTrimmedText(m_SuperPropertyKeyInput);
            value = GetText(m_SuperPropertyValueInput) ?? string.Empty;
            if (string.IsNullOrEmpty(key))
            {
                AppendFeedback("公共属性操作失败：属性名不能为空。", FeedbackLevel.Warn);
                return false;
            }

            return true;
        }

        /// <summary>
        /// 隐藏事件参数预览文本；添加结果统一输出到底部反馈区。
        /// </summary>
        private void RefreshEventParamsPreview()
        {
            if (m_EventParamsPreviewText == null)
            {
                return;
            }

            m_EventParamsPreviewText.text = string.Empty;
            m_EventParamsPreviewText.gameObject.SetActive(false);
        }

        /// <summary>
        /// 将当前打点参数格式化为反馈区使用的 JSON 字符串。
        /// </summary>
        private string FormatEventParams()
        {
            return FormatKeyValueData(m_EventParams);
        }

        /// <summary>
        /// 将 key/value 数据格式化为稳定的 JSON 字符串，空数据返回空对象。
        /// </summary>
        private string FormatKeyValueData(IReadOnlyDictionary<string, object> data)
        {
            return data == null || data.Count == 0 ? "{}" : JsonConvert.SerializeObject(data, Formatting.Indented);
        }

        /// <summary>
        /// 读取输入框内容并移除首尾空白。
        /// </summary>
        private string GetTrimmedText(TMP_InputField input)
        {
            return input != null ? input.text?.Trim() ?? string.Empty : string.Empty;
        }

        /// <summary>
        /// 读取输入框原始内容。
        /// </summary>
        private string GetText(TMP_InputField input)
        {
            return input != null ? input.text : string.Empty;
        }
    }
}
