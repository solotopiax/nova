/***************************************************************
 * (c) copyright 2026 - 2030, Solotopia
 * All Rights Reserved.
 * -------------------------------------------------------------
 * filename:  IIAPVoucherCapable.cs
 * author:    yingzheng
 * created:   2026/8/3
 * descrip:   Voucher 钱包刷新与不可变报价能力接口
 ***************************************************************/

using System.Threading;
using Cysharp.Threading.Tasks;

namespace NovaFramework.SDK.IAP.Runtime
{
    /// <summary>
    /// Voucher Store 暴露给业务层的最小能力接口。
    /// </summary>
    public interface IIAPVoucherCapable : IIAPCapable
    {
        /// <summary>
        /// 当前账号可见的不可变钱包快照。
        /// </summary>
        VoucherWalletSnapshot Wallet { get; }

        /// <summary>
        /// 异步刷新当前账号钱包；账号切换后的迟到响应不会发布。
        /// </summary>
        /// <param name="ct">取消令牌。</param>
        /// <returns>刷新结果及刷新后当前可见快照。</returns>
        UniTask<VoucherRefreshResult> RefreshWalletAsync(CancellationToken ct = default);

        /// <summary>
        /// 基于当前钱包计算精确覆盖价格的不可变报价。
        /// </summary>
        /// <param name="tableId">商品配置表行 ID。</param>
        /// <param name="priceMills">商品价格，单位为 mills。</param>
        /// <returns>显式携带 Ready 或失败状态的报价。</returns>
        VoucherQuote Quote(long tableId, long priceMills);
    }
}
