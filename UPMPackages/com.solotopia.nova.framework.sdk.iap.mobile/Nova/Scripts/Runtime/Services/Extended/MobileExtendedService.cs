/***************************************************************
 * (c) copyright 2026 - 2030, Solotopia
 * All Rights Reserved.
 * -------------------------------------------------------------
 * filename:  MobileExtendedService.cs
 * author:    yingzheng
 * created:   2026/5/27
 * descrip:   StoreController 调用唯一收口：封装所有平台调用，
 *            其他 Service 一律通过此服务调用，不直接访问 Controller
 ***************************************************************/

using System;
using System.Collections.Generic;
using Cysharp.Threading.Tasks;
using UnityEngine.Purchasing;

namespace NovaFramework.SDK.IAP.Mobile.Runtime
{
    /// <summary>
    /// StoreController 调用收口服务。
    /// 持有 StoreController 唯一引用，统一暴露所有平台调用入口。
    /// 初始化流程：SetController → RegisterStoreCallbacks → Connect → （OnStoreConnected 后）RegisterProductCallbacks。
    /// 其他 Service 通过 m_Hub.ExtendedService 访问，禁止直接持有或访问 Controller。
    /// </summary>
    internal sealed partial class MobileExtendedService
    {
        /// <summary>
        /// 构造 MobileExtendedService。
        /// </summary>
        /// <param name="hub">服务容器。</param>
        internal MobileExtendedService(MobileServiceHub hub)
        {
            m_Hub = hub;
        }

        /// <summary>
        /// 注入 Controller，InitializeAsync 第一步调用，确保后续所有平台操作都经过此服务。
        /// </summary>
        /// <param name="controller">从 UnityIAPServices.StoreController() 取得的控制器。</param>
        internal void SetController(StoreController controller)
        {
            if (m_Controller != null && m_Controller != controller)
            {
                DetachController();
            }
            m_Controller = controller;
        }

        /// <summary>
        /// 清空 Controller 引用，由 MobileInitService.Dispose 调用。
        /// </summary>
        internal void DetachController()
        {
            UnregisterProductCallbacks();
            UnregisterStoreCallbacks();
            m_Controller = null;
        }

        /// <summary>
        /// 注册商店级事件回调（Connected/Disconnected/购买系列/CheckEntitlement）。
        /// 回调目标为 MobileStoreService 的 On* 方法，SetController 之后、Connect 之前调用。
        /// </summary>
        internal void RegisterStoreCallbacks()
        {
            var ctrl = m_Controller;
            if (ctrl == null || m_StoreCallbacksRegistered)
            {
                return;
            }

            // 商店连接成功：注册商品级回调并通知 InitService 完成初始化。
            ctrl.OnStoreConnected += m_Hub.StoreService.OnStoreConnected;
            // 商店连接断开：初始化阶段转为失败，运行期只记录断连状态。
            ctrl.OnStoreDisconnected += m_Hub.StoreService.OnStoreDisconnected;
            // 平台购买进入 Pending：客户端验单成功后再 ConfirmPurchase。
            ctrl.OnPurchasePending += m_Hub.StoreService.OnPurchasePending;
            // 平台购买被延期：家长控制等场景，仅记录等待状态。
            ctrl.OnPurchaseDeferred += m_Hub.StoreService.OnPurchaseDeferred;
            // 平台确认结果：缓存票据并进入验单，或处理确认失败。
            ctrl.OnPurchaseConfirmed += m_Hub.StoreService.OnPurchaseConfirmed;
            // 平台购买失败：映射失败原因并结束当前支付等待。
            ctrl.OnPurchaseFailed += m_Hub.StoreService.OnPurchaseFailed;
            // 平台权益查询结果：Restore 流程收集订阅/非消耗品权益。
            ctrl.OnCheckEntitlement += m_Hub.StoreService.OnCheckEntitlement;
            m_StoreCallbacksRegistered = true;
        }

