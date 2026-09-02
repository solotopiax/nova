/***************************************************************
 * (c) copyright 2026 - 2030, Solotopia
 * All Rights Reserved.
 * -------------------------------------------------------------
 * filename:  NetworkManager.Methods.cs
 * author:    taoye
 * created:   2026/3/11
 * descrip:   Network管理器 —— 私有方法
 ***************************************************************/

using System;
using System.Collections.Generic;

namespace NovaFramework.Runtime
{
    /// <summary>
    /// Network 管理器。
    /// </summary>
    internal sealed partial class NetworkManager : NetworkManagerBase
    {
        /// <summary>
        /// 从数据缓存构建运行时缓存与 Luban Tables 对象。
        /// 通过 ITable<INetworkHostKeyRow> 和 ITable<INetworkCmdRow> 协变直接访问 DataList，彻底消除 JArray 手动解析。
        /// </summary>
        /// <param name="dataCache">Phase 1 写入的数据加载缓存，消费后由本方法清空。</param>
        /// <returns>构建成功返回 true。</returns>
        private bool BuildTablesFromCache(LubanDataCache dataCache)
        {
            if (dataCache == null)
            {
                Log.Error(LogTag.Network, "数据缓存为 null，无法构建 Tables。");
                return false;
            }

            IConfigManager configManager = FrameworkManagersGroup.GetManager<IConfigManager>();
            if (configManager == null)
            {
                Log.Error(LogTag.Network, "IConfigManager 未注册，无法加载 Network 数据，请确认场景中存在 ConfigComponent。");
                return false;
            }
            string namespace_ = configManager.Namespace;
            if (!string.IsNullOrEmpty(namespace_))
            {
                Dictionary<Type, ITable> hostKeyTables = LubanRuntimeData.LoadTables(
                    m_DataFormat, c_HostKeyTablesClassName, namespace_, dataCache, LogTag.Network, "HostKey");
                if (hostKeyTables == null)
                {
                    return false;
                }

                foreach (var kv in hostKeyTables)
                {
                    m_NetworkDatas[kv.Key.Name] = kv.Value;
                    BuildHostKeyCacheFromTable(kv.Value);
                }

                Dictionary<Type, ITable> networkTables = LubanRuntimeData.LoadTables(
                    m_DataFormat, c_NetworkTablesClassName, namespace_, dataCache, LogTag.Network, "NetCmd");
                if (networkTables == null)
                {
                    return false;
                }

                foreach (var kv in networkTables)
                {
                    m_NetworkDatas[kv.Key.Name] = kv.Value;
                    BuildCmdCacheFromTable(kv.Value);
                }
            }

            Log.Debug(LogTag.Network, "Network 成功加载了 {0} 个数据文件，共计 {1} 个表格数据。", m_HostKeyUnitSettings.Count + m_NetCmdUnitSettings.Count, m_NetworkDatas.Count);
            return true;
        }

        /// <summary>
        /// 从单个 ITable 中通过 ITable<INetworkHostKeyRow> 协变提取域名数据到 m_HostKeyCache。
        /// </summary>
        /// <param name="table">Luban 表实例。</param>
        private void BuildHostKeyCacheFromTable(ITable table)
        {
            if (!(table is ITable<INetworkHostKeyRow> typedTable))
            {
                Log.Warning(LogTag.Network, "表类型 '{0}' 未实现 ITable<INetworkHostKeyRow>，已跳过。请确认 Luban bean 已实现 INetworkHostKeyRow 接口。", table.GetType().Name);
                return;
            }

            IReadOnlyList<INetworkHostKeyRow> dataList = typedTable.DataList;
            for (int i = 0; i < dataList.Count; i++)
            {
                INetworkHostKeyRow row = dataList[i];
                if (row == null || string.IsNullOrEmpty(row.Name))
                {
                    continue;
                }

                bool primaryValid = TryNormalizeBaseUrl(row.Value, out string primary);
                bool fallbackValid = TryNormalizeBaseUrl(row.FallbackValue, out string fallback);
                if (!primaryValid && !string.IsNullOrWhiteSpace(row.Value))
                {
                    Log.Warning(LogTag.Network, "HostKey [{0}] 的主域名格式无效，将尝试使用备用域名。", row.Name);
                }
                if (!fallbackValid && !string.IsNullOrWhiteSpace(row.FallbackValue))
                {
                    Log.Warning(LogTag.Network, "HostKey [{0}] 的备用域名格式无效，已忽略备用域名。", row.Name);
                }

                if (!primaryValid && fallbackValid)
                {
                    primary = fallback;
                    fallback = string.Empty;
                    primaryValid = true;
                    fallbackValid = false;
                }

                if (!primaryValid)
                {
                    Log.Error(LogTag.Network, "HostKey [{0}] 的主域名和备用域名均无效，相关请求将不会发出。", row.Name);
                    continue;
                }

                if (fallbackValid &&
                    (!HasSameScheme(primary, fallback) || string.Equals(primary, fallback, StringComparison.OrdinalIgnoreCase)))
                {
                    fallback = string.Empty;
                }

                m_HostKeyCache[row.Name] = new HostKeyCacheEntry
                {
                    Primary = primary,
                    Fallback = fallback
                };
            }
        }

