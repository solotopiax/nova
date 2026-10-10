/***************************************************************
 * (c) copyright 2026 - 2030, Solotopia
 * All Rights Reserved.
 * -------------------------------------------------------------
 * filename:  ResourceEncryptionCodec.cs
 * author:    taoye
 * created:   2026/10/9
 * descrip:   带包标识和认证的资源文件编码
 ***************************************************************/

using System;
using System.IO;
using System.Security.Cryptography;
using System.Text;

namespace NovaFramework.Runtime
{
    /// <summary>Nova 资源编码器；AES 先认证后解密，Offset 和 XOR 仅为弱混淆。</summary>
    public static class ResourceEncryptionCodec
    {
        internal const int ChunkSize = 64 * 1024;
        internal static readonly byte[] Magic = Encoding.ASCII.GetBytes("NOVARES1");
        private static readonly Encoding Utf8 = new UTF8Encoding(false, true);

        /// <summary>将原始资源编码为指定模式；AES 需要 32 字节主密钥，不保存明文密钥。</summary>
        public static byte[] Encrypt(byte[] data, string packageName, string keyId, byte[] key, ResourceEncryptionMode mode)
        {
            if (data == null) throw new ArgumentNullException(nameof(data));
            ValidateName(packageName, nameof(packageName));
            if (mode < ResourceEncryptionMode.Offset || mode > ResourceEncryptionMode.AesStream)
                throw new ArgumentOutOfRangeException(nameof(mode));
            if (mode != ResourceEncryptionMode.Offset) ValidateName(keyId, nameof(keyId));
            if (mode != ResourceEncryptionMode.Offset) ValidateKey(key, IsAes(mode));
            byte[] header = BuildHeader(packageName, mode == ResourceEncryptionMode.Offset ? "" : keyId, data.LongLength, mode);
            using (var output = new MemoryStream())
            {
                output.Write(header, 0, header.Length);
                if (mode == ResourceEncryptionMode.Offset)
                    output.Write(data, 0, data.Length);
                else if (mode == ResourceEncryptionMode.Xor)
                {
                    byte[] encoded = Xor(data, key);
                    output.Write(encoded, 0, encoded.Length);
                }
                else
                {
                    byte[] encKey = Derive(key, "Nova.Resource.Encryption.v1");
                    byte[] authKey = Derive(key, "Nova.Resource.Authentication.v1");
                    try
                    {
                        byte[] headerTag = Tag(authKey, header);
                        output.Write(headerTag, 0, headerTag.Length);
                        int size = mode == ResourceEncryptionMode.AesStream ? ChunkSize : Math.Max(1, data.Length);
                        long index = 0;
                        for (int offset = 0; offset < data.Length;)
                        {
                            int count = Math.Min(size, data.Length - offset);
                            byte[] iv = RandomBytes(16);
                            byte[] cipher = Transform(data, offset, count, encKey, iv, true);
                            output.Write(iv, 0, iv.Length);
                            output.Write(cipher, 0, cipher.Length);
                            byte[] tag = ChunkTag(authKey, header, index++, count, iv, cipher);
                            output.Write(tag, 0, tag.Length);
                            offset += count;
                        }
                    }
                    finally { Array.Clear(encKey, 0, encKey.Length); Array.Clear(authKey, 0, authKey.Length); }
                }
                return output.ToArray();
            }
        }

