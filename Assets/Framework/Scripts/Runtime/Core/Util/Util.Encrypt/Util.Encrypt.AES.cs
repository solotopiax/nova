/***************************************************************
 * (c) copyright 2026 - 2030, Solotopia
 * All Rights Reserved.
 * -------------------------------------------------------------
 * filename:  Util.Encrypt.AES.cs
 * author:    taoye
 * created:   2026/1/15
 * descrip:   AES加解密
 ***************************************************************/

using System;
using System.Security.Cryptography;
using System.Text;

namespace NovaFramework.Runtime
{
    public static partial class Util
    {
        public static partial class Encrypt
        {
            /// <summary>
            /// AES 加密算法实现。
            /// 支持两种工作模式：
            /// 1. 静态模式：外部显式传入 Key / IV。
            /// 2. 默认模式：使用内置默认 Key / IV 进行本地数据混淆。
            /// </summary>
            public static class AES
            {
                /// <summary>
                /// AES Key / IV 固定长度（16 字节 = 128 位）。
                /// </summary>
                private const int c_SecretBytesLength = 16;

                /// <summary>
                /// 运行时注入的默认 Key（UTF-8，16 字节）。框架不内置任何密钥；未配置时为 null，
                /// 此时调用不显式传 key 的加解密接口将报错。由使用方启动时通过 <see cref="Configure"/> 注入。
                /// </summary>
                private static byte[] s_DefaultKey;

                /// <summary>
                /// 运行时注入的默认 IV（UTF-8，16 字节）。未配置时为 null。详见 <see cref="s_DefaultKey"/>。
                /// </summary>
                private static byte[] s_DefaultIV;

                /// <summary>
                /// 配置默认 Key / IV，供不显式传 key/iv 的加解密调用使用。
                /// <para>框架不内置任何默认密钥；使用默认加解密接口前，必须由使用方在启动时调用一次本方法注入项目自有密钥。</para>
                /// </summary>
                /// <param name="key">默认 Key（UTF-8 字符串，必须为 16 字节）。</param>
                /// <param name="iv">默认 IV（UTF-8 字符串，必须为 16 字节）。</param>
                /// <exception cref="ArgumentException">key 或 iv 为空或长度不为 16 字节时抛出。</exception>
                public static void Configure(string key, string iv)
                {
                    if (string.IsNullOrEmpty(key) || Encoding.UTF8.GetByteCount(key) != c_SecretBytesLength)
                    {
                        throw new ArgumentException("AES 默认 Key 必须为 16 字节 UTF-8 字符串。");
                    }
                    if (string.IsNullOrEmpty(iv) || Encoding.UTF8.GetByteCount(iv) != c_SecretBytesLength)
                    {
                        throw new ArgumentException("AES 默认 IV 必须为 16 字节 UTF-8 字符串。");
                    }
                    s_DefaultKey = Encoding.UTF8.GetBytes(key);
                    s_DefaultIV = Encoding.UTF8.GetBytes(iv);
                }

                /// <summary>
                /// 将字符串进行 AES 加密，并输出 Base64 字符串。
                /// </summary>
                /// <param name="content">待加密的明文字符串。</param>
                /// <param name="key">加密 Key（UTF8 字符串，长度必须为 16 字节），为空时使用内置默认 Key。</param>
                /// <param name="iv">加密 IV（UTF8 字符串，长度必须为 16 字节），为空时使用内置默认 IV。</param>
                /// <returns>Base64 格式的加密结果字符串。</returns>
                public static string EncryptString(string content, string key = null, string iv = null)
                {
                    if (string.IsNullOrEmpty(content))
                    {
                        return string.Empty;
                    }

                    byte[] bytes = EncryptBytes(Encoding.UTF8.GetBytes(content), key, iv);
                    return Convert.ToBase64String(bytes);
                }

                /// <summary>
                /// 从 Base64 字符串解密出原始明文字符串。
                /// </summary>
                /// <param name="content">Base64 格式的密文字符串。</param>
                /// <param name="key">解密 Key（UTF8 字符串，长度必须为 16 字节），为空时使用内置默认 Key。</param>
                /// <param name="iv">解密 IV（UTF8 字符串，长度必须为 16 字节），为空时使用内置默认 IV。</param>
                /// <returns>解密后的原始明文字符串。</returns>
                public static string DecryptString(string content, string key = null, string iv = null)
                {
                    try
                    {
                        if (string.IsNullOrEmpty(content))
                        {
                            return string.Empty;
                        }

                        byte[] bytes = Convert.FromBase64String(content);
                        byte[] decoded = DecryptBytes(bytes, key, iv);

                        return Encoding.UTF8.GetString(decoded);
                    }
                    catch (Exception e)
                    {
                        Log.Error(LogTag.Encrypt, "AES 解密失败：{0}。", e);
                        return string.Empty;
                    }
                }

