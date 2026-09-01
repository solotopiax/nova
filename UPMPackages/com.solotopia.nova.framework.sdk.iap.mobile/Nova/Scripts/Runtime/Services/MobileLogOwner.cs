/***************************************************************
 * (c) copyright 2026 - 2030, Solotopia
 * All Rights Reserved.
 * -------------------------------------------------------------
 * filename:  MobileLogOwner.cs
 * author:    yingzheng
 * created:   2026/8/31
 * descrip:   Mobile IAP 内部服务日志基类
 ***************************************************************/

using NovaFramework.SDK.IAP.Runtime;

namespace NovaFramework.SDK.IAP.Mobile.Runtime
{
    /// <summary>
    /// Mobile IAP 内部服务日志基类。
    /// 服务继承后可直接调用 LogDebug / LogWarning / LogError，
    /// 日志标签固定解析为 IAP Mobile。
    /// </summary>
    public abstract class MobileLogOwner : IAPLogOwner
    {
        /// <summary>
        /// Mobile 服务固定使用官方移动内购日志标签。
        /// </summary>
        protected override string LogTag => NovaFramework.Runtime.LogTag.IAPMobile;
    }
}
