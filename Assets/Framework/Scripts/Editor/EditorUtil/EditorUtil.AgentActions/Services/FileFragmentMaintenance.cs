/***************************************************************
 * (c) copyright 2026 - 2030, Solotopia
 * All Rights Reserved.
 * -------------------------------------------------------------
 * filename:  FileFragmentMaintenance.cs
 * author:    taoye
 * created:   2026/10/13
 * descrip:   FileFragment 整集冻结、编码复用与可恢复替换
 ***************************************************************/

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using Newtonsoft.Json;
using NovaFramework.Runtime;
using Path = System.IO.Path;

namespace NovaFramework.Editor
{
    /// <summary>只处理现存分片；不调用具有恢复副作用的 LoadWithRecovery，不复制二进制或 AES 算法。</summary>
    internal static class FileFragmentMaintenance
    {
        internal const string LockName = ".nova-persistence-maintenance.lock";
        internal const string IdleLockMarker = "idle-v1";
        private const long MaximumBytes = 64L * 1024 * 1024;

        /// <summary>每个真实文件的整体摘要；没有逐值摘要或玩家内容。</summary>
        [Serializable]
        internal sealed class Entry
        {
            public string name;
            public string beforeSha256;
            public string afterSha256;
            public int itemCount;
            public long byteCount;
        }

        /// <summary>仅驻留内存的原始配置与文件冻结；凭据不能写入计划、收据、日志或恢复日志。</summary>
        internal sealed class Plan
        {
            internal string Root;
            internal bool BeforeAES;
            internal bool AfterAES;
            internal string Key;
            internal string IV;
            internal Entry[] Entries;
            internal string SnapshotSha256;
        }

        /// <summary>严格冻结所有正式文件及已有备份，拒绝孤立备份、临时文件、符号链接和容量超限。</summary>
        internal static Plan Build(string root, bool beforeAES, bool afterAES, string key, string iv, CancellationToken token)
        {
            CheckPath(root);
            if ((beforeAES || afterAES) && (string.IsNullOrEmpty(key) || string.IsNullOrEmpty(iv) ||
                System.Text.Encoding.UTF8.GetByteCount(key) != 16 || System.Text.Encoding.UTF8.GetByteCount(iv) != 16))
                throw new InvalidOperationException("当前隐私 AES 凭据不满足格式。");
            if (!Directory.Exists(root)) throw new InvalidOperationException("分片目录不存在。");
            Entry[] entries = Inventory(root, null, token);
            if (entries.Length == 0) throw new InvalidOperationException("没有现存分片，不把空目录当真实迁移。");
            foreach (Entry entry in entries)
            {
                FileFragmentItemGroup group = Read(Path.Combine(root, entry.name), beforeAES, key, iv);
                entry.itemCount = group.Count;
            }
            var plan = new Plan { Root = Path.GetFullPath(root), BeforeAES = beforeAES, AfterAES = afterAES,
                Key = key, IV = iv, Entries = entries, SnapshotSha256 = Fingerprint(entries, false) };
            AssertBefore(plan, null, token);
            return plan;
        }

        /// <summary>重新核对全体名字、数量、大小和字节摘要；出现新增、删除或变化立即阻断。</summary>
        internal static void AssertBefore(Plan plan, string operationId, CancellationToken token)
        {
            if (Fingerprint(Inventory(plan.Root, operationId, token), false) != plan.SnapshotSha256)
                throw new InvalidOperationException("分片集合已变化。");
        }

