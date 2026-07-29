/***************************************************************
 * (c) copyright 2026 - 2030, Solotopia
 * All Rights Reserved.
 * -------------------------------------------------------------
 * filename:  AppConfigSnapshot.cs
 * author:    taoye
 * created:   2026/7/27
 * descrip:   Custom 本地默认 JSON 与远端完整快照查询器
 ***************************************************************/

using System;
using System.Collections.Generic;
using System.Globalization;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace NovaFramework.Runtime
{
    /// <summary>
    /// Custom 运行时快照；本地路径默认值与远端完整 JSON 分层保存，查询时优先远端。
    /// </summary>
    internal sealed class AppConfigSnapshot
    {
        private readonly JObject m_LocalRoot = new();
        private JObject m_RemoteRoot = new();

        /// <summary>
        /// 使用 ConfigRuntimeSO 导出的路径键值构建本地默认 JSON。
        /// </summary>
        /// <param name="localConfig">本地路径键值；非法或冲突路径会被忽略。</param>
        public AppConfigSnapshot(CustomConfigData localConfig)
        {
            if (localConfig?.Entries == null)
            {
                return;
            }

            for (int i = 0; i < localConfig.Entries.Count; i++)
            {
                CustomConfigEntry entry = localConfig.Entries[i];
                if (entry == null || string.IsNullOrWhiteSpace(entry.Key))
                {
                    continue;
                }

                if (!TrySetLocalPath(entry.Key.Trim(), entry.Value ?? string.Empty, out string error))
                {
                    Log.Warning(LogTag.Config, "Custom 本地路径已忽略：path={0}, error={1}", entry.Key, error);
                }
            }
        }

        /// <summary>
        /// 解析并替换远端完整 JSON；失败时保留当前远端快照。
        /// </summary>
        public bool TryReplaceRemoteJson(string json, out string error)
        {
            if (!TryParseRemoteJson(json, out JObject candidate, out error))
            {
                return false;
            }

            ApplyRemote(candidate);
            return true;
        }

        /// <summary>
        /// 解析远端 JSON object，但不修改当前快照，供先持久化后切换内存的流程使用。
        /// </summary>
        public bool TryParseRemoteJson(string json, out JObject remoteRoot, out string error)
        {
            remoteRoot = null;
            error = null;
            if (string.IsNullOrWhiteSpace(json))
            {
                error = "Custom 配置响应 value 为空。";
                return false;
            }

            try
            {
                JToken token = Util.Json.Deserialize<JToken>(json);
                if (token is not JObject root)
                {
                    error = "Custom 配置 JSON 根节点必须是 object。";
                    return false;
                }
                remoteRoot = root;
                return true;
            }
            catch (Exception e) when (e is JsonException || e is ArgumentException)
            {
                error = $"Custom 配置 JSON 解析失败：{e.Message}";
                return false;
            }
        }

        /// <summary>
        /// 原子替换当前远端完整快照；调用方须传入已校验的 JSON object。
        /// </summary>
        public void ApplyRemote(JObject remoteRoot)
        {
            m_RemoteRoot = remoteRoot != null ? (JObject)remoteRoot.DeepClone() : new JObject();
        }

        /// <summary>
        /// 序列化当前远端完整快照，供磁盘缓存保存。
        /// </summary>
        public string GetRemoteJson()
        {
            return m_RemoteRoot.ToString(Formatting.None);
        }

        /// <summary>
        /// 按 JSONPath 读取当前有效字符串；远端缺失时回退本地，显式 null 返回调用方默认值。
        /// </summary>
        public string GetString(string path, string defaultValue)
        {
            if (TrySelect(m_RemoteRoot, path, out JToken remote))
            {
                return remote.Type == JTokenType.Null ? defaultValue : TokenToString(remote);
            }
            return TrySelect(m_LocalRoot, path, out JToken local) && local.Type != JTokenType.Null
                ? TokenToString(local)
                : defaultValue;
        }

        /// <summary>
        /// 读取不受远端覆盖影响的本地默认字符串。
        /// </summary>
        public string GetLocalString(string path, string defaultValue)
        {
            return TrySelect(m_LocalRoot, path, out JToken local) && local.Type != JTokenType.Null
                ? TokenToString(local)
                : defaultValue;
        }

        /// <summary>
        /// 判断远端路径是否显式提供 JSON null；类型化读取据此禁止回退本地。
        /// </summary>
        public bool IsRemoteNull(string path)
        {
            return TrySelect(m_RemoteRoot, path, out JToken token) && token.Type == JTokenType.Null;
        }

        /// <summary>
        /// 尝试读取当前有效字符串；路径不存在或远端显式 null 时返回 false。
        /// </summary>
        public bool TryGetString(string path, out string value)
        {
            const string sentinel = "\u0000__nova_custom_missing__";
            value = GetString(path, sentinel);
            if (string.Equals(value, sentinel, StringComparison.Ordinal))
            {
                value = null;
                return false;
            }
            return true;
        }

        /// <summary>
        /// 将 JToken 转成稳定字符串；对象和数组返回紧凑 JSON。
        /// </summary>
        private static string TokenToString(JToken token)
        {
            if (token.Type == JTokenType.String)
            {
                return token.Value<string>();
            }
            if (token is JValue value && value.Value is IFormattable formattable)
            {
                return formattable.ToString(null, CultureInfo.InvariantCulture).ToLowerInvariant();
            }
            return token.ToString(Formatting.None);
        }

        /// <summary>
        /// 安全执行 JSONPath 查询；空路径或非法表达式按未命中处理。
        /// </summary>
        private static bool TrySelect(JObject root, string path, out JToken token)
        {
            token = null;
            if (root == null || string.IsNullOrWhiteSpace(path))
            {
                return false;
            }
            try
            {
                token = root.SelectToken(path, false);
                return token != null;
            }
            catch (JsonException)
            {
                return false;
            }
        }

        /// <summary>
        /// 将支持属性段与数字数组下标的 JSONPath 写入本地 JSON 树。
        /// </summary>
        private bool TrySetLocalPath(string path, string value, out string error)
        {
            if (!TryParsePath(path, out List<PathSegment> segments, out error))
            {
                return false;
            }

            JToken current = m_LocalRoot;
            for (int i = 0; i < segments.Count; i++)
            {
                PathSegment segment = segments[i];
                bool isLast = i == segments.Count - 1;
                bool nextIsIndex = !isLast && segments[i + 1].IsIndex;
                if (!segment.IsIndex)
                {
                    if (current is not JObject obj)
                    {
                        error = "属性段的父节点不是 object。";
                        return false;
                    }
                    if (isLast)
                    {
                        obj[segment.Name] = value;
                        continue;
                    }
                    JToken next = obj[segment.Name];
                    if (next == null)
                    {
                        next = nextIsIndex ? new JArray() : new JObject();
                        obj[segment.Name] = next;
                    }
                    if ((nextIsIndex && next is not JArray) || (!nextIsIndex && next is not JObject))
                    {
                        error = "路径与已存在节点类型冲突。";
                        return false;
                    }
                    current = next;
                    continue;
                }

                if (current is not JArray array)
                {
                    error = "数组下标的父节点不是 array。";
                    return false;
                }
                while (array.Count <= segment.Index)
                {
                    array.Add(JValue.CreateNull());
                }
                if (isLast)
                {
                    array[segment.Index] = value;
                    continue;
                }
                JToken child = array[segment.Index];
                if (child == null || child.Type == JTokenType.Null)
                {
                    child = nextIsIndex ? new JArray() : new JObject();
                    array[segment.Index] = child;
                }
                if ((nextIsIndex && child is not JArray) || (!nextIsIndex && child is not JObject))
                {
                    error = "路径与已存在数组节点类型冲突。";
                    return false;
                }
                current = child;
            }

            error = null;
            return true;
        }

        /// <summary>
        /// 解析简单 JSONPath：属性使用点分隔，数组使用非负数字下标。
        /// </summary>
        private static bool TryParsePath(string path, out List<PathSegment> segments, out string error)
        {
            segments = new List<PathSegment>();
            int index = 0;
            while (index < path.Length)
            {
                int start = index;
                while (index < path.Length && path[index] != '.' && path[index] != '[')
                {
                    index++;
                }
                if (index > start)
                {
                    segments.Add(PathSegment.Property(path.Substring(start, index - start)));
                }
                if (index < path.Length && path[index] == '.')
                {
                    index++;
                    if (index >= path.Length)
                    {
                        error = "路径不能以点结尾。";
                        return false;
                    }
                    continue;
                }
                while (index < path.Length && path[index] == '[')
                {
                    int close = path.IndexOf(']', index + 1);
                    if (close < 0 || !int.TryParse(path.Substring(index + 1, close - index - 1), out int arrayIndex) || arrayIndex < 0)
                    {
                        error = "数组下标必须是非负整数。";
                        return false;
                    }
                    segments.Add(PathSegment.ArrayIndex(arrayIndex));
                    index = close + 1;
                }
                if (index < path.Length && path[index] != '.')
                {
                    error = "路径包含不支持的字符。";
                    return false;
                }
                if (index < path.Length && path[index] == '.')
                {
                    index++;
                }
            }

            if (segments.Count == 0 || segments[0].IsIndex)
            {
                error = "路径必须以属性名开始。";
                return false;
            }
            error = null;
            return true;
        }

        /// <summary>
        /// JSONPath 解析后的单个属性段或数组下标段。
        /// </summary>
        private readonly struct PathSegment
        {
            public string Name { get; }
            public int Index { get; }
            public bool IsIndex { get; }

            private PathSegment(string name, int index, bool isIndex)
            {
                Name = name;
                Index = index;
                IsIndex = isIndex;
            }

            public static PathSegment Property(string name) => new(name, -1, false);
            public static PathSegment ArrayIndex(int index) => new(null, index, true);
        }
    }
}
