/***************************************************************
 * (c) copyright 2026 - 2030, Solotopia
 * All Rights Reserved.
 * -------------------------------------------------------------
 * filename:  VoucherWalletSession.cs
 * author:    yingzheng
 * created:   2026/8/3
 * descrip:   Voucher 账号 generation 与钱包发布入口
 ***************************************************************/

using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using Cysharp.Threading.Tasks;
using NovaFramework.SDK.IAP.Runtime;

namespace NovaFramework.SDK.IAP.Voucher.Runtime
{
    /// <summary>
    /// Voucher 钱包会话。
    /// 维护账号 generation、取消令牌和不可变钱包版本。
    /// </summary>
    internal sealed partial class VoucherWalletSession : IDisposable
    {
        /// <summary>
        /// 切换账号，并在取消旧账号请求后同步清空可见钱包。
        /// </summary>
        /// <param name="accountId">新账号 ID。</param>
        /// <returns>新账号的异步请求作用域。</returns>
        internal VoucherSessionScope SwitchAccount(string accountId)
        {
            lock (m_Gate)
            {
                ThrowIfDisposed();
                string normalizedAccountId = accountId ?? string.Empty;
                if (string.Equals(m_Current.AccountId, normalizedAccountId, StringComparison.Ordinal))
                {
                    return new VoucherSessionScope(normalizedAccountId, m_Generation, m_AccountCancellation.Token);
                }

                m_Generation++;
                m_AccountCancellation.Cancel();
                m_AccountCancellation.Dispose();
                m_AccountCancellation = new CancellationTokenSource();
                m_Current = VoucherWalletData.NotReady(normalizedAccountId, m_Generation);
                m_HasRefreshTask = false;
                return new VoucherSessionScope(normalizedAccountId, m_Generation, m_AccountCancellation.Token);
            }
        }

        /// <summary>
        /// 合并同一账号 generation 内重叠的钱包刷新请求。
        /// </summary>
        /// <param name="refresh">执行单次钱包刷新的异步方法。</param>
        /// <returns>当前 generation 共享的钱包刷新任务。</returns>
        /// <exception cref="ArgumentNullException">刷新方法为空时抛出。</exception>
        internal UniTask<VoucherRefreshResult> RunMergedRefreshAsync(Func<VoucherSessionScope, UniTask<VoucherRefreshResult>> refresh)
        {
            if (refresh == null)
            {
                throw new ArgumentNullException(nameof(refresh));
            }

            lock (m_Gate)
            {
                ThrowIfDisposed();
                if (m_HasRefreshTask && m_RefreshGeneration == m_Generation && !m_RefreshTask.Status.IsCompleted())
                {
                    return m_RefreshTask;
                }

                var scope = new VoucherSessionScope(m_Current.AccountId, m_Generation, m_AccountCancellation.Token);
                m_RefreshGeneration = m_Generation;
                m_RefreshTask = refresh(scope).Preserve();
                m_HasRefreshTask = true;
                return m_RefreshTask;
            }
        }

        /// <summary>
        /// 捕获当前账号的异步请求作用域。
        /// </summary>
        /// <returns>当前账号 ID、generation 和取消令牌。</returns>
        internal VoucherSessionScope CaptureScope()
        {
            lock (m_Gate)
            {
                ThrowIfDisposed();
                return new VoucherSessionScope(m_Current.AccountId, m_Generation, m_AccountCancellation.Token);
            }
        }

        /// <summary>
        /// 在账号 ID 和 generation 同时匹配时发布新钱包，并递增钱包版本。
        /// </summary>
        /// <param name="scope">发起请求时捕获的账号作用域。</param>
        /// <param name="vouchers">服务端返回的礼券资产。</param>
        /// <param name="coins">服务端返回的赠币资产。</param>
        /// <param name="refreshedAtUnixTimeMs">钱包刷新完成的 UTC 毫秒时间戳。</param>
        /// <returns>新钱包成功发布时返回 true，迟到或旧账号响应返回 false。</returns>
        internal bool TryPublish(VoucherSessionScope scope, IEnumerable<VoucherAssetData> vouchers, IEnumerable<CoinAssetData> coins, long refreshedAtUnixTimeMs)
        {
            if (scope == null)
            {
                return false;
            }

            lock (m_Gate)
            {
                if (m_Disposed || scope.Generation != m_Generation || !string.Equals(scope.AccountId, m_Current.AccountId, StringComparison.Ordinal))
                {
                    return false;
                }

                long nextVersion = m_Current.Version + 1;
                m_Current = VoucherWalletData.Ready(scope.AccountId, scope.Generation, nextVersion, refreshedAtUnixTimeMs, vouchers?.OrderBy(item => item.VoucherTierId), coins?.OrderBy(item => item.CoinId));
                return true;
            }
        }

        /// <summary>
        /// 判断报价是否仍绑定当前账号 generation 和钱包版本。
        /// </summary>
        /// <param name="quote">待验证的 Voucher 报价。</param>
        /// <returns>报价仍属于当前钱包时返回 true，否则返回 false。</returns>
        internal bool IsQuoteCurrent(VoucherQuote quote)
        {
            if (quote == null || quote.Status != VoucherQuoteStatus.Ready)
            {
                return false;
            }

            lock (m_Gate)
            {
                return !m_Disposed && m_Current.IsReady && string.Equals(quote.AccountId, m_Current.AccountId, StringComparison.Ordinal) && quote.AccountGeneration == m_Generation && quote.WalletVersion == m_Current.Version;
            }
        }

        /// <summary>
        /// 释放当前账号取消令牌并阻止后续钱包发布。
        /// </summary>
        public void Dispose()
        {
            lock (m_Gate)
            {
                if (m_Disposed)
                {
                    return;
                }

                m_Disposed = true;
                m_AccountCancellation.Cancel();
                m_AccountCancellation.Dispose();
                m_HasRefreshTask = false;
                m_Current = VoucherWalletData.NotReady(string.Empty, ++m_Generation);
            }
        }
    }
}