        /// <summary>备份原始完整字节，并在源目录同文件系统的隔离子目录转换；每份新文件独立重读比较完整条目。</summary>
        internal static void Prepare(Plan plan, string operationId, string operationDirectory, CancellationToken token)
        {
            AssertBefore(plan, operationId, token);
            string backup = Path.Combine(operationDirectory, "backup");
            string stage = StagePath(plan.Root, operationId);
            if (Directory.Exists(stage) || Directory.Exists(backup)) throw new InvalidOperationException("维护产物已存在。");
            Directory.CreateDirectory(backup);
            Directory.CreateDirectory(stage);
            foreach (Entry entry in plan.Entries)
            {
                token.ThrowIfCancellationRequested();
                string original = Path.Combine(plan.Root, entry.name);
                string saved = Path.Combine(backup, entry.name);
                CopyFlushed(original, saved);
                if (Hash(saved, token) != entry.beforeSha256) throw new InvalidOperationException("备份与冻结源不同。");
                FileFragmentItemGroup source = Read(saved, plan.BeforeAES, plan.Key, plan.IV);
                string prepared = Path.Combine(stage, entry.name);
                source.Serialize(prepared, plan.AfterAES, plan.Key, plan.IV);
                FileFragmentItemGroup reopened = Read(prepared, plan.AfterAES, plan.Key, plan.IV);
                if (source.Count != reopened.Count || source.Items.Any(pair =>
                        !reopened.Items.TryGetValue(pair.Key, out string value) || value != pair.Value))
                    throw new InvalidOperationException("转换后完整条目不一致。");
                entry.afterSha256 = Hash(prepared, token);
            }
            AssertBefore(plan, operationId, token);
        }

        /// <summary>只在恢复日志已持久化后调用；每次替换前核对该文件仍为原字节，使用同文件系统原子替换。</summary>
        internal static void Apply(Plan plan, string operationId, CancellationToken token)
        {
            AssertBefore(plan, operationId, token);
            foreach (Entry entry in plan.Entries)
            {
                token.ThrowIfCancellationRequested();
                string target = Path.Combine(plan.Root, entry.name);
                string stage = Path.Combine(StagePath(plan.Root, operationId), entry.name);
                if (Hash(target, token) != entry.beforeSha256 || Hash(stage, token) != entry.afterSha256)
                    throw new InvalidOperationException("提交前源或阶段文件漂移。");
                File.Replace(stage, target, null);
            }
            AssertAfter(plan.Root, plan.Entries, operationId, token);
        }

        /// <summary>只接受全集原字节或本操作新字节；默认拒未知restore，已绑定原token的领域恢复才可隔离半写证据。</summary>
        internal static void Restore(string root, Entry[] entries, string operationId, string operationDirectory, CancellationToken token, bool authorizedRecovery = false)
        {
            AssertRecoverable(root, entries, operationId, operationDirectory, token);
            string stage = StagePath(root, operationId);
            Directory.CreateDirectory(stage);
            foreach (Entry entry in entries)
            {
                token.ThrowIfCancellationRequested();
                string target = Path.Combine(root, entry.name);
                string prepared = Path.Combine(stage, entry.name + ".restore");
                CheckPath(prepared);
                if (File.Exists(prepared))
                {
                    if (Hash(prepared, token) != entry.beforeSha256)
                    {
                        // 原token领域恢复已核全部备份和当前原/新内容，隔离半写证据而不覆盖或删除未知字节。
                        if (!authorizedRecovery) throw new InvalidOperationException("已有恢复阶段文件不是原字节，保留现场。");
                        string preserved = Path.Combine(operationDirectory, "restore-interrupted-" + Guid.NewGuid().ToString("N") + ".bin");
                        CheckPath(preserved); File.Move(prepared, preserved);
                    }
                    else File.Delete(prepared);
                }
                if (Hash(target, token) == entry.beforeSha256) continue;
                CopyFlushed(Path.Combine(operationDirectory, "backup", entry.name), prepared);
                string current = Hash(target, token);
                if (current != entry.beforeSha256 && current != entry.afterSha256)
                    throw new InvalidOperationException("恢复前数据发生第三方变化。");
                File.Replace(prepared, target, null);
            }
            if (Fingerprint(Inventory(root, operationId, token), false) != Fingerprint(entries, false))
                throw new InvalidOperationException("全集恢复未通过原字节验证。");
        }

