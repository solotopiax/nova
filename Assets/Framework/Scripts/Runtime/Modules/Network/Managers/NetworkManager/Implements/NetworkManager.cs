/***************************************************************
 * (c) copyright 2026 - 2030, Solotopia
 * All Rights Reserved.
 * -------------------------------------------------------------
 * filename:  NetworkManager.cs
 * author:    taoye
 * created:   2026/3/9
 * descrip:   Network管理器
 ***************************************************************/

using System;
using System.Collections.Generic;
using System.Net.NetworkInformation;
using System.Net.Sockets;
using Cysharp.Threading.Tasks;
using UnityEngine;

namespace NovaFramework.Runtime
{
    /// <summary>
    /// Network 管理器，负责 Luban 域名表/指令表加载、NetCmd URL 路由、网络状态检测与服务器时间获取。
    /// </summary>
    internal sealed partial class NetworkManager : NetworkManagerBase
    {
        /// <summary>
        /// 初始化 NetworkManager 的新实例。
        /// </summary>
        public NetworkManager()
        {
            m_NetworkDatas = new Dictionary<string, ITable>();
            m_HostKeyCache = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            m_CmdCache = new Dictionary<string, CmdCacheEntry>(StringComparer.OrdinalIgnoreCase);
            m_CmdRowIndex = new Dictionary<string, INetworkCmdRow>(StringComparer.OrdinalIgnoreCase);
        }

        /// <summary>
        /// 初始化。
        /// </summary>
        /// <param name="config">配置信息。</param>
        public override void Initialize(NetworkManagerConfig config)
        {
            m_HostKeyUnitSettings = config.HostKeyUnitSettings ?? new List<HostKeyUnitSetting>();
            m_NetCmdUnitSettings = config.NetCmdUnitSettings ?? new List<NetCmdUnitSetting>();
            m_AssetManager = FrameworkManagersGroup.GetManager<IAssetManager>();
            m_HttpManager = FrameworkManagersGroup.GetManager<IHttpManager>();
        }

        /// <summary>
        /// 异步加载域名表与指令表的 Luban 数据并构建运行时缓存。
        /// Phase 1：并行加载所有 HostKey + NetCmd 单元的 AB 资源，LubanDataReceiver 写入局部 dataCache。
        /// Phase 2：从 dataCache 构建运行时缓存与 Luban Tables 对象。
        /// </summary>
        /// <returns>加载并解析成功返回 true。</returns>
        public override async UniTask<bool> LoadNetCmdsAsync()
        {
            List<UniTask<bool>> tasks = new List<UniTask<bool>>();
            IAssetManager am = m_AssetManager;
            DataReceiver.LoadAssetAsyncFunc loadFunc = async (assetLocation) =>
            {
                IAssetHandle<TextAsset> handle = await am.LoadAsync<TextAsset>(assetLocation);
                TextAsset asset = handle.Asset;
                handle.Release();
                return asset;
            };
            DataReceiver.ReleaseAssetAction releaseFunc = _ => { };

            LubanDataCache dataCache = new LubanDataCache();

            for (int i = 0; i < m_HostKeyUnitSettings.Count; i++)
            {
                HostKeyUnitSetting unit = m_HostKeyUnitSettings[i];
                if (string.IsNullOrEmpty(unit.AssetLocation))
                {
                    continue;
                }
                tasks.Add(new LubanDataReceiver(dataCache, unit, loadFunc, releaseFunc).ReadDataAssetAsync(unit.AssetLocation));
            }

            for (int i = 0; i < m_NetCmdUnitSettings.Count; i++)
            {
                NetCmdUnitSetting unit = m_NetCmdUnitSettings[i];
                if (string.IsNullOrEmpty(unit.AssetLocation))
                {
                    continue;
                }
                tasks.Add(new LubanDataReceiver(dataCache, unit, loadFunc, releaseFunc).ReadDataAssetAsync(unit.AssetLocation));
            }

            if (tasks.Count > 0)
            {
                bool[] results = await UniTask.WhenAll(tasks);
                for (int i = 0; i < results.Length; i++)
                {
                    if (!results[i])
                    {
                        return false;
                    }
                }
            }

            return BuildTablesFromCache(dataCache);
        }

