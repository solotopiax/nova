/***************************************************************
 * (c) copyright 2026 - 2030, Solotopia
 * All Rights Reserved.
 * -------------------------------------------------------------
 * filename:  IAPProductCatalogUploader.cs
 * author:    Codex
 * created:   2026/10/19
 * descrip:   Editor IAP 商品目录上传服务
 ***************************************************************/

using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Net.Http;
using System.Runtime.ExceptionServices;
using System.Threading;
using Cysharp.Threading.Tasks;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using UnityEngine;
using EdUtil = NovaFramework.Editor.EditorUtil;

namespace NovaFramework.SDK.IAP.Editor
{
    /// <summary>
    /// 统一上传当前导出的 IAP 商品目录，供 Inspector 与 Pipify 共用。
    /// </summary>
    public static class IAPProductCatalogUploader
    {
        /// <summary>
        /// 商品目录上传请求的固定超时时间。
        /// </summary>
        private static readonly TimeSpan s_UploadTimeout = TimeSpan.FromSeconds(30);

        /// <summary>
        /// 商品目录上传不附加额外请求头。
        /// </summary>
        private static readonly IReadOnlyDictionary<string, string> s_EmptyHeaders = new ReadOnlyDictionary<string, string>(new Dictionary<string, string>());

        /// <summary>
        /// Inspector、Pipify 与受控 Action 共用的一次发送门；并发调用直接拒绝，不排队或自动重试。
        /// </summary>
        private static int s_IsSending;

        /// <summary>
        /// 可注入的 JSON 发送入口，用于在不访问真实服务器的情况下验证完整上传参数。
        /// </summary>
        internal delegate UniTask<UploadResponse> SendJsonDelegate(HttpMethod method, Uri requestUri, string json, IReadOnlyDictionary<string, string> headers, TimeSpan timeout, CancellationToken cancellationToken);

        /// <summary>
        /// 与底层 HTTP 响应生命周期解耦的最小上传响应。
        /// </summary>
        internal sealed class UploadResponse
        {
            /// <summary>
            /// 创建上传响应快照。
            /// </summary>
            internal UploadResponse(int statusCode, bool isSuccessStatusCode, string reasonPhrase, string body)
            {
                StatusCode = statusCode;
                IsSuccessStatusCode = isSuccessStatusCode;
                ReasonPhrase = reasonPhrase ?? string.Empty;
                Body = body ?? string.Empty;
            }

            internal int StatusCode { get; }
            internal bool IsSuccessStatusCode { get; }
            internal string ReasonPhrase { get; }
            internal string Body { get; }
        }

        /// <summary>
        /// 一次发送的内部完整结果；原始正文和异常只供旧包装保留兼容语义，不进入 Action JSON。
        /// </summary>
        internal sealed class UploadAttempt
        {
            internal string Outcome;
            internal bool Dispatched;
            internal UploadResponse Response;
            internal Exception Error;
            internal string ExceptionKind = string.Empty;
            internal DateTime StartedAtUtc;
            internal DateTime CompletedAtUtc;
        }

        /// <summary>
        /// 从当前激活且已导出的 ConfigRuntime 构造商品目录并上传。
        /// </summary>
        /// <param name="cancellationToken">用于取消本次上传的令牌。</param>
        public static UniTask UploadCurrentAsync(CancellationToken cancellationToken = default)
        {
            IAPProductCatalogSnapshot snapshot = IAPProductCatalogProtocol.CreateFromCurrentConfig();
            return UploadAsync(snapshot, cancellationToken, SendJsonAsync);
        }

        /// <summary>
        /// 上传指定商品目录快照，并统一处理响应和失败语义。
        /// </summary>
        /// <param name="snapshot">冻结的上传地址与请求。</param>
        /// <param name="cancellationToken">本次调用的取消令牌。</param>
        /// <param name="sendJson">既有生产发送器或内部确定性测试发送器。</param>
        /// <returns>保留旧行为：2xx 成功、精确版本冲突只警告，其他失败抛出原异常。</returns>
        internal static async UniTask UploadAsync(IAPProductCatalogSnapshot snapshot, CancellationToken cancellationToken, SendJsonDelegate sendJson)
        {
            if (snapshot == null) throw new ArgumentNullException(nameof(snapshot));
            if (sendJson == null) throw new ArgumentNullException(nameof(sendJson));
            cancellationToken.ThrowIfCancellationRequested();

            string json = IAPProductCatalogProtocol.Serialize(snapshot.Request);
            UploadAttempt attempt = await UploadOnceAsync(snapshot, json, cancellationToken, sendJson);
            if (attempt.Error != null)
            {
                ExceptionDispatchInfo.Capture(attempt.Error).Throw();
            }

            UploadResponse response = attempt.Response;
            string responseBody = string.IsNullOrWhiteSpace(response.Body) ? "（空响应）" : response.Body;
            if (!response.IsSuccessStatusCode)
            {
                if (attempt.Outcome == "version-conflict")
                {
                    Debug.LogWarning($"IAP 商品目录版本冲突，当前版本已绑定不同的支付表；请核对商品表并使用新客户端版本上传：{snapshot.UploadUri}\n{responseBody}");
                    return;
                }

                throw new InvalidOperationException($"IAP 商品目录上传失败：{snapshot.UploadUri}，HTTP {response.StatusCode} {response.ReasonPhrase}\n{responseBody}");
            }

            Debug.Log($"IAP 商品目录上传成功：{snapshot.UploadUri}\n{responseBody}");
        }

