/***************************************************************
 * (c) copyright 2026 - 2030, Solotopia
 * All Rights Reserved.
 * -------------------------------------------------------------
 * filename:  SQLiteManager.cs
 * author:    taoye
 * created:   2026/3/18
 * descrip:   SQLite 持久化管理器
 ***************************************************************/

using System.Collections.Generic;
using Cysharp.Threading.Tasks;

namespace NovaFramework.Runtime
{
    /// <summary>
    /// SQLite 持久化管理器。
    /// 每个 classify 对应一张 SQLite 表，每行存储 Name/Value 键值对。
    /// IO 优化：写缓冲（SetXxx 只写内存）+ 单次事务批量提交（Save 时统一提交）+ WAL 模式 + 懒加载表数据。
    /// 需要 SqlCipher4Unity3D 插件（package：com.github-glitchenzo.sqlcipher4unity3d）。
    /// WebGL 平台不可用：Initialize 时输出警告，所有操作返回默认值。
    /// </summary>
    internal sealed partial class SQLiteManager : PersistManagerBase<SQLiteManagerConfig>, ISQLiteManager
    {
        /// <summary>
        /// 初始化 SQLiteManager 的新实例。
        /// </summary>
        public SQLiteManager() { }

        /// <summary>
        /// 初始化，建立数据库连接并启用 WAL 模式。
        /// </summary>
        /// <param name="config">配置信息。</param>
        public override UniTask Initialize(SQLiteManagerConfig config)
        {
#if UNITY_WEBGL
            Log.Warning(LogTag.Persist, "SQLiteManager 在 WebGL 平台不可用，所有操作将被忽略。");
#else
            InitializeBase(config);
            m_CipherPassword = config.CipherPassword;
            OpenConnection();
            Load();
#endif
            return UniTask.CompletedTask;
        }

        /// <summary>
        /// 管理器轮询：驱动自动保存计时器。
        /// </summary>
        public override void Update()
        {
#if !UNITY_WEBGL
            TickAutoSave(UnityEngine.Time.unscaledDeltaTime);
#endif
        }

        /// <summary>
        /// 关闭并清理管理器：强制提交所有未保存数据，关闭连接。
        /// </summary>
        public override void Shutdown()
        {
#if !UNITY_WEBGL
            try
            {
                Save();
            }
            catch (System.Exception ex)
            {
                Log.Error(LogTag.Persist, "SQLiteManager.Shutdown Save failed: {0}", ex.Message);
            }
            finally
            {
                try
                {
                    m_Connection?.Close();
                    m_Connection?.Dispose();
                }
                catch (System.Exception ex)
                {
                    Log.Error(LogTag.Persist, "SQLiteManager.Shutdown connection close failed: {0}", ex.Message);
                }

                m_Connection = null;
                m_Tables.Clear();
                m_DirtyTables.Clear();
            }
#endif
        }

        /// <summary>
        /// 懒加载模式：Load 仅确保连接就绪，表数据在首次访问时按需加载。
        /// </summary>
        /// <returns>成功返回 true。</returns>
        public override UniTask<bool> Load()
        {
#if UNITY_WEBGL
            return UniTask.FromResult(false);
#else
            return UniTask.FromResult(m_Connection != null);
#endif
        }

        /// <summary>
        /// 通过单次事务将所有脏表的写缓冲批量提交到数据库。
        /// 逐表 try-catch：成功的从脏集合移除，失败的保留。
        /// </summary>
        /// <returns>全部成功返回 true，任一失败返回 false。</returns>
        public override bool Save()
        {
#if UNITY_WEBGL
            return false;
#else
            bool allSuccess = true;
            var saved = new List<string>();

            foreach (var classify in m_DirtyTables)
            {
                if (m_Tables.TryGetValue(classify, out var table))
                {
                    try
                    {
                        table.FlushToDb(m_Connection, m_UseAESEncrypt);
                        saved.Add(classify);
                    }
                    catch (System.Exception ex)
                    {
                        Log.Error(LogTag.Persist, "SQLiteManager.Save table [{0}] failed: {1}", classify, ex.Message);
                        allSuccess = false;
                    }
                }
            }

            foreach (var classify in saved)
            {
                m_DirtyTables.Remove(classify);
            }

            return allSuccess;
#endif
        }

        /// <summary>
        /// 将指定分类（表）的写缓冲提交到数据库。
        /// </summary>
        /// <param name="classify">分类名（表名）。</param>
        /// <returns>成功返回 true。</returns>
        public override bool Save(string classify)
        {
#if UNITY_WEBGL
            return false;
#else
            ValidateSQLiteClassify(classify);
            if (!m_Tables.TryGetValue(classify, out var table))
            {
                return false;
            }

            table.FlushToDb(m_Connection, m_UseAESEncrypt);
            m_DirtyTables.Remove(classify);
            return true;
#endif
        }

