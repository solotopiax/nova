/***************************************************************
 * (c) copyright 2026 - 2030, Solotopia
 * All Rights Reserved.
 * -------------------------------------------------------------
 * filename:  DemoIAPBridge.Event.cs
 * author:    nova-create-sample
 * created:   2026/06/05
 * descrip:   DemoIAPView IAP 调度桥接层 - 事件监听
 ***************************************************************/

using System.Collections.Generic;
using NovaFramework.SDK.IAP.Runtime;

using FeedbackLevel = NovaFramework.Sdk.IAP.Samples.Runtime.BaseDemoView.FeedbackLevel;

namespace NovaFramework.Sdk.IAP.Samples.Runtime
{
    internal sealed partial class DemoIAPBridge
    {
        /// <summary>
        /// 订阅 IAPPlugin 事件。
        /// </summary>
        private void SubscribeEvents()
        {
            if (m_EventsSubscribed || m_IAP == null)
            {
                return;
            }

            m_IAP.Events.InitResult.Subscribe(OnInitResult);
            m_IAP.Events.PaySuccess.Subscribe(OnPaySuccess);
            m_IAP.Events.PayFailed.Subscribe(OnPayFailed);
            m_IAP.Events.SubscriptionRestored.Subscribe(OnSubscriptionRestored);
            m_IAP.Events.NonConsumeRestored.Subscribe(OnNonConsumeRestored);
            m_EventsSubscribed = true;
        }

        /// <summary>
        /// 取消当前 bridge 持有的 IAPPlugin 事件订阅。
        /// </summary>
        private void UnsubscribeEvents()
        {
            if (!m_EventsSubscribed || m_IAP == null)
            {
                return;
            }

            m_IAP.Events.InitResult.Unsubscribe(OnInitResult);
            m_IAP.Events.PaySuccess.Unsubscribe(OnPaySuccess);
            m_IAP.Events.PayFailed.Unsubscribe(OnPayFailed);
            m_IAP.Events.SubscriptionRestored.Unsubscribe(OnSubscriptionRestored);
            m_IAP.Events.NonConsumeRestored.Unsubscribe(OnNonConsumeRestored);
            m_EventsSubscribed = false;
        }

        /// <summary>
        /// IAPPlugin.Events.InitResult 全局事件回调；
        /// 反馈区前缀【全局事件】用于和直接 await 调用的返回结果区分。
        /// </summary>
        /// <param name="result">初始化结果。</param>
        private void OnInitResult(IAPInitResult result)
        {
            if (result == null)
            {
                AppendFeedback("【全局事件】IAPPlugin.Events.InitResult：空结果。", FeedbackLevel.Warn);
                return;
            }

            if (result.IsSuccess)
            {
                AppendFeedback("【全局事件】IAPPlugin.Events.InitResult：初始化成功。", FeedbackLevel.Success);
                return;
            }

            AppendFeedback("【全局事件】IAPPlugin.Events.InitResult：初始化失败，Reason=" + result.FailReason + "，Detail=" + result.Detail, FeedbackLevel.Error);
        }

        /// <summary>
        /// IAPPlugin.Events.PaySuccess 全局事件回调；
        /// 任意位置发起的支付成功都会进入此回调，反馈区前缀【全局事件】用于和发起调用本地的 PayMobileAsync 返回值区分。
        /// </summary>
        /// <param name="result">支付结果。</param>
        private void OnPaySuccess(IAPResult result)
        {
            AppendFeedback("【全局事件】IAPPlugin.Events.PaySuccess：" + FormatResult(result), FeedbackLevel.Success);
        }

        /// <summary>
        /// IAPPlugin.Events.PayFailed 全局事件回调；
        /// 任意位置发起的支付失败都会进入此回调，反馈区前缀【全局事件】用于和发起调用本地的 PayMobileAsync 返回值区分。
        /// </summary>
        /// <param name="result">支付结果。</param>
        private void OnPayFailed(IAPResult result)
        {
            AppendFeedback("【全局事件】IAPPlugin.Events.PayFailed：" + FormatResult(result), FeedbackLevel.Error);
        }

        /// <summary>
        /// IAPPlugin.Events.SubscriptionRestored 全局事件回调；
        /// 由插件自动恢复或外部主动调用 RestorePurchasesAsync 都会触发此事件。
        /// </summary>
        /// <param name="results">恢复的订阅结果列表。</param>
        private void OnSubscriptionRestored(IReadOnlyList<IAPResult> results)
        {
            AppendRestoreFeedback("【全局事件】IAPPlugin.Events.SubscriptionRestored", results);
        }

        /// <summary>
        /// IAPPlugin.Events.NonConsumeRestored 全局事件回调；
        /// 由插件自动恢复或外部主动调用 RestorePurchasesAsync 都会触发此事件。
        /// </summary>
        /// <param name="results">恢复的非消耗品结果列表。</param>
        private void OnNonConsumeRestored(IReadOnlyList<IAPResult> results)
        {
            AppendRestoreFeedback("【全局事件】IAPPlugin.Events.NonConsumeRestored", results);
        }

        /// <summary>
        /// 将一批恢复结果按标题汇总并逐条追加到反馈区。
        /// </summary>
        /// <param name="title">汇总标题。</param>
        /// <param name="results">恢复结果列表。</param>
        private void AppendRestoreFeedback(string title, IReadOnlyList<IAPResult> results)
        {
            int count = results != null ? results.Count : 0;
            AppendFeedback(title + "：count=" + count, count > 0 ? FeedbackLevel.Success : FeedbackLevel.Info);
            if (results == null)
            {
                return;
            }

            for (int i = 0; i < results.Count; i++)
            {
                IAPResult result = results[i];
                AppendFeedback("  [" + i + "] " + FormatResult(result), result != null && result.IsSuccess ? FeedbackLevel.Success : FeedbackLevel.Warn);
            }
        }
    }
}
