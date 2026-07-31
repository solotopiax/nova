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
    internal sealed partial class MobileInitService
    {
        /// <summary>
        /// 通过 ExtendedService 向平台发起商品拉取请求，使用 InitializeAsync 阶段构建的商品定义列表。
        /// 拉取进行中或已经成功时忽略重复请求；失败状态允许后续连接回调重新拉取。
        /// </summary>
        private void FetchProducts()
        {
            if (ProductFetchState is MobileProductFetchState.Fetching or MobileProductFetchState.Succeeded)
            {
                Log.Debug(LogTag.IAPMobile, $"商品拉取请求已跳过，当前状态={ProductFetchState}。");
                return;
            }

            ProductFetchState = MobileProductFetchState.Fetching;
            m_ProductFetchTcs = new UniTaskCompletionSource<MobileProductFetchState>();
            List<ProductDefinition> productDefs = m_PendingProductDefs ?? new List<ProductDefinition>();
            Log.Debug(LogTag.IAPMobile, $"Unity IAP 注册商品定义数量={productDefs.Count}。");
            m_Hub.ExtendedService?.FetchProducts(productDefs);
        }

        /// <summary>
        /// 等待商品拉取完成；超时不抛出，返回当前状态，调用方自行决定是否延后补跑。
        /// </summary>
        internal async UniTask<MobileProductFetchState> WaitForProductsFetchedAsync(int timeoutMs, CancellationToken ct)
        {
            if (ProductFetchState != MobileProductFetchState.Fetching)
            {
                return ProductFetchState;
            }

            UniTaskCompletionSource<MobileProductFetchState> tcs = m_ProductFetchTcs;
            if (tcs == null)
            {
                return ProductFetchState;
            }

            using var timeoutCts = CancellationTokenSource.CreateLinkedTokenSource(ct);
            timeoutCts.CancelAfter(timeoutMs);
            try
            {
                return await tcs.Task.AttachExternalCancellation(timeoutCts.Token);
            }
            catch (OperationCanceledException) when (!ct.IsCancellationRequested)
            {
                return ProductFetchState;
            }
        }

        /// <summary>
        /// 触发初始化失败：幂等地标记 RuntimeContext 失败状态，重置 IsReady，触发事件桥通知，完成 InitTcs。
        /// TryMarkFailed 有幂等保护，重复调用安全。
        /// </summary>
        /// <param name="reason">初始化失败原因。</param>
        /// <param name="detail">失败详情描述。</param>
        private void FailInitialization(MobileStoreInitFailureReason reason, string detail)
        {
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
    }
}
