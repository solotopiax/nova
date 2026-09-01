/***************************************************************
 * (c) copyright 2026 - 2030, Solotopia
 * All Rights Reserved.
 * -------------------------------------------------------------
 * filename:  VoucherTransactionCoordinator.cs
 * author:    yingzheng
 * created:   2026/8/3
 * descrip:   Voucher 可恢复交易状态机入口
 ***************************************************************/

using System;
using System.Threading;
using Cysharp.Threading.Tasks;
using NovaFramework.SDK.IAP.Runtime;

namespace NovaFramework.SDK.IAP.Voucher.Runtime
{
    /// <summary>
    /// Voucher 可恢复交易协调器。
    /// 串行执行新支付、补单恢复、终态持久化和事件派发。
    /// </summary>
    internal sealed partial class VoucherTransactionCoordinator : VoucherLogOwner
    {
        /// <summary>
        /// 创建 Voucher 交易协调器。
        /// </summary>
        /// <param name="gateway">Voucher 协议网关。</param>
        /// <param name="journal">当前账号交易日志。</param>
        /// <param name="session">当前账号钱包会话。</param>
        /// <param name="dispatcher">终态 IAP 结果派发端口。</param>
        /// <param name="orderIdFactory">稳定订单号工厂。</param>
        /// <param name="utcNowUnixTimeMs">UTC 毫秒时间戳提供器。</param>
        /// <param name="countryProvider">当前国家或地区提供器。</param>
        /// <exception cref="ArgumentNullException">任一依赖为空时抛出。</exception>
        internal VoucherTransactionCoordinator(IVoucherGateway gateway, VoucherTransactionJournal journal, VoucherWalletSession session, IVoucherResultDispatcher dispatcher, Func<string> orderIdFactory, Func<long> utcNowUnixTimeMs, Func<string> countryProvider)
        {
            m_Gateway = gateway ?? throw new ArgumentNullException(nameof(gateway));
            m_Journal = journal ?? throw new ArgumentNullException(nameof(journal));
            m_Session = session ?? throw new ArgumentNullException(nameof(session));
            m_Dispatcher = dispatcher ?? throw new ArgumentNullException(nameof(dispatcher));
            m_OrderIdFactory = orderIdFactory ?? throw new ArgumentNullException(nameof(orderIdFactory));
            m_UtcNowUnixTimeMs = utcNowUnixTimeMs ?? throw new ArgumentNullException(nameof(utcNowUnixTimeMs));
            m_CountryProvider = countryProvider ?? throw new ArgumentNullException(nameof(countryProvider));
        }

        /// <summary>
        /// 验证报价、优先恢复未知订单，再持久化并发送一个新交易命令。
        /// </summary>
        /// <param name="quote">当前钱包生成的不可变报价。</param>
        /// <param name="customData">业务自定义透传数据。</param>
        /// <param name="ct">取消令牌。</param>
        /// <returns>本次支付或待恢复状态对应的 IAP 结果。</returns>
        internal async UniTask<IAPResult> PayAsync(VoucherQuote quote, string customData, CancellationToken ct)
        {
            await EnterAsync(ct);
            try
            {
                string accountId = m_Session.Current.AccountId;
                if (m_Journal.HasAny(accountId))
                {
                    await RecoverCoreAsync(accountId, ct);
                    if (m_Journal.HasAny(accountId))
                    {
                        return CreatePendingResult(quote?.TableId ?? 0, customData);
                    }
                }

                if (quote == null || quote.Status != VoucherQuoteStatus.Ready)
                {
                    return CreateFailure(quote?.TableId ?? 0, IAPVoucherErrorCode.InvalidPrice, "Voucher 报价不可提交。", customData);
                }

                if (!m_Session.IsQuoteCurrent(quote) || quote.TableId <= 0)
                {
                    return CreateFailure(quote.TableId, IAPVoucherErrorCode.StaleQuote, "Voucher 报价所属账号或钱包版本已变化。", customData);
                }

                string orderId = m_OrderIdFactory();
                var command = new VoucherSpendCommand(c_CommandSchemaVersion, quote.AccountId, orderId, quote.TableId, quote.FrozenSpend.VoucherCodes, quote.FrozenSpend.CoinUsages, m_CountryProvider(), customData, m_UtcNowUnixTimeMs());

                VoucherJournalAddResult addResult;
                try
                {
                    addResult = m_Journal.TryAdd(command);
                }
                catch (Exception exception)
                {
                    return CreateFailure(quote.TableId, IAPVoucherErrorCode.JournalFailure, $"Voucher 交易日志保存失败：{exception.Message}", customData);
                }

                if (addResult == VoucherJournalAddResult.Conflict)
                {
                    return CreateFailure(quote.TableId, IAPVoucherErrorCode.JournalFailure, "Voucher game_order_id 与已有 payload 冲突。", customData);
                }

                return await SubmitAsync(command, false, ct);
            }
            finally
            {
                Exit();
            }
        }

        /// <summary>
        /// 对当前账号执行一次有限恢复，每个未完成订单最多发送一次。
        /// </summary>
        /// <param name="ct">取消令牌。</param>
        /// <returns>恢复扫描完成的异步任务。</returns>
        internal async UniTask RecoverAsync(CancellationToken ct)
        {
            await EnterAsync(ct);
            try
            {
                await RecoverCoreAsync(m_Session.Current.AccountId, ct);
            }
            finally
            {
                Exit();
            }
        }
    }
}
