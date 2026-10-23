/***************************************************************
 * (c) copyright 2026 - 2030, Solotopia
 * All Rights Reserved.
 * -------------------------------------------------------------
 * filename:  EditorUtil.Network.Http.cs
 * author:    Codex
 * created:   2026/10/19
 * descrip:   Editor 管理协议的默认请求头与 HTTP JSON 传输门面
 ***************************************************************/

using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Globalization;
using System.Linq;
using System.Net.Http;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Cysharp.Threading.Tasks;
using Newtonsoft.Json;
using NovaFramework.Runtime;
using UnityEditor;

namespace NovaFramework.Editor
{
    public static partial class EditorUtil
    {
        public static partial class Network
        {
            /// <summary>
            /// Editor 管理协议的公共 HTTP 门面，统一提供默认请求头和 JSON 传输能力。
            /// </summary>
            public static class Http
            {
                /// <summary>
                /// 所有 Editor JSON 请求复用的 HttpClient；单次超时由链接取消令牌控制。
                /// </summary>
                private static readonly HttpClient s_Client = new HttpClient
                {
                    Timeout = Timeout.InfiniteTimeSpan
                };

                /// <summary>
                /// Editor 管理协议默认请求头。字段含义与运行时请求头保持一致。
                /// </summary>
                [JsonObject(MemberSerialization.OptIn)]
                public sealed class RequestHeader
                {
                    /// <summary>
                    /// 创建经过校验的 Editor 管理协议请求头。
                    /// </summary>
                    /// <param name="appId">正整数应用 ID。</param>
                    /// <param name="platform">运行平台名称。</param>
                    /// <param name="channel">运营渠道名称。</param>
                    /// <param name="developMode">开发模式，协议中输出 Debug 或 Release。</param>
                    /// <param name="version">客户端版本号。</param>
                    public RequestHeader(int appId, string platform, string channel, DevelopMode developMode, string version)
                    {
                        if (appId <= 0)
                        {
                            throw new ArgumentOutOfRangeException(nameof(appId), appId, "app_id 必须是正 int32。");
                        }
                        if (!IsSupportedPlatformName(platform))
                        {
                            throw new ArgumentException("platform 必须是 Android、iOS 或 WebGL。", nameof(platform));
                        }
                        if (!IsSupportedChannelName(channel))
                        {
                            throw new ArgumentException("channel 必须是有效运营渠道。", nameof(channel));
                        }
                        if (!Enum.IsDefined(typeof(DevelopMode), developMode))
                        {
                            throw new ArgumentOutOfRangeException(nameof(developMode), developMode, "develop_mode 必须是 Debug 或 Release。");
                        }
                        RequireText(version, nameof(version));

                        AppId = appId;
                        Platform = platform;
                        Channel = channel;
                        DevelopMode = developMode.ToString();
                        Version = version;
                    }

                    /// <summary>
                    /// 应用 ID。
                    /// </summary>
                    [JsonProperty("app_id", Order = 1)]
                    public int AppId { get; }

                    /// <summary>
                    /// 运行平台名称。
                    /// </summary>
                    [JsonProperty("platform", Order = 2)]
                    public string Platform { get; }

                    /// <summary>
                    /// 运营渠道名称。
                    /// </summary>
                    [JsonProperty("channel", Order = 3)]
                    public string Channel { get; }

                    /// <summary>
                    /// 开发模式名称，只会输出 Debug 或 Release。
                    /// </summary>
                    [JsonProperty("develop_mode", Order = 4)]
                    public string DevelopMode { get; }

                    /// <summary>
                    /// 客户端版本号。
                    /// </summary>
                    [JsonProperty("version", Order = 5)]
                    public string Version { get; }

                }

                /// <summary>
                /// 一次 Editor HTTP 请求的不可变响应快照。
                /// </summary>
                public sealed class Response
                {
                    /// <summary>
                    /// 创建已经脱离 HttpResponseMessage 生命周期的响应快照。
                    /// </summary>
                    /// <param name="statusCode">HTTP 数字状态码。</param>
                    /// <param name="isSuccessStatusCode">状态码是否属于成功范围。</param>
                    /// <param name="reasonPhrase">HTTP 原因短语。</param>
                    /// <param name="body">响应正文。</param>
                    /// <param name="headers">普通响应头与内容响应头的只读副本。</param>
                    internal Response(int statusCode, bool isSuccessStatusCode, string reasonPhrase, string body, IReadOnlyDictionary<string, IReadOnlyList<string>> headers)
                    {
                        StatusCode = statusCode;
                        IsSuccessStatusCode = isSuccessStatusCode;
                        ReasonPhrase = reasonPhrase ?? string.Empty;
                        Body = body ?? string.Empty;
                        Headers = headers ?? throw new ArgumentNullException(nameof(headers));
                    }

