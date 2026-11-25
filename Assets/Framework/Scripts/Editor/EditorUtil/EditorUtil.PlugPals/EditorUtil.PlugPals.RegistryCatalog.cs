/***************************************************************
 * (c) copyright 2026 - 2030, Solotopia
 * All Rights Reserved.
 * -------------------------------------------------------------
 * filename:  EditorUtil.PlugPals.RegistryCatalog.cs
 * author:    taoye
 * created:   2026/11/25
 * descrip:   仓库包清单复用、条件请求与失败退避
 ***************************************************************/

using System;
using System.Collections.Generic;
using System.Net;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Threading;
using System.Threading.Tasks;
using Newtonsoft.Json;
using NovaFramework.Runtime;
using UnityEditor;

namespace NovaFramework.Editor
{
    public static partial class EditorUtil
    {
        public static partial class PlugPals
        {
            private const string c_RegistryCatalogSessionKey = "Nova.PlugPals.RegistryCatalog.v1";
            private const int c_MaxSavedCatalogLength = 1024 * 1024;
            private static readonly int[] s_CatalogRetrySeconds = { 300, 900, 1800, 3600 };
            private static readonly Dictionary<string, RegistryCatalogCacheEntry> s_RegistryCatalogs = new(StringComparer.Ordinal);

            /// <summary>一个仓库最近验证的清单、失败重试时间及当前共享请求。</summary>
            private sealed class RegistryCatalogCacheEntry
            {
                public string Json;
                public string ETag;
                public long LastSuccess;
                public long RetryAfter;
                public int FailureCount;

                [JsonIgnore] internal Task<string> InFlight;
            }

            /// <summary>从同一 Editor 会话恢复仓库结果，并在脚本重载前保存当前配置对应的清单。</summary>
            [InitializeOnLoadMethod]
            private static void InitializeRegistryCatalogs()
            {
                string saved = SessionState.GetString(c_RegistryCatalogSessionKey, string.Empty);
                if (!string.IsNullOrEmpty(saved))
                {
                    try
                    {
                        var catalogs = Util.Json.Deserialize<Dictionary<string, RegistryCatalogCacheEntry>>(saved);
                        lock (s_RegistryCatalogs)
                        {
                            foreach (var entry in catalogs ?? new Dictionary<string, RegistryCatalogCacheEntry>())
                            {
                                if (entry.Value != null && !s_RegistryCatalogs.ContainsKey(entry.Key))
                                    s_RegistryCatalogs.Add(entry.Key, entry.Value);
                            }
                        }
                    }
                    catch (Exception)
                    {
                        SessionState.EraseString(c_RegistryCatalogSessionKey);
                    }
                }
                AssemblyReloadEvents.beforeAssemblyReload += SaveRegistryCatalogs;
            }

            /// <summary>缓存只服务于当前配置，未完成请求和过大的响应不进入重载快照。</summary>
            private static void SaveRegistryCatalogs()
            {
                try
                {
                    RegistriesConfig registries = LoadRegistries();
                    var saved = new Dictionary<string, RegistryCatalogCacheEntry>(StringComparer.Ordinal);
                    lock (s_RegistryCatalogs)
                    {
                        foreach (string registry in new[] { registries.externalUrl, registries.internalUrl })
                        {
                            if (string.IsNullOrEmpty(registry)) continue;
                            string url = RegistryCatalogUrl(registry);
                            if (!Uri.TryCreate(url, UriKind.Absolute, out Uri endpoint) ||
                                endpoint.UserInfo.Length > 0 || endpoint.Query.Length > 0) continue;
                            if (s_RegistryCatalogs.TryGetValue(url, out RegistryCatalogCacheEntry catalog) &&
                                (catalog.Json?.Length ?? 0) <= c_MaxSavedCatalogLength)
                                saved[url] = catalog;
                        }
                        SessionState.SetString(c_RegistryCatalogSessionKey, Util.Json.Serialize(saved));
                    }
                }
                catch (Exception)
                {
                    SessionState.EraseString(c_RegistryCatalogSessionKey);
                }
            }

            /// <summary>根据成功时间和独立退避判断额外自动检查是否到期。人工检查不使用这个门槛。</summary>
            internal static bool IsRegistryCatalogDue(string registryUrl, long now, long freshnessSeconds)
            {
                if (string.IsNullOrEmpty(registryUrl)) return false;
                lock (s_RegistryCatalogs)
                {
                    if (!s_RegistryCatalogs.TryGetValue(RegistryCatalogUrl(registryUrl), out RegistryCatalogCacheEntry catalog))
                        return true;
                    if (catalog.InFlight?.IsCompleted == false) return false;
                    if (catalog.FailureCount > 0 && catalog.RetryAfter <= now + s_CatalogRetrySeconds[3])
                        return catalog.RetryAfter <= now;
                    return now < catalog.LastSuccess || now - catalog.LastSuccess >= freshnessSeconds;
                }
            }

