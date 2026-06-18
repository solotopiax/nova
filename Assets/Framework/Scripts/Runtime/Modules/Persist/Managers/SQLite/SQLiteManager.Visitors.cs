/***************************************************************
 * (c) copyright 2026 - 2030, Solotopia
 * All Rights Reserved.
 * -------------------------------------------------------------
 * filename:  SQLiteManager.Visitors.cs
 * author:    taoye
 * created:   2026/3/18
 * descrip:   SQLiteManager —— 属性与字段
 ***************************************************************/

#if !UNITY_WEBGL
using System.Collections.Generic;
using SqlCipher4Unity3D;
#endif

namespace NovaFramework.Runtime
{
    internal sealed partial class SQLiteManager : PersistManagerBase<SQLiteManagerConfig>, ISQLiteManager
    {
#if !UNITY_WEBGL
        /// <summary>
        /// SQLite Cipher 数据库加密密码（为空时不启用数据库级加密）。
        /// </summary>
        private string m_CipherPassword;

        /// <summary>
        /// SQLite 数据库连接实例（实例字段，彻底消除多实例污染风险）。
        /// </summary>
        private SQLiteConnection m_Connection;

        /// <summary>
        /// 分类名 → Table 对象，<分类名（表名）, Table 缓存对象>。
        /// </summary>
        private Dictionary<string, Table> m_Tables = new();

        /// <summary>
        /// 有未提交写缓冲的表名集合（脏追踪）。
        /// </summary>
        private HashSet<string> m_DirtyTables = new();
#endif
    }
}
