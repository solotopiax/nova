/***************************************************************
 * (c) copyright 2026 - 2030, Solotopia
 * All Rights Reserved.
 * -------------------------------------------------------------
 * filename:  Login.Visitors.cs
 * author:    taoye
 * created:   2026/4/18
 * descrip:   登录业务网络 Service — 字段与属性
 ***************************************************************/

using NovaFramework.Runtime;

namespace NovaFramework.Kit.Network.GameLogin.Runtime
{
    /// <summary>
    /// 登录业务网络 Service。
    /// 封装登录与账号删除协议的发送逻辑，通过 NetService.SendAsync 完成 Protobuf 序列化、AES 加密、HTTP 请求及解析全流程。
    /// 登录成功后根据业务响应同步 UID、OpenID，后续请求 Header 自动携带当前身份。
    /// 删除账号成功后清空本地登录态（等同登出），防止继续以失效 UID 发请求。
    /// 通过 Nova.Network.Kit<Login>() 获取实例，不继承任何基类，无参构造即可使用。
    /// </summary>
    public sealed partial class Login
    {
        /// <summary>
        /// 当前已登录用户的 UID；直接读取 NetService 统一身份缓存。
        /// 初值为空字符串。
        /// </summary>
        public string UID => NetService.UID;

        /// <summary>
        /// 当前是否已登录（UID 非空）。
        /// </summary>
        public bool IsLoggedIn => !string.IsNullOrEmpty(UID);

        /// <summary>
        /// 当前 Service 实例的调试模式覆盖值。
        /// 为 null 时沿用 NetService.IsDebugMode 全局开关。
        /// </summary>
        private bool? m_DebugModeOverride;
    }
}
