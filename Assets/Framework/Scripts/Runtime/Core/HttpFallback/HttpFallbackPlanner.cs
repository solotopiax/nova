/***************************************************************
 * (c) copyright 2026 - 2030, Solotopia
 * All Rights Reserved.
 * -------------------------------------------------------------
 * filename:  HttpFallbackPlanner.cs
 * author:    taoye
 * created:   2026/9/2
 * descrip:   HTTP 主备候选纯规划器
 ***************************************************************/

using System;
using System.Collections.Generic;

namespace NovaFramework.Runtime
{
    /// <summary>
    /// 负责候选去重、域名规范化与最近成功优先排序，不执行网络请求。
    /// </summary>
    internal static class HttpFallbackPlanner
    {
        /// <summary>
        /// 构建不可变执行计划；偏好仅影响单轮首选，不改变候选原始主备角色。
        /// </summary>
        public static HttpFallbackExecutionPlan Build(
            IReadOnlyList<string> urls,
            HttpFallbackPolicy policy,
            HttpFallbackPreferenceSnapshot preference = default)
        {
            var candidates = new List<HttpFallbackCandidate>(urls?.Count ?? 0);
            if (urls != null)
            {
                for (int i = 0; i < urls.Count; i++)
                {
                    string url = urls[i];
                    if (string.IsNullOrWhiteSpace(url) || ContainsUrl(candidates, url))
                    {
                        continue;
                    }

                    candidates.Add(new HttpFallbackCandidate(
                        url,
                        GetEndpointId(url),
                        ResolveRole(i),
                        i));
                }
            }

            if (policy.PreferLastSuccessfulHost && preference.HasValue)
            {
                int preferredIndex = candidates.FindIndex(candidate => string.Equals(
                    candidate.EndpointId,
                    preference.EndpointId,
                    StringComparison.OrdinalIgnoreCase));
                if (preferredIndex > 0)
                {
                    HttpFallbackCandidate preferred = candidates[preferredIndex];
                    candidates.RemoveAt(preferredIndex);
                    candidates.Insert(0, preferred);
                }
            }

            return new HttpFallbackExecutionPlan(candidates.ToArray(), policy);
        }

        /// <summary>
        /// 从 HTTP(S) URL 提取规范化 scheme、host 与 port，供跨路径请求共享域名偏好。
        /// </summary>
        public static string GetEndpointId(string url)
        {
            if (!Uri.TryCreate(url, UriKind.Absolute, out Uri uri) ||
                (uri.Scheme != Uri.UriSchemeHttp && uri.Scheme != Uri.UriSchemeHttps))
            {
                return null;
            }

            return new UriBuilder(uri.Scheme, uri.Host)
            {
                Port = uri.IsDefaultPort ? -1 : uri.Port,
                Path = string.Empty,
                Query = string.Empty,
                Fragment = string.Empty,
            }.Uri.GetLeftPart(UriPartial.Authority);
        }

        private static bool ContainsUrl(List<HttpFallbackCandidate> candidates, string url)
        {
            for (int i = 0; i < candidates.Count; i++)
            {
                // HTTP 的 scheme/host 不区分大小写，但 path/query 可能区分大小写并参与签名。
                // 这里只去掉完全相同的完整 URL，不能把大小写不同的业务路径误判为同一候选。
                if (string.Equals(candidates[i].Url, url, StringComparison.Ordinal))
                {
                    return true;
                }
            }

            return false;
        }

        private static HttpFallbackRouteRole ResolveRole(int originalIndex)
        {
            return originalIndex switch
            {
                0 => HttpFallbackRouteRole.Primary,
                1 => HttpFallbackRouteRole.Fallback,
                _ => HttpFallbackRouteRole.Other,
            };
        }
    }
}
