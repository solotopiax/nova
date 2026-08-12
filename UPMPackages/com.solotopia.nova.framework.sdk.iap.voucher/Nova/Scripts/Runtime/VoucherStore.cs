/***************************************************************
 * (c) copyright 2026 - 2030, Solotopia
 * All Rights Reserved.
 * -------------------------------------------------------------
 * filename:  VoucherStore.cs
 * author:    yingzheng
 * created:   2026/8/3
 * descrip:   Voucher IAP Store 对外入口与生命周期实现
 ***************************************************************/

using System;
using System.Threading;
using Cysharp.Threading.Tasks;
using NovaFramework.Runtime;
using NovaFramework.SDK.IAP.Runtime;

namespace NovaFramework.SDK.IAP.Voucher.Runtime
{
    /// <summary>
    /// Voucher IAP Store。
    /// 负责 IAP 生命周期接入与公开能力转发，内部业务由钱包、报价和交易服务完成。
    /// </summary>
    [IAPStore]
    public sealed partial class VoucherStore : IAPStoreBase, IIAPVoucherCapable, IIAPVoucherTestCapable, IVoucherResultDispatcher
    {
        /// <summary>
        /// 初始化 Voucher Store，并创建钱包、协议和交易服务。
        /// </summary>
        /// <param name="table">IAP 商品表。</param>
        /// <param name="config">Voucher Store 配置。</param>
        /// <param name="ctx">IAP Store 运行时上下文。</param>
        /// <param name="ct">取消令牌。</param>
        /// <returns>初始化完成的异步任务。</returns>
        public override async UniTask InitializeAsync(IIAPProductTable table, IIAPStoreConfig config, IIAPStoreContext ctx, CancellationToken ct)
        {
            await base.InitializeAsync(table, config, ctx, ct);

            m_Config = config as VoucherStoreConfig;
            m_Session?.Dispose();
            m_Session = new VoucherWalletSession();
            var netService = new VoucherIapNetService();
            m_Gateway = new ProtobufVoucherGateway(netService, m_Config?.GetVoucherListCmdName, m_Config?.DeductVoucherCmdName, m_Config?.TestGrantVoucherCmdName);
            m_PersistData = (VoucherStorePersistData)CreateEmptyPersistData();
            BuildCoordinator();
        }

        /// <summary>
        /// 切换当前账号，并为新账号重新加载交易日志、恢复订单和刷新钱包。
        /// </summary>
        /// <param name="uid">当前登录用户的唯一 ID。</param>
        public override void SetUserId(string uid)
        {
            string previousUid = m_GameUID;
            base.SetUserId(uid);
            if (string.IsNullOrEmpty(m_GameUID) || string.Equals(previousUid, m_GameUID, StringComparison.Ordinal))
            {
                return;
            }

            m_Session.SwitchAccount(m_GameUID);
            m_PersistData = LoadPersistData<VoucherStorePersistData>();
            BuildCoordinator();
            CheckLocalOrdersAsync(CancellationToken.None).Forget();
            RefreshWalletAsync(CancellationToken.None).Forget();
        }

        /// <summary>
        /// 判断当前 Store 是否能够处理指定支付请求。
        /// </summary>
        /// <param name="request">待处理的支付请求。</param>
        /// <returns>请求为 Voucher 支付请求时返回 true，否则返回 false。</returns>
        public override bool CanHandle(IAPRequest request) => request is IAPVoucherRequest;

        /// <summary>
        /// 校验 Voucher 报价并将支付交给可恢复交易协调器。
        /// </summary>
        /// <param name="request">绑定不可变 Voucher 报价的支付请求。</param>
        /// <param name="ct">取消令牌。</param>
        /// <returns>本次 Voucher 支付结果。</returns>
        public override UniTask<IAPResult> PayAsync(IAPRequest request, CancellationToken ct)
        {
            ct.ThrowIfCancellationRequested();

#if UNITY_EDITOR
            if (Context?.EnableAlwaysPaySucceed == true)
            {
                var mockResult = new IAPResult(request.TableId, $"MOCK_VOUCHER_{Guid.NewGuid():N}", false, true, request.CustomData);
                Context.EventBridge?.RaisePaySuccess(mockResult);
                return UniTask.FromResult(mockResult);
            }
#endif

            return PayGuardAsync(request, ct, async () =>
            {
                var voucherRequest = (IAPVoucherRequest)request;
                if (voucherRequest.Quote == null || voucherRequest.TableId != voucherRequest.Quote.TableId)
                {
                    var invalidResult = new IAPResult(request.TableId, (int)IAPVoucherErrorCode.StaleQuote, IAPErrorSource.Voucher, "Voucher 请求 TableId 与报价不一致。", request.CustomData);
                    Context?.EventBridge?.RaisePayFailed(invalidResult);
                    return invalidResult;
                }

                if (m_Coordinator == null)
                {
                    var unavailableResult = new IAPResult(request.TableId, (int)IAPVoucherErrorCode.WalletNotReady, IAPErrorSource.Voucher, "Voucher Store 尚未完成账号初始化。", request.CustomData);
                    Context?.EventBridge?.RaisePayFailed(unavailableResult);
                    return unavailableResult;
                }

                m_InPayTableId = request.TableId;
                try
                {
                    IAPResult result = await m_Coordinator.PayAsync(voucherRequest.Quote, request.CustomData, ct);
                    // 协调器只派发已持久化的服务端终态，前置失败和待恢复结果由 Store 派发失败事件。
                    if (!result.IsSuccess && result.ErrorCode != (int)IAPVoucherErrorCode.ServerRejected)
                    {
                        Context?.EventBridge?.RaisePayFailed(result);
                    }

                    return result;
                }
                finally
                {
                    m_InPayTableId = 0;
                }
            });
        }

