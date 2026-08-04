/***************************************************************
 * (c) copyright 2026 - 2030, Solotopia
 * All Rights Reserved.
 * -------------------------------------------------------------
 * filename:  Login.Methods.cs
 * author:    taoye
 * created:   2026/4/18
 * descrip:   登录业务网络 Service — 私有方法
 ***************************************************************/

using Cysharp.Threading.Tasks;
using NovaFramework.Runtime;

namespace NovaFramework.Kit.Network.GameLogin.Runtime
{
    /// <summary>
    /// 登录业务网络 Service。
    /// 封装登录与账号删除协议的发送逻辑，通过 NetService.SendAsync 完成 Protobuf 序列化、AES 加密、HTTP 请求及解析全流程。
    /// 登录成功后根据业务响应同步 UID、OpenID，后续请求 Header 自动携带当前身份。
    /// 删除账号成功后清空本地登录态（等同登出），防止继续以失效 UID 发请求。
    /// 通过 Nova.Network.Kit<Login>() 获取实例，不继承任何基类，无参构造即可使用。
    /// </summary>
    public sealed partial class Login
    {
        /// <summary>
        /// 登录内部实现：按已解析的 cmdRow 发起请求。
        /// Header 只携带当前已确认身份；本次待登录 UID/OpenID 写入 Body，登录成功后以业务响应身份同步全局缓存。
        /// </summary>
        /// <param name="cmdRow">NetCmd 指令行数据，由 Async 解析 LoginKitConfig.LoginCmdName 得到。</param>
        /// <param name="uid">本次待登录 UID，只写入 Body。</param>
        /// <param name="openid">第三方登录提供方返回的用户唯一标识。</param>
        /// <param name="forceNewAccount">是否强制注册新账号。</param>
        /// <returns>包含登录响应数据或错误信息的 NetResponse。</returns>
        private async UniTask<NetResponse<PbNetLoginResp>> SendAsync(
            INetworkCmdRow cmdRow, string uid, string openid, bool forceNewAccount = false)
        {
            PbNetLoginReq body = BuildLoginRequest(NetBuilder.BuildHeader(), uid, openid, forceNewAccount);
            var resp = await NetService.SendAsync(cmdRow, body, PbNetLoginResp.Parser, m_DebugModeOverride);
            if (IsValidLoginResponse(resp))
            {
                string respUID = resp.Data.Uid ?? string.Empty;
                string respOpenID = resp.Data.Openid ?? string.Empty;
                NetService.SetIdentity(respUID, respOpenID);
                // 通知 SDK 登录成功
                Nova.SDK.Login(respUID);
            }
            else if (resp.IsSuccess)
            {
                resp = NetResponse<PbNetLoginResp>.Fail(
                    LoginErrorCode.ErrInvalidLoginResponse,
                    "Login response does not contain a normal confirmed identity",
                    resp.Data);
                LogLoginError(resp.ErrorCode, resp.ErrorMessage);
            }
            else
            {
                // 失败分支：按 LoginErrorCode 归类码值，打可读日志；不改变返回值，业务侧仍按 resp.ErrorCode 自行分支
                LogLoginError(resp.ErrorCode, resp.ErrorMessage);
            }

            return resp;
        }

        /// <summary>
        /// 构建登录请求：Header 保留当前确认身份，候选身份只进入 Body；强制新账号时候选身份清空。
        /// </summary>
        private static PbNetLoginReq BuildLoginRequest(
            PbNetReqHeader head, string uid, string openid, bool forceNewAccount)
        {
            return new PbNetLoginReq
            {
                Head = head,
                Uid = forceNewAccount ? string.Empty : uid ?? string.Empty,
                Openid = forceNewAccount ? string.Empty : openid ?? string.Empty,
                ForceNewAccount = forceNewAccount
            };
        }

        /// <summary>
        /// 仅接受传输成功、Data 非空、UID 非空且账号状态为 Normal 的登录响应。
        /// OpenID 允许为空，以支持游客身份。
        /// </summary>
        private static bool IsValidLoginResponse(NetResponse<PbNetLoginResp> response)
        {
            return response != null &&
                   response.IsSuccess &&
                   response.Data != null &&
                   !string.IsNullOrEmpty(response.Data.Uid) &&
                   response.Data.Status == PbNetAccountStatus.Normal;
        }

