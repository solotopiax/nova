/***************************************************************
 * (c) copyright 2026 - 2030, Solotopia
 * All Rights Reserved.
 * -------------------------------------------------------------
 * filename:  FileFragmentItemGroup.cs
 * author:    taoye
 * created:   2026/3/18
 * descrip:   文件片段数据容器（单个 .dat 文件的内存映射）
 ***************************************************************/

using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Security.Cryptography;

namespace NovaFramework.Runtime
{
    /// <summary>
    /// 文件片段数据容器，对应磁盘上一个 .dat 文件。
    /// 内部以 <条目名, 字符串值> 字典存储数据，序列化为二进制格式（可选 AES 加密）。
    /// </summary>
    internal sealed class FileFragmentItemGroup
    {
        private const uint c_FormatMagic = 0x3146464E; // NFF1
        private const int c_FormatVersion = 1;
        private const int c_ChecksumLength = 32;
        private const int c_FormatTrailerLength = sizeof(uint) + sizeof(int) + sizeof(int) + c_ChecksumLength;
        private const string c_BackupSuffix = ".bak";
        private const string c_TemporarySuffix = ".tmp";

        /// <summary>
        /// 条目数据，<条目名, 字符串值>。
        /// </summary>
        private SortedDictionary<string, string> m_Items = new();

        /// <summary>
        /// 获取条目字典（只读访问）。
        /// </summary>
        public IReadOnlyDictionary<string, string> Items => m_Items;

        /// <summary>
        /// 获取条目数量。
        /// </summary>
        public int Count => m_Items.Count;

        // ---- 查询

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

        // ---- 删除

        /// <summary>
        /// 删除指定条目。
        /// </summary>
        /// <param name="item">条目名。</param>
        /// <returns>删除成功返回 true。</returns>
        public bool RemoveItem(string item)
        {
            return m_Items.Remove(item);
        }

        /// <summary>
        /// 删除全部条目。
        /// </summary>
        public void RemoveAll()
        {
            m_Items.Clear();
        }

        // ---- 读取

        /// <summary>
        /// 读取布尔值。
        /// </summary>
        /// <param name="item">条目名。</param>
        /// <param name="defaultValue">不存在时的默认值。</param>
        /// <returns>读取到的值，不存在时返回默认值。</returns>
        public bool GetBool(string item, bool defaultValue = default)
        {
            return m_Items.TryGetValue(item, out var raw) ? raw == "1" : defaultValue;
        }

        /// <summary>
        /// 读取整型值。
        /// </summary>
        /// <param name="item">条目名。</param>
        /// <param name="defaultValue">不存在时的默认值。</param>
        /// <returns>读取到的值，不存在时返回默认值。</returns>
        public int GetInt(string item, int defaultValue = default)
        {
            if (!m_Items.TryGetValue(item, out var raw))
            {
                return defaultValue;
            }

            return int.TryParse(raw, out var result) ? result : defaultValue;
        }

        /// <summary>
        /// 读取浮点值。
        /// </summary>
        /// <param name="item">条目名。</param>
        /// <param name="defaultValue">不存在时的默认值。</param>
        /// <returns>读取到的值，不存在时返回默认值。</returns>
        public float GetFloat(string item, float defaultValue = default)
        {
            if (!m_Items.TryGetValue(item, out var raw))
            {
                return defaultValue;
            }

            return float.TryParse(raw, System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out var result) ? result : defaultValue;
        }

        /// <summary>
        /// 读取字符串值。
        /// </summary>
        /// <param name="item">条目名。</param>
        /// <param name="defaultValue">不存在时的默认值。</param>
        /// <returns>读取到的值，不存在时返回默认值。</returns>
        public string GetString(string item, string defaultValue = "")
        {
            return m_Items.TryGetValue(item, out var raw) ? raw : defaultValue;
        }

        // ---- 写入

        /// <summary>
        /// 写入布尔值。
        /// </summary>
        /// <param name="item">条目名。</param>
        /// <param name="value">要写入的值。</param>
        public void SetBool(string item, bool value)
        {
            m_Items[item] = value ? "1" : "0";
        }

        /// <summary>
        /// 写入整型值。
        /// </summary>
        /// <param name="item">条目名。</param>
        /// <param name="value">要写入的值。</param>
        public void SetInt(string item, int value)
        {
            m_Items[item] = value.ToString();
        }

