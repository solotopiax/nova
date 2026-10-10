/***************************************************************
 * (c) copyright 2026 - 2030, Solotopia
 * All Rights Reserved.
 * -------------------------------------------------------------
 * filename:  ResourceAuthenticatedStream.cs
 * author:    taoye
 * created:   2026/10/9
 * descrip:   资源 AES 分块认证随机访问流
 ***************************************************************/

using System;
using System.IO;
using System.Security.Cryptography;

namespace NovaFramework.Runtime
{
    /// <summary>只缓存当前已认证明文块的只读流；锁保护位置、文件访问与释放。</summary>
    internal sealed class ResourceAuthenticatedStream : Stream
    {
        private readonly object m_Gate = new object();
        private readonly Stream m_Input;
        private readonly ResourceEncryptionCodec.Header m_Header;
        private readonly byte[] m_EncKey, m_AuthKey;
        private readonly long m_DataOffset;
        private readonly long m_BlockSize;
        private long m_Position, m_CachedIndex = -1;
        private byte[] m_Cached;
        private bool m_Disposed;

        /// <summary>冻结密钥并认证头部及物理长度；构造失败时由调用方释放输入流。</summary>
        internal ResourceAuthenticatedStream(Stream input, ResourceEncryptionCodec.Header header, IResourceKeyProvider provider)
        {
            m_Input = input; m_Header = header;
            byte[] key = ResourceEncryptionCodec.ResolveKey(provider, header.Package, header.KeyId, true);
            try
            {
                m_EncKey = ResourceEncryptionCodec.Derive(key, "Nova.Resource.Encryption.v1");
                m_AuthKey = ResourceEncryptionCodec.Derive(key, "Nova.Resource.Authentication.v1");
            }
            finally { Array.Clear(key, 0, key.Length); }
            try
            {
                byte[] tag = new byte[32];
                ResourceEncryptionCodec.ReadExactly(input, tag, 0, 32);
                ResourceEncryptionCodec.Verify(tag, ResourceEncryptionCodec.Tag(m_AuthKey, header.Bytes));
                m_DataOffset = input.Position;
                m_BlockSize = header.Mode == ResourceEncryptionMode.AesStream ? header.Chunk : Math.Max(1, header.Length);
                long blocks = header.Length == 0 ? 0 : (header.Length - 1) / m_BlockSize + 1;
                long expected = m_DataOffset;
                if (blocks > 0)
                {
                    long last = header.Length - (blocks - 1) * m_BlockSize;
                    expected = checked(expected + (blocks - 1) * RecordSize(m_BlockSize) + RecordSize(last));
                }
                if (input.Length != expected) throw new InvalidDataException("认证资源物理长度不匹配。");
            }
            catch
            {
                Array.Clear(m_EncKey, 0, m_EncKey.Length); Array.Clear(m_AuthKey, 0, m_AuthKey.Length);
                throw;
            }
        }

        /// <summary>兜底释放未被加载调用方接管的流；终结器运行时间不确定，不替代正常即时释放。</summary>
        ~ResourceAuthenticatedStream()
        {
            try { Dispose(false); }
            catch { /* 终结器不能向运行时抛出底层流释放异常。 */ }
        }

        /// <summary>返回是否仍支持读取。</summary>
        public override bool CanRead { get { lock (m_Gate) return !m_Disposed; } }
        /// <summary>返回是否仍支持随机访问。</summary>
        public override bool CanSeek { get { lock (m_Gate) return !m_Disposed; } }
        /// <summary>认证流始终不可写。</summary>
        public override bool CanWrite => false;
        /// <summary>返回已认证头部中的明文长度。</summary>
        public override long Length { get { lock (m_Gate) { EnsureOpen(); return m_Header.Length; } } }
        /// <summary>读取或设置明文位置，允许移动到文件末尾之后。</summary>
        public override long Position
        {
            get { lock (m_Gate) { EnsureOpen(); return m_Position; } }
            set { Seek(value, SeekOrigin.Begin); }
        }

