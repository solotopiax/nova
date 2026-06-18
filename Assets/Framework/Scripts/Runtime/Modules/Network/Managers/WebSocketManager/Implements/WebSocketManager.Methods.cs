/***************************************************************
 * (c) copyright 2026 - 2030, Solotopia
 * All Rights Reserved.
 * -------------------------------------------------------------
 * filename:  WebSocketManager.Methods.cs
 * author:    taoye
 * created:   2026/3/9
 * descrip:   WebSocket管理器 —— 私有方法
 ***************************************************************/

using System;
using System.Collections;
using Cysharp.Threading.Tasks;
using UnityEngine;

namespace NovaFramework.Runtime
{
    /// <summary>
    /// WebSocket 管理器。
    /// </summary>
    internal sealed partial class WebSocketManager : WebSocketManagerBase, WebSocketScope.IWebSocketManagerBridge
    {
        /// <summary>
        /// 协程：建立 WebSocket 连接，成功后依次启动认证、心跳（和重连）协程。
        /// </summary>
        /// <param name="channel">通道实例。</param>
        private IEnumerator ConnectServerCoroutine(WebSocketScope.NetChannelBase channel)
        {
            yield return null;

#if !UNITY_EDITOR && UNITY_WEBGL
            yield return InnerConnectServerCoroutine(channel);
#else
            yield return InnerConnectServerCoroutineAsync(channel).ToCoroutine();
#endif

            if (!channel.IsConnected)
            {
                yield break;
            }

            m_AutoAuthenticateCoroutines[channel] = m_CoroutineRunner.StartCoroutine(AutoAuthenticateCoroutine(channel));
            m_AutoHeartBeatCoroutines[channel]    = m_CoroutineRunner.StartCoroutine(AutoHeartBeatCoroutine(channel));
            if (m_EnableAutoReconnect && channel.AutoReconnect)
            {
                m_AutoReconnectCoroutines[channel] = m_CoroutineRunner.StartCoroutine(AutoReconnectServerCoroutine(channel));
            }
        }

        /// <summary>
        /// 协程：身份认证自动驱动（等待触发 → 发送认证 → 等待响应 → 注入离线缓冲）。
        /// </summary>
        /// <param name="channel">通道实例。</param>
        private IEnumerator AutoAuthenticateCoroutine(WebSocketScope.NetChannelBase channel)
        {
            while (true)
            {
                if (channel.IsDisconnectedSubjectively)
                {
                    yield break;
                }

                if (!channel.IsConnected)
                {
                    yield return null;
                    continue;
                }

                if (channel.NeedAuthenticateRightNow && !channel.WaitingAuthenticate)
                {
                    yield return new WaitForSeconds(0.1f);
                    WebSocketScope.NetMessageBase authMsg = m_SpecialMessageCreator?.Invoke(channel.GetNetChannelType(), "authenticate");
                    if (authMsg != null)
                    {
                        byte[] bytes = channel.PackageMessageToBytes(authMsg);
                        channel.InjectBytesToBuffer(bytes, authMsg);
                        channel.WaitingAuthenticate = true;
                        channel.AuthenticateSendTime = DateTime.Now;
                    }
                    else
                    {
                        // 没有认证委托，视为不需要认证，直接标记认证完成。
                        channel.NeedAuthenticateRightNow = false;
                        channel.WaitingAuthenticate      = false;
                    }
                }
                else if (channel.WaitingAuthenticate && channel.CheckAuthenticateTimeout())
                {
#if !UNITY_EDITOR && UNITY_WEBGL
                    m_CoroutineRunner.StartCoroutine(channel.CloseSocket(false));
#else
                    channel.CloseSocket(false).Forget();
#endif
                    Log.Warning(LogTag.WebSocket, "{0} 身份认证超时，断开重连。", channel);
                }
                else if (!channel.NeedAuthenticateRightNow && !channel.WaitingAuthenticate)
                {
                    channel.InjectOfflineBufferIntoBuffer();
                    int channelIndex = FindNetChannelIndex(channel);
                    OnAuthenticateSuccess?.Invoke(channelIndex, channel.ServerAddress);
                    yield break;
                }

                yield return null;
            }
        }

