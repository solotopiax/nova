/***************************************************************
 * (c) copyright 2026 - 2030, Solotopia
 * All Rights Reserved.
 * -------------------------------------------------------------
 * filename:  MobileStore.Methods.cs
 * author:    yingzheng
 * created:   2026/5/28
 * descrip:   MobileStore 非公开方法
 ***************************************************************/

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
