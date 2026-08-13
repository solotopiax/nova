/***************************************************************
 * (c) copyright 2026 - 2030, Solotopia
 * All Rights Reserved.
 * -------------------------------------------------------------
 * filename:  Bind.Visitors.cs
 * author:    taoye
 * created:   2026/7/2
 * descrip:   账号绑定业务网络 Service — 字段与属性
 ***************************************************************/

using NovaFramework.Runtime;

namespace NovaFramework.Kit.Network.GameBind.Runtime
{
    /// <summary>
    /// 账号绑定业务网络 Service。
    /// 封装绑定、冲突查询、裁决三段协议的发送逻辑，通过 NetService.SendAsync 完成 Protobuf 序列化、AES 加密、HTTP 请求及解析全流程。
    /// 只负责账号归属裁决，不处理存档数据覆盖；成功后根据权威业务结果同步 NetService 身份。
    /// 通过 Nova.Network.Kit<Bind>() 获取实例，不继承任何基类，无参构造即可使用。
    /// </summary>
    public sealed partial class Bind
    {
        /// <summary>
        /// 当前进程内已确认归属于当前 UID 的第三方 OpenID。
        /// 直接读取 NetService 统一身份缓存，不做独立持久化。
        /// </summary>
        public string OpenID => NetService.OpenID;

    }
}
