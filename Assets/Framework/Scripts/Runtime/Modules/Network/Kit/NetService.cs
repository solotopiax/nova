/***************************************************************
 * (c) copyright 2026 - 2030, Solotopia
 * All Rights Reserved.
 * -------------------------------------------------------------
 * filename:  NetService.cs
 * author:    taoye
 * created:   2026/5/26
 * descrip:   网络请求静态编排器，封装 Protobuf + AES-128-CBC 请求全流程
 ***************************************************************/

using System;
using System.ComponentModel;
using System.Threading;
using Cysharp.Threading.Tasks;
using Google.Protobuf;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace NovaFramework.Runtime
{
    /// <summary>
    /// 网络请求静态编排器，封装 Protobuf + AES-128-CBC 请求全流程。
    /// 无需业务层调用 Initialize，配置在运行时从 Nova.Config.AppConfigs 与 Nova.SDK 自动读取。
    /// 全局调试开关由 SetDebugMode 控制；单次请求可通过 debugModeOverride 覆盖。
    /// </summary>
    public static class NetService
    {
        private static readonly object s_IdentityLock = new object();
        private static string s_UID = string.Empty;
        private static string s_OpenID = string.Empty;
        private static int s_IdentityOperationActive;

        /// <summary>
        /// 当前业务流程确认的用户 UID。
        /// 不做持久化，进程重启归空。
        /// </summary>
        public static string UID
        {
            get
            {
                lock (s_IdentityLock)
                {
                    return s_UID;
                }
            }
        }

        /// <summary>
        /// 当前业务流程确认的第三方 OpenID。
        /// 不做持久化，进程重启归空；空字符串表示当前没有可用 OpenID。
        /// </summary>
        public static string OpenID
        {
            get
            {
                lock (s_IdentityLock)
                {
                    return s_OpenID;
                }
            }
        }

        /// <summary>
        /// 全局调试开关。调试模式下跳过 AES 加解密，发送 X-Debug-Plain 头。
        /// 默认 false，可由 NetworkComponentKitExtensions.SetDebugMode 或 SetDebugMode 方法修改。
        /// </summary>
        public static bool IsDebugMode { get; private set; }

        /// <summary>
        /// 写回当前 UID。仅供 Login、Bind 等 Network Kit 根据权威业务结果或清理登录态时调用。
        /// 带 EditorBrowsable(Never) 以在 IDE 补全中隐藏，防止业务侧误调。
        /// </summary>
        /// <param name="uid">服务端返回的 UID；为 null 时视为空串。</param>
        [EditorBrowsable(EditorBrowsableState.Never)]
        public static void SetUID(string uid)
        {
            lock (s_IdentityLock)
            {
                s_UID = uid ?? string.Empty;
            }
        }

        /// <summary>
        /// 写回当前 OpenID。仅供 Login、Bind 等 Network Kit 根据权威业务结果或清理登录态时调用。
        /// 带 EditorBrowsable(Never) 以在 IDE 补全中隐藏，防止业务侧绕过登录与绑定流程直接改写。
        /// </summary>
        /// <param name="openid">当前第三方账号唯一标识；为 null 时视为空串。</param>
        [EditorBrowsable(EditorBrowsableState.Never)]
        public static void SetOpenID(string openid)
        {
            lock (s_IdentityLock)
            {
                s_OpenID = openid ?? string.Empty;
            }
        }

        /// <summary>
        /// 原子写入当前已由服务端确认的 UID/OpenID 身份对。
        /// </summary>
        [EditorBrowsable(EditorBrowsableState.Never)]
        public static void SetIdentity(string uid, string openid)
        {
            lock (s_IdentityLock)
            {
                s_UID = uid ?? string.Empty;
                s_OpenID = openid ?? string.Empty;
            }
        }

        /// <summary>
        /// 原子读取当前已由服务端确认的 UID/OpenID 身份对。
        /// </summary>
        [EditorBrowsable(EditorBrowsableState.Never)]
        public static void GetIdentity(out string uid, out string openid)
        {
            lock (s_IdentityLock)
            {
                uid = s_UID;
                openid = s_OpenID;
            }
        }

        /// <summary>
        /// 原子清空当前身份，后续请求 Header 不再携带 UID/OpenID。
        /// </summary>
        [EditorBrowsable(EditorBrowsableState.Never)]
        public static void ClearIdentity()
        {
            SetIdentity(string.Empty, string.Empty);
        }

        /// <summary>
        /// 尝试获取全局身份变更操作租约；已有 Login/Delete/Bind/Resolve 执行时立即返回 null，不排队。
        /// </summary>
        [EditorBrowsable(EditorBrowsableState.Never)]
        public static IDisposable TryBeginIdentityOperation()
        {
            return Interlocked.CompareExchange(ref s_IdentityOperationActive, 1, 0) == 0
                ? new IdentityOperationLease()
                : null;
        }

        private sealed class IdentityOperationLease : IDisposable
        {
            private int m_Disposed;

            public void Dispose()
            {
                if (Interlocked.Exchange(ref m_Disposed, 1) == 0)
                {
                    Volatile.Write(ref s_IdentityOperationActive, 0);
                }
            }
        }

        /// <summary>
        /// 设置全局调试模式开关。
        /// </summary>
        /// <param name="debugMode">是否启用调试模式。</param>
        public static void SetDebugMode(bool debugMode)
        {
            IsDebugMode = debugMode;
        }

        /// <summary>
        /// 发送 Protobuf 请求并返回泛型响应。
        /// 流程：URL 解析 → NetBuilder.SerializeBody → NetBuilder.Encrypt → HTTP POST → NetParser.Decrypt → BaseResponse 解析 → 业务 Proto 解析。
        /// AesKey / AesIV 在运行时从 Nova.Config.AppConfigs.AppAesKey / AppAesIV 读取。
        /// 直接传入业务 Proto Body（调用方在 Body 内自行填充 Head 字段），无需再包装为 NetRequest 容器。
        /// 仅供 Network 子包使用，业务侧请通过 Login 等业务 Service 接入。
        /// </summary>
        /// <typeparam name="TReq">请求 Proto 消息类型。</typeparam>
        /// <typeparam name="TResp">响应 Proto 消息类型。</typeparam>
        /// <param name="cmdRow">NetCmd 指令行数据，由调用方通过 GetNetCmd 获取表后点出。</param>
        /// <param name="request">直接传入业务 Proto Body 实例；Body 内须已含 Head（由 NetBuilder.BuildHeader() 填充）。</param>
        /// <param name="parser">响应 Proto 消息解析器（通常为 TResp.Parser）。</param>
        /// <param name="debugModeOverride">单次请求调试模式覆盖；为 null 时沿用全局 IsDebugMode。</param>
        /// <returns>包含业务响应数据或错误信息的 NetResponse。</returns>
        [EditorBrowsable(EditorBrowsableState.Never)]
        public static async UniTask<NetResponse<TResp>> SendAsync<TReq, TResp>(
            INetworkCmdRow cmdRow,
            TReq request,
            MessageParser<TResp> parser,
            bool? debugModeOverride = null)
            where TReq : IMessage<TReq>
            where TResp : IMessage<TResp>
        {
            bool effectiveDebug = debugModeOverride ?? IsDebugMode;
            string netCmdName = cmdRow?.Name ?? "unknown";

            string url = Nova.Network.ResolveNetCmdUrl(cmdRow);
            if (string.IsNullOrEmpty(url))
            {
                LogRequest(netCmdName, url, request, false, "url_not_found");
                Log.Warning(LogTag.Network, "NetService.SendAsync：未找到 NetCmd URL，name={0}。", netCmdName);
                return NetResponse<TResp>.Fail(NetErrorCode.URL_NOT_FOUND, Txt.Format("NetCmd not found: {0}", netCmdName));
            }

            string aesKey = Nova.Config.AppConfigs.AppAesKey ?? string.Empty;
            string aesIv = Nova.Config.AppConfigs.AppAesIV ?? string.Empty;

            int appId = 0;
            if (!int.TryParse(Nova.Config.AppConfigs.AppID, out appId))
            {
                Log.Warning(LogTag.Network, "NetService.SendAsync：Nova.Config.AppConfigs.AppID 无法解析为 int32，已回退为 0。");
            }

            if (!effectiveDebug && (string.IsNullOrEmpty(aesKey) || string.IsNullOrEmpty(aesIv)))
            {
                LogRequest(netCmdName, url, request, false, "aes_config_missing");
                Log.Error(LogTag.Network, "NetService.SendAsync：AES Key/IV not configured, please check Nova.Config.AppConfigs. name={0}.", netCmdName);
                return NetResponse<TResp>.Fail(NetErrorCode.AES_ENCRYPT_FAILED, "AES Key/IV not configured");
            }

            byte[] protoBytes = NetBuilder.SerializeBody(request);

            byte[] bodyBytes;
            string headerInfos;
            if (effectiveDebug)
            {
                bodyBytes = protoBytes;
                headerInfos = NetBuilder.BuildDebugHeaderInfos(appId);
            }
            else
            {
                try
                {
                    bodyBytes = NetBuilder.Encrypt(protoBytes, aesKey, aesIv);
                    headerInfos = NetBuilder.BuildHeaderInfos(appId, aesIv);
                }
                catch (Exception e)
                {
                    LogRequest(netCmdName, url, request, false, "aes_encrypt_failed");
                    Log.Error(LogTag.Network, "NetService.SendAsync：AES 加密失败，name={0}，error={1}。", netCmdName, e.Message);
                    return NetResponse<TResp>.Fail(NetErrorCode.AES_ENCRYPT_FAILED, $"AES encrypt failed: {e.Message}");
                }
            }

            HttpResponse httpResponse = null;
            try
            {
                LogRequest(netCmdName, url, request, true);
                try
                {
                    httpResponse = await Nova.Network.PostRawDataAsync(url, bodyBytes, -1f, -1f, headerInfos);
                }
                catch (Exception e)
                {
                    LogResponseFailure<TResp>(netCmdName, httpResponse, "transport", e.Message);
                    throw;
                }

                if (!httpResponse.IsSuccess || httpResponse.RawData == null)
                {
                    string error = httpResponse.Error ?? "Network request failed";
                    LogResponseFailure<TResp>(netCmdName, httpResponse, "http", error);
                    Log.Warning(LogTag.Network, "NetService.SendAsync：HTTP 请求失败，name={0}，error={1}。", netCmdName, error);
                    return NetResponse<TResp>.Fail(NetErrorCode.NETWORK_ERROR, error);
                }

                byte[] decryptedBytes;
                if (effectiveDebug)
                {
                    decryptedBytes = httpResponse.RawData;
                }
                else
                {
                    try
                    {
                        decryptedBytes = NetParser.Decrypt(httpResponse.RawData, aesKey, aesIv);
                    }
                    catch (Exception e)
                    {
                        LogResponseFailure<TResp>(netCmdName, httpResponse, "decrypt", e.Message);
                        Log.Error(LogTag.Network, "NetService.SendAsync：AES 解密失败，name={0}，error={1}。", netCmdName, e.Message);
                        return NetResponse<TResp>.Fail(NetErrorCode.AES_DECRYPT_FAILED, $"AES decrypt failed: {e.Message}");
                    }
                }

                NetResult parseResult;
                try
                {
                    parseResult = NetParser.ParseResponse(decryptedBytes);
                }
                catch (Exception e)
                {
                    LogResponseFailure<TResp>(netCmdName, httpResponse, "base_response_parse", e.Message);
                    Log.Error(LogTag.Network, "NetService.SendAsync：BaseResponse 解析失败，name={0}，error={1}。", netCmdName, e.Message);
                    return NetResponse<TResp>.Fail(NetErrorCode.PROTO_PARSE_FAILED, $"BaseResponse parse failed: {e.Message}");
                }

                if (parseResult.Code != NetErrorCode.SUCCESS)
                {
                    Log.Warning(LogTag.Network, "NetService.SendAsync：服务端返回业务错误，name={0}，code={1}，msg={2}。", netCmdName, parseResult.Code, parseResult.Message);
                    // 业务错误码下服务端仍可能携带业务体（如绑定冲突返回 existing_uid）；尝试解析并随失败响应带回，
                    // 解析失败或无业务体则降级为不带 data 的失败响应，不影响错误码/描述的透传。
                    if (parseResult.BusinessData != null && parseResult.BusinessData.Length > 0)
                    {
                        try
                        {
                            TResp errorData = parser.ParseFrom(parseResult.BusinessData);
                            LogResponse(netCmdName, httpResponse.StatusCode, parseResult.Code, parseResult.Message,
                                errorData, rawDataLength: httpResponse.RawData.Length);
                            return NetResponse<TResp>.Fail(parseResult.Code, parseResult.Message, errorData);
                        }
                        catch (Exception e)
                        {
                            Log.Warning(LogTag.Network, "NetService.SendAsync：业务错误响应体解析失败，降级为不带 data，name={0}，error={1}。", netCmdName, e.Message);
                            LogResponse<TResp>(netCmdName, httpResponse.StatusCode, parseResult.Code, parseResult.Message,
                                default, "business_response_parse", e.Message, httpResponse.RawData.Length);
                            return NetResponse<TResp>.Fail(parseResult.Code, parseResult.Message);
                        }
                    }
                    LogResponse<TResp>(netCmdName, httpResponse.StatusCode, parseResult.Code, parseResult.Message,
                        default, rawDataLength: httpResponse.RawData.Length);
                    return NetResponse<TResp>.Fail(parseResult.Code, parseResult.Message);
                }

                TResp responseData;
                try
                {
                    responseData = parser.ParseFrom(parseResult.BusinessData);
                }
                catch (Exception e)
                {
                    Log.Error(LogTag.Network, "NetService.SendAsync：业务 Proto 解析失败，name={0}，error={1}。", netCmdName, e.Message);
                    LogResponse<TResp>(netCmdName, httpResponse.StatusCode, parseResult.Code, parseResult.Message,
                        default, "business_response_parse", e.Message, httpResponse.RawData.Length);
                    return NetResponse<TResp>.Fail(NetErrorCode.PROTO_PARSE_FAILED, $"Response parse failed: {e.Message}");
                }

                LogResponse(netCmdName, httpResponse.StatusCode, parseResult.Code, parseResult.Message,
                    responseData, rawDataLength: httpResponse.RawData.Length);
                return NetResponse<TResp>.Success(responseData);
            }
            finally
            {
                if (httpResponse != null)
                {
                    ReferencePool.Put(httpResponse);
                }
            }
        }

        /// <summary>
        /// 以统一单行 JSON 输出客户端请求信息；已发送请求使用 Debug，未发送请求使用 Warning。
        /// 调用在非 Editor、非 Development Build 中会被编译器移除。
        /// </summary>
        /// <typeparam name="TReq">请求 Proto 消息类型。</typeparam>
        /// <param name="netCmdName">网络指令名称。</param>
        /// <param name="url">最终请求地址。</param>
        /// <param name="request">待发送的请求 Proto。</param>
        /// <param name="sent">是否已进入实际 HTTP 发送阶段。</param>
        /// <param name="reason">未发送原因；sent 为 true 时为空。</param>
        [System.Diagnostics.Conditional("UNITY_EDITOR")]
        [System.Diagnostics.Conditional("DEVELOPMENT_BUILD")]
        private static void LogRequest<TReq>(
            string netCmdName,
            string url,
            TReq request,
            bool sent,
            string reason = null)
            where TReq : IMessage<TReq>
        {
            try
            {
                var logData = new JObject
                {
                    ["source"] = "Nova.NetService",
                    ["stage"] = "request",
                    ["name"] = netCmdName,
                    ["url"] = url ?? string.Empty,
                    ["sent"] = sent,
                    ["data"] = FormatProtoJson(request)
                };
                if (!sent)
                {
                    logData["reason"] = reason ?? string.Empty;
                    Log.Warning(LogTag.Network, logData.ToString(Formatting.None));
                    return;
                }
                Log.Debug(LogTag.Network, logData.ToString(Formatting.None));
            }
            catch (Exception e)
            {
                Log.Warning(LogTag.Network, "NetService 请求日志格式化失败：name={0}，error={1}。", netCmdName, e.Message);
            }
        }

        /// <summary>
        /// 输出尚未得到可解析业务响应时的统一响应终态，保留已获取的 HTTP 状态码与原始响应长度。
        /// </summary>
        /// <typeparam name="TResp">响应 Proto 消息类型。</typeparam>
        /// <param name="netCmdName">网络指令名称。</param>
        /// <param name="httpResponse">HTTP 响应；传输层抛异常且未返回响应时为空。</param>
        /// <param name="failureStage">失败阶段。</param>
        /// <param name="error">失败信息。</param>
        [System.Diagnostics.Conditional("UNITY_EDITOR")]
        [System.Diagnostics.Conditional("DEVELOPMENT_BUILD")]
        private static void LogResponseFailure<TResp>(
            string netCmdName,
            HttpResponse httpResponse,
            string failureStage,
            string error)
            where TResp : IMessage<TResp>
        {
            LogResponse<TResp>(
                netCmdName,
                httpResponse?.StatusCode,
                null,
                string.Empty,
                default,
                failureStage,
                error,
                httpResponse?.RawData?.Length ?? 0);
        }

        /// <summary>
        /// 以统一单行 JSON 输出服务端响应及解析结果；调用在非 Editor、非 Development Build 中会被编译器移除。
        /// </summary>
        /// <typeparam name="TResp">响应 Proto 消息类型。</typeparam>
        /// <param name="netCmdName">网络指令名称。</param>
        /// <param name="httpStatusCode">HTTP 状态码；传输层未返回响应时为空。</param>
        /// <param name="code">服务端 BaseResponse 错误码；协议尚未解析时为空。</param>
        /// <param name="message">服务端 BaseResponse 错误信息。</param>
        /// <param name="response">解析后的业务响应 Proto；无业务体或解析失败时为 null。</param>
        /// <param name="failureStage">失败阶段；响应完整解析时为空。</param>
        /// <param name="error">失败信息；响应完整解析时为空。</param>
        /// <param name="rawDataLength">HTTP 原始响应字节数。</param>
        [System.Diagnostics.Conditional("UNITY_EDITOR")]
        [System.Diagnostics.Conditional("DEVELOPMENT_BUILD")]
        private static void LogResponse<TResp>(
            string netCmdName,
            int? httpStatusCode,
            int? code,
            string message,
            TResp response,
            string failureStage = null,
            string error = null,
            int rawDataLength = 0)
            where TResp : IMessage<TResp>
        {
            try
            {
                var logData = new JObject
                {
                    ["source"] = "Nova.NetService",
                    ["stage"] = "response",
                    ["name"] = netCmdName,
                    ["httpStatusCode"] = httpStatusCode.HasValue ? new JValue(httpStatusCode.Value) : JValue.CreateNull(),
                    ["code"] = code.HasValue ? new JValue(code.Value) : JValue.CreateNull(),
                    ["msg"] = message ?? string.Empty,
                    ["data"] = FormatProtoJson(response),
                    ["rawDataLength"] = rawDataLength
                };
                if (!string.IsNullOrEmpty(failureStage))
                {
                    logData["failureStage"] = failureStage;
                }
                if (!string.IsNullOrEmpty(error))
                {
                    logData["error"] = error;
                }
                Log.Debug(LogTag.Network, logData.ToString(Formatting.None));
            }
            catch (Exception e)
            {
                Log.Warning(LogTag.Network, "NetService 响应日志格式化失败：name={0}，error={1}。", netCmdName, e.Message);
            }
        }

        /// <summary>
        /// 使用 Protobuf 官方 JSON 规则格式化消息，保留字段映射、枚举和 ByteString 的标准语义。
        /// </summary>
        /// <param name="message">待格式化的 Proto 消息，可为空。</param>
        /// <returns>可直接嵌入统一日志对象的 JSON 节点。</returns>
        private static JToken FormatProtoJson(IMessage message)
        {
            return message == null
                ? JValue.CreateNull()
                : JToken.Parse(JsonFormatter.Default.Format(message));
        }
    }
}
