/***************************************************************
 * (c) copyright 2026 - 2030, Solotopia
 * All Rights Reserved.
 * -------------------------------------------------------------
 * filename:  Util.SQLite.cs
 * author:    taoye
 * created:   2026/3/18
 * descrip:   SQLite 数据库操作实用函数
 ***************************************************************/

#if !UNITY_WEBGL

using System;
using System.Collections.Generic;
using System.Linq;
using SqlCipher4Unity3D;

namespace NovaFramework.Runtime
{
    public static partial class Util
    {
        /// <summary>
        /// SQLite 数据库操作实用函数集。
        /// 封装连接管理、表操作、数据 CRUD 和事务执行，需要 SqlCipher4Unity3D 插件。
        /// 仅非 WebGL 平台可用。
        /// </summary>
        public static class SQLite
        {
            /// <summary>
            /// 数据库同步写入级别。
            /// </summary>
            public enum SynchronousLevel
            {
                /// <summary>
                /// 不同步，最快但崩溃时可能丢失数据。
                /// </summary>
                Off = 0,

                /// <summary>
                /// 普通同步，性能与安全的平衡点，适用于大多数场景。
                /// </summary>
                Normal = 1,

                /// <summary>
                /// 完全同步，每次写入都刷盘，最安全但性能最低。
                /// </summary>
                Full = 2,
            }

            /// <summary>
            /// SQLite 表的最小行映射基类，子类可追加列属性。
            /// </summary>
            public class Table
            {
            /// <summary>
            /// 行名称（主键）。sqlite-net 按属性名自动映射 Name → name 列，无需显式 Column 特性。
            /// </summary>
            public string Name { get; set; }
            }

            // ---- 连接管理

            /// <summary>
            /// 创建并初始化 SQLite 数据库连接，同时启动一个事务。
            /// </summary>
            /// <param name="databasePath">数据库文件的绝对路径。</param>
            /// <param name="password">数据库 Cipher 加密密码，为空时不加密。</param>
            /// <param name="synchronous">写入同步级别，默认 Normal。</param>
            /// <returns>已就绪的 SQLiteConnection 实例。</returns>
            public static SQLiteConnection CreateConnection(string databasePath, string password = null, SynchronousLevel synchronous = SynchronousLevel.Normal)
            {
                var connection = new SQLiteConnection(databasePath, password);
                connection.Execute($"PRAGMA synchronous = {(int)synchronous};");
                connection.BeginTransaction();
                return connection;
            }

            /// <summary>
            /// 提交当前事务、关闭并释放数据库连接。
            /// 注意：仅 Commit 不再 BeginTransaction，避免关闭前重新开事务导致 statement 未 finalize、
            /// Win 平台 sqlite3_close 返回 SQLITE_BUSY、底层句柄滞留持锁的问题。
            /// 关闭后调用方若要再使用必须重新 CreateConnection。
            /// </summary>
            /// <param name="connection">要关闭的 SQLiteConnection 实例，null 时直接返回。</param>
            public static void CloseConnection(SQLiteConnection connection)
            {
                if (connection == null)
                {
                    return;
                }

                if (connection.IsInTransaction)
                {
                    connection.Commit();
                }

                // 主动 checkpoint 收口 WAL/-shm 附属文件（rollback journal 模式无副作用），
                // 之后调用方若需删库，删主文件即可，附属文件由 DeleteDatabase 统一兜底处理。
                try
                {
                    connection.Execute("PRAGMA wal_checkpoint(TRUNCATE);");
                }
                catch (Exception ex)
                {
                    Log.Warning(LogTag.SysIO, "Util.SQLite.CloseConnection wal_checkpoint 失败（通常不影响关闭）：{0}", ex.Message);
                }

                connection.Close();
                connection.Dispose();
            }

            /// <summary>
            /// 物理删除 SQLite 数据库文件及其附属文件（-shm / -wal / -journal）。
            /// 调用前必须已通过 CloseConnection 释放占用，否则 Win 下会因句柄持锁导致 unlink 失败。
            /// 单次调用足够覆盖 rollback journal / WAL 两种 journal_mode 残留。
            /// </summary>
            /// <param name="databasePath">数据库主文件绝对路径。</param>
            public static void DeleteDatabase(string databasePath)
            {
                if (string.IsNullOrEmpty(databasePath))
                {
                    Log.Fatal(LogTag.SysIO, "Util.SQLite.DeleteDatabase databasePath 无效。");
                    return;
                }

                Util.SysIO.File.Delete(databasePath);
                Util.SysIO.File.Delete(databasePath + "-shm");
                Util.SysIO.File.Delete(databasePath + "-wal");
                Util.SysIO.File.Delete(databasePath + "-journal");
            }