                /// <summary>
                /// 对二进制数据进行 AES 加密。
                /// </summary>
                /// <param name="content">待加密的原始二进制数据。</param>
                /// <param name="key">加密 Key（UTF8 字符串，长度必须为 16 字节），为空时使用内置默认 Key。</param>
                /// <param name="iv">加密 IV（UTF8 字符串，长度必须为 16 字节），为空时使用内置默认 IV。</param>
                /// <returns>加密后的二进制数据（仅包含密文，不含 Key/IV）。</returns>
                public static byte[] EncryptBytes(byte[] content, string key = null, string iv = null)
                {
                    if (content == null || content.Length == 0)
                    {
                        return Array.Empty<byte>();
                    }

                    if (!TryResolveKeyAndIV(key, iv, out byte[] keyArray, out byte[] ivArray))
                    {
                        return Array.Empty<byte>();
                    }

                    using var aes = Aes.Create();
                    aes.Key = keyArray;
                    aes.IV = ivArray;
                    aes.Mode = CipherMode.CBC;
                    aes.Padding = PaddingMode.PKCS7;

                    using var encryptor = aes.CreateEncryptor();
                    return encryptor.TransformFinalBlock(content, 0, content.Length);
                }

                /// <summary>
                /// 对 AES 加密后的二进制数据进行解密。
                /// </summary>
                /// <param name="content">AES 加密后的二进制数据（仅密文，不含 Key/IV 头部）。</param>
                /// <param name="key">解密 Key（UTF8 字符串，长度必须为 16 字节），为空时使用内置默认 Key。</param>
                /// <param name="iv">解密 IV（UTF8 字符串，长度必须为 16 字节），为空时使用内置默认 IV。</param>
                /// <returns>解密后的原始二进制数据。</returns>
                public static byte[] DecryptBytes(byte[] content, string key = null, string iv = null)
                {
                    if (content == null || content.Length == 0)
                    {
                        return Array.Empty<byte>();
                    }

                    if (!TryResolveKeyAndIV(key, iv, out byte[] keyArray, out byte[] ivArray))
                    {
                        return Array.Empty<byte>();
                    }

                    using var aes = Aes.Create();
                    aes.Key = keyArray;
                    aes.IV = ivArray;
                    aes.Mode = CipherMode.CBC;
                    aes.Padding = PaddingMode.PKCS7;

                    using var decryptor = aes.CreateDecryptor();
                    return decryptor.TransformFinalBlock(content, 0, content.Length);
                }

                /// <summary>
                /// 解析 Key 和 IV：显式传入优先，否则取 <see cref="Configure"/> 注入的默认值。
                /// 既未传入也未配置默认值时，打印错误日志并返回 false。
                /// </summary>
                /// <param name="key">Key 字符串，为空时取注入的默认 Key。</param>
                /// <param name="iv">IV 字符串，为空时取注入的默认 IV。</param>
                /// <param name="keyBytes">解析出的 Key 字节数组。</param>
                /// <param name="ivBytes">解析出的 IV 字节数组。</param>
                /// <returns>成功解析返回 true；默认 Key/IV 未初始化返回 false。</returns>
                /// <exception cref="ArgumentException">显式传入的 key/iv 长度不为 16 字节时抛出。</exception>
                private static bool TryResolveKeyAndIV(string key, string iv, out byte[] keyBytes, out byte[] ivBytes)
                {
                    if (!string.IsNullOrEmpty(key))
                    {
                        keyBytes = Encoding.UTF8.GetBytes(key);
                        if (keyBytes.Length != c_SecretBytesLength)
                        {
                            throw new ArgumentException("AES Key 长度必须为 16 字节。");
                        }
                    }
                    else
                    {
                        keyBytes = s_DefaultKey;
                    }

                    if (!string.IsNullOrEmpty(iv))
                    {
                        ivBytes = Encoding.UTF8.GetBytes(iv);
                        if (ivBytes.Length != c_SecretBytesLength)
                        {
                            throw new ArgumentException("AES IV 长度必须为 16 字节。");
                        }
                    }
                    else
                    {
                        ivBytes = s_DefaultIV;
                    }

                    if (keyBytes == null || ivBytes == null)
                    {
                        Log.Error(LogTag.Encrypt, "AES 默认 Key/IV 未初始化：请先调用 Util.Encrypt.AES.Configure(key, iv) 完成初始化，或在调用加解密接口时显式传入 key/iv。");
                        return false;
                    }
                    return true;
                }
            }
        }
    }
}
