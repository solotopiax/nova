/***************************************************************
 * (c) copyright 2026 - 2030, Solotopia
 * All Rights Reserved.
 * -------------------------------------------------------------
 * filename:  IIAPThirdPayOrderRecoveryCapable.cs
 * author:    yingzheng
 * created:   2026/11/5
 * descrip:   Android 第三方支付购买前补单查询能力
 ***************************************************************/

using NovaFramework.SDK.IAP.Runtime;

namespace NovaFramework.SDK.IAP.ThirdPay.Runtime
{
    /// <summary>
    /// 控制 Android 重复点击购买前的服务端补单查询，不影响独立后台补单。
    /// </summary>
    public interface IIAPThirdPayOrderRecoveryCapable : IIAPCapable
    {
        /// <summary>
        /// 设置是否启用 Android 购买前补单查询；关闭后清理当前业务旧记录再正常购买。
        /// </summary>
        /// <param name="isEnabled">是否启用查询。</param>
        void SetAndroidThirdPayOrderRecoveryEnabled(bool isEnabled);

        /// <summary>
        /// 设置查询窗口，限制为零至三十分钟；零表示跳过购买前查询。
        /// </summary>
        /// <param name="windowMinutes">查询窗口分钟数。</param>
        void SetAndroidThirdPayOrderRecoveryWindowMinutes(int windowMinutes);

        /// <summary>
        /// 获取当前运行时查询开关。
        /// </summary>
        bool IsAndroidThirdPayOrderRecoveryEnabled { get; }

        /// <summary>
        /// 获取当前运行时查询窗口分钟数。
        /// </summary>
        int AndroidThirdPayOrderRecoveryWindowMinutes { get; }
    }
}
