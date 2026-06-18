/***************************************************************
 * (c) copyright 2026 - 2030, Solotopia
 * All Rights Reserved.
 * -------------------------------------------------------------
 * filename:  WebSocketScope.WebGL.cs
 * author:    taoye
 * created:   2026/3/9
 * descrip:   WebGL原生库接口暴露
 ***************************************************************/

using System;
using System.Runtime.InteropServices;
using AOT;

namespace NovaFramework.Runtime
{
    public partial class WebSocketScope
    {
#if !UNITY_EDITOR && UNITY_WEBGL
        /// <summary>
        /// WebGL 平台 WebSocket 原生 JS 库桥接类。
        /// 通过静态 C# 事件将 JS 回调分发到 C# 消费方，避免直接依赖任何 Framework 模块。
        /// </summary>
        public static class WebGL
        {
            /// <summary>WebSocket 连接打开回调。</summary>
            public delegate void OnOpenCallback(int instanceId);

            /// <summary>WebSocket 收到二进制消息回调。</summary>
            public delegate void OnMessageCallback(int instanceId, IntPtr msgPtr, int msgSize);

            /// <summary>WebSocket 收到字符串消息回调。</summary>
            public delegate void OnMessageStrCallback(int instanceId, IntPtr msgStrPtr);

            /// <summary>WebSocket 发生错误回调。</summary>
            public delegate void OnErrorCallback(int instanceId, IntPtr errorPtr);

            /// <summary>WebSocket 关闭回调。</summary>
            public delegate void OnCloseCallback(int instanceId, int closeCode, IntPtr reasonPtr);

            /// <summary>WebSocket 连接成功事件。参数：instanceId。</summary>
            public static event Action<int> OnOpenWebSocket;

            /// <summary>WebSocket 收到二进制消息事件。参数：instanceId, 字节数组。</summary>
            public static event Action<int, byte[]> OnMessageWebSocket;

            /// <summary>WebSocket 收到字符串消息事件。参数：instanceId, 字符串。</summary>
            public static event Action<int, string> OnMessageStrWebSocket;

            /// <summary>WebSocket 发生错误事件。参数：instanceId, 错误信息。</summary>
            public static event Action<int, string> OnErrorWebSocket;

            /// <summary>WebSocket 关闭事件。参数：instanceId, closeCode, reason。</summary>
            public static event Action<int, int, string> OnCloseWebSocket;

            /// <summary>建立 WebSocket 连接。</summary>
            [DllImport("__Internal")]
            public static extern int WebSocketConnect(int instanceId);

            /// <summary>关闭 WebSocket 连接。</summary>
            [DllImport("__Internal")]
            public static extern int WebSocketClose(int instanceId, int code, string reason);

            /// <summary>发送二进制数据帧。</summary>
            [DllImport("__Internal")]
            public static extern int WebSocketSend(int instanceId, byte[] dataPtr, int dataLength);

            /// <summary>发送字符串帧。</summary>
            [DllImport("__Internal")]
            public static extern int WebSocketSendStr(int instanceId, string data);

            /// <summary>查询 WebSocket 连接状态。</summary>
            [DllImport("__Internal")]
            public static extern int WebSocketGetState(int instanceId);

            /// <summary>分配新的 WebSocket 实例。</summary>
            [DllImport("__Internal")]
            public static extern int WebSocketAllocate(string url, string binaryType);

            /// <summary>添加子协议。</summary>
            [DllImport("__Internal")]
            public static extern int WebSocketAddSubProtocol(int instanceId, string protocol);

            /// <summary>释放 WebSocket 实例。</summary>
            [DllImport("__Internal")]
            public static extern void WebSocketFree(int instanceId);

            /// <summary>设置打开事件回调。</summary>
            [DllImport("__Internal")]
            public static extern void WebSocketSetOnOpen(OnOpenCallback callback);

            /// <summary>设置二进制消息回调。</summary>
            [DllImport("__Internal")]
            public static extern void WebSocketSetOnMessage(OnMessageCallback callback);

