/***************************************************************
 * (c) copyright 2026 - 2030, Solotopia
 * All Rights Reserved.
 * -------------------------------------------------------------
 * filename:  MobileOrderRecord.cs
 * author:    yingzheng
 * created:   2026/5/25
 * descrip:   移动端单条订单存档结构，按 UID 隔离持久化
 ***************************************************************/

using System;
using Newtonsoft.Json;

namespace NovaFramework.SDK.IAP.Mobile.Runtime
{
    /// <summary>
    /// 移动端单条订单的持久化存档。
    /// 整个存档以 Dictionary&lt;long, MobileOrderRecord&gt; 形式序列化，key 为 tableId。
    /// 非终态订单（Purchasing/PendingValidate/ValidateFailed）保留供下次启动补单扫描；
    /// LocalPayFailed 只作为平台失败后的终态清理标记，扫描时直接删除。
    /// </summary>
    [Serializable]
    public sealed class MobileOrderRecord
    {
        /// <summary>
        /// 平台订单 ID。Android 运行期可写入 Google order id，但不落本地存档；
        /// iOS 写入 Apple transaction id，并随本地存档保留供补单验单使用。
        /// </summary>
#if UNITY_ANDROID
        [NonSerialized]
        [JsonIgnore]
#endif
        public string TransactionId;

        /// <summary>
        /// 商品配置表行 ID，与 IAPRequest.TableId 一致。
        /// </summary>
        public long TableId;

        /// <summary>
        /// Google Play 购买 token（purchaseToken），用于服务端 Android 验单；iOS 为空。
        /// </summary>
        public string GoogleToken;

        /// <summary>
        /// 订单当前状态，驱动补单扫描行为。
        /// </summary>
        public MobileOrderStatus Status;

        /// <summary>
        /// true = 补单重试（上次验单失败遗留），验单时不重试；false = 首次支付，允许重试。
        /// </summary>
        public bool IsReplenish;

        /// <summary>
        /// 业务透传数据，原样回传给 IAPResult.CustomData.Param。
        /// </summary>
        public string CustomDataParam;
    }
}
