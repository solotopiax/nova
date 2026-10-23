/***************************************************************
 * (c) copyright 2026 - 2030, Solotopia
 * All Rights Reserved.
 * -------------------------------------------------------------
 * filename:  AndroidResolveSearchEmptyDatabaseProbe.cs
 * author:    taoye
 * created:   2026/10/16
 * descrip:   Android 刷新中既有空 Search 属性缓存的只读联合快照
 ***************************************************************/
using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using System.Security.Cryptography;
using System.Threading;

namespace NovaFramework.Editor
{
    /// <summary>只观察既有两个固定缓存的联合逻辑物化，绝不初始化、Flush、执行或取消后台任务。</summary>
    internal static class AndroidResolveSearchEmptyDatabaseProbe
    {
        internal enum Result { Captured, Pending, Unsupported }

        /// <summary>保持在同一 Editor 域内的空属性缓存观察，不代表后台 Task 生命周期。</summary>
        internal sealed class Snapshot
        {
            internal object Database, SharedFiles, VolatileStore, MemoryStore, FileStore, StringTable, Lifetime;
            internal string DatabasePath, StringTablePath, DatabaseSha256, StringTableSha256;
            internal int StringTableBytes;
        }

        /// <summary>
        /// 在零等待同步屏障内核对现存空属性缓存；只改变临时锁持有状态，不更改缓存内容或调度。
        /// 调用方先核 Search 已初始化、同域、程序集/MVID、事务写入尾部和实际四数组，不以本函数代替这些条件。
        /// </summary>
        /// <param name="databases">按 propertyDatabase、propertyAliases 顺序给出的两个现存且已核身份的对象。</param>
        /// <param name="sharedFiles">已存在的 PropertyDatabaseLock.s_DatabaseFiles backing dictionary；禁止通过初始化 getter取得。</param>
        /// <param name="projectRoot">本次已确认的项目绝对根路径。</param>
        /// <param name="baselines">按相同顺序的前置快照；首次捕获传 null，后验必须传同域原对象快照。</param>
        /// <param name="snapshots">成功时返回同一组同步屏障内的两个空缓存快照，仅供逻辑后置条件与来源核对。</param>
        /// <param name="reason">不满足条件的固定原因，不包含缓存值、字符串表正文或凭据。</param>
        /// <returns>Captured仅表示本次锁内空状态和精确文件快照成立；Pending应保留partial；不支持结构返回Unsupported。</returns>
        internal static Result TryCapturePair(object[] databases, IDictionary sharedFiles, string projectRoot,
            Snapshot[] baselines, out Snapshot[] snapshots, out string reason)
        {
            snapshots = null;
            reason = null;
            if (databases == null || databases.Length != 2 || databases[0] == null || databases[1] == null ||
                ReferenceEquals(databases[0], databases[1]) || sharedFiles == null || (baselines != null && baselines.Length != 2))
            { reason = "缺少已确认实例或固定空缓存目标。"; return Result.Unsupported; }
            bool entered = false;
            // 恰好两组各三个store锁和一个string锁；预分配，释放时严格按获取逆序。
            var acquired = new List<KeyValuePair<ReaderWriterLockSlim, bool>>(8);
            try
            {
                // Monitor只保护现有实例引用计数；零超时，不Invoke引擎代码，不等待worker。
                entered = Monitor.TryEnter(sharedFiles, 0);
                if (!entered) { reason = "实例共享表正在变化。"; return Result.Pending; }
                var values = new Snapshot[2];
                for (int index = 0; index < 2; index++)
                {
                    object database = databases[index];
                    Snapshot baseline = baselines == null ? null : baselines[index];
                    if (baselines != null && baseline == null) throw new InvalidOperationException();
                    string baseName = index == 0 ? "propertyDatabase.db" : "propertyAliases.db";
                    var value = new Snapshot { Database = database, SharedFiles = sharedFiles,
                        VolatileStore = Field(database, "m_LocalVolatileStore"),
                        MemoryStore = Field(database, "m_LocalStore"), FileStore = Field(database, "m_FileStore"),
                        StringTable = Field(database, "m_StringTable"), Lifetime = Field(database, "m_PropertyDatabaseLock") };
                    if (!(bool)Field(database, "m_Valid") || (bool)Field(database, "<disposed>k__BackingField") ||
                        !(bool)Field(database, "<autoFlush>k__BackingField") || (bool)Field(value.Lifetime, "m_Disposed"))
                    { reason = "空缓存实例状态与前置合同不同。"; return Result.Unsupported; }
                    string relative = "Library/Search/" + baseName;
                    value.DatabasePath = ConfirmPath(projectRoot, (string)Field(database, "<filePath>k__BackingField"), relative);
                    value.StringTablePath = ConfirmPath(projectRoot, (string)Field(database, "<stringTableFilePath>k__BackingField"), relative + ".st");
                    ConfirmPath(projectRoot, (string)Field(value.FileStore, "<filePath>k__BackingField"), relative);
                    ConfirmPath(projectRoot, (string)Field(value.StringTable, "<filePath>k__BackingField"), relative + ".st");
                    string lifetimePath = (string)Field(value.Lifetime, "m_Path");
                    ConfirmPath(projectRoot, lifetimePath, relative);
                    if (!sharedFiles.Contains(lifetimePath) || (int)Field(sharedFiles[lifetimePath], "value") != 1)
                    { reason = "现有View或后台View仍持有共享实例；不能认定物化观察已稳定。"; return Result.Pending; }
                    if (baseline != null && (!ReferenceEquals(value.Database, baseline.Database) ||
                        !ReferenceEquals(value.SharedFiles, baseline.SharedFiles) || !ReferenceEquals(value.VolatileStore, baseline.VolatileStore) ||
                        !ReferenceEquals(value.MemoryStore, baseline.MemoryStore) || !ReferenceEquals(value.FileStore, baseline.FileStore) ||
                        !ReferenceEquals(value.StringTable, baseline.StringTable) || !ReferenceEquals(value.Lifetime, baseline.Lifetime) ||
                        value.DatabasePath != baseline.DatabasePath || value.StringTablePath != baseline.StringTablePath))
                    { reason = "域内实例、底层store或固定路径发生变化。"; return Result.Unsupported; }
                    // 可升级读锁不升级为写锁。它排除MergeStores的另一个可升级读持有者。
                    // volatile在前；memory→file与引擎MergeStores相同；任何一步忙都立即反序释放。
                    foreach (object store in new[] { value.VolatileStore, value.MemoryStore, value.FileStore })
                    {
                        var gate = (ReaderWriterLockSlim)Field(store, "m_Lock");
                        if (!gate.TryEnterUpgradeableReadLock(0))
                        { reason = "store观察屏障正忙。"; return Result.Pending; }
                        acquired.Add(new KeyValuePair<ReaderWriterLockSlim, bool>(gate, true));
                    }
                    var stringLock = (ReaderWriterLockSlim)Field(value.StringTable, "m_Lock");
                    if (!stringLock.TryEnterReadLock(0)) { reason = "字符串表观察屏障正忙。"; return Result.Pending; }
                    acquired.Add(new KeyValuePair<ReaderWriterLockSlim, bool>(stringLock, false));
                    values[index] = value;
                }
                // 两组全部同步屏障持有后才读集合与文件，避免逐库快照之间的TOCTOU。
                for (int index = 0; index < 2; index++)
                {
                    Snapshot value = values[index];
                    Snapshot baseline = baselines == null ? null : baselines[index];
                    string baseName = index == 0 ? "propertyDatabase.db" : "propertyAliases.db";
                    foreach (object store in new[] { value.VolatileStore, value.MemoryStore })
                        if (!Empty(Field(Field(store, "m_Store"), "m_Data")))
                        { reason = "当前含内存属性记录；本合同只处理自然空缓存。"; return baseline == null ? Result.Unsupported : Result.Pending; }
                    foreach (string name in new[] { "m_InvalidatedDocumentKeys", "m_InvalidatedDocumentKeyHiWords", "m_InvalidatedDocumentKeyMasks" })
                        if (!Empty(Field(value.FileStore, name)))
                        { reason = "属性失效尚待物化。"; return Result.Pending; }
                    if (Field(value.FileStore, "m_FileStoreChanged") != null || Field(value.FileStore, "m_FileStoreAboutToChange") != null ||
                        Field(value.StringTable, "m_StringTableChanged") != null)
                    { reason = "仍有活View或未审订阅者。"; return Result.Pending; }
                    string directory = Path.GetDirectoryName(value.DatabasePath);
                    // 本支路要求无遗留临时文件，检查不删除。正式允许足迹仅是base_GUID32_temp。
                    if (Directory.GetFiles(directory, baseName + "_*_temp").Length != 0)
                    { reason = "当前存在未完成或未知派生阶段文件。"; return Result.Pending; }
                    if (new FileInfo(value.DatabasePath).Length != 4)
                    { reason = "非空或不完整属性数据库不在本自然空缓存分支。"; return Result.Unsupported; }
                    byte[] bytes = ReadOnlyBytes(value.DatabasePath, 4);
                    if (bytes.Length != 4 || BitConverter.ToInt32(bytes, 0) != 392647681)
                    { reason = "属性缓存并非已固定版本的header-only状态。"; return Result.Unsupported; }
                    byte[] strings = ReadOnlyBytes(value.StringTablePath, 1024 * 1024);
                    if (strings.Length < 20 || BitConverter.ToInt32(strings, 0) != 1347638274 ||
                        BitConverter.ToInt32(strings, 4) != 0)
                    { reason = "字符串表不在已存在空表分支。"; return Result.Unsupported; }
                    int slots = BitConverter.ToInt32(strings, 8), allocated = BitConverter.ToInt32(strings, 12), used = BitConverter.ToInt32(strings, 16);
                    if (slots < 1 || allocated < 4 || used < 4 || used > allocated || 20L + 4L * slots + allocated != strings.LongLength)
                    { reason = "空字符串表文件结构不完整。"; return Result.Unsupported; }
                    using (var hash = SHA256.Create()) value.DatabaseSha256 = BitConverter.ToString(hash.ComputeHash(bytes)).Replace("-", "").ToLowerInvariant();
                    using (var hash = SHA256.Create()) value.StringTableSha256 = BitConverter.ToString(hash.ComputeHash(strings)).Replace("-", "").ToLowerInvariant();
                    value.StringTableBytes = strings.Length;
                    if (baseline != null && (value.DatabaseSha256 != baseline.DatabaseSha256 ||
                        value.StringTableSha256 != baseline.StringTableSha256 || value.StringTableBytes != baseline.StringTableBytes))
                    { reason = "空缓存文件相对前置快照出现非预期变化。"; return Result.Unsupported; }
                }
                snapshots = values;
                return Result.Captured;
            }
            catch (IOException) { reason = "派生缓存当前不能稳定只读观察。"; return Result.Pending; }
            catch (Exception exception) when (!(exception is OperationCanceledException))
            { reason = "已固定字段/类型或只读观察契约不匹配：" + exception.GetType().Name; return Result.Unsupported; }
            finally
            {
                for (int index = acquired.Count - 1; index >= 0; index--)
                    if (acquired[index].Value) acquired[index].Key.ExitUpgradeableReadLock();
                    else acquired[index].Key.ExitReadLock();
                if (entered) Monitor.Exit(sharedFiles);
            }
        }

