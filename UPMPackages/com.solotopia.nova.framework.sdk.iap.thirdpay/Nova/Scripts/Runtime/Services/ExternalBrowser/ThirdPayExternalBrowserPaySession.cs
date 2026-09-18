/***************************************************************
 * (c) copyright 2026 - 2030, Solotopia
 * All Rights Reserved.
 * -------------------------------------------------------------
 * filename:  ThirdPayExternalBrowserPaySession.cs
 * author:    yingzheng
 * created:   2026/8/27
 * descrip:   ThirdPay 外部浏览器支付运行期会话
 ***************************************************************/

using System;
using System.Threading;
using Cysharp.Threading.Tasks;
using NovaFramework.SDK.IAP.Runtime;

namespace NovaFramework.SDK.IAP.ThirdPay.Runtime
{
    /// <summary>
    /// 单笔外部浏览器支付的运行期状态。
    /// </summary>
    internal sealed class ThirdPayExternalBrowserPaySession : IDisposable
    {
        /// <summary>
        /// 初始化外部浏览器支付会话。
        /// </summary>
        /// <param name="clientOrderId">客户端订单号。</param>
        /// <param name="tableId">支付商品表行 ID。</param>
        /// <param name="userId">订单创建时的用户 UID。</param>
        /// <param name="customData">支付请求透传数据。</param>
        /// <param name="receiptParam">票据透传参数。</param>
        public ThirdPayExternalBrowserPaySession(string clientOrderId, long tableId, string userId, string customData, string receiptParam)
        {
            ClientOrderId = clientOrderId;
            TableId = tableId;
            UserId = userId ?? string.Empty;
            CustomData = customData ?? string.Empty;
            ReceiptParam = receiptParam ?? string.Empty;
            SessionCts = new CancellationTokenSource();
        }

        /// <summary>
        /// 客户端订单号。
        /// </summary>
        public string ClientOrderId { get; }

        /// <summary>
        /// 支付商品表行 ID。
        /// </summary>
        public long TableId { get; }

        /// <summary>
        /// 订单创建时的用户 UID。
        /// </summary>
        public string UserId { get; }

        /// <summary>
        /// 支付请求透传数据，返回验单完成时回传给 PayAsync 调用方。
        /// </summary>
        public string CustomData { get; }

        /// <summary>
        /// 票据透传参数，返回验单完成时回传给 PayAsync 调用方。
        /// </summary>
        public string ReceiptParam { get; }

        /// <summary>
        /// 外部浏览器返回验单结果完成源，用于桥接生命周期回调到 PayAsync await 点。
        /// </summary>
        public UniTaskCompletionSource<IAPResult> PayTcs { get; } = new UniTaskCompletionSource<IAPResult>();

        /// <summary>
        /// 当前浏览器支付是否已经触发过离开 App。
        /// </summary>
        public bool HasLeftApp;

        /// <summary>
        /// 当前会话是否已经进入验单流程。
        /// </summary>
        public bool IsValidating;

        /// <summary>
        /// 当前会话是否已经完成或被清理。
        /// </summary>
        public bool Completed;

        /// <summary>
        /// 返回 App 后的倒计时等待期是否已经持有 Loading 引用。
        /// </summary>
        public bool ReturnWaitingRefAdded;

        /// <summary>
        /// App 返回前台的版本号；每次稳定返回前台都会递增，用于让旧延迟任务失效。
        /// </summary>
        public int ReturnVersion;

        /// <summary>
        /// 会话级取消源；会话清理时取消验单和延迟任务。
        /// </summary>
        public CancellationTokenSource SessionCts;

        /// <summary>
        /// 当前返回前台后的延迟验单取消源；反复切前后台时会被替换。
        /// </summary>
        public CancellationTokenSource DelayCts;

        /// <summary>
        /// 取消并释放当前返回前台延迟验单计时器。
        /// </summary>
        public void CancelReturnDelay()
        {
            CancellationTokenSource delayCts = DelayCts;
            if (delayCts == null)
            {
                return;
            }

            DelayCts = null;
            if (!delayCts.IsCancellationRequested)
            {
                delayCts.Cancel();
            }

            delayCts.Dispose();
        }

        /// <summary>
        /// 释放会话取消源和延迟验单计时器。
        /// </summary>
        public void Dispose()
        {
            CancelReturnDelay();
            if (SessionCts == null)
            {
                return;
            }

            if (!SessionCts.IsCancellationRequested)
            {
                SessionCts.Cancel();
            }

            SessionCts.Dispose();
            SessionCts = null;
        }
    }
}
