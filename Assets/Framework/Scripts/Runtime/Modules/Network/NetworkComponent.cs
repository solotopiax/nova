/***************************************************************
 * (c) copyright 2026 - 2030, Solotopia
 * All Rights Reserved.
 * -------------------------------------------------------------
 * filename:  NetworkComponent.cs
 * author:    taoye
 * created:   2026/3/9
 * descrip:   Network组件
 ***************************************************************/

using System;
using System.Collections;
using Cysharp.Threading.Tasks;
using UnityEngine;

namespace NovaFramework.Runtime
{
    /// <summary>
    /// Network 组件，负责创建并初始化 Network / Http / WebSocket 三个并列管理器。
    /// 实现 ICoroutineRunner，为需要协程支持的管理器提供运行环境。
    /// </summary>
    [DisallowMultipleComponent]
    public sealed partial class NetworkComponent : FrameworkComponent, ICoroutineRunner
    {
        /// <summary>
        /// 唤醒，按依赖顺序创建三个管理器。
        /// </summary>
        protected override void Awake()
        {
            base.Awake();

            m_HttpManager = Util.TypeCreator.Create<IHttpManager>(m_CurHttpManagerTypeName);
            if (m_HttpManager == null)
            {
                throw new InvalidOperationException("HttpManager 无效。");
            }

            m_NetworkManager = Util.TypeCreator.Create<INetworkManager>(m_CurNetworkManagerTypeName);
            if (m_NetworkManager == null)
            {
                throw new InvalidOperationException("NetworkManager 无效。");
            }

            m_WebSocketManager = Util.TypeCreator.Create<IWebSocketManager>(m_CurWebSocketManagerTypeName);
            if (m_WebSocketManager == null)
            {
                throw new InvalidOperationException("WebSocketManager 无效。");
            }
        }

        /// <summary>
        /// 开始，按依赖顺序初始化三个管理器。
        /// </summary>
        private void Start()
        {
            if (m_Settings == null)
            {
                throw new InvalidOperationException("NetworkSettings 无效，请检查 NetworkComponent 配置。");
            }

            m_HttpManager.Initialize(new HttpManagerConfig
            {
                EnableUWRTracks = m_HttpSettings.EnableUWRTracks,
                PreferLastSuccessfulHost = m_HttpSettings.PreferLastSuccessfulHost,
                BusinessFallbackRoundCount = m_HttpSettings.BusinessFallbackRoundCount,
                RetryRequestCount = m_HttpSettings.RetryRequestCount,
                RequestTimeout = m_HttpSettings.RequestTimeout
            });

            m_NetworkManager.Initialize(new NetworkManagerConfig
            {
                DataFormat = m_Settings.DataFormat,
                HostKeyUnitSettings = m_Settings.HostKeySettings.HostKeyUnits,
                NetCmdUnitSettings = m_Settings.NetCmdSettings.NetCmdUnits
            });

            m_WebSocketManager.Initialize(new WebSocketManagerConfig
            {
                ConnectTimeout = m_WebSocketSettings.ConnectTimeout,
                AuthenticateTimeout = m_WebSocketSettings.AuthenticateTimeout,
                HeartBeatTimeInterval = m_WebSocketSettings.HeartBeatTimeInterval,
                HeartBeatTimeout = m_WebSocketSettings.HeartBeatTimeout,
                AutoReconnectMaxCounter = m_WebSocketSettings.AutoReconnectMaxCounter,
                AutoReconnectTimeInterval = m_WebSocketSettings.AutoReconnectTimeInterval,
                EnableAutoReconnect = m_WebSocketSettings.EnableAutoReconnect,
                AutoReconnectFailedUIAssetLocation = m_WebSocketSettings.AutoReconnectFailedUIAssetLocation,
                CoroutineRunner = this
            });
        }


        /// <summary>
        /// 异步加载 NetCmd 数据。
        /// </summary>
        /// <returns>是否加载成功。</returns>
        public async UniTask<bool> LoadAsync()
        {
            if (IsLoadOver)
            {
                return true;
            }

            if (m_LoadTcs != null)
            {
                return await m_LoadTcs.Task;
            }

            m_LoadTcs = new UniTaskCompletionSource<bool>();
            var tcs = m_LoadTcs;

            bool success;
            try
            {
                success = await m_NetworkManager.LoadNetCmdsAsync();
            }
            catch (Exception e)
            {
                Log.Error(LogTag.Network, "NetworkComponent.LoadAsync 发生异常：{0}", e);
                success = false;
            }

            IsLoadOver = success;
            tcs.TrySetResult(success);

            // 仅失败时清除 m_LoadTcs 允许重试，成功时保留防止后续并发重复加载。
            if (!success)
            {
                m_LoadTcs = null;
            }

            return success;
        }

        /// <summary>
        /// 同步加载 NetCmd 数据。
        /// </summary>
        /// <returns>是否加载成功。</returns>
        public bool LoadSync()
        {
            if (IsLoadOver)
            {
                return true;
            }

            bool success;
            try
            {
                success = m_NetworkManager.LoadNetCmdsSync();
            }
            catch (Exception e)
            {
                Log.Error(LogTag.Network, "NetworkComponent.LoadSync 发生异常：{0}", e);
                success = false;
            }

            IsLoadOver = success;
            return success;
        }

        /// <summary>
        /// 销毁时清理组件级状态。管理器的 Shutdown 由 FrameworkManagersGroup 统一调度。
        /// </summary>
        private void OnDestroy()
        {
            m_LoadTcs = null;
            IsLoadOver = false;
            m_KitInstances.Clear();
        }

        /// <summary>
        /// 获取当前网络设置。
        /// </summary>
        /// <returns>当前 NetworkSettings。</returns>
        public NetworkSettings GetCurrentSettings()
        {
            return m_Settings;
        }

    }
}
