/***************************************************************
 * (c) copyright 2026 - 2030, Solotopia
 * All Rights Reserved.
 * -------------------------------------------------------------
 * filename:  Util.Convert.Bytes.cs
 * author:    taoye
 * created:   2026/1/27
 * descrip:   类型转换工具 —— 基础类型字节序列化与反序列化
 ***************************************************************/

using System;
using System.Text;

namespace NovaFramework.Runtime
{
    public static partial class Util
    {
        public static partial class Convert
        {
            /// <summary>
            /// 将指定的布尔值转换成字节数组。
            /// </summary>
            /// <param name="value">要转换的布尔值。</param>
            /// <returns>字节数组。</returns>
            public static byte[] ToBytes(bool value)
            {
                byte[] buffer = new byte[1];
                ToBytes(value, buffer, 0);
                return buffer;
            }

            /// <summary>
            /// 将指定的布尔值转换成字节数组。
            /// </summary>
            /// <param name="value">要转换的布尔值。</param>
            /// <param name="buffer">用于存放结果的字节数组。</param>
            public static void ToBytes(bool value, byte[] buffer)
            {
                ToBytes(value, buffer, 0);
            }

            /// <summary>
            /// 将指定的布尔值转换成字节数组。
            /// </summary>
            /// <param name="value">要转换的布尔值。</param>
            /// <param name="buffer">用于存放结果的字节数组。</param>
            /// <param name="startIndex">buffer 内的起始位置。</param>
            public static void ToBytes(bool value, byte[] buffer, int startIndex)
            {
                if (buffer == null)
                {
                    throw new ArgumentNullException(nameof(buffer), "Buffer 无效。");
                }

                if (startIndex < 0 || startIndex + 1 > buffer.Length)
                {
                    throw new ArgumentOutOfRangeException(nameof(startIndex), "startIndex 无效。");
                }

                buffer[startIndex] = value ? (byte)1 : (byte)0;
            }

            /// <summary>
            /// 将指定的 Unicode 字符值转换成字节数组。
            /// </summary>
            /// <param name="value">要转换的字符。</param>
            /// <returns>字节数组。</returns>
            public static byte[] ToBytes(char value)
            {
                byte[] buffer = new byte[2];
                ToBytes((short)value, buffer, 0);
                return buffer;
            }

            /// <summary>
            /// 将指定的 Unicode 字符值转换成字节数组。
            /// </summary>
            /// <param name="value">要转换的字符。</param>
            /// <param name="buffer">用于存放结果的字节数组。</param>
            public static void ToBytes(char value, byte[] buffer)
            {
                ToBytes((short)value, buffer, 0);
            }

            /// <summary>
            /// 将指定的 Unicode 字符值转换成字节数组。
            /// </summary>
            /// <param name="value">要转换的字符。</param>
            /// <param name="buffer">用于存放结果的字节数组。</param>
            /// <param name="startIndex">buffer 内的起始位置。</param>
            public static void ToBytes(char value, byte[] buffer, int startIndex)
            {
                ToBytes((short)value, buffer, startIndex);
            }

            /// <summary>
            /// 将指定的 16 位有符号整数值转换成字节数组。
            /// </summary>
            /// <param name="value">要转换的数字。</param>
            /// <returns>字节数组。</returns>
            public static byte[] ToBytes(short value)
            {
                byte[] buffer = new byte[2];
                ToBytes(value, buffer, 0);
                return buffer;
            }

            /// <summary>
            /// 将指定的 16 位有符号整数值转换成字节数组。
            /// </summary>
            /// <param name="value">要转换的数字。</param>
            /// <param name="buffer">用于存放结果的字节数组。</param>
            public static void ToBytes(short value, byte[] buffer)
            {
                ToBytes(value, buffer, 0);
            }

            /// <summary>
            /// 将指定的 16 位有符号整数值转换成字节数组。
            /// </summary>
            /// <param name="value">要转换的数字。</param>
            /// <param name="buffer">用于存放结果的字节数组。</param>
            /// <param name="startIndex">buffer 内的起始位置。</param>
            public static unsafe void ToBytes(short value, byte[] buffer, int startIndex)
            {
                if (buffer == null)
                {
                    throw new ArgumentNullException(nameof(buffer), "Buffer 无效。");
                }

                if (startIndex < 0 || startIndex + 2 > buffer.Length)
                {
                    throw new ArgumentOutOfRangeException(nameof(startIndex), "startIndex 无效。");
                }

                fixed (byte* valueRef = buffer)
                {
                    *(short*)(valueRef + startIndex) = value;
                }
            }

