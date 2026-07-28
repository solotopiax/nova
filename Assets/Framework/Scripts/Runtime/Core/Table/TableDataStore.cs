/***************************************************************
 * (c) copyright 2026 - 2030, Solotopia
 * All Rights Reserved.
 * -------------------------------------------------------------
 * filename:  TableDataStore.cs
 * author:    taoye
 * created:   2026/7/27
 * descrip:   Table 原始数据文件内存存储与格式化 Loader
 ***************************************************************/

using System;
using System.Collections.Generic;
using System.Text;
using Luban;
using Newtonsoft.Json.Linq;

namespace NovaFramework.Runtime
{
    /// <summary>
    /// 保存 Catalog 中全部原始表文件，并向各类 Luban 生成代码提供强类型 Loader。
    /// </summary>
    public sealed class TableDataStore
    {
        private readonly Dictionary<string, byte[]> m_Data =
            new Dictionary<string, byte[]>(StringComparer.Ordinal);

        /// <summary>
        /// 添加一张表的原始数据；键必须与 Luban 的 output_data_file 完全一致。
        /// </summary>
        /// <param name="outputDataFile">Luban 输出文件逻辑名，不含扩展名。</param>
        /// <param name="bytes">资源原始字节。</param>
        public void Add(string outputDataFile, byte[] bytes)
        {
            if (string.IsNullOrWhiteSpace(outputDataFile))
            {
                throw new ArgumentException("output_data_file 不能为空。", nameof(outputDataFile));
            }
            if (bytes == null)
            {
                throw new ArgumentNullException(nameof(bytes));
            }

            if (!m_Data.TryAdd(outputDataFile, bytes))
            {
                throw new InvalidOperationException($"Table 原始数据键重复：{outputDataFile}。");
            }
        }

        /// <summary>
        /// 获取原始字节；Protobuf Binary 与 MsgPack 生成代码共用此入口。
        /// </summary>
        /// <param name="outputDataFile">Luban 输出文件逻辑名。</param>
        /// <returns>原始字节。</returns>
        public byte[] GetBytes(string outputDataFile)
        {
            return GetRequired(outputDataFile);
        }

        /// <summary>
        /// 以 UTF-8 文本读取原始数据；Protobuf JSON 使用此入口。
        /// </summary>
        /// <param name="outputDataFile">Luban 输出文件逻辑名。</param>
        /// <returns>UTF-8 文本。</returns>
        public string GetText(string outputDataFile)
        {
            return Encoding.UTF8.GetString(GetRequired(outputDataFile));
        }

        /// <summary>
        /// 把单表 JSON 原始文件解析为 JArray，供 cs-newtonsoft-json 生成代码消费。
        /// </summary>
        /// <param name="outputDataFile">Luban 输出文件逻辑名。</param>
        /// <returns>解析后的 JSON 数组。</returns>
        public JArray GetJson(string outputDataFile)
        {
            return JArray.Parse(GetText(outputDataFile));
        }

        /// <summary>
        /// 把单表 Binary 原始文件包装为独立 ByteBuf，供 cs-bin 生成代码消费。
        /// </summary>
        /// <param name="outputDataFile">Luban 输出文件逻辑名。</param>
        /// <returns>从文件首字节开始读取的 ByteBuf。</returns>
        public ByteBuf GetBinary(string outputDataFile)
        {
            return new ByteBuf(GetRequired(outputDataFile));
        }

        /// <summary>
        /// 将 Luban msgpack 单表原始文件无反射解码为 JArray，供 AOT 安全的生成代码消费。
        /// </summary>
        /// <param name="outputDataFile">Luban 输出文件逻辑名。</param>
        /// <returns>解码后的记录数组。</returns>
        public JArray GetMsgPackJson(string outputDataFile)
        {
            JToken token = LubanMsgPackDecoder.Decode(GetRequired(outputDataFile));
            if (token is not JArray array)
            {
                throw new FormatException($"Luban MsgPack 表 {outputDataFile} 的根节点不是数组。");
            }
            return array;
        }

        /// <summary>
        /// 获取必需数据，缺失时抛出带逻辑键的异常，避免生成空表掩盖资源错误。
        /// </summary>
        /// <param name="outputDataFile">Luban 输出文件逻辑名。</param>
        /// <returns>已加载的原始字节。</returns>
        private byte[] GetRequired(string outputDataFile)
        {
            if (!m_Data.TryGetValue(outputDataFile, out byte[] bytes))
            {
                throw new KeyNotFoundException($"Table 原始数据未加载：{outputDataFile}。");
            }

            return bytes;
        }
    }
}