        /// <summary>
        /// 按 <see cref="LoginErrorCode"/> 归类登录业务错误码并打可读日志。
        /// 仅做码值归类与日志提示，不改变任何返回值；NetService 已对原始 code/msg 透传到 NetResponse.ErrorCode。
        /// </summary>
        /// <param name="errorCode">NetResponse.ErrorCode 原始码值。</param>
        /// <param name="errorMessage">NetResponse.ErrorMessage 原始描述。</param>
        private static void LogLoginError(int errorCode, string errorMessage)
        {
            switch (errorCode)
            {
                case LoginErrorCode.ErrKicked:
                    Log.Warning(LogTag.Network, "登录业务错误：device_id 非最新被顶号（ErrKicked={0}）。msg={1}", errorCode, errorMessage);
                    break;
                case LoginErrorCode.ErrAccountLocked:
                    Log.Warning(LogTag.Network, "登录业务错误：账号已锁定（ErrAccountLocked={0}）。msg={1}", errorCode, errorMessage);
                    break;
                case LoginErrorCode.ErrAccountBanned:
                    Log.Warning(LogTag.Network, "登录业务错误：账号已封禁（ErrAccountBanned={0}）。msg={1}", errorCode, errorMessage);
                    break;
                case LoginErrorCode.ErrAccountDeleted:
                    Log.Warning(LogTag.Network, "登录业务错误：账号已删除（ErrAccountDeleted={0}）。msg={1}", errorCode, errorMessage);
                    break;
                case LoginErrorCode.ErrUserNotFound:
                case LoginErrorCode.ErrAccountNotFound:
                    Log.Warning(LogTag.Network, "登录业务错误：账号不存在（code={0}）。msg={1}", errorCode, errorMessage);
                    break;
                case LoginErrorCode.ErrInvalidUID:
                    Log.Warning(LogTag.Network, "登录业务错误：UID 无效（ErrInvalidUID={0}）。msg={1}", errorCode, errorMessage);
                    break;
                case LoginErrorCode.ErrDeviceIdRequired:
                    Log.Warning(LogTag.Network, "登录业务错误：device_id 不能为空（ErrDeviceIdRequired={0}）。msg={1}", errorCode, errorMessage);
                    break;
                case LoginErrorCode.ErrOpenidUIDMismatch:
                    Log.Warning(LogTag.Network, "登录业务错误：三方账号与当前账号不匹配（ErrOpenidUIDMismatch={0}）。msg={1}", errorCode, errorMessage);
                    break;
                case LoginErrorCode.ErrInvalidLoginResponse:
                    Log.Warning(LogTag.Network, "登录响应身份无效（ErrInvalidLoginResponse={0}）。msg={1}", errorCode, errorMessage);
                    break;
                default:
                    // 非 LoginErrorCode 段（NetErrorCode 通用段或未知），不打登录专属日志，交由 NetService 已有日志覆盖
                    break;
            }
        }

        /// <summary>
        /// 删除账号内部实现：按已解析的 cmdRow 发起删除请求。
        /// Header.Uid 与 Body.Uid 使用调用方捕获的同一当前身份快照。
        /// 渠道由 BuildHeader 内 InferChannel 从 Nova.Config.Channel 自动填充，无需传入。
        /// 本方法只负责发送请求；埋点与按响应状态清理由 DeleteAsync 按顺序处理。
        /// </summary>
        /// <param name="cmdRow">NetCmd 指令行数据，由 DeleteAsync 解析得到。</param>
        /// <returns>包含删除响应数据或错误信息的 NetResponse。</returns>
        private async UniTask<NetResponse<PbNetDeleteResp>> SendDeleteAsync(INetworkCmdRow cmdRow, string targetUID)
        {
            PbNetDeleteReq body = BuildDeleteRequest(NetBuilder.BuildHeader(), targetUID);
            var resp = await NetService.SendAsync(cmdRow, body, PbNetDeleteResp.Parser, m_DebugModeOverride);
            return resp;
        }

        /// <summary>
        /// 由同一身份快照构造删除 Header 与 Body，确保二者 UID 完全一致。
        /// </summary>
        private static PbNetDeleteReq BuildDeleteRequest(PbNetReqHeader head, string targetUID)
        {
            string normalizedUID = targetUID ?? string.Empty;
            head.Uid = normalizedUID;
            return new PbNetDeleteReq
            {
                Head = head,
                Uid = normalizedUID
            };
        }

        /// <summary>
        /// 服务端明确返回目标账号 Locked/Banned/Deleted，且目标仍是当前身份时才清理缓存。
        /// 业务失败响应携带有效 Data 时同样适用。
        /// </summary>
        private static bool ShouldClearIdentity(
            NetResponse<PbNetDeleteResp> response, string targetUID, string currentUID)
        {
            if (response?.Data == null ||
                string.IsNullOrEmpty(targetUID) ||
                !string.Equals(targetUID, currentUID, System.StringComparison.Ordinal))
            {
                return false;
            }

            return response.Data.Status == PbNetAccountStatus.Locked ||
                   response.Data.Status == PbNetAccountStatus.Banned ||
                   response.Data.Status == PbNetAccountStatus.Deleted;
        }
    }
}
