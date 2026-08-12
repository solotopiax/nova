/***************************************************************
 * (c) copyright 2026 - 2030, Solotopia
 * All Rights Reserved.
 * -------------------------------------------------------------
 * filename:  VoucherStore.Methods.cs
 * author:    yingzheng
 * created:   2026/8/3
 * descrip:   VoucherStore 非公开方法
 ***************************************************************/

using System;
using System.Threading;
using Cysharp.Threading.Tasks;
using NovaFramework.SDK.IAP.Runtime;

namespace NovaFramework.SDK.IAP.Voucher.Runtime
{
    /// <summary>
    /// VoucherStore 非公开方法。
    /// </summary>
    public sealed partial class VoucherStore
    {
        /// <summary>
        /// 执行一次账号作用域内的钱包加载，并拒绝发布旧账号的迟到响应。
        /// </summary>
        /// <param name="scope">发起刷新时捕获的账号作用域。</param>
        /// <returns>本次钱包刷新结果。</returns>
        private async UniTask<VoucherRefreshResult> RefreshWalletCoreAsync(VoucherSessionScope scope)
        {
            try
            {
                VoucherGatewayWalletResult result = await m_Gateway.FetchWalletAsync(scope.Token);
                if (!result.IsSuccess)
                {
                    return new VoucherRefreshResult(false, result.ErrorCode, result.Message, Wallet);
                }

                bool published = m_Session.TryPublish(scope, result.Vouchers, result.Coins, DateTimeOffset.UtcNow.ToUnixTimeMilliseconds());
                return published ? new VoucherRefreshResult(true, IAPVoucherErrorCode.None, string.Empty, Wallet) : new VoucherRefreshResult(false, IAPVoucherErrorCode.StaleQuote, "Voucher 钱包响应属于旧账号 generation，已丢弃。", Wallet);
            }
            catch (OperationCanceledException)
            {
                return new VoucherRefreshResult(false, IAPVoucherErrorCode.NetworkError, "Voucher 钱包刷新已取消。", Wallet);
            }
            catch (Exception exception)
            {
                return new VoucherRefreshResult(false, IAPVoucherErrorCode.NetworkError, exception.Message, Wallet);
            }
        }

        /// <summary>
        /// 在捕获的账号作用域内执行一次测试发放，并拒绝发布迟到响应。
        /// </summary>
        /// <param name="request">测试发放请求。</param>
        /// <param name="scope">发起请求时捕获的账号作用域。</param>
        /// <param name="ct">调用方取消令牌。</param>
        /// <returns>测试发放结果及调用完成后的当前钱包。</returns>
        private async UniTask<VoucherTestGrantResult> TestGrantCoreAsync(VoucherTestGrantRequest request, VoucherSessionScope scope, CancellationToken ct)
        {
            using (var linkedCancellation = CancellationTokenSource.CreateLinkedTokenSource(ct, scope.Token))
            {
                try
                {
                    VoucherGatewayWalletResult result = await m_Gateway.TestGrantAsync(request, linkedCancellation.Token);
                    if (!result.IsSuccess)
                    {
                        return new VoucherTestGrantResult(false, result.ErrorCode, result.Message, Wallet);
                    }

                    bool published = m_Session.TryPublish(scope, result.Vouchers, result.Coins, DateTimeOffset.UtcNow.ToUnixTimeMilliseconds());
                    return published ? new VoucherTestGrantResult(true, IAPVoucherErrorCode.None, result.Message, Wallet) : new VoucherTestGrantResult(false, IAPVoucherErrorCode.StaleAccount, "Voucher 测试发放响应属于旧账号 generation，已丢弃。", Wallet);
                }
                catch (OperationCanceledException)
                {
                    bool accountChanged = scope.Token.IsCancellationRequested && !ct.IsCancellationRequested;
                    return new VoucherTestGrantResult(false, accountChanged ? IAPVoucherErrorCode.StaleAccount : IAPVoucherErrorCode.NetworkError, accountChanged ? "Voucher 测试发放所属账号已经切换。" : "Voucher 测试发放已取消，服务端结果未知。", Wallet);
                }
                catch (Exception exception)
                {
                    return new VoucherTestGrantResult(false, IAPVoucherErrorCode.NetworkError, exception.Message, Wallet);
                }
            }
        }

        /// <summary>
        /// 创建使用当前交易日志结构的空 Voucher 持久化容器。
        /// </summary>
        /// <returns>完成初始化的 Voucher 持久化容器。</returns>
        protected override IIAPStorePersistData CreateEmptyPersistData()
        {
            var data = new VoucherStorePersistData();
            data.EnsureInitialized();
            return data;
        }

        /// <summary>
        /// 派发已经持久化的成功或明确拒绝终态。
        /// </summary>
        /// <param name="result">待派发的 IAP 支付结果。</param>
        void IVoucherResultDispatcher.Dispatch(IAPResult result)
        {
            if (result.IsSuccess)
            {
                Context?.EventBridge?.RaisePaySuccess(result);
            }
            else
            {
                Context?.EventBridge?.RaisePayFailed(result);
            }
        }

        /// <summary>
        /// 使用当前账号持久化数据重新创建交易日志和交易协调器。
        /// </summary>
        private void BuildCoordinator()
        {
            if (m_PersistData == null || m_Session == null || m_Gateway == null)
            {
                m_Journal = null;
                m_Coordinator = null;
                return;
            }

            m_Journal = new VoucherTransactionJournal(m_PersistData, SavePersistData);
            m_Coordinator = new VoucherTransactionCoordinator(m_Gateway, m_Journal, m_Session, this, () => Guid.NewGuid().ToString("N"), () => DateTimeOffset.UtcNow.ToUnixTimeMilliseconds(), () => string.Empty);
        }
    }
}