            /// <summary>
            /// 将指定的 16 位无符号整数值转换成字节数组。
            /// </summary>
            /// <param name="value">要转换的数字。</param>
            /// <returns>字节数组。</returns>
            public static byte[] ToBytes(ushort value)
            {
                byte[] buffer = new byte[2];
                ToBytes((short)value, buffer, 0);
                return buffer;
            }

            /// <summary>
            /// 将指定的 16 位无符号整数值转换成字节数组。
            /// </summary>
            /// <param name="value">要转换的数字。</param>
            /// <param name="buffer">用于存放结果的字节数组。</param>
            public static void ToBytes(ushort value, byte[] buffer)
            {
                ToBytes((short)value, buffer, 0);
            }

            /// <summary>
            /// 将指定的 16 位无符号整数值转换成字节数组。
            /// </summary>
            /// <param name="value">要转换的数字。</param>
            /// <param name="buffer">用于存放结果的字节数组。</param>
            /// <param name="startIndex">buffer 内的起始位置。</param>
            public static void ToBytes(ushort value, byte[] buffer, int startIndex)
            {
                ToBytes((short)value, buffer, startIndex);
            }

            /// <summary>
            /// 将指定的 32 位有符号整数值转换成字节数组。
            /// </summary>
            /// <param name="value">要转换的数字。</param>
            /// <returns>字节数组。</returns>
            public static byte[] ToBytes(int value)
            {
                byte[] buffer = new byte[4];
                ToBytes(value, buffer, 0);
                return buffer;
            }

            /// <summary>
            /// 将指定的 32 位有符号整数值转换成字节数组。
            /// </summary>
            /// <param name="value">要转换的数字。</param>
            /// <param name="buffer">用于存放结果的字节数组。</param>
            public static void ToBytes(int value, byte[] buffer)
            {
                ToBytes(value, buffer, 0);
            }

            /// <summary>
            /// 将指定的 32 位有符号整数值转换成字节数组。
            /// </summary>
            /// <param name="value">要转换的数字。</param>
            /// <param name="buffer">用于存放结果的字节数组。</param>
            /// <param name="startIndex">buffer 内的起始位置。</param>
            public static unsafe void ToBytes(int value, byte[] buffer, int startIndex)
            {
                if (buffer == null)
                {
                    throw new ArgumentNullException(nameof(buffer), "Buffer 无效。");
                }

                if (startIndex < 0 || startIndex + 4 > buffer.Length)
                {
                    throw new ArgumentOutOfRangeException(nameof(startIndex), "Start index 无效。");
                }

                fixed (byte* valueRef = buffer)
                {
                    *(int*)(valueRef + startIndex) = value;
                }
            }

            /// <summary>
            /// 将指定的 32 位无符号整数值转换成字节数组。
            /// </summary>
            /// <param name="value">要转换的数字。</param>
            /// <returns>字节数组。</returns>
            public static byte[] ToBytes(uint value)
            {
                byte[] buffer = new byte[4];
                ToBytes((int)value, buffer, 0);
                return buffer;
            }

            /// <summary>
            /// 将指定的 32 位无符号整数值转换成字节数组。
            /// </summary>
            /// <param name="value">要转换的数字。</param>
            /// <param name="buffer">用于存放结果的字节数组。</param>
            public static void ToBytes(uint value, byte[] buffer)
            {
                ToBytes((int)value, buffer, 0);
            }

            /// <summary>
            /// 将指定的 32 位无符号整数值转换成字节数组。
            /// </summary>
            /// <param name="value">要转换的数字。</param>
            /// <param name="buffer">用于存放结果的字节数组。</param>
            /// <param name="startIndex">buffer 内的起始位置。</param>
            public static void ToBytes(uint value, byte[] buffer, int startIndex)
            {
                ToBytes((int)value, buffer, startIndex);
            }

            /// <summary>
            /// 将指定的 64 位有符号整数值转换成字节数组。
            /// </summary>
            /// <param name="value">要转换的数字。</param>
            /// <returns>字节数组。</returns>
            public static byte[] ToBytes(long value)
            {
                byte[] buffer = new byte[8];
                ToBytes(value, buffer, 0);
                return buffer;
            }

            /// <summary>
            /// 将指定的 64 位有符号整数值转换成字节数组。
            /// </summary>
            /// <param name="value">要转换的数字。</param>
            /// <param name="buffer">用于存放结果的字节数组。</param>
            public static void ToBytes(long value, byte[] buffer)
            {
                ToBytes(value, buffer, 0);
            }

