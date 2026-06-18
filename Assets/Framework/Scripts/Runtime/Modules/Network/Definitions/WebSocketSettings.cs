/***************************************************************
 * (c) copyright 2026 - 2030, Solotopia
 * All Rights Reserved.
 * -------------------------------------------------------------
 * filename:  WebSocketSettings.cs
 * author:    taoye
 * created:   2026/3/11
 * descrip:   WebSocket 管理器配置
 ***************************************************************/

using System;

namespace NovaFramework.Runtime
{
    /// <summary>
    /// WebSocket 管理器配置，包含连接、认证、心跳与重连参数。
    /// </summary>
    [Serializable]
    public class WebSocketSettings
    {
        /// <summary>
        /// WebSocket 连接超时时间（秒）。
        /// </summary>
        public float ConnectTimeout = 10f;

        /// <summary>
        /// WebSocket 身份认证超时时间（秒）。
        /// </summary>
        public float AuthenticateTimeout = 10f;

        /// <summary>
        /// WebSocket 心跳发送间隔（秒）。
        /// </summary>
        public float HeartBeatTimeInterval = 20f;

        /// <summary>
        /// WebSocket 心跳响应超时时间（秒）。
        /// </summary>
        public float HeartBeatTimeout = 10f;

        /// <summary>
        /// 是否启用 WebSocket 自动重连。
        /// </summary>
        public bool EnableAutoReconnect = true;

        /// <summary>
        /// WebSocket 自动重连最大次数。
        /// </summary>
        public int AutoReconnectMaxCounter = 5;

        /// <summary>
        /// WebSocket 自动重连间隔时间（秒）。
        /// </summary>
        public float AutoReconnectTimeInterval = 3f;

        /// <summary>
        /// 自动重连失败提示界面的资源地址。
        /// </summary>
        public string AutoReconnectFailedUIAssetLocation;
    }
}
