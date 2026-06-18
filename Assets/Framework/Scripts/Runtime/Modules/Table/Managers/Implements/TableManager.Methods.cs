/***************************************************************
 * (c) copyright 2026 - 2030, Solotopia
 * All Rights Reserved.
 * -------------------------------------------------------------
 * filename:  TableManager.Methods.cs
 * author:    taoye
 * created:   2026/2/5
 * descrip:   表格管理器 -- 私有方法
 ***************************************************************/

using System;
using System.Collections.Generic;
using Cysharp.Threading.Tasks;
using Newtonsoft.Json.Linq;

namespace NovaFramework.Runtime
{
    internal sealed partial class TableManager : TableManagerBase
    {
        /// <summary>
        /// 并发加载单个表格单元，将数据拆分到 m_DataCache。
        /// </summary>
        /// <param name="unit">表格单元设置。</param>
        /// <param name="loadFunc">异步加载委托。</param>
        /// <param name="releaseFunc">资源释放委托。</param>
        /// <returns>是否加载并解析成功。</returns>
        private UniTask<bool> InternalLoadUnitAsync(TableUnitSetting unit, DataReceiver.LoadAssetAsyncFunc loadFunc, DataReceiver.ReleaseAssetAction releaseFunc)
        {
            return new LubanDataReceiver(m_DataCache, unit, loadFunc, releaseFunc).ReadDataAssetAsync(unit.AssetLocation);
        }

        /// <summary>
        /// 同步加载单个表格单元，将数据拆分到 m_DataCache。
        /// </summary>
        /// <param name="unit">表格单元设置。</param>
        /// <param name="syncLoadFunc">同步加载委托。</param>
        /// <param name="releaseFunc">资源释放委托。</param>
        /// <returns>是否加载并解析成功。</returns>
        private bool InternalLoadUnitSync(TableUnitSetting unit, DataReceiver.LoadAssetSyncFunc syncLoadFunc, DataReceiver.ReleaseAssetAction releaseFunc)
        {
            return new LubanDataReceiver(m_DataCache, unit, syncLoadFunc, releaseFunc).ReadDataAssetSync(unit.AssetLocation);
        }

        /// <summary>
        /// 反射构造 TableTables 实例，将所有 ITable 提取到 m_Tables。
        /// </summary>
        /// <returns>是否构建成功。</returns>
        private bool BuildTablesFromCache()
        {
            Func<string, JArray> loader = key =>
            {
                if (m_DataCache.DataMap.TryGetValue(key, out object value) && value is JArray jArray)
                {
                    return jArray;
                }
                Log.Warning(LogTag.Table, "数据缓存中未找到表格数据：{0}", key);
                return new JArray();
            };

            IConfigManager configManager = FrameworkManagersGroup.GetManager<IConfigManager>();
            if (configManager == null)
            {
                Log.Error(LogTag.Table, "IConfigManager 未注册，无法加载 Table 数据，请确认场景中存在 ConfigComponent。");
                return false;
            }
            string namespace_ = configManager.Namespace;
            Dictionary<Type, ITable> tables = LubanTablesLoader.Load(c_TableTablesClassName, namespace_, loader);
            if (tables == null)
            {
                return false;
            }

            foreach (var kv in tables)
            {
                m_Tables[kv.Key] = kv.Value;
            }

            m_DataCache.Clear();
            m_DataCache = null;
            Log.Debug(LogTag.Table, "Table 成功加载了 {0} 个数据文件，共计 {1} 个表格数据。", m_UnitSettings.Count, m_Tables.Count);
            return true;
        }
    }
}
