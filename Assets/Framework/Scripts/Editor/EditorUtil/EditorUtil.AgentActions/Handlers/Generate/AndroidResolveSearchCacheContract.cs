/***************************************************************
 * (c) copyright 2026 - 2030, Solotopia
 * All Rights Reserved.
 * -------------------------------------------------------------
 * filename:  AndroidResolveSearchCacheContract.cs
 * author:    taoye
 * created:   2026/10/16
 * descrip:   Android 刷新中既有 Search 空缓存的逻辑物化与事务增量合同
 ***************************************************************/
using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using Path = System.IO.Path;
using System.Linq;
using System.Reflection;
using System.Runtime.CompilerServices;
using System.Threading;
using System.Threading.Tasks;
using NovaFramework.Runtime;
using UnityEditor;
using UnityEngine;

namespace NovaFramework.Editor
{
    /// <summary>只观察既有 Search；允许精确派生缓存维护，绝不把逻辑空状态当作所有后台 Task 退出。</summary>
    internal static class AndroidResolveSearchCacheContract
    {
        /// <summary>可序列化只读证据；不含 vendor 实例、缓存正文或原始设置内容。</summary>
        [Serializable]
        internal sealed class Snapshot
        {
            public string identitiesSha256;
            public string logicalCachesSha256;
            public long transactionLength;
            public string transactionsSha256;
            public string sha256;
        }

        /// <summary>仅由当前域弱表分配对象身份，避免使用会碰撞的 runtime hash 或序列化 vendor 对象。</summary>
        private sealed class Identity { internal readonly string Value = Guid.NewGuid().ToString("N"); }
        private static readonly ConditionalWeakTable<object, Identity> s_Identities = new ConditionalWeakTable<object, Identity>();
        private static Type s_SearchType;
        private static Type s_CoreType;
        private const string c_SearchHash = "0ca16197f4fcc755460371942d380ca092534d5fca9882caf032eb3837cd9b74";
        private const string c_CoreHash = "14a25302095cb3c648b4b83b9936ba2df10454ae254da6440a1a968395f2d581";
        private const long c_MaximumTransactionBytes = 128L * 1024 * 1024;
        internal const int MaximumObservedTransactions = 4096;

