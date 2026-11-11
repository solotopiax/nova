/***************************************************************
 * (c) copyright 2026 - 2030, Solotopia
 * All Rights Reserved.
 * -------------------------------------------------------------
 * filename:  ThirdPayOrderRecoveryService.cs
 * author:    yingzheng
 * created:   2026/9/17
 * descrip:   ThirdPay 本地与服务端待验订单恢复服务
 ***************************************************************/

using System;
using System.Collections.Generic;
using System.Threading;
using Cysharp.Threading.Tasks;
using NovaFramework.Runtime;
using NovaFramework.SDK.IAP.Runtime;

namespace NovaFramework.SDK.IAP.ThirdPay.Runtime
{
    /// <summary>
    /// 查询并合并待验订单，再交由统一验单服务处理。
    /// </summary>
    internal sealed class ThirdPayOrderRecoveryService : ThirdPayLogOwner
    {
        /// <summary>
        /// ThirdPay 强类型服务容器。
        /// </summary>
        private readonly ThirdPayServiceHub m_Hub;

        /// <summary>
        /// 统一的待验订单查询函数，保持网络请求与购买前决策分离。
        /// </summary>
        private readonly Func<string, UniTask<NetResponse<PbNetThirdQueryPendingOrderResp>>> m_QueryPendingOrders;

        /// <summary>
        /// 创建待验订单恢复服务。
        /// </summary>
        /// <param name="hub">ThirdPay 强类型服务容器。</param>
        public ThirdPayOrderRecoveryService(ThirdPayServiceHub hub)
        {
            m_Hub = hub ?? throw new ArgumentNullException(nameof(hub));
            m_QueryPendingOrders = cmd => m_Hub.NetService.QueryPendingOrderAsync(cmd);
        }

        /// <summary>
        /// 执行 Android 购买前补单决策；只有明确无需补单时才返回 null 允许创建新订单。
        /// 调用方负责等待界面引用；查询失败不回退到本地直接验单。
        /// </summary>
        /// <param name="request">本次购买请求，不用于覆盖历史订单的业务上下文。</param>
        /// <param name="utcSeconds">本次点击的 UTC 秒时间戳。</param>
        /// <param name="ct">本次购买取消令牌。</param>
        /// <returns>补单批次结果或查询失败；允许正常新购时返回 null。</returns>
        internal async UniTask<IAPResult> RecoverBeforePayAsync(IAPThirdPayRequest request, long utcSeconds, CancellationToken ct)
        {
            using var linkedCts = CancellationTokenSource.CreateLinkedTokenSource(ct, m_Hub.RuntimeTaskToken);
            CancellationToken token = linkedCts.Token;
            long version = m_Hub.PersistContext.Version;
            string userId = m_Hub.Store.CurrentUserId;
            EnsureCurrentContext(version, userId, token);
            try
            {
                if (string.IsNullOrEmpty(userId))
                {
                    return PreflightFailure(request, "尚未登录，无法查询第三方支付补单。");
                }

                if (!m_Hub.AndroidOrderRecoveryEnabled || !m_Hub.PersistContext.IsOrderRecoveryWithinWindow(
                        request.TableId, request.ReceiptParam, m_Hub.AndroidOrderRecoveryWindowMinutes, utcSeconds))
                {
                    ClearCurrentBusinessOrders(request);
                    return null;
                }

                string cmd = m_Hub.Config?.QueryPendingOrderCmdName;
                if (m_Hub.NetService == null || string.IsNullOrEmpty(cmd))
                {
                    return PreflightFailure(request, "第三方支付补单查询未配置，本次购买已停止。");
                }

                NetResponse<PbNetThirdQueryPendingOrderResp> response = await m_QueryPendingOrders(cmd).AttachExternalCancellation(token);
                EnsureCurrentContext(version, userId, token);
                if (response == null || !response.IsSuccess || response.Data == null)
                {
                    return PreflightFailure(request, "查询未完成支付订单失败，请稍后重试。");
                }

                List<ThirdPayOrderRecord> orders = SelectOrdersForProduct(response.Data, request.TableId, userId);
                if (orders.Count == 0)
                {
                    ClearCurrentBusinessOrders(request);
                    return null;
                }

                foreach (ThirdPayOrderRecord order in orders)
                {
                    m_Hub.PersistContext.UpsertOrder(order);
                }

                // 命中服务器订单后只补单，任何验单结果都不允许继续创建本次新订单。
                List<IAPResult> results = await m_Hub.OrderValidationService.ValidateAsync(orders, ThirdPayValidationScene.Recovered, token);
                EnsureCurrentContext(version, userId, token);
                foreach (IAPResult result in results)
                {
                    if (!result.IsSuccess)
                    {
                        return PreflightFailure(request, "历史支付订单尚未全部完成校验，请稍后重试。");
                    }
                }

                // 每笔历史订单已经通过原事件链反馈。单次调用只返回批次完成，不再次授予发货资格。
                return new IAPResult(request.TableId, string.Empty, true, false, request.CustomData, request.ReceiptParam, IAPStoreType.ThirdPay);
            }
            catch (OperationCanceledException)
            {
                throw;
            }
            catch (Exception ex)
            {
                EnsureCurrentContext(version, userId, token);
                LogWarning($"第三方支付购买前补单处理失败，已停止新购：{ex.Message}");
                return PreflightFailure(request, "查询或处理历史支付订单失败，请稍后重试。");
            }
        }

