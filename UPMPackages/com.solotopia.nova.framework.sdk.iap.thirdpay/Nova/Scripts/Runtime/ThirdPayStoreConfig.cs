/***************************************************************
 * (c) copyright 2026 - 2030, Solotopia
 * All Rights Reserved.
 * -------------------------------------------------------------
 * filename:  ThirdPayStoreConfig.cs
 * author:    yingzheng
 * created:   2026/5/20
 * descrip:   第三方支付 store 配置
 ***************************************************************/

using System;
using UnityEngine;
using NovaFramework.SDK.IAP.Runtime;

namespace NovaFramework.SDK.IAP.ThirdPay.Runtime
{
    /// <summary>
    /// 第三方支付页面的打开方式。
    /// </summary>
    public enum IAPThirdOpenMode
    {
        /// <summary>
        /// 跳转系统默认浏览器打开（Google Play 外链合规推荐）。
        /// </summary>
        Browser,

        /// <summary>
        /// 在应用内嵌 WebView 打开（业务层覆写 OpenAsync 接入 WebView 实现）。
        /// </summary>
        InAppAuto,
    }

    /// <summary>
    /// 第三方支付 store 配置。
    /// 实现 IIAPStoreConfig 标记接口，由外部通过 Inspector 或代码赋值后
    /// 传入 ThirdPayStore.InitializeAsync。
    /// </summary>
    [Serializable]
    public sealed class ThirdPayStoreConfig : IIAPStoreConfig
    {
        /// <summary>
        /// 当前配置对应的 store 渠道类型，固定为 ThirdPay。
        /// </summary>
        public IAPStoreType StoreType => IAPStoreType.ThirdPay;

        /// <summary>
        /// 当前 store 是否启用；false 时 IAPPlugin 跳过初始化。
        /// </summary>
        [SerializeField, Tooltip("默认是否启用 ThirdPay Store；运行时可通过 IAPPlugin.SetStoreEnabled 覆盖")]
        private bool m_Enabled = true;
        /// <summary>
        /// 当前 store 是否启用。
        /// </summary>
        public bool Enabled => m_Enabled;

        /// <summary>
        /// Android 平台支付页面打开方式。
        /// </summary>
        [SerializeField]
        private IAPThirdOpenMode m_AndroidOpenMode = IAPThirdOpenMode.Browser;
        public IAPThirdOpenMode AndroidOpenMode => m_AndroidOpenMode;

        /// <summary>
        /// iOS 平台支付页面打开方式。
        /// </summary>
        [SerializeField]
        private IAPThirdOpenMode m_IosOpenMode = IAPThirdOpenMode.Browser;
        public IAPThirdOpenMode IosOpenMode => m_IosOpenMode;

        /// <summary>
        /// 当前设备/用户所在国家/地区代码（ISO 3166-1 alpha-2，如 "US"、"CN"），
        /// 传入第三方支付请求体的 country 字段。
        /// </summary>
        [SerializeField]
        private string m_CountryCode = string.Empty;
        public string CountryCode => m_CountryCode;

        /// <summary>
        /// 第三方支付平台分配的应用 ID，写入所有第三方支付请求体的 app_id 字段。
        /// </summary>
        [SerializeField]
        private int m_AppId;
        public int AppId => m_AppId;

        /// <summary>
        /// 第三方支付页面跳转地址（纯前端跳转，不走 NetCmd）。
        /// 客户端构造 AES 加密后的 query 拼接到此 URL 上跳转。
        /// </summary>
        [SerializeField, Tooltip("第三方支付页 URL 基址，客户端 AES query 拼接到此 URL 后跳转")]
        private string m_PayUrlBase = string.Empty;
        /// <summary>
        /// 第三方支付页面跳转地址。
        /// </summary>
        public string PayUrlBase => m_PayUrlBase;

        /// <summary>
        /// 拉取第三方商品列表的协议名（对应 INetworkCmdRow.Name）。
        /// 初始化时由 store 层通过 NetworkManager.ResolveNetCmdRow 解析为 INetworkCmdRow 注入 ThirdIapNetService。
        /// </summary>
        [SerializeField, Tooltip("拉取商品列表协议名，如 GetThirdProductList")]
        private string m_GetProductListCmdName = string.Empty;
        /// <summary>
        /// 拉取第三方商品列表协议名。
        /// </summary>
        public string GetProductListCmdName => m_GetProductListCmdName;

        /// <summary>
        /// 创建第三方支付订单的协议名（对应 INetworkCmdRow.Name）。
        /// 初始化时由 store 层通过 NetworkManager.ResolveNetCmdRow 解析为 INetworkCmdRow 注入 ThirdIapNetService。
        /// </summary>
        [SerializeField, Tooltip("创建订单协议名，如 CreateThirdOrder")]
        private string m_CreateOrderCmdName = string.Empty;
        /// <summary>
        /// 创建第三方支付订单协议名。
        /// </summary>
        public string CreateOrderCmdName => m_CreateOrderCmdName;

        /// <summary>
        /// 验证第三方支付订单的协议名（对应 INetworkCmdRow.Name）。
        /// 初始化时由 store 层通过 NetworkManager.ResolveNetCmdRow 解析为 INetworkCmdRow 注入 ThirdIapNetService。
        /// </summary>
        [SerializeField, Tooltip("验单协议名，如 CheckThirdOrder")]
        private string m_CheckOrderCmdName = string.Empty;
        /// <summary>
        /// 验证第三方支付订单协议名。
        /// </summary>
        public string CheckOrderCmdName => m_CheckOrderCmdName;
    }
}
