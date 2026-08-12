/***************************************************************
 * (c) copyright 2026 - 2030, Solotopia
 * All Rights Reserved.
 * -------------------------------------------------------------
 * filename:  VoucherSessionScope.cs
 * author:    yingzheng
 * created:   2026/8/3
 * descrip:   Voucher 账号 generation 异步作用域
 ***************************************************************/

using System.Threading;

namespace NovaFramework.SDK.IAP.Voucher.Runtime
{
    /// <summary>
    /// Voucher 单次账号 generation 的异步请求作用域。
    /// </summary>
    internal sealed class VoucherSessionScope
    {
        /// <summary>
        /// 发起请求时的账号 ID。
        /// </summary>
        internal string AccountId { get; }

        /// <summary>
        /// 发起请求时的账号 generation。
        /// </summary>
        internal long Generation { get; }

        /// <summary>
        /// 当前账号作用域的取消令牌。
        /// </summary>
        internal CancellationToken Token { get; }

        /// <summary>
        /// 创建 Voucher 账号异步作用域。
        /// </summary>
        /// <param name="accountId">账号 ID。</param>
        /// <param name="generation">账号 generation。</param>
        /// <param name="token">账号作用域取消令牌。</param>
        internal VoucherSessionScope(string accountId, long generation, CancellationToken token)
        {
            AccountId = accountId ?? string.Empty;
            Generation = generation;
            Token = token;
        }
    }
}
