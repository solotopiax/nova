/***************************************************************
 * (c) copyright 2026 - 2030, Solotopia
 * All Rights Reserved.
 * -------------------------------------------------------------
 * filename:  WebSocketState.cs
 * author:    taoye
 * created:   2026/3/9
 * descrip:   WebSocket连接状态
 ***************************************************************/

namespace NovaFramework.Runtime
{
    public partial class WebSocketScope
    {
        /// <summary>
        /// WebSocket 连接就绪状态，对应 HTML5 WebSocket ReadyState。
        /// </summary>
        public enum WebSocketState : ushort
        {
            /// <summary>
            /// 连接尚未建立。
            /// </summary>
            Connecting = 0,

            /// <summary>
            /// 连接已建立，可以通信。
            /// </summary>
            Open = 1,

            /// <summary>
            /// 连接正在进行关闭握手，或已调用 close 方法。
            /// </summary>
            Closing = 2,

            /// <summary>
            /// 连接已关闭或无法建立。
            /// </summary>
            Closed = 3
        }
    }
}
