/***************************************************************
 * (c) copyright 2026 - 2030, Solotopia
 * All Rights Reserved.
 * -------------------------------------------------------------
 * filename:  ResourceEncryptionMode.cs
 * author:    taoye
 * created:   2026/10/9
 * descrip:   资源加密模式与业务密钥提供契约
 ***************************************************************/

using System;
using System.Threading;

namespace NovaFramework.Runtime
{
    /// <summary>资源文件的编码模式；不加密时不使用此编码器。</summary>
    public enum ResourceEncryptionMode : byte
    {
        /// <summary>仅追加文件头，不提供保密性或完整性保护。</summary>
        Offset = 1,
        /// <summary>循环异或弱混淆，不提供密码学保密性或完整性保护。</summary>
        Xor = 2,
        /// <summary>AES-256 CBC 与 HMAC-SHA256，整文件认证后解密。</summary>
        AesMemory = 3,
        /// <summary>AES-256 CBC 与 HMAC-SHA256，按独立认证块随机访问。</summary>
        AesStream = 4
    }

    /// <summary>由业务提供密钥；AES 主密钥必须为 32 字节，禁止把密钥写进资源配置。</summary>
    public interface IResourceKeyProvider
    {
        /// <summary>按包名与密钥标识获取密钥；未找到时可返回 null。实现需支持并发调用。</summary>
        byte[] GetKey(string packageName, string keyId);
    }

    /// <summary>业务密钥提供器注册入口；未加密资源不依赖此注册状态。</summary>
    public static class ResourceKeyRegistry
    {
        private static IResourceKeyProvider s_Provider;

        /// <summary>获取或替换当前业务密钥提供器；解密流创建时冻结其密钥副本。</summary>
        public static IResourceKeyProvider Provider
        {
            get => Volatile.Read(ref s_Provider);
            set => Volatile.Write(ref s_Provider, value);
        }

        /// <summary>按包名与密钥标识获取密钥副本；未配置或未找到密钥时拒绝解密。</summary>
        public static byte[] GetKey(string packageName, string keyId)
        {
            var key = Provider?.GetKey(packageName, keyId);
            if (key == null || key.Length == 0)
                throw new InvalidOperationException("未提供资源密钥：" + packageName + "/" + keyId);
            return (byte[])key.Clone();
        }
    }
}
