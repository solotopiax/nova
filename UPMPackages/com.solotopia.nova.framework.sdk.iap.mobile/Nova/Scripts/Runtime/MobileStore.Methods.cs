/***************************************************************
 * (c) copyright 2026 - 2030, Solotopia
 * All Rights Reserved.
 * -------------------------------------------------------------
 * filename:  MobileStore.Methods.cs
 * author:    yingzheng
 * created:   2026/5/28
 * descrip:   MobileStore 非公开方法
 ***************************************************************/

using System;
using NovaFramework.Runtime;
using NovaFramework.SDK.IAP.Runtime;
using UnityEngine.Purchasing;

namespace NovaFramework.SDK.IAP.Mobile.Runtime
{
    public sealed partial class MobileStore
    {
        /// <summary>
        /// 将指定商品 ID 标记为不可购买（由 MobileInitService 回调）。
        /// </summary>
        /// <param name="productId">平台商品 ID。</param>
        internal void AddUnavailableSkuInternal(string productId) => AddUnavailableSku(productId);

        /// <summary>
        /// 清空平台不可购买 SKU 标记（由商品拉取重试前调用）。
        /// </summary>
        internal void ClearUnavailableSkusInternal() => ClearUnavailableSkus();

        /// <summary>
        /// 判断指定商品 ID 是否已被平台标记为不可购买（拉取失败），供各内部服务在查询/购买前提前拦截。
        /// </summary>
        /// <param name="productId">平台商品 ID。</param>
        /// <returns>已标记为不可购买时返回 true。</returns>
        internal bool IsUnavailableSkuInternal(string productId) => IsUnavailableSku(productId);

        /// <summary>
        /// 加载当前账号存档到 m_PersistData，PersistManager 不可用时落回空容器。
        /// 切换 UID 后由 SetAccountID 重新调用。
        /// </summary>
        internal void LoadPersistDataInternal()
        {
            m_PersistData = LoadPersistData<MobileStorePersistData>();
        }

        /// <summary>
        /// 将当前 m_PersistData 单原子写入持久化层。供内部服务在改动字段后调用。
        /// </summary>
        internal void SavePersistDataInternal()
        {
            SavePersistData(m_PersistData);
        }

        /// <summary>
        /// Mobile 的 guard 失败不在基类直接上报；PayGuardAsync 返回失败结果后由 PayAsync 返回边界统一上报。
        /// </summary>
        /// <param name="result">支付前置校验生成的失败结果。</param>
        /// <returns>固定返回 false，禁止基类在 guard 内直接上报。</returns>
        protected override bool ShouldTrackPayGuardFailure(IAPResult result)
        {
            return false;
        }

        /// <summary>
        /// 上报 MobileStore.PayAsync 返回的失败结果，作为移动内购失败打点的统一出口。
        /// </summary>
        /// <param name="result">PayAsync 返回的支付结果。</param>
        internal void TrackReturnedPayFailureInternal(IAPResult result)
        {
            if (result == null || result.IsSuccess)
            {
                return;
            }

            IAPMobileErrorCode reason = MapPayFailureResultToMobileReason(result);
            string reasonDetail = string.IsNullOrEmpty(result.ErrorDesc)
                ? $"{result.ErrorSource}:{result.ErrorCode}"
                : $"{result.ErrorSource}:{result.ErrorCode} {result.ErrorDesc}";
            Product product = ResolveTrackProduct(result.TableId);
            TrackLocalPayFail(tableId: result.TableId,
                productId: ResolveProductId(result.TableId, product),
                debug: IsTrackDebugMode(),
                price: ResolvePrice(result.TableId),
                reason: reason,
                reasonDetail: reasonDetail,
                customData: result.CustomData);
        }

        /// <summary>
        /// 将 PayAsync 失败结果映射到 Mobile 打点使用的 IAPMobileErrorCode 枚举域。
        /// </summary>
        /// <param name="result">PayAsync 返回的失败结果。</param>
        /// <returns>用于 nova_reason 的 Mobile 错误码。</returns>
        private IAPMobileErrorCode MapPayFailureResultToMobileReason(IAPResult result)
        {
            if (result.ErrorSource == IAPErrorSource.Mobile)
            {
                return Enum.IsDefined(typeof(IAPMobileErrorCode), result.ErrorCode)
                    ? (IAPMobileErrorCode)result.ErrorCode
                    : IAPMobileErrorCode.StoreNotAvailable;
            }

            if (result.ErrorSource != IAPErrorSource.PluginRouter ||
                !Enum.IsDefined(typeof(IAPPluginErrorCode), result.ErrorCode))
            {
                return IAPMobileErrorCode.StoreNotAvailable;
            }

            return (IAPPluginErrorCode)result.ErrorCode switch
            {
                IAPPluginErrorCode.ProductNotFound => IAPMobileErrorCode.ProductNotFound,
                IAPPluginErrorCode.StoreInitFailed => IAPMobileErrorCode.StoreInitFailed,
                IAPPluginErrorCode.AlreadyPurchasing => IAPMobileErrorCode.AlreadyPurchasing,
                IAPPluginErrorCode.StoreNotAvailable => IAPMobileErrorCode.StoreNotAvailable,
                _ => IAPMobileErrorCode.StoreNotAvailable,
            };
        }

        /// <summary>
        /// 解析失败打点使用的 Unity IAP 商品对象。
        /// </summary>
        /// <param name="tableId">商品配置表行 ID。</param>
        /// <returns>Unity IAP Product；无法解析时返回 null。</returns>
        private Product ResolveTrackProduct(long tableId)
        {
            IAPProductEntry entry = Table?.FindByTableId(tableId);
            return string.IsNullOrEmpty(entry?.ProductID) ? null : m_Hub?.ProductService?.GetProduct(entry.ProductID);
        }

        /// <summary>
        /// 覆写基类订阅到期时间读取，路由给 MobileSubscriptionService。
        /// </summary>
        /// <param name="tableId">订阅商品配置表行 ID。</param>
        /// <returns>到期 Unix 毫秒时间戳；未存档时返回 0。</returns>
        protected override long GetSubscriptionExpireTimeMs(long tableId) => m_Hub?.SubscriptionService?.GetExpireTimeMs(tableId) ?? 0L;

        /// <summary>
        /// 覆写基类工厂，提供 MobileStore 专属空存档容器。
        /// </summary>
        /// <returns>已 EnsureInitialized 的 MobileStorePersistData 实例。</returns>
        protected override IIAPStorePersistData CreateEmptyPersistData()
        {
            var data = new MobileStorePersistData();
            data.EnsureInitialized();
            return data;
        }
    }
}
