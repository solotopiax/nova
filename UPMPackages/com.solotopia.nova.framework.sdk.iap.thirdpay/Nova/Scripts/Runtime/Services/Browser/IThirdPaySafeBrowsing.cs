/***************************************************************
 * (c) copyright 2026 - 2030, Solotopia
 * All Rights Reserved.
 * -------------------------------------------------------------
 * filename:  IThirdPaySafeBrowsing.cs
 * author:    yingzheng
 * created:   2026/11/16
 * descrip:   系统浏览器页面生命周期合同
 ***************************************************************/

using System;
using System.Threading;
using Cysharp.Threading.Tasks;

namespace NovaFramework.SDK.IAP.ThirdPay.Runtime
{
    /// <summary>
    /// 只管理页面显示和关闭；支付 Deep Link 与订单规则由上层负责。
    /// </summary>
    internal interface IThirdPaySafeBrowsing : IDisposable
    {
        bool IsSupported { get; }
        event Action<long> UserDismissed;

        /// <summary>
        /// 在主线程显示页面；true 只证明展示完成，不证明网页加载或支付成功。
        /// </summary>
        UniTask<bool> ShowAsync(string url, long sessionId, CancellationToken ct);

        /// <summary>
        /// 关闭指定会话，包含展示尚未完成的情况；不派发用户关闭事件。
        /// </summary>
        void Dismiss(long sessionId);
    }
}