        /// <summary>只读取已存在实例的指定backing field，沿继承链定位；不访问属性或静态初始化入口。</summary>
        /// <param name="owner">已存在的实例。</param>
        /// <param name="name">已由固定引擎IL核对的字段名称。</param>
        /// <returns>字段原值；字段或实例不匹配时抛错，调用方明确阻断。</returns>
        private static object Field(object owner, string name)
        {
            if (owner == null) throw new InvalidOperationException();
            for (Type type = owner.GetType(); type != null; type = type.BaseType)
            {
                FieldInfo field = type.GetField(name, BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public | BindingFlags.DeclaredOnly);
                if (field != null) return field.GetValue(owner);
            }
            throw new MissingFieldException();
        }

        /// <summary>在调用方已持相应store屏障时观察已有集合是否为空，不调用Unity getter或修改集合。</summary>
        /// <param name="collection">已固定字段取得的现存List或HashSet。</param>
        /// <returns>首个元素不存在返回true；非集合或异常交由调用方阻断。</returns>
        private static bool Empty(object collection)
        {
            if (!(collection is IEnumerable enumerable)) throw new InvalidOperationException();
            IEnumerator iterator = enumerable.GetEnumerator();
            try { return !iterator.MoveNext(); }
            finally { (iterator as IDisposable)?.Dispose(); }
        }

