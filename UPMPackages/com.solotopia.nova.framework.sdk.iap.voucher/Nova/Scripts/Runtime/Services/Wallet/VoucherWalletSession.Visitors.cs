/***************************************************************
 * (c) copyright 2026 - 2030, Solotopia
 * All Rights Reserved.
 * -------------------------------------------------------------
 * filename:  VoucherWalletSession.Visitors.cs
 * author:    yingzheng
 * created:   2026/8/3
 * descrip:   VoucherWalletSession 字段与属性
 ***************************************************************/

using System.Threading;
using Cysharp.Threading.Tasks;
using NovaFramework.SDK.IAP.Runtime;

namespace NovaFramework.SDK.IAP.Voucher.Runtime
{
    /// <summary>
    /// VoucherWalletSession 字段与属性。
    /// </summary>
    internal sealed partial class VoucherWalletSession
    {
        /// <summary>
        /// 保护账号 generation、钱包和刷新任务的同步锁。
        /// </summary>
        private readonly object m_Gate = new object();

        /// <summary>
        /// 当前账号作用域取消令牌源。
        /// </summary>
        private CancellationTokenSource m_AccountCancellation = new CancellationTokenSource();

        /// <summary>
        /// 当前账号内部不可变钱包。
        /// </summary>
        private VoucherWalletData m_Current = VoucherWalletData.NotReady(string.Empty, 0);

        /// <summary>
        /// 当前 generation 正在执行的钱包刷新任务。
        /// </summary>
        private UniTask<VoucherRefreshResult> m_RefreshTask;

        /// <summary>
        /// 当前共享刷新任务所属的账号 generation。
        /// </summary>
        private long m_RefreshGeneration = -1;

        /// <summary>
        /// 是否已经记录共享刷新任务。
        /// </summary>
        private bool m_HasRefreshTask;

        /// <summary>
        /// 当前账号 generation。
        /// </summary>
        private long m_Generation;

        /// <summary>
        /// 当前钱包会话是否已经释放。
        /// </summary>
        private bool m_Disposed;

        /// <summary>
        /// 当前内部钱包。
        /// </summary>
        internal VoucherWalletData Current
        {
            get
            {
                lock (m_Gate)
                {
                    return m_Current;
                }
            }
        }

        /// <summary>
        /// 当前对外不可变钱包快照。
        /// </summary>
        internal VoucherWalletSnapshot Wallet
        {
            get
            {
                lock (m_Gate)
                {
                    return m_Current.ToPublicSnapshot();
                }
            }
        }
    }
}
