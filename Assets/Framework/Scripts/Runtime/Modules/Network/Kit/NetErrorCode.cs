/***************************************************************
 * (c) copyright 2026 - 2030, Solotopia
 * All Rights Reserved.
 * -------------------------------------------------------------
 * filename:  NetErrorCode.cs
 * author:    taoye
 * created:   2026/4/18
 * descrip:   网络层错误码常量（客户端段 + 服务端通用段）
 ***************************************************************/

namespace NovaFramework.Runtime
{
    /// <summary>
    /// 网络层错误码常量。
    /// 客户端段（负数）：本地流程错误，不经过服务端。
    /// 服务端通用段（1000 / 5000 / 6000 / 6001）：服务端统一返回的协议级错误。
    /// 业务专属错误码（如登录段 7000~7999）由各业务子包自行定义，不在本类扩展。
    /// </summary>
    public static class NetErrorCode
    {
        /// <summary>
        /// 成功。
        /// </summary>
        public const int SUCCESS = 0;

        /// <summary>
        /// 参数错误（服务端通用段）。
        /// </summary>
        public const int PARAM_ERROR = 1000;

        /// <summary>
        /// 服务器内部错误（服务端通用段）。
        /// </summary>
        public const int SERVER_ERROR = 5000;

        /// <summary>
        /// AES 加解密错误（服务端通用段）。
        /// </summary>
        public const int AES_ERROR = 6000;

        /// <summary>
        /// app_id 缺失（服务端通用段）。
        /// </summary>
        public const int APPID_MISSING = 6001;

        /// <summary>
        /// 网络不可用或 HTTP 请求失败（客户端段）。
        /// </summary>
        public const int NETWORK_ERROR = -1;

        /// <summary>
        /// AES 解密失败（客户端段）。
        /// </summary>
        public const int AES_DECRYPT_FAILED = -2;

        /// <summary>
        /// Proto 反序列化失败（客户端段）。
        /// </summary>
        public const int PROTO_PARSE_FAILED = -3;

        /// <summary>
        /// NetCmd URL 未找到（客户端段）。
        /// </summary>
        public const int URL_NOT_FOUND = -4;

        /// <summary>
        /// AES 加密失败（客户端段）。
        /// </summary>
        public const int AES_ENCRYPT_FAILED = -5;
    }
}
