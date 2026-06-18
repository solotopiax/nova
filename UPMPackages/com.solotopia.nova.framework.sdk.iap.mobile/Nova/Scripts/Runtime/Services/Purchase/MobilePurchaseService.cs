/***************************************************************
 * (c) copyright 2026 - 2030, Solotopia
 * All Rights Reserved.
 * -------------------------------------------------------------
 * filename:  MobilePurchaseService.cs
 * author:    yingzheng
 * created:   2026/5/25
 * descrip:   购买发起、UUID 透传编码、IAP 5.x 平台回调处理
 ***************************************************************/

using System.Threading;
using Cysharp.Threading.Tasks;
using NovaFramework.SDK.IAP.Runtime;
using NovaFramework.Runtime;
using UnityEngine.Purchasing;

namespace NovaFramework.SDK.IAP.Mobile.Runtime
{
    /// <summary>
    /// 购买流程服务（Unity IAP 5.x）。
    /// 负责购买发起、订阅升降级路由、UUID 透传参数编码；
    /// 通过 OnPurchasePending / OnPurchaseConfirmed / OnPurchaseFailed 处理平台回调。
    /// </summary>
    internal sealed partial class MobilePurchaseService
    {
        /// <summary>
        /// 服务容器，持有共享外部依赖与其他服务引用。
        /// </summary>
        private readonly MobileServiceHub m_Hub;

        /// <summary>
        /// 构造 MobilePurchaseService。
        /// </summary>
        /// <param name="hub">服务容器，持有共享外部依赖与其他服务引用。</param>
        internal MobilePurchaseService(MobileServiceHub hub)
        {
            m_Hub = hub;
        }

        /// <summary>
        /// 设置 Android 订阅升降级的 ProrationMode。
        /// </summary>
        /// <param name="replaceMode">Google Play ProrationMode 枚举整数值。</param>
        internal void SetSubscriptionReplaceMode(int replaceMode)
        {
            m_SubscriptionReplaceMode = replaceMode;
        }

        /// <summary>
        /// 异步发起购买流程，结果通过 await 返回。
        /// </summary>
        /// <param name="request">移动端支付请求。</param>
        /// <param name="ct">取消令牌。</param>
        /// <returns>包含支付结果的 IAPResult。</returns>
        internal async UniTask<IAPResult> PayAsync(IAPMobileRequest request, CancellationToken ct)
        {
            ct.ThrowIfCancellationRequested();

            if (request == null)
            {
                return BroadcastFail(new IAPResult(0L, (int)IAPMobileErrorCode.StoreNotAvailable, "移动端支付请求为空。", null));
            }

            if (string.IsNullOrEmpty(m_Hub.Store?.GameUID))
            {
                return BroadcastFail(new IAPResult(request.TableId, (int)IAPMobileErrorCode.StoreNotAvailable, "未设置账号 UID，不能发起真实支付。", request.CustomData));
            }

            IAPResult localValidationResult = await m_Hub.ValidationService.TryValidatePaidLocalOrdersBeforePayAsync(request.TableId, request.CustomData, ct);
            if (localValidationResult != null)
            {
                if (!localValidationResult.IsSuccess && localValidationResult.ErrorCode == (int)IAPMobileErrorCode.AlreadyPurchasing)
                {
                    return BroadcastFail(localValidationResult);
                }

                return localValidationResult;
            }

            IAPProductEntry entry = m_Hub.Table?.FindByTableId(request.TableId);
            if (entry == null)
            {
                return BroadcastFail(new IAPResult(request.TableId, (int)IAPMobileErrorCode.ProductNotFound, $"TableId={request.TableId} 未在配置中找到对应商品。", request.CustomData));
            }

            Product product = m_Hub.ExtendedService.GetProductById(entry.ProductID);
            if (product == null)
            {
                return BroadcastFail(new IAPResult(request.TableId, (int)IAPMobileErrorCode.ProductNotFound, $"商品 {entry.ProductID} 在平台不存在。", request.CustomData));
            }

            if (product.definition.type == ProductType.Subscription)
            {
                IAPResult subResult = await HandleSubscriptionPayAsync(request, entry, product);
                if (subResult != null)
                {
                    return subResult;
                }

                return BroadcastFail(new IAPResult(request.TableId, (int)IAPMobileErrorCode.StoreNotAvailable, "订阅商品未进入订阅支付分支。", request.CustomData));
            }

            return await DoPlatformPayAsync(request, product);
        }

