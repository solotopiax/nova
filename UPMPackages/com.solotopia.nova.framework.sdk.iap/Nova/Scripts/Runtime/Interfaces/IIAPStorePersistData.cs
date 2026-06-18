/***************************************************************
 * (c) copyright 2026 - 2030, Solotopia
 * All Rights Reserved.
 * -------------------------------------------------------------
 * filename:  IIAPStorePersistData.cs
 * author:    yingzheng
 * created:   2026/5/27
 * descrip:   IAP 各 Store 本地存档容器的统一契约
 ***************************************************************/

namespace NovaFramework.SDK.IAP.Runtime
{
    /// <summary>
    /// IAP 各 Store 本地存档容器的统一契约。
    /// IAPStoreBase 通过 IPersistManager.GetObject/SetObject 单原子读写实现：
    /// classify = "iap_{storeType.ToLowerInvariant()}"，item = "data_{uid}"。
    /// 各 Store 的存档容器须为 [System.Serializable] 并实现本接口；
    /// 反序列化结果可能字段为 null（视底层 JSON 行为而定），
    /// 由 EnsureInitialized 兜底初始化集合或字符串字段。
    /// </summary>
    public interface IIAPStorePersistData
    {
        /// <summary>
        /// 反序列化后或新建空容器后，由 IAPStoreBase 调用一次以保证集合/字符串字段非 null。
        /// </summary>
        void EnsureInitialized();
    }
}
