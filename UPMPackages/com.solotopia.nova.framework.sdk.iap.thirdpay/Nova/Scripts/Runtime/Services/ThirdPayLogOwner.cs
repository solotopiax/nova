/***************************************************************
 * (c) copyright 2026 - 2030, Solotopia
 * All Rights Reserved.
 * -------------------------------------------------------------
 * filename:  ThirdPayLogOwner.cs
 * author:    yingzheng
 * created:   2026/8/31
 * descrip:   ThirdPay 内部服务日志基类
 ***************************************************************/

using NovaFramework.SDK.IAP.Runtime;

namespace NovaFramework.SDK.IAP.ThirdPay.Runtime
{
    /// <summary>
    /// ThirdPay 内部服务日志基类。
    /// 服务继承后可直接调用 LogDebug / LogWarning / LogError，
    /// 日志标签固定解析为 IAP ThirdPay。
    /// </summary>
    public abstract class ThirdPayLogOwner : IAPLogOwner
    {
        /// <summary>
        /// ThirdPay 服务固定使用第三方支付日志标签。
        /// </summary>
        protected override string LogTag => NovaFramework.Runtime.LogTag.IAPThirdPay;
    }
}
