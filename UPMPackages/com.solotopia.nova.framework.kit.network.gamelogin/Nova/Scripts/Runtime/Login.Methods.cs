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
