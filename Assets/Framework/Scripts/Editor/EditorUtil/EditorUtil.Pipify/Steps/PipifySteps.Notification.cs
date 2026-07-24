/***************************************************************
 * (c) copyright 2026 - 2030, Solotopia
 * All Rights Reserved.
 * -------------------------------------------------------------
 * filename:  PipifySteps.Notification.cs
 * author:    Codex
 * created:   2026/7/23
 * descrip:   Pipify 内置 Step 合集 —— 外部通知
 ***************************************************************/

using System;
using System.Net;
using System.Net.Http;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Cysharp.Threading.Tasks;
using UnityEngine;

namespace NovaFramework.Editor
{
    /// <summary>
    /// Pipify 内置 Step 合集（partial）：飞书机器人等外部通知入口。
    /// </summary>
    internal static partial class PipifySteps
    {
        private static readonly HttpClient s_FeishuHttpClient = new HttpClient
        {
            Timeout = TimeSpan.FromSeconds(15),
        };

        /// <summary>
        /// Step：向飞书自定义机器人发送标准文本消息。
        /// </summary>
        /// <param name="ctx">Runner 下发的运行时上下文。</param>
        /// <param name="parameters">Webhook URL 与自定义文案。</param>
        /// <returns>请求完成后的 UniTask。</returns>
        [PipifyStep("notification.feishu_webhook", "飞书机器人 Webhook", "通知", ParamsType = typeof(FeishuWebhookParams))]
        internal static UniTask RunFeishuWebhook(PipifyContext ctx, FeishuWebhookParams parameters)
        {
            return SendFeishuWebhookAsync(
                ctx,
                parameters,
                s_FeishuHttpClient.SendAsync,
                EditorUtil.Placeholder.ResolveFromActiveConfig);
        }

        /// <summary>
        /// 校验参数并发送飞书文本请求；发送委托用于隔离网络传输并支持编辑器测试。
        /// </summary>
        /// <param name="ctx">Runner 下发的运行时上下文。</param>
        /// <param name="parameters">Webhook URL 与自定义文案。</param>
        /// <param name="sendAsync">实际执行 HTTP 请求的委托。</param>
        /// <returns>请求与飞书业务响应均成功时完成的 UniTask。</returns>
        internal static async UniTask SendFeishuWebhookAsync(
            PipifyContext ctx,
            FeishuWebhookParams parameters,
            Func<HttpRequestMessage, CancellationToken, Task<HttpResponseMessage>> sendAsync)
        {
            await SendFeishuWebhookAsync(ctx, parameters, sendAsync, message => message);
        }

        /// <summary>
        /// 校验参数、解析消息占位符并发送飞书文本请求。
        /// </summary>
        internal static async UniTask SendFeishuWebhookAsync(
            PipifyContext ctx,
            FeishuWebhookParams parameters,
            Func<HttpRequestMessage, CancellationToken, Task<HttpResponseMessage>> sendAsync,
            Func<string, string> resolveMessage)
        {
            if (ctx == null) throw new ArgumentNullException(nameof(ctx));
            if (parameters == null) throw new ArgumentNullException(nameof(parameters));
            if (sendAsync == null) throw new ArgumentNullException(nameof(sendAsync));
            if (resolveMessage == null) throw new ArgumentNullException(nameof(resolveMessage));
            if (!Uri.TryCreate(parameters.WebhookUrl, UriKind.Absolute, out Uri webhookUri) ||
                (webhookUri.Scheme != Uri.UriSchemeHttps && webhookUri.Scheme != Uri.UriSchemeHttp))
            {
                throw new ArgumentException("[Pipify] 飞书 Webhook URL 为空或格式无效。", nameof(parameters));
            }
            if (string.IsNullOrWhiteSpace(parameters.MessageText))
            {
                throw new ArgumentException("[Pipify] 飞书机器人文案不能为空。", nameof(parameters));
            }

            string resolvedMessage = resolveMessage(parameters.MessageText);
            string payload = BuildFeishuTextPayload(resolvedMessage);
            using (var request = new HttpRequestMessage(HttpMethod.Post, webhookUri))
            {
                request.Content = new StringContent(payload, Encoding.UTF8, "application/json");
                using (HttpResponseMessage response = await sendAsync(request, ctx.CancellationToken))
                {
                    string responseBody = response.Content == null
                        ? string.Empty
                        : await response.Content.ReadAsStringAsync();
                    EnsureFeishuResponseSucceeded(response.StatusCode, responseBody);
                }
            }
        }

        /// <summary>
        /// 构造飞书自定义机器人标准文本消息 JSON。
        /// </summary>
        /// <param name="messageText">待发送文案。</param>
        /// <returns>UTF-8 请求体使用的 JSON 文本。</returns>
        internal static string BuildFeishuTextPayload(string messageText)
        {
            return JsonUtility.ToJson(new FeishuTextPayload
            {
                msg_type = "text",
                content = new FeishuTextContent { text = messageText },
            });
        }

        /// <summary>
        /// 同时校验 HTTP 状态与飞书业务码；响应结构异常或非零业务码时中断流水线。
        /// </summary>
        /// <param name="statusCode">HTTP 响应状态码。</param>
        /// <param name="responseBody">飞书响应 JSON。</param>
        internal static void EnsureFeishuResponseSucceeded(HttpStatusCode statusCode, string responseBody)
        {
            int numericStatus = (int)statusCode;
            if (numericStatus < 200 || numericStatus >= 300)
            {
                throw new InvalidOperationException($"[Pipify] 飞书机器人请求失败，HTTP 状态码：{numericStatus}。");
            }

            bool hasModernCode = responseBody?.IndexOf("\"code\"", StringComparison.OrdinalIgnoreCase) >= 0;
            bool hasLegacyCode = responseBody?.IndexOf("\"StatusCode\"", StringComparison.Ordinal) >= 0;
            if (!hasModernCode && !hasLegacyCode)
            {
                throw new InvalidOperationException("[Pipify] 飞书机器人响应缺少业务状态码。");
            }

            FeishuWebhookResponse response;
            try
            {
                response = JsonUtility.FromJson<FeishuWebhookResponse>(responseBody);
            }
            catch (Exception exception)
            {
                throw new InvalidOperationException("[Pipify] 飞书机器人响应不是有效 JSON。", exception);
            }

            int code = hasModernCode ? response.code : response.StatusCode;
            string message = hasModernCode ? response.msg : response.StatusMessage;
            if (code != 0)
            {
                throw new InvalidOperationException($"[Pipify] 飞书机器人返回失败，业务码：{code}，消息：{message ?? string.Empty}。");
            }
        }

        [Serializable]
        private sealed class FeishuTextPayload
        {
            public string msg_type;
            public FeishuTextContent content;
        }

        [Serializable]
        private sealed class FeishuTextContent
        {
            public string text;
        }

        [Serializable]
        private sealed class FeishuWebhookResponse
        {
            public int code;
            public string msg;
            public int StatusCode;
            public string StatusMessage;
        }
    }
}
