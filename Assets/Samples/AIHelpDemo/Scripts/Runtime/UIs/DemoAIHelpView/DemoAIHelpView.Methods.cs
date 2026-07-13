/***************************************************************
 * (c) copyright 2026 - 2030, Solotopia
 * All Rights Reserved.
 * -------------------------------------------------------------
 * filename:  DemoAIHelpView.Methods.cs
 * author:    nova-create-sample
 * created:   2026/07/09
 * descrip:   DemoAIHelpView 演示 View — 私有方法（各接口按钮回调）。
 *            Editor 下 vendor 为空操作（iOS/Android 才有真实 UI），点击后以文案回显「已调用 xxx」即可。
 ***************************************************************/

using System.Collections.Generic;
using NovaFramework.Runtime;
using NovaFramework.SDK.AIHelp.Runtime;
using UnityEngine.Events;
using UnityEngine.UI;

namespace NovaFramework.Sdk.Aihelp.Samples.Runtime
{
    /// <summary>
    /// DemoAIHelpView 演示 View 的私有方法。
    /// </summary>
    public sealed partial class DemoAIHelpView
    {
        /// <summary>
        /// 帮助中心入口 ID（AIHelp 后台已配置的真实 entrance，无欢迎语）。
        /// </summary>
        private const string HelpCenterEntranceId = "E001";

        /// <summary>
        /// 在线客服入口 ID（AIHelp 后台已配置的真实 entrance，带欢迎语）。
        /// </summary>
        private const string CustomerServiceEntranceId = "E002";

        /// <summary>
        /// 在线客服入口的欢迎语。
        /// </summary>
        private const string CustomerServiceWelcome = "Hello Solotopia Nova!";

        /// <summary>
        /// 演示用登录 uid。
        /// </summary>
        private const string DemoUid = "test_uid";

        /// <summary>
        /// 缓存的 AIHelpPlugin 引用，用于订阅 / 退订消息事件。
        /// </summary>
        private AIHelpPlugin m_AIHelp;

        /// <summary>
        /// 绑定按钮点击回调并设置就近 API 提示。
        /// </summary>
        /// <param name="button">目标按钮，可为 null（跳过）。</param>
        /// <param name="onClick">点击回调。</param>
        /// <param name="apiHint">按钮下 ApiHintText 显示的接口签名提示。</param>
        private void BindButton(Button button, UnityAction onClick, string apiHint)
        {
            if (button == null)
            {
                return;
            }
            button.onClick.RemoveAllListeners();
            button.onClick.AddListener(onClick);
            SetButtonApiHint(button, apiHint);
        }

        /// <summary>
        /// 获取 AIHelpPlugin；未获取到（未在 ConfigMaster 启用）时向反馈区打印引导并返回 false。
        /// </summary>
        /// <param name="plugin">输出的插件实例。</param>
        /// <returns>可用返回 true，否则 false。</returns>
        private bool TryGetAIHelp(out AIHelpPlugin plugin)
        {
            if (Nova.SDK.TryGet(out plugin) && plugin != null)
            {
                return true;
            }
            AppendFeedback("未获取到 AIHelpPlugin：请在 ConfigMaster 启用并配置该插件（Domain / AppId）。", FeedbackLevel.Error);
            return false;
        }

        /// <summary>
        /// 订阅 AIHelpPlugin 消息到达 / 未读工单数事件（先退后订防重复）。
        /// 未获取到插件时静默跳过（不打扰，点击具体按钮时再引导启用）。
        /// </summary>
        private void SubscribeAIHelpEvents()
        {
            if (!Nova.SDK.TryGet(out m_AIHelp) || m_AIHelp == null)
            {
                return;
            }
            m_AIHelp.OnMessageArrived -= OnMessageArrived;
            m_AIHelp.OnMessageArrived += OnMessageArrived;
            m_AIHelp.OnUnreadTaskCountChanged -= OnUnreadTaskCountChanged;
            m_AIHelp.OnUnreadTaskCountChanged += OnUnreadTaskCountChanged;
        }

        /// <summary>
        /// 退订 AIHelpPlugin 消息事件。
        /// </summary>
        private void UnsubscribeAIHelpEvents()
        {
            if (m_AIHelp == null)
            {
                return;
            }
            m_AIHelp.OnMessageArrived -= OnMessageArrived;
            m_AIHelp.OnUnreadTaskCountChanged -= OnUnreadTaskCountChanged;
        }

        /// <summary>
        /// 消息到达事件回调（AIHelpPlugin.OnMessageArrived）：FetchUnreadMessageCount() 的未读消息数结果
        /// 经此事件回传（对应 vendor EventType.MessageArrival），参数为原始 JSON 字符串。
        /// 演示：既打印到日志（真机可经 logcat 查看），也回显到屏幕反馈区。
        /// </summary>
        /// <param name="jsonEventData">vendor 回传的原始事件 JSON。</param>
        private void OnMessageArrived(string jsonEventData)
        {
            Log.Debug(LogTag.SDK, $"[AIHelpDemo] OnMessageArrived 回调触发，未读消息数据：{jsonEventData}");
            AppendFeedback($"OnMessageArrived → {jsonEventData}", FeedbackLevel.Success);
        }

