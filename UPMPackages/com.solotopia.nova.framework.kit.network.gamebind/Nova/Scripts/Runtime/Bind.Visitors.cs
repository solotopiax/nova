/***************************************************************
 * (c) copyright 2026 - 2030, Solotopia
 * All Rights Reserved.
 * -------------------------------------------------------------
 * filename:  Bind.Visitors.cs
 * author:    taoye
 * created:   2026/7/2
 * descrip:   账号绑定业务网络 Service — 字段与属性
 ***************************************************************/

namespace NovaFramework.Kit.Network.GameBind.Runtime
{
    /// <summary>
    /// 账号绑定业务网络 Service。
    /// 封装绑定、冲突查询、裁决三段协议的发送逻辑，通过 NetService.SendAsync 完成 Protobuf 序列化、AES 加密、HTTP 请求及解析全流程。
    /// 只负责账号归属裁决，不处理存档数据覆盖，也不改动本地登录态。
    /// 通过 Nova.Network.Kit<Bind>() 获取实例，不继承任何基类，无参构造即可使用。
    /// </summary>
    public sealed partial class Bind
    {
        /// <summary>
        /// 当前 Service 实例的调试模式覆盖值。
        /// 为 null 时沿用 NetService.IsDebugMode 全局开关。
        /// </summary>
        private bool? m_DebugModeOverride;
    }
}
