/***************************************************************
 * (c) copyright 2026 - 2030, Solotopia
 * All Rights Reserved.
 * -------------------------------------------------------------
 * filename:  LubanMsgPackDecoder.cs
 * author:    taoye
 * created:   2026/7/27
 * descrip:   Unity IL2CPP 兼容的 Luban MsgPack 原始数据解码器
 ***************************************************************/

using System;
using System.Text;
using Newtonsoft.Json.Linq;

namespace NovaFramework.Runtime
{
    /// <summary>
    /// 将 Luban msgpack DataTarget 产物无反射地解码为 JToken，供统一 Table 生成 API 使用。
    /// </summary>
    internal static class LubanMsgPackDecoder
    {
        /// <summary>
        /// 解码一份完整 MsgPack 文档，并拒绝尾随字节。
        /// </summary>
        /// <param name="bytes">Luban msgpack 单表原始字节。</param>
        /// <returns>解码后的 JSON Token。</returns>
        internal static JToken Decode(byte[] bytes)
        {
            if (bytes == null)
            {
                throw new ArgumentNullException(nameof(bytes));
            }

            var reader = new Reader(bytes);
            JToken token = reader.ReadValue(0);
            if (!reader.End)
            {
                throw new FormatException("Luban MsgPack 文档包含尾随字节。");
            }
            return token;
        }