        /// <summary>
        /// 写入浮点值。
        /// </summary>
        /// <param name="item">条目名。</param>
        /// <param name="value">要写入的值。</param>
        public void SetFloat(string item, float value)
        {
            m_Items[item] = value.ToString(System.Globalization.CultureInfo.InvariantCulture);
        }

        /// <summary>
        /// 写入字符串值。
        /// </summary>
        /// <param name="item">条目名。</param>
        /// <param name="value">要写入的值。</param>
        public void SetString(string item, string value)
        {
            m_Items[item] = value ?? string.Empty;
        }

        // ---- 序列化

        /// <summary>
        /// 将数据写入版本化二进制封套，可选 AES 加密，并通过临时文件与备份完成崩溃安全替换。
        /// 负载格式：[int32 条目数] + N × [length-prefixed string key, length-prefixed string value]。
        /// </summary>
        /// <param name="filePath">目标文件路径。</param>
        /// <param name="useAES">是否启用 AES 加密。</param>
        /// <returns>成功返回 true。</returns>
        public bool Serialize(string filePath, bool useAES)
        {
            return Serialize(filePath, useAES, null, null);
        }

        /// <summary>
        /// 使用可选显式 AES 凭据写入文件，供配套 Editor 工具在非运行态复用同一格式。
        /// </summary>
        internal bool Serialize(string filePath, bool useAES, string aesKey, string aesIV)
        {
            byte[] payload;
            using (var ms = new MemoryStream())
            using (var bw = new BinaryWriter(ms, System.Text.Encoding.UTF8))
            {
                bw.Write(m_Items.Count);
                foreach (var kv in m_Items)
                {
                    bw.Write(kv.Key);
                    bw.Write(kv.Value ?? string.Empty);
                }

                bw.Flush();
                payload = ms.ToArray();
            }

            byte[] bytes = BuildVersionedPayload(payload);
            if (useAES)
            {
                bytes = Util.Encrypt.AES.EncryptBytes(bytes, aesKey, aesIV);
                if (bytes == null || bytes.Length == 0)
                {
                    throw new InvalidOperationException("FileFragment AES 加密失败，拒绝覆盖原存档。");
                }
            }

            WriteCrashSafe(filePath, bytes, useAES, aesKey, aesIV);
            return true;
        }

        /// <summary>
        /// 从文件反序列化数据到内存，兼容旧版裸负载与当前版本化封套。
        /// 数据损坏返回 false 并记录 Warning；文件系统访问错误继续向上抛出。
        /// </summary>
        /// <param name="filePath">源文件路径。</param>
        /// <param name="useAES">是否启用 AES 解密。</param>
        /// <returns>成功返回 true。</returns>
        public bool Deserialize(string filePath, bool useAES)
        {
            return Deserialize(filePath, useAES, null, null);
        }

        /// <summary>
        /// 使用可选显式 AES 凭据读取文件，供配套 Editor 工具在非运行态复用同一格式。
        /// </summary>
        internal bool Deserialize(string filePath, bool useAES, string aesKey, string aesIV)
        {
            byte[] bytes;
            try
            {
                bytes = Util.SysIO.File.ReadAllBytesSync(filePath);
            }
            catch (IOException)
            {
                // 文件系统本身不可用时不能伪装成新存档，由上层决定重试或中止。
                throw;
            }
            catch (UnauthorizedAccessException)
            {
                throw;
            }

            if (bytes == null || bytes.Length == 0)
            {
                return false;
            }

            try
            {
                if (useAES)
                {
                    bytes = Util.Encrypt.AES.DecryptBytes(bytes, aesKey, aesIV);
                    if (bytes == null || bytes.Length == 0)
                    {
                        Log.Warning(LogTag.Persist, "FileFragmentItemGroup.Deserialize AES 解密失败: {0}", filePath);
                        return false;
                    }
                }

                byte[] payload = ReadVersionedPayloadOrLegacy(bytes);
                var items = ReadItems(payload);
                m_Items = items;
                return true;
            }
            catch (Exception ex)
            {
                Log.Warning(LogTag.Persist, "FileFragmentItemGroup.Deserialize 文件损坏: {0}, {1}", filePath, ex.Message);
                return false;
            }
        }

