/***************************************************************
 * (c) copyright 2026 - 2030, Solotopia
 * All Rights Reserved.
 * -------------------------------------------------------------
 * filename:  ThirdLoginProvider.cs
 * author:    taoye
 * created:   2026/8/3
 * descrip:   第三方登录提供方
 ***************************************************************/

namespace NovaFramework.Kit.Network.GameBind.Runtime
{
    /// <summary>
    /// 第三方登录提供方，对应 GameBind 协议 provider 字段的客户端取值契约。
    /// </summary>
    public enum ThirdLoginProvider
    {
        /// <summary>
        /// 未指定；禁止用于实际绑定请求。
        /// </summary>
        Unspecified = 0,

        /// <summary>
        /// Facebook 登录。
        /// </summary>
        Facebook = 1,

        /// <summary>
        /// Google 登录。
        /// </summary>
        Google = 2,

        /// <summary>
        /// Apple 登录。
        /// </summary>
        Apple = 3,

        /// <summary>
        /// 微信登录。
        /// </summary>
        Wechat = 4,
    }
}
