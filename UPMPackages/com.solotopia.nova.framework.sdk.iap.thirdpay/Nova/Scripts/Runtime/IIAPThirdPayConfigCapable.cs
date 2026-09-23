/***************************************************************
 * (c) copyright 2026 - 2030, Solotopia
 * All Rights Reserved.
 * -------------------------------------------------------------
 * filename:  IIAPThirdPayConfigCapable.cs
 * author:    yingzheng
 * created:   2026/9/17
 * descrip:   第三方支付配置状态能力接口
 ***************************************************************/

using NovaFramework.SDK.IAP.Runtime;

namespace NovaFramework.SDK.IAP.ThirdPay.Runtime
{
    /// <summary>
    /// 第三方支付国家配置与服务端可用状态能力。
    /// </summary>
    public interface IIAPThirdPayConfigCapable : IIAPCapable
    {
        /// <summary>
        /// 设置 Debug 覆盖用支付国家或地区代码；生产环境通常留空，由 Store 自动解析。
        /// </summary>
        /// <param name="countryCode">ISO 3166-1 alpha-2 国家或地区代码；空值表示取消 Debug 覆盖。</param>
        void SetDebugCountryCode(string countryCode);

        /// <summary>
        /// 获取 ThirdPay 当前实际使用的国家或地区代码；登录后首次读取会锁定自动解析结果。
        /// </summary>
        /// <returns>规范化后的 ISO 3166-1 alpha-2 国家或地区代码；无有效来源时返回空字符串。</returns>
        string GetCountryCode();

        /// <summary>
        /// 获取统一支付配置是否已经成功加载且仍在有效期内。
        /// </summary>
        bool IsPaymentConfigReady { get; }

        /// <summary>
        /// 获取服务端是否允许当前用户在当前国家发起新的第三方支付。
        /// </summary>
        bool IsPaymentAvailable { get; }

        /// <summary>
        /// 获取服务端返回的第三方支付关闭原因码；配置未就绪时返回 0，调用方需结合 IsPaymentConfigReady 判断。
        /// </summary>
        int PaymentDisabledReason { get; }
    }
}
