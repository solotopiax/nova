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
using System.Collections.Generic;
using System.ComponentModel;
using System.Text;
using System.Threading;
using Cysharp.Threading.Tasks;
using Google.Protobuf;

namespace NovaFramework.Runtime
{
    /// <summary>
    /// 网络请求静态编排器，封装 Protobuf + AES-128-CBC 请求全流程。
    /// 无需业务层调用 Initialize，配置在运行时从 Nova.Config.AppConfigs 与 Nova.SDK 自动读取。
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
        /// <returns>包含业务响应数据或错误信息的 NetResponse。</returns>
        [EditorBrowsable(EditorBrowsableState.Never)]
        public static UniTask<NetResponse<TResp>> SendAsync<TReq, TResp>(
            INetworkCmdRow cmdRow,
            TReq request,
            MessageParser<TResp> parser)
            where TReq : IMessage<TReq>
            where TResp : IMessage<TResp>
        {
            return SendAsync(cmdRow, request, parser, CancellationToken.None);
        }

        /// <summary>
        /// 发送支持主动取消的 Protobuf 请求；取消会中止当前 UWR，且不会继续备用候选。
        /// </summary>
        /// <typeparam name="TReq">请求 Proto 消息类型。</typeparam>
        /// <typeparam name="TResp">响应 Proto 消息类型。</typeparam>
        /// <param name="cmdRow">NetCmd 指令行数据。</param>
        /// <param name="request">直接传入的业务 Proto Body。</param>
        /// <param name="parser">响应 Proto 消息解析器。</param>
        /// <param name="cancellationToken">取消令牌。</param>
        /// <returns>包含业务响应数据或错误信息的 NetResponse。</returns>
        [EditorBrowsable(EditorBrowsableState.Never)]
        public static async UniTask<NetResponse<TResp>> SendAsync<TReq, TResp>(
            INetworkCmdRow cmdRow,
            TReq request,
            MessageParser<TResp> parser,
            CancellationToken cancellationToken)
            where TReq : IMessage<TReq>
            where TResp : IMessage<TResp>
        {
            string netCmdName = cmdRow?.Name ?? "unknown";
            cancellationToken.ThrowIfCancellationRequested();

            IReadOnlyList<string> routeUrls = Nova.Network.ResolveNetCmdUrls(cmdRow);
            if (routeUrls.Count == 0)
            {
                Log.Warning(LogTag.Network, "NetService.SendAsync：未找到 NetCmd URL，name={0}。", netCmdName);
                return NetResponse<TResp>.Fail(NetErrorCode.URL_NOT_FOUND, Txt.Format("NetCmd not found: {0}", netCmdName));
            }

            ConfigComponent config = Nova.Config;
            AppConfigs appConfigs = config?.AppConfigs;
            string aesKey = appConfigs?.AppAesKey ?? string.Empty;
            string aesIv = appConfigs?.AppAesIV ?? string.Empty;

            if (config == null || !config.IsLoadOver ||
                !IsValidAppAesSecret(aesKey) || !IsValidAppAesSecret(aesIv))
            {
                Log.Error(LogTag.Network,
                    "NetService.SendAsync：应用配置中的 App Aes Key / App Aes IV 未就绪或无效，name={0}。请先完成 await Nova.Config.LoadAsync()；然后在 Nova/Open Config → 通用配置 → 应用配置 → App Aes Key / App Aes IV 中，为当前 Platform × Channel × DevelopMode 配置 UTF-8 各 16 字节的值，保存后重新导出 ConfigRuntimeSO。",
                    netCmdName);
                return NetResponse<TResp>.Fail(NetErrorCode.AES_ENCRYPT_FAILED, "App AES Key/IV is not ready");
            }

            int appId = 0;
            if (!int.TryParse(appConfigs.AppID, out appId))
            {
                Log.Warning(LogTag.Network, "NetService.SendAsync：Nova.Config.AppConfigs.AppID 无法解析为 int32，已回退为 0。");
            }

            byte[] protoBytes = NetBuilder.SerializeBody(request);

            byte[] bodyBytes;
            string headerInfos;
            try
            {
                bodyBytes = NetBuilder.Encrypt(protoBytes, aesKey, aesIv);
                headerInfos = NetBuilder.BuildHeaderInfos(appId, aesIv);
            }
            catch (Exception e)
            {
                Log.Error(LogTag.Network, "NetService.SendAsync：AES 加密失败，name={0}，error={1}。", netCmdName, e.Message);
                return NetResponse<TResp>.Fail(NetErrorCode.AES_ENCRYPT_FAILED, $"AES encrypt failed: {e.Message}");
            }

            HttpResponse httpResponse = null;
            try
            {
                httpResponse = await Nova.Network.PostBusinessRawDataAsync(
                    routeUrls,
                    cmdRow?.HostKey,
                    netCmdName,
                    bodyBytes,
                    -1f,
                    headerInfos,
                    cancellationToken
                );

                if (!httpResponse.IsSuccess || httpResponse.RawData == null)
                {
                    string error = httpResponse.Error ?? "Network request failed";
                    Log.Warning(LogTag.Network, "NetService.SendAsync：HTTP 请求失败，name={0}，error={1}。", netCmdName, error);
                    return NetResponse<TResp>.Fail(NetErrorCode.NETWORK_ERROR, error);
                }

                byte[] decryptedBytes;
                try
                {
                    decryptedBytes = NetParser.Decrypt(httpResponse.RawData, aesKey, aesIv);
                }
                catch (Exception e)
                {
                    Log.Error(LogTag.Network, "NetService.SendAsync：AES 解密失败，name={0}，error={1}。", netCmdName, e.Message);
                    return NetResponse<TResp>.Fail(NetErrorCode.AES_DECRYPT_FAILED, $"AES decrypt failed: {e.Message}");
                }

                NetResult parseResult;
                try
                {
                    parseResult = NetParser.ParseResponse(decryptedBytes);
                }
                catch (Exception e)
                {
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
                            return NetResponse<TResp>.Fail(parseResult.Code, parseResult.Message, errorData);
                        }
                        catch (Exception e)
                        {
                            Log.Warning(LogTag.Network, "NetService.SendAsync：业务错误响应体解析失败，降级为不带 data，name={0}，error={1}。", netCmdName, e.Message);
                            return NetResponse<TResp>.Fail(parseResult.Code, parseResult.Message);
                        }
                    }
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
                    return NetResponse<TResp>.Fail(NetErrorCode.PROTO_PARSE_FAILED, $"Response parse failed: {e.Message}");
                }

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
        /// 校验应用协议 AES 单个凭据是否为可传入底层 AES 的 UTF-8 16 字节字符串。
        /// </summary>
        /// <param name="value">待校验的 AppConfigs AES Key 或 IV。</param>
        /// <returns>非空且 UTF-8 编码长度为 16 字节时返回 true。</returns>
        private static bool IsValidAppAesSecret(string value)
        {
            return !string.IsNullOrEmpty(value) && Encoding.UTF8.GetByteCount(value) == 16;
        }

    }
}
