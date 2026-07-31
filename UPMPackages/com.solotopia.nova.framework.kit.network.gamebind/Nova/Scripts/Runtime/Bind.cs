/***************************************************************
 * (c) copyright 2026 - 2030, Solotopia
 * All Rights Reserved.
 * -------------------------------------------------------------
 * filename:  Bind.cs
 * author:    taoye
 * created:   2026/7/2
 * descrip:   账号绑定业务网络 Service，封装绑定/冲突查询/裁决协议
 ***************************************************************/

using System.Diagnostics;
using Cysharp.Threading.Tasks;
using NovaFramework.Runtime;

namespace NovaFramework.Kit.Network.GameBind.Runtime
{
    /// <summary>
    /// 账号绑定业务网络 Service。
    /// 封装绑定、冲突查询、裁决三段协议的发送逻辑，通过 NetService.SendAsync 完成 Protobuf 序列化、AES 加密、HTTP 请求及解析全流程。
    /// 只负责账号归属裁决（open_id 绑哪个 uid、冲突时谁为主），不处理存档数据覆盖——数据流向由业务层配合 GameSave 模块编排。
    /// 身份统一由请求 Header.Uid（即 NetService.UID，当前登录态）识别，业务侧无需传 uid；绑定前提是已登录。
    /// 通过 Nova.Network.Kit<Bind>() 获取实例，不继承任何基类，无参构造即可使用。
    /// </summary>
    public sealed partial class Bind
    {
        /// <summary>
        /// 设置当前 Service 实例的调试模式覆盖。
        /// 设置后仅影响本实例发出的请求；传 null 可恢复沿用全局开关。
        /// </summary>
        /// <param name="debugMode">是否启用调试模式。</param>
        public void SetDebugMode(bool debugMode)
        {
            m_DebugModeOverride = debugMode;
        }

        /// <summary>
        /// 为当前账号绑定三方 OpenID（业务入口，极简形态）。
        /// 身份由请求 Header.Uid（即 NetService.UID，当前登录态）识别，业务侧只需提供 provider 与 openid。
        /// 命中 <see cref="BindErrorCode.ErrBindConflict"/>(10402) 时响应带 existing_uid，需继续调 <see cref="QueryConflictAsync"/> 拉冲突详情、由玩家二选一后调 <see cref="ResolveAsync"/>。
        /// cmdName 取自 ConfigWindow 配置的 BindKitConfig.BindCmdName。
        /// </summary>
        /// <param name="provider">三方平台（与 <see cref="NovaFramework.Runtime.PbNetChannel"/> 枚举值对齐，直接透传）。选值：Facebook=1 / Google=2 / Apple=3 / Wechat=4（0=Unspecified 禁用）。</param>
        /// <param name="openid">要绑定的第三方平台返回的用户唯一标识。</param>
        /// <returns>包含绑定响应数据或错误信息的 NetResponse。</returns>
        public async UniTask<NetResponse<PbNetBindResp>> BindAsync(int provider, string openid)
        {
            Stopwatch stopwatch = Stopwatch.StartNew();
            try
            {
                BindKitConfig config = ResolveConfig();
                NetResponse<PbNetBindResp> response = await SendBindAsync(
                    Nova.Network.ResolveNetCmdRow(config.BindCmdName), provider, openid);
                if (response.IsSuccess)
                {
                    NetService.SetOpenID(openid);
                }
                TrackBind(response, provider, openid, stopwatch.ElapsedMilliseconds);
                return response;
            }
            catch
            {
                TrackBindException(provider, stopwatch.ElapsedMilliseconds);
                throw;
            }
        }

        /// <summary>
        /// 查询绑定冲突详情（业务入口，极简形态）。
        /// 仅在 <see cref="BindAsync"/> 返回 <see cref="BindErrorCode.ErrBindConflict"/>(10402) 后调用，拉取对方账号（existing）进度摘要供玩家二选一决策。
        /// 服务端自查 existing_uid，不接受客户端传；guest 侧摘要由客户端本地取。
        /// 身份由请求 Header.Uid（即 guest_uid，当前登录态）识别；cmdName 取自 ConfigWindow 配置的 BindKitConfig.BindConflictCmdName。
        /// </summary>
        /// <param name="openid">冲突的三方标识（与触发冲突的绑定 OpenID 一致）。</param>
        /// <returns>包含对方账号进度摘要或错误信息的 NetResponse。</returns>
        public async UniTask<NetResponse<PbNetBindConflictResp>> QueryConflictAsync(string openid)
        {
            Stopwatch stopwatch = Stopwatch.StartNew();
            try
            {
                BindKitConfig config = ResolveConfig();
                NetResponse<PbNetBindConflictResp> response = await SendQueryConflictAsync(
                    Nova.Network.ResolveNetCmdRow(config.BindConflictCmdName), openid);
                TrackQueryConflict(response, stopwatch.ElapsedMilliseconds);
                return response;
            }
            catch
            {
                TrackQueryConflictException(stopwatch.ElapsedMilliseconds);
                throw;
            }
        }

        /// <summary>
        /// 绑定冲突裁决（业务入口，极简形态）。
        /// 玩家二选一后调用，服务端做纯账号归属裁决，返回 final_uid（裁决后的主账号）与 abandoned_uid（被放弃但保留的账号）。
        /// 不处理存档数据覆盖——数据流向（本地覆盖云端 / 云端覆盖本地）由业务层配合 GameSave 模块编排。
        /// 裁决成功后以业务响应 FinalUid 与目标 OpenID 同步 NetService 身份。
        /// 身份由请求 Header.Uid（即 guest_uid，当前登录态）识别，经 device_id 顶号校验；服务端自查 existing_uid，不接受客户端传。
        /// cmdName 取自 ConfigWindow 配置的 BindKitConfig.BindResolveCmdName。
        /// </summary>
        /// <param name="openid">冲突的三方标识（与触发冲突的绑定 OpenID 一致）。</param>
        /// <param name="choice">二选一选项（直接透传字符串）。选值："guest"=保留当前账号 / "existing"=保留对方账号。</param>
        /// <param name="verifyCode">二次验证码（高危操作防盗号，按业务开启；无则传 null 或空）。</param>
        /// <returns>包含裁决响应数据或错误信息的 NetResponse。</returns>
        public async UniTask<NetResponse<PbNetBindResolveResp>> ResolveAsync(string openid, string choice, string verifyCode = null)
        {
            Stopwatch stopwatch = Stopwatch.StartNew();
            try
            {
                BindKitConfig config = ResolveConfig();
                NetResponse<PbNetBindResolveResp> response = await SendResolveAsync(
                    Nova.Network.ResolveNetCmdRow(config.BindResolveCmdName), openid, choice, verifyCode);
                if (response.IsSuccess && response.Data != null)
                {
                    string finalUID = response.Data.FinalUid ?? string.Empty;
                    NetService.SetUID(finalUID);
                    NetService.SetOpenID(openid);
                }
                TrackResolve(response, openid, choice, verifyCode, stopwatch.ElapsedMilliseconds);
                return response;
            }
            catch
            {
                TrackResolveException(choice, verifyCode, stopwatch.ElapsedMilliseconds);
                throw;
            }
        }
    }
}