        /// <summary>校验恢复名字、源集合与所有备份；空、重复、越界路径或第三方字节均拒绝。</summary>
        internal static void AssertRecoverable(string root, Entry[] entries, string operationId, string operationDirectory, CancellationToken token)
        {
            ValidateEntries(entries);
            Entry[] current = Inventory(root, operationId, token);
            if (!current.Select(entry => entry.name).SequenceEqual(entries.Select(entry => entry.name)))
                throw new InvalidOperationException("恢复集合发生新增或删除。");
            foreach (Entry entry in entries)
            {
                string original = Path.Combine(operationDirectory, "backup", entry.name);
                CheckPath(original);
                string now = current.First(item => item.name == entry.name).beforeSha256;
                if ((now != entry.beforeSha256 && now != entry.afterSha256) || Hash(original, token) != entry.beforeSha256)
                    throw new InvalidOperationException("数据或原始备份漂移，拒绝覆盖。");
            }
        }

        /// <summary>原Operation在崩溃前未保存新摘要时，以同一codec比较当前新格式与原备份的完整条目，拒绝伪造新摘要覆盖第三方值。</summary>
        internal static void AssertRecoveryContent(string root, Entry[] entries, string operationDirectory,
            bool beforeAES, bool afterAES, string key, string iv, CancellationToken token)
        {
            foreach (Entry entry in entries)
            {
                token.ThrowIfCancellationRequested();
                string current = Path.Combine(root, entry.name);
                if (Hash(current, token) == entry.beforeSha256) continue;
                var original = Read(Path.Combine(operationDirectory, "backup", entry.name), beforeAES, key, iv);
                var observed = Read(current, afterAES, key, iv);
                if (original.Count != observed.Count || original.Items.Any(pair =>
                        !observed.Items.TryGetValue(pair.Key, out string value) || pair.Value != value))
                    throw new InvalidOperationException("当前新格式包含非原操作值，拒绝恢复覆盖。");
            }
        }

        /// <summary>核对全部替换后的字节及集合；这里只读，不以 Manager 缓存读回替代文件证据。</summary>
        internal static void AssertAfter(string root, Entry[] entries, string operationId, CancellationToken token)
        {
            ValidateEntries(entries);
            if (Fingerprint(Inventory(root, operationId, token), false) != Fingerprint(entries, true))
                throw new InvalidOperationException("转换后文件集合不匹配。");
        }

        /// <summary>清理本操作同目录阶段目录；原始备份及恢复日志是明确保留的恢复产物。</summary>
        internal static void CleanStage(string root, string operationId, Entry[] entries)
        {
            string stage = StagePath(root, operationId);
            CheckPath(stage);
            if (Directory.Exists(stage)) foreach (string path in Directory.GetFileSystemEntries(stage))
            {
                CheckPath(path);
                string name = Path.GetFileName(path);
                var entry = entries?.SingleOrDefault(item => item != null && (item.name == name || item.name + ".restore" == name));
                string expected = entry == null ? null : name.EndsWith(".restore", StringComparison.Ordinal) ? entry.beforeSha256 : entry.afterSha256;
                if (Directory.Exists(path) || !ValidHash(expected) || Hash(path, CancellationToken.None) != expected)
                    throw new InvalidOperationException("阶段目录含非本操作普通文件或未知字节，拒绝删除。");
            }
            if (Directory.Exists(stage)) Directory.Delete(stage, true);
        }

        /// <summary>阶段路径由规范操作标识构造，不接受任意路径。</summary>
        internal static string StagePath(string root, string operationId)
        {
            if (!Guid.TryParseExact(operationId, "N", out _)) throw new InvalidOperationException("操作标识无效。");
            return Path.Combine(root, ".nova-maintenance-" + operationId);
        }

