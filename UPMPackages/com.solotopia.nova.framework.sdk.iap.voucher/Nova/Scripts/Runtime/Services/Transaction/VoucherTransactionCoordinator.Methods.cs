/***************************************************************
 * (c) copyright 2026 - 2030, Solotopia
 * All Rights Reserved.
 * -------------------------------------------------------------
 * filename:  VoucherTransactionCoordinator.Methods.cs
 * author:    yingzheng
 * created:   2026/8/3
 * descrip:   VoucherTransactionCoordinator 非公开方法
 ***************************************************************/

using System;
using System.Threading;
using Cysharp.Threading.Tasks;
using NovaFramework.Runtime;
using NovaFramework.SDK.IAP.Runtime;

namespace NovaFramework.SDK.IAP.Voucher.Runtime
{
    /// <summary>
    /// VoucherTransactionCoordinator 非公开方法。
    /// </summary>
    internal sealed partial class VoucherTransactionCoordinator
    {
        /// <summary>
        /// 在已经持有串行权时恢复指定账号的全部交易记录。
        /// </summary>
        /// <param name="accountId">待恢复账号 ID。</param>
        /// <param name="ct">取消令牌。</param>
        /// <returns>恢复完成的异步任务。</returns>
        private async UniTask RecoverCoreAsync(string accountId, CancellationToken ct)
        {
            foreach (VoucherTransactionEntry entry in m_Journal.GetAll(accountId))
            {
                if (entry.State == VoucherTransactionState.SucceededPendingDispatch)
                {
                    DispatchAndRemove(CreateSuccess(entry.Command, true));
                    continue;
                }

                if (entry.State == VoucherTransactionState.RejectedPendingDispatch)
                {
                    DispatchAndRemove(CreateRejected(entry.Command, entry.ErrorMessage, true));
                    continue;
                }

                await SubmitAsync(entry.Command, true, ct);
            }
        }

        /// <summary>
        /// 发送一条已经持久化的交易命令并执行有限状态迁移。
        /// </summary>
        /// <param name="command">已经持久化的不可变交易命令。</param>
        /// <param name="isRecoveredOrder">是否为补单恢复请求。</param>
        /// <param name="ct">取消令牌。</param>
        /// <returns>交易提交结果。</returns>
        private async UniTask<IAPResult> SubmitAsync(VoucherSpendCommand command, bool isRecoveredOrder, CancellationToken ct)
        {
            VoucherSessionScope scope = m_Session.CaptureScope();
            if (!string.Equals(scope.AccountId, command.AccountId, StringComparison.Ordinal))
            {
                m_Journal.MarkPendingRecovery(command.GameOrderId, "账号已切换，跳过非当前账号订单发送。");
                return CreatePendingResult(command.TableId, command.CustomData);
            }

            VoucherGatewayDeductResult gatewayResult;
            using (CancellationTokenSource linkedCancellation = CancellationTokenSource.CreateLinkedTokenSource(ct, scope.Token))
            {
                try
                {
                    gatewayResult = await m_Gateway.DeductAsync(command, linkedCancellation.Token);
                }
                catch (OperationCanceledException exception)
                {
                    m_Journal.MarkPendingRecovery(command.GameOrderId, exception.Message);
                    return CreatePendingResult(command.TableId, command.CustomData);
                }
                catch (Exception exception)
                {
                    m_Journal.MarkPendingRecovery(command.GameOrderId, exception.Message);
                    return CreatePendingResult(command.TableId, command.CustomData);
                }
            }

            switch (gatewayResult.Disposition)
            {
                case VoucherGatewayDisposition.Succeeded:
                    m_Session.TryPublish(scope, gatewayResult.Vouchers, gatewayResult.Coins, m_UtcNowUnixTimeMs());
                    m_Journal.MarkSucceededPendingDispatch(command.GameOrderId);
                    IAPResult successResult = CreateSuccess(command, isRecoveredOrder);
                    DispatchAndRemove(successResult);
                    return successResult;

                case VoucherGatewayDisposition.Rejected:
                    m_Journal.MarkRejectedPendingDispatch(command.GameOrderId, (int)IAPVoucherErrorCode.ServerRejected, gatewayResult.Message);
                    IAPResult rejectedResult = CreateRejected(command, gatewayResult.Message, isRecoveredOrder);
                    DispatchAndRemove(rejectedResult);
                    return rejectedResult;

                case VoucherGatewayDisposition.Retryable:
                case VoucherGatewayDisposition.Unknown:
                default:
                    m_Journal.MarkPendingRecovery(command.GameOrderId, gatewayResult.Message);
                    return CreatePendingResult(command.TableId, command.CustomData);
            }
        }

