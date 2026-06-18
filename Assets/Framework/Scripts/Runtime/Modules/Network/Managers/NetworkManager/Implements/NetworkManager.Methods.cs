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
using Newtonsoft.Json.Linq;

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

            Func<string, JArray> loader = key =>
            {
                if (dataCache.DataMap.TryGetValue(key, out object value) && value is JArray jArray)
                {
                    return jArray;
                }
                Log.Warning(LogTag.Network, "数据缓存中未找到数据：{0}", key);
                return new JArray();
            };

            IConfigManager configManager = FrameworkManagersGroup.GetManager<IConfigManager>();
            if (configManager == null)
            {
                Log.Error(LogTag.Network, "IConfigManager 未注册，无法加载 Network 数据，请确认场景中存在 ConfigComponent。");
                return false;
            }
            string namespace_ = configManager.Namespace;
            if (!string.IsNullOrEmpty(namespace_))
            {
                Dictionary<Type, ITable> hostKeyTables = LubanTablesLoader.Load(c_HostKeyTablesClassName, namespace_, loader);
                if (hostKeyTables == null)
                {
                    return false;
                }

                foreach (var kv in hostKeyTables)
                {
                    m_NetworkDatas[kv.Key.Name] = kv.Value;
                    BuildHostKeyCacheFromTable(kv.Value);
                }

                Dictionary<Type, ITable> networkTables = LubanTablesLoader.Load(c_NetworkTablesClassName, namespace_, loader);
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

                m_HostKeyCache[row.Name] = row.Value ?? string.Empty;
            }
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
