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
    /// 由框架内支付页服务返回，区分用户路径以决定主链路下一步。
    /// </summary>
    internal enum ThirdPayOpenResult
    {
        /// <summary>
        /// 应用内支付页确认支付流程完成，可以立即发起验单。
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