                    /// <summary>
                    /// HTTP 数字状态码。
                    /// </summary>
                    public int StatusCode { get; }

                    /// <summary>
                    /// 状态码是否属于成功范围。
                    /// </summary>
                    public bool IsSuccessStatusCode { get; }

                    /// <summary>
                    /// HTTP 原因短语；响应未提供时为空字符串。
                    /// </summary>
                    public string ReasonPhrase { get; }

                    /// <summary>
                    /// 完整响应正文；响应无 Content 时为空字符串。
                    /// </summary>
                    public string Body { get; }

                    /// <summary>
                    /// 普通响应头与内容响应头的大小写不敏感只读深拷贝。
                    /// </summary>
                    public IReadOnlyDictionary<string, IReadOnlyList<string>> Headers { get; }
                }

                /// <summary>
                /// 从当前激活 ConfigRuntime 与 PlayerSettings 构造默认请求头。
                /// </summary>
                /// <returns>不可变的默认请求头。</returns>
                public static RequestHeader CreateDefaultHeader()
                {
                    ConfigRuntimeSO runtime = Config.RuntimeProvider.GetCurrent();
                    return CreateDefaultHeader(runtime);
                }

                /// <summary>
                /// 从指定 ConfigRuntime 与当前 PlayerSettings 构造默认请求头。
                /// </summary>
                /// <param name="runtime">本次协议已锚定的 ConfigRuntime 导出物。</param>
                /// <returns>不可变的默认请求头。</returns>
                public static RequestHeader CreateDefaultHeader(ConfigRuntimeSO runtime)
                {
                    PlatformType activePlatform = Config.ActivePlatform.RequireCurrent("创建 Editor 默认请求头");
                    return CreateDefaultHeader(runtime, activePlatform, PlayerSettings.bundleVersion);
                }

                /// <summary>
                /// 使用显式的 Editor 环境快照构造默认请求头，供确定性测试与上层同源编排复用。
                /// </summary>
                /// <param name="runtime">ConfigRuntime 导出物。</param>
                /// <param name="activePlatform">Unity 当前活动构建平台的 Nova 映射。</param>
                /// <param name="version">PlayerSettings 版本快照。</param>
                /// <returns>不可变的默认请求头。</returns>
                internal static RequestHeader CreateDefaultHeader(ConfigRuntimeSO runtime, PlatformType activePlatform, string version)
                {
                    if (runtime == null)
                    {
                        throw new InvalidOperationException("当前 ConfigRuntime 不存在，请先导出 Config。");
                    }
                    if (runtime.AppConfigs == null)
                    {
                        throw new InvalidOperationException("当前 ConfigRuntime.AppConfigs 不存在，无法创建默认请求头。");
                    }
                    if (!IsSupportedPlatform(runtime.Platform))
                    {
                        throw new InvalidOperationException($"ConfigRuntime.Platform={runtime.Platform} 不是受支持的平台。");
                    }
                    if (runtime.Platform != activePlatform)
                    {
                        throw new InvalidOperationException($"ConfigRuntime.Platform={runtime.Platform} 与 Unity Active Platform={activePlatform} 不一致。");
                    }
                    if (!IsSupportedChannel(runtime.Channel))
                    {
                        throw new InvalidOperationException($"ConfigRuntime.Channel={runtime.Channel} 不是受支持的渠道。");
                    }
                    if (!Enum.IsDefined(typeof(DevelopMode), runtime.DevelopMode))
                    {
                        throw new InvalidOperationException($"ConfigRuntime.DevelopMode={runtime.DevelopMode} 未定义。");
                    }
                    if (!int.TryParse(runtime.AppConfigs.AppID, NumberStyles.None, CultureInfo.InvariantCulture, out int appId) || appId <= 0)
                    {
                        throw new InvalidOperationException("ConfigRuntime.AppConfigs.AppID 必须是无符号前缀的正 int32。");
                    }
                    if (string.IsNullOrWhiteSpace(version))
                    {
                        throw new InvalidOperationException("PlayerSettings version 不能为空。");
                    }
                    return new RequestHeader(appId, runtime.Platform.ToString(), runtime.Channel.ToString(), runtime.DevelopMode, version);
                }

