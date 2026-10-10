/***************************************************************
 * (c) copyright 2026 - 2030, Solotopia
 * All Rights Reserved.
 * -------------------------------------------------------------
 * filename:  ResourceOffsetStream.cs
 * author:    taoye
 * created:   2026/10/9
 * descrip:   去除资源偏移头的随机访问只读流
 ***************************************************************/

using System;
using System.IO;

namespace NovaFramework.Runtime
{
    /// <summary>以明文起点包装底层文件，不驻留整包；锁保护读取、定位与释放。</summary>
    internal sealed class ResourceOffsetStream : Stream
    {
        private readonly object m_Gate = new object();
        private readonly Stream m_Input;
        private readonly long m_Offset, m_Length;
        private readonly byte[] m_ClearBuffer;
        private long m_Position;
        private bool m_Disposed;

        /// <summary>接管底层流所有权并校验长度；内存回退可交付需要在释放时清零的明文数组。</summary>
        internal ResourceOffsetStream(Stream input, long offset, long length, byte[] clearBuffer = null)
        {
            if (input.Length - offset != length) throw new InvalidDataException("Offset 文件长度不匹配。");
            m_Input = input; m_Offset = offset; m_Length = length; m_ClearBuffer = clearBuffer;
        }
        /// <summary>兜底关闭未被加载调用方接管的流；执行时间不确定，不能替代正常显式释放。</summary>
        ~ResourceOffsetStream()
        {
            try { Dispose(false); }
            catch { /* 终结器不能传播底层流释放异常。 */ }
        }

        /// <summary>返回流是否仍可读取。</summary>
        public override bool CanRead { get { lock (m_Gate) return !m_Disposed; } }
        /// <summary>返回流是否仍可定位。</summary>
        public override bool CanSeek { get { lock (m_Gate) return !m_Disposed; } }
        /// <summary>此流不支持写入。</summary>
        public override bool CanWrite => false;
        /// <summary>返回头部之后的原始数据长度。</summary>
        public override long Length { get { lock (m_Gate) { EnsureOpen(); return m_Length; } } }
        /// <summary>获取或设置相对原始数据的读取位置。</summary>
        public override long Position
        {
            get { lock (m_Gate) { EnsureOpen(); return m_Position; } }
            set { Seek(value, SeekOrigin.Begin); }
        }
        /// <summary>从原始数据区域读取，永远不返回头部字节。</summary>
        public override int Read(byte[] buffer, int offset, int count)
        {
            if (buffer == null) throw new ArgumentNullException(nameof(buffer));
            if (offset < 0 || count < 0 || offset > buffer.Length - count) throw new ArgumentOutOfRangeException(nameof(offset));
            lock (m_Gate)
            {
                EnsureOpen();
                if (m_Position >= m_Length) return 0;
                try
                {
                    m_Input.Position = checked(m_Offset + m_Position);
                    int read = m_Input.Read(buffer, offset, (int)Math.Min(count, m_Length - m_Position));
                    if (read == 0 && count > 0) throw new EndOfStreamException("Offset 资源在读取过程中被截断。");
                    m_Position += read;
                    return read;
                }
                catch (IOException)
                {
                    // I/O 故障立即关闭底层流；仍向调用方报告最初的读取异常。
                    try { Dispose(); } catch { }
                    throw;
                }
            }
        }
        /// <summary>按原始数据长度定位，允许定位到末尾之后，拒绝负位置。</summary>
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
                    case SeekOrigin.End: target = checked(m_Length + offset); break;
                    default: throw new ArgumentOutOfRangeException(nameof(origin));
                }
                if (target < 0) throw new IOException("不能移动到资源起点之前。");
                return m_Position = target;
            }
        }
        /// <summary>只读流无缓冲写入，校验释放状态。</summary>
        public override void Flush() { lock (m_Gate) EnsureOpen(); }
        /// <summary>拒绝调整原始数据长度。</summary>
        public override void SetLength(long value) => throw new NotSupportedException();
        /// <summary>拒绝向原始数据写入。</summary>
        public override void Write(byte[] buffer, int offset, int count) => throw new NotSupportedException();
        /// <summary>同步释放底层流并清零内存明文；终结器路径同样关闭托管文件流。</summary>
        protected override void Dispose(bool disposing)
        {
            lock (m_Gate)
            {
                if (!m_Disposed)
                {
                    m_Disposed = true;
                    if (m_ClearBuffer != null) Array.Clear(m_ClearBuffer, 0, m_ClearBuffer.Length);
                    if (m_Input != null) m_Input.Dispose();
                }
            }
            base.Dispose(disposing);
        }
        /// <summary>阻止已经释放的流继续访问底层文件。</summary>
        private void EnsureOpen()
        {
            if (m_Disposed) throw new ObjectDisposedException(nameof(ResourceOffsetStream));
        }
    }
}
