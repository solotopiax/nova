/***************************************************************
 * (c) copyright 2026 - 2030, Solotopia
 * All Rights Reserved.
 * -------------------------------------------------------------
 * filename:  MobileInitService.Methods.cs
 * author:    yingzheng
 * created:   2026/5/28
 * descrip:   MobileInitService 内部方法
 ***************************************************************/

using System;
using System.Collections.Generic;
using System.Threading;
using Cysharp.Threading.Tasks;
using NovaFramework.SDK.IAP.Runtime;
using NovaFramework.Runtime;
using UnityEngine.Purchasing;

namespace NovaFramework.SDK.IAP.Mobile.Runtime
{
    /// <summary>
    /// MobileInitService 内部辅助方法定义。
    /// </summary>
    internal sealed partial class MobileInitService
    {
        /// <summary>
        /// 等待商品拉取完成；超时不抛出，返回当前状态，调用方自行决定是否延后补跑。
        /// </summary>
        /// <param name="timeoutMs">等待超时时间，单位毫秒。</param>
        /// <param name="ct">外部取消令牌。</param>
        /// <returns>当前商品拉取状态。</returns>
        internal async UniTask<MobileProductFetchState> WaitForProductsFetchedAsync(int timeoutMs, CancellationToken ct)
        {
            return await m_ProductFetchCoordinator.WaitForProductsFetchedAsync(timeoutMs, ct);
        }

        /// <summary>
        /// 触发初始化失败：幂等地标记 RuntimeContext 失败状态，重置 IsReady，触发事件桥通知，完成 InitTcs。
        /// TryMarkFailed 有幂等保护，重复调用安全。
        /// </summary>
        /// <param name="reason">初始化失败原因。</param>
        /// <param name="detail">失败详情描述。</param>
        private void FailInitialization(MobileStoreInitFailureReason reason, string detail)
        {
            m_ProductFetchCoordinator.CancelRetry();
            if (m_RuntimeContext == null)
            {
                m_InitTcs?.TrySetResult(false);
                m_InitTcs = null;
                return;
            }

            if (!m_RuntimeContext.TryMarkFailed(reason, detail))
            {
                return;
            }

            Log.Warning(LogTag.IAPMobile, $"Unity IAP 初始化失败，原因={reason}，详情={detail}");
            IsReady = false;
            m_Hub.Store.TrackInitFailedInternal(reason);
            m_Hub.Context.EventBridge?.RaiseInitResult(IAPInitResult.Fail((int)reason, detail));
            m_InitTcs?.TrySetResult(false);
            m_InitTcs = null;
        }

        /// <summary>
        /// 将框架 IAPProductType 转换为 Unity IAP ProductType 枚举值。
        /// </summary>
        /// <param name="type">框架商品类型。</param>
        /// <returns>对应的 Unity IAP ProductType。</returns>
        private static ProductType ToUnityProductType(IAPProductType type)
        {
            return type switch
            {
                IAPProductType.NonConsumable => ProductType.NonConsumable,
                IAPProductType.Subscription => ProductType.Subscription,
                _ => ProductType.Consumable,
            };
        }

        /// <summary>
        /// 商品拉取进入成功态后的后续流程，只由商品拉取协调器首次完成时触发。
        /// 启动期不调用平台 RestoreTransactions，避免 iOS 在无用户交互时弹出 Apple ID 验证框。
        /// </summary>
        private void OnProductFetchCompleted()
        {
            m_Hub.RestoreService.TryRunPendingEntitlementRefreshAfterProductsFetched();
            m_Hub.ExtendedService.FetchPurchases();
        }
    }
}