        /// <summary>
        /// 只持有当前字节偏移的栈上 Reader，避免反射和动态代码以兼容 IL2CPP/AOT。
        /// </summary>
        private ref struct Reader
        {
            private const int c_MaxDepth = 128;
            private readonly ReadOnlySpan<byte> m_Bytes;
            private int m_Offset;

            /// <summary>
            /// 创建从首字节开始读取的 Reader。
            /// </summary>
            /// <param name="bytes">完整 MsgPack 文档。</param>
            internal Reader(byte[] bytes)
            {
                m_Bytes = bytes;
                m_Offset = 0;
            }

            internal bool End => m_Offset == m_Bytes.Length;

            /// <summary>
            /// 按 MessagePack 标记读取一个值，覆盖 Luban DataTarget 会写出的全部基础与集合类型。
            /// </summary>
            /// <param name="depth">当前集合递归深度。</param>
            /// <returns>对应的 JSON Token。</returns>
            internal JToken ReadValue(int depth)
            {
                if (depth > c_MaxDepth)
                {
                    throw new FormatException("Luban MsgPack 嵌套深度超过限制。");
                }

                byte code = ReadByte();
                if (code <= 0x7F)
                {
                    return new JValue((int)code);
                }
                if (code >= 0xE0)
                {
                    return new JValue(unchecked((sbyte)code));
                }
                if ((code & 0xF0) == 0x80)
                {
                    return ReadMap(code & 0x0F, depth + 1);
                }
                if ((code & 0xF0) == 0x90)
                {
                    return ReadArray(code & 0x0F, depth + 1);
                }
                if ((code & 0xE0) == 0xA0)
                {
                    return new JValue(ReadString(code & 0x1F));
                }

                switch (code)
                {
                    case 0xC0: return JValue.CreateNull();
                    case 0xC2: return new JValue(false);
                    case 0xC3: return new JValue(true);
                    case 0xC4: return new JValue(Convert.ToBase64String(ReadBytes(ReadByte())));
                    case 0xC5: return new JValue(Convert.ToBase64String(ReadBytes(ReadUInt16())));
                    case 0xC6: return new JValue(Convert.ToBase64String(ReadBytes(CheckedLength(ReadUInt32()))));
                    case 0xCA: return new JValue(BitConverter.Int32BitsToSingle(unchecked((int)ReadUInt32())));
                    case 0xCB: return new JValue(BitConverter.Int64BitsToDouble(unchecked((long)ReadUInt64())));
                    case 0xCC: return new JValue((int)ReadByte());
                    case 0xCD: return new JValue((int)ReadUInt16());
                    case 0xCE: return new JValue(ReadUInt32());
                    case 0xCF: return new JValue(ReadUInt64());
                    case 0xD0: return new JValue(unchecked((sbyte)ReadByte()));
                    case 0xD1: return new JValue(unchecked((short)ReadUInt16()));
                    case 0xD2: return new JValue(unchecked((int)ReadUInt32()));
                    case 0xD3: return new JValue(unchecked((long)ReadUInt64()));
                    case 0xD9: return new JValue(ReadString(ReadByte()));
                    case 0xDA: return new JValue(ReadString(ReadUInt16()));
                    case 0xDB: return new JValue(ReadString(CheckedLength(ReadUInt32())));
                    case 0xDC: return ReadArray(ReadUInt16(), depth + 1);
                    case 0xDD: return ReadArray(CheckedLength(ReadUInt32()), depth + 1);
                    case 0xDE: return ReadMap(ReadUInt16(), depth + 1);
                    case 0xDF: return ReadMap(CheckedLength(ReadUInt32()), depth + 1);
                    default:
                        throw new FormatException($"Luban MsgPack 包含不支持的标记：0x{code:X2}。");
                }
            }

            /// <summary>
            /// 读取固定数量的数组元素。
            /// </summary>
            /// <param name="count">元素数量。</param>
            /// <param name="depth">子元素深度。</param>
            /// <returns>JSON 数组。</returns>
            private JArray ReadArray(int count, int depth)
            {
                var array = new JArray();
                for (int i = 0; i < count; i++)
                {
                    array.Add(ReadValue(depth));
                }
                return array;
            }

            /// <summary>
            /// 读取固定数量的映射条目；非字符串键按其不变文本表示写入 JObject。
            /// </summary>
            /// <param name="count">键值对数量。</param>
            /// <param name="depth">子元素深度。</param>
            /// <returns>JSON 对象。</returns>
            private JObject ReadMap(int count, int depth)
            {
                var map = new JObject();
                for (int i = 0; i < count; i++)
                {
                    JToken key = ReadValue(depth);
                    string name = key.Type == JTokenType.String ? (string)key : key.ToString();
                    map.Add(name, ReadValue(depth));
                }
                return map;
            }

            /// <summary>
            /// 读取 UTF-8 字符串。
            /// </summary>
            /// <param name="length">字节长度。</param>
            /// <returns>解码后的字符串。</returns>
            private string ReadString(int length)
            {
                byte[] bytes = ReadBytes(length);
                return Encoding.UTF8.GetString(bytes);
            }

            /// <summary>
            /// 读取并复制指定长度字节，同时执行边界检查。
            /// </summary>
            /// <param name="length">读取长度。</param>
            /// <returns>独立字节数组。</returns>
            private byte[] ReadBytes(int length)
            {
                EnsureAvailable(length);
                byte[] result = m_Bytes.Slice(m_Offset, length).ToArray();
                m_Offset += length;
                return result;
            }

            /// <summary>
            /// 读取一个字节并执行边界检查。
            /// </summary>
            /// <returns>当前字节。</returns>
            private byte ReadByte()
            {
                EnsureAvailable(1);
                return m_Bytes[m_Offset++];
            }

            /// <summary>
            /// 按网络字节序读取 16 位无符号整数。
            /// </summary>
            /// <returns>读取值。</returns>
            private ushort ReadUInt16()
            {
                return (ushort)((ReadByte() << 8) | ReadByte());
            }

            /// <summary>
            /// 按网络字节序读取 32 位无符号整数。
            /// </summary>
            /// <returns>读取值。</returns>
            private uint ReadUInt32()
            {
                return ((uint)ReadByte() << 24) | ((uint)ReadByte() << 16) |
                       ((uint)ReadByte() << 8) | ReadByte();
            }

            /// <summary>
            /// 按网络字节序读取 64 位无符号整数。
            /// </summary>
            /// <returns>读取值。</returns>
            private ulong ReadUInt64()
            {
                return ((ulong)ReadUInt32() << 32) | ReadUInt32();
            }

            /// <summary>
            /// 把 32 位集合长度安全转换为 CLR 数组长度。
            /// </summary>
            /// <param name="length">MessagePack 无符号长度。</param>
            /// <returns>非负 Int32 长度。</returns>
            private static int CheckedLength(uint length)
            {
                if (length > int.MaxValue)
                {
                    throw new FormatException("Luban MsgPack 集合长度超过运行时限制。");
                }
                return (int)length;
            }

            /// <summary>
            /// 校验剩余字节足够完成当前读取。
            /// </summary>
            /// <param name="length">需要读取的字节数。</param>
            private void EnsureAvailable(int length)
            {
                if (length < 0 || m_Offset > m_Bytes.Length - length)
                {
                    throw new FormatException("Luban MsgPack 数据被截断。");
                }
            }
        }
    }
}