            /// <summary>
            /// 提交当前事务并立即开启新事务（用于显式落盘）。
            /// </summary>
            /// <param name="connection">数据库连接。</param>
            public static void CommitAndBeginTransaction(SQLiteConnection connection)
            {
                if (connection == null)
                {
                    return;
                }

                if (connection.IsInTransaction)
                {
                    connection.Commit();
                }

                connection.BeginTransaction();
            }

            // ---- 表操作

            /// <summary>
            /// 获取数据库中所有用户表的名称（排除 sqlite_sequence 系统表）。
            /// </summary>
            /// <param name="connection">数据库连接。</param>
            /// <returns>表名列表。</returns>
            public static List<string> GetTableNames(SQLiteConnection connection)
            {
                EnsureConnection(connection);
                var rows = connection.Query<Table>("SELECT name FROM sqlite_master WHERE type = 'table' AND name != 'sqlite_sequence'");
                return rows.Select(r => r.Name).ToList();
            }

            /// <summary>
            /// 获取数据库中所有用户表，并将结果映射为自定义行类型。
            /// </summary>
            /// <typeparam name="T">继承 Table 的自定义行类型，必须有无参构造函数。</typeparam>
            /// <param name="connection">数据库连接。</param>
            /// <returns>自定义类型的行列表。</returns>
            public static List<T> GetTableNames<T>(SQLiteConnection connection) where T : Table, new()
            {
                EnsureConnection(connection);
                return connection.Query<T>("SELECT name FROM sqlite_master WHERE type = 'table' AND name != 'sqlite_sequence'");
            }

            /// <summary>
            /// 创建指定名称的数据库表（已存在则跳过）。
            /// 表固定包含 Name TEXT PRIMARY KEY 列，可追加自定义列。
            /// </summary>
            /// <param name="connection">数据库连接。</param>
            /// <param name="tableName">表名。</param>
            /// <param name="columns"><列名, .NET 类型> 字典，支持 int/string/bool/float/double。</param>
            public static void CreateTable(SQLiteConnection connection, string tableName, Dictionary<string, Type> columns)
            {
                EnsureConnection(connection);
                ValidateIdentifier(tableName, nameof(tableName));

                var columnDefs = new List<string> { "[Name] TEXT PRIMARY KEY" };
                foreach (var kv in columns)
                {
                    ValidateIdentifier(kv.Key, "columnName");
                    columnDefs.Add($"[{kv.Key}] {GetColumnType(kv.Value)}");
                }

                connection.Execute($"CREATE TABLE IF NOT EXISTS [{tableName}] ({string.Join(", ", columnDefs)})");
            }

            /// <summary>
            /// 删除指定数据库表（不存在则忽略）。
            /// </summary>
            /// <param name="connection">数据库连接。</param>
            /// <param name="tableName">表名。</param>
            public static void DeleteTable(SQLiteConnection connection, string tableName)
            {
                EnsureConnection(connection);
                ValidateIdentifier(tableName, nameof(tableName));
                connection.Execute($"DROP TABLE IF EXISTS [{tableName}]");
            }

            /// <summary>
            /// 清空整个数据库（删除所有用户表）。
            /// </summary>
            /// <param name="connection">数据库连接。</param>
            public static void ClearDatabase(SQLiteConnection connection)
            {
                EnsureConnection(connection);
                foreach (var tableName in GetTableNames(connection))
                {
                    DeleteTable(connection, tableName);
                }
            }

            // ---- 数据 CRUD

