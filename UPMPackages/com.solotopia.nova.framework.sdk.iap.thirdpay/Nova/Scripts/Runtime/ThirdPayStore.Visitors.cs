/***************************************************************
 * (c) copyright 2026 - 2030, Solotopia
 * All Rights Reserved.
 * -------------------------------------------------------------
 * filename:  ThirdPayStore.Visitors.cs
 * author:    yingzheng
 * created:   2026/5/20
 * descrip:   ThirdPayStore 字段、属性、常量
 ***************************************************************/

using Cysharp.Threading.Tasks;
using NovaFramework.SDK.IAP.Runtime;
using NovaFramework.SDK.IAP.ThirdPay.Runtime.Internal;
using NovaFramework.Runtime;

namespace NovaFramework.SDK.IAP.ThirdPay.Runtime
{
    public partial class ThirdPayStore
    {
        /// <summary>
        /// 渠道标识，用于打点事件的 Channel 字段；取枚举名小写确保与 StoreType 枚举一致。
        /// </summary>
        protected override string TrackChannel => IAPStoreType.ThirdPay.ToString().ToLowerInvariant();

        /// <summary>
        /// 日志标签字符串，固定为 IAPThirdPay。
        /// </summary>
        protected override string StoreLogTag => LogTag.IAPThirdPay;

        /// <summary>
        /// 服务端验单 status：支付成功，待发货。
        /// </summary>
        private const int c_StatusPaySuccess = 3;

        /// <summary>
        /// 服务端验单 status：已发货（奖励已到账）。
        /// </summary>
        private const int c_StatusReceivedAward = 5;

        /// <summary>
        /// 服务端验单 status：明确支付失败（含过期）。
        /// </summary>
        private const int c_StatusPayFailed = 4;

        /// <summary>
        /// 服务端验单 status：待支付。
        /// </summary>
        private const int c_StatusNotPay = 1;

        /// <summary>
        /// Browser 模式 OpenAsync 等待 DeepLink 回调的最长时长（秒）。
        /// </summary>
        private const float c_BrowserWaitTimeoutSeconds = 60f;

        /// <summary>
        /// 服务端校验订单时的延迟重试间隔（秒），对应 IAP3 的 m_ValidateTimeInterval。
        /// </summary>
        private static readonly float[] s_ValidateRetryIntervals = { 0.2f, 0.5f, 1.0f, 2.0f, 4.0f };

        /// <summary>
        /// store 专属配置，InitializeAsync 阶段注入。
        /// </summary>
        private ThirdPayStoreConfig m_Config;

        /// <summary>
        /// 业务网络 Service，持有已注入的 NetCmd 行并封装三条协议发送。
        /// </summary>
        private ThirdIapNetService m_IapNetService;

        /// <summary>
        /// 服务端返回的第三方商品列表（含商品信息和支付渠道信息）。
        /// 未拉取时为 null。
        /// </summary>
        private PbNetThirdProductListResp m_ProductListInfo;

        /// <summary>
        /// 当前账号的本地存档统一容器，按 m_GameUID 为 Key 读写。
        /// SetAccountID 阶段加载，DisposeAsync 阶段清空。
        /// </summary>
        private ThirdPayPersistData m_PersistData;

        /// <summary>
        /// 当前国家/地区代码，由业务层通过 SetCountryCode 写入；用于 BuildAesUrl 的 country 字段。
        /// </summary>
        private string m_CountryCode = string.Empty;

        /// <summary>
        /// Google 外部链接合规扩展，仅 Android + InAppAuto 模式下启用。
        /// </summary>
        private ThirdPayGoogleExpand m_GoogleExpand;

        /// <summary>
        /// Browser 模式下等待 DeepLink 回调的 awaiter；OpenBrowserAsync 写入，OnBrowserDeepLinkResolved/超时清空。
        /// </summary>
        private UniTaskCompletionSource<ThirdPayOpenResult> m_BrowserOpenWaiter;
    }
}
