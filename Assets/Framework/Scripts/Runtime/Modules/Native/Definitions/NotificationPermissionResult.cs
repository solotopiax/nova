/***************************************************************
 * (c) copyright 2026 - 2030, Solotopia
 * All Rights Reserved.
 * -------------------------------------------------------------
 * filename:  NotificationPermissionResult.cs
 * author:    taoye
 * created:   2026/8/7
 * descrip:   通知权限请求结果
 ***************************************************************/

namespace NovaFramework.Runtime
{
    /// <summary>
    /// 通知权限请求结果。用户拒绝仍属于成功完成的系统请求，不等同于原生调用失败。
    /// </summary>
    public readonly struct NotificationPermissionResult
    {
        /// <summary>
        /// 创建通知权限请求结果。
        /// </summary>
        /// <param name="isOperationSuccessful">原生请求流程是否成功完成。</param>
        /// <param name="status">请求完成后重新查询得到的系统权威状态。</param>
        /// <param name="errorCode">原生错误码；无错误时为 0。</param>
        /// <param name="errorDomain">原生错误域；无错误时为空。</param>
        /// <param name="errorMessage">原生错误描述；无错误时为空。</param>
        public NotificationPermissionResult(
            bool isOperationSuccessful,
            NotificationPermissionStatus status,
            long errorCode = 0,
            string errorDomain = null,
            string errorMessage = null)
        {
            IsOperationSuccessful = isOperationSuccessful;
            Status = status;
            ErrorCode = errorCode;
            ErrorDomain = errorDomain ?? string.Empty;
            ErrorMessage = errorMessage ?? string.Empty;
        }

        /// <summary>
        /// 原生请求流程是否成功完成。
        /// </summary>
        public bool IsOperationSuccessful { get; }

        /// <summary>
        /// 请求完成后的系统权威状态。
        /// </summary>
        public NotificationPermissionStatus Status { get; }

        /// <summary>
        /// 原生错误码；无错误时为 0。
        /// </summary>
        public long ErrorCode { get; }

        /// <summary>
        /// 原生错误域；无错误时为空。
        /// </summary>
        public string ErrorDomain { get; }

        /// <summary>
        /// 原生错误描述；无错误时为空。
        /// </summary>
        public string ErrorMessage { get; }
    }
}

