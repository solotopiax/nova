/***************************************************************
 * (c) copyright 2026 - 2030, Solotopia
 * All Rights Reserved.
 * -------------------------------------------------------------
 * filename:  LoginErrorCode.cs
 * author:    taoye
 * created:   2026/5/26
 * descrip:   登录业务错误码（服务端登录/绑定业务段 10000~10499 + 客户端段 7000~7999 预留）
 ***************************************************************/

namespace NovaFramework.Kit.Network.GameLogin.Runtime
{
    /// <summary>
    /// 登录业务错误码常量。
    /// 服务端登录/绑定业务段（10000~10499）：服务端原样返回，经 NetService 透传到 <see cref="NovaFramework.Runtime.NetResponse{T}.ErrorCode"/>，业务侧用本类常量与 ErrorCode 比对。
    /// 客户端段（7000~7999）：与 <see cref="NovaFramework.Runtime.NetErrorCode"/> 客户端段（负数）/ 服务端通用段（1000/5000/6000/6001）错开，预留供后续纯客户端业务错误扩展，当前无定义。
    /// </summary>
    public static class LoginErrorCode
    {
        /// <summary>
        /// 成功。
        /// </summary>
        public const int OK = 0;

        /// <summary>
        /// 用户不存在。
        /// </summary>
        public const int ErrUserNotFound = 10000;

        /// <summary>
        /// UID 无效。
        /// </summary>
        public const int ErrInvalidUID = 10003;

        /// <summary>
        /// device_id 不能为空。
        /// </summary>
        public const int ErrDeviceIdRequired = 10006;

        /// <summary>
        /// 账号已锁定。
        /// </summary>
        public const int ErrAccountLocked = 10007;

        /// <summary>
        /// 账号已封禁。
        /// </summary>
        public const int ErrAccountBanned = 10008;

        /// <summary>
        /// 账号已删除。
        /// </summary>
        public const int ErrAccountDeleted = 10011;

        /// <summary>
        /// device_id 非最新，被顶号。
        /// </summary>
        public const int ErrKicked = 10400;

        /// <summary>
        /// 该三方号已被他人占用（换绑场景）。
        /// </summary>
        public const int ErrOpenidAlreadyBound = 10401;

        /// <summary>
        /// 绑定冲突，需二选一。登录路径触发：响应带 guest_summary / existing_summary，客户端调 BindResolveAsync 二选一。
        /// </summary>
        public const int ErrBindConflict = 10402;

        /// <summary>
        /// open_id 缺失或格式非法（三方鉴权失败）。
        /// </summary>
        public const int ErrThirdPartyAuthFailed = 10403;

        /// <summary>
        /// 三方号未注册（账号不存在）。
        /// </summary>
        public const int ErrAccountNotFound = 10404;
    }
}
