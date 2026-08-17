/***************************************************************
 * (c) copyright 2026 - 2030, Solotopia
 * All Rights Reserved.
 * -------------------------------------------------------------
 * filename:  InAppReviewRequestStatus.cs
 * author:    taoye
 * created:   2026/8/14
 * descrip:   应用内评价请求状态定义
 ***************************************************************/

namespace NovaFramework.Runtime
{
    /// <summary>
    /// 应用内评价请求的原生流程状态，不表示系统弹窗、用户评价或提交结果。
    /// </summary>
    public enum InAppReviewRequestStatus
    {
        /// <summary>
        /// 当前平台或系统版本不支持该能力。
        /// </summary>
        Unsupported = 0,

        /// <summary>
        /// 当前没有可用于发起请求的前台原生界面。
        /// </summary>
        Unavailable = 1,

        /// <summary>
        /// 已将请求交给系统原生流程；不表示系统实际展示弹窗或用户完成评价。
        /// </summary>
        RequestDispatched = 2,

        /// <summary>
        /// 原生桥接或平台请求链报告技术失败。
        /// </summary>
        Failed = 3,
    }
}
