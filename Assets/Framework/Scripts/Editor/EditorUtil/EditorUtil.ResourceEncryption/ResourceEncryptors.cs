/***************************************************************
 * (c) copyright 2026 - 2030, Solotopia
 * All Rights Reserved.
 * filename:  ResourceEncryptors.cs
 * author:    taoye
 * created:   2026/10/9
 * descrip:   YooAsset 与 Pipify 共用的资源加密器
 ***************************************************************/
using System;
using System.IO;
using System.Linq;
using NovaFramework.Runtime;
using UnityEditor;
using YooAsset;
using Coord = NovaFramework.Editor.EditorUtil.Config.DimensionProjector.Coord;

namespace NovaFramework.Editor
{
    /// <summary>构建期间按包裹读取本地密钥，不注册或覆盖运行时提供器。</summary>
    internal sealed class EditorResourceKeyProvider : IResourceKeyProvider
    {
        /// <summary>只返回指定编号的密钥，缺失时阻断，不猜测其他密钥。</summary>
        public byte[] GetKey(string packageName, string keyId)
        {
            var key = ResourceKeyStore.GetPackage(packageName).Keys.FirstOrDefault(k => k.KeyId == keyId);
            if (key == null) throw new InvalidDataException("资源加解密配置缺少所需密钥编号。");
            return Convert.FromBase64String(key.KeyBase64);
        }
    }
    /// <summary>所有 Nova 加密器共用的构建服务，复用前必须认证并比较原始内容。</summary>
    internal static class ResourceEncryptionBuildService
    {
        // YooAsset 清单接口不携带目标；Nova 构建入口在同步管线期间显式提供并释放坐标。
        private static BuildKeyContext s_Context;

        /// <summary>一次构建的坐标快照；嵌套构建结束恢复上层，失败也不残留状态。</summary>
        private sealed class BuildKeyContext : IDisposable
        {
            internal readonly string Package;
            internal readonly Coord Coordinate;
            private readonly BuildKeyContext m_Previous;
            /// <summary>保存本次包名与坐标，并接管当前同步构建上下文。</summary>
            internal BuildKeyContext(string package, Coord coord)
            {
                Package = package;
                Coordinate = coord;
                m_Previous = s_Context;
                s_Context = this;
            }
            /// <summary>构建成功或失败后恢复进入前的上下文。</summary>
            public void Dispose() { s_Context = m_Previous; }
        }

        /// <summary>为 Nova/Pipify 的 Bundle 与清单加密冻结实际目标和已保存渠道、模式。</summary>
        internal static IDisposable EnterBuild(string package, BuildTarget target, IBundleEncryptor bundle, IManifestEncryptor manifest)
        {
            bool needsKeys = (bundle is ResourceBundleEncryptor && !(bundle is ResourceOffsetEncryptor)) || manifest is ResourceManifestEncryptor;
            Coord coord = default;
            if (needsKeys)
            {
                ResourceKeyStoreData data = ResourceKeyStore.Load();
                coord = ResourceKeyStore.GetBuildCoord(data.DimensionMask, EditorUtil.Config.ActivePlatform.FromBuildTarget(target));
            }
            return new BuildKeyContext(package, coord);
        }

        /// <summary>取构建明确坐标；官方清单缺少平台且配置按平台分维时拒绝猜测。</summary>
        private static Coord ResolveCoord(ResourceKeyStoreData data, string package, PlatformType? platform)
        {
            if (s_Context != null)
            {
                if (s_Context.Package != package || (platform.HasValue && platform.Value != s_Context.Coordinate.Platform && data.DimensionMask.ByPlatform))
                    throw new InvalidDataException("资源加密的包名或目标平台与本次构建不一致。");
                return s_Context.Coordinate;
            }
            if (!platform.HasValue && data.DimensionMask.ByPlatform)
                throw new InvalidDataException("Nova 清单加密需要明确目标平台。资源密钥按平台分别配置时，请使用 Pipify 构建资源；官方清单加密接口不提供目标平台。");
            return ResourceKeyStore.GetBuildCoord(data.DimensionMask, platform);
        }
        /// <summary>在 Nova 构建入口提前检查密钥和清单配套，避免构建完成后才发现 Catalog 无法读取。</summary>
        internal static void ValidateSelection(string packageName, IBundleEncryptor bundle, IManifestEncryptor manifest, IManifestDecryptor decryptor)
        {
            Type expectedDecryptor = manifest is ResourceManifestAesEncryptor ? typeof(ResourceManifestAesDecryptor)
                : manifest is ResourceManifestXorEncryptor ? typeof(ResourceManifestXorDecryptor) : null;
            if (expectedDecryptor != null && decryptor?.GetType() != expectedDecryptor)
                throw new InvalidOperationException($"{manifest.GetType().Name} 必须配套选择 {expectedDecryptor.Name}。");
            if ((bundle is ResourceBundleEncryptor && !(bundle is ResourceOffsetEncryptor)) || manifest is ResourceManifestEncryptor)
            {
                ResourceKeyStoreData data = ResourceKeyStore.Load();
                ResourceKeyStore.GetActiveKey(data, packageName, ResolveCoord(data, packageName, null));
            }
        }
        /// <summary>从 YooAsset 管线的包裹输出目录确定包名；不依赖全局当前窗口选择。</summary>
        internal static string GetBundlePackage(string filePath)
        {
            var cache = Directory.GetParent(System.IO.Path.GetFullPath(filePath));
            if (cache == null || cache.Name != "OutputCache" || cache.Parent == null)
                throw new InvalidDataException("Nova 资源加密器要求使用 YooAsset 默认 OutputCache 构建目录。");
            return cache.Parent.Name;
        }