        /// <summary>
        /// 加载正式文件；损坏时尝试备份，均不可用时隔离坏文件并返回空容器。
        /// </summary>
        internal static FileFragmentItemGroup LoadWithRecovery(string filePath, bool useAES)
        {
            return LoadWithRecovery(filePath, useAES, null, null);
        }

        /// <summary>
        /// 使用可选显式 AES 凭据加载并恢复文件，供配套 Editor 工具复用运行时恢复语义。
        /// </summary>
        internal static FileFragmentItemGroup LoadWithRecovery(string filePath, bool useAES, string aesKey, string aesIV)
        {
            var group = new FileFragmentItemGroup();
            bool primaryExists = System.IO.File.Exists(filePath);
            if (primaryExists && group.Deserialize(filePath, useAES, aesKey, aesIV))
            {
                return group;
            }

            string backupPath = GetBackupPath(filePath);
            bool backupExists = System.IO.File.Exists(backupPath);
            if (backupExists)
            {
                var backupGroup = new FileFragmentItemGroup();
                if (backupGroup.Deserialize(backupPath, useAES, aesKey, aesIV))
                {
                    if (primaryExists)
                    {
                        Quarantine(filePath);
                    }

                    // 用已校验的内存数据重建正式文件，同时保留最后一份有效备份。
                    backupGroup.Serialize(filePath, useAES, aesKey, aesIV);
                    Log.Warning(LogTag.Persist, "FileFragment 主存档损坏，已从备份恢复: {0}", filePath);
                    return backupGroup;
                }

                Quarantine(backupPath);
            }

            if (primaryExists)
            {
                Quarantine(filePath);
            }

            if (primaryExists || backupExists)
            {
                Log.Warning(LogTag.Persist, "FileFragment 存档及备份均不可恢复，已隔离坏文件并以空数据运行: {0}", filePath);
            }

            return group;
        }

        /// <summary>
        /// 获取正式文件对应的备份路径。
        /// </summary>
        internal static string GetBackupPath(string filePath)
        {
            return filePath + c_BackupSuffix;
        }

        /// <summary>
        /// 获取正式文件对应的临时文件路径。
        /// </summary>
        internal static string GetTemporaryPath(string filePath)
        {
            return filePath + c_TemporarySuffix;
        }

        private static byte[] BuildVersionedPayload(byte[] payload)
        {
            byte[] checksum;
            using (var sha256 = SHA256.Create())
            {
                checksum = sha256.ComputeHash(payload);
            }

            using (var ms = new MemoryStream())
            using (var bw = new BinaryWriter(ms, System.Text.Encoding.UTF8))
            {
                // 新格式把元数据追加在旧负载之后，使旧版客户端仍能读取前面的条目并忽略尾部。
                bw.Write(payload);
                bw.Write(c_FormatMagic);
                bw.Write(c_FormatVersion);
                bw.Write(payload.Length);
                bw.Write(checksum);
                bw.Flush();
                return ms.ToArray();
            }
        }

        private static byte[] ReadVersionedPayloadOrLegacy(byte[] bytes)
        {
            if (bytes.Length < c_FormatTrailerLength)
            {
                return bytes;
            }

            using (var ms = new MemoryStream(bytes))
            using (var br = new BinaryReader(ms, System.Text.Encoding.UTF8))
            {
                ms.Position = ms.Length - c_FormatTrailerLength;
                uint magic = br.ReadUInt32();
                if (magic != c_FormatMagic)
                {
                    return bytes;
                }

                int version = br.ReadInt32();
                if (version != c_FormatVersion)
                {
                    throw new InvalidDataException($"不支持的 FileFragment 格式版本: {version}");
                }

                int payloadLength = br.ReadInt32();
                long expectedLength = c_FormatTrailerLength + (long)payloadLength;
                if (payloadLength < 0 || expectedLength != ms.Length)
                {
                    throw new InvalidDataException("FileFragment 负载长度无效。");
                }

                byte[] expectedChecksum = br.ReadBytes(c_ChecksumLength);
                if (expectedChecksum.Length != c_ChecksumLength)
                {
                    throw new EndOfStreamException("FileFragment 文件被截断。");
                }

                ms.Position = 0;
                byte[] payload = br.ReadBytes(payloadLength);

                byte[] actualChecksum;
                using (var sha256 = SHA256.Create())
                {
                    actualChecksum = sha256.ComputeHash(payload);
                }

                if (!ChecksumsEqual(expectedChecksum, actualChecksum))
                {
                    throw new InvalidDataException("FileFragment 完整性校验失败。");
                }

                return payload;
            }
        }