        /// <summary>
        /// 同步加载域名表与指令表的 Luban 数据并构建运行时缓存。
        /// Phase 1：串行加载所有 HostKey + NetCmd 单元的 AB 资源，LubanDataReceiver 写入局部 dataCache。
        /// Phase 2：从 dataCache 构建运行时缓存与 Luban Tables 对象。
        /// </summary>
        /// <returns>加载并解析成功返回 true。</returns>
        public override bool LoadNetCmdsSync()
        {
            DataReceiver.LoadAssetSyncFunc syncLoadFunc = (assetLocation) =>
            {
                IAssetHandle<TextAsset> handle = m_AssetManager.LoadSync<TextAsset>(assetLocation);
                TextAsset asset = handle.Asset;
                handle.Release();
                return asset;
            };
            DataReceiver.ReleaseAssetAction releaseFunc = _ => { };

            LubanDataCache dataCache = new LubanDataCache();

            for (int i = 0; i < m_HostKeyUnitSettings.Count; i++)
            {
                HostKeyUnitSetting unit = m_HostKeyUnitSettings[i];
                if (string.IsNullOrEmpty(unit.AssetLocation))
                {
                    continue;
                }

                bool success = new LubanDataReceiver(dataCache, unit, syncLoadFunc, releaseFunc).ReadDataAssetSync(unit.AssetLocation);
                if (!success)
                {
                    return false;
                }
            }

            for (int i = 0; i < m_NetCmdUnitSettings.Count; i++)
            {
                NetCmdUnitSetting unit = m_NetCmdUnitSettings[i];
                if (string.IsNullOrEmpty(unit.AssetLocation))
                {
                    continue;
                }

                bool success = new LubanDataReceiver(dataCache, unit, syncLoadFunc, releaseFunc).ReadDataAssetSync(unit.AssetLocation);
                if (!success)
                {
                    return false;
                }
            }

            return BuildTablesFromCache(dataCache);
        }

        /// <summary>
        /// 根据复合键（tbName + dtName）获取完整 URL（HostKey URL + Path）。
        /// </summary>
        /// <param name="tbName">Luban 表类型名称，与 dtName 共同构成复合缓存键。</param>
        /// <param name="dtName">NetCmd 数据类型名称。</param>
        /// <returns>URL 字符串，不存在时返回 null。</returns>
        public override string GetNetCmdUrl(string tbName, string dtName)
        {
            string compositeKey = tbName + "." + dtName;
            if (!m_CmdCache.TryGetValue(compositeKey, out CmdCacheEntry cmd))
            {
                Log.Warning(LogTag.Network, "GetNetCmdUrl 未找到 NetCmd：表[{0}] 行[{1}]。", tbName, dtName);
                return null;
            }

            if (!m_HostKeyCache.TryGetValue(cmd.HostKey, out string hostUrl))
            {
                Log.Warning(LogTag.Network, "GetNetCmdUrl NetCmd [{0}.{1}] 的 HostKey [{2}] 未找到对应 Host 配置。", tbName, dtName, cmd.HostKey);
                return null;
            }

            return hostUrl + cmd.Path;
        }

        /// <summary>
        /// 根据 NetCmd 名称获取完整 URL（HostKey URL + Path），泛型版本提供编译期类型约束。
        /// </summary>
        /// <typeparam name="T">Luban 表类型（编译期约束），Name 用于构造复合缓存键。</typeparam>
        /// <param name="dtName">NetCmd 数据类型名称。</param>
        /// <returns>URL 字符串，不存在时返回 null。</returns>
        public override string GetNetCmdUrl<T>(string dtName)
        {
            return GetNetCmdUrl(typeof(T).Name, dtName);
        }

        /// <summary>
        /// 根据指令行数据解析完整 URL（HostKey URL + Path）。
        /// </summary>
        /// <param name="cmdRow">网络指令行数据。</param>
        /// <returns>完整 URL 字符串，解析失败时返回 null。</returns>
        public override string ResolveNetCmdUrl(INetworkCmdRow cmdRow)
        {
            if (cmdRow == null)
            {
                return null;
            }

            if (!m_HostKeyCache.TryGetValue(cmdRow.HostKey, out string hostUrl))
            {
                Log.Warning(LogTag.Network, "ResolveNetCmdUrl NetCmd [{0}] 的 HostKey [{1}] 未找到对应 Host 配置。", cmdRow.Name, cmdRow.HostKey);
                return null;
            }

            return hostUrl + cmdRow.Path;
        }

        /// <summary>
        /// 根据指令名称（INetworkCmdRow.Name）解析对应指令行数据。
        /// </summary>
        /// <param name="cmdName">指令名称。</param>
        /// <returns>匹配的指令行数据，未找到时返回 null。</returns>
        public override INetworkCmdRow ResolveNetCmdRow(string cmdName)
        {
            if (string.IsNullOrEmpty(cmdName))
            {
                return null;
            }

            if (m_CmdRowIndex.TryGetValue(cmdName, out INetworkCmdRow row))
            {
                return row;
            }

            Log.Warning(LogTag.Network, "ResolveNetCmdRow 未找到 NetCmd：{0}。", cmdName);
            return null;
        }

        /// <summary>
        /// 获取所有 HTTP 类型 NetCmd 的完整 URL 集合（去重），供 DoH 收集 IP 使用。
        /// </summary>
        /// <returns>完整 URL 枚举。</returns>
        public override IEnumerable<string> GetAllNetCmdUrls()
        {
            var urls = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            foreach (var kvp in m_CmdCache)
            {
                CmdCacheEntry data = kvp.Value;
                if (!data.Way.Equals("HTTP_GET", StringComparison.OrdinalIgnoreCase) && !data.Way.Equals("HTTP_POST", StringComparison.OrdinalIgnoreCase) && !data.Way.Equals("HTTP_URL", StringComparison.OrdinalIgnoreCase))
                {
                    continue;
                }

                if (!m_HostKeyCache.TryGetValue(data.HostKey, out string hostUrl))
                {
                    continue;
                }

                urls.Add(hostUrl + data.Path);
            }

            return urls;
        }

