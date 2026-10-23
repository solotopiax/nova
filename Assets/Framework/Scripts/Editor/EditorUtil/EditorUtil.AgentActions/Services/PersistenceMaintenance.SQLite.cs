using System;
using System.IO;
using System.Linq;
using System.Threading;
using Newtonsoft.Json;
using NovaFramework.Runtime;
using SqlCipher4Unity3D;

namespace NovaFramework.Editor
{
    internal static partial class PersistenceMaintenance
    {
        /// <summary>sqlite_master 固定元数据映射；不允许未知表、触发器或自定义索引进入值维护。</summary>
        internal sealed class SchemaRow { public string Type { get; set; } public string Name { get; set; } public string Sql { get; set; } }
        /// <summary>固定列元数据映射。</summary>
        internal sealed class ColumnRow { public string Name { get; set; } public string Type { get; set; } public int Pk { get; set; } }
        /// <summary>标准 Name/Value 两列映射。</summary>
        internal sealed class DatabaseRow { public string Name { get; set; } public string Value { get; set; } }

        /// <summary>精确验证已有标准 schema 与全体 TEXT 行；不存在库、未知结构、NULL 或坏行阻断。</summary>
        internal static ValueEntry[] ReadSQLiteValues(SQLiteConnection connection, CancellationToken token)
        {
            if (connection.ExecuteScalar<string>("PRAGMA quick_check") != "ok") throw new InvalidOperationException("数据库完整性检查未通过。");
            var schema = connection.Query<SchemaRow>("SELECT type AS Type,name AS Name,sql AS Sql FROM sqlite_master ORDER BY name");
            if (schema.Any(row => row.Type != "table" && !(row.Type == "index" && row.Name.StartsWith("sqlite_autoindex_", StringComparison.Ordinal) && row.Sql == null)))
                throw new InvalidOperationException("数据库存在非标准表、索引或触发器。");
            var values = new System.Collections.Generic.List<ValueEntry>();
            long bytes = 0;
            foreach (SchemaRow table in schema.Where(row => row.Type == "table"))
            {
                token.ThrowIfCancellationRequested();
                if (string.IsNullOrEmpty(table.Name) || table.Name.StartsWith("sqlite_", StringComparison.Ordinal) ||
                    table.Name.Length > 128 || table.Name.Any(value => value != '_' && !char.IsLetterOrDigit(value)) || char.IsDigit(table.Name[0]))
                    throw new InvalidOperationException("表名不满足标准存储格式。");
                string normalized = new string((table.Sql ?? string.Empty).Where(value => !char.IsWhiteSpace(value) && value != '[' && value != ']' && value != '"' && value != '`').ToArray()).ToUpperInvariant();
                string expected = "CREATETABLE" + table.Name.ToUpperInvariant() + "(NAMETEXTPRIMARYKEY,VALUETEXT)";
                if (normalized != expected && normalized != expected.Replace("CREATETABLE", "CREATETABLEIFNOTEXISTS"))
                    throw new InvalidOperationException("数据库不是标准两列无额外约束表。");
                string name = QuoteIdentifier(table.Name);
                var columns = connection.Query<ColumnRow>("PRAGMA table_info(" + name + ")");
                if (columns.Count != 2 || columns[0].Name != "Name" || columns[0].Type.ToUpperInvariant() != "TEXT" || columns[0].Pk != 1 ||
                    columns[1].Name != "Value" || columns[1].Type.ToUpperInvariant() != "TEXT" || columns[1].Pk != 0 ||
                    connection.Query<ColumnRow>("PRAGMA table_xinfo(" + name + ")").Count != 2 ||
                    connection.ExecuteScalar<long>("SELECT count(*) FROM pragma_foreign_key_list(?)", table.Name) != 0 ||
                    connection.ExecuteScalar<long>("SELECT count(*) FROM " + name + " WHERE typeof(Name) != 'text' OR typeof(Value) != 'text'") != 0)
                    throw new InvalidOperationException("数据库 schema 或值类型不是标准 Name/Value TEXT。");
                // 空表的结构记录也计入同一容量，避免只有数据行触发上界检查。
                bytes += System.Text.Encoding.UTF8.GetByteCount(table.Name) + System.Text.Encoding.UTF8.GetByteCount(table.Sql ?? string.Empty);
                if (bytes > MaximumSQLiteBytes || values.Count >= 16384) throw new InvalidOperationException("数据库值超出维护容量。");
                values.Add(new ValueEntry { classify = table.Name, name = "__nova_schema__", index = true, value = table.Sql ?? string.Empty });
                var rows = connection.Query<DatabaseRow>("SELECT Name,Value FROM " + name + " ORDER BY Name COLLATE BINARY");
                foreach (DatabaseRow row in rows)
                {
                    token.ThrowIfCancellationRequested();
                    if (string.IsNullOrEmpty(row.Name) || row.Name.IndexOf('\0') >= 0 || row.Value == null) throw new InvalidOperationException("标准条目身份无效。");
                    bytes += System.Text.Encoding.UTF8.GetByteCount(table.Name) + System.Text.Encoding.UTF8.GetByteCount(row.Name) + System.Text.Encoding.UTF8.GetByteCount(row.Value);
                    if (bytes > MaximumSQLiteBytes || values.Count >= 16384) throw new InvalidOperationException("数据库值超出维护容量。");
                    values.Add(new ValueEntry { classify = table.Name, name = row.Name, value = row.Value });
                }
            }
            return values.ToArray();
        }

