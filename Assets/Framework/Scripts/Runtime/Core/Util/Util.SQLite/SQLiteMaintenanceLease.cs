using System;
using System.Collections.Generic;
using System.IO;

namespace NovaFramework.Runtime
{
    /// <summary>按数据库路径约束参与 Nova 协议的连接与维护；不代替 SQLite 事务锁或操作系统排他。</summary>
    internal static class SQLiteMaintenanceLease
    {
        private static readonly object Sync = new object();
        private static readonly StringComparer PathComparer = System.IO.Path.DirectorySeparatorChar == '\\' ? StringComparer.OrdinalIgnoreCase : StringComparer.Ordinal;
        private static readonly Dictionary<string, int> Connections = new Dictionary<string, int>(PathComparer);
        private static readonly HashSet<string> Maintenance = new HashSet<string>(PathComparer);

        /// <summary>连接创建前注册；已有维护操作时拒绝，不自动关闭或提交其它连接。</summary>
        /// <param name="databasePath">将打开的数据库路径。</param>
        /// <returns>关闭或创建失败后释放的登记。</returns>
        internal static IDisposable RegisterConnection(string databasePath)
        {
            string path = System.IO.Path.GetFullPath(databasePath);
            lock (Sync)
            {
                if (Maintenance.Contains(path)) throw new InvalidOperationException("SQLite 数据库正在维护。");
                Connections.TryGetValue(path, out int count);
                Connections[path] = count + 1;
                return new Scope(path, false);
            }
        }

        /// <summary>没有已登记连接时取得维护门；未参与协议的外部连接仍由实际事务锁约束。</summary>
        /// <param name="databasePath">唯一数据库路径。</param>
        /// <returns>维护结束后释放的门。</returns>
        internal static IDisposable Acquire(string databasePath)
        {
            string path = System.IO.Path.GetFullPath(databasePath);
            lock (Sync)
            {
                if (Maintenance.Contains(path) || Connections.TryGetValue(path, out int count) && count != 0)
                    throw new InvalidOperationException("SQLite 存在已登记连接或维护操作。");
                Maintenance.Add(path);
                return new Scope(path, true);
            }
        }

        /// <summary>一次性释放本次连接登记或维护门。</summary>
        private sealed class Scope : IDisposable
        {
            private string m_Path;
            private readonly bool m_Maintenance;
            internal Scope(string path, bool maintenance) { m_Path = path; m_Maintenance = maintenance; }
            public void Dispose()
            {
                lock (Sync)
                {
                    if (m_Path == null) return;
                    if (m_Maintenance) Maintenance.Remove(m_Path);
                    else if (Connections.TryGetValue(m_Path, out int count))
                    {
                        if (count <= 1) Connections.Remove(m_Path); else Connections[m_Path] = count - 1;
                    }
                    m_Path = null;
                }
            }
        }
    }
}
