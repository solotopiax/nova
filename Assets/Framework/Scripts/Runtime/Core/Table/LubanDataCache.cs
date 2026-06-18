/***************************************************************
 * (c) copyright 2026 - 2030, Solotopia
 * All Rights Reserved.
 * -------------------------------------------------------------
 * filename:  LubanDataCache.cs
 * author:    taoye
 * created:   2026/4/27
 * descrip:   Luban 数据加载缓存容器
 ***************************************************************/

using System.Collections.Generic;

namespace NovaFramework.Runtime
{
    /// <summary>
    /// Luban 数据加载缓存容器，封装 Phase 1 加载阶段产生的临时数据。
    /// 持有数据字典与来源追踪字典，供 LubanDataReceiver 写入，
    /// 并由各 Manager 的 BuildXxxFromCache 方法消费后清空。
    /// </summary>
    public sealed class LubanDataCache
    {
        /// <summary>
        /// 数据字典（小写表名 -> 数据对象）。
        /// </summary>
        private readonly Dictionary<string, object> m_DataMap;
        /// <summary>
        /// 数据字典（小写表名 -> 数据对象）的访问属性。
        /// </summary>
        public Dictionary<string, object> DataMap => m_DataMap;

        /// <summary>
        /// 来源追踪字典（小写表名 -> 写入该表的 unit 名称列表），用于合并冲突时定位来源。
        /// </summary>
        private readonly Dictionary<string, List<string>> m_SourceTracker;
        /// <summary>
        /// 来源追踪字典（小写表名 -> 写入该表的 unit 名称列表）的访问属性。
        /// </summary>
        public Dictionary<string, List<string>> SourceTracker => m_SourceTracker;

        /// <summary>
        /// 构造方法，初始化内部字典。
        /// </summary>
        public LubanDataCache()
        {
            m_DataMap = new Dictionary<string, object>();
            m_SourceTracker = new Dictionary<string, List<string>>();
        }

        /// <summary>
        /// 清空所有缓存数据。  
        /// </summary>
        public void Clear()
        {
            m_DataMap.Clear();
            m_SourceTracker.Clear();
        }
    }
}
