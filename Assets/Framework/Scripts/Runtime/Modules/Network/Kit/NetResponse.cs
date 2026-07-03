/***************************************************************
 * (c) copyright 2026 - 2030, Solotopia
 * All Rights Reserved.
 * -------------------------------------------------------------
 * filename:  NetResponse.cs
 * author:    taoye
 * created:   2026/4/18
 * descrip:   业务层网络响应泛型包装
 ***************************************************************/

using Google.Protobuf;

namespace NovaFramework.Runtime
{
    /// <summary>
    /// 网络请求响应包装，业务层通过此结构获取请求结果。
    /// 使用静态工厂方法 Success / Fail 创建，不允许外部直接实例化。
    /// </summary>
    /// <typeparam name="T">业务 Proto 响应消息类型。</typeparam>
    public sealed class NetResponse<T> where T : IMessage<T>
    {
        /// <summary>
        /// 请求是否成功。
        /// </summary>
        public bool IsSuccess { get; }

        /// <summary>
        /// 错误码，成功时为 NetErrorCode.SUCCESS（0）。
        /// </summary>
        public int ErrorCode { get; }

        /// <summary>
        /// 错误描述，成功时为空字符串。
        /// </summary>
        public string ErrorMessage { get; }

        /// <summary>
        /// 业务响应数据。成功时为业务响应；失败时通常为 default，但当服务端在业务错误码下仍返回业务体
        /// （如绑定冲突返回 existing_uid）且解析成功时，此处会携带解析后的数据。
        /// </summary>
        public T Data { get; }

        /// <summary>
        /// 初始化 NetResponse 的新实例。
        /// </summary>
        /// <param name="isSuccess">是否成功。</param>
        /// <param name="errorCode">错误码。</param>
        /// <param name="errorMessage">错误描述。</param>
        /// <param name="data">业务响应数据。</param>
        private NetResponse(bool isSuccess, int errorCode, string errorMessage, T data)
        {
            IsSuccess = isSuccess;
            ErrorCode = errorCode;
            ErrorMessage = errorMessage;
            Data = data;
        }

        /// <summary>
        /// 创建成功响应。
        /// </summary>
        /// <param name="data">业务响应数据。</param>
        /// <returns>成功的 NetResponse 实例。</returns>
        public static NetResponse<T> Success(T data)
        {
            return new NetResponse<T>(true, NetErrorCode.SUCCESS, string.Empty, data);
        }

        /// <summary>
        /// 创建失败响应。
        /// </summary>
        /// <param name="errorCode">错误码。</param>
        /// <param name="errorMessage">错误描述。</param>
        /// <returns>失败的 NetResponse 实例。</returns>
        public static NetResponse<T> Fail(int errorCode, string errorMessage)
        {
            return new NetResponse<T>(false, errorCode, errorMessage, default);
        }

        /// <summary>
        /// 创建携带业务数据的失败响应。
        /// 用于服务端在业务错误码下仍返回业务体的场景（如绑定冲突 10402 返回 existing_uid），
        /// 业务侧可在 IsSuccess=false 时读取 Data 获取附带信息。
        /// </summary>
        /// <param name="errorCode">错误码。</param>
        /// <param name="errorMessage">错误描述。</param>
        /// <param name="data">业务响应数据（可为 default）。</param>
        /// <returns>失败的 NetResponse 实例。</returns>
        public static NetResponse<T> Fail(int errorCode, string errorMessage, T data)
        {
            return new NetResponse<T>(false, errorCode, errorMessage, data);
        }
    }
}