                /// <summary>
                /// 使用公共静态客户端发送一次 Editor JSON 请求。
                /// </summary>
                /// <param name="method">HTTP 方法。</param>
                /// <param name="requestUri">绝对 HTTP/HTTPS 地址。</param>
                /// <param name="json">UTF-8 JSON 正文。</param>
                /// <param name="headers">额外请求头；不能覆盖 Content-Type。</param>
                /// <param name="timeout">本次请求超时。</param>
                /// <param name="cancellationToken">调用方取消令牌。</param>
                /// <returns>不依赖底层响应对象生命周期的响应快照。</returns>
                public static UniTask<Response> SendJsonAsync(HttpMethod method, Uri requestUri, string json, IReadOnlyDictionary<string, string> headers, TimeSpan timeout, CancellationToken cancellationToken = default)
                {
                    return SendJsonAsync(method, requestUri, json, headers, timeout, cancellationToken, s_Client.SendAsync);
                }

                /// <summary>
                /// 使用可注入发送器发送一次 Editor JSON 请求，供确定性测试复用完整传输逻辑。
                /// </summary>
                /// <param name="method">HTTP 方法。</param>
                /// <param name="requestUri">绝对 HTTP/HTTPS 地址。</param>
                /// <param name="json">UTF-8 JSON 正文。</param>
                /// <param name="headers">额外请求头；不能覆盖 Content-Type。</param>
                /// <param name="timeout">本次请求超时。</param>
                /// <param name="cancellationToken">调用方取消令牌。</param>
                /// <param name="sendAsync">实际发送 HTTP 请求的委托。</param>
                /// <returns>不依赖底层响应对象生命周期的响应快照。</returns>
                internal static async UniTask<Response> SendJsonAsync(HttpMethod method, Uri requestUri, string json, IReadOnlyDictionary<string, string> headers, TimeSpan timeout, CancellationToken cancellationToken, Func<HttpRequestMessage, CancellationToken, Task<HttpResponseMessage>> sendAsync)
                {
                    ValidateRequest(method, requestUri, json, headers, timeout, sendAsync);

                    using var request = new HttpRequestMessage(method, requestUri)
                    {
                        Content = new StringContent(json, Encoding.UTF8, "application/json")
                    };
                    AddRequestHeaders(request, headers);

                    using var linkedCancellation = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
                    linkedCancellation.CancelAfter(timeout);
                    try
                    {
                        using HttpResponseMessage response = await sendAsync(request, linkedCancellation.Token);
                        if (response == null)
                        {
                            throw new InvalidOperationException("Editor HTTP 发送器返回了空响应。");
                        }

                        string body = response.Content == null
                            ? string.Empty
                            : await response.Content.ReadAsStringAsync();
                        return new Response((int)response.StatusCode, response.IsSuccessStatusCode, response.ReasonPhrase, body, CopyResponseHeaders(response));
                    }
                    catch (OperationCanceledException exception) when (!cancellationToken.IsCancellationRequested)
                    {
                        throw new TimeoutException($"Editor HTTP 请求超时：{requestUri}，超时={timeout.TotalSeconds.ToString(CultureInfo.InvariantCulture)} 秒。", exception);
                    }
                }

                /// <summary>
                /// 校验发送 JSON 请求所需的全部基础参数。
                /// </summary>
                private static void ValidateRequest(HttpMethod method, Uri requestUri, string json, IReadOnlyDictionary<string, string> headers, TimeSpan timeout, Func<HttpRequestMessage, CancellationToken, Task<HttpResponseMessage>> sendAsync)
                {
                    if (method == null) throw new ArgumentNullException(nameof(method));
                    if (requestUri == null) throw new ArgumentNullException(nameof(requestUri));
                    if (!requestUri.IsAbsoluteUri ||
                        (requestUri.Scheme != Uri.UriSchemeHttp && requestUri.Scheme != Uri.UriSchemeHttps))
                    {
                        throw new ArgumentException("requestUri 必须是绝对 HTTP/HTTPS 地址。", nameof(requestUri));
                    }
                    if (json == null) throw new ArgumentNullException(nameof(json));
                    if (headers == null) throw new ArgumentNullException(nameof(headers));
                    if (timeout <= TimeSpan.Zero)
                    {
                        throw new ArgumentOutOfRangeException(nameof(timeout), timeout, "timeout 必须大于零。");
                    }
                    if (sendAsync == null) throw new ArgumentNullException(nameof(sendAsync));
                }