        /// <summary>严格解码 Nova 资源格式；支持四种模式，流式 AES 在内存路径也逐块认证。</summary>
        public static byte[] Decrypt(byte[] data, string packageName, IResourceKeyProvider provider)
        {
            if (data == null) throw new ArgumentNullException(nameof(data));
            using (var input = new MemoryStream(data, false))
            {
                Header header = ReadHeader(input, packageName);
                if (header.Length > int.MaxValue) throw new InvalidDataException("资源过大，不能使用内存解密。");
                if (IsAes(header.Mode))
                {
                    using (var stream = new ResourceAuthenticatedStream(input, header, provider))
                    {
                        byte[] result = new byte[(int)header.Length];
                        ReadExactly(stream, result, 0, result.Length);
                        return result;
                    }
                }
                if (input.Length - input.Position != header.Length) throw new InvalidDataException("资源长度不匹配。");
                byte[] plain = new byte[(int)header.Length];
                ReadExactly(input, plain, 0, plain.Length);
                if (header.Mode == ResourceEncryptionMode.Offset) return plain;
                byte[] key = ResolveKey(provider, header.Package, header.KeyId, false);
                try { return Xor(plain, key); }
                finally { Array.Clear(key, 0, key.Length); }
            }
        }

        /// <summary>打开调用方拥有的解密流；AES stream 仅缓存当前认证块，Offset 去头定位，XOR 和 AES memory 使用内存回退。</summary>
        public static Stream OpenDecryptionStream(string path, string packageName, IResourceKeyProvider provider)
        {
            var input = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.Read);
            try
            {
                Header header = ReadHeader(input, packageName);
                if (header.Mode == ResourceEncryptionMode.Offset)
                    return new ResourceOffsetStream(input, input.Position, header.Length);
                if (header.Mode == ResourceEncryptionMode.AesStream)
                    return new ResourceAuthenticatedStream(input, header, provider);
                input.Dispose();
                byte[] plain = Decrypt(File.ReadAllBytes(path), packageName, provider);
                return new ResourceOffsetStream(new MemoryStream(plain, false), 0, plain.LongLength, plain);
            }
            catch { input.Dispose(); throw; }
        }