            /// <summary>
            /// 将指定的 64 位有符号整数值转换成字节数组。
            /// </summary>
            /// <param name="value">要转换的数字。</param>
            /// <param name="buffer">用于存放结果的字节数组。</param>
            /// <param name="startIndex">buffer 内的起始位置。</param>
            public static unsafe void ToBytes(long value, byte[] buffer, int startIndex)
            {
                if (buffer == null)
                {
                    throw new ArgumentNullException(nameof(buffer), "Buffer 无效。");
                }

                if (startIndex < 0 || startIndex + 8 > buffer.Length)
                {
                    throw new ArgumentOutOfRangeException(nameof(startIndex), "Start index 无效。");
                }

                fixed (byte* valueRef = buffer)
                {
                    *(long*)(valueRef + startIndex) = value;
                }
            }

            /// <summary>
            /// 将指定的 64 位无符号整数值转换成字节数组。
            /// </summary>
            /// <param name="value">要转换的数字。</param>
            /// <returns>字节数组。</returns>
            public static byte[] ToBytes(ulong value)
            {
                byte[] buffer = new byte[8];
                ToBytes((long)value, buffer, 0);
                return buffer;
            }

            /// <summary>
            /// 将指定的 64 位无符号整数值转换成字节数组。
            /// </summary>
            /// <param name="value">要转换的数字。</param>
            /// <param name="buffer">用于存放结果的字节数组。</param>
            public static void ToBytes(ulong value, byte[] buffer)
            {
                ToBytes((long)value, buffer, 0);
            }

            /// <summary>
            /// 将指定的 64 位无符号整数值转换成字节数组。
            /// </summary>
            /// <param name="value">要转换的数字。</param>
            /// <param name="buffer">用于存放结果的字节数组。</param>
            /// <param name="startIndex">buffer 内的起始位置。</param>
            public static void ToBytes(ulong value, byte[] buffer, int startIndex)
            {
                ToBytes((long)value, buffer, startIndex);
            }

            /// <summary>
            /// 将指定的单精度浮点值转换成字节数组。
            /// </summary>
            /// <param name="value">要转换的数字。</param>
            /// <returns>字节数组。</returns>
            public static unsafe byte[] ToBytes(float value)
            {
                byte[] buffer = new byte[4];
                ToBytes(*(int*)&value, buffer, 0);
                return buffer;
            }

            /// <summary>
            /// 将指定的单精度浮点值转换成字节数组。
            /// </summary>
            /// <param name="value">要转换的数字。</param>
            /// <param name="buffer">用于存放结果的字节数组。</param>
            public static unsafe void ToBytes(float value, byte[] buffer)
            {
                ToBytes(*(int*)&value, buffer, 0);
            }

            /// <summary>
            /// 将指定的单精度浮点值转换成字节数组。
            /// </summary>
            /// <param name="value">要转换的数字。</param>
            /// <param name="buffer">用于存放结果的字节数组。</param>
            /// <param name="startIndex">buffer 内的起始位置。</param>
            public static unsafe void ToBytes(float value, byte[] buffer, int startIndex)
            {
                ToBytes(*(int*)&value, buffer, startIndex);
            }

            /// <summary>
            /// 将指定的双精度浮点值转换成字节数组。
            /// </summary>
            /// <param name="value">要转换的数字。</param>
            /// <returns>字节数组。</returns>
            public static unsafe byte[] ToBytes(double value)
            {
                byte[] buffer = new byte[8];
                ToBytes(*(long*)&value, buffer, 0);
                return buffer;
            }

            /// <summary>
            /// 将指定的双精度浮点值转换成字节数组。
            /// </summary>
            /// <param name="value">要转换的数字。</param>
            /// <param name="buffer">用于存放结果的字节数组。</param>
            public static unsafe void ToBytes(double value, byte[] buffer)
            {
                ToBytes(*(long*)&value, buffer, 0);
            }

            /// <summary>
            /// 将指定的双精度浮点值转换成字节数组。
            /// </summary>
            /// <param name="value">要转换的数字。</param>
            /// <param name="buffer">用于存放结果的字节数组。</param>
            /// <param name="startIndex">buffer 内的起始位置。</param>
            public static unsafe void ToBytes(double value, byte[] buffer, int startIndex)
            {
                ToBytes(*(long*)&value, buffer, startIndex);
            }

            /// <summary>
            /// 将指定的 UTF-8 字符串转换成字节数组。
            /// </summary>
            /// <param name="value">要转换的字符串。</param>
            /// <returns>字节数组。</returns>
            public static byte[] ToBytes(string value)
            {
                return Encoding.UTF8.GetBytes(value);
            }