        /// <summary>
        /// 未读工单数变化事件回调（AIHelpPlugin.OnUnreadTaskCountChanged）：FetchUnreadTaskCount() 的
        /// 未读工单数结果经此事件回传（对应 vendor EventType.UnreadTaskCount），参数为原始 JSON 字符串。
        /// 演示：既打印到日志（真机可经 logcat 查看），也回显到屏幕反馈区。
        /// </summary>
        /// <param name="jsonEventData">vendor 回传的原始事件 JSON。</param>
        private void OnUnreadTaskCountChanged(string jsonEventData)
        {
            Log.Debug(LogTag.SDK, $"[AIHelpDemo] OnUnreadTaskCountChanged 回调触发，未读工单数据：{jsonEventData}");
            AppendFeedback($"OnUnreadTaskCountChanged → {jsonEventData}", FeedbackLevel.Success);
        }

        /// <summary>
        /// 查看 SDK 版本与可用状态（GetSDKVersion() / IsAvailable）。
        /// </summary>
        private void OnInfoClick()
        {
            if (!TryGetAIHelp(out AIHelpPlugin plugin))
            {
                return;
            }
            string version = plugin.GetSDKVersion();
            AppendFeedback($"GetSDKVersion() → {version}；IsAvailable={plugin.IsAvailable}", FeedbackLevel.Info);
        }

        /// <summary>
        /// 拉起帮助中心页面（Show("E001")，无欢迎语）。Editor 下 vendor 为空操作，不弹真 UI，仅回显是否调用成功。
        /// </summary>
        private void OnHelpCenterClick()
        {
            if (!TryGetAIHelp(out AIHelpPlugin plugin))
            {
                return;
            }
            bool ok = plugin.Show(HelpCenterEntranceId);
            AppendFeedback($"已调用 Show(\"{HelpCenterEntranceId}\") → {ok}", ok ? FeedbackLevel.Success : FeedbackLevel.Warn);
        }

        /// <summary>
        /// 拉起在线客服页面（Show("E002", welcomeMessage)，带欢迎语）。Editor 下 vendor 为空操作，不弹真 UI，仅回显是否调用成功。
        /// </summary>
        private void OnCustomerServiceClick()
        {
            if (!TryGetAIHelp(out AIHelpPlugin plugin))
            {
                return;
            }
            bool ok = plugin.Show(CustomerServiceEntranceId, CustomerServiceWelcome);
            AppendFeedback($"已调用 Show(\"{CustomerServiceEntranceId}\", \"{CustomerServiceWelcome}\") → {ok}", ok ? FeedbackLevel.Success : FeedbackLevel.Warn);
        }

        /// <summary>
        /// 同步登录用户（Login），并同步用户资料（UpdateUserInfo：昵称 / 服务器 ID / 标签 / 自定义数据）。
        /// Editor 下 vendor 为空操作，仅回显已调用。
        /// </summary>
        private void OnLoginClick()
        {
            if (!TryGetAIHelp(out AIHelpPlugin plugin))
            {
                return;
            }
            plugin.Login(DemoUid);

            const string userName = "Nova123";
            const string serverId = "100123";
            var userTags = new List<string> { "recharge", "suggestion123" };
            const string customDataJson = "{\"a\":123,\"b\":\"https://www.baidu.com\"}";
            plugin.UpdateUserInfo(userName, serverId, userTags, customDataJson);

            AppendFeedback(
                $"已调用 Login(\"{DemoUid}\")；UpdateUserInfo(name=\"{userName}\", serverId=\"{serverId}\", tags=[{string.Join(",", userTags)}], customData={customDataJson})",
                FeedbackLevel.Success);
        }

        /// <summary>
        /// 查询未读消息数 / 未读工单数（FetchUnreadMessageCount / FetchUnreadTaskCount）。
        /// 结果经 vendor 异步事件回传，见 OnMessageArrived / OnUnreadTaskCountChanged 回显；
        /// Editor 下 vendor 为空操作，通常不会触发回调。
        /// </summary>
        private void OnFetchUnreadClick()
        {
            if (!TryGetAIHelp(out AIHelpPlugin plugin))
            {
                return;
            }
            plugin.FetchUnreadMessageCount();
            plugin.FetchUnreadTaskCount();
            AppendFeedback("已调用 FetchUnreadMessageCount() / FetchUnreadTaskCount()，结果经事件异步回显（见上方 OnMessageArrived / OnUnreadTaskCountChanged）。", FeedbackLevel.Info);
        }

        /// <summary>
        /// 关闭当前 AIHelp 页面（Close）。
        /// </summary>
        private void OnCloseAIHelpClick()
        {
            if (!TryGetAIHelp(out AIHelpPlugin plugin))
            {
                return;
            }
            plugin.Close();
            AppendFeedback("已调用 Close()", FeedbackLevel.Info);
        }
    }
}
