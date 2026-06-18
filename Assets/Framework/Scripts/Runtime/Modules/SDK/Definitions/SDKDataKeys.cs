/***************************************************************
 * (c) copyright 2026 - 2030, Solotopia
 * All Rights Reserved.
 * -------------------------------------------------------------
 * filename:  SDKDataKeys.cs
 * author:    yingzheng
 * created:   2026/4/29
 * descrip:   SDK 插件间共享数据槽位的 key 常量
 ***************************************************************/

namespace NovaFramework.Runtime
{
    /// <summary>
    /// SDK 插件间共享数据槽位的 key 常量集合。
    /// 配合 SDKPluginBase.PublishData / FetchDataAsync 使用；
    /// 同一 key 由唯一发布方 Publish，消费方 Fetch 后按约定类型强转。
    /// </summary>
    public static class SDKDataKeys
    {
        /// <summary>
        /// AppsFlyer 设备 ID（string 类型）。
        /// 由 AppsFlyerPlugin 初始化完成后发布；
        /// 业务层 Fetch 后可写入 TGA UserSetOnce 属性 nova_appsflyer_id。
        /// </summary>
        public const string AppsFlyerId = "AppsFlyerId";

        /// <summary>
        /// TGA 设备 ID（string 类型）。
        /// 由 TGAPlugin 初始化完成后发布；
        /// 业务层 Fetch 后可写入 TGA UserSetOnce 属性 nova_tga_devices_id。
        /// </summary>
        public const string TGADevicesId = "TGADevicesId";

        /// <summary>
        /// TGA 设备 ID（string 类型）。
        /// 由 TGAPlugin 初始化完成后发布；
        /// 业务层 Fetch 后可写入 TGA UserSetOnce 属性 nova_tga_distinct_id。
        /// </summary>
        public const string TGADistinctId = "TGADistinctId";

        /// <summary>
        /// TGA 账号 ID（string 类型）。
        /// 由 TGAPlugin 在 SDKEventData.UserLogin 触发后通过 PublishData 发布；
        /// 与 login.UserId 一致，供登录后上报协议消费。
        /// </summary>
        public const string TGAAccountId = "TGAAccountId";

        /// <summary>
        /// Firebase 云推送 Token（string 类型）。
        /// 由 FirebasePlugin 在 FCM TokenReceived 回调内通过 PublishData 发布；
        /// 业务层 Fetch 后可作为登录后上报协议参数。
        /// </summary>
        public const string FirebasePushToken = "FirebasePushToken";

        /// <summary>
        /// Firebase Analytics 实例 ID（string 类型）。
        /// 由 FirebasePlugin 在 GetAnalyticsInstanceIdAsync 回调内通过 PublishData 发布；
        /// 业务层 Fetch 后可作为登录后上报协议参数。
        /// </summary>
        public const string FirebaseAnalyticsInstanceId = "FirebaseAnalyticsInstanceId";
    }
}
