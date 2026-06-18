/***************************************************************
 * (c) copyright 2026 - 2030, Solotopia
 * All Rights Reserved.
 * -------------------------------------------------------------
 * filename:  NetParser.cs
 * author:    taoye
 * created:   2026/5/26
 * descrip:   网络响应解析静态工具（包内私有）：AES 解密 + 响应解析
 ***************************************************************/

using System;
namespace NovaFramework.Runtime
{
    /// <summary>
    /// 网络响应解析静态工具（内部解析工具，仅本包内使用），承接 AES 解密与响应解析职责。
    /// </summary>
    internal static class NetParser
    {
        /// <summary>
        /// 使用 AES-128-CBC + PKCS7 解密密文字节数据。
        /// 委托框架层 Util.Encrypt.AES 实现，本方法仅做职责归属封装。
        /// </summary>
        /// <param name="cipherBytes">待解密的密文字节数组。</param>
        /// <param name="key">AES 密钥（16 字节 UTF-8 字符串）。</param>
        /// <param name="iv">AES 初始向量（16 字节 UTF-8 字符串）。</param>
        /// <returns>解密后的明文字节数组。</returns>
        public static byte[] Decrypt(byte[] cipherBytes, string key, string iv)
        {
            return Util.Encrypt.AES.DecryptBytes(cipherBytes, key, iv);
        }

        /// <summary>
        /// 解析服务端 PbNetBaseResponse 字节数据为通用 NetResult。
        /// </summary>
        /// <param name="decryptedBytes">AES 解密后的原始字节数据。</param>
        /// <returns>响应解析结果。</returns>
        public static NetResult ParseResponse(byte[] decryptedBytes)
        {
            if (decryptedBytes == null || decryptedBytes.Length == 0)
            {
                return new NetResult(NetErrorCode.PROTO_PARSE_FAILED, "decryptedBytes is null or empty", Array.Empty<byte>());
            }

            PbNetBaseResponse baseResponse = PbNetBaseResponse.Parser.ParseFrom(decryptedBytes);
            return new NetResult(baseResponse.Code, baseResponse.Message, baseResponse.Data.ToByteArray());
        }
    }
}