        /// <summary>
        /// 注册商品级事件回调（商品拉取成功/失败、历史购买拉取完成）。
        /// 在 OnStoreConnected 后由 StoreService 调用，商品回调注册晚于商店级回调。
        /// </summary>
        internal void RegisterProductCallbacks()
        {
            var ctrl = m_Controller;
            if (ctrl == null || m_ProductCallbacksRegistered)
            {
                return;
            }

            // 商品信息拉取成功：通知 InitService，并触发启动期平台已有购买拉取。
            ctrl.OnProductsFetched += m_Hub.StoreService.OnProductsFetched;
            // 商品信息拉取失败：记录不可用 SKU，不回退初始化状态。
            ctrl.OnProductsFetchFailed += m_Hub.StoreService.OnProductsFetchFailed;
            // 平台已有购买拉取成功：缓存历史票据并恢复 PendingOrder。
            ctrl.OnPurchasesFetched += m_Hub.StoreService.OnExistingPurchasesFetched;
            // 平台已有购买拉取失败：记录失败原因，等待后续补单或手动 Restore。
            ctrl.OnPurchasesFetchFailed += m_Hub.StoreService.OnExistingPurchasesFetchFailed;
            m_ProductCallbacksRegistered = true;
        }

        /// <summary>
        /// 注销商店级事件回调。
        /// </summary>
        private void UnregisterStoreCallbacks()
        {
            var ctrl = m_Controller;
            if (ctrl == null || !m_StoreCallbacksRegistered)
            {
                return;
            }

            // 商店连接成功监听退订。
            ctrl.OnStoreConnected -= m_Hub.StoreService.OnStoreConnected;
            // 商店连接断开监听退订。
            ctrl.OnStoreDisconnected -= m_Hub.StoreService.OnStoreDisconnected;
            // 平台 Pending 订单监听退订。
            ctrl.OnPurchasePending -= m_Hub.StoreService.OnPurchasePending;
            // 平台延期订单监听退订。
            ctrl.OnPurchaseDeferred -= m_Hub.StoreService.OnPurchaseDeferred;
            // 平台确认结果监听退订。
            ctrl.OnPurchaseConfirmed -= m_Hub.StoreService.OnPurchaseConfirmed;
            // 平台购买失败监听退订。
            ctrl.OnPurchaseFailed -= m_Hub.StoreService.OnPurchaseFailed;
            // 平台权益查询监听退订。
            ctrl.OnCheckEntitlement -= m_Hub.StoreService.OnCheckEntitlement;
            m_StoreCallbacksRegistered = false;
        }

        /// <summary>
        /// 注销商品级事件回调。
        /// </summary>
        private void UnregisterProductCallbacks()
        {
            var ctrl = m_Controller;
            if (ctrl == null || !m_ProductCallbacksRegistered)
            {
                return;
            }

            // 商品信息拉取成功监听退订。
            ctrl.OnProductsFetched -= m_Hub.StoreService.OnProductsFetched;
            // 商品信息拉取失败监听退订。
            ctrl.OnProductsFetchFailed -= m_Hub.StoreService.OnProductsFetchFailed;
            // 平台已有购买拉取成功监听退订。
            ctrl.OnPurchasesFetched -= m_Hub.StoreService.OnExistingPurchasesFetched;
            // 平台已有购买拉取失败监听退订。
            ctrl.OnPurchasesFetchFailed -= m_Hub.StoreService.OnExistingPurchasesFetchFailed;
            m_ProductCallbacksRegistered = false;
        }

        /// <summary>
        /// 异步连接商店，连接结果通过 OnStoreConnected / OnStoreDisconnected 回调返回。
        /// </summary>
        /// <returns>连接完成的 UniTask；Controller 未就绪时返回 CompletedTask。</returns>
        internal UniTask Connect()
        {
            if (m_Controller == null)
            {
                return UniTask.CompletedTask;
            }

            return m_Controller.Connect().AsUniTask();
        }

