/***************************************************************
 * (c) copyright 2026 - 2030, Solotopia
 * All Rights Reserved.
 * -------------------------------------------------------------
 * filename:  InAppReviewRequestResult.cs
 * author:    taoye
 * created:   2026/8/14
 * descrip:   应用内评价请求结果
 ***************************************************************/

namespace NovaFramework.Runtime
{
    /// <summary>
    /// 应用内评价请求结果。系统不会向 Nova 回传弹窗展示、用户评价或提交状态。
    /// </summary>
    public readonly struct InAppReviewRequestResult
    {
        /// <summary>
        /// 创建应用内评价请求结果。
        /// </summary>
        /// <param name="status">平台原生请求状态。</param>
        /// <param name="errorMessage">技术失败时的框架错误描述。</param>
        public InAppReviewRequestResult(InAppReviewRequestStatus status, string errorMessage = null)
        {
            Status = status;
            ErrorMessage = errorMessage ?? string.Empty;
        }

        /// <summary>
        /// 平台原生请求状态。
        /// </summary>
        public InAppReviewRequestStatus Status { get; }

        /// <summary>
        /// 是否已将请求交给系统原生流程；不表示系统弹窗实际展示或用户完成评价。
        /// </summary>
        public bool IsRequestDispatched => Status == InAppReviewRequestStatus.RequestDispatched;

        /// <summary>
        /// 技术失败时的框架错误描述；非失败状态为空。
        /// </summary>
        public string ErrorMessage { get; }
    }
}
