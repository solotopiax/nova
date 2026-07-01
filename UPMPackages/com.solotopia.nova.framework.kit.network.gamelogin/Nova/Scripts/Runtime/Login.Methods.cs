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
    /// 登录成功后自动写回 UID 到本地属性与 NetService 静态字段，后续请求 Header 自动带 Uid。
    /// 删除账号成功后清空本地登录态（等同登出），防止继续以失效 Uid 发请求。
    /// 通过 Nova.Network.Kit<Login>() 获取实例，不继承任何基类，无参构造即可使用。
    /// </summary>
    public sealed partial class Login
    {
        /// <summary>
        /// 登录内部实现：按已解析的 cmdRow 发起请求。
        /// Header 由 NetBuilder.BuildHeader() 自动填充（含渠道，由 BuildHeader 内 InferChannel 从 Nova.Config.Channel 取得）；登录成功后 UID 自动写回本实例与 NetService 静态字段。
        /// </summary>
        /// <param name="cmdRow">NetCmd 指令行数据，由 Async 解析 LoginKitConfig.LoginCmdName 得到。</param>
        /// <param name="uid">显式指定请求 Header 中的 Uid；非空时优先覆盖 BuildHeader 填入的 NetService.Uid；为空则沿用。</param>
        /// <param name="openId">第三方平台返回的用户唯一标识。</param>
        /// <param name="forceNewAccount">是否强制注册新账号。</param>
        /// <returns>包含登录响应数据或错误信息的 NetResponse。</returns>
        private async UniTask<NetResponse<PbNetLoginResp>> SendAsync(
            INetworkCmdRow cmdRow, string uid, string openId, bool forceNewAccount = false)
        {
            var body = new PbNetLoginReq
            {
                Head = NetBuilder.BuildHeader(),
                OpenId = openId ?? string.Empty,
                ForceNewAccount = forceNewAccount
            };
            // 传入 uid 非空时优先覆盖；否则沿用 BuildHeader 已填入的 NetService.Uid
            if (!string.IsNullOrEmpty(uid))
            {
                body.Head.Uid = uid;
            }
            if(forceNewAccount)
            {
                body.Head.Uid = string.Empty;
            }
            var resp = await NetService.SendAsync(cmdRow, body, PbNetLoginResp.Parser, m_DebugModeOverride);
            if (resp.IsSuccess && resp.Data != null)
            {
                string respUid = resp.Data.Uid ?? string.Empty;
                UID = respUid;
                NetService.SetUid(respUid);
                // 通知 SDK 登录成功
                Nova.SDK.Login(respUid);
            }
            else
            {
                // 失败分支：按 LoginErrorCode 归类码值，打可读日志；不改变返回值，业务侧仍按 resp.ErrorCode 自行分支
                LogLoginError(resp.ErrorCode, resp.ErrorMessage);
            }

            return resp;
        }

        /// <summary>
        /// 按 <see cref="LoginErrorCode"/> 归类登录业务错误码并打可读日志。
        /// 仅做码值归类与日志提示，不改变任何返回值；NetService 已对原始 code/msg 透传到 NetResponse.ErrorCode。
        /// 命中 ErrBindConflict(10402) 时提示需走 BindResolveAsync 二选一。
        /// </summary>
        /// <param name="errorCode">NetResponse.ErrorCode 原始码值。</param>
        /// <param name="errorMessage">NetResponse.ErrorMessage 原始描述。</param>
        private static void LogLoginError(int errorCode, string errorMessage)
        {
            switch (errorCode)
            {
                case LoginErrorCode.ErrBindConflict:
                    Log.Warning(LogTag.Network, "登录业务错误：绑定冲突需二选一（ErrBindConflict={0}），请读取 guest_summary/existing_summary 后调 BindResolveAsync。msg={1}", errorCode, errorMessage);
                    break;
                case LoginErrorCode.ErrOpenidAlreadyBound:
                    Log.Warning(LogTag.Network, "登录业务错误：该三方号已被他人占用（ErrOpenidAlreadyBound={0}）。msg={1}", errorCode, errorMessage);
                    break;
                case LoginErrorCode.ErrThirdPartyAuthFailed:
                    Log.Warning(LogTag.Network, "登录业务错误：open_id 缺失或格式非法（ErrThirdPartyAuthFailed={0}）。msg={1}", errorCode, errorMessage);
                    break;
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
                default:
                    // 非 LoginErrorCode 段（NetErrorCode 通用段或未知），不打登录专属日志，交由 NetService 已有日志覆盖
                    break;
            }
        }

        /// <summary>
        /// 绑定冲突二选一内部实现：按已解析的 cmdRow 发起二选一请求。
        /// Header 由 NetBuilder.BuildHeader() 自动填充（uid 即 guest_uid，经 device_id 顶号校验）；渠道由 BuildHeader 内 InferChannel 自动填充。
        /// 服务端自查 existing_uid，不接受客户端传；客户端只需提供 provider / openId / choice / verifyCode。
        /// </summary>
        /// <param name="cmdRow">NetCmd 指令行数据，由 BindResolveAsync 解析 LoginKitConfig.BindResolveCmdName 得到。</param>
        /// <param name="provider">三方平台（与 PbNetChannel 枚举值对齐，直接透传）。</param>
        /// <param name="openId">冲突的三方标识。</param>
        /// <param name="choice">guest=保留当前进度 / existing=保留云端进度。</param>
        /// <param name="verifyCode">二次验证码（高危操作防盗号，按业务开启；无则传空）。</param>
        /// <returns>包含二选一响应数据或错误信息的 NetResponse。</returns>
        private async UniTask<NetResponse<PbNetBindResolveResp>> SendBindResolveAsync(
            INetworkCmdRow cmdRow, int provider, string openId, string choice, string verifyCode)
        {
            var body = new PbNetBindResolveReq
            {
                Head = NetBuilder.BuildHeader(),
                Provider = provider,
                OpenId = openId ?? string.Empty,
                Choice = choice ?? string.Empty,
                VerifyCode = verifyCode ?? string.Empty
            };
            var resp = await NetService.SendAsync(cmdRow, body, PbNetBindResolveResp.Parser, m_DebugModeOverride);
            if (resp.IsSuccess && resp.Data != null)
            {
                // 二选一成功后，最终选中的主账号 uid 写回本地登录态，后续请求 Header 自动带新 Uid
                string respUid = resp.Data.Uid ?? string.Empty;
                UID = respUid;
                NetService.SetUid(respUid);
                Nova.SDK.Login(respUid);
            }
            else
            {
                LogLoginError(resp.ErrorCode, resp.ErrorMessage);
            }
            return resp;
        }

        /// <summary>
        /// 删除账号内部实现：按已解析的 cmdRow 发起删除请求。
        /// 身份由 NetBuilder.BuildHeader() 填充的 Header.Uid（即 NetService.Uid）识别，无需传 uid。
        /// 渠道由 BuildHeader 内 InferChannel 从 Nova.Config.Channel 自动填充，无需传入。
        /// 删除成功后清空本实例 UID 与 NetService.Uid 静态字段，后续请求 Header 不再携带 Uid。
        /// </summary>
        /// <param name="cmdRow">NetCmd 指令行数据，由 DeleteAsync 解析得到。</param>
        /// <returns>包含删除响应数据或错误信息的 NetResponse。</returns>
        private async UniTask<NetResponse<PbNetDeleteResp>> SendDeleteAsync(INetworkCmdRow cmdRow)
        {
            var body = new PbNetDeleteReq
            {
                Head = NetBuilder.BuildHeader()
            };
            var resp = await NetService.SendAsync(cmdRow, body, PbNetDeleteResp.Parser, m_DebugModeOverride);
            if (resp.IsSuccess)
            {
                // 删除成功 = 账号已不存在，清空登录态，防止继续以失效 Uid 发请求
                UID = string.Empty;
                NetService.SetUid(string.Empty);
            }
            return resp;
        }

    }
}