        /// <summary>
        /// 释放服务资源，清空 Controller 引用。
        /// </summary>
        internal void Dispose()
        {
            DetachController();
        }

        /// <summary>
        /// 发起商品购买。
        /// </summary>
        /// <param name="product">目标商品。</param>
        internal void PurchaseProduct(Product product)
        {
            m_Controller?.PurchaseProduct(product);
        }

        /// <summary>
        /// 通过商品 ID 获取 Product 对象；Controller 未就绪或商品不存在时返回 null。
        /// </summary>
        /// <param name="productId">平台商品 ID。</param>
        /// <returns>Product 对象；未找到时返回 null。</returns>
        internal Product GetProductById(string productId)
        {
            if (string.IsNullOrEmpty(productId))
            {
                return null;
            }

            return m_Controller?.GetProductById(productId);
        }

        /// <summary>
        /// 获取当前商店所有已拉取商品列表；Controller 未就绪时返回空集合。
        /// </summary>
        /// <returns>商品枚举；Controller 未就绪时返回 Array.Empty。</returns>
        internal IEnumerable<Product> GetProducts()
        {
            if (m_Controller == null)
            {
                return Array.Empty<Product>();
            }

            return m_Controller.GetProducts();
        }

        /// <summary>
        /// 确认待处理订单，通知平台发货完成。
        /// </summary>
        /// <param name="order">待确认的 PendingOrder。</param>
        internal void ConfirmPurchase(PendingOrder order)
        {
            m_Controller?.ConfirmPurchase(order);
        }

        /// <summary>
        /// 发起商品权益查询，结果通过 OnCheckEntitlement 回调返回。
        /// </summary>
        /// <param name="product">待查询的商品。</param>
        internal void CheckEntitlement(Product product)
        {
            m_Controller?.CheckEntitlement(product);
        }

        /// <summary>
        /// 触发平台历史订单恢复，结果通过回调返回。
        /// </summary>
        /// <param name="callback">恢复结果回调：(bool success, string errorInfo)。</param>
        internal void RestoreTransactions(Action<bool, string> callback)
        {
            m_Controller?.RestoreTransactions(callback);
        }

        /// <summary>
        /// 向平台发起商品信息拉取请求。
        /// </summary>
        /// <param name="defs">待拉取的商品定义列表。</param>
        internal void FetchProducts(List<ProductDefinition> defs)
        {
            m_Controller?.FetchProducts(defs);
        }

        /// <summary>
        /// 向平台拉取已有购买，结果通过 OnPurchasesFetched / OnPurchasesFetchFailed 回调返回。
        /// 用于启动期在商品信息可用后恢复 PendingOrder 的 receipt/token。
        /// </summary>
        internal void FetchPurchases()
        {
            m_Controller?.FetchPurchases();
        }

        /// <summary>
        /// 设置 Android ObfuscatedAccountId，非 Android 平台编译为空实现。
        /// </summary>
        /// <param name="id">编码后的账号 ID。</param>
        internal void SetObfuscatedAccountId(string id)
        {
#if UNITY_ANDROID
            m_Controller?.GooglePlayStoreExtendedService?.SetObfuscatedAccountId(id);
#endif
        }

        /// <summary>
        /// 设置 Android ObfuscatedProfileId，非 Android 平台编译为空实现。
        /// </summary>
        /// <param name="id">编码后的档案 ID。</param>
        internal void SetObfuscatedProfileId(string id)
        {
#if UNITY_ANDROID
            m_Controller?.GooglePlayStoreExtendedService?.SetObfuscatedProfileId(id);
#endif
        }

        /// <summary>
        /// 设置 iOS AppAccountToken，非 iOS 平台编译为空实现。
        /// </summary>
        /// <param name="token">账号关联 GUID Token。</param>
        internal void SetAppAccountToken(Guid token)
        {
#if UNITY_IOS
            m_Controller?.AppleStoreExtendedService?.SetAppAccountToken(token);
#endif
        }
    }
}