        /// <summary>
        /// 判断指定条目是否存在（首次访问时懒加载表数据）。
        /// </summary>
        /// <param name="classify">分类名。</param>
        /// <param name="item">条目名。</param>
        /// <returns>存在返回 true。</returns>
        public override bool HasItem(string classify, string item)
        {
#if UNITY_WEBGL
            return false;
#else
            ValidateSQLiteClassify(classify);
            ValidateClassifyAndItem(classify, item);
            return GetOrCreateTable(classify).HasItem(item);
#endif
        }

        /// <summary>
        /// 删除指定条目（首次访问时懒加载，写入删除缓冲）。
        /// </summary>
        /// <param name="classify">分类名。</param>
        /// <param name="item">条目名。</param>
        /// <returns>删除成功返回 true。</returns>
        public override bool RemoveItem(string classify, string item)
        {
#if UNITY_WEBGL
            return false;
#else
            ValidateSQLiteClassify(classify);
            ValidateClassifyAndItem(classify, item);
            var removed = GetOrCreateTable(classify).RemoveItem(item);
            if (removed)
            {
                MarkDirty(classify);
            }

            return removed;
#endif
        }

        /// <summary>
        /// 删除指定分类下的全部条目并删除对应表。
        /// </summary>
        /// <param name="classify">分类名。</param>
        public override void RemoveAll(string classify)
        {
#if !UNITY_WEBGL
            ValidateSQLiteClassify(classify);
            if (m_Tables.TryGetValue(classify, out var table))
            {
                table.RemoveAll();
                table.ClearPendingBuffers();
                m_Connection.Execute($"DROP TABLE IF EXISTS [{classify}]");
            }

            m_Tables.Remove(classify);
            m_DirtyTables.Remove(classify);
#endif
        }

        /// <summary>
        /// 获取指定分类下的全部条目名数组。
        /// </summary>
        /// <param name="classify">分类名。</param>
        /// <returns>条目名数组，不存在时返回空数组。</returns>
        public override string[] GetAllItemNames(string classify)
        {
#if UNITY_WEBGL
            return System.Array.Empty<string>();
#else
            ValidateSQLiteClassify(classify);
            return GetOrCreateTable(classify).GetAllItemNames();
#endif
        }

        /// <summary>
        /// 将指定分类下的全部条目名填充到列表。
        /// </summary>
        /// <param name="classify">分类名。</param>
        /// <param name="results">结果列表，方法会追加而非清空。</param>
        public override void GetAllItemNames(string classify, List<string> results)
        {
#if !UNITY_WEBGL
            ValidateSQLiteClassify(classify);
            GetOrCreateTable(classify).GetAllItemNames(results);
#endif
        }

        /// <summary>
        /// 获取指定分类下的条目数量。
        /// </summary>
        /// <param name="classify">分类名。</param>
        /// <returns>条目数量。</returns>
        public override int Count(string classify)
        {
#if UNITY_WEBGL
            return 0;
#else
            ValidateSQLiteClassify(classify);
            return GetOrCreateTable(classify).Count;
#endif
        }

        /// <summary>
        /// 读取布尔值。
        /// </summary>
        /// <param name="classify">分类名。</param>
        /// <param name="item">条目名。</param>
        /// <param name="defaultValue">不存在时的默认值。</param>
        /// <returns>读取到的值，不存在时返回默认值。</returns>
        public override bool GetBool(string classify, string item, bool defaultValue = default)
        {
#if UNITY_WEBGL
            return defaultValue;
#else
            var raw = ReadRaw(classify, item, null);
            if (raw == null)
            {
                return defaultValue;
            }

            return raw == "1";
#endif
        }

        /// <summary>
        /// 读取整型值。
        /// </summary>
        /// <param name="classify">分类名。</param>
        /// <param name="item">条目名。</param>
        /// <param name="defaultValue">不存在时的默认值。</param>
        /// <returns>读取到的值，不存在时返回默认值。</returns>
        public override int GetInt(string classify, string item, int defaultValue = default)
        {
#if UNITY_WEBGL
            return defaultValue;
#else
            var raw = ReadRaw(classify, item, null);
            if (raw == null)
            {
                return defaultValue;
            }

            return int.TryParse(raw, out var result) ? result : defaultValue;
#endif
        }

        /// <summary>
        /// 读取浮点值。
        /// </summary>
        /// <param name="classify">分类名。</param>
        /// <param name="item">条目名。</param>
        /// <param name="defaultValue">不存在时的默认值。</param>
        /// <returns>读取到的值，不存在时返回默认值。</returns>
        public override float GetFloat(string classify, string item, float defaultValue = default)
        {
#if UNITY_WEBGL
            return defaultValue;
#else
            var raw = ReadRaw(classify, item, null);
            if (raw == null)
            {
                return defaultValue;
            }

            return float.TryParse(raw, System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out var result) ? result : defaultValue;
#endif
        }

