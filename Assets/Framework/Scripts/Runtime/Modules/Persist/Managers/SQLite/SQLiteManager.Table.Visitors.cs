/***************************************************************
 * (c) copyright 2026 - 2030, Solotopia
 * All Rights Reserved.
 * -------------------------------------------------------------
 * filename:  SQLiteManager.Table.Visitors.cs
 * author:    taoye
 * created:   2026/3/18
 * descrip:   SQLiteManager 内嵌 Table 类 —— 属性与字段
 ***************************************************************/

#if !UNITY_WEBGL

using System.Collections.Generic;

namespace NovaFramework.Runtime
{
    internal sealed partial class SQLiteManager : PersistManagerBase<SQLiteManagerConfig>, ISQLiteManager
    {
        /// <summary>
        /// 单张 SQLite 表的内存缓存与写缓冲容器。
        /// 读：从 m_Items 内存字典返回（启动时全量 SELECT 或首次访问时懒加载）。
        /// 写：累积到 m_PendingWrites，在 FlushToDb 时通过单次事务批量提交。
        /// </summary>
        internal sealed partial class Table
        {
            /// <summary>
            /// 表名（即 classify 名）。
            /// </summary>
            private string m_Classify;
            public string Classify => m_Classify;

            /// <summary>
            /// 内存数据缓存，<条目名, 字符串值>。
            /// </summary>
            private Dictionary<string, string> m_Items = new();

            /// <summary>
            /// 获取条目字典（只读访问）。
            /// </summary>
            public IReadOnlyDictionary<string, string> Items => m_Items;

            /// <summary>
            /// 写缓冲，<条目名, 待写入字符串值>。
            /// </summary>
            private Dictionary<string, string> m_PendingWrites = new();

            /// <summary>
            /// 待删除条目名集合。
            /// </summary>
            private HashSet<string> m_PendingDeletes = new();

            /// <summary>
            /// 是否已从数据库加载全量数据。
            /// </summary>
            private bool m_IsLoaded;

            /// <summary>
            /// 获取条目数量。
            /// </summary>
            public int Count => m_Items.Count;
        }
    }
}

#endif