        /// <summary>严格读取官方缓存路径的 BuildTarget，不使用编辑窗口或上次构建的平台。</summary>
        internal static PlatformType GetBundlePlatform(string filePath)
        {
            GetBundlePackage(filePath);
            DirectoryInfo target = Directory.GetParent(System.IO.Path.GetFullPath(filePath))?.Parent?.Parent;
            if (target == null || !Enum.TryParse(target.Name, out BuildTarget value) || !Enum.IsDefined(typeof(BuildTarget), value))
                throw new InvalidDataException("资源加密无法识别 YooAsset 输出路径中的目标平台。");
            return EditorUtil.Config.ActivePlatform.FromBuildTarget(value);
        }
        /// <summary>编码资源；模式、密钥编号和原始内容一致时复用已有密文，保留随机 IV 安全性。</summary>
        internal static byte[] Encode(byte[] data, string packageName, ResourceEncryptionMode mode, string existingPath = null, PlatformType? platform = null)
        {
            string id = "";
            byte[] key = null;
            if (mode != ResourceEncryptionMode.Offset)
            {
                ResourceKeyStoreData keys = ResourceKeyStore.Load();
                var entry = ResourceKeyStore.GetActiveKey(keys, packageName, ResolveCoord(keys, packageName, platform));
                id = entry.KeyId;
                key = Convert.FromBase64String(entry.KeyBase64);
            }
            if (existingPath != null && File.Exists(existingPath))
            {
                try
                {
                    byte[] existing = File.ReadAllBytes(existingPath);
                    if (ResourceEncryptionCodec.GetMode(existing) == mode &&
                        ResourceEncryptionCodec.GetPackageName(existing) == packageName &&
                        ResourceEncryptionCodec.GetKeyId(existing) == id &&
                        ResourceEncryptionCodec.Decrypt(existing, packageName, new EditorResourceKeyProvider()).SequenceEqual(data))
                    {
                        if (key != null) Array.Clear(key, 0, key.Length);
                        return existing;
                    }
                }
                catch (Exception error) when (error is InvalidDataException || error is System.Security.Cryptography.CryptographicException || error is ArgumentException || error is InvalidOperationException || error is EndOfStreamException)
                { /* 已有产物损坏或密钥变化时重新加密；不输出密钥或密文。 */ }
            }
            try { return ResourceEncryptionCodec.Encrypt(data, packageName, id, key, mode); }
            finally { if (key != null) Array.Clear(key, 0, key.Length); }
        }
        /// <summary>从官方明文清单头读取包名，避免编辑器当前包裹状态影响 CLI 构建。</summary>
        internal static string GetManifestPackage(byte[] data)
        {
            using (var reader = new BinaryReader(new MemoryStream(data)))
            {
                if (reader.ReadUInt32() != PackageManifestConsts.FileMagic || reader.ReadInt32() != PackageManifestConsts.FileVersion)
                    throw new InvalidDataException("无法识别 YooAsset 清单格式。");
                reader.ReadBytes(5);
                reader.ReadInt32(); reader.ReadInt32();
                ReadString(reader);
                return ReadString(reader);
            }
        }
        /// <summary>读取官方清单的 ushort 长度 UTF-8 字符串；截断时拒绝继续。</summary>
        private static string ReadString(BinaryReader reader)
        {
            int size = reader.ReadUInt16();
            var bytes = reader.ReadBytes(size);
            if (bytes.Length != size) throw new EndOfStreamException("清单字符串被截断。");
            return System.Text.Encoding.UTF8.GetString(bytes);
        }
    }
    /// <summary>资源包加密公共实现；具体类型供官方构建页面及 Pipify 下拉选择。</summary>
    public abstract class ResourceBundleEncryptor : IBundleEncryptor
    {
        /// <summary>具体文件编码模式。</summary>
        protected abstract ResourceEncryptionMode Mode { get; }
        /// <summary>处理管线原始 Bundle；缺少密钥在生成密文之前阻断构建。</summary>
        public BundleEncryptResult Encrypt(BundleEncryptArgs args)
        {
            string package = ResourceEncryptionBuildService.GetBundlePackage(args.FilePath);
            byte[] data = File.ReadAllBytes(args.FilePath);
            return new BundleEncryptResult(true, ResourceEncryptionBuildService.Encode(data, package, Mode, args.FilePath + ".encrypt",
                ResourceEncryptionBuildService.GetBundlePlatform(args.FilePath)));
        }
    }
    /// <summary>Offset 只添加头部，不需要密钥，不提供保密性。</summary>
    public sealed class ResourceOffsetEncryptor : ResourceBundleEncryptor { protected override ResourceEncryptionMode Mode => ResourceEncryptionMode.Offset; }
    /// <summary>XOR 循环异或弱混淆，使用包裹当前密钥。</summary>
    public sealed class ResourceXorEncryptor : ResourceBundleEncryptor { protected override ResourceEncryptionMode Mode => ResourceEncryptionMode.Xor; }
    /// <summary>AES-256 CBC 与 HMAC 整文件认证，运行时内存解密。</summary>
    public sealed class ResourceAesMemoryEncryptor : ResourceBundleEncryptor { protected override ResourceEncryptionMode Mode => ResourceEncryptionMode.AesMemory; }
    /// <summary>AES-256 CBC 与 HMAC 分块认证，原生平台运行时随机访问。</summary>
    public sealed class ResourceAesStreamEncryptor : ResourceBundleEncryptor { protected override ResourceEncryptionMode Mode => ResourceEncryptionMode.AesStream; }
    /// <summary>清单加密器公共实现；清单只提供内存模式。</summary>
    public abstract class ResourceManifestEncryptor : IManifestEncryptor
    {
        /// <summary>清单编码模式。</summary>
        protected abstract ResourceEncryptionMode Mode { get; }
        /// <summary>读取清单包名后使用包裹当前密钥独立加密。</summary>
        public byte[] Encrypt(byte[] fileData) => ResourceEncryptionBuildService.Encode(fileData, ResourceEncryptionBuildService.GetManifestPackage(fileData), Mode);
    }
    /// <summary>XOR 清单混淆，与 Bundle 方案独立选择。</summary>
    public sealed class ResourceManifestXorEncryptor : ResourceManifestEncryptor { protected override ResourceEncryptionMode Mode => ResourceEncryptionMode.Xor; }
    /// <summary>AES 清单加密及完整性认证，与 Bundle 方案独立选择。</summary>
    public sealed class ResourceManifestAesEncryptor : ResourceManifestEncryptor { protected override ResourceEncryptionMode Mode => ResourceEncryptionMode.AesMemory; }
    /// <summary>构建清单解密公共实现；只接受具体类型负责的模式，密钥来自已保存的 ConfigMaster。</summary>
    internal sealed class ResourceManifestBuildDecoder
    {
        private readonly ResourceManifestDecryptor m_Decryptor = new ResourceManifestDecryptor(null, new EditorResourceKeyProvider());
        private readonly ResourceEncryptionMode m_Mode;