        /// <summary>
        /// 校验并保留 HostKey 基础地址；运行时不主动修剪非法尾空格或尾斜杠。
        /// </summary>
        /// <param name="value">原始基础地址。</param>
        /// <param name="normalized">有效时返回原值，否则返回空字符串。</param>
        /// <returns>是否为有效的 HTTP(S) 绝对地址。</returns>
        private static bool TryNormalizeBaseUrl(string value, out string normalized)
        {
            normalized = string.Empty;
            if (string.IsNullOrWhiteSpace(value) ||
                !string.Equals(value, value.Trim(), StringComparison.Ordinal) ||
                value.EndsWith("/", StringComparison.Ordinal) ||
                !Uri.TryCreate(value, UriKind.Absolute, out Uri uri) ||
                (uri.Scheme != Uri.UriSchemeHttp && uri.Scheme != Uri.UriSchemeHttps) ||
                string.IsNullOrWhiteSpace(uri.Host))
            {
                return false;
            }

            normalized = value;
            return true;
        }

        /// <summary>
        /// 判断两个有效基础地址是否使用相同协议。
        /// </summary>
        private static bool HasSameScheme(string left, string right)
        {
            return Uri.TryCreate(left, UriKind.Absolute, out Uri leftUri) &&
                   Uri.TryCreate(right, UriKind.Absolute, out Uri rightUri) &&
                   string.Equals(leftUri.Scheme, rightUri.Scheme, StringComparison.OrdinalIgnoreCase);
        }

        /// <summary>
        /// 向有序列表追加基础地址与 Path 的拼接结果，并忽略空值和重复项。
        /// </summary>
        private static void AddUniqueUrl(List<string> urls, string baseUrl, string path)
        {
            if (string.IsNullOrEmpty(baseUrl))
            {
                return;
            }

            string url = baseUrl + (path ?? string.Empty);
            for (int i = 0; i < urls.Count; i++)
            {
                if (string.Equals(urls[i], url, StringComparison.OrdinalIgnoreCase))
                {
                    return;
                }
            }

            urls.Add(url);
        }

        /// <summary>
        /// 从单个 ITable 中通过 ITable<INetworkCmdRow> 协变提取指令数据到 m_CmdCache。
        /// 缓存键使用 "表类型名.行Name" 复合格式，避免不同表中同名行互相覆盖。
        /// </summary>
        /// <param name="table">Luban 表实例。</param>
        private void BuildCmdCacheFromTable(ITable table)
        {
            if (!(table is ITable<INetworkCmdRow> typedTable))
            {
                Log.Warning(LogTag.Network, "表类型 '{0}' 未实现 ITable<INetworkCmdRow>，已跳过。请确认 Luban bean 已实现 INetworkCmdRow 接口。", table.GetType().Name);
                return;
            }

            string tableTypeName = table.GetType().Name;
            IReadOnlyList<INetworkCmdRow> dataList = typedTable.DataList;
            for (int i = 0; i < dataList.Count; i++)
            {
                INetworkCmdRow row = dataList[i];
                if (row == null || string.IsNullOrEmpty(row.Name))
                {
                    continue;
                }

                string compositeKey = tableTypeName + "." + row.Name;
                m_CmdCache[compositeKey] = new CmdCacheEntry
                {
                    Way = row.Way ?? string.Empty,
                    HostKey = row.HostKey ?? string.Empty,
                    Path = row.Path ?? string.Empty
                };
                m_CmdRowIndex[row.Name] = row;
            }
        }

    }
}
