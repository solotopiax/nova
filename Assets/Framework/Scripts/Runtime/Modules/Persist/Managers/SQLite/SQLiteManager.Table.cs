/***************************************************************
 * (c) copyright 2026 - 2030, Solotopia
 * All Rights Reserved.
 * -------------------------------------------------------------
 * filename:  SQLiteManager.Table.cs
 * author:    taoye
 * created:   2026/3/18
 * descrip:   SQLiteManager 内嵌 Table 类 —— CRUD 实现
 ***************************************************************/

#if !UNITY_WEBGL

using System.Collections.Generic;
using SqlCipher4Unity3D;

namespace NovaFramework.Runtime
{
    internal sealed partial class SQLiteManager : PersistManagerBase<SQLiteManagerConfig>, ISQLiteManager
    {
        /// <summary>
        /// 单张 SQLite 表的内存缓存与写缓冲容器（写缓冲模式，不实时写库）。
        /// </summary>
        internal sealed partial class Table
        {
            /// <summary>
            /// 初始化指定分类名的 Table 实例。
            /// </summary>
            /// <param name="classify">分类名（表名）。</param>
            public Table(string classify)
            {
                m_Classify = classify;
            }

            /// <summary>
            /// 从数据库加载全量数据到内存缓存（懒加载，首次访问时调用）。
            /// </summary>
            /// <param name="connection">SQLite 连接。</param>
            /// <param name="useAES">是否启用 AES 解密。</param>
            public void EnsureLoaded(SQLiteConnection connection, bool useAES)
            {
                if (m_IsLoaded)
                {
                    return;
                }

                CreateTableIfNotExist(connection);

                var rows = connection.Query<Row>($"SELECT Name, Value FROM [{m_Classify}]");
                foreach (var row in rows)
                {
                    var value = useAES ? Util.Encrypt.AES.DecryptString(row.Value) : row.Value;
                    m_Items[row.Name] = value;
                }

                m_IsLoaded = true;
            }

            /// <summary>
            /// 将写缓冲与删除缓冲通过单次事务批量提交到数据库，提交后清空缓冲。
            /// </summary>
            /// <param name="connection">SQLite 连接。</param>
            /// <param name="useAES">是否启用 AES 加密。</param>
            public void FlushToDb(SQLiteConnection connection, bool useAES)
            {
                if (m_PendingWrites.Count == 0 && m_PendingDeletes.Count == 0)
                {
                    return;
                }

                connection.BeginTransaction();
                try
                {
                    foreach (var kv in m_PendingWrites)
                    {
                        var stored = useAES ? Util.Encrypt.AES.EncryptString(kv.Value) : kv.Value;
                        connection.Execute($"INSERT OR REPLACE INTO [{m_Classify}] (Name, Value) VALUES (?, ?)", kv.Key, stored);
                    }

                    foreach (var name in m_PendingDeletes)
                    {
                        connection.Execute($"DELETE FROM [{m_Classify}] WHERE Name = ?", name);
                    }

                    connection.Commit();
                }
                catch (System.Exception ex)
                {
                    Log.Error(LogTag.Persist, "SQLiteManager.Table.FlushToDb [{0}] failed: {1}", m_Classify, ex.Message);
                    try
                    {
                        connection.Rollback();
                    }
                    catch (System.Exception rollbackEx)
                    {
                        Log.Error(LogTag.Persist, "SQLiteManager.Table.FlushToDb [{0}] rollback failed: {1}", m_Classify, rollbackEx.Message);
                    }

                    throw;
                }

                m_PendingWrites.Clear();
                m_PendingDeletes.Clear();
            }

            // ---- CRUD

            /// <summary>
            /// 判断指定条目是否存在。
            /// </summary>
            /// <param name="item">条目名。</param>
            /// <returns>存在返回 true。</returns>
            public bool HasItem(string item)
            {
                return m_Items.ContainsKey(item);
            }

            /// <summary>
            /// 删除指定条目（写入删除缓冲，不立即执行 SQL）。
            /// </summary>
            /// <param name="item">条目名。</param>
            /// <returns>删除成功返回 true。</returns>
            public bool RemoveItem(string item)
            {
                if (!m_Items.Remove(item))
                {
                    return false;
                }

                m_PendingWrites.Remove(item);
                m_PendingDeletes.Add(item);
                return true;
            }

            /// <summary>
            /// 删除全部条目（写入删除缓冲）。
            /// </summary>
            public void RemoveAll()
            {
                foreach (var key in m_Items.Keys)
                {
                    m_PendingDeletes.Add(key);
                }

                m_Items.Clear();
                m_PendingWrites.Clear();
            }

            /// <summary>
            /// 清空所有待提交的写缓冲和删除缓冲，不执行任何 IO 操作。
            /// 用于 RemoveAll + DROP TABLE 场景，避免冗余的 FlushToDb。
            /// </summary>
            public void ClearPendingBuffers()
            {
                m_PendingWrites.Clear();
                m_PendingDeletes.Clear();
            }

            /// <summary>
            /// 获取全部条目名数组。
            /// </summary>
            /// <returns>条目名数组。</returns>
            public string[] GetAllItemNames()
            {
                var keys = new string[m_Items.Count];
                m_Items.Keys.CopyTo(keys, 0);
                return keys;
            }

            /// <summary>
            /// 将全部条目名填充到列表。
            /// </summary>
            /// <param name="results">结果列表，方法会追加而非清空。</param>
            public void GetAllItemNames(List<string> results)
            {
                foreach (var key in m_Items.Keys)
                {
                    results.Add(key);
                }
            }

            /// <summary>
            /// 读取字符串值（从内存缓存读取）。
            /// </summary>
            /// <param name="item">条目名。</param>
            /// <param name="defaultValue">不存在时的默认值。</param>
            /// <returns>读取到的值，不存在时返回默认值。</returns>
            public string GetString(string item, string defaultValue = "")
            {
                return m_Items.TryGetValue(item, out var value) ? value : defaultValue;
            }

            /// <summary>
            /// 写入字符串值（写入内存缓存和写缓冲，不立即执行 SQL）。
            /// </summary>
            /// <param name="item">条目名。</param>
            /// <param name="value">要写入的值。</param>
            public void SetString(string item, string value)
            {
                m_Items[item] = value;
                m_PendingWrites[item] = value;
                m_PendingDeletes.Remove(item);
            }

            // ---- 内部辅助

            /// <summary>
            /// 确保表在数据库中存在，不存在则创建。
            /// </summary>
            /// <param name="connection">SQLite 连接。</param>
            private void CreateTableIfNotExist(SQLiteConnection connection)
            {
                connection.Execute($"CREATE TABLE IF NOT EXISTS [{m_Classify}] (Name TEXT PRIMARY KEY, Value TEXT)");
            }

            /// <summary>
            /// SQLite 表的行映射对象。
            /// </summary>
            private class Row
            {
                /// <summary>
                /// 条目名（主键）。
                /// </summary>
                public string Name { get; set; }

                /// <summary>
                /// 条目值（字符串，可能经过 AES 加密）。
                /// </summary>
                public string Value { get; set; }
            }
        }
    }
}

#endif