        /// <summary>
        /// 读取字符串值。
        /// </summary>
        /// <param name="classify">分类名。</param>
        /// <param name="item">条目名。</param>
        /// <param name="defaultValue">不存在时的默认值。</param>
        /// <returns>读取到的值，不存在时返回默认值。</returns>
        public override string GetString(string classify, string item, string defaultValue = "")
        {
#if UNITY_WEBGL
            return defaultValue;
#else
            return ReadRaw(classify, item, defaultValue);
#endif
        }

        /// <summary>
        /// 写入布尔值（写入写缓冲，不立即提交数据库）。
        /// </summary>
        /// <param name="classify">分类名。</param>
        /// <param name="item">条目名。</param>
        /// <param name="value">要写入的值。</param>
        public override void SetBool(string classify, string item, bool value)
        {
#if !UNITY_WEBGL
            WriteRaw(classify, item, value ? "1" : "0");
#endif
        }

        /// <summary>
        /// 写入整型值（写入写缓冲，不立即提交数据库）。
        /// </summary>
        /// <param name="classify">分类名。</param>
        /// <param name="item">条目名。</param>
        /// <param name="value">要写入的值。</param>
        public override void SetInt(string classify, string item, int value)
        {
#if !UNITY_WEBGL
            WriteRaw(classify, item, value.ToString());
#endif
        }

        /// <summary>
        /// 写入浮点值（写入写缓冲，不立即提交数据库）。
        /// </summary>
        /// <param name="classify">分类名。</param>
        /// <param name="item">条目名。</param>
        /// <param name="value">要写入的值。</param>
        public override void SetFloat(string classify, string item, float value)
        {
#if !UNITY_WEBGL
            WriteRaw(classify, item, value.ToString(System.Globalization.CultureInfo.InvariantCulture));
#endif
        }

        /// <summary>
        /// 写入字符串值（写入写缓冲，不立即提交数据库）。
        /// </summary>
        /// <param name="classify">分类名。</param>
        /// <param name="item">条目名。</param>
        /// <param name="value">要写入的值。</param>
        public override void SetString(string classify, string item, string value)
        {
#if !UNITY_WEBGL
            WriteRaw(classify, item, value ?? string.Empty);
#endif
        }

        /// <summary>
        /// 读取长整型值。
        /// </summary>
        /// <param name="classify">分类名。</param>
        /// <param name="item">条目名。</param>
        /// <param name="defaultValue">不存在时的默认值。</param>
        /// <returns>读取到的值，不存在时返回默认值。</returns>
        public override long GetLong(string classify, string item, long defaultValue = default)
        {
#if UNITY_WEBGL
            return defaultValue;
#else
            var raw = ReadRaw(classify, item, null);
            return raw != null && long.TryParse(raw, out var result) ? result : defaultValue;
#endif
        }

        /// <summary>
        /// 写入长整型值。
        /// </summary>
        /// <param name="classify">分类名。</param>
        /// <param name="item">条目名。</param>
        /// <param name="value">要写入的值。</param>
        public override void SetLong(string classify, string item, long value)
        {
#if !UNITY_WEBGL
            WriteRaw(classify, item, value.ToString());
#endif
        }

        /// <summary>
        /// 读取双精度浮点值。
        /// </summary>
        /// <param name="classify">分类名。</param>
        /// <param name="item">条目名。</param>
        /// <param name="defaultValue">不存在时的默认值。</param>
        /// <returns>读取到的值，不存在时返回默认值。</returns>
        public override double GetDouble(string classify, string item, double defaultValue = default)
        {
#if UNITY_WEBGL
            return defaultValue;
#else
            var raw = ReadRaw(classify, item, null);
            return raw != null && double.TryParse(raw, System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out var result) ? result : defaultValue;
#endif
        }

        /// <summary>
        /// 写入双精度浮点值。
        /// </summary>
        /// <param name="classify">分类名。</param>
        /// <param name="item">条目名。</param>
        /// <param name="value">要写入的值。</param>
        public override void SetDouble(string classify, string item, double value)
        {
#if !UNITY_WEBGL
            WriteRaw(classify, item, value.ToString(System.Globalization.CultureInfo.InvariantCulture));
#endif
        }