        /// <summary>读取当前真实实例和逻辑空状态；首次及最终检查完整 DLL，轮询可复用已核实际模块身份。</summary>
        /// <param name="token">取消只读遍历的令牌，不用于取消引擎任务。</param>
        /// <param name="verifyDiskIdentity">Plan、写前及最终验收必须 true；短轮询只复用同域已核模块。</param>
        /// <param name="snapshot">成功时返回当前逻辑状态与完整事务文件摘要。</param>
        /// <param name="error">未初始化、busy、非空、漂移或未知结构的具体原因。</param>
        /// <returns>当前精确自然支路满足返回 true；不会初始化或执行任何 Search 方法。</returns>
        internal static bool TryCapture(CancellationToken token, bool verifyDiskIdentity, out Snapshot snapshot, out string error)
        {
            snapshot = null;
            error = null;
            try
            {
                if (!VerifyBindings(token, verifyDiskIdentity, out error)) return false;
                Type search = s_SearchType;
                if (!(Static(search, "s_Initialize") is bool initialized) || !initialized ||
                    !(Static(search, "s_ContentRefreshedEnabled") is bool content) || content ||
                    Static(search, "s_ContentRefreshOff") != null || Static(search, "s_ContentRefreshed") != null ||
                    Static(search, "documentsInvalidated") != null)
                { error = "Search 未自然初始化或有内容/失效订阅；当前空缓存支路不接纳未知通知。"; return false; }
                foreach (string name in new[] { "s_UpdatedItems", "s_RemovedItems", "s_MovedItems", "s_DocumentsToInvalidate" })
                    if (!Empty(Static(search, name)))
                    { error = "Search 仍有自然待处理输入：" + name; return false; }
                object delayed = Static(search, "s_DelayedInvalidate");
                if (!DelayerIdle(delayed)) { error = "Search 文档失效仍在正常延迟处理，等待自然物化。"; return false; }
                object manager = Static(search, "s_TransactionManager");
                if (manager == null || manager.GetType() != search.Assembly.GetType("UnityEditor.Search.TransactionManager", false) ||
                    !(Field(manager, "m_Initialized") is bool ready) || !ready || Field(manager, "transactionsAdded") != null)
                { error = "Search 事务管理器身份/初始化或实际订阅不在已审空支路。"; return false; }
                object writer = Field(manager, "m_Writer"), reader = Field(manager, "m_Reader");
                if (writer?.GetType() != search.Assembly.GetType("UnityEditor.Search.TransactionWriter", false) ||
                    reader?.GetType() != search.Assembly.GetType("UnityEditor.Search.TransactionReader", false) ||
                    !(Field(manager, "m_ReaderSynchronizationEvent") is ManualResetEventSlim))
                { error = "Search 事务 reader/writer/backing 同步结构变化。"; return false; }
                Task beforeTask = Field(writer, "m_CurrentTask") as Task;
                if (beforeTask != null && (!beforeTask.IsCompleted || beforeTask.IsFaulted || beforeTask.IsCanceled))
                { error = "Search 事务尾部仍在自然写入或写入失败。"; return false; }
                string transactions = ConfirmPath((string)Field(manager, "m_FilePath"), "Library/Search/transactions.db");
                string file = ConfirmPath((string)Field(writer, "m_FileName"), "Library/Search/transactions.db");
                if (file != transactions) throw new InvalidDataException();
                object[] databases = { Static(search, "propertyDatabase"), Static(search, "propertyAliases") };
                Type lockType = search.Assembly.GetType("UnityEditor.Search.PropertyDatabaseLock", true);
                var shared = Static(lockType, "s_DatabaseFiles") as IDictionary;
                var result = AndroidResolveSearchEmptyDatabaseProbe.TryCapturePair(databases, shared,
                    GenerateActionCommon.ProjectRoot, null, out AndroidResolveSearchEmptyDatabaseProbe.Snapshot[] values, out error);
                if (result != AndroidResolveSearchEmptyDatabaseProbe.Result.Captured) return false;
                foreach (object database in databases)
                    if (!DelayerIdle(Field(database, "m_Debounce")))
                    { error = "Search 属性缓存仍有自然 Delayer 工作。"; return false; }
                var identities = new List<object> { manager, writer, reader, Field(manager, "m_ReaderSynchronizationEvent"), delayed, shared };
                identities.AddRange(values.SelectMany(value => new[] { value.Database, value.VolatileStore, value.MemoryStore, value.FileStore, value.StringTable, value.Lifetime }));
                string identity = GenerateActionCommon.ComputeTextHash(string.Join("\n", identities.Select(IdentityOf)));
                string caches = GenerateActionCommon.ComputeTextHash(Util.Json.Serialize(values.Select(value => new
                { path = value.DatabasePath, database = value.DatabaseSha256, strings = value.StringTableSha256, bytes = value.StringTableBytes }).ToArray()));
                ReadTransactionFile(transactions, token, out long length, out string digest, null, 0, 0, 0);
                if (!ReferenceEquals(beforeTask, Field(writer, "m_CurrentTask")) ||
                    beforeTask != null && (!beforeTask.IsCompleted || beforeTask.IsFaulted || beforeTask.IsCanceled) ||
                    !ReferenceEquals(manager, Static(search, "s_TransactionManager")) || !ReferenceEquals(databases[0], Static(search, "propertyDatabase")) ||
                    !ReferenceEquals(databases[1], Static(search, "propertyAliases")))
                { error = "Search 在只读事务/属性快照期间发生并发漂移。"; return false; }
                snapshot = new Snapshot { identitiesSha256 = identity, logicalCachesSha256 = caches,
                    transactionLength = length, transactionsSha256 = digest };
                snapshot.sha256 = ComputeHash(snapshot);
                return true;
            }
            catch (Exception exception) when (!(exception is OperationCanceledException))
            { error = "Search 逻辑物化只读捕获失败：" + exception.GetType().Name; return false; }
        }

        /// <summary>固定 DTO 摘要；只排除自身，不把写入长度相同当作内容未变。</summary>
        /// <param name="value">需要检查或规范化的当前值；不执行其自定义方法。</param>
        /// <returns>排除自身摘要字段后的固定 DTO SHA。</returns>
        internal static string ComputeHash(Snapshot value)
        {
            return value == null ? null : GenerateActionCommon.ComputeTextHash(Util.Json.Serialize(new
            { value.identitiesSha256, value.logicalCachesSha256, value.transactionLength, value.transactionsSha256 }));
        }

