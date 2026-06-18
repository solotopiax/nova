/***************************************************************
 * (c) copyright 2026 - 2030, Solotopia
 * All Rights Reserved.
 * -------------------------------------------------------------
 * filename:  ThirdPayOpenResult.cs
 * author:    yingzheng
 * created:   2026/5/26
 * descrip:   第三方支付页打开结果三态枚举
 ***************************************************************/

namespace NovaFramework.SDK.IAP.ThirdPay.Runtime
{
    /// <summary>
    /// 第三方支付页打开结果。
    /// 由 ThirdPayStore.OpenAsync 返回，区分用户路径以决定主链路下一步。
    /// </summary>
    public enum ThirdPayOpenResult
    {
        /// <summary>
        /// 用户支付完成回到 App（Browser 模式由 AppsFlyer DeepLink 命中 OrderId / InAppAuto 模式由 WebView 关闭或 JS bridge 通知）。
        /// </summary>
        Success,

        /// <summary>
        /// 用户主动取消支付页面。
        /// </summary>
        Cancel,

        /// <summary>
        /// 打开失败 / 进程异常 / 不确定状态——保留订单走补单链路。
        /// </summary>
        Failed,
    }
}