        /// <summary>
        /// 读取字节数组（Base64 解码）。
        /// </summary>
        /// <param name="classify">分类名。</param>
        /// <param name="item">条目名。</param>
        /// <param name="defaultValue">不存在时的默认值。</param>
        /// <returns>读取到的值，不存在时返回默认值。</returns>
        public override byte[] GetBytes(string classify, string item, byte[] defaultValue = null)
        {
#if UNITY_WEBGL
            return defaultValue;
#else
            var raw = ReadRaw(classify, item, null);
            if (raw == null)
            {
                return defaultValue;
            }

            try
            {
                return System.Convert.FromBase64String(raw);
            }
            catch (System.Exception ex)
            {
                Log.Warning(LogTag.Persist, "GetBytes 反序列化失败 [{0}::{1}]: {2}", classify, item, ex.Message);
                return defaultValue;
            }
#endif
        }

        /// <summary>
        /// 写入字节数组（Base64 编码存储为 TEXT）。
        /// </summary>
        /// <param name="classify">分类名。</param>
        /// <param name="item">条目名。</param>
        /// <param name="value">要写入的值。</param>
        public override void SetBytes(string classify, string item, byte[] value)
        {
#if !UNITY_WEBGL
            WriteRaw(classify, item, value != null ? System.Convert.ToBase64String(value) : string.Empty);
#endif
        }

        /// <summary>
        /// 读取对象（JSON 反序列化）。
        /// </summary>
        /// <typeparam name="T">对象类型。</typeparam>
        /// <param name="classify">分类名。</param>
        /// <param name="item">条目名。</param>
        /// <param name="defaultValue">不存在时的默认值。</param>
        /// <returns>读取到的值，不存在时返回默认值。</returns>
        public override T GetObject<T>(string classify, string item, T defaultValue = default)
        {
#if UNITY_WEBGL
            return defaultValue;
#else
            var raw = ReadRaw(classify, item, null);
            if (raw == null)
            {
                return defaultValue;
            }

            try
            {
                return Util.Json.Deserialize<T>(raw);
            }
            catch (System.Exception ex)
            {
                Log.Warning(LogTag.Persist, "GetObject 反序列化失败 [{0}::{1}]: {2}", classify, item, ex.Message);
                return defaultValue;
            }
#endif
        }

        /// <summary>
        /// 写入对象（JSON 序列化存储为 TEXT）。
        /// </summary>
        /// <typeparam name="T">对象类型。</typeparam>
        /// <param name="classify">分类名。</param>
        /// <param name="item">条目名。</param>
        /// <param name="value">要写入的值。</param>
        public override void SetObject<T>(string classify, string item, T value)
        {
#if !UNITY_WEBGL
            WriteRaw(classify, item, Util.Json.Serialize(value));
#endif
        }

#if !UNITY_WEBGL
        /// <summary>
        /// 获取或创建指定分类的 Table 实例，并确保数据已加载。
        /// </summary>
        /// <param name="classify">分类名。</param>
        /// <returns>对应的 Table 实例。</returns>
        private Table GetOrCreateTable(string classify)
        {
            if (m_Connection == null)
            {
                throw new System.InvalidOperationException("SQLiteManager: connection is not open. Cannot access table.");
            }

            if (!m_Tables.TryGetValue(classify, out var table))
            {
                table = new Table(classify);
                m_Tables[classify] = table;
            }

            table.EnsureLoaded(m_Connection, m_UseAESEncrypt);
            return table;
        }
#endif

        /// <summary>
        /// 获取数据库中所有分类（表）的名称，供 Inspector 运行时枚举使用。
        /// </summary>
        /// <returns>分类名数组，连接未就绪时返回空数组。</returns>
        public override string[] GetAllClassifyNames()
        {
#if UNITY_WEBGL
            return System.Array.Empty<string>();
#else
            if (m_Connection == null)
            {
                return System.Array.Empty<string>();
            }

            var names = Util.SQLite.GetTableNames(m_Connection);
            return names?.ToArray() ?? System.Array.Empty<string>();
#endif
        }

#if !UNITY_WEBGL
        /// <summary>
        /// 打开 SQLite 连接并初始化 WAL 日志模式。
        /// WAL 模式允许读写并发，大幅提升写入性能。
        /// </summary>
        private void OpenConnection()
        {
            Util.SysIO.Directory.CreateIfNotExist(Path.Persist.SQLite.FolderFullPath);

            try
            {
                m_Connection = new SqlCipher4Unity3D.SQLiteConnection(
                    Path.Persist.SQLite.FileFullPath,
                    string.IsNullOrEmpty(m_CipherPassword) ? null : m_CipherPassword
                );

                m_Connection.ExecuteScalar<string>("PRAGMA journal_mode=WAL");
            }
            catch (System.Exception ex)
            {
                m_Connection?.Dispose();
                m_Connection = null;
                Log.Error(LogTag.Persist, "SQLiteManager.OpenConnection failed: {0}", ex.Message);
            }
        }
#endif
    }
}