        /// <summary>
        /// 派发已经落盘的终态，并在派发返回后删除交易日志。
        /// </summary>
        /// <param name="result">已经携带稳定订单号的 IAP 结果。</param>
        private void DispatchAndRemove(IAPResult result)
        {
            m_Dispatcher.Dispatch(result);
            m_Journal.Remove(result.OrderId);
        }

        /// <summary>
        /// 创建携带稳定订单号的成功结果。
        /// </summary>
        /// <param name="command">原始不可变交易命令。</param>
        /// <param name="isRecoveredOrder">是否为补单恢复结果。</param>
        /// <returns>成功 IAP 结果。</returns>
        private static IAPResult CreateSuccess(VoucherSpendCommand command, bool isRecoveredOrder)
        {
            return new IAPResult(command.TableId, command.GameOrderId, isRecoveredOrder, true, command.CustomData);
        }

        /// <summary>
        /// 创建携带稳定订单号的明确拒绝结果。
        /// </summary>
        /// <param name="command">原始不可变交易命令。</param>
        /// <param name="message">服务端拒绝原因。</param>
        /// <param name="isRecoveredOrder">是否为补单恢复结果。</param>
        /// <returns>失败 IAP 结果。</returns>
        private static IAPResult CreateRejected(VoucherSpendCommand command, string message, bool isRecoveredOrder)
        {
            return new IAPResult(command.TableId, (int)IAPVoucherErrorCode.ServerRejected, IAPErrorSource.Voucher, string.IsNullOrEmpty(message) ? "Voucher 抵扣被服务端拒绝。" : message, command.CustomData, command.GameOrderId, isRecoveredOrder);
        }

        /// <summary>
        /// 创建保留交易日志的待恢复结果。
        /// </summary>
        /// <param name="tableId">商品配置表行 ID。</param>
        /// <param name="customData">业务自定义透传数据。</param>
        /// <returns>待恢复 IAP 结果。</returns>
        private static IAPResult CreatePendingResult(long tableId, string customData)
        {
            return CreateFailure(tableId, IAPVoucherErrorCode.TransactionPending, "Voucher 订单结果未知，已保留原订单等待恢复。", customData);
        }

        /// <summary>
        /// 创建尚未生成稳定订单号的前置失败结果。
        /// </summary>
        /// <param name="tableId">商品配置表行 ID。</param>
        /// <param name="errorCode">Voucher 错误码。</param>
        /// <param name="message">失败原因。</param>
        /// <param name="customData">业务自定义透传数据。</param>
        /// <returns>失败 IAP 结果。</returns>
        private static IAPResult CreateFailure(long tableId, IAPVoucherErrorCode errorCode, string message, string customData)
        {
            return new IAPResult(tableId, (int)errorCode, IAPErrorSource.Voucher, message, customData);
        }

        /// <summary>
        /// 异步等待交易协调器串行权。
        /// </summary>
        /// <param name="ct">取消令牌。</param>
        /// <returns>取得串行权后完成的异步任务。</returns>
        private async UniTask EnterAsync(CancellationToken ct)
        {
            while (Interlocked.CompareExchange(ref m_IsBusy, 1, 0) != 0)
            {
                ct.ThrowIfCancellationRequested();
                await UniTask.Yield();
            }
        }

        /// <summary>
        /// 释放交易协调器串行权。
        /// </summary>
        private void Exit() => Volatile.Write(ref m_IsBusy, 0);
    }
}