                /// <summary>
                /// 把调用方额外 Header 写入请求，并阻止覆盖内容类型或静默丢弃非法值。
                /// </summary>
                private static void AddRequestHeaders(HttpRequestMessage request, IReadOnlyDictionary<string, string> headers)
                {
                    foreach (KeyValuePair<string, string> pair in headers)
                    {
                        if (string.Equals(pair.Key, "Content-Type", StringComparison.OrdinalIgnoreCase))
                        {
                            throw new ArgumentException("Content-Type 由 Editor HTTP 门面统一设置。", nameof(headers));
                        }
                        if (string.IsNullOrWhiteSpace(pair.Key) || pair.Value == null)
                        {
                            throw new ArgumentException("请求 Header 名称不能为空且值不能为 null。", nameof(headers));
                        }

                        try
                        {
                            if (!request.Headers.TryAddWithoutValidation(pair.Key, pair.Value))
                            {
                                throw new ArgumentException($"请求 Header {pair.Key} 不是有效的普通请求头。", nameof(headers));
                            }
                        }
                        catch (FormatException exception)
                        {
                            throw new ArgumentException($"请求 Header {pair.Key} 格式无效。", nameof(headers), exception);
                        }
                    }
                }

                /// <summary>
                /// 深拷贝普通与内容响应头，确保返回值不依赖 HttpResponseMessage 生命周期。
                /// </summary>
                private static IReadOnlyDictionary<string, IReadOnlyList<string>> CopyResponseHeaders(HttpResponseMessage response)
                {
                    var mutable = new Dictionary<string, List<string>>(StringComparer.OrdinalIgnoreCase);
                    AppendHeaders(mutable, response.Headers);
                    if (response.Content != null)
                    {
                        AppendHeaders(mutable, response.Content.Headers);
                    }

                    var frozen = new Dictionary<string, IReadOnlyList<string>>(StringComparer.OrdinalIgnoreCase);
                    foreach (KeyValuePair<string, List<string>> pair in mutable)
                    {
                        frozen.Add(pair.Key, Array.AsReadOnly(pair.Value.ToArray()));
                    }
                    return new ReadOnlyDictionary<string, IReadOnlyList<string>>(frozen);
                }

                /// <summary>
                /// 将一组响应头追加到大小写不敏感的临时集合。
                /// </summary>
                private static void AppendHeaders(IDictionary<string, List<string>> destination, IEnumerable<KeyValuePair<string, IEnumerable<string>>> source)
                {
                    foreach (KeyValuePair<string, IEnumerable<string>> pair in source)
                    {
                        if (!destination.TryGetValue(pair.Key, out List<string> values))
                        {
                            values = new List<string>();
                            destination.Add(pair.Key, values);
                        }
                        values.AddRange(pair.Value);
                    }
                }

                /// <summary>
                /// 判断 Nova 平台是否允许进入 Editor 管理协议。
                /// </summary>
                private static bool IsSupportedPlatform(PlatformType platform)
                {
                    return platform == PlatformType.Android ||
                           platform == PlatformType.iOS ||
                           platform == PlatformType.WebGL;
                }

                /// <summary>
                /// 判断平台字符串是否为协议允许值。
                /// </summary>
                private static bool IsSupportedPlatformName(string platform)
                {
                    return platform == nameof(PlatformType.Android) ||
                           platform == nameof(PlatformType.iOS) ||
                           platform == nameof(PlatformType.WebGL);
                }

                /// <summary>
                /// 判断 Nova 渠道是否允许进入 Editor 管理协议。
                /// </summary>
                private static bool IsSupportedChannel(ChannelType channel)
                {
                    return channel == ChannelType.Official ||
                           channel == ChannelType.Google ||
                           channel == ChannelType.Apple ||
                           channel == ChannelType.WeChat ||
                           channel == ChannelType.TikTok ||
                           channel == ChannelType.Alipay;
                }

                /// <summary>
                /// 判断渠道字符串是否为协议允许值。
                /// </summary>
                private static bool IsSupportedChannelName(string channel)
                {
                    return channel == nameof(ChannelType.Official) ||
                           channel == nameof(ChannelType.Google) ||
                           channel == nameof(ChannelType.Apple) ||
                           channel == nameof(ChannelType.WeChat) ||
                           channel == nameof(ChannelType.TikTok) ||
                           channel == nameof(ChannelType.Alipay);
                }

                /// <summary>
                /// 校验协议必填字符串但保留调用方原值。
                /// </summary>
                /// <param name="value">待校验字符串。</param>
                /// <param name="parameterName">异常使用的参数名。</param>
                private static void RequireText(string value, string parameterName)
                {
                    if (string.IsNullOrWhiteSpace(value))
                    {
                        throw new ArgumentException($"{parameterName} 不能为空。", parameterName);
                    }
                }
            }
        }
    }
}