        /// <summary>在块认证成功后复制明文；跨块读取按块推进，不暴露未认证块内容。</summary>
        public override int Read(byte[] buffer, int offset, int count)
        {
            if (buffer == null) throw new ArgumentNullException(nameof(buffer));
            if (offset < 0 || count < 0 || offset > buffer.Length - count) throw new ArgumentOutOfRangeException(nameof(offset));
            lock (m_Gate)
            {
                EnsureOpen();
                try
                {
                    int total = 0;
                    while (count > 0 && m_Position < m_Header.Length)
                    {
                        long index = m_Position / m_BlockSize;
                        LoadBlock(index);
                        int local = (int)(m_Position % m_BlockSize);
                        int read = Math.Min(count, m_Cached.Length - local);
                        Buffer.BlockCopy(m_Cached, local, buffer, offset, read);
                        offset += read; count -= read; total += read; m_Position += read;
                    }
                    return total;
                }
                catch (Exception exception) when (exception is CryptographicException || exception is IOException)
                {
                    // 故障流不再可用，立即清零并关闭；释放异常不能覆盖最初的认证或读取错误。
                    try { Dispose(); } catch { }
                    throw;
                }
            }
        }

        /// <summary>按明文位置移动；拒绝负位置与溢出，不触发预读。</summary>
        public override long Seek(long offset, SeekOrigin origin)
        {
            lock (m_Gate)
            {
                EnsureOpen();
                long target;
                switch (origin)
                {
                    case SeekOrigin.Begin: target = offset; break;
                    case SeekOrigin.Current: target = checked(m_Position + offset); break;
                    case SeekOrigin.End: target = checked(m_Header.Length + offset); break;
                    default: throw new ArgumentOutOfRangeException(nameof(origin));
                }
                if (target < 0) throw new IOException("不能移动到资源起点之前。");
                return m_Position = target;
            }
        }

        /// <summary>只读流无写缓冲，检查流尚未释放。</summary>
        public override void Flush() { lock (m_Gate) EnsureOpen(); }
        /// <summary>拒绝更改认证资源长度。</summary>
        public override void SetLength(long value) => throw new NotSupportedException();
        /// <summary>拒绝写入认证资源。</summary>
        public override void Write(byte[] buffer, int offset, int count) => throw new NotSupportedException();

        /// <summary>与读取同步释放底层文件并清零密钥；终结器路径也关闭托管文件流，防止加载接管失败后遗留句柄。</summary>
        protected override void Dispose(bool disposing)
        {
            lock (m_Gate)
            {
                if (!m_Disposed)
                {
                    m_Disposed = true;
                    if (m_EncKey != null) Array.Clear(m_EncKey, 0, m_EncKey.Length);
                    if (m_AuthKey != null) Array.Clear(m_AuthKey, 0, m_AuthKey.Length);
                    if (m_Cached != null) Array.Clear(m_Cached, 0, m_Cached.Length);
                    m_Cached = null;
                    if (m_Input != null) m_Input.Dispose();
                }
            }
            base.Dispose(disposing);
        }

        /// <summary>读取目标记录，先验证完整块认证码再进行 CBC 解密及缓存替换。</summary>
        private void LoadBlock(long index)
        {
            if (m_CachedIndex == index) return;
            int length = checked((int)Math.Min(m_BlockSize, m_Header.Length - index * m_BlockSize));
            int cipherLength = checked((int)((length / 16L + 1) * 16));
            m_Input.Position = checked(m_DataOffset + index * RecordSize(m_BlockSize));
            byte[] iv = new byte[16], cipher = new byte[cipherLength], tag = new byte[32];
            ResourceEncryptionCodec.ReadExactly(m_Input, iv, 0, iv.Length);
            ResourceEncryptionCodec.ReadExactly(m_Input, cipher, 0, cipher.Length);
            ResourceEncryptionCodec.ReadExactly(m_Input, tag, 0, tag.Length);
            ResourceEncryptionCodec.Verify(tag, ResourceEncryptionCodec.ChunkTag(m_AuthKey, m_Header.Bytes, index, length, iv, cipher));
            byte[] plain = ResourceEncryptionCodec.Transform(cipher, 0, cipher.Length, m_EncKey, iv, false);
            if (plain.Length != length) { Array.Clear(plain, 0, plain.Length); throw new CryptographicException("认证块明文长度不匹配。"); }
            if (m_Cached != null) Array.Clear(m_Cached, 0, m_Cached.Length);
            m_Cached = plain; m_CachedIndex = index;
        }

        /// <summary>计算 CBC 填充密文与 IV、认证码的固定记录尺寸。</summary>
        private static long RecordSize(long length) => checked((length / 16 + 1) * 16 + 48);

        /// <summary>已释放实例拒绝读取、定位或长度访问。</summary>
        private void EnsureOpen()
        {
            if (m_Disposed) throw new ObjectDisposedException(nameof(ResourceAuthenticatedStream));
        }
    }
}
