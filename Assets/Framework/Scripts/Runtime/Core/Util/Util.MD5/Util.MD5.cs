/***************************************************************
 * (c) copyright 2026 - 2030, Solotopia
 * All Rights Reserved.
 * -------------------------------------------------------------
 * filename:  Util.MD5.cs
 * author:    taoye
 * created:   2026/3/6
 * descrip:   MD5 哈希工具
 ***************************************************************/

using System.IO;
using System.Text;
using SystemMD5 = System.Security.Cryptography.MD5;

namespace NovaFramework.Runtime
{
    public static partial class Util
    {
        /// <summary>
        /// MD5 哈希工具。
        /// 提供从字节数组或文件路径计算 MD5 哈希值的静态方法。
        /// </summary>
        public static class MD5
        {
            /// <summary>
            /// 从字节数组计算 MD5 哈希值。
            /// </summary>
            /// <param name="bytes">字节数组。</param>
            /// <returns>小写 MD5 十六进制字符串；输入为空时返回 string.Empty。</returns>
            public static string GetHashFromBytes(byte[] bytes)
            {
                if (bytes == null || bytes.Length == 0)
                {
                    return string.Empty;
                }

                using var md5 = SystemMD5.Create();
                byte[] hash = md5.ComputeHash(bytes);
                return HashToHexString(hash);
            }

            /// <summary>
            /// 从文件计算 MD5 哈希值。
            /// </summary>
            /// <param name="filePath">文件路径。</param>
            /// <returns>小写 MD5 十六进制字符串；文件不存在时返回 string.Empty。</returns>
            public static string GetHashFromFile(string filePath)
            {
                if (!File.Exists(filePath))
                {
                    return string.Empty;
                }

                using var md5 = SystemMD5.Create();
                using var stream = File.OpenRead(filePath);
                byte[] hash = md5.ComputeHash(stream);
                return HashToHexString(hash);
            }

            /// <summary>
            /// 将哈希字节数组转换为十六进制字符串。
            /// </summary>
            /// <param name="hash">哈希字节数组。</param>
            /// <returns>十六进制字符串。</returns>
            private static string HashToHexString(byte[] hash)
            {
                StringBuilder sb = new StringBuilder(hash.Length * 2);
                for (int i = 0; i < hash.Length; i++)
                {
                    sb.Append(hash[i].ToString("x2"));
                }
                return sb.ToString();
            }
        }
    }
}