        /// <summary>写前必须完整状态相同；未来正常事务写入不能继承旧计划的输入授权。</summary>
        /// <param name="before">同域执行前冻结的完整状态。</param>
        /// <param name="current">当前重新捕获的完整状态。</param>
        /// <returns>满足上述精确条件时为 true，否则为 false；不会为通过检查而改变环境。</returns>
        internal static bool SameBefore(Snapshot before, Snapshot current)
        {
            return before != null && current != null && !string.IsNullOrEmpty(before.sha256) && before.sha256 == ComputeHash(before) &&
                current.sha256 == ComputeHash(current) && before.sha256 == current.sha256;
        }

        /// <summary>正常派发时记录 Search 同一算法所得的精确有序 state/guid，保持四数组语义，不读取回调原日志。</summary>
        /// <param name="imported">正常 Unity 派发的实际导入数组，保持输入顺序。</param>
        /// <param name="deleted">正常 Unity 派发的实际删除数组，保持输入顺序。</param>
        /// <param name="moved">正常 Unity 派发的实际移动目标数组，与 movedFrom 成对。</param>
        /// <param name="movedFrom">正常 Unity 派发的实际移动来源数组，与 moved 成对。</param>
        /// <returns>按照 Search 同一顺序和状态算法得到的精确事务标记。</returns>
        internal static string[] CaptureExpectedTransactions(string[] imported, string[] deleted, string[] moved, string[] movedFrom)
        {
            IEnumerable<string> updates = imported.Concat(moved).Distinct();
            return updates.Select(path => Expected(path, 1)).Concat(deleted.Select(path => Expected(path, 2)))
                .Concat(movedFrom.Select(path => Expected(path, 4))).ToArray();
        }

        /// <summary>只读核对本次实际派发的事务增量和同域空缓存；允许声明范围内的后续派生空 merge，不承诺 Task 退出。</summary>
        /// <param name="before">同域执行前冻结的完整状态。</param>
        /// <param name="current">当前重新捕获的完整状态。</param>
        /// <param name="expectedTransactions">根据实际四数组观察生成的有序 state/GUID 事务序列。</param>
        /// <param name="startedUtcTicks">本次正常执行观察窗开始的 UTC ticks。</param>
        /// <param name="closedUtcTicks">本次正常执行观察窗结束的 UTC ticks。</param>
        /// <param name="token">本次只读捕获或遍历的取消令牌；不取消厂商或引擎工作。</param>
        /// <param name="error">失败时的精确原因；成功时为 null。</param>
        /// <returns>满足上述精确条件时为 true，否则为 false；不会为通过检查而改变环境。</returns>
        internal static bool TryVerifyAfter(Snapshot before, Snapshot current, string[] expectedTransactions,
            long startedUtcTicks, long closedUtcTicks, CancellationToken token, out string error)
        {
            error = null;
            if (before == null || current == null || before.sha256 != ComputeHash(before) || current.sha256 != ComputeHash(current) ||
                before.identitiesSha256 != current.identitiesSha256 || before.logicalCachesSha256 != current.logicalCachesSha256 ||
                expectedTransactions == null || expectedTransactions.Length > MaximumObservedTransactions || startedUtcTicks <= 0 || closedUtcTicks < startedUtcTicks)
            { error = "Search 同域身份、空缓存、时间窗或本次派发证据不完整。"; return false; }
            try
            {
                string path = ConfirmPath((string)Field(Static(s_SearchType, "s_TransactionManager"), "m_FilePath"), "Library/Search/transactions.db");
                ReadTransactionFile(path, token, out long length, out string prefix, expectedTransactions,
                    before.transactionLength, startedUtcTicks, closedUtcTicks);
                if (length != before.transactionLength + 28L * expectedTransactions.Length || prefix != before.transactionsSha256)
                { error = "Search 事务原前缀或精确本轮增量与已观察派发不符。"; return false; }
                // 再捕获尾部及逻辑状态，拒绝发生在上次快照之后的并发写入。
                if (!TryCapture(token, false, out Snapshot after, out error) || after.sha256 != current.sha256)
                { error = error ?? "Search 后验只读窗口出现新的事务/逻辑变化。"; return false; }
                return true;
            }
            catch (Exception exception) when (!(exception is OperationCanceledException))
            { error = "Search 事务增量只读核对失败：" + exception.GetType().Name; return false; }
        }

        /// <summary>每个精确路径使用引擎同一公开 GUID/Hash128 格式；不加载资产、不调用 Search 私有方法。</summary>
        /// <param name="path">当前检查的精确路径，不授予目录通配权限。</param>
        /// <param name="state">Search 对本路径采用的固定状态 1、2 或 4。</param>
        /// <returns>单个精确 state/GUID 事务标记。</returns>
        private static string Expected(string path, int state)
        {
            return state + "|" + Hash128.Parse(AssetDatabase.AssetPathToGUID(path)).ToString();
        }

