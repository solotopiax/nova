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
using System.ComponentModel;
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
                /// 默认 AES 凭据缺失或无效时的统一配置指引；只适用于隐私配置，不得用于应用协议 AES。
                /// </summary>
                private const string c_DefaultAesConfigurationGuide =
                    "请在 Nova/Open Config → 通用配置 → 隐私配置中，为当前 Platform × Channel × DevelopMode 配置 AES-Key / AES-IV（UTF-8 各 16 字节），保存后重新导出 ConfigRuntimeSO。";

                /// <summary>
                /// 运行时注入的默认 Key（UTF-8，16 字节）。框架不内置任何密钥；未配置时为 null，
                /// 此时调用不显式传 key 的加解密接口将报错。由 Config 隐私配置在加载完成前注入。
                /// </summary>
                private static byte[] s_DefaultKey;

                /// <summary>
                /// 运行时注入的默认 IV（UTF-8，16 字节）。未配置时为 null。详见 <see cref="s_DefaultKey"/>。
                /// </summary>
                private static byte[] s_DefaultIV;

                /// <summary>
                /// 旧版手动配置入口；Config 隐私配置已完全接管 AES 默认密钥初始化，本方法不再修改任何状态。
                /// </summary>
                /// <param name="key">默认 Key（UTF-8 字符串，必须为 16 字节）。</param>
                /// <param name="iv">默认 IV（UTF-8 字符串，必须为 16 字节）。</param>
                [EditorBrowsable(EditorBrowsableState.Never)]
                [Obsolete("AES 默认 Key/IV 已由 Config 隐私配置接管，禁止手动调用 Configure。")]
                public static void Configure(string key, string iv)
                {
                    Log.Error(LogTag.Encrypt, "AES 默认 Key/IV 已由 Config 隐私配置完全接管，禁止手动调用 Util.Encrypt.AES.Configure。{0}", c_DefaultAesConfigurationGuide);
                }

                /// <summary>
                /// 使用 ConfigRuntimeSO 导出的隐私配置初始化默认 Key / IV；仅允许 Config 加载链调用。
                /// </summary>
                /// <param name="config">当前运行坐标导出的隐私配置。</param>
                /// <exception cref="InvalidOperationException">配置缺失或 Key/IV 不是 16 字节 UTF-8 字符串时抛出。</exception>
                internal static void InitializeFromConfig(PrivacyConfigs config)
                {
                    if (config == null)
                    {
                        throw new InvalidOperationException($"AES 默认 Key/IV 初始化失败：ConfigRuntimeSO.PrivacyConfigs 为空。{c_DefaultAesConfigurationGuide}");
                    }
                    if (!TryEncodeSecret(config.AESKey, "Key", out byte[] keyBytes) ||
                        !TryEncodeSecret(config.AESIV, "IV", out byte[] ivBytes))
                    {
                        throw new InvalidOperationException($"AES 默认 Key/IV 初始化失败：隐私配置中的 AES-Key 与 AES-IV 按 UTF-8 编码后必须各为 16 字节。{c_DefaultAesConfigurationGuide}");
                    }
                    s_DefaultKey = keyBytes;
                    s_DefaultIV = ivBytes;
                }

                /// <summary>
                /// 仅允许在所有 FrameworkManager 完成 Shutdown 后调用；清空 Config 生命周期内注入的默认密钥，
                /// 避免关闭或禁用 Domain Reload 后残留上一次运行的默认凭据。
                /// </summary>
                internal static void ResetConfigInitialization()
                {
                    s_DefaultKey = null;
                    s_DefaultIV = null;
                }

                /// <summary>
                /// 确认 Config 生命周期已经注入可用的默认 AES Key/IV；Persist 等默认模式调用方应在执行读写前调用。
                /// </summary>
                /// <exception cref="InvalidOperationException">默认凭据尚未由隐私配置注入时抛出。</exception>
                internal static void EnsureDefaultKeyAndIVReady()
                {
                    if (s_DefaultKey != null && s_DefaultKey.Length == c_SecretBytesLength &&
                        s_DefaultIV != null && s_DefaultIV.Length == c_SecretBytesLength)
                    {
                        return;
                    }

                    string message = $"AES 默认 Key/IV 未初始化。{c_DefaultAesConfigurationGuide}";
                    Log.Error(LogTag.Encrypt, message);
                    throw new InvalidOperationException(message);
                }

                /// <summary>
                /// 将配置字符串编码为固定长度密钥字节。
                /// </summary>
                /// <param name="value">待编码字符串。</param>
                /// <param name="name">用于区分 Key 与 IV 的字段名。</param>
                /// <param name="bytes">编码成功后的 16 字节数组。</param>
                /// <returns>字符串非空且 UTF-8 长度为 16 字节时返回 true。</returns>
                private static bool TryEncodeSecret(string value, string name, out byte[] bytes)
                {
                    bytes = string.IsNullOrEmpty(value) ? null : Encoding.UTF8.GetBytes(value);
                    if (bytes == null || bytes.Length != c_SecretBytesLength)
                    {
                        Log.Error(LogTag.Encrypt, "AES 默认 Key/IV 初始化失败：隐私配置中的 AES-{0} 按 UTF-8 编码后必须为 16 字节。{1}", name, c_DefaultAesConfigurationGuide);
                        return false;
                    }
                    return true;
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
                /// 解析 Key 和 IV：显式模式必须成对传入，否则取 Config 隐私配置注入的默认值。
                /// 默认凭据未就绪时打印配置指引并返回 false，避免底层 AES 以空凭据执行。
                /// </summary>
                /// <param name="key">Key 字符串，为空时取注入的默认 Key。</param>
                /// <param name="iv">IV 字符串，为空时取注入的默认 IV。</param>
                /// <param name="keyBytes">解析出的 Key 字节数组。</param>
                /// <param name="ivBytes">解析出的 IV 字节数组。</param>
                /// <returns>成功解析返回 true；默认 Key/IV 未初始化返回 false。</returns>
                /// <exception cref="ArgumentException">显式 Key/IV 未成对传入或 UTF-8 长度不为 16 字节时抛出。</exception>
                private static bool TryResolveKeyAndIV(string key, string iv, out byte[] keyBytes, out byte[] ivBytes)
                {
                    bool hasExplicitKey = !string.IsNullOrEmpty(key);
                    bool hasExplicitIV = !string.IsNullOrEmpty(iv);
                    if (hasExplicitKey != hasExplicitIV)
                    {
                        throw new ArgumentException("显式 AES Key/IV 必须同时传入，不能与 Config 隐私配置默认值混用。");
                    }

                    if (hasExplicitKey)
                    {
                        keyBytes = Encoding.UTF8.GetBytes(key);
                        if (keyBytes.Length != c_SecretBytesLength)
                        {
                            throw new ArgumentException("AES Key 长度必须为 16 字节。");
                        }

                        ivBytes = Encoding.UTF8.GetBytes(iv);
                        if (ivBytes.Length != c_SecretBytesLength)
                        {
                            throw new ArgumentException("AES IV 长度必须为 16 字节。");
                        }

                        return true;
                    }

                    try
                    {
                        EnsureDefaultKeyAndIVReady();
                        keyBytes = s_DefaultKey;
                        ivBytes = s_DefaultIV;
                        return true;
                    }
                    catch (InvalidOperationException)
                    {
                        keyBytes = null;
                        ivBytes = null;
                        return false;
                    }
                }
            }
        }
    }
}