        /// <summary>扫描固定顶层分片；有未解决维护或异常文件时阻断，不隐式隔离、删除或恢复坏档。</summary>
        private static Entry[] Inventory(string root, string operationId, CancellationToken token)
        {
            CheckPath(root);
            string allowedStage = operationId == null ? null : StagePath(root, operationId);
            foreach (string directory in Directory.GetDirectories(root))
            {
                CheckPath(directory);
                if (Path.GetFileName(directory).StartsWith(".nova-maintenance-", StringComparison.Ordinal) && directory != allowedStage)
                    throw new InvalidOperationException("存在未解决维护阶段目录，请先恢复该操作。");
            }
            var entries = new List<Entry>();
            long bytes = 0;
            foreach (string path in Directory.GetFiles(root).OrderBy(Path.GetFileName, StringComparer.Ordinal))
            {
                token.ThrowIfCancellationRequested();
                CheckPath(path);
                string name = Path.GetFileName(path);
                if (name == LockName)
                {
                    string marker = ReadLockMarker(path);
                    if (marker == IdleLockMarker || operationId != null && marker == operationId) continue;
                }
                if (!ValidName(name)) throw new InvalidOperationException("目录含临时、隔离或非分片文件，先明确处理现状。");
                long length = new FileInfo(path).Length;
                if (length <= 0 || length > 16L * 1024 * 1024 || (bytes += length) > MaximumBytes || entries.Count >= 128)
                    throw new InvalidOperationException("分片为空或超出受控维护容量。");
                entries.Add(new Entry { name = name, beforeSha256 = Hash(path, token), byteCount = length });
            }
            foreach (Entry entry in entries.Where(entry => entry.name.EndsWith(".bak", StringComparison.Ordinal)))
                if (!entries.Any(primary => primary.name == entry.name.Substring(0, entry.name.Length - 4)))
                    throw new InvalidOperationException("仅有备份缺少正式分片，不在加密切换中暗中恢复。");
            return entries.ToArray();
        }

        /// <summary>只读锁标记允许既有维护写句柄并存；写者仍以FileShare.Read独占，避免Windows共享模式阻断自检。</summary>
        internal static string ReadLockMarker(string path)
        {
            CheckPath(path);
            using var stream = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite);
            if (stream.Length > 64) throw new InvalidOperationException("维护锁标记过大。");
            using var reader = new StreamReader(stream, System.Text.Encoding.UTF8);
            return reader.ReadToEnd();
        }

        /// <summary>名称必须是真实单层 dat 或 dat.bak，拒绝保留名、分隔符和穿越路径。</summary>
        private static bool ValidName(string name) => name != null && name.Length <= 260 && name == Path.GetFileName(name) &&
            name.IndexOfAny(new[] { '/', '\\', '\0', ':' }) < 0 && !name.StartsWith(".", StringComparison.Ordinal) &&
            (name.EndsWith(".dat", StringComparison.Ordinal) || name.EndsWith(".dat.bak", StringComparison.Ordinal));

        /// <summary>收据全体条目须规范、有序、唯一并有完整原/新摘要，防止恢复日志成为任意文件写入器。</summary>
        internal static void ValidateEntries(Entry[] entries)
        {
            if (entries == null || entries.Length == 0 || entries.Length > 128 ||
                entries.Any(entry => entry == null || !ValidName(entry.name) || !ValidHash(entry.beforeSha256) || !ValidHash(entry.afterSha256) ||
                    entry.itemCount < 0 || entry.byteCount <= 0 || entry.byteCount > 16L * 1024 * 1024) ||
                entries.Sum(entry => entry.byteCount) > MaximumBytes ||
                entries.Any(entry => entry.name.EndsWith(".bak", StringComparison.Ordinal) &&
                    !entries.Any(primary => primary.name == entry.name.Substring(0, entry.name.Length - 4))) ||
                !entries.Select(entry => entry.name).SequenceEqual(entries.Select(entry => entry.name).Distinct().OrderBy(name => name, StringComparer.Ordinal)))
                throw new InvalidOperationException("维护条目收据无效。");
        }

