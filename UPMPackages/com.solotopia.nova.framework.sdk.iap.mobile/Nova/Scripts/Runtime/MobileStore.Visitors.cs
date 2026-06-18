/***************************************************************
 * (c) copyright 2026 - 2030, Solotopia
 * All Rights Reserved.
 * -------------------------------------------------------------
 * filename:  MobileStore.Visitors.cs
 * author:    yingzheng
 * created:   2026/5/25
 * descrip:   MobileStore 字段与属性
 ***************************************************************/

using System.Collections.Generic;
using NovaFramework.SDK.IAP.Runtime;
using NovaFramework.Runtime;

namespace NovaFramework.SDK.IAP.Mobile.Runtime
{
    public sealed partial class MobileStore
    {
        /// <summary>
        /// 渠道标识，用于打点事件的 Channel 字段。
        /// </summary>
        protected override string TrackChannel
        {
            get
            {
#if UNITY_ANDROID
                return "google";
#elif UNITY_IOS
                return "ios";
#else
                return IAPStoreType.Mobile.ToString().ToLowerInvariant();
#endif
            }
        }

        /// <summary>
        /// 日志标签字符串，固定为 IAPMobile。
        /// </summary>
        protected override string StoreLogTag => LogTag.IAPMobile;

        /// <summary>
        /// 当前登录用户 UID，供 Service 读取用于透传参数编码。
        /// </summary>
        internal string GameUID => m_GameUID;

        /// <summary>
        /// 当前运行期已处理过的交易订单号，用于避免 Pending / Confirmed 双回调重复打点和重复验单。
        /// </summary>
        private readonly HashSet<string> m_RuntimeHandledTransactionIds = new HashSet<string>();

        /// <summary>
        /// 当前正在进行中的支付对应的配置表行 ID，供 MobilePurchaseService 读写；0 = 空闲。
        /// </summary>
        internal long InPayTableId { get => m_InPayTableId; set => m_InPayTableId = value; }

        /// <summary>
        /// store 是否已通过 Unity IAP StoreController 初始化就绪。
        /// </summary>
        protected override bool IsStoreReady => m_Hub?.InitService?.IsReady == true && m_Hub.ExtendedService?.IsAttached == true;

        /// <summary>
        /// 服务容器，持有共享外部依赖与内部服务引用。
        /// </summary>
        private MobileServiceHub m_Hub;

        /// <summary>
        /// 当前账号下的本地存档统一容器，按 m_GameUID 隔离。
        /// 由 InitializeAsync / SetAccountID 通过基类 LoadPersistData 加载，DisposeAsync 置空。
        /// </summary>
        private MobileStorePersistData m_PersistData;

        /// <summary>
        /// 暴露给内部服务的当前存档容器引用；为 null 时由 LoadPersistDataInternal 兜底初始化。
        /// </summary>
        internal MobileStorePersistData PersistData => m_PersistData;
    }
}