        /// <summary>
        /// 协程：心跳自动驱动（等待认证完成 → 间隔发送 → 超时断开）。
        /// </summary>
        /// <param name="channel">通道实例。</param>
        private IEnumerator AutoHeartBeatCoroutine(WebSocketScope.NetChannelBase channel)
        {
            while (true)
            {
                if (channel.IsDisconnectedSubjectively)
                {
                    yield break;
                }

                if (!channel.IsAuthenticatedSuccess)
                {
                    yield return null;
                    continue;
                }

                yield return new WaitForSeconds(m_HeartBeatTimeInterval);

                if (!channel.IsConnected || channel.IsDisconnectedSubjectively)
                {
                    yield break;
                }

                WebSocketScope.NetMessageBase heartbeatMsg = m_SpecialMessageCreator?.Invoke(channel.GetNetChannelType(), "heartbeat");
                if (heartbeatMsg != null)
                {
                    byte[] bytes = channel.PackageMessageToBytes(heartbeatMsg);
                    channel.InjectBytesToBuffer(bytes, heartbeatMsg);
                    channel.WaitingHeartBeat  = true;
                    channel.HeartBeatSendTime = DateTime.Now;

                    while (channel.WaitingHeartBeat)
                    {
                        if (channel.CheckHeatBeatTimeout())
                        {
#if !UNITY_EDITOR && UNITY_WEBGL
                            m_CoroutineRunner.StartCoroutine(channel.CloseSocket(false));
#else
                            channel.CloseSocket(false).Forget();
#endif
                            Log.Warning(LogTag.WebSocket, "{0} 心跳超时，断开重连。", channel);
                            yield break;
                        }

                        yield return null;
                    }
                }
            }
        }

        /// <summary>
        /// 协程：断线自动重连（监测断线 → 等待间隔 → 重试连接 → 达到上限触发失败事件）。
        /// </summary>
        /// <param name="channel">通道实例。</param>
        private IEnumerator AutoReconnectServerCoroutine(WebSocketScope.NetChannelBase channel)
        {
            int counter = 0;
            while (true)
            {
                if (channel.IsDisconnectedSubjectively)
                {
                    yield break;
                }

                if (channel.IsConnected)
                {
                    counter = 0;
                    yield return null;
                    continue;
                }

                counter++;
                if (counter > m_AutoReconnectMaxCounter)
                {
                    int channelIndex = FindNetChannelIndex(channel);
                    Log.Warning(LogTag.WebSocket, "{0} 重连失败次数已达上限 {1}。", channel, m_AutoReconnectMaxCounter);
                    OnReconnectFailed?.Invoke(channelIndex, channel.ServerAddress);
                    yield break;
                }

                yield return new WaitForSeconds(m_AutoReconnectTimeInterval);

#if !UNITY_EDITOR && UNITY_WEBGL
                yield return InnerConnectServerCoroutine(channel);
#else
                yield return InnerConnectServerCoroutineAsync(channel).ToCoroutine();
#endif
            }
        }

        /// <summary>
        /// 执行一次实际连接操作（WebGL 协程版）。
        /// </summary>
        /// <param name="channel">通道实例。</param>
        private IEnumerator InnerConnectServerCoroutine(WebSocketScope.NetChannelBase channel)
        {
            int channelIndex = FindNetChannelIndex(channel);
            OnBeginConnect?.Invoke(channelIndex, channel.ServerAddress);

#if !UNITY_EDITOR && UNITY_WEBGL
            yield return channel.CloseSocket(false);
            yield return channel.CreateSocket();
#endif

            if (channel.IsConnected)
            {
                OnConnectSuccess?.Invoke(channelIndex, channel.ServerAddress);
            }
            else
            {
                OnConnectFail?.Invoke(channelIndex, channel.ServerAddress);
            }

            yield break;
        }