        /// <summary>
        /// 按商品选择全部服务器成功未验订单并去重，优先保留本地订单自身的业务上下文。
        /// </summary>
        /// <param name="response">服务端成功未验订单列表。</param>
        /// <param name="tableId">本次点击的商品表行 ID。</param>
        /// <param name="userId">此次查询所属账号。</param>
        /// <returns>可交给统一批量验单的订单集合。</returns>
        private List<ThirdPayOrderRecord> SelectOrdersForProduct(PbNetThirdQueryPendingOrderResp response, long tableId, string userId)
        {
            var orders = new List<ThirdPayOrderRecord>();
            var ids = new HashSet<string>(StringComparer.Ordinal);
            foreach (PbNetThirdQueryPendingOrderInfo item in response.OrderList)
            {
                if (item == null || item.TableId != tableId || string.IsNullOrEmpty(item.ClientOrderId) || !ids.Add(item.ClientOrderId))
                {
                    continue;
                }

                if (!m_Hub.PersistContext.TryGetOrder(item.ClientOrderId, out ThirdPayOrderRecord order) || order == null)
                {
                    order = new ThirdPayOrderRecord { ClientOrderId = item.ClientOrderId, TableId = tableId, UserId = userId };
                }

                orders.Add(order);
            }

            return orders;
        }

        /// <summary>
        /// 清理当前账号同业务键的旧订单与查询起点，其他 ReceiptParam 的订单保持不变。
        /// </summary>
        /// <param name="request">当前业务购买请求。</param>
        private void ClearCurrentBusinessOrders(IAPThirdPayRequest request)
        {
            IReadOnlyCollection<ThirdPayOrderRecord> orders = m_Hub.PersistContext.GetAllOrders();
            if (orders != null)
            {
                foreach (ThirdPayOrderRecord order in orders)
                {
                    if (ThirdPayOrderKey.Matches(order, request.TableId, request.ReceiptParam))
                    {
                        m_Hub.Store.RemoveLocalOrderAndTrack(order, false, 0, default, ThirdPayOrderDeleteReason.ExplicitCleanup, false);
                    }
                }
            }

            m_Hub.PersistContext.ClearOrderRecoveryTime(request.TableId, request.ReceiptParam);
            m_Hub.PersistContext.Save();
        }

        /// <summary>
        /// 检查取消、Store 释放和账号版本，禁止迟到响应继续修改当前账号。
        /// </summary>
        /// <param name="version">请求开始时的账号版本。</param>
        /// <param name="userId">请求开始时的账号。</param>
        /// <param name="ct">请求取消令牌。</param>
        private void EnsureCurrentContext(long version, string userId, CancellationToken ct)
        {
            ct.ThrowIfCancellationRequested();
            if (version != m_Hub.PersistContext.Version || !string.Equals(userId, m_Hub.Store.CurrentUserId, StringComparison.Ordinal))
            {
                throw new OperationCanceledException("第三方支付账号上下文已变化。", ct);
            }
        }

