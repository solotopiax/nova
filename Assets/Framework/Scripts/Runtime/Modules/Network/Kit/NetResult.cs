/***************************************************************
 * (c) copyright 2026 - 2030, Solotopia
 * All Rights Reserved.
 * -------------------------------------------------------------
 * filename:  NetResult.cs
 * author:    taoye
 * created:   2026/5/26
 * descrip:   网络响应解析结果（包内私有），承载 Code / Message / BusinessData
 ***************************************************************/

namespace NovaFramework.Runtime
{
    /// <summary>
    /// 服务端基础响应解析结果（仅本包内使用），承载 Code / Message / BusinessData。
    /// </summary>
    internal sealed class NetResult
    {
        /// <summary>
        /// 服务端返回的业务错误码。
        /// </summary>
        public int Code { get; }

        /// <summary>
        /// 服务端返回的错误描述。
        /// </summary>
        public string Message { get; }

        /// <summary>
        /// 业务数据原始字节（用于后续 Proto 解析）。
        /// </summary>
        public byte[] BusinessData { get; }

        /// <summary>
        /// 初始化响应解析结果。
        /// </summary>
        /// <param name="code">业务错误码。</param>
        /// <param name="message">错误描述。</param>
        /// <param name="businessData">业务数据原始字节。</param>
        public NetResult(int code, string message, byte[] businessData)
        {
            Code = code;
            Message = message ?? string.Empty;
            BusinessData = businessData;
        }
    }
}
