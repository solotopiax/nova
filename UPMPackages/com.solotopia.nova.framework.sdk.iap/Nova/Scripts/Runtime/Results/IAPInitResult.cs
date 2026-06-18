/***************************************************************
 * (c) copyright 2026 - 2030, Solotopia
 * All Rights Reserved.
 * -------------------------------------------------------------
 * filename:  IAPInitResult.cs
 * author:    yingzheng
 * created:   2026/5/26
 * descrip:   IAP 初始化结果
 ***************************************************************/

namespace NovaFramework.SDK.IAP.Runtime
{
    /// <summary>
    /// IAP store 初始化结果。
    /// 通过静态工厂方法 Success / Fail 构造，对外不暴露构造函数。
    /// </summary>
    public sealed class IAPInitResult
    {
        /// <summary>
        /// 是否初始化成功。
        /// </summary>
        public bool IsSuccess { get; }

        /// <summary>
        /// 初始化失败原因码，IsSuccess 为 true 时值为 0。
        /// 失败码由具体 Store 自行定义，IAP 核心层只负责透传。
        /// </summary>
        public int FailReason { get; }

        /// <summary>
        /// 附加详情字符串，用于诊断日志，IsSuccess 为 true 时为空字符串。
        /// </summary>
        public string Detail { get; }

        /// <summary>
        /// 构造初始化成功结果。
        /// </summary>
        /// <returns>IsSuccess=true 的初始化结果实例。</returns>
        public static IAPInitResult Success() => new IAPInitResult(true, 0, string.Empty);

        /// <summary>
        /// 构造初始化失败结果。
        /// </summary>
        /// <param name="reason">具体 Store 定义的失败原因码。</param>
        /// <param name="detail">附加详情字符串。</param>
        /// <returns>IsSuccess=false 的初始化结果实例。</returns>
        public static IAPInitResult Fail(int reason, string detail) => new IAPInitResult(false, reason, detail ?? string.Empty);

        /// <summary>
        /// 私有构造函数，外部通过 Success / Fail 工厂方法创建实例。
        /// </summary>
        /// <param name="isSuccess">是否成功。</param>
        /// <param name="failReason">失败原因码。</param>
        /// <param name="detail">附加详情。</param>
        private IAPInitResult(bool isSuccess, int failReason, string detail)
        {
            IsSuccess = isSuccess;
            FailReason = failReason;
            Detail = detail;
        }
    }
}
