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
    /// 封装绑定状态查询、绑定、冲突查询、裁决协议的发送逻辑，通过 NetService.SendAsync 完成 Protobuf 序列化、AES 加密、HTTP 请求及解析全流程。
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
        /// Header 由 NetBuilder.BuildHeader() 自动填充（Header.Uid 即当前登录态 UID，为被绑定的账号；渠道由 InferChannel 自动填充）。
        /// </summary>
        /// <param name="cmdRow">NetCmd 指令行数据，由 BindAsync 解析 BindKitConfig.BindCmdName 得到。</param>
        /// <param name="provider">第三方登录提供方，写入协议时转换为约定的 int32 数值。</param>
        /// <param name="openid">要绑定的三方标识。</param>
        /// <returns>包含绑定响应数据或错误信息的 NetResponse。</returns>
        private async UniTask<NetResponse<PbNetBindResp>> SendBindAsync(
            INetworkCmdRow cmdRow, ThirdLoginProvider provider, string openid)
        {
            var body = new PbNetBindReq
            {
                Head = NetBuilder.BuildHeader(),
                Provider = (int)provider,
                Openid = openid ?? string.Empty
            };
            var resp = await NetService.SendAsync(cmdRow, body, PbNetBindResp.Parser, m_DebugModeOverride);
            if (!resp.IsSuccess)
            {
                LogBindError(resp.ErrorCode, resp.ErrorMessage);
            }
            return resp;
        }

        /// <summary>
        /// 构造绑定状态查询请求，Header 与目标 OpenID 均按调用方提供的值写入 Body。
        /// </summary>
        /// <param name="header">当前网络请求公共头。</param>
        /// <param name="openid">要查询的第三方账号唯一标识。</param>
        /// <returns>可直接发送的绑定状态查询请求。</returns>
        private static PbNetBindingQueryReq BuildBindingQueryRequest(PbNetReqHeader header, string openid)
        {
            return new PbNetBindingQueryReq
            {
                Head = header,
                Openid = openid ?? string.Empty
            };
        }

        /// <summary>
        /// 绑定状态查询内部实现：按已解析的 cmdRow 查询指定 OpenID，不修改当前登录身份。
        /// </summary>
        /// <param name="cmdRow">NetCmd 指令行数据，由 QueryBindingAsync 解析 BindingQueryCmdName 得到。</param>
        /// <param name="openid">要查询的第三方账号唯一标识。</param>
        /// <returns>包含是否绑定与对应 UID 的网络响应。</returns>
        private async UniTask<NetResponse<PbNetBindingQueryResp>> SendQueryBindingAsync(
            INetworkCmdRow cmdRow, string openid)
        {
            PbNetBindingQueryReq body = BuildBindingQueryRequest(NetBuilder.BuildHeader(), openid);
            NetResponse<PbNetBindingQueryResp> resp = await NetService.SendAsync(
                cmdRow, body, PbNetBindingQueryResp.Parser, m_DebugModeOverride);
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
        /// <param name="openid">冲突的三方标识。</param>
        /// <returns>包含对方账号进度摘要或错误信息的 NetResponse。</returns>
        private async UniTask<NetResponse<PbNetBindConflictResp>> SendQueryConflictAsync(
            INetworkCmdRow cmdRow, string openid)
        {
            var body = new PbNetBindConflictReq
            {
                Head = NetBuilder.BuildHeader(),
                Openid = openid ?? string.Empty
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
        /// 纯账号归属裁决，不处理存档数据；成功后由外层根据 FinalUid 与目标 OpenID 同步身份。
        /// </summary>
        /// <param name="cmdRow">NetCmd 指令行数据，由 ResolveAsync 解析 BindKitConfig.BindResolveCmdName 得到。</param>
        /// <param name="openid">冲突的三方标识。</param>
        /// <param name="choice">guest=保留当前账号 / existing=保留对方账号。</param>
        /// <param name="verifyCode">二次验证码（无则传空）。</param>
        /// <returns>包含裁决响应数据或错误信息的 NetResponse。</returns>
        private async UniTask<NetResponse<PbNetBindResolveResp>> SendResolveAsync(
            INetworkCmdRow cmdRow, string openid, string choice, string verifyCode)
        {
            var body = new PbNetBindResolveReq
            {
                Head = NetBuilder.BuildHeader(),
                Openid = openid ?? string.Empty,
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
                case BindErrorCode.ErrAccountNotFound:
                    Log.Warning(LogTag.Network, "账号绑定错误：对应绑定不存在（ErrAccountNotFound={0}），请刷新绑定或冲突状态。msg={1}", errorCode, errorMessage);
                    break;
                case BindErrorCode.ErrOpenidUIDMismatch:
                    Log.Warning(LogTag.Network, "账号绑定错误：三方账号与当前账号不匹配（ErrOpenidUIDMismatch={0}）。msg={1}", errorCode, errorMessage);
                    break;
                case BindErrorCode.ErrUIDAlreadyBoundOtherOpenID:
                    Log.Warning(LogTag.Network, "账号绑定错误：当前 UID 已绑定其他 OpenID（ErrUIDAlreadyBoundOtherOpenID={0}）。msg={1}", errorCode, errorMessage);
                    break;
                default:
                    // 非 BindErrorCode 段（NetErrorCode 通用段或未知），不打绑定专属日志，交由 NetService 已有日志覆盖
                    break;
            }
        }
    }
}