        /// <summary>校验包名、Offset 模式及文件长度，返回可供 YooAsset 跳过的头部长度。</summary>
        public static long GetOffset(string path, string packageName)
        {
            using (var input = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.Read))
            {
                Header header = ReadHeader(input, packageName);
                if (header.Mode != ResourceEncryptionMode.Offset || input.Length - input.Position != header.Length)
                    throw new InvalidDataException("资源不是有效的 Offset 文件。");
                return input.Position;
            }
        }

        /// <summary>严格解析资源头并返回包名；包名在解密前尚未认证，调用方必须继续执行解密校验。</summary>
        public static string GetPackageName(byte[] data)
        {
            if (data == null) throw new ArgumentNullException(nameof(data));
            if (data.Length < 42 || !HasMagic(data)) throw new InvalidDataException("资源头部不完整。");
            int size = data[10] | data[11] << 8;
            if (size == 0 || size > 4096 || size > data.Length - 42) throw new InvalidDataException("资源包标识长度不合法。");
            string packageName = Utf8.GetString(data, 42, size);
            using (var input = new MemoryStream(data, false)) return ReadHeader(input, packageName).Package;
        }

        /// <summary>严格解析头部并返回算法；读取结果尚未认证，复用文件前必须解密验证。</summary>
        public static ResourceEncryptionMode GetMode(byte[] data)
        {
            string packageName = GetPackageName(data);
            using (var input = new MemoryStream(data, false)) return ReadHeader(input, packageName).Mode;
        }

        /// <summary>严格解析头部并返回密钥标识；读取结果尚未认证，必须继续解密验证。</summary>
        public static string GetKeyId(byte[] data)
        {
            string packageName = GetPackageName(data);
            using (var input = new MemoryStream(data, false)) return ReadHeader(input, packageName).KeyId;
        }

        /// <summary>仅完整匹配 Nova 标识时返回 true，避免将普通清单误判为加密文件。</summary>
        internal static bool HasMagic(byte[] data)
        {
            if (data == null || data.Length < Magic.Length) return false;
            for (int i = 0; i < Magic.Length; i++) if (data[i] != Magic[i]) return false;
            return true;
        }

        /// <summary>记录已验证的头部字段和原始头部字节，供认证绑定使用。</summary>
        internal sealed class Header
        {
            internal ResourceEncryptionMode Mode;
            internal string Package, KeyId;
            internal long Length;
            internal int Chunk;
            internal byte[] Bytes;
        }

        /// <summary>读取有界头部并拒绝未知版本、算法、包名及不合法长度。</summary>
        internal static Header ReadHeader(Stream input, string packageName)
        {
            ValidateName(packageName, nameof(packageName));
            byte[] fixedPart = new byte[42];
            ReadExactly(input, fixedPart, 0, fixedPart.Length);
            if (!HasMagic(fixedPart) || fixedPart[8] != 1) throw new InvalidDataException("资源格式或版本不匹配。");
            var mode = (ResourceEncryptionMode)fixedPart[9];
            if (mode < ResourceEncryptionMode.Offset || mode > ResourceEncryptionMode.AesStream)
                throw new InvalidDataException("未知资源算法。");
            int packageSize = fixedPart[10] | fixedPart[11] << 8;
            int keySize = fixedPart[12] | fixedPart[13] << 8;
            if (packageSize == 0 || packageSize > 4096 || keySize > 4096) throw new InvalidDataException("资源头部标识长度不合法。");
            long length;
            int chunk;
            using (var reader = new BinaryReader(new MemoryStream(fixedPart, false)))
            {
                reader.BaseStream.Position = 14;
                length = reader.ReadInt64();
                chunk = reader.ReadInt32();
            }
            if (length < 0 || chunk != (mode == ResourceEncryptionMode.AesStream ? ChunkSize : 0))
                throw new InvalidDataException("资源长度或分块参数不合法。");
            byte[] header = new byte[42 + packageSize + keySize];
            Buffer.BlockCopy(fixedPart, 0, header, 0, 42);
            ReadExactly(input, header, 42, header.Length - 42);
            string package = Utf8.GetString(header, 42, packageSize);
            string keyId = Utf8.GetString(header, 42 + packageSize, keySize);
            if (!string.Equals(package, packageName, StringComparison.Ordinal)) throw new InvalidDataException("资源包名不匹配。");
            if ((mode == ResourceEncryptionMode.Offset) != (keySize == 0)) throw new InvalidDataException("资源密钥标识不合法。");
            return new Header { Mode = mode, Package = package, KeyId = keyId, Length = length, Chunk = chunk, Bytes = header };
        }

        /// <summary>生成包含随机文件标识的头部，防止同一密钥下不同文件的认证块互换。</summary>
        private static byte[] BuildHeader(string package, string keyId, long length, ResourceEncryptionMode mode)
        {
            byte[] packageBytes = Utf8.GetBytes(package), keyBytes = Utf8.GetBytes(keyId);
            using (var output = new MemoryStream())
            using (var writer = new BinaryWriter(output, Utf8, true))
            {
                writer.Write(Magic); writer.Write((byte)1); writer.Write((byte)mode);
                writer.Write((ushort)packageBytes.Length); writer.Write((ushort)keyBytes.Length);
                writer.Write(length); writer.Write(mode == ResourceEncryptionMode.AesStream ? ChunkSize : 0);
                writer.Write(RandomBytes(16)); writer.Write(packageBytes); writer.Write(keyBytes);
                return output.ToArray();
            }
        }

        /// <summary>读取指定字节数；截断输入立即拒绝，避免使用未完整读取的数据。</summary>
        internal static void ReadExactly(Stream input, byte[] buffer, int offset, int count)
        {
            while (count > 0)
            {
                int read = input.Read(buffer, offset, count);
                if (read <= 0) throw new EndOfStreamException("资源文件被截断。");
                offset += read; count -= read;
            }
        }

        /// <summary>判断当前模式是否需要 AES 主密钥和完整性认证。</summary>
        internal static bool IsAes(ResourceEncryptionMode mode) => mode == ResourceEncryptionMode.AesMemory || mode == ResourceEncryptionMode.AesStream;

        /// <summary>校验标识非空且 UTF-8 编码不超过头部限制。</summary>
        private static void ValidateName(string value, string argument)
        {
            if (string.IsNullOrWhiteSpace(value) || Utf8.GetByteCount(value) > 4096) throw new ArgumentException("资源标识为空或过长。", argument);
        }

        /// <summary>校验弱混淆密钥非空，或 AES 主密钥恰好为 32 字节。</summary>
        private static void ValidateKey(byte[] key, bool aes)
        {
            if (key == null || key.Length == 0 || (aes && key.Length != 32)) throw new CryptographicException("资源密钥无效；AES 主密钥必须为 32 字节。");
        }

        /// <summary>冻结业务密钥副本；解密过程不会修改提供器持有的密钥数组。</summary>
        internal static byte[] ResolveKey(IResourceKeyProvider provider, string package, string keyId, bool aes)
        {
            byte[] key = provider?.GetKey(package, keyId);
            ValidateKey(key, aes);
            return (byte[])key.Clone();
        }

        /// <summary>通过不同域标签派生独立的 AES 与 HMAC 密钥。</summary>
        internal static byte[] Derive(byte[] key, string domain)
        {
            using (var hmac = new HMACSHA256(key)) return hmac.ComputeHash(Encoding.ASCII.GetBytes(domain));
        }

        /// <summary>使用平台密码学随机源生成 IV 或文件标识。</summary>
        private static byte[] RandomBytes(int count)
        {
            byte[] bytes = new byte[count];
            using (var rng = RandomNumberGenerator.Create()) rng.GetBytes(bytes);
            return bytes;
        }

        /// <summary>使用 CBC 与 PKCS7 编码；解密只允许在对应 HMAC 已通过后调用。</summary>
        internal static byte[] Transform(byte[] data, int offset, int count, byte[] key, byte[] iv, bool encrypt)
        {
            using (var aes = Aes.Create())
            {
                aes.Mode = CipherMode.CBC; aes.Padding = PaddingMode.PKCS7; aes.Key = key; aes.IV = iv;
                using (var transform = encrypt ? aes.CreateEncryptor() : aes.CreateDecryptor())
                    return transform.TransformFinalBlock(data, offset, count);
            }
        }

        /// <summary>计算完整头部认证码，包括算法、包名、长度、密钥标识和文件标识。</summary>
        internal static byte[] Tag(byte[] key, byte[] bytes)
        {
            using (var hmac = new HMACSHA256(key)) return hmac.ComputeHash(bytes);
        }

        /// <summary>将头部、块序号、明文长度、IV 和密文绑定到同一认证码。</summary>
        internal static byte[] ChunkTag(byte[] key, byte[] header, long index, int length, byte[] iv, byte[] cipher)
        {
            using (var hmac = new HMACSHA256(key))
            using (var prefix = new MemoryStream())
            using (var writer = new BinaryWriter(prefix, Utf8, true))
            {
                writer.Write(header); writer.Write(index); writer.Write(length); writer.Write(iv);
                byte[] bytes = prefix.ToArray();
                hmac.TransformBlock(bytes, 0, bytes.Length, bytes, 0);
                hmac.TransformFinalBlock(cipher, 0, cipher.Length);
                return hmac.Hash;
            }
        }

        /// <summary>以固定长度比较认证码，失败时拒绝返回任何该块明文。</summary>
        internal static void Verify(byte[] actual, byte[] expected)
        {
            int difference = actual.Length ^ expected.Length;
            for (int i = 0; i < Math.Min(actual.Length, expected.Length); i++) difference |= actual[i] ^ expected[i];
            if (difference != 0) throw new CryptographicException("资源认证失败。");
        }

        /// <summary>返回循环异或后的新数组；此模式不提供任何认证或强保密性。</summary>
        private static byte[] Xor(byte[] data, byte[] key)
        {
            byte[] result = new byte[data.Length];
            for (int i = 0; i < data.Length; i++) result[i] = (byte)(data[i] ^ key[i % key.Length]);
            return result;
        }
    }
}
