/***************************************************************
 * (c) copyright 2026 - 2030, Solotopia
 * All Rights Reserved.
 * -------------------------------------------------------------
 * filename:  LubanBinaryDataReceiver.cs
 * author:    taoye
 * created:   2026/7/29
 * descrip:   Luban Binary 数据包接收器
 ***************************************************************/

using System;
using System.IO;
using System.Text;

namespace NovaFramework.Runtime
{
    /// <summary>
    /// 读取 Nova Luban Binary 数据包，并把包内原始单表字节写入缓存。
    /// </summary>
    public sealed class LubanBinaryDataReceiver : DataReceiver
    {
        private static readonly byte[] s_Magic = Encoding.ASCII.GetBytes("NLBP");
        private const byte c_Version = 1;
        private readonly LubanDataCache m_Cache;
        private readonly string m_UnitSourceName;

        public LubanBinaryDataReceiver(LubanDataCache cache, IDataTableUnitSetting unit,
            LoadAssetAsyncFunc loadAssetAsyncFunc, ReleaseAssetAction releaseAssetAction)
            : base(loadAssetAsyncFunc, releaseAssetAction)
        {
            m_Cache = cache ?? throw new ArgumentNullException(nameof(cache));
            m_UnitSourceName = unit?.AssetLocation ?? throw new ArgumentNullException(nameof(unit));
        }

        public LubanBinaryDataReceiver(LubanDataCache cache, IDataTableUnitSetting unit,
            LoadAssetSyncFunc loadAssetSyncFunc, ReleaseAssetAction releaseAssetAction)
            : base(loadAssetSyncFunc, releaseAssetAction)
        {
            m_Cache = cache ?? throw new ArgumentNullException(nameof(cache));
            m_UnitSourceName = unit?.AssetLocation ?? throw new ArgumentNullException(nameof(unit));
        }

        public override bool OnParseDataAsset(string contentString)
        {
            return false;
        }

        public override bool OnParseDataAsset(byte[] contentBytes)
        {
            if (contentBytes == null || contentBytes.Length == 0)
            {
                return false;
            }

            try
            {
                using var stream = new MemoryStream(contentBytes, false);
                using var reader = new BinaryReader(stream, Encoding.UTF8, false);
                if (!BytesEqual(reader.ReadBytes(s_Magic.Length), s_Magic))
                {
                    throw new InvalidDataException("Luban Binary 数据包 magic 无效。");
                }
                if (reader.ReadByte() != c_Version)
                {
                    throw new InvalidDataException("Luban Binary 数据包版本不受支持。");
                }

                int count = reader.ReadInt32();
                if (count < 0)
                {
                    throw new InvalidDataException("Luban Binary 数据包表数量无效。");
                }
                lock (m_Cache.DataMap)
                {
                    for (int i = 0; i < count; i++)
                    {
                        string name = ReadString(reader, stream);
                        byte[] payload = ReadBytes(reader, stream);
                        if (m_Cache.DataMap.ContainsKey(name))
                        {
                            throw new InvalidDataException($"Luban Binary 数据包包含重复表：{name}。");
                        }
                        m_Cache.DataMap.Add(name, payload);
                        m_Cache.SourceTracker[name] = new System.Collections.Generic.List<string> { m_UnitSourceName };
                    }
                }
                if (stream.Position != stream.Length)
                {
                    throw new InvalidDataException("Luban Binary 数据包存在尾随数据。");
                }
                return count > 0;
            }
            catch (Exception exception)
            {
                Log.Error(LogTag.Base, "解析 Luban Binary 数据包失败：{0}", exception.Message);
                return false;
            }
        }

        private static string ReadString(BinaryReader reader, Stream stream)
        {
            return Encoding.UTF8.GetString(ReadBytes(reader, stream));
        }

        private static byte[] ReadBytes(BinaryReader reader, Stream stream)
        {
            int length = reader.ReadInt32();
            if (length < 0 || length > stream.Length - stream.Position)
            {
                throw new InvalidDataException("Luban Binary 数据包字段长度无效。");
            }
            byte[] bytes = reader.ReadBytes(length);
            if (bytes.Length != length)
            {
                throw new EndOfStreamException();
            }
            return bytes;
        }

        private static bool BytesEqual(byte[] left, byte[] right)
        {
            if (left.Length != right.Length) return false;
            for (int i = 0; i < left.Length; i++)
            {
                if (left[i] != right[i]) return false;
            }
            return true;
        }
    }
}
