/***************************************************************
 * (c) copyright 2026 - 2030, Solotopia
 * All Rights Reserved.
 * -------------------------------------------------------------
 * filename:  IAPStoreType.cs
 * author:    yingzheng
 * created:   2026/5/22
 * descrip:   IAP store 类型枚举，用于标识各渠道 store 并替代字符串路由
 ***************************************************************/

namespace NovaFramework.SDK.IAP.Runtime
{
    /// <summary>
    /// IAP store 渠道类型枚举。
    /// 用于标识每个 IIAPInternalStore 实现所属渠道，
    /// 替代旧有 StoreTypeName 字符串路由，消除魔法字符串风险。
    /// </summary>
    public enum IAPStoreType
    {
        /// <summary>
        /// 未指定渠道，默认占位值，正常运行时不应出现此值。
        /// </summary>
        None = 0,

        /// <summary>
        /// Google Play 与 iOS App Store 官方移动内购渠道。
        /// 对应 MobileStore 实现。
        /// </summary>
        Mobile = 1,

        /// <summary>
        /// 第三方支付渠道（WebView / 系统浏览器方式）。
        /// 对应 ThirdPayStore 实现。
        /// </summary>
        ThirdPay = 2,

        /// <summary>
        /// 代金券与金币虚拟货币渠道。
        /// 对应 VoucherStore 实现。
        /// </summary>
        Voucher = 3,
    }
}