        /// <summary>
        /// OnPurchasePending 回调：购买进入待确认状态，先入队服务端验单，验单成功后再 ConfirmPurchase。
        /// </summary>
        /// <param name="order">待确认订单。</param>
        internal void OnPurchasePending(PendingOrder order)
        {
            Product product = m_Hub.ProductService.GetFirstProductInOrder(order);
            if (product == null)
            {
                Log.Warning(LogTag.IAPMobile, "平台待确认购买回调中未找到商品。");
                CompleteActivePayFailure(InPayTableId, IAPMobileErrorCode.ProductNotFound, "平台待确认回调中未找到商品。", m_CurrentCustomData, true);
                return;
            }

            Log.Debug(LogTag.IAPMobile, $"平台待确认购买回调：商品ID={product.definition.id}，等待服务端验单后确认订单。");
            HandlePendingOrder(order, product);
        }

        /// <summary>
        /// OnPurchaseConfirmed 回调：按 Order 类型路由到 ConfirmedOrder 或 FailedOrder 处理。
        /// </summary>
        /// <param name="order">确认结果订单。</param>
        internal void OnPurchaseConfirmed(Order order)
        {
            switch (order)
            {
                case ConfirmedOrder confirmed:
                    HandleConfirmedOrder(confirmed);
                    break;
                case FailedOrder failed:
                    HandleConfirmFailed(failed);
                    break;
                default:
                    Log.Warning(LogTag.IAPMobile, "平台订单确认回调收到未知订单类型。");
                    HandleConfirmFailed(new FailedOrder(order, PurchaseFailureReason.Unknown, string.Empty));
                    break;
            }
        }

        /// <summary>
        /// OnPurchaseFailed 回调（平台侧报告失败，非 ConfirmPurchase 失败）。
        /// </summary>
        /// <param name="order">失败订单。</param>
        internal void OnPurchaseFailed(FailedOrder order)
        {
            HandlePurchaseFailed(order);
        }

        /// <summary>
        /// OnPurchaseDeferred 回调（家长控制等场景延期）。
        /// </summary>
        /// <param name="order">延期订单。</param>
        internal void OnPurchaseDeferred(DeferredOrder order)
        {
            Product product = m_Hub.ProductService.GetFirstProductInOrder(order);
            Log.Debug(LogTag.IAPMobile, $"平台购买延期回调：商品ID={product?.definition.id}");
        }

        /// <summary>
        /// 释放服务资源。
        /// </summary>
        internal void Dispose()
        {
            if (InPayTableId != 0)
            {
                var disposeResult = new IAPResult(InPayTableId, (int)IAPMobileErrorCode.StoreNotAvailable, "store 已释放。", m_CurrentCustomData);
                // 通知业务层支付中断
                m_Hub.Context.EventBridge?.RaisePayFailed(disposeResult);
                // 解除 PayAsync 的 await
                m_PayTcs?.TrySetResult(disposeResult);
            }
            // 释放 TCS
            m_PayTcs = null;
            // 清空防重入标记
            InPayTableId = 0;
            // 释放透传数据引用
            m_CurrentCustomData = null;
        }

        /// <summary>
        /// 广播支付失败事件并原样返回结果，供 PayAsync early return 处使用。
        /// </summary>
        /// <param name="result">已构造的失败结果。</param>
        /// <returns>传入的失败结果（原样透传）。</returns>
        private IAPResult BroadcastFail(IAPResult result)
        {
            m_Hub.Context.EventBridge?.RaisePayFailed(result);
            return result;
        }
    }
}
