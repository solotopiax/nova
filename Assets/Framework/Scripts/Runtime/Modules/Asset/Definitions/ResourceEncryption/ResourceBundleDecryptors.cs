/***************************************************************
 * (c) copyright 2026 - 2030, Solotopia
 * All Rights Reserved.
 * -------------------------------------------------------------
 * filename:  ResourceBundleDecryptors.cs
 * author:    taoye
 * created:   2026/10/9
 * descrip:   Nova 资源格式的 YooAsset 解密适配
 ***************************************************************/

using System;
using System.IO;
using YooAsset;

namespace NovaFramework.Runtime
{
    /// <summary>全模式内存解密适配，适用于 WebGL 和需要原始字节的文件系统。</summary>
    public sealed class ResourceBundleMemoryDecryptor : IBundleMemoryDecryptor
    {
        private readonly string m_PackageName;
        /// <summary>绑定预期包名，防止资源在不同包之间误用。</summary>
        public ResourceBundleMemoryDecryptor(string packageName) { m_PackageName = packageName; }
        /// <summary>优先解码 YooAsset 提供的内存数据，否则读取本地文件；不修改输入数组。</summary>
        public byte[] GetDecryptedData(BundleDecryptArgs args) => ResourceEncryptionCodec.Decrypt(args.FileData ?? File.ReadAllBytes(args.FilePath), m_PackageName, ResourceKeyRegistry.Provider);
    }

    /// <summary>仅 Offset 模式的本地文件偏移加载适配。</summary>
    public sealed class ResourceBundleOffsetDecryptor : IBundleOffsetDecryptor
    {
        private readonly string m_PackageName;
        /// <summary>绑定预期包名；本适配器不依赖密钥提供器。</summary>
        public ResourceBundleOffsetDecryptor(string packageName) { m_PackageName = packageName; }
        /// <summary>返回已校验 Offset 文件的原始资源起点。</summary>
        public long GetFileOffset(BundleDecryptArgs args) => ResourceEncryptionCodec.GetOffset(args.FilePath, m_PackageName);
    }

    /// <summary>自动按文件头选择去头流、内存回退或 AES 分块认证流。</summary>
    public sealed class ResourceBundleStreamDecryptor : IBundleStreamDecryptor
    {
        private readonly string m_PackageName;
        /// <summary>绑定预期包名；返回的流由 YooAsset 持有并释放。</summary>
        public ResourceBundleStreamDecryptor(string packageName) { m_PackageName = packageName; }
        /// <summary>返回与 AES stream 默认块大小相同的读取缓冲建议。</summary>
        public int GetBufferSize(BundleDecryptArgs args) => ResourceEncryptionCodec.ChunkSize;
        /// <summary>创建支持随机访问的解密流，AES 块认证成功之前不返回其明文。</summary>
        public Stream CreateDecryptionStream(BundleDecryptArgs args) => ResourceEncryptionCodec.OpenDecryptionStream(args.FilePath, m_PackageName, ResourceKeyRegistry.Provider);
    }

    /// <summary>原生 AssetBundle 自动解密入口，算法由资源头决定，无需运行时算法配置。</summary>
    public sealed class ResourceBundleAutoDecryptor : IBundleStreamDecryptor
    {
        private readonly string m_PackageName;
        /// <summary>绑定预期包名；密钥按头部的 keyId 从业务提供器获取。</summary>
        public ResourceBundleAutoDecryptor(string packageName) { m_PackageName = packageName; }
        /// <summary>返回 64 KiB 读取缓冲建议。</summary>
        public int GetBufferSize(BundleDecryptArgs args) => ResourceEncryptionCodec.ChunkSize;
        /// <summary>按头部选择 Offset 只读流、XOR/AES memory 内存流或 AES stream 分块流。</summary>
        public Stream CreateDecryptionStream(BundleDecryptArgs args) => ResourceEncryptionCodec.OpenDecryptionStream(args.FilePath, m_PackageName, ResourceKeyRegistry.Provider);
    }

    /// <summary>独立清单解密适配；普通清单原样返回，完整 Nova 标识触发严格解密。</summary>
    public sealed class ResourceManifestDecryptor : IManifestDecryptor
    {
        private readonly string m_PackageName;
        private readonly IResourceKeyProvider m_KeyProvider;
        private readonly bool m_ReadPackageFromHeader;

        /// <summary>绑定客户端预期包名；每次解密读取当前已注册的客户端密钥提供器，保持既有调用语义。</summary>
        public ResourceManifestDecryptor(string packageName) { m_PackageName = packageName; }

        /// <summary>绑定指定密钥提供器；包名为 null 时从文件头读取，读取结果仍须通过解密认证。</summary>
        public ResourceManifestDecryptor(string packageName, IResourceKeyProvider keyProvider)
        {
            m_PackageName = packageName;
            m_KeyProvider = keyProvider ?? throw new ArgumentNullException(nameof(keyProvider));
            m_ReadPackageFromHeader = packageName == null;
        }

        /// <summary>对带完整标识的清单解密；未标识的数据交由 YooAsset 正常解析或拒绝。</summary>
        public byte[] Decrypt(byte[] fileData)
        {
            if (fileData == null) throw new ArgumentNullException(nameof(fileData));
            if (!ResourceEncryptionCodec.HasMagic(fileData)) return fileData;
            string packageName = m_ReadPackageFromHeader ? ResourceEncryptionCodec.GetPackageName(fileData) : m_PackageName;
            return ResourceEncryptionCodec.Decrypt(fileData, packageName, m_KeyProvider ?? ResourceKeyRegistry.Provider);
        }
    }
}