        /// <summary>严格读取二进制/完整性/AES；false 或异常均阻断，不以空容器继续迁移。</summary>
        private static FileFragmentItemGroup Read(string path, bool aes, string key, string iv)
        {
            var group = new FileFragmentItemGroup();
            if (!group.Deserialize(path, aes, key, iv)) throw new InvalidOperationException("分片严格解码失败。");
            return group;
        }

        /// <summary>整体集合摘要只使用规范文件名和整体文件摘要，不保留玩家值。</summary>
        internal static string Fingerprint(Entry[] entries, bool after) => GenerateActionCommon.ComputeTextHash(
            string.Join("\n", entries.Select(entry => entry.name + ":" + (after ? entry.afterSha256 : entry.beforeSha256))));

        /// <summary>沿全路径拒绝符号链接；不存在的末端由受控目录创建，现存祖先仍必须安全。</summary>
        internal static void CheckPath(string path)
        {
            string current = Path.GetFullPath(path);
            while (!string.IsNullOrEmpty(current))
            {
                if ((File.Exists(current) || Directory.Exists(current)) && (File.GetAttributes(current) & FileAttributes.ReparsePoint) != 0)
                    throw new InvalidOperationException("维护路径不能含符号链接。");
                string parent = Path.GetDirectoryName(current);
                if (parent == current) break;
                current = parent;
            }
        }

        /// <summary>完整摘要复用已有工具，读取期间遵守取消令牌。</summary>
        internal static string Hash(string path, CancellationToken token) => GenerateActionCommon.ComputeFileHash(path, token);

        /// <summary>整体摘要必须恰好为规范 SHA-256 字符串。</summary>
        internal static bool ValidHash(string hash) => hash != null && hash.Length == 64 && hash.All(Uri.IsHexDigit);

        /// <summary>使用独占新文件复制全部原字节并刷新；拒绝覆盖已有备份或阶段内容。</summary>
        private static void CopyFlushed(string source, string destination)
        {
            CheckPath(source); CheckPath(destination); bool created = false;
            try
            {
                using var input = new FileStream(source, FileMode.Open, FileAccess.Read, FileShare.Read);
                using var output = new FileStream(destination, FileMode.CreateNew, FileAccess.Write, FileShare.None);
                created = true;
#if UNITY_INCLUDE_TESTS
                if (s_RestoreCopyFixtureRoot != null && destination.StartsWith(s_RestoreCopyFixtureRoot + Path.DirectorySeparatorChar, StringComparison.Ordinal) && destination.EndsWith(".restore", StringComparison.Ordinal))
                {
                    int first = input.ReadByte(); if (first >= 0) output.WriteByte((byte)first);
                    s_RestoreCopyFixtureFault?.Invoke();
                }
#endif
                input.CopyTo(output); output.Flush(true);
            }
            catch { if (created && File.Exists(destination)) File.Delete(destination); throw; }
        }
#if UNITY_INCLUDE_TESTS
        private static string s_RestoreCopyFixtureRoot;
        private static Action s_RestoreCopyFixtureFault;
        /// <summary>仅已有测试独立根可模拟真实restore复制半写，不允许生产目录或任意公开故障输入。</summary>
        internal static IDisposable UseRestoreCopyFixture(string root, Action fault)
        {
            CheckPath(root);
            if (s_RestoreCopyFixtureRoot != null || !Path.GetFileName(root).StartsWith("nova-file-maintenance-", StringComparison.Ordinal))
                throw new InvalidOperationException("restore测试范围无效。");
            s_RestoreCopyFixtureRoot = Path.GetFullPath(root); s_RestoreCopyFixtureFault = fault; return new RestoreCopyFixtureScope();
        }
        private sealed class RestoreCopyFixtureScope : IDisposable
        {
            /// <summary>结束独立复制故障范围，清静态回调，不删除当前或其它测试数据。</summary>
            public void Dispose() { s_RestoreCopyFixtureRoot = null; s_RestoreCopyFixtureFault = null; }
        }
#endif
    }
}
