/***************************************************************
 * (c) copyright 2026 - 2030, Solotopia
 * All Rights Reserved.
 * -------------------------------------------------------------
 * filename:  DemoAppsflyerView.Methods.cs
 * author:    nova-create-sample
 * created:   2026/06/02
 * descrip:   DemoAppsflyerView 演示 View - 私有方法
 ***************************************************************/

using System;
using System.Collections.Generic;
using System.Text;
using Cysharp.Threading.Tasks;
using NovaFramework.Kit.Network.GameLogin.Runtime;
using NovaFramework.Runtime;
using NovaFramework.SDK.AppsFlyerPlugin.Runtime;
using Newtonsoft.Json;

namespace NovaFramework.Sdk.Appsflyer.Samples.Runtime
{
    /// <summary>
    /// DemoAppsflyerView 演示 View 的私有方法。
    /// </summary>
    public sealed partial class DemoAppsflyerView
    {
        /// <summary>
        /// 获取 AppsFlyer 设备标识并输出到反馈区。
        /// </summary>
        private void OnGetAppsFlyerIdButtonClick()
        {
            if (!TryGetAppsFlyerPlugin(out AppsFlyerPlugin plugin))
            {
                return;
            }

            string appsFlyerId = plugin.GetAppsFlyerID();
            AppendFeedback($"{plugin.GetType().Name}.GetAppsFlyerID() -> {appsFlyerId}", FeedbackLevel.Success);
        }

        /// <summary>
        /// 从 AppsFlyer 插件读取当前归因数据并格式化展示。
        /// </summary>
        private void OnGetConversionDataButtonClick()
        {
            if (!TryGetAppsFlyerPlugin(out AppsFlyerPlugin plugin))
            {
                return;
            }

            Dictionary<string, object> conversionData = plugin.GetConversionData();
            AppendFeedback($"{plugin.GetType().Name}.GetConversionData() -> {FormatKeyValueData(conversionData)}", FeedbackLevel.Success);
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
        /// 校验事件名后，将当前参数发送到 AppsFlyer。
        /// </summary>
        private void OnSendEventButtonClick()
        {
            string eventName = m_EventNameInput != null ? m_EventNameInput.text?.Trim() : string.Empty;
            if (string.IsNullOrEmpty(eventName))
            {
                AppendFeedback("发送打点失败：打点名字不能为空。", FeedbackLevel.Warn);
                return;
            }

            if (!TryGetAppsFlyerPlugin(out AppsFlyerPlugin plugin))
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
            string openId = "test_openid_guest";
            bool forceNewAccount = false;
            try
            {
                NetResponse<PbNetLoginResp> resp = await Nova.Network.Kit<Login>().Async(string.Empty, openId, forceNewAccount);
                if (resp.IsSuccess)
                {
                    string uid = resp.Data != null ? resp.Data.Uid : string.Empty;
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
        /// 从 Nova.SDK 中获取 AppsFlyerPlugin，并在不可用时给出反馈。
        /// </summary>
        private bool TryGetAppsFlyerPlugin(out AppsFlyerPlugin plugin)
        {
            plugin = null;
            if (Nova.SDK == null)
            {
                AppendFeedback("Nova.SDK 不可用。", FeedbackLevel.Error);
                return false;
            }

            if (!Nova.SDK.TryGet(out plugin) || plugin == null)
            {
                AppendFeedback("AppsFlyerPlugin 不可用，请确认 AppsFlyer SDK 配置已启用并初始化完成。", FeedbackLevel.Error);
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

            if (m_EventParams.Count == 0)
            {
                m_EventParamsPreviewText.text = "当前属性：空";
                return;
            }
            m_EventParamsPreviewText.text = "当前属性：" + JsonConvert.SerializeObject(m_EventParams, Formatting.Indented);
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
            if (data == null || data.Count == 0)
            {
                return "{}";
            }

            return JsonConvert.SerializeObject(data, Formatting.Indented);
        }
    }
}