        /// <summary>绑定具体清单解密器负责的编码模式，不作为 YooAsset 可选接口实现暴露。</summary>
        internal ResourceManifestBuildDecoder(ResourceEncryptionMode mode) { m_Mode = mode; }

        /// <summary>明文原样返回；Nova 密文须符合本类型模式，再委托 Runtime 实现解密认证。</summary>
        public byte[] Decrypt(byte[] fileData)
        {
            if (fileData == null) throw new ArgumentNullException(nameof(fileData));
            if (ResourceEncryptionCodec.HasMagic(fileData) && ResourceEncryptionCodec.GetMode(fileData) != m_Mode)
                throw new InvalidDataException("清单加密格式与所选解密器不匹配，请选择对应的清单解密器。");
            return m_Decryptor.Decrypt(fileData);
        }
    }

    /// <summary>AES 清单解密及完整性认证，与 ResourceManifestAesEncryptor 配套。</summary>
    public sealed class ResourceManifestAesDecryptor : IManifestDecryptor
    {
        private readonly ResourceManifestBuildDecoder m_Decoder = new ResourceManifestBuildDecoder(ResourceEncryptionMode.AesMemory);
        /// <summary>仅解密 AES 清单；明文原样返回，其他加密模式明确拒绝。</summary>
        public byte[] Decrypt(byte[] fileData) => m_Decoder.Decrypt(fileData);
    }
    /// <summary>XOR 清单解密，与 ResourceManifestXorEncryptor 配套。</summary>
    public sealed class ResourceManifestXorDecryptor : IManifestDecryptor
    {
        private readonly ResourceManifestBuildDecoder m_Decoder = new ResourceManifestBuildDecoder(ResourceEncryptionMode.Xor);
        /// <summary>仅解密 XOR 清单；明文原样返回，其他加密模式明确拒绝。</summary>
        public byte[] Decrypt(byte[] fileData) => m_Decoder.Decrypt(fileData);
    }

}