        /// <summary>
        /// 执行一次实际连接操作（非 WebGL 异步版，转为协程供调用）。
        /// </summary>
        /// <param name="channel">通道实例。</param>
        private async UniTask InnerConnectServerCoroutineAsync(WebSocketScope.NetChannelBase channel)
        {
            int channelIndex = FindNetChannelIndex(channel);
            OnBeginConnect?.Invoke(channelIndex, channel.ServerAddress);

            await channel.CloseSocket(false);
            await channel.CreateSocket();

            if (channel.IsConnected)
            {
                OnConnectSuccess?.Invoke(channelIndex, channel.ServerAddress);
            }
            else
            {
                OnConnectFail?.Invoke(channelIndex, channel.ServerAddress);
            }
        }

        /// <summary>
        /// 查找已存在的空闲通道（未连接且地址匹配）。
        /// </summary>
        /// <param name="channelType">通道类型。</param>
        /// <param name="serverAddress">服务器地址。</param>
        /// <returns>找到返回对应通道，否则返回 null。</returns>
        private WebSocketScope.NetChannelBase FindFreeNetChannel(WebSocketScope.NetChannelType channelType, string serverAddress)
        {
            foreach (WebSocketScope.NetChannelBase channel in m_NetChannels)
            {
                if (channel.Equals(channelType, serverAddress) && !channel.IsConnected)
                {
                    return channel;
                }
            }

            return null;
        }

        /// <summary>
        /// 按类型和地址查找通道实例。
        /// </summary>
        /// <param name="channelType">通道类型。</param>
        /// <param name="serverAddress">服务器地址。</param>
        /// <returns>找到返回通道实例，否则返回 null。</returns>
        private WebSocketScope.NetChannelBase FindNetChannel(WebSocketScope.NetChannelType channelType, string serverAddress)
        {
            foreach (WebSocketScope.NetChannelBase channel in m_NetChannels)
            {
                if (channel.Equals(channelType, serverAddress))
                {
                    return channel;
                }
            }

            return null;
        }

        /// <summary>
        /// 查找指定通道实例的索引（仅用于需要向外部传递 int 索引的事件回调）。
        /// </summary>
        /// <param name="channel">通道实例。</param>
        /// <returns>找到返回非负索引，否则返回 -1。</returns>
        private int FindNetChannelIndex(WebSocketScope.NetChannelBase channel)
        {
            return m_NetChannels.IndexOf(channel);
        }

        /// <summary>
        /// 停止并清空指定通道的所有协程。
        /// </summary>
        /// <param name="channel">通道实例。</param>
        private void StopChannelCoroutines(WebSocketScope.NetChannelBase channel)
        {
            if (m_ConnectServerCoroutines.TryGetValue(channel, out Coroutine c1) && c1 != null)
            {
                m_CoroutineRunner.StopCoroutine(c1);
                m_ConnectServerCoroutines.Remove(channel);
            }

            if (m_AutoAuthenticateCoroutines.TryGetValue(channel, out Coroutine c2) && c2 != null)
            {
                m_CoroutineRunner.StopCoroutine(c2);
                m_AutoAuthenticateCoroutines.Remove(channel);
            }

            if (m_AutoHeartBeatCoroutines.TryGetValue(channel, out Coroutine c3) && c3 != null)
            {
                m_CoroutineRunner.StopCoroutine(c3);
                m_AutoHeartBeatCoroutines.Remove(channel);
            }

            if (m_AutoReconnectCoroutines.TryGetValue(channel, out Coroutine c4) && c4 != null)
            {
                m_CoroutineRunner.StopCoroutine(c4);
                m_AutoReconnectCoroutines.Remove(channel);
            }
        }
    }
}
