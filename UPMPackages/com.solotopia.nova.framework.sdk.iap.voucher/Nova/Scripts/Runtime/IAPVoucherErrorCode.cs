/***************************************************************
 * (c) copyright 2026 - 2030, Solotopia
 * All Rights Reserved.
 * -------------------------------------------------------------
 * filename:  IAPVoucherErrorCode.cs
 * author:    yingzheng
 * created:   2026/6/5
 * descrip:   Voucher store 专属错误码，从 0 起编
 ***************************************************************/

namespace NovaFramework.SDK.IAP.Runtime
{
    /// <summary>
    /// Voucher Store 对外错误码。
    /// </summary>
    public enum IAPVoucherErrorCode
    {
        /// <summary>
        /// 没有错误。
        /// </summary>
        None = 0,

        /// <summary>
        /// 当前账号的钱包尚未准备完成。
        /// </summary>
        WalletNotReady = 1,

        /// <summary>
        /// 商品价格不是可支付的正 mills 整数。
        /// </summary>
        InvalidPrice = 2,

        /// <summary>
        /// 当前资产无法精确覆盖商品价格。
        /// </summary>
        InsufficientBalance = 3,

        /// <summary>
        /// 报价所属账号或钱包版本已经变化。
        /// </summary>
        StaleQuote = 4,

        /// <summary>
        /// 当前账号存在结果未知、等待恢复的交易。
        /// </summary>
        TransactionPending = 5,

        /// <summary>
        /// 网络请求失败，可在后续恢复原订单。
        /// </summary>
        NetworkError = 6,

        /// <summary>
        /// 服务端明确拒绝本次抵扣。
        /// </summary>
        ServerRejected = 7,

        /// <summary>
        /// 本地交易日志无法持久化。
        /// </summary>
        JournalFailure = 8,

        /// <summary>
        /// 服务端响应无法被安全分类。
        /// </summary>
        ProtocolError = 9,

        /// <summary>
        /// 异步响应所属账号已经切换，结果未发布。
        /// </summary>
        StaleAccount = 10,

        /// <summary>
        /// 测试发放协议未配置或当前环境不允许使用。
        /// </summary>
        TestGrantUnavailable = 11,
    }
}