        /// <summary>把现存backing路径限定为当前项目的唯一Search缓存文件，拒绝相关目录或文件链接。</summary>
        /// <param name="projectRoot">已确认项目根。</param>
        /// <param name="backing">当前引擎对象backing路径。</param>
        /// <param name="relative">此分支唯一允许的项目相对文件。</param>
        /// <returns>精确匹配的绝对路径；不创建目录或文件。</returns>
        private static string ConfirmPath(string projectRoot, string backing, string relative)
        {
            string root = Path.GetFullPath(projectRoot);
            string expected = Path.GetFullPath(Path.Combine(root, relative));
            string actual = Path.GetFullPath(Path.IsPathRooted(backing) ? backing : Path.Combine(root, backing));
            if (!string.Equals(actual, expected, StringComparison.Ordinal)) throw new InvalidOperationException();
            foreach (string path in new[] { Path.Combine(root, "Library"), Path.Combine(root, "Library/Search"), actual })
                if ((File.GetAttributes(path) & FileAttributes.ReparsePoint) != 0) throw new InvalidOperationException();
            if ((File.GetAttributes(actual) & FileAttributes.Directory) != 0) throw new InvalidOperationException();
            return actual;
        }

        /// <summary>有界只读现存文件；不创建、不刷新、不改写，不保证外部非Unity进程的绝对排他。</summary>
        /// <param name="path">已通过固定路径和链接检查的文件。</param>
        /// <param name="maximum">允许读取的最大字节数。</param>
        /// <returns>本次完整字节；增长、短读或超限时拒绝。</returns>
        private static byte[] ReadOnlyBytes(string path, int maximum)
        {
            using (var stream = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.Read))
            {
                if (stream.Length < 0 || stream.Length > maximum) throw new InvalidDataException();
                byte[] bytes = new byte[(int)stream.Length];
                int offset = 0;
                while (offset < bytes.Length)
                {
                    int count = stream.Read(bytes, offset, bytes.Length - offset);
                    if (count == 0) throw new EndOfStreamException();
                    offset += count;
                }
                if (stream.ReadByte() != -1) throw new InvalidDataException();
                return bytes;
            }
        }
    }

}
