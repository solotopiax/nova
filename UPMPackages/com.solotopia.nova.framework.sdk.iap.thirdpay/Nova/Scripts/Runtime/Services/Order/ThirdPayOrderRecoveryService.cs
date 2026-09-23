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
        /// 创建待验订单恢复服务。
        /// </summary>
        /// <param name="hub">ThirdPay 强类型服务容器。</param>
        public ThirdPayOrderRecoveryService(ThirdPayServiceHub hub)
        {
            m_Hub = hub ?? throw new ArgumentNullException(nameof(hub));
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
