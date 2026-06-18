/***************************************************************
 * (c) copyright 2026 - 2030, Solotopia
 * All Rights Reserved.
 * -------------------------------------------------------------
 * filename:  ThirdPayStore.Browser.cs
 * author:    yingzheng
 * created:   2026/5/26
 * descrip:   ThirdPayStore Browser 模式默认 OpenAsync 实现 + DeepLink 回调入口
 ***************************************************************/

using System;
using System.Threading;
using Cysharp.Threading.Tasks;
using NovaFramework.Runtime;
using UnityEngine;

namespace NovaFramework.SDK.IAP.ThirdPay.Runtime
{
    public partial class ThirdPayStore
    {
        /// <summary>
        /// AppsFlyer DeepLink 回调入口。
        /// 业务层在收到 OneLink 回流后调用本方法；命中本地未完成订单 → 唤醒等待中的 OpenBrowserAsync 返回 Success。
        /// </summary>
        /// <param name="orderId">DeepLink 参数解析出的订单 ID。</param>
        public void OnBrowserDeepLinkResolved(string orderId)
        {
            if (string.IsNullOrEmpty(orderId))
                return;
            if (m_PersistData == null || m_PersistData.OrderingStates == null)
                return;

            bool hit = false;
            foreach (var kv in m_PersistData.OrderingStates)
            {
                if (kv.Value != null && kv.Value.ClientOrderId == orderId)
                {
                    hit = true;
                    break;
                }
            }
            if (!hit)
            {
                Log.Warning(LogTag.IAPThirdPay, $"DeepLink 未命中本地订单 orderId={orderId}，忽略。");
                return;
            }

            m_BrowserOpenWaiter?.TrySetResult(ThirdPayOpenResult.Success);
        }

        /// <summary>
        /// Browser 模式默认 OpenAsync 实现：跳系统浏览器 + 等待 DeepLink 命中或超时。
        /// </summary>
        /// <param name="url">已构造好的 AES 加密支付 URL。</param>
        /// <param name="ct">取消令牌。</param>
        /// <returns>三态打开结果。</returns>
        private async UniTask<ThirdPayOpenResult> OpenBrowserAsync(string url, CancellationToken ct)
        {
            try
            {
                Application.OpenURL(url);
            }
            catch (Exception ex)
            {
                Log.Warning(LogTag.IAPThirdPay, $"Application.OpenURL 失败：{ex.Message}");
                return ThirdPayOpenResult.Failed;
            }

            m_BrowserOpenWaiter = new UniTaskCompletionSource<ThirdPayOpenResult>();
            try
            {
                var timeout = UniTask.Delay(TimeSpan.FromSeconds(c_BrowserWaitTimeoutSeconds), cancellationToken: ct);
                var (hasResult, result) = await UniTask.WhenAny(m_BrowserOpenWaiter.Task, timeout);
                return hasResult ? result : ThirdPayOpenResult.Failed;
            }
            finally
            {
                m_BrowserOpenWaiter = null;
            }
        }
    }
}
