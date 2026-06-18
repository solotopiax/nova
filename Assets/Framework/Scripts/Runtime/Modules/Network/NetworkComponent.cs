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
    /// Network 组件，负责创建并初始化 Network / Http / DoH / WebSocket 四个并列管理器。
    /// 实现 ICoroutineRunner，为需要协程支持的管理器提供运行环境。
    /// </summary>
    [DisallowMultipleComponent]
    public sealed partial class NetworkComponent : FrameworkComponent, ICoroutineRunner
    {
        /// <summary>
        /// 唤醒，按依赖顺序创建四个管理器。
        /// </summary>
        protected override void Awake()
        {
            base.Awake();

            m_DoHManager = Util.TypeCreator.Create<IDoHManager>(m_CurDoHManagerTypeName);
            if (m_DoHManager == null)
            {
                throw new InvalidOperationException("DoHManager 无效。");
            }

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
        /// 开始，按依赖顺序初始化四个管理器。
        /// </summary>
        private void Start()
        {
            if (m_Settings == null)
            {
                throw new InvalidOperationException("NetworkSettings 无效，请检查 NetworkComponent 配置。");
            }

            m_DoHManager.Initialize(new DoHManagerConfig
            {
                UseDoH = m_DoHSettings.UseDoH,
                DnsTimeoutSeconds = m_DoHSettings.DnsTimeoutSeconds
            });

            m_HttpManager.Initialize(new HttpManagerConfig
            {
                ConnectTimeout = m_HttpSettings.ConnectTimeout,
                RequestTimeout = m_HttpSettings.RequestTimeout,
                DoHManager = m_DoHManager
            });

            m_NetworkManager.Initialize(new NetworkManagerConfig
            {
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

            if (success)
            {
                BeginAutoCollectAllIPAddresses();
            }

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
            if (success)
            {
                BeginAutoCollectAllIPAddresses();
            }

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
        /// NetCmd / HostKey 加载成功后立即启动一轮后台 DoH 预热，不阻塞 LoadAsync / LoadSync 的既有语义。
        /// </summary>
        private void BeginAutoCollectAllIPAddresses()
        {
            AutoCollectAllIPAddressesAsync().Forget();
        }

        /// <summary>
        /// 后台执行 DoH 预热，异常仅记录日志，不影响既有网络加载结果。
        /// </summary>
        private async UniTaskVoid AutoCollectAllIPAddressesAsync()
        {
            try
            {
                await m_DoHManager.CollectAllIPAddresses(m_NetworkManager.GetAllNetCmdUrls());
            }
            catch (Exception e)
            {
                Log.Warning(LogTag.Network, "NetworkComponent 自动 DoH 预热异常：{0}。", e);
            }
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
