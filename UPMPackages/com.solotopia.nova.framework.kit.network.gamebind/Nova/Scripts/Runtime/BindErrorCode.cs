/***************************************************************
 * (c) copyright 2026 - 2030, Solotopia
 * All Rights Reserved.
 * -------------------------------------------------------------
 * filename:  BindErrorCode.cs
 * author:    taoye
 * created:   2026/7/2
 * descrip:   账号绑定业务错误码（服务端绑定业务段 10400~10499 + 客户端段 7000~7999 预留）
 ***************************************************************/

namespace NovaFramework.Kit.Network.GameBind.Runtime
{
    /// <summary>
    /// 账号绑定业务错误码常量。
    /// 服务端绑定业务段（10400~10499）：服务端原样返回，经 NetService 透传到 <see cref="NovaFramework.Runtime.NetResponse{T}.ErrorCode"/>，业务侧用本类常量与 ErrorCode 比对。
    /// 客户端段（7000~7999）：与 <see cref="NovaFramework.Runtime.NetErrorCode"/> 客户端段（负数）/ 服务端通用段（1000/5000/6000/6001）错开，预留供后续纯客户端业务错误扩展，当前无定义。
    /// </summary>
    public static class BindErrorCode
    {
        /// <summary>
        /// 成功。
        /// </summary>
        public const int OK = 0;

        /// <summary>
        /// device_id 非最新，被顶号。
        /// </summary>
        public const int ErrKicked = 10400;

        /// <summary>
        /// 该三方号已被他人占用（不支持改绑，无法迁到本账号）。
        /// </summary>
        public const int ErrOpenidAlreadyBound = 10401;

        /// <summary>
        /// 绑定冲突，需二选一。BindAsync 在 open_id 已绑别的 uid 时返回，响应带 existing_uid，客户端调 QueryConflictAsync 拉详情后由玩家二选一，再调 ResolveAsync 裁决。
        /// ResolveAsync 在并发复核到归属变化时也可能返回此码，提示客户端重试。
        /// </summary>
        public const int ErrBindConflict = 10402;

        /// <summary>
        /// open_id 缺失或格式非法（三方鉴权失败）。
        /// </summary>
        public const int ErrThirdPartyAuthFailed = 10403;

        /// <summary>
        /// 操作繁忙，请稍后重试。ResolveAsync 行锁竞争 / 事务超时时返回，客户端稍后原样重试即可。
        /// </summary>
        public const int ErrBindBusy = 10406;
    }
}