        /// <summary>标识符始终双引号转义；不接受用户 SQL。</summary>
        private static string QuoteIdentifier(string value) => "\"" + value.Replace("\"", "\"\"") + "\"";

        /// <summary>通过原厂连接字符串打开既有库，禁止 Create；密码只来自已冻结组件。</summary>
        private static SQLiteConnection OpenMaintenanceDatabase(string path, string password) =>
            new SQLiteConnection(new SQLiteConnectionString(path, SQLiteOpenFlags.ReadWrite | SQLiteOpenFlags.FullMutex, true,
                string.IsNullOrEmpty(password) ? null : password));

        /// <summary>Plan 先核四文件变更探测，再真实写事务冻结逻辑值；回滚后记录伴随文件新摘要。</summary>
        private static void BuildSQLiteValues(ValuePlan plan, string sourceHash, CancellationToken token)
        {
            string path = AesMaintenanceSqlitePath;
            SQLiteFileMetadata metadata = InspectSQLiteFiles(path, token);
            if (metadata.metadataSha256 != sourceHash || !metadata.files[0].present || metadata.files[0].byteCount == 0 || metadata.unexpectedEntryCount != 0)
                throw new InvalidOperationException("数据库不存在、空文件、未知文件或检查摘要变化。");
            ReadMetadataTarget(new Request { targetGuid = plan.Journal.origin.target.targetGuid,
                globalObjectId = plan.Journal.origin.target.globalObjectId }, out _);
            var component = UnityEditor.GlobalObjectId.GlobalObjectIdentifierToObjectSlow(
                ParseGlobalObjectId(plan.Journal.origin.target.globalObjectId)) as PersistComponent;
            using (var serialized = new UnityEditor.SerializedObject(component))
                plan.Password = serialized.FindProperty("m_SQLiteCipherPassword").stringValue;
            using (SQLiteMaintenanceLease.Acquire(path))
            using (var connection = OpenMaintenanceDatabase(path, plan.Password))
            {
                connection.Execute("BEGIN IMMEDIATE");
                try { plan.Before = ReadSQLiteValues(connection, token); }
                finally { connection.Execute("ROLLBACK"); }
            }
            plan.Journal.origin.sourceRoot = System.IO.Path.GetFullPath(path);
            plan.Journal.logicalBeforeSha256 = ValueHash(plan.Before);
            plan.Journal.metadataSha256 = InspectSQLiteFiles(path, token).metadataSha256;
        }

        /// <summary>读取已校验的精确 Unity 组件身份。</summary>
        private static UnityEditor.GlobalObjectId ParseGlobalObjectId(string value)
        {
            if (!UnityEditor.GlobalObjectId.TryParse(value, out UnityEditor.GlobalObjectId id)) throw new InvalidOperationException("组件身份无效。");
            return id;
        }

        /// <summary>独占新建目标后由原厂sqlcipher_export在真实事务生成加密一致副本；源连接保持禁止Create，不复制活库文件。</summary>
        internal static void BackupSQLite(SQLiteConnection source, string path, string password)
        {
            if (File.Exists(path)) throw new InvalidOperationException("备份路径已存在。");
            if (string.IsNullOrEmpty(password)) throw new InvalidOperationException("一致备份必须加密。");
            FileFragmentMaintenance.CheckPath(path);
            // ATTACH沿用源连接的禁止Create标记；先以CreateNew独占本轮空目标，不给源库增加创建权限。
            using (var target = new FileStream(path, FileMode.CreateNew, FileAccess.Write, FileShare.None)) target.Flush(true);
            bool attached = false; bool active = false;
            try
            {
                source.Execute("ATTACH DATABASE ? AS nova_maintenance_backup KEY ?", path, password); attached = true;
                source.Execute("BEGIN IMMEDIATE"); active = true;
                source.ExecuteScalar<string>("SELECT sqlcipher_export('nova_maintenance_backup')");
                source.Execute("COMMIT"); active = false;
            }
            finally
            {
                try { if (active) source.Execute("ROLLBACK"); }
                finally { if (attached) source.Execute("DETACH DATABASE nova_maintenance_backup"); }
            }
        }

        /// <summary>真实事务内逐行更新相同身份；参数绑定值，任一失败整体回滚，不替换主库。</summary>
        internal static void ApplySQLiteValues(SQLiteConnection connection, ValueEntry[] values)
        {
            foreach (ValueEntry value in values)
            {
                if (value.index) continue;
                if (connection.Execute("UPDATE " + QuoteIdentifier(value.classify) + " SET Value=? WHERE Name=?", value.value, value.name) != 1)
                    throw new InvalidOperationException("数据库行身份发生变化。");
            }
        }
    }
}
