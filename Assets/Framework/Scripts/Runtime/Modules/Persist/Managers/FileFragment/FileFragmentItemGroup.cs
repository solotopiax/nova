/***************************************************************
 * (c) copyright 2026 - 2030, Solotopia
 * All Rights Reserved.
 * -------------------------------------------------------------
 * filename:  FileFragmentItemGroup.cs
 * author:    taoye
 * created:   2026/3/18
 * descrip:   文件片段数据容器（单个 .dat 文件的内存映射）
 ***************************************************************/

using System.Collections.Generic;
using System.IO;

namespace NovaFramework.Runtime
{
    /// <summary>
    /// 文件片段数据容器，对应磁盘上一个 .dat 文件。
    /// 内部以 <条目名, 字符串值> 字典存储数据，序列化为二进制格式（可选 AES 加密）。
    /// </summary>
    internal sealed class FileFragmentItemGroup
    {
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
        /// 将数据序列化并写入文件（二进制格式，可选 AES 加密）。
        /// 格式：[int32 条目数] + N × [length-prefixed string key, length-prefixed string value]。
        /// </summary>
        /// <param name="filePath">目标文件路径。</param>
        /// <param name="useAES">是否启用 AES 加密。</param>
        /// <returns>成功返回 true。</returns>
        public bool Serialize(string filePath, bool useAES)
        {
            byte[] bytes;
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
                bytes = ms.ToArray();
            }

            if (useAES)
            {
                bytes = Util.Encrypt.AES.EncryptBytes(bytes);
            }

            Util.SysIO.File.WriteAllBytesSync(filePath, bytes);
            return true;
        }

        /// <summary>
        /// 从文件反序列化数据到内存（二进制格式，可选 AES 解密）。
        /// 防御性处理：AES 解密失败、count 为负、流损坏均返回 false 并记录 Warning。
        /// </summary>
        /// <param name="filePath">源文件路径。</param>
        /// <param name="useAES">是否启用 AES 解密。</param>
        /// <returns>成功返回 true。</returns>
        public bool Deserialize(string filePath, bool useAES)
        {
            var bytes = Util.SysIO.File.ReadAllBytesSync(filePath);
            if (bytes == null || bytes.Length == 0)
            {
                return false;
            }

            if (useAES)
            {
                bytes = Util.Encrypt.AES.DecryptBytes(bytes);
                if (bytes == null || bytes.Length == 0)
                {
                    Log.Warning(LogTag.Persist, "FileFragmentItemGroup.Deserialize AES 解密失败: {0}", filePath);
                    return false;
                }
            }

            try
            {
                using (var ms = new MemoryStream(bytes))
                using (var br = new BinaryReader(ms, System.Text.Encoding.UTF8))
                {
                    var count = br.ReadInt32();
                    if (count < 0)
                    {
                        Log.Warning(LogTag.Persist, "FileFragmentItemGroup.Deserialize 条目数为负: {0}, count={1}", filePath, count);
                        return false;
                    }

                    var items = new SortedDictionary<string, string>();
                    for (int i = 0; i < count; i++)
                    {
                        var key = br.ReadString();
                        var value = br.ReadString();
                        items[key] = value;
                    }

                    m_Items = items;
                }
            }
            catch (System.Exception ex)
            {
                Log.Warning(LogTag.Persist, "FileFragmentItemGroup.Deserialize 文件损坏: {0}, {1}", filePath, ex.Message);
                return false;
            }

            return true;
        }
    }
}
