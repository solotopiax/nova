/***************************************************************
 * (c) copyright 2026 - 2030, Solotopia
 * All Rights Reserved.
 * -------------------------------------------------------------
 * filename:  Util.Convert.Base64.cs
 * author:    taoye
 * created:   2026/1/27
 * descrip:   类型转换工具 —— Base64 编解码
 ***************************************************************/

using System.Text.RegularExpressions;

namespace NovaFramework.Runtime
{
    public static partial class Util
    {
        public static partial class Convert
        {
            /// <summary>
            /// 判断字符串内容是否为合法的 Base64 编码。
            /// </summary>
            /// <param name="content">内容。</param>
            /// <returns>是否为合法 Base64 编码。</returns>
            public static bool IsBase64(string content)
            {
                return Regex.IsMatch(content, @"^[a-zA-Z0-9\+/]*={0,2}$");
            }

            /// <summary>
            /// 判断字节数组内容是否为合法的 Base64 编码。
            /// </summary>
            /// <param name="content">字节数组。</param>
            /// <returns>是否为合法 Base64 编码。</returns>
            public static bool IsBase64(byte[] content)
            {
                if (content == null || content.Length == 0 || content.Length % 4 != 0)
                {
                    return false;
                }

                for (int i = 0; i < content.Length; i++)
                {
                    byte b = content[i];
                    if (b >= (byte)'A' && b <= (byte)'Z') continue;
                    if (b >= (byte)'a' && b <= (byte)'z') continue;
                    if (b >= (byte)'0' && b <= (byte)'9') continue;
                    if (b == (byte)'+' || b == (byte)'/') continue;
                    if (b == (byte)'=' && i >= content.Length - 2) continue;
                    return false;
                }

                return true;
            }

            /// <summary>
            /// 将字节数组转换成 Base64 字符串。
            /// </summary>
            /// <param name="bytes">字节数组。</param>
            /// <returns>Base64 字符串。</returns>
            public static string ToBase64String(byte[] bytes)
            {
                return System.Convert.ToBase64String(bytes);
            }

            /// <summary>
            /// 将 Base64 字符串转换成字节数组。
            /// </summary>
            /// <param name="str">Base64 字符串。</param>
            /// <returns>字节数组。</returns>
            public static byte[] FromBase64String(string str)
            {
                return System.Convert.FromBase64String(str);
            }
        }
    }
}