        /// <summary>
        /// 使用当前账号钱包计算精确覆盖指定价格的不可变报价。
        /// </summary>
        /// <param name="tableId">商品配置表行 ID。</param>
        /// <param name="priceMills">商品价格，单位为 mills。</param>
        /// <returns>包含可提交状态与抵扣明细的 Voucher 报价。</returns>
        public VoucherQuote Quote(long tableId, long priceMills) => VoucherQuoteEngine.Quote(m_Session?.Current, tableId, priceMills);

        /// <summary>
        /// 刷新当前账号钱包，并合并同账号下的并发刷新请求。
        /// </summary>
        /// <param name="ct">调用方取消令牌。</param>
        /// <returns>刷新结果及刷新后的当前钱包快照。</returns>
        public async UniTask<VoucherRefreshResult> RefreshWalletAsync(CancellationToken ct = default)
        {
            if (m_Session == null || m_Gateway == null || string.IsNullOrEmpty(m_GameUID))
            {
                return new VoucherRefreshResult(false, IAPVoucherErrorCode.WalletNotReady, "Voucher 当前没有已登录账号。", Wallet);
            }

            try
            {
                UniTask<VoucherRefreshResult> refreshTask = m_Session.RunMergedRefreshAsync(RefreshWalletCoreAsync);
                return await refreshTask.AttachExternalCancellation(ct);
            }
            catch (OperationCanceledException)
            {
                return new VoucherRefreshResult(false, IAPVoucherErrorCode.NetworkError, "Voucher 钱包刷新已取消。", Wallet);
            }
        }

        /// <summary>
        /// 向当前账号测试发放礼券和赠币，并在成功后发布服务端钱包。
        /// </summary>
        /// <param name="request">测试发放请求。</param>
        /// <param name="ct">取消令牌。</param>
        /// <returns>测试发放结果及调用完成后的当前钱包。</returns>
        /// <exception cref="ArgumentNullException">测试发放请求为空时抛出。</exception>
        public UniTask<VoucherTestGrantResult> TestGrantAsync(VoucherTestGrantRequest request, CancellationToken ct = default)
        {
            if (request == null)
            {
                throw new ArgumentNullException(nameof(request));
            }

            if (m_Session == null || m_Gateway == null || string.IsNullOrEmpty(m_GameUID))
            {
                return UniTask.FromResult(new VoucherTestGrantResult(false, IAPVoucherErrorCode.WalletNotReady, "Voucher 当前没有已登录账号。", Wallet));
            }

            VoucherSessionScope scope = m_Session.CaptureScope();
            return TestGrantCoreAsync(request, scope, ct);
        }

        /// <summary>
        /// 恢复当前账号交易日志中尚未完成派发的订单。
        /// </summary>
        /// <param name="ct">取消令牌。</param>
        /// <returns>恢复扫描完成的异步任务。</returns>
        public override async UniTask CheckLocalOrdersAsync(CancellationToken ct)
        {
            if (m_Coordinator == null || string.IsNullOrEmpty(m_GameUID))
            {
                return;
            }

            await m_Coordinator.RecoverAsync(ct);
        }

        /// <summary>
        /// 释放当前账号作用域和 Voucher 内部服务。
        /// </summary>
        /// <param name="ct">取消令牌。</param>
        /// <returns>资源释放完成的异步任务。</returns>
        public override async UniTask DisposeAsync(CancellationToken ct)
        {
            m_Session?.Dispose();
            m_Session = null;
            m_Coordinator = null;
            m_Journal = null;
            m_PersistData = null;
            m_Gateway = null;
            m_Config = null;
            await base.DisposeAsync(ct);
        }
    }
}
