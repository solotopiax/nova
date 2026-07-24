/***************************************************************
 * (c) copyright 2026 - 2030, Solotopia
 * All Rights Reserved.
 * -------------------------------------------------------------
 * filename:  EditorUtil.CDN.Cloudflare.cs
 * author:    Codex
 * created:   2026/7/21
 * descrip:   Cloudflare 缓存批量清理适配器
 ***************************************************************/

using System;
using System.Collections.Generic;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text;
using Cysharp.Threading.Tasks;
using NovaFramework.Runtime;
using UnityEngine;

namespace NovaFramework.Editor
{
    public static partial class EditorUtil
    {
        public static partial class CDN
        {
            private static readonly HttpClient s_CloudflareHttpClient = new()
            {
                Timeout = TimeSpan.FromSeconds(30)
            };

            /// <summary>
            /// Cloudflare purge API 请求正文。
            /// </summary>
            [Serializable]
            private sealed class CloudflarePurgeRequest
            {
                public string[] files;
            }

            /// <summary>
            /// 使用 Cloudflare API 按批清理配置中的缓存 URL。
            /// </summary>
            /// <param name="config">CDN 编辑态配置。</param>
            /// <param name="onProgress">进度回调，参数依次为完成批数和总批数。</param>
            /// <returns>成功清理 URL 数量。</returns>
            internal static UniTask<int> PurgeAsync(CDNEditorConfigs config, Action<int, int> onProgress)
            {
                return PurgeAsync(config, SendCloudflareBatchAsync, onProgress);
            }

            /// <summary>
            /// 向 Cloudflare purge API 发送单批 URL。
            /// </summary>
            /// <param name="purgeUrl">Zone purge API 完整 URL。</param>
            /// <param name="token">Bearer Token。</param>
            /// <param name="urls">本批待清理 URL。</param>
            /// <returns>HTTP 状态与响应正文。</returns>
            private static async UniTask<CloudflareHttpResult> SendCloudflareBatchAsync(
                string purgeUrl,
                string token,
                IReadOnlyList<string> urls)
            {
                var payload = new CloudflarePurgeRequest
                {
                    files = urls is string[] array ? array : new List<string>(urls).ToArray()
                };
                string json = JsonUtility.ToJson(payload);
                using var request = new HttpRequestMessage(HttpMethod.Post, purgeUrl)
                {
                    Content = new StringContent(json, Encoding.UTF8, "application/json")
                };
                request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);

                using HttpResponseMessage response = await s_CloudflareHttpClient.SendAsync(request);
                string body = await response.Content.ReadAsStringAsync();
                return new CloudflareHttpResult((int)response.StatusCode, response.IsSuccessStatusCode, body);
            }
        }
    }
}