            /// <summary>
            /// 插入或覆盖更新指定行（INSERT OR REPLACE）。
            /// </summary>
            /// <param name="connection">数据库连接。</param>
            /// <param name="tableName">表名。</param>
            /// <param name="itemName">行的 Name 主键值。</param>
            /// <param name="fields"><列名, 值> 字典。</param>
            public static void InsertOrReplaceData(SQLiteConnection connection, string tableName, string itemName, Dictionary<string, object> fields)
            {
                EnsureConnection(connection);
                ValidateIdentifier(tableName, nameof(tableName));
                var columnNames = new List<string>(fields.Count);
                foreach (var key in fields.Keys)
                {
                    columnNames.Add($"[{ValidateIdentifier(key, "columnName")}]");
                }

                var columns = string.Join(", ", columnNames);
                var placeholders = string.Join(", ", Enumerable.Repeat("?", fields.Count));
                var values = new List<object> { itemName };
                values.AddRange(fields.Values);
                connection.Execute($"INSERT OR REPLACE INTO [{tableName}] ([Name], {columns}) VALUES (?, {placeholders})", values.ToArray());
            }

            /// <summary>
            /// 插入新行（主键冲突时抛出异常）。
            /// </summary>
            /// <param name="connection">数据库连接。</param>
            /// <param name="tableName">表名。</param>
            /// <param name="itemName">行的 Name 主键值。</param>
            /// <param name="fields"><列名, 值> 字典。</param>
            public static void InsertData(SQLiteConnection connection, string tableName, string itemName, Dictionary<string, object> fields)
            {
                EnsureConnection(connection);
                ValidateIdentifier(tableName, nameof(tableName));
                var columnNames = new List<string>(fields.Count);
                foreach (var key in fields.Keys)
                {
                    columnNames.Add($"[{ValidateIdentifier(key, "columnName")}]");
                }

                var columns = string.Join(", ", columnNames);
                var placeholders = string.Join(", ", Enumerable.Repeat("?", fields.Count));
                var values = new List<object> { itemName };
                values.AddRange(fields.Values);
                connection.Execute($"INSERT INTO [{tableName}] ([Name], {columns}) VALUES (?, {placeholders})", values.ToArray());
            }

            /// <summary>
            /// 更新指定行的字段值（按 Name 主键定位）。
            /// </summary>
            /// <param name="connection">数据库连接。</param>
            /// <param name="tableName">表名。</param>
            /// <param name="itemName">行的 Name 主键值。</param>
            /// <param name="fields"><列名, 新值> 字典。</param>
            public static void UpdateData(SQLiteConnection connection, string tableName, string itemName, Dictionary<string, object> fields)
            {
                EnsureConnection(connection);
                ValidateIdentifier(tableName, nameof(tableName));
                var setClause = string.Join(", ", fields.Keys.Select(k => $"[{ValidateIdentifier(k, "columnName")}] = ?"));
                var values = new List<object>(fields.Values) { itemName };
                connection.Execute($"UPDATE [{tableName}] SET {setClause} WHERE [Name] = ?", values.ToArray());
            }

            /// <summary>
            /// 删除指定行（按 Name 主键定位）。
            /// </summary>
            /// <param name="connection">数据库连接。</param>
            /// <param name="tableName">表名。</param>
            /// <param name="itemName">行的 Name 主键值。</param>
            public static void DeleteData(SQLiteConnection connection, string tableName, string itemName)
            {
                EnsureConnection(connection);
                ValidateIdentifier(tableName, nameof(tableName));
                connection.Execute($"DELETE FROM [{tableName}] WHERE [Name] = ?", itemName);
            }

            /// <summary>
            /// 删除表中所有行（保留表结构）。
            /// </summary>
            /// <param name="connection">数据库连接。</param>
            /// <param name="tableName">表名。</param>
            public static void DeleteAllData(SQLiteConnection connection, string tableName)
            {
                EnsureConnection(connection);
                ValidateIdentifier(tableName, nameof(tableName));
                connection.Execute($"DELETE FROM [{tableName}]");
            }

            /// <summary>
            /// 查询指定表的全部行数据。
            /// </summary>
            /// <typeparam name="T">行映射类型，必须有无参构造函数。</typeparam>
            /// <param name="connection">数据库连接。</param>
            /// <param name="tableName">表名。</param>
            /// <returns>行对象列表。</returns>
            public static List<T> GetAllData<T>(SQLiteConnection connection, string tableName) where T : new()
            {
                EnsureConnection(connection);
                ValidateIdentifier(tableName, nameof(tableName));
                return connection.Query<T>($"SELECT * FROM [{tableName}]");
            }

            // ---- 自定义 SQL