        private static SortedDictionary<string, string> ReadItems(byte[] payload)
        {
            using (var ms = new MemoryStream(payload))
            using (var br = new BinaryReader(ms, System.Text.Encoding.UTF8))
            {
                int count = br.ReadInt32();
                long remainingBytes = ms.Length - ms.Position;
                if (count < 0 || count > remainingBytes / 2)
                {
                    throw new InvalidDataException($"FileFragment 条目数无效: {count}");
                }

                var items = new SortedDictionary<string, string>();
                for (int i = 0; i < count; i++)
                {
                    string key = br.ReadString();
                    string value = br.ReadString();
                    items.Add(key, value);
                }

                if (ms.Position != ms.Length)
                {
                    throw new InvalidDataException("FileFragment 存在未识别的尾部数据。");
                }

                return items;
            }
        }

        private static bool ChecksumsEqual(byte[] left, byte[] right)
        {
            if (left == null || right == null || left.Length != right.Length)
            {
                return false;
            }

            int difference = 0;
            for (int i = 0; i < left.Length; i++)
            {
                difference |= left[i] ^ right[i];
            }

            return difference == 0;
        }

        private static void WriteCrashSafe(string filePath, byte[] bytes, bool useAES, string aesKey, string aesIV)
        {
            string directoryPath = System.IO.Path.GetDirectoryName(filePath);
            if (!string.IsNullOrEmpty(directoryPath))
            {
                System.IO.Directory.CreateDirectory(directoryPath);
            }

            string temporaryPath = GetTemporaryPath(filePath);
            string backupPath = GetBackupPath(filePath);
            bool primaryMovedToBackup = false;

            try
            {
                WriteAndFlush(temporaryPath, bytes);

                var verifier = new FileFragmentItemGroup();
                if (!verifier.Deserialize(temporaryPath, useAES, aesKey, aesIV))
                {
                    throw new InvalidDataException("FileFragment 临时文件写入后校验失败。");
                }

                if (System.IO.File.Exists(filePath))
                {
                    if (System.IO.File.Exists(backupPath))
                    {
                        System.IO.File.Delete(backupPath);
                    }

                    System.IO.File.Move(filePath, backupPath);
                    primaryMovedToBackup = true;
                }

                System.IO.File.Move(temporaryPath, filePath);
                Util.SysIO.WebGLSyncFs();
            }
            catch
            {
                if (!System.IO.File.Exists(filePath) && primaryMovedToBackup && System.IO.File.Exists(backupPath))
                {
                    System.IO.File.Move(backupPath, filePath);
                }

                throw;
            }
            finally
            {
                if (System.IO.File.Exists(temporaryPath))
                {
                    System.IO.File.Delete(temporaryPath);
                }
            }
        }

        private static void WriteAndFlush(string filePath, byte[] bytes)
        {
            using (var stream = new FileStream(filePath, FileMode.Create, FileAccess.Write, FileShare.None))
            {
                stream.Write(bytes, 0, bytes.Length);
#if UNITY_WEBGL && !UNITY_EDITOR
                stream.Flush();
#else
                stream.Flush(true);
#endif
            }

            Util.SysIO.WebGLSyncFs();
        }

        private static void Quarantine(string filePath)
        {
            if (!System.IO.File.Exists(filePath))
            {
                return;
            }

            string timestamp = DateTime.UtcNow.ToString("yyyyMMddHHmmssfff", CultureInfo.InvariantCulture);
            string quarantinePath = filePath + ".corrupt." + timestamp;
            int suffix = 0;
            while (System.IO.File.Exists(quarantinePath))
            {
                suffix++;
                quarantinePath = filePath + ".corrupt." + timestamp + "." + suffix.ToString(CultureInfo.InvariantCulture);
            }

            System.IO.File.Move(filePath, quarantinePath);
            Util.SysIO.WebGLSyncFs();
        }
    }
}
