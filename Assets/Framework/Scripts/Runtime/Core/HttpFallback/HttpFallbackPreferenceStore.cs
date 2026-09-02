/***************************************************************
 * (c) copyright 2026 - 2030, Solotopia
 * All Rights Reserved.
 * -------------------------------------------------------------
 * filename:  HttpFallbackPreferenceStore.cs
 * author:    taoye
 * created:   2026/9/2
 * descrip:   HTTP 最近成功域名偏好存储
 ***************************************************************/

using System;
using System.Collections.Generic;

namespace NovaFramework.Runtime
{
    /// <summary>
    /// 最近成功域名及其并发版本快照。
    /// </summary>
    internal readonly struct HttpFallbackPreferenceSnapshot
    {
        public HttpFallbackPreferenceSnapshot(string scopeKey, string endpointId, long version)
        {
            ScopeKey = scopeKey;
            EndpointId = endpointId;
            Version = version;
        }

        public string ScopeKey { get; }
        public string EndpointId { get; }
        public long Version { get; }
        public bool HasValue => !string.IsNullOrEmpty(EndpointId) && Version > 0;
    }

    /// <summary>
    /// 按模块提供的 scope key 隔离最近成功域名，并用单调版本保护并发清理。
    /// </summary>
    internal sealed class HttpFallbackPreferenceStore
    {
        private readonly object m_Gate = new object();
        private readonly Dictionary<string, PreferenceEntry> m_Entries =
            new Dictionary<string, PreferenceEntry>(StringComparer.Ordinal);
        private long m_Version;

        /// <summary>
        /// 捕获当前 scope 的稳定偏好快照；不存在时返回空快照。
        /// </summary>
        public HttpFallbackPreferenceSnapshot Capture(string scopeKey)
        {
            if (string.IsNullOrEmpty(scopeKey))
            {
                return default;
            }

            lock (m_Gate)
            {
                return m_Entries.TryGetValue(scopeKey, out PreferenceEntry entry)
                    ? new HttpFallbackPreferenceSnapshot(scopeKey, entry.EndpointId, entry.Version)
                    : new HttpFallbackPreferenceSnapshot(scopeKey, null, 0);
            }
        }

        /// <summary>
        /// 记录 scope 最近一次真正成功的规范化域名。
        /// </summary>
        public void MarkSuccess(string scopeKey, string endpointId)
        {
            if (string.IsNullOrEmpty(scopeKey) || string.IsNullOrEmpty(endpointId))
            {
                return;
            }

            lock (m_Gate)
            {
                m_Version++;
                m_Entries[scopeKey] = new PreferenceEntry(endpointId, m_Version);
            }
        }

        /// <summary>
        /// 配置候选已不再包含旧域名时，仅在偏好仍与旧快照一致的前提下清理。
        /// 普通请求耗尽不得调用本方法，以保持 Girl 风格的最近成功偏好。
        /// </summary>
        public void ClearIfUnchanged(HttpFallbackPreferenceSnapshot snapshot)
        {
            if (!snapshot.HasValue)
            {
                return;
            }

            lock (m_Gate)
            {
                if (m_Entries.TryGetValue(snapshot.ScopeKey, out PreferenceEntry entry) &&
                    entry.Version == snapshot.Version)
                {
                    m_Entries.Remove(snapshot.ScopeKey);
                }
            }
        }

        /// <summary>
        /// 清理全部进程内偏好；网络环境变化与模块关闭时调用。
        /// </summary>
        public void ClearAll()
        {
            lock (m_Gate)
            {
                m_Entries.Clear();
            }
        }

        private readonly struct PreferenceEntry
        {
            public PreferenceEntry(string endpointId, long version)
            {
                EndpointId = endpointId;
                Version = version;
            }

            public string EndpointId { get; }
            public long Version { get; }
        }
    }
}