        /// <summary>
        /// 发送已经冻结的最终 JSON 一次，返回结构化事实；本层不记录正文、不抛业务响应错误，也不重试。
        /// </summary>
        /// <param name="snapshot">冻结地址和请求的快照。</param>
        /// <param name="json">准备阶段生成的最终 JSON，原样交给既有发送入口。</param>
        /// <param name="cancellationToken">本次调用的取消令牌。</param>
        /// <param name="sendJson">生产发送入口或同程序集测试发送器；不得为 null。</param>
        /// <returns>包含是否进入发送、响应或原异常的一次结果；取消和忙碌不会排队。</returns>
        internal static async UniTask<UploadAttempt> UploadOnceAsync(IAPProductCatalogSnapshot snapshot, string json, CancellationToken cancellationToken, SendJsonDelegate sendJson)
        {
            if (snapshot == null) throw new ArgumentNullException(nameof(snapshot));
            if (json == null) throw new ArgumentNullException(nameof(json));
            if (sendJson == null) throw new ArgumentNullException(nameof(sendJson));

            var attempt = new UploadAttempt { StartedAtUtc = DateTime.UtcNow };
            bool ownsGate = false;
            try
            {
                cancellationToken.ThrowIfCancellationRequested();
                if (Interlocked.CompareExchange(ref s_IsSending, 1, 0) != 0)
                {
                    attempt.Outcome = "busy";
                    attempt.ExceptionKind = "busy";
                    attempt.Error = new InvalidOperationException("IAP 商品目录已有上传正在执行，不会排队或重复提交。");
                    return attempt;
                }

                ownsGate = true;
                cancellationToken.ThrowIfCancellationRequested();
                // dispatched 仅证明已进入发送器；不能据此断言服务器收到、未收到或已经应用。
                attempt.Dispatched = true;
                attempt.Response = await sendJson(HttpMethod.Post, snapshot.UploadUri, json, s_EmptyHeaders, s_UploadTimeout, cancellationToken);
                if (attempt.Response == null)
                {
                    throw new NullReferenceException("IAP 商品目录发送器返回空响应。");
                }

                attempt.Outcome = attempt.Response.IsSuccessStatusCode
                    ? "http-accepted"
                    : IsCatalogVersionConflict(attempt.Response) ? "version-conflict" : "http-error";
            }
            catch (OperationCanceledException exception)
            {
                attempt.Outcome = attempt.Dispatched ? "cancelled" : "cancelled-before-send";
                attempt.ExceptionKind = "cancelled";
                attempt.Error = exception;
            }
            catch (Exception exception)
            {
                attempt.Outcome = "transport-error";
                attempt.ExceptionKind = exception is TimeoutException ? "timeout" : "transport";
                attempt.Error = exception;
            }
            finally
            {
                attempt.CompletedAtUtc = DateTime.UtcNow;
                if (ownsGate) Interlocked.Exchange(ref s_IsSending, 0);
            }
            return attempt;
        }

        /// <summary>
        /// 只将服务端明确返回的商品目录版本冲突识别为可继续执行的警告；其他 409 仍按上传失败处理。
        /// </summary>
        /// <param name="response">服务端上传响应。</param>
        /// <returns>HTTP 409 且业务码为 CATALOG_VERSION_CONFLICT 时返回 true。</returns>
        private static bool IsCatalogVersionConflict(UploadResponse response)
        {
            if (response.StatusCode != 409 || string.IsNullOrWhiteSpace(response.Body)) return false;

            try
            {
                JObject body = JObject.Parse(response.Body);
                JToken code = body["code"];
                return code?.Type == JTokenType.String &&
                    string.Equals(code.Value<string>(), "CATALOG_VERSION_CONFLICT", StringComparison.Ordinal);
            }
            catch (JsonException)
            {
                return false;
            }
        }

        /// <summary>
        /// 使用 Editor HTTP 门面发送商品目录请求。
        /// </summary>
        /// <param name="method">共享发送核心固定提供的 POST 方法。</param>
        /// <param name="requestUri">已经冻结的固定服务器地址。</param>
        /// <param name="json">准备或旧包装正式序列化的最终 JSON。</param>
        /// <param name="headers">共享发送核心提供的空额外请求头。</param>
        /// <param name="timeout">固定三十秒超时。</param>
        /// <param name="cancellationToken">本次请求取消令牌。</param>
        /// <returns>已脱离底层 HTTP 生命周期的响应，不新增发送或重试路径。</returns>
        internal static async UniTask<UploadResponse> SendJsonAsync(HttpMethod method, Uri requestUri, string json, IReadOnlyDictionary<string, string> headers, TimeSpan timeout, CancellationToken cancellationToken)
        {
            EdUtil.Network.Http.Response response = await EdUtil.Network.Http.SendJsonAsync(method, requestUri, json, headers, timeout, cancellationToken);
            return new UploadResponse(response.StatusCode, response.IsSuccessStatusCode, response.ReasonPhrase, response.Body);
        }
    }
}
