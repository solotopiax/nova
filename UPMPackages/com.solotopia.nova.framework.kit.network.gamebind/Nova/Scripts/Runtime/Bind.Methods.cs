/***************************************************************
 * (c) copyright 2026 - 2030, Solotopia
 * All Rights Reserved.
 * -------------------------------------------------------------
 * filename:  Bind.Methods.cs
 * author:    taoye
 * created:   2026/7/2
 * descrip:   账号绑定业务网络 Service — 私有方法
 ***************************************************************/

using Cysharp.Threading.Tasks;
using NovaFramework.Runtime;

namespace NovaFramework.Kit.Network.GameBind.Runtime
{
    /// <summary>
    /// 账号绑定业务网络 Service。
    /// 封装绑定、冲突查询、裁决三段协议的发送逻辑，通过 NetService.SendAsync 完成 Protobuf 序列化、AES 加密、HTTP 请求及解析全流程。
    /// 通过 Nova.Network.Kit<Bind>() 获取实例，不继承任何基类，无参构造即可使用。
    /// </summary>
    public sealed partial class Bind
    {
        /// <summary>
        /// 拉取绑定 Kit 配置：从 ConfigWindow「Kit 配置」取 BindKitConfig，未配置时抛 KitConfigMissingException（开发期 fail-fast）。
        /// </summary>
        /// <returns>已配置的 BindKitConfig。</returns>
        private static BindKitConfig ResolveConfig()
        {
            BindKitConfig config = Nova.Config.GetKitConfig<BindKitConfig>();
            if (config == null)
            {
                throw new KitConfigMissingException(typeof(BindKitConfig).FullName);
            }
            return config;
        }

        /// <summary>
        /// 绑定内部实现：按已解析的 cmdRow 发起绑定请求。
        /// Header 由 NetBuilder.BuildHeader() 自动填充（Header.Uid 即当前登录态 uid，为被绑定的账号；渠道由 InferChannel 自动填充）。
        /// </summary>
        /// <param name="cmdRow">NetCmd 指令行数据，由 BindAsync 解析 BindKitConfig.BindCmdName 得到。</param>
        /// <param name="provider">三方平台（与 PbNetChannel 枚举值对齐，直接透传）。</param>
        /// <param name="openId">要绑定的三方标识。</param>
        /// <returns>包含绑定响应数据或错误信息的 NetResponse。</returns>
        private async UniTask<NetResponse<PbNetBindResp>> SendBindAsync(
            INetworkCmdRow cmdRow, int provider, string openId)
        {
            var body = new PbNetBindReq
            {
                Head = NetBuilder.BuildHeader(),
                Provider = provider,
                OpenId = openId ?? string.Empty
            };
            var resp = await NetService.SendAsync(cmdRow, body, PbNetBindResp.Parser, m_DebugModeOverride);
            if (!resp.IsSuccess)
            {
                LogBindError(resp.ErrorCode, resp.ErrorMessage);
            }
            return resp;
        }

        /// <summary>
        /// 冲突查询内部实现：按已解析的 cmdRow 发起冲突详情请求。
        /// Header 由 NetBuilder.BuildHeader() 自动填充（Header.Uid 即 guest_uid，当前登录态）。
        /// </summary>
        /// <param name="cmdRow">NetCmd 指令行数据，由 QueryConflictAsync 解析 BindKitConfig.BindConflictCmdName 得到。</param>
        /// <param name="openId">冲突的三方标识。</param>
        /// <returns>包含对方账号进度摘要或错误信息的 NetResponse。</returns>
        private async UniTask<NetResponse<PbNetBindConflictResp>> SendQueryConflictAsync(
            INetworkCmdRow cmdRow, string openId)
        {
            var body = new PbNetBindConflictReq
            {
                Head = NetBuilder.BuildHeader(),
                OpenId = openId ?? string.Empty
            };
            var resp = await NetService.SendAsync(cmdRow, body, PbNetBindConflictResp.Parser, m_DebugModeOverride);
            if (!resp.IsSuccess)
            {
                LogBindError(resp.ErrorCode, resp.ErrorMessage);
            }
            return resp;
        }

        /// <summary>
        /// 裁决内部实现：按已解析的 cmdRow 发起二选一裁决请求。
        /// Header 由 NetBuilder.BuildHeader() 自动填充（Header.Uid 即 guest_uid，经 device_id 顶号校验）；服务端自查 existing_uid，不接受客户端传。
        /// 纯账号归属裁决，不改动本地登录态、不处理存档数据。
        /// </summary>
        /// <param name="cmdRow">NetCmd 指令行数据，由 ResolveAsync 解析 BindKitConfig.BindResolveCmdName 得到。</param>
        /// <param name="openId">冲突的三方标识。</param>
        /// <param name="choice">guest=保留当前账号 / existing=保留对方账号。</param>
        /// <param name="verifyCode">二次验证码（无则传空）。</param>
        /// <returns>包含裁决响应数据或错误信息的 NetResponse。</returns>
        private async UniTask<NetResponse<PbNetBindResolveResp>> SendResolveAsync(
            INetworkCmdRow cmdRow, string openId, string choice, string verifyCode)
        {
            var body = new PbNetBindResolveReq
            {
                Head = NetBuilder.BuildHeader(),
                OpenId = openId ?? string.Empty,
                Choice = choice ?? string.Empty,
                VerifyCode = verifyCode ?? string.Empty
            };
            var resp = await NetService.SendAsync(cmdRow, body, PbNetBindResolveResp.Parser, m_DebugModeOverride);
            if (!resp.IsSuccess)
            {
                LogBindError(resp.ErrorCode, resp.ErrorMessage);
            }
            return resp;
        }

        /// <summary>
        /// 按 <see cref="BindErrorCode"/> 归类账号绑定业务错误码并打可读日志。
        /// 仅做码值归类与日志提示，不改变任何返回值；NetService 已对原始 code/msg 透传到 NetResponse.ErrorCode。
        /// </summary>
        /// <param name="errorCode">NetResponse.ErrorCode 原始码值。</param>
        /// <param name="errorMessage">NetResponse.ErrorMessage 原始描述。</param>
        private static void LogBindError(int errorCode, string errorMessage)
        {
            switch (errorCode)
            {
                case BindErrorCode.ErrBindConflict:
                    Log.Warning(LogTag.Network, "账号绑定错误：绑定冲突需二选一（ErrBindConflict={0}），请调 QueryConflictAsync 拉冲突详情后由玩家二选一。msg={1}", errorCode, errorMessage);
                    break;
                case BindErrorCode.ErrOpenidAlreadyBound:
                    Log.Warning(LogTag.Network, "账号绑定错误：该三方号已被他人占用（ErrOpenidAlreadyBound={0}）。msg={1}", errorCode, errorMessage);
                    break;
                case BindErrorCode.ErrThirdPartyAuthFailed:
                    Log.Warning(LogTag.Network, "账号绑定错误：open_id 缺失或格式非法（ErrThirdPartyAuthFailed={0}）。msg={1}", errorCode, errorMessage);
                    break;
                case BindErrorCode.ErrKicked:
                    Log.Warning(LogTag.Network, "账号绑定错误：device_id 非最新被顶号（ErrKicked={0}）。msg={1}", errorCode, errorMessage);
                    break;
                default:
                    // 非 BindErrorCode 段（NetErrorCode 通用段或未知），不打绑定专属日志，交由 NetService 已有日志覆盖
                    break;
            }
        }
    }
}