            /// <summary>
            /// 执行自定义 SQL 查询并返回结果列表。
            /// </summary>
            /// <typeparam name="T">结果行映射类型，必须有无参构造函数。</typeparam>
            /// <param name="connection">数据库连接。</param>
            /// <param name="query">SQL 查询语句。</param>
            /// <param name="parameters">SQL 参数（对应语句中的 ? 占位符）。</param>
            /// <returns>查询结果列表。</returns>
            public static List<T> ExecuteQuery<T>(SQLiteConnection connection, string query, params object[] parameters) where T : new()
            {
                EnsureConnection(connection);
                return connection.Query<T>(query, parameters);
            }

            /// <summary>
            /// 执行自定义 SQL 命令（INSERT / UPDATE / DELETE 等无返回值语句）。
            /// </summary>
            /// <param name="connection">数据库连接。</param>
            /// <param name="command">SQL 命令语句。</param>
            /// <param name="parameters">SQL 参数（对应语句中的 ? 占位符）。</param>
            public static void ExecuteCommand(SQLiteConnection connection, string command, params object[] parameters)
            {
                EnsureConnection(connection);
                connection.Execute(command, parameters);
            }

            /// <summary>
            /// 在独立事务中执行批量操作，成功则 Commit，失败则 Rollback 并重抛异常。
            /// 由于 CreateConnection 维持"始终处于事务"约定，此方法会先提交当前事务，
            /// 再开启新事务执行 action，最终在 finally 中恢复常驻事务状态。
            /// </summary>
            /// <param name="connection">数据库连接。</param>
            /// <param name="action">批量操作委托，参数为当前连接。</param>
            public static void ExecuteTransaction(SQLiteConnection connection, Action<SQLiteConnection> action)
            {
                EnsureConnection(connection);
                if (connection.IsInTransaction)
                {
                    connection.Commit();
                }

                connection.BeginTransaction();
                try
                {
                    action(connection);
                    connection.Commit();
                }
                catch (Exception ex)
                {
                    connection.Rollback();
                    throw new InvalidOperationException("SQLite 事务执行失败。", ex);
                }
                finally
                {
                    connection.BeginTransaction();
                }
            }

            // ---- 私有工具

            /// <summary>
            /// 验证 SQL 标识符（表名/列名）是否合法。
            /// 仅允许字母、数字、下划线，且不能以数字开头，长度 1-128。
            /// </summary>
            /// <param name="identifier">待验证的标识符。</param>
            /// <param name="paramName">参数名称（用于异常信息）。</param>
            /// <returns>验证通过的原始标识符。</returns>
            private static string ValidateIdentifier(string identifier, string paramName)
            {
                if (string.IsNullOrEmpty(identifier))
                {
                    throw new ArgumentException("SQL 标识符不能为空。", paramName);
                }

                if (identifier.Length > 128)
                {
                    throw new ArgumentException($"SQL 标识符长度不能超过 128 字符：{identifier}。", paramName);
                }

                for (int i = 0; i < identifier.Length; i++)
                {
                    char c = identifier[i];
                    if (c != '_' && !char.IsLetterOrDigit(c))
                    {
                        throw new ArgumentException($"SQL 标识符包含非法字符 '{c}'：{identifier}。", paramName);
                    }
                }

                if (char.IsDigit(identifier[0]))
                {
                    throw new ArgumentException($"SQL 标识符不能以数字开头：{identifier}。", paramName);
                }

                return identifier;
            }

            /// <summary>
            /// 验证连接不为 null，否则抛出异常。
            /// </summary>
            /// <param name="connection">待验证的数据库连接。</param>
            private static void EnsureConnection(SQLiteConnection connection)
            {
                if (connection == null)
                {
                    throw new InvalidOperationException("SQLite connection 为 null。");
                }
            }

            /// <summary>
            /// 将 .NET 类型映射为对应的 SQLite 列类型字符串。
            /// </summary>
            /// <param name="type">.NET 类型。</param>
            /// <returns>SQLite 列类型字符串（INTEGER / TEXT / REAL）。</returns>
            private static string GetColumnType(Type type)
            {
                if (type == typeof(int) || type == typeof(bool))
                {
                    return "INTEGER";
                }

                if (type == typeof(string))
                {
                    return "TEXT";
                }

                if (type == typeof(float) || type == typeof(double))
                {
                    return "REAL";
                }

                throw new ArgumentException($"不支持的列类型：{type.FullName}。");
            }
        }
    }
}

#endif
