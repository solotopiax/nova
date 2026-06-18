/***************************************************************
 * (c) copyright 2026 - 2030, Solotopia
 * All Rights Reserved.
 * -------------------------------------------------------------
 * filename:  MobileInitService.cs
 * author:    yingzheng
 * created:   2026/5/25
 * descrip:   Unity IAP 5.x 初始化生命周期管理
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
    /// Unity IAP 5.x 初始化服务。
    /// 通过 ExtendedService 完成三步初始化：SetController → RegisterStoreCallbacks → Connect；
    /// 不直接持有或注册 Controller 事件——所有回调由 MobileStoreService 路由过来。
    /// </summary>
    internal sealed partial class MobileInitService
    {
        /// <summary>
        /// 构造 MobileInitService。
        /// </summary>
        /// <param name="hub">服务容器，持有共享外部依赖与其他服务引用。</param>
        internal MobileInitService(MobileServiceHub hub)
        {
            m_Hub = hub;
        }

        /// <summary>
        /// 异步初始化 Unity IAP：经由 ExtendedService 完成 Controller 注入、事件注册、平台连接。
        /// 初始化结果通过 OnStoreConnected / FailInitialization 回调驱动，await InitTcs 等待结果。
        /// </summary>
        /// <param name="table">IAP 商品表接口，用于注册 productId。</param>
        /// <param name="ct">取消令牌。</param>
        /// <returns>初始化是否成功。</returns>
        internal async UniTask<bool> InitializeAsync(IIAPProductTable table, CancellationToken ct)
        {
            m_RuntimeContext = new MobileRuntimeContext();
            m_InitTcs = new UniTaskCompletionSource<bool>();

            using var reg = ct.Register(() =>
            {
                FailInitialization(MobileStoreInitFailureReason.InitializationCanceled, "初始化被取消。");
            });
            if (ct.IsCancellationRequested)
            {
                return false;
            }

            StoreController controller = UnityIAPServices.StoreController();
            if (controller == null)
            {
                FailInitialization(MobileStoreInitFailureReason.StoreControllerUnavailable, "Unity IAP StoreController 创建失败。");
                return false;
            }
            m_RuntimeContext.BeginInitialization(controller);

            // ExtendedService 第一步接管 Controller，后续所有平台操作都经过它
            m_Hub.ExtendedService.SetController(controller);
            // 注册商店级回调（路由到 StoreService），必须在 Connect 之前完成
            m_Hub.ExtendedService.RegisterStoreCallbacks();

            m_PendingProductDefs = new List<ProductDefinition>();
            if (table?.Products != null)
            {
                foreach (IAPProductEntry entry in table.Products)
                {
                    // 框架类型 → Unity IAP 类型
                    m_PendingProductDefs.Add(new ProductDefinition(entry.ProductID, ToUnityProductType(entry.ProductType)));
                }
            }

            try
            {
                await m_Hub.ExtendedService.Connect();
            }
            catch (OperationCanceledException e)
            {
                FailInitialization(MobileStoreInitFailureReason.InitializationCanceled, e.Message);
                return false;
            }
            catch (Exception e)
            {
                Log.Warning(LogTag.IAPMobile, $"Unity IAP 商店连接异常，详情={e.Message}");
                FailInitialization(MobileStoreInitFailureReason.StoreConnectException, e.Message);
                return false;
            }

            var tcs = m_InitTcs;
            if (tcs == null)
            {
                return m_RuntimeContext?.IsReady == true;
            }

            return await tcs.Task;
        }

        /// <summary>
        /// 商店连接成功回调（由 MobileStoreService 路由）：标记已连接并完成初始化，随后触发商品拉取。
        /// RegisterProductCallbacks 已由 StoreService.OnStoreConnected 通过 ExtendedService 调用，此处无需重复。
        /// </summary>
        internal void OnStoreConnected()
        {
            if (m_RuntimeContext == null)
            {
                return;
            }

            Log.Debug(LogTag.IAPMobile, "商店连接成功，商品信息将在后台拉取。");
            m_RuntimeContext.MarkConnected();
            if (m_RuntimeContext.IsInitializing)
            {
                m_RuntimeContext.MarkReady();
                IsReady = true;
                m_Hub.Store.TrackInitSuccessInternal();
                m_Hub.Context.EventBridge?.RaiseInitResult(IAPInitResult.Success());
                m_InitTcs?.TrySetResult(true);
                m_InitTcs = null;
            }
            FetchProducts();
        }

        /// <summary>
        /// 商店连接断开回调（由 MobileStoreService 路由）：初始化期间断开则直接标记失败。
        /// </summary>
        /// <param name="description">断连描述。</param>
        internal void OnStoreDisconnected(StoreConnectionFailureDescription description)
        {
            if (m_RuntimeContext == null)
            {
                return;
            }

            Log.Debug(LogTag.IAPMobile, $"商店连接断开，详情={description.message}");
            m_RuntimeContext.MarkDisconnected();
            if (m_RuntimeContext.IsInitializing)
            {
                FailInitialization(MobileStoreInitFailureReason.StoreDisconnected, $"初始化期间连接断开: {description.message}");
            }
        }

        /// <summary>
        /// 商品拉取成功回调（由 MobileStoreService 路由）：商品信息可用后触发一次平台已有购买拉取。
        /// 初始化状态不再依赖该回调，但启动补单依赖 OnPurchasesFetched 恢复 PendingOrder 凭据。
        /// </summary>
        /// <param name="products">成功拉取的商品列表。</param>
        internal void OnProductsFetched(List<Product> products)
        {
            if (m_RuntimeContext == null)
            {
                return;
            }

            Log.Debug(LogTag.IAPMobile, $"商品拉取成功，共 {products.Count} 个。");
            m_Hub.RestoreService.RequestPlatformRestoreAfterProductsFetched();
            m_Hub.ExtendedService.FetchPurchases();
        }

        /// <summary>
        /// 商品拉取失败回调（由 MobileStoreService 路由）：记录不可用 SKU，不再回退初始化结果。
        /// </summary>
        /// <param name="failure">失败详情。</param>
        internal void OnProductsFetchFailed(ProductFetchFailed failure)
        {
            if (m_RuntimeContext == null)
            {
                return;
            }

            Log.Warning(LogTag.IAPMobile, $"商品拉取失败，数量={failure.FailedFetchProducts.Count}，原因={failure.FailureReason}");
            foreach (var def in failure.FailedFetchProducts)
            {
                m_Hub.Store.AddUnavailableSkuInternal(def.id);
            }
        }

        /// <summary>
        /// 释放服务持有资源，通知 ExtendedService 清空 Controller 引用并重置状态。
        /// </summary>
        internal void Dispose()
        {
            // 清空 Controller 引用，断开平台回调
            m_Hub.ExtendedService?.Dispose();
            // 清空状态机，阻止 Dispose 后的回调继续执行
            m_RuntimeContext = null;
            // 标记服务不可用
            IsReady = false;
            // 释放可能悬空的 TCS
            m_InitTcs = null;
        }
    }
}