        /// <summary>
        /// 获取指定类型的 Luban 表实例。
        /// </summary>
        /// <typeparam name="T">Luban 表类型。</typeparam>
        /// <returns>表实例，不存在时返回 null。</returns>
        public override T GetNetCmd<T>()
        {
            string typeName = typeof(T).Name;
            if (m_NetworkDatas.TryGetValue(typeName, out ITable table))
            {
                return table as T;
            }

            return null;
        }

        /// <summary>
        /// 根据表类型名获取指定的 Luban 表实例。
        /// </summary>
        /// <param name="tbName">Luban 表类型名。</param>
        /// <returns>表实例，不存在时返回 null。</returns>
        public override ITable GetNetCmd(string tbName)
        {
            if (string.IsNullOrEmpty(tbName))
            {
                return null;
            }

            if (m_NetworkDatas.TryGetValue(tbName, out ITable table))
            {
                return table;
            }

            return null;
        }

        /// <summary>
        /// 检查网络激活状态。
        /// </summary>
        /// <returns>网络可用返回 true。</returns>
        public override bool CheckNetworkActive()
        {
            return Application.internetReachability != NetworkReachability.NotReachable;
        }

        /// <summary>
        /// URL 编码（UTF-8 百分号编码）。
        /// </summary>
        /// <param name="str">待编码字符串。</param>
        /// <returns>编码后的字符串。</returns>
        public override string UrlEncode(string str)
        {
            if (string.IsNullOrEmpty(str))
            {
                return string.Empty;
            }

            return Uri.EscapeDataString(str);
        }

        /// <summary>
        /// 异步查询外网 IP 地址。
        /// </summary>
        /// <returns>外网 IP 地址字符串。</returns>
        public override async UniTask<string> QueryPublicIPAddressAsync()
        {
            if (m_HttpManager == null)
            {
                Log.Warning(LogTag.Network, "QueryPublicIPAddressAsync：HttpManager 未就绪。");
                return string.Empty;
            }

            HttpResponse response = null;
            try
            {
                response = await m_HttpManager.GetAsync("http://api.ipify.org");
                bool isSuccess = response != null && response.IsSuccess;
                string body = response?.Body;
                string error = response?.Error;
                if (isSuccess)
                {
                    return body ?? string.Empty;
                }

                Log.Warning(LogTag.Network, "QueryPublicIPAddressAsync 请求失败：{0}。", error);
                return string.Empty;
            }
            catch (Exception e)
            {
                Log.Error(LogTag.Network, "QueryPublicIPAddressAsync 异常：{0}。", e);
                return string.Empty;
            }
            finally
            {
                if (response != null)
                {
                    ReferencePool.Put(response);
                }
            }
        }

        /// <summary>
        /// 查询本机内网 IP 地址。
        /// </summary>
        /// <returns>内网 IP 地址字符串，无可用地址时返回 null。</returns>
        public override string QueryLocalIPAddress()
        {
            foreach (NetworkInterface ni in NetworkInterface.GetAllNetworkInterfaces())
            {
                if (ni.NetworkInterfaceType is NetworkInterfaceType.Wireless80211 or NetworkInterfaceType.Ethernet && ni.OperationalStatus == OperationalStatus.Up)
                {
                    foreach (UnicastIPAddressInformation ip in ni.GetIPProperties().UnicastAddresses)
                    {
                        if (ip.Address.AddressFamily == AddressFamily.InterNetwork)
                        {
                            return ip.Address.ToString();
                        }
                    }
                }
            }

            return null;
        }

        /// <summary>
        /// 注入服务器时间获取委托，由业务层提供具体的网络请求实现。
        /// </summary>
        /// <param name="fetcher">异步获取服务器 UTC0 时间戳（毫秒）的委托。</param>
        public override void SetServerTimeFetcher(Func<UniTask<long>> fetcher)
        {
            m_ServerTimeFetcher = fetcher;
        }

        /// <summary>
        /// 异步调用已注入的委托获取服务器 UTC0 时间戳，结果写入 ServerTime。
        /// </summary>
        /// <returns>表示异步操作的 UniTask。</returns>
        public override async UniTask FetchServerTimeAsync()
        {
            if (m_ServerTimeFetcher == null)
            {
                Log.Warning(LogTag.Network, "FetchServerTimeAsync 尚未注入 ServerTimeFetcher，请先调用 SetServerTimeFetcher。");
                return;
            }

            m_ServerTime = await m_ServerTimeFetcher.Invoke();
            Log.Debug(LogTag.Network, "服务器时间戳获取成功：{0}。", m_ServerTime);
        }

        /// <summary>
        /// 管理器轮询。
        /// </summary>
        public override void Update()
        {
        }

        /// <summary>
        /// 关闭并清理管理器。
        /// </summary>
        public override void Shutdown()
        {
            m_NetworkDatas.Clear();
            m_HostKeyCache?.Clear();
            m_CmdCache?.Clear();
            m_CmdRowIndex?.Clear();
        }
    }
}
