/***************************************************************
 * (c) copyright 2026 - 2030, Solotopia
 * All Rights Reserved.
 * -------------------------------------------------------------
 * filename:  IIAPStoreConfig.cs
 * author:    yingzheng
 * created:   2026/5/20
 * descrip:   IAP store 配置接口，由各渠道实现并通过 SerializeReference 多态序列化
 ***************************************************************/

using NovaFramework.Runtime;

namespace NovaFramework.SDK.IAP.Runtime
{
    /// <summary>
    /// IAP store 配置接口。
    /// 各渠道 store 的配置类须实现此接口，IAPPluginConfig 以
    /// [SerializeReference] List 多态持有所有渠道配置。
    /// IAPPlugin 启动期根据 StoreType 构建字典并按 store.StoreType 查表注入。
    /// </summary>
    public interface IIAPStoreConfig
    {
        /// <summary>
        /// 当前配置对应的 store 渠道类型；用于 IAPPlugin 构建运行期字典。
        /// 为 IAPStoreType.None 时视为非法配置，将被启动期跳过并打 Warning。
        /// </summary>
        IAPStoreType StoreType { get; }

        /// <summary>
        /// 当前 store 的默认启用状态。
        /// IAPPlugin 初始化时读取此值：false 时跳过 InitializeAsync（懒初始化），等 SetStoreEnabled(true) 时再补初始化；
        /// 运行时可通过 IAPPlugin.SetStoreEnabled 动态覆盖，默认为 true。
        /// </summary>
        bool Enabled { get; }
    }
}