        /// <summary>有界完整前缀 SHA 与逐条事务验证；读取不锁引擎 writer，也不 Flush/Wait/写入文件。</summary>
        /// <param name="path">当前检查的精确路径，不授予目录通配权限。</param>
        /// <param name="token">本次只读捕获或遍历的取消令牌；不取消厂商或引擎工作。</param>
        /// <param name="length">实际事务文件的字节长度。</param>
        /// <param name="digest">成功时的稳定 SHA 摘要，不包含原始用户内容。</param>
        /// <param name="expected">需要核对的本次有序增量；null 表示仅捕获完整文件。</param>
        /// <param name="prefixLength">执行前冻结的完整事务文件长度。</param>
        /// <param name="startedUtcTicks">本次正常执行观察窗开始的 UTC ticks。</param>
        /// <param name="closedUtcTicks">本次正常执行观察窗结束的 UTC ticks。</param>
        private static void ReadTransactionFile(string path, CancellationToken token, out long length, out string digest,
            string[] expected, long prefixLength, long startedUtcTicks, long closedUtcTicks)
        {
            using (var stream = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite | FileShare.Delete))
            {
                length = stream.Length;
                if (length < 4 || length > c_MaximumTransactionBytes || (length - 4) % 28 != 0) throw new InvalidDataException();
                long prefix = expected == null ? length : prefixLength;
                if (prefix < 4 || prefix > length || (prefix - 4) % 28 != 0 || expected != null && length != prefix + 28L * expected.Length)
                    throw new InvalidDataException();
                byte[] bytes = new byte[checked((int)prefix)];
                int position = 0;
                while (position < bytes.Length)
                {
                    token.ThrowIfCancellationRequested();
                    int count = stream.Read(bytes, position, Math.Min(65536, bytes.Length - position));
                    if (count <= 0) throw new EndOfStreamException();
                    position += count;
                }
                if (BitConverter.ToInt32(bytes, 0) != 1414332419) throw new InvalidDataException();
                using (var hash = System.Security.Cryptography.SHA256.Create())
                    digest = BitConverter.ToString(hash.ComputeHash(bytes)).Replace("-", "").ToLowerInvariant();
                if (expected != null)
                {
                    using (var reader = new BinaryReader(stream, System.Text.Encoding.UTF8, true))
                    {
                        foreach (string transaction in expected)
                        {
                            token.ThrowIfCancellationRequested();
                            DateTime timestamp = DateTime.FromBinary(reader.ReadInt64());
                            string guid = new Hash128(reader.ReadUInt64(), reader.ReadUInt64()).ToString();
                            int state = reader.ReadInt32();
                            if (timestamp.Kind != DateTimeKind.Utc || timestamp.Ticks < startedUtcTicks || timestamp.Ticks > closedUtcTicks ||
                                state + "|" + guid != transaction) throw new InvalidDataException();
                        }
                    }
                }
                if (stream.Position != length || stream.Length != length) throw new InvalidDataException();
            }
        }

        /// <summary>仅绑定实际已加载的同一引擎模块；不加载新程序集或触发 SearchMonitor 初始化。</summary>
        /// <param name="token">本次只读捕获或遍历的取消令牌；不取消厂商或引擎工作。</param>
        /// <param name="disk">是否重新核对完整磁盘 DLL；短轮询仅复用同域已核模块。</param>
        /// <param name="error">失败时的精确原因；成功时为 null。</param>
        /// <returns>满足上述精确条件时为 true，否则为 false；不会为通过检查而改变环境。</returns>
        private static bool VerifyBindings(CancellationToken token, bool disk, out string error)
        {
            error = null;
            Type search = AppDomain.CurrentDomain.GetAssemblies().Select(assembly => assembly.GetType("UnityEditor.Search.SearchMonitor", false)).Where(type => type != null).Distinct().SingleOrDefault();
            Type core = typeof(EditorApplication);
            if (search == null || search.Module.ModuleVersionId.ToString() != "bf8d264b-6e3a-4341-a376-382a20f39d5a" ||
                core.Module.ModuleVersionId.ToString() != "11f76f7e-1291-4a87-8174-9af3fa5b621a" ||
                !CustomAttributeData.GetCustomAttributes(search).Any(attribute => attribute.AttributeType == typeof(InitializeOnLoadAttribute)) ||
                !disk && (s_SearchType != search || s_CoreType != core))
            { error = "Search/Core 实际模块、自然初始化或已核域身份不符。"; return false; }
            if (disk && (GenerateActionCommon.ComputeFileHash(search.Assembly.Location, token) != c_SearchHash ||
                GenerateActionCommon.ComputeFileHash(core.Assembly.Location, token) != c_CoreHash))
            { error = "Search/Core 完整磁盘 DLL 指纹漂移。"; return false; }
            s_SearchType = search; s_CoreType = core;
            return true;
        }

