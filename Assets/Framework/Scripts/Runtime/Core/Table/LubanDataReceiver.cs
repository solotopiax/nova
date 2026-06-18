/***************************************************************
 * (c) copyright 2026 - 2030, Solotopia
 * All Rights Reserved.
 * -------------------------------------------------------------
 * filename:  LubanDataReceiver.cs
 * author:    taoye
 * created:   2026/4/16
 * descrip:   Luban 数据接收器
 ***************************************************************/

using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using Newtonsoft.Json.Linq;

namespace NovaFramework.Runtime
{
    /// <summary>
    /// Luban 数据接收器，将合并 JSON 拆分为 JArray 缓存到数据字典。
    /// Table / Config 等模块共用此实现，避免重复编写相同的 JSON 解析逻辑。
    /// 支持同名 Sheet 跨 unit 追加合并，Map 模式下检测主键冲突并中断。
    /// </summary>
    public sealed class LubanDataReceiver : DataReceiver
    {
        /// <summary>
        /// 数据加载缓存容器，持有 DataMap 与 SourceTracker，由外部 Manager 创建并传入。
        /// </summary>
        private readonly LubanDataCache m_Cache;
        /// <summary>
        /// 当前 unit 的来源名称，用于冲突报错定位。
        /// </summary>
        private readonly string m_UnitSourceName;
        /// <summary>
        /// 数据表模式（List / Map / One），决定是否启用主键冲突检测。
        /// </summary>
        private readonly DataTableMode m_Mode;
        /// <summary>
        /// Map 模式下的主键字段名，空串时跳过冲突检测。
        /// </summary>
        private readonly string m_IndexField;

        /// <summary>
        /// 构造方法（异步加载）。
        /// </summary>
        /// <param name="cache">数据加载缓存容器，持有 DataMap 与 SourceTracker。</param>
        /// <param name="unit">数据表单元设置，提供 AssetLocation、Mode 与 IndexField。</param>
        /// <param name="loadAssetAsyncFunc">异步加载资源的委托。</param>
        /// <param name="releaseAssetAction">释放资源的委托。</param>
        public LubanDataReceiver(LubanDataCache cache, IDataTableUnitSetting unit, LoadAssetAsyncFunc loadAssetAsyncFunc, ReleaseAssetAction releaseAssetAction)
            : base(loadAssetAsyncFunc, releaseAssetAction)
        {
            m_Cache = cache ?? throw new ArgumentNullException(nameof(cache));
            if (unit == null) throw new ArgumentNullException(nameof(unit));
            m_UnitSourceName = unit.AssetLocation ?? string.Empty;
            m_Mode = unit.Mode;
            m_IndexField = unit.IndexField ?? string.Empty;
        }

        /// <summary>
        /// 构造方法（同步加载）。
        /// </summary>
        /// <param name="cache">数据加载缓存容器，持有 DataMap 与 SourceTracker。</param>
        /// <param name="unit">数据表单元设置，提供 AssetLocation、Mode 与 IndexField。</param>
        /// <param name="loadAssetSyncFunc">同步加载资源的委托。</param>
        /// <param name="releaseAssetAction">释放资源的委托。</param>
        public LubanDataReceiver(LubanDataCache cache, IDataTableUnitSetting unit, LoadAssetSyncFunc loadAssetSyncFunc, ReleaseAssetAction releaseAssetAction)
            : base(loadAssetSyncFunc, releaseAssetAction)
        {
            m_Cache = cache ?? throw new ArgumentNullException(nameof(cache));
            if (unit == null) throw new ArgumentNullException(nameof(unit));
            m_UnitSourceName = unit.AssetLocation ?? string.Empty;
            m_Mode = unit.Mode;
            m_IndexField = unit.IndexField ?? string.Empty;
        }

        /// <summary>
        /// 解析 JSON 文本，遍历属性将每个 Sheet 的数据缓存到 m_Cache.DataMap。
        /// JSON key 为 Sheet 原名（大小写敏感），cacheKey 拼接为 "tb" + sheetName 再转小写，
        /// 与 Luban *Tables 构造函数中 loader 的 output_data_file 一致。
        /// 若 cacheKey 已存在，则执行追加合并；Map 模式下先检测主键冲突，有冲突立即中断返回 false。
        /// </summary>
        /// <param name="contentString">JSON 文本内容。</param>
        /// <returns>至少有一个属性解析成功时返回 true；发生主键冲突时返回 false。</returns>
        public override bool OnParseDataAsset(string contentString)
        {
            JObject jObject = JObject.Parse(contentString);
            bool anySuccess = false;

            foreach (var property in jObject.Properties())
            {
                string sheetName = property.Name;
                string cacheKey = ("tb" + sheetName).ToLower();

                JArray jArray = property.Value is JArray arr ? arr : new JArray { property.Value };

                if (m_Cache.DataMap.TryGetValue(cacheKey, out object existing) && existing is JArray existingArray)
                {
                    if (m_Mode == DataTableMode.Map && !string.IsNullOrEmpty(m_IndexField))
                    {
                        HashSet<string> existingKeys = new HashSet<string>();
                        foreach (JToken item in existingArray)
                        {
                            if (item is JObject jObj && jObj.TryGetValue(m_IndexField, out JToken keyToken))
                            {
                                existingKeys.Add(keyToken.ToString());
                            }
                        }

                        string previousSources = m_Cache.SourceTracker.TryGetValue(cacheKey, out List<string> srcList) ? string.Join(", ", srcList) : "(unknown)";
                        foreach (JToken item in jArray)
                        {
                            if (item is JObject jObj && jObj.TryGetValue(m_IndexField, out JToken keyToken))
                            {
                                string keyValue = keyToken.ToString();
                                if (existingKeys.Contains(keyValue))
                                {
                                    Log.Error(LogTag.Base, "数据合并冲突：Sheet '{0}' 在 [{1}] 与 '{2}' 中存在重复主键 {3}={4}，合并中止。", sheetName, previousSources, m_UnitSourceName, m_IndexField, keyValue);
                                    return false;
                                }
                            }
                        }
                    }

                    foreach (JToken item in jArray)
                    {
                        existingArray.Add(item);
                    }

                    if (m_Cache.SourceTracker.TryGetValue(cacheKey, out List<string> sourceList))
                    {
                        sourceList.Add(m_UnitSourceName);
                    }
                    else
                    {
                        m_Cache.SourceTracker[cacheKey] = new List<string> { m_UnitSourceName };
                    }
                }
                else
                {
                    m_Cache.DataMap[cacheKey] = jArray;
                    m_Cache.SourceTracker[cacheKey] = new List<string> { m_UnitSourceName };
                }

                anySuccess = true;
            }

            return anySuccess;
        }

        /// <summary>
        /// 解析字节流，将 JSON 拆分到缓存。
        /// </summary>
        /// <param name="contentBytes">JSON 字节流内容。</param>
        /// <returns>至少有一个属性解析成功时返回 true。</returns>
        public override bool OnParseDataAsset(byte[] contentBytes)
        {
            if (contentBytes == null || contentBytes.Length == 0)
            {
                return false;
            }

            try
            {
                using var stream = new MemoryStream(contentBytes);
                using var reader = new StreamReader(stream, Encoding.UTF8);
                return OnParseDataAsset(reader.ReadToEnd());
            }
            catch (Exception e)
            {
                Log.Error(LogTag.Base, "解析 Luban 数据失败：{0}", e.Message);
                return false;
            }
        }
    }
}
