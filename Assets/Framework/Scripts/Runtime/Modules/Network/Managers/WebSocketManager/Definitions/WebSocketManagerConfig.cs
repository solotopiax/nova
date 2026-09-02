/***************************************************************
 * (c) copyright 2026 - 2030, Solotopia
 * All Rights Reserved.
 * -------------------------------------------------------------
 * filename:  WebSocketManagerConfig.cs
 * author:    taoye
 * created:   2026/3/9
 * descrip:   WebSocket管理器配置
 ***************************************************************/

using System;

namespace NovaFramework.Runtime
{
    /// <summary>
    /// WebSocket 管理器配置。
    /// </summary>
    public class WebSocketManagerConfig
    {
        /// <summary>
        /// 连接超时时间（秒），默认 10 秒。
        /// </summary>
        public float ConnectTimeout = 10f;

        /// <summary>
        /// 身份认证超时时间（秒），默认 10 秒。
        /// </summary>
        public float AuthenticateTimeout = 10f;

        /// <summary>
        /// 心跳发送间隔（秒），默认 20 秒。
        /// </summary>
        public float HeartBeatTimeInterval = 20f;

        /// <summary>
        /// 心跳响应超时时间（秒），默认 10 秒。
        /// </summary>
        public float HeartBeatTimeout = 10f;

        /// <summary>
        /// 自动重连最大次数，超过后触发 OnReconnectFailed 事件，默认 5 次。
        /// </summary>
        public int AutoReconnectMaxCounter = 5;

        /// <summary>
        /// 自动重连间隔时间（秒），默认 3 秒。
        /// </summary>
        public float AutoReconnectTimeInterval = 3f;

        /// <summary>
        /// 是否启用自动重连机制，默认 true。
        /// </summary>
        public bool EnableAutoReconnect = true;

        /// <summary>
        /// 自动重连失败提示界面的资源地址。
        /// </summary>
        public string AutoReconnectFailedUIAssetLocation;

        /// <summary>
        /// 协程运行器接口，由 NetworkComponent 注入，用于 WebGL 平台协程与通用定时任务。
        /// </summary>
        public ICoroutineRunner CoroutineRunner;

        /// <summary>
        /// 特殊消息创建委托（心跳 / 认证消息），游戏层注入协议相关逻辑。
        /// 参数：(NetChannelType channelType, string messageCategory)，
        /// messageCategory 值为 "heartbeat" 或 "authenticate"。
        /// 若为 null，则不发送特殊消息。
        /// </summary>
        public Func<WebSocketScope.NetChannelType, string, WebSocketScope.NetMessageBase> SpecialMessageCreator;
    }
}
