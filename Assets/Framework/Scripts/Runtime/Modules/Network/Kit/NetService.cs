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
using Cysharp.Threading.Tasks;
using Google.Protobuf;
namespace NovaFramework.Runtime
{
    /// <summary>
    /// 网络请求静态编排器，封装 Protobuf + AES-128-CBC 请求全流程。
    /// 无需业务层调用 Initialize，配置在运行时从 Nova.Config.Common 与 Nova.SDK 自动读取。
    /// 全局调试开关由 SetDebugMode 控制；单次请求可通过 debugModeOverride 覆盖。
    /// </summary>
    public static class NetService
    {
        /// <summary>
        /// 当前登录用户 Uid，登录成功后由 Login 通过 SetUid 写回。
        /// 不做持久化，进程重启归空。
        /// </summary>
        public static string Uid { get; private set; } = string.Empty;

        /// <summary>
        /// 全局调试开关。调试模式下跳过 AES 加解密，发送 X-Debug-Plain 头。
        /// 默认 false，可由 NetworkComponentKitExtensions.SetDebugMode 或 SetDebugMode 方法修改。
        /// </summary>
        public static bool IsDebugMode { get; private set; }

        /// <summary>
        /// 写回当前登录用户 Uid。仅供 Login 子包（Login）在登录成功后调用。
        /// 带 EditorBrowsable(Never) 以在 IDE 补全中隐藏，防止业务侧误调。
        /// </summary>
        /// <param name="uid">服务端返回的 Uid；为 null 时视为空串。</param>
        [EditorBrowsable(EditorBrowsableState.Never)]
        public static void SetUid(string uid)
        {
            Uid = uid ?? string.Empty;
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
        /// AesKey / AesIV 在运行时从 Nova.Config.Common.AppAesKey / AppAesIV 读取。
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
                Log.Warning(LogTag.Network, "NetService.SendAsync：未找到 NetCmd URL，name={0}。", netCmdName);
                return NetResponse<TResp>.Fail(NetErrorCode.URL_NOT_FOUND, Txt.Format("NetCmd not found: {0}", netCmdName));
            }

            string aesKey = Nova.Config.Common.AppAesKey ?? string.Empty;
            string aesIv = Nova.Config.Common.AppAesIV ?? string.Empty;

            int appId = 0;
            if (!int.TryParse(Nova.Config.Common.AppID, out appId))
            {
                Log.Warning(LogTag.Network, "NetService.SendAsync：Nova.Config.Common.AppID 无法解析为 int32，已回退为 0。");
            }

            if (!effectiveDebug && (string.IsNullOrEmpty(aesKey) || string.IsNullOrEmpty(aesIv)))
            {
                Log.Error(LogTag.Network, "NetService.SendAsync：AES Key/IV not configured, please check Nova.Config.Common. name={0}.", netCmdName);
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
                    Log.Error(LogTag.Network, "NetService.SendAsync：AES 加密失败，name={0}，error={1}。", netCmdName, e.Message);
                    return NetResponse<TResp>.Fail(NetErrorCode.AES_ENCRYPT_FAILED, $"AES encrypt failed: {e.Message}");
                }
            }

            HttpResponse httpResponse = null;
            try
            {
                httpResponse = await Nova.Network.PostRawDataAsync(url, bodyBytes, -1f, -1f, headerInfos);
                if (!httpResponse.IsSuccess || httpResponse.RawData == null)
                {
                    string error = httpResponse.Error ?? "Network request failed";
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
                    Log.Error(LogTag.Network, "NetService.SendAsync：BaseResponse 解析失败，name={0}，error={1}。", netCmdName, e.Message);
                    return NetResponse<TResp>.Fail(NetErrorCode.PROTO_PARSE_FAILED, $"BaseResponse parse failed: {e.Message}");
                }

                if (parseResult.Code != NetErrorCode.SUCCESS)
                {
                    Log.Warning(LogTag.Network, "NetService.SendAsync：服务端返回业务错误，name={0}，code={1}，msg={2}。", netCmdName, parseResult.Code, parseResult.Message);
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
    }
}
