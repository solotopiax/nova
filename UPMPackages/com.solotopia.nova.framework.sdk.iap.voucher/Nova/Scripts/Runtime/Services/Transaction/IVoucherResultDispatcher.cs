/***************************************************************
 * (c) copyright 2026 - 2030, Solotopia
 * All Rights Reserved.
 * -------------------------------------------------------------
 * filename:  IVoucherResultDispatcher.cs
 * author:    yingzheng
 * created:   2026/8/3
 * descrip:   Voucher 交易终态结果派发端口
 ***************************************************************/

using NovaFramework.SDK.IAP.Runtime;

namespace NovaFramework.SDK.IAP.Voucher.Runtime
{
    /// <summary>
    /// Voucher 交易终态结果派发端口。
    /// 实现层负责将已经落盘的终态接入 IAP 事件桥。
    /// </summary>
    internal interface IVoucherResultDispatcher
    {
        /// <summary>
        /// 派发已经持久化终态的 IAP 结果。
        /// </summary>
        /// <param name="result">待派发的 IAP 结果。</param>
        void Dispatch(IAPResult result);
    }
}
