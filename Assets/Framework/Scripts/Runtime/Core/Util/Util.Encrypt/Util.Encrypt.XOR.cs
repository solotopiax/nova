/***************************************************************
 * (c) copyright 2026 - 2030, Solotopia
 * All Rights Reserved.
 * -------------------------------------------------------------
 * filename:  Util.Encrypt.XOR.cs
 * author:    taoye
 * created:   2026/1/15
 * descrip:   XOR加解密
 ***************************************************************/

using System;
using System.Text;

namespace NovaFramework.Runtime
{
    public static partial class Util
    {
        public static partial class Encrypt
        {
            /// <summary>
            /// XOR 加密算法实现。
            /// 特点说明：
            /// 1. 属于轻量级“数据混淆”算法，不具备安全加密强度。
            /// 2. 加密与解密逻辑完全一致。
            /// 3. 适用于资源文件、配置数据的简单防反编译处理。
            /// 4. 不适用于账号、通信、支付等安全敏感数据。
            /// </summary>
            public static class XOR
            {
                #region Base64 接口

                /// <summary>
                /// 将字符串进行 XOR 加密，并输出 Base64 字符串。
                /// </summary>
                /// <param name="content">待加密的原始明文字符串。</param>
                /// <param name="code">加密秘钥。</param>
                /// <returns>Base64 格式的加密字符串。</returns>
                public static string EncryptString(string content, byte[] code)
                {
                    if (string.IsNullOrEmpty(content))
                    {
                        return string.Empty;
                    }
                    if (code == null || code.Length == 0)
                    {
                        return string.Empty;
                    }

                    byte[] encrypted = EncryptBytes(Encoding.UTF8.GetBytes(content), code);
                    return Convert.ToBase64String(encrypted);
                }

                /// <summary>
                /// 从 Base64 字符串解密出原始明文字符串。
                /// </summary>
                /// <param name="content">Base64 格式的 XOR 密文字符串。</param>
                /// <param name="code">加密秘钥。</param>
                /// <returns>解密后的原始明文字符串。</returns>
                /// <remarks> 当 Base64 非法或解密异常时，返回空字符串并输出错误日志。</remarks>
                public static string DecryptString(string content, byte[] code)
                {
                    try
                    {
                        if (string.IsNullOrEmpty(content))
                        {
                            return string.Empty;
                        }
                        if (code == null || code.Length == 0)
                        {
                            return string.Empty;
                        }

                        byte[] bytes = Convert.FromBase64String(content);
                        byte[] decoded = DecryptBytes(bytes, code);

                        return Encoding.UTF8.GetString(decoded);
                    }
                    catch (Exception e)
                    {
                        Log.Error(LogTag.Encrypt, "XOR 解密失败：{0}。", e);
                        return string.Empty;
                    }
                }

                #endregion

                #region 二进制接口

                /// <summary>
                /// 对二进制数据进行 XOR 加密。
                /// </summary>
                /// <param name="content">待加密的原始二进制数据。</param>
                /// <param name="code">加密秘钥。</param>
                /// <returns>加密后的二进制数据。</returns>
                public static byte[] EncryptBytes(byte[] content, byte[] code)
                {
                    if (content == null || content.Length == 0)
                    {
                        return Array.Empty<byte>();
                    }
                    if (code == null || code.Length == 0)
                    {
                        return Array.Empty<byte>();
                    }
                    
                    return XorBytes(content, code);
                }

                /// <summary>
                /// 对 XOR 加密后的二进制数据进行解密。
                /// </summary>
                /// <param name="content">XOR 加密后的二进制数据。</param>
                /// <param name="code"> 预留参数（接口兼容用，XOR 实现中不使用）。</param>
                /// <returns>解密后的原始二进制数据。</returns>
                public static byte[] DecryptBytes(byte[] content, byte[] code)
                {
                    if (content == null || content.Length == 0)
                    {
                        return Array.Empty<byte>();
                    }
                    if (code == null || code.Length == 0)
                    {
                        return Array.Empty<byte>();
                    }

                    return XorBytes(content, code);
                }

                #endregion

                #region 私有工具方法

                /// <summary>
                /// XOR 运算核心实现。
                /// </summary>
                /// <param name="data">原始数据。</param>
                /// <param name="code">异或码字节数组。</param>
                /// <returns>经过 XOR 运算后的结果字节数组。</returns>
                private static byte[] XorBytes(byte[] data, byte[] code)
                {
                    byte[] result = new byte[data.Length];
                    int codeLen = code.Length;

                    for (int i = 0; i < data.Length; i++)
                    {
                        // 通过取模方式循环使用 XOR 码。
                        result[i] = (byte)(data[i] ^ code[i % codeLen]);
                    }

                    return result;
                }

                #endregion
            }
        }
        
    }

}
