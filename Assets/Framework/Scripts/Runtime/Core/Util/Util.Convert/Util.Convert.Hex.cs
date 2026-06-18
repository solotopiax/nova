/***************************************************************
 * (c) copyright 2026 - 2030, Solotopia
 * All Rights Reserved.
 * -------------------------------------------------------------
 * filename:  Util.Convert.Hex.cs
 * author:    taoye
 * created:   2026/1/27
 * descrip:   类型转换工具 —— 十六进制转换
 ***************************************************************/

using System.Text;

namespace NovaFramework.Runtime
{
    public static partial class Util
    {
        public static partial class Convert
        {
            /// <summary>
            /// 将字节转换为十六进制字符串。
            /// </summary>
            /// <param name="b">字节。</param>
            /// <returns>十六进制字符串。</returns>
            public static string ToHex(byte b)
            {
                return b.ToString("X2");
            }

            /// <summary>
            /// 将字节数组转换为十六进制字符串。
            /// </summary>
            /// <param name="bytes">字节数组。</param>
            /// <returns>十六进制字符串。</returns>
            public static string ToHex(byte[] bytes)
            {
                StringBuilder stringBuilder = new StringBuilder(bytes.Length * 2);
                foreach (byte b in bytes)
                {
                    stringBuilder.Append(b.ToString("X2"));
                }
                return stringBuilder.ToString();
            }

            /// <summary>
            /// 将字节数组转换为十六进制字符串。
            /// </summary>
            /// <param name="bytes">字节数组。</param>
            /// <param name="format">格式。</param>
            /// <returns>十六进制字符串。</returns>
            public static string ToHex(byte[] bytes, string format)
            {
                StringBuilder stringBuilder = new StringBuilder(bytes.Length * 2);
                foreach (byte b in bytes)
                {
                    stringBuilder.Append(b.ToString(format));
                }
                return stringBuilder.ToString();
            }

            /// <summary>
            /// 将字节数组转换为十六进制字符串。
            /// </summary>
            /// <param name="bytes">字节数组。</param>
            /// <param name="offset">偏移。</param>
            /// <param name="count">数量。</param>
            /// <returns>十六进制字符串。</returns>
            public static string ToHex(byte[] bytes, int offset, int count)
            {
                StringBuilder stringBuilder = new StringBuilder(count * 2);
                for (int i = offset; i < offset + count; ++i)
                {
                    stringBuilder.Append(bytes[i].ToString("X2"));
                }
                return stringBuilder.ToString();
            }
        }
    }
}