        /// <summary>
        /// 发布明确的购买前失败结果，不允许继续创建订单。
        /// </summary>
        /// <param name="request">本次购买请求。</param>
        /// <param name="message">可供业务提示的失败描述。</param>
        /// <returns>ThirdPay 网络或恢复失败结果。</returns>
        private IAPResult PreflightFailure(IAPThirdPayRequest request, string message)
        {
            LogWarning(message);
            var failure = new IAPResult(request.TableId, (int)IAPThirdPayErrorCode.OrderRecoveryFailed, IAPErrorSource.ThirdPay, message, request.CustomData, request.ReceiptParam);
            m_Hub.Context?.EventBridge?.RaisePayFailed(failure);
            return failure;
        }

        /// <summary>
        /// 合并本地订单与服务端待验订单，并执行一次后台补单验单。
        /// </summary>
        /// <param name="ct">取消令牌。</param>
        /// <returns>补单查询与验单完成的异步任务。</returns>
        public async UniTask RecoverAsync(CancellationToken ct)
        {
            IReadOnlyCollection<ThirdPayOrderRecord> localOrders = m_Hub.PersistContext.GetAllOrders();

            PbNetThirdQueryPendingOrderResp serverOrders = null;
            string queryCmdName = m_Hub.Config?.QueryPendingOrderCmdName;
            if (m_Hub.NetService != null && !string.IsNullOrEmpty(queryCmdName))
            {
                try
                {
                    NetResponse<PbNetThirdQueryPendingOrderResp> response = await m_Hub.NetService.QueryPendingOrderAsync(queryCmdName);
                    if (response.IsSuccess && response.Data != null)
                    {
                        serverOrders = response.Data;
                    }
                }
                catch (OperationCanceledException)
                {
                    throw;
                }
                catch (Exception ex)
                {
                    LogWarning($"查询第三方支付未校验订单失败：{ex.Message}");
                }
            }

            List<ThirdPayOrderRecord> merged = MergeRecoverableOrders(localOrders, serverOrders);
            foreach (ThirdPayOrderRecord order in merged)
            {
                if (string.IsNullOrEmpty(order.UserId))
                {
                    order.UserId = m_Hub.Store.CurrentUserId;
                }
            }

            if (merged.Count == 0)
            {
                return;
            }

            m_Hub.Store.AddWaitingRef();
            try
            {
                await m_Hub.OrderValidationService.ValidateAsync(merged, ThirdPayValidationScene.Recovered, ct);
            }
            finally
            {
                m_Hub.Store.SubWaitingRef();
            }
        }

        /// <summary>
        /// 合并本地订单与服务端未校验订单，并按客户端订单号去重。
        /// </summary>
        /// <param name="localOrders">当前账号的本地订单集合。</param>
        /// <param name="serverResponse">服务端待验订单响应。</param>
        /// <returns>按客户端订单号去重后的可恢复订单列表。</returns>
        internal static List<ThirdPayOrderRecord> MergeRecoverableOrders(IReadOnlyCollection<ThirdPayOrderRecord> localOrders, PbNetThirdQueryPendingOrderResp serverResponse)
        {
            var result = new List<ThirdPayOrderRecord>();
            var clientOrderIds = new HashSet<string>(StringComparer.Ordinal);

            if (localOrders != null)
            {
                foreach (ThirdPayOrderRecord order in localOrders)
                {
                    if (order == null || string.IsNullOrEmpty(order.ClientOrderId) || !clientOrderIds.Add(order.ClientOrderId))
                    {
                        continue;
                    }

                    result.Add(order);
                }
            }

            if (serverResponse?.OrderList == null)
            {
                return result;
            }

            foreach (PbNetThirdQueryPendingOrderInfo serverOrder in serverResponse.OrderList)
            {
                if (serverOrder == null || string.IsNullOrEmpty(serverOrder.ClientOrderId) || !clientOrderIds.Add(serverOrder.ClientOrderId))
                {
                    continue;
                }

                result.Add(new ThirdPayOrderRecord { ClientOrderId = serverOrder.ClientOrderId, TableId = serverOrder.TableId });
            }

            return result;
        }
    }
}
