/***************************************************************
 * (c) copyright 2026 - 2030, Solotopia
 * All Rights Reserved.
 * -------------------------------------------------------------
 * filename:  VoucherWalletSession.Methods.cs
 * author:    yingzheng
 * created:   2026/8/3
 * descrip:   VoucherWalletSession 非公开方法
 ***************************************************************/

using System;

namespace NovaFramework.SDK.IAP.Voucher.Runtime
{
    /// <summary>
    /// VoucherWalletSession 非公开方法。
    /// </summary>
    internal sealed partial class VoucherWalletSession
    {
        /// <summary>
        /// 阻止在已经释放的钱包会话上继续捕获或切换账号作用域。
        /// </summary>
        /// <exception cref="ObjectDisposedException">钱包会话已经释放时抛出。</exception>
        private void ThrowIfDisposed()
        {
            if (m_Disposed)
            {
                throw new ObjectDisposedException(nameof(VoucherWalletSession));
            }
        }
    }
}