            /// <summary>
            /// 返回由字节数组中首字节转换来的布尔值。
            /// </summary>
            /// <param name="value">字节数组。</param>
            /// <returns>如果 value 中的首字节非零，则返回 true，否则返回 false。</returns>
            public static bool ToBool(byte[] value)
            {
                return BitConverter.ToBoolean(value, 0);
            }

            /// <summary>
            /// 返回由字节数组中指定位置的一个字节转换来的布尔值。
            /// </summary>
            /// <param name="value">字节数组。</param>
            /// <param name="startIndex">value 内的起始位置。</param>
            /// <returns>如果 value 中指定位置的字节非零，则返回 true，否则返回 false。</returns>
            public static bool ToBool(byte[] value, int startIndex)
            {
                return BitConverter.ToBoolean(value, startIndex);
            }

            /// <summary>
            /// 将字节数组中前两个字节转换成 Unicode 字符。
            /// </summary>
            /// <param name="value">字节数组。</param>
            /// <returns>Unicode 字符。</returns>
            public static char ToChar(byte[] value)
            {
                return BitConverter.ToChar(value, 0);
            }

            /// <summary>
            /// 将字节数组中指定位置的两个字节转换成 Unicode 字符。
            /// </summary>
            /// <param name="value">字节数组。</param>
            /// <param name="startIndex">value 内的起始位置。</param>
            /// <returns>Unicode 字符。</returns>
            public static char ToChar(byte[] value, int startIndex)
            {
                return BitConverter.ToChar(value, startIndex);
            }

            /// <summary>
            /// 将字节数组中前两个字节转换成 16 位有符号整数。
            /// </summary>
            /// <param name="value">字节数组。</param>
            /// <returns>16 位有符号整数。</returns>
            public static short ToInt16(byte[] value)
            {
                return BitConverter.ToInt16(value, 0);
            }

            /// <summary>
            /// 将字节数组中指定位置的两个字节转换成 16 位有符号整数。
            /// </summary>
            /// <param name="value">字节数组。</param>
            /// <param name="startIndex">value 内的起始位置。</param>
            /// <returns>16 位有符号整数。</returns>
            public static short ToInt16(byte[] value, int startIndex)
            {
                return BitConverter.ToInt16(value, startIndex);
            }

            /// <summary>
            /// 将字节数组中前两个字节转换成 16 位无符号整数。
            /// </summary>
            /// <param name="value">字节数组。</param>
            /// <returns>16 位无符号整数。</returns>
            public static ushort ToUInt16(byte[] value)
            {
                return BitConverter.ToUInt16(value, 0);
            }

            /// <summary>
            /// 将字节数组中指定位置的两个字节转换成 16 位无符号整数。
            /// </summary>
            /// <param name="value">字节数组。</param>
            /// <param name="startIndex">value 内的起始位置。</param>
            /// <returns>16 位无符号整数。</returns>
            public static ushort ToUInt16(byte[] value, int startIndex)
            {
                return BitConverter.ToUInt16(value, startIndex);
            }

            /// <summary>
            /// 将字节数组中前四个字节转换成 32 位有符号整数。
            /// </summary>
            /// <param name="value">字节数组。</param>
            /// <returns>32 位有符号整数。</returns>
            public static int ToInt32(byte[] value)
            {
                return BitConverter.ToInt32(value, 0);
            }

            /// <summary>
            /// 将字节数组中前四个字节转换成 32 位有符号整数。
            /// </summary>
            /// <param name="value">字节数组。</param>
            /// <param name="startIndex">value 内的起始位置。</param>
            /// <returns>32 位有符号整数。</returns>
            public static int ToInt32(byte[] value, int startIndex)
            {
                return BitConverter.ToInt32(value, startIndex);
            }

            /// <summary>
            /// 将字节数组中前四个字节转换成 32 位无符号整数。
            /// </summary>
            /// <param name="value">字节数组。</param>
            /// <returns>32 位无符号整数。</returns>
            public static uint ToUInt32(byte[] value)
            {
                return BitConverter.ToUInt32(value, 0);
            }

            /// <summary>
            /// 将字节数组中指定位置的四个字节转换成 32 位无符号整数。
            /// </summary>
            /// <param name="value">字节数组。</param>
            /// <param name="startIndex">value 内的起始位置。</param>
            /// <returns>32 位无符号整数。</returns>
            public static uint ToUInt32(byte[] value, int startIndex)
            {
                return BitConverter.ToUInt32(value, startIndex);
            }

            /// <summary>
            /// 将字节数组中前八个字节转换成 64 位有符号整数。
            /// </summary>
            /// <param name="value">字节数组。</param>
            /// <returns>64 位有符号整数。</returns>
            public static long ToInt64(byte[] value)
            {
                return BitConverter.ToInt64(value, 0);
            }