        /// <summary>只接受固定项目 Library/Search 文件；拒绝链接，不创建目录或文件。</summary>
        /// <param name="backing">现有实例保存的文件路径，不通过 getter 创建文件。</param>
        /// <param name="relative">唯一允许的项目内相对文件路径。</param>
        /// <returns>经过固定路径、项目根与链接校验的绝对路径。</returns>
        private static string ConfirmPath(string backing, string relative)
        {
            string root = Path.GetFullPath(GenerateActionCommon.ProjectRoot);
            string expected = Path.GetFullPath(Path.Combine(root, relative));
            string actual = Path.GetFullPath(Path.IsPathRooted(backing) ? backing : Path.Combine(root, backing));
            if (actual != expected) throw new InvalidDataException();
            foreach (string path in new[] { Path.Combine(root, "Library"), Path.Combine(root, "Library/Search"), actual })
                if ((File.GetAttributes(path) & FileAttributes.ReparsePoint) != 0) throw new InvalidDataException();
            return actual;
        }

        /// <summary>读取已自然初始化的指定静态字段；只用于完整已核模块的明确 owner。</summary>
        /// <param name="owner">字段或单例 backing 的已核声明类型。</param>
        /// <param name="name">固定完整类型名或已审字段名，不接受业务输入作为反射指令。</param>
        /// <returns>固定静态字段的现值，合法 null 原样保留。</returns>
        private static object Static(Type owner, string name)
        {
            FieldInfo field = owner?.GetField(name, BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.DeclaredOnly);
            if (field == null) throw new MissingFieldException(owner?.FullName, name);
            return field.GetValue(null);
        }

        /// <summary>只读取既有实例的字段，沿继承层定位，不访问属性或执行实例方法。</summary>
        /// <param name="owner">字段或单例 backing 的已核声明类型。</param>
        /// <param name="name">固定完整类型名或已审字段名，不接受业务输入作为反射指令。</param>
        /// <returns>固定实例字段的现值，缺字段抛出异常。</returns>
        private static object Field(object owner, string name)
        {
            for (Type type = owner?.GetType(); type != null; type = type.BaseType)
            {
                FieldInfo field = type.GetField(name, BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.DeclaredOnly);
                if (field != null) return field.GetValue(owner);
            }
            throw new MissingFieldException(owner?.GetType().FullName, name);
        }

        /// <summary>只观察已核引擎集合；有任何元素都返回 false，不通过读取清空或消费集合。</summary>
        /// <param name="value">需要检查或规范化的当前值；不执行其自定义方法。</param>
        /// <returns>满足上述精确条件时为 true，否则为 false；不会为通过检查而改变环境。</returns>
        private static bool Empty(object value)
        {
            if (!(value is IEnumerable values)) return false;
            IEnumerator iterator = values.GetEnumerator();
            try { return !iterator.MoveNext(); } finally { (iterator as IDisposable)?.Dispose(); }
        }

        /// <summary>Delayer 空闲仅是逻辑输入谓词，不代表其曾提交的 Task 已全部退出。</summary>
        /// <param name="value">需要检查或规范化的当前值；不执行其自定义方法。</param>
        /// <returns>满足上述精确条件时为 true，否则为 false；不会为通过检查而改变环境。</returns>
        private static bool DelayerIdle(object value)
        {
            return value != null && value.GetType().FullName == "UnityEditor.Delayer" && value.GetType().Assembly == s_CoreType.Assembly &&
                Field(value, "m_DelayInProgress") is bool pending && !pending && Field(value, "m_LastExecutionTime") is long last && last == 0;
        }

        /// <summary>同一域同一对象复用随机身份，不返回或调用其 ToString，弱表不延长 vendor 实例寿命。</summary>
        /// <param name="value">需要检查或规范化的当前值；不执行其自定义方法。</param>
        /// <returns>仅供本域比较的不透明弱身份字符串。</returns>
        private static string IdentityOf(object value)
        {
            if (value == null) throw new InvalidDataException();
            return s_Identities.GetValue(value, key => new Identity()).Value;
        }
    }
}
