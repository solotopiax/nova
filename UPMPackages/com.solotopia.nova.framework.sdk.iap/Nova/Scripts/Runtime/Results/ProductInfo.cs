/***************************************************************
 * (c) copyright 2026 - 2030, Solotopia
 * All Rights Reserved.
 * -------------------------------------------------------------
 * filename:  ProductInfo.cs
 * author:    yingzheng
 * created:   2026/5/20
 * descrip:   平台商店商品信息（价格、标题等展示数据）
 ***************************************************************/

namespace NovaFramework.SDK.IAP.Runtime
{
    /// <summary>
    /// 平台商店商品信息。
    /// 由实现 IIAPQueryCapable 的 store 查询并返回，经 IAPPlugin.TryGetCapability 取用，
    /// 用于在 UI 中展示本地化价格与商品标题。
    /// 所有属性只读，创建后不可变更。
    /// </summary>
    public sealed class ProductInfo
    {
        /// <summary>
        /// 平台商店商品唯一标识（如 Google Play 或 App Store 的 ProductId）。
        /// </summary>
        public string ProductId { get; }

        /// <summary>
        /// 本地化价格字符串（含货币符号，如 "¥6.00"、"$0.99"），直接用于 UI 展示。
        /// </summary>
        public string LocalizedPrice { get; }

        /// <summary>
        /// ISO 4217 货币代码（如 "CNY"、"USD"），用于数值计算或日志记录。
        /// </summary>
        public string CurrencyCode { get; }

        /// <summary>
        /// 平台本地化商品标题，语言跟随设备地区设置。
        /// </summary>
        public string Title { get; }

        /// <summary>
        /// 构造商品信息。
        /// </summary>
        /// <param name="productId">平台商店商品唯一标识。</param>
        /// <param name="localizedPrice">本地化价格字符串（含货币符号）。</param>
        /// <param name="currencyCode">ISO 4217 货币代码。</param>
        /// <param name="title">平台本地化商品标题。</param>
        public ProductInfo(string productId, string localizedPrice, string currencyCode, string title)
        {
            ProductId = productId;
            LocalizedPrice = localizedPrice;
            CurrencyCode = currencyCode;
            Title = title;
        }
    }
}