            /// <summary>统一同一仓库的清单请求地址。</summary>
            private static string RegistryCatalogUrl(string registryUrl) => registryUrl.TrimEnd('/') + c_RegistryApiPath;

            /// <summary>并发消费者共用同一个 HTTP 请求，各消费者继续独立取消等待。</summary>
            private static async Task<string> FetchRegistryCatalogAsync(string registryUrl, CancellationToken token)
            {
                token.ThrowIfCancellationRequested();
                string url = RegistryCatalogUrl(registryUrl);
                Task<string> flight;
                lock (s_RegistryCatalogs)
                {
                    if (!s_RegistryCatalogs.TryGetValue(url, out RegistryCatalogCacheEntry catalog))
                    {
                        catalog = new RegistryCatalogCacheEntry();
                        s_RegistryCatalogs.Add(url, catalog);
                    }
                    if (catalog.InFlight == null || catalog.InFlight.IsCompleted)
                    {
                        catalog.InFlight = DownloadRegistryCatalogAsync(url, catalog);
                        // 所有调用方都取消后，仍观察共享请求最终的错误。
                        _ = catalog.InFlight.ContinueWith(failed => { _ = failed.Exception; }, CancellationToken.None,
                            TaskContinuationOptions.OnlyOnFaulted | TaskContinuationOptions.ExecuteSynchronously, TaskScheduler.Default);
                    }
                    flight = catalog.InFlight;
                }

                if (token.CanBeCanceled && !flight.IsCompleted)
                {
                    var canceled = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
                    using (token.Register(() => canceled.TrySetResult(true)))
                    {
                        await Task.WhenAny(flight, canceled.Task).ConfigureAwait(false);
                        token.ThrowIfCancellationRequested();
                    }
                }
                return await flight.ConfigureAwait(false);
            }

            /// <summary>304 复用已验证正文，成功刷新时间。失败保留上次成功正文并推进仓库自己的重试时间。</summary>
            private static async Task<string> DownloadRegistryCatalogAsync(string url, RegistryCatalogCacheEntry catalog)
            {
                try
                {
                    using var request = new HttpRequestMessage(HttpMethod.Get, url);
                    if (catalog.Json != null && EntityTagHeaderValue.TryParse(catalog.ETag, out EntityTagHeaderValue tag))
                        request.Headers.IfNoneMatch.Add(tag);
                    using HttpResponseMessage response = await s_HttpClient.SendAsync(request).ConfigureAwait(false);
                    string json = catalog.Json;
                    string etag = catalog.ETag;
                    if (response.StatusCode != HttpStatusCode.NotModified || json == null)
                    {
                        response.EnsureSuccessStatusCode();
                        json = await response.Content.ReadAsStringAsync().ConfigureAwait(false);
                        if (Util.Json.Deserialize<VerdaccioPackageInfo[]>(json) == null)
                        {
                            RecordCatalogFailure(catalog);
                            return json;
                        }
                        etag = response.Headers.ETag?.ToString();
                    }
                    lock (s_RegistryCatalogs)
                    {
                        catalog.Json = response.Headers.CacheControl?.NoStore == true ? null : json;
                        catalog.ETag = catalog.Json == null ? null : etag;
                        catalog.LastSuccess = DateTimeOffset.UtcNow.ToUnixTimeSeconds();
                        catalog.FailureCount = 0;
                        catalog.RetryAfter = 0;
                    }
                    return json;
                }
                catch (Exception)
                {
                    RecordCatalogFailure(catalog);
                    throw;
                }
            }

            /// <summary>按 5、15、30、60 分钟退避，后续失败保持 60 分钟上限。</summary>
            private static void RecordCatalogFailure(RegistryCatalogCacheEntry catalog)
            {
                lock (s_RegistryCatalogs)
                {
                    int index = Math.Min(catalog.FailureCount, s_CatalogRetrySeconds.Length - 1);
                    catalog.RetryAfter = DateTimeOffset.UtcNow.ToUnixTimeSeconds() + s_CatalogRetrySeconds[index];
                    catalog.FailureCount = index + 1;
                }
            }
        }
    }
}