            /// <summary>
            /// 将字节数组中指定位置的八个字节转换成 64 位有符号整数。
            /// </summary>
            /// <param name="value">字节数组。</param>
            /// <param name="startIndex">value 内的起始位置。</param>
            /// <returns>64 位有符号整数。</returns>
            public static long ToInt64(byte[] value, int startIndex)
            {
                return BitConverter.ToInt64(value, startIndex);
            }

            /// <summary>
            /// 将字节数组中前八个字节转换成 64 位无符号整数。
            /// </summary>
            /// <param name="value">字节数组。</param>
            /// <returns>64 位无符号整数。</returns>
            public static ulong ToUInt64(byte[] value)
            {
                return BitConverter.ToUInt64(value, 0);
            }

            /// <summary>
            /// 将字节数组中指定位置的八个字节转换成 64 位无符号整数。
            /// </summary>
            /// <param name="value">字节数组。</param>
            /// <param name="startIndex">value 内的起始位置。</param>
            /// <returns>64 位无符号整数。</returns>
            public static ulong ToUInt64(byte[] value, int startIndex)
            {
                return BitConverter.ToUInt64(value, startIndex);
            }

            /// <summary>
            /// 将字节数组中前四个字节转换成单精度浮点数。
            /// </summary>
            /// <param name="value">字节数组。</param>
            /// <returns>单精度浮点数。</returns>
            public static float ToFloat(byte[] value)
            {
                return BitConverter.ToSingle(value, 0);
            }

            /// <summary>
            /// 将字节数组中指定位置的四个字节转换成单精度浮点数。
            /// </summary>
            /// <param name="value">字节数组。</param>
            /// <param name="startIndex">value 内的起始位置。</param>
            /// <returns>单精度浮点数。</returns>
            public static float ToFloat(byte[] value, int startIndex)
            {
                return BitConverter.ToSingle(value, startIndex);
            }

            /// <summary>
            /// 将字节数组中前八个字节转换成双精度浮点数。
            /// </summary>
            /// <param name="value">字节数组。</param>
            /// <returns>双精度浮点数。</returns>
            public static double ToDouble(byte[] value)
            {
                return BitConverter.ToDouble(value, 0);
            }

            /// <summary>
            /// 将字节数组中指定位置的八个字节转换成双精度浮点数。
            /// </summary>
            /// <param name="value">字节数组。</param>
            /// <param name="startIndex">value 内的起始位置。</param>
            /// <returns>双精度浮点数。</returns>
            public static double ToDouble(byte[] value, int startIndex)
            {
                return BitConverter.ToDouble(value, startIndex);
            }

            /// <summary>
            /// 将字节数组转换成 UTF-8 字符串。
            /// </summary>
            /// <param name="value">字节数组。</param>
            /// <returns>UTF-8 字符串。</returns>
            public static string ToString(byte[] value)
            {
                if (value == null)
                {
                    throw new ArgumentNullException(nameof(value), "Value 无效。");
                }
                return Encoding.UTF8.GetString(value);
            }

            /// <summary>
            /// 将字节数组转换成 UTF-8 字符串。
            /// </summary>
            /// <param name="value">字节数组。</param>
            /// <param name="startIndex">value 内的起始位置。</param>
            /// <param name="length">长度。</param>
            /// <returns>UTF-8 字符串。</returns>
            public static string ToString(byte[] value, int startIndex, int length)
            {
                if (value == null)
                {
                    throw new ArgumentNullException(nameof(value), "Value 无效。");
                }

                return Encoding.UTF8.GetString(value, startIndex, length);
            }

            /// <summary>
            /// 转换为网络字节顺序。
            /// </summary>
            /// <param name="value">值。</param>
            /// <returns>网络字节顺序的值。</returns>
            public static uint ToNetworkOrder(uint value)
            {
                byte[] bytes = BitConverter.GetBytes(value);
                if (BitConverter.IsLittleEndian)
                {
                    Array.Reverse(bytes);
                }
                return BitConverter.ToUInt32(bytes, 0);
            }

            /// <summary>
            /// 转换为主机字节顺序。
            /// </summary>
            /// <param name="value">值。</param>
            /// <returns>主机字节顺序的值。</returns>
            public static uint ToHostOrder(uint value)
            {
                byte[] bytes = BitConverter.GetBytes(value);
                if (BitConverter.IsLittleEndian)
                {
                    Array.Reverse(bytes);
                }
                return BitConverter.ToUInt32(bytes, 0);
            }
        }
    }
}