            /// <summary>设置字符串消息回调。</summary>
            [DllImport("__Internal")]
            public static extern void WebSocketSetOnMessageStr(OnMessageStrCallback callback);

            /// <summary>设置错误回调。</summary>
            [DllImport("__Internal")]
            public static extern void WebSocketSetOnError(OnErrorCallback callback);

            /// <summary>设置关闭回调。</summary>
            [DllImport("__Internal")]
            public static extern void WebSocketSetOnClose(OnCloseCallback callback);

            /// <summary>
            /// 向 JS 层注册所有 WebSocket 事件回调，必须在 WebGL 平台初始化时调用一次。
            /// </summary>
            public static void Initialize()
            {
                WebSocketSetOnOpen(DelegateOnOpenEvent);
                WebSocketSetOnMessage(DelegateOnMessageEvent);
                WebSocketSetOnMessageStr(DelegateOnMessageStrEvent);
                WebSocketSetOnError(DelegateOnErrorEvent);
                WebSocketSetOnClose(DelegateOnCloseEvent);
            }

            /// <summary>
            /// JS 层 WebSocket 打开事件桥接。
            /// </summary>
            /// <param name="instanceId">WebSocket 实例 ID。</param>
            [MonoPInvokeCallback(typeof(OnOpenCallback))]
            public static void DelegateOnOpenEvent(int instanceId)
            {
                OnOpenWebSocket?.Invoke(instanceId);
            }

            /// <summary>
            /// JS 层 WebSocket 二进制消息事件桥接。
            /// </summary>
            /// <param name="instanceId">WebSocket 实例 ID。</param>
            /// <param name="msgPtr">消息字节指针。</param>
            /// <param name="msgSize">消息字节长度。</param>
            [MonoPInvokeCallback(typeof(OnMessageCallback))]
            public static void DelegateOnMessageEvent(int instanceId, IntPtr msgPtr, int msgSize)
            {
                byte[] bytes = new byte[msgSize];
                Marshal.Copy(msgPtr, bytes, 0, msgSize);
                OnMessageWebSocket?.Invoke(instanceId, bytes);
            }

            /// <summary>
            /// JS 层 WebSocket 字符串消息事件桥接。
            /// </summary>
            /// <param name="instanceId">WebSocket 实例 ID。</param>
            /// <param name="msgStrPtr">消息字符串指针。</param>
            [MonoPInvokeCallback(typeof(OnMessageStrCallback))]
            public static void DelegateOnMessageStrEvent(int instanceId, IntPtr msgStrPtr)
            {
                string msgStr = Marshal.PtrToStringAuto(msgStrPtr);
                OnMessageStrWebSocket?.Invoke(instanceId, msgStr);
            }

            /// <summary>
            /// JS 层 WebSocket 错误事件桥接。
            /// </summary>
            /// <param name="instanceId">WebSocket 实例 ID。</param>
            /// <param name="errorPtr">错误信息指针。</param>
            [MonoPInvokeCallback(typeof(OnErrorCallback))]
            public static void DelegateOnErrorEvent(int instanceId, IntPtr errorPtr)
            {
                string errorMsg = Marshal.PtrToStringAuto(errorPtr);
                OnErrorWebSocket?.Invoke(instanceId, errorMsg);
            }

            /// <summary>
            /// JS 层 WebSocket 关闭事件桥接。
            /// </summary>
            /// <param name="instanceId">WebSocket 实例 ID。</param>
            /// <param name="closeCode">关闭码。</param>
            /// <param name="reasonPtr">关闭原因指针。</param>
            [MonoPInvokeCallback(typeof(OnCloseCallback))]
            public static void DelegateOnCloseEvent(int instanceId, int closeCode, IntPtr reasonPtr)
            {
                string reason = Marshal.PtrToStringAuto(reasonPtr);
                OnCloseWebSocket?.Invoke(instanceId, closeCode, reason);
            }
        }
#endif
    }
}
