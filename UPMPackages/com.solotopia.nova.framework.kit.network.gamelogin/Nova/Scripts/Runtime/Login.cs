/***************************************************************
 * (c) copyright 2026 - 2030, Solotopia
 * All Rights Reserved.
 * -------------------------------------------------------------
 * filename:  Login.cs
 * author:    taoye
 * created:   2026/4/18
 * descrip:   登录业务网络 Service，自持 UID 状态，不继承基类
 ***************************************************************/

using System.Diagnostics;
using Cysharp.Threading.Tasks;
using NovaFramework.Runtime;

namespace NovaFramework.Kit.Network.GameLogin.Runtime
{
    /// <summary>
    /// 登录业务网络 Service。
    /// 封装登录与账号删除协议的发送逻辑，通过 NetService.SendAsync 完成 Protobuf 序列化、AES 加密、HTTP 请求及解析全流程。
    /// 登录成功后根据业务响应同步 UID、OpenID，后续请求 Header 自动携带当前身份。
    /// 删除账号成功后清空本地登录态（等同登出），防止继续以失效 UID 发请求。
    /// 渠道（Channel）由 NetBuilder.BuildHeader() 内 InferChannel() 自动填充，业务侧无需感知。
    /// 通过 Nova.Network.Kit<Login>() 获取实例，不继承任何基类，无参构造即可使用。
    /// </summary>
    public sealed partial class Login
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
        /// 登录（业务入口，极简形态）。
        /// open_id 仅用于"读"绑定关系找 uid 登入，不做绑定副作用——服务端查 open_id 已绑的 uid 并登入，未绑返回 <see cref="LoginErrorCode.ErrAccountNotFound"/>(10404)。
        /// 为当前账号绑定三方 open_id 请使用 gamebind 模块的 Bind 服务（Nova.Network.Kit 泛型获取 Bind 实例后调 BindAsync）。
        /// cmdName 取自 ConfigWindow 配置的 LoginKitConfig.LoginCmdName，渠道由 BuildHeader 自动填充。
        /// </summary>
        /// <param name="uid">显式指定请求 Header 中的 UID；传入非空值时优先使用此值填充，否则沿用 NetService.UID（登录态自动写回值）。</param>
        /// <param name="openid">第三方平台返回的用户唯一标识；用于读取 open_id 绑定关系找 uid 登入，未绑返回 10404。</param>
        /// <param name="forceNewAccount">是否强制注册新账号，默认 false。</param>
        /// <returns>包含登录响应数据或错误信息的 NetResponse。</returns>
        public async UniTask<NetResponse<PbNetLoginResp>> Async(string uid, string openid, bool forceNewAccount = false)
        {
            Stopwatch stopwatch = Stopwatch.StartNew();
            try
            {
                LoginKitConfig config = Nova.Config.GetKitConfig<LoginKitConfig>();
                if (config == null)
                {
                    throw new KitConfigMissingException(typeof(LoginKitConfig).FullName);
                }

                NetResponse<PbNetLoginResp> response = await SendAsync(
                    Nova.Network.ResolveNetCmdRow(config.LoginCmdName), uid, openid, forceNewAccount);
                TrackLogin(response, uid, openid, forceNewAccount, stopwatch.ElapsedMilliseconds);
                return response;
            }
            catch
            {
                TrackLoginException(uid, openid, forceNewAccount, stopwatch.ElapsedMilliseconds);
                throw;
            }
        }

        /// <summary>
        /// 删除当前登录账号（业务入口，极简形态）。
        /// 身份由请求 Header.Uid（即 NetService.UID，当前登录态 UID）识别，业务侧无需传参。
        /// 渠道由 BuildHeader 自动填充，业务侧无需传参。
        /// 删除成功后自动清空本地登录态（UID、OpenID 与 NetService 进程内身份），防止继续以失效 UID 发请求，语义等同登出。
        /// cmdName 取自 ConfigWindow 配置的 LoginKitConfig.DeleteCmdName。
        /// </summary>
        /// <returns>包含删除响应数据或错误信息的 NetResponse。</returns>
        public async UniTask<NetResponse<PbNetDeleteResp>> DeleteAsync()
        {
            Stopwatch stopwatch = Stopwatch.StartNew();
            try
            {
                LoginKitConfig config = Nova.Config.GetKitConfig<LoginKitConfig>();
                if (config == null)
                {
                    throw new KitConfigMissingException(typeof(LoginKitConfig).FullName);
                }

                NetResponse<PbNetDeleteResp> response = await SendDeleteAsync(
                    Nova.Network.ResolveNetCmdRow(config.DeleteCmdName));
                TrackDeleteAccount(response, stopwatch.ElapsedMilliseconds);
                if (response.IsSuccess)
                {
                    Clear();
                }
                return response;
            }
            catch
            {
                TrackDeleteAccountException(stopwatch.ElapsedMilliseconds);
                throw;
            }
        }

        /// <summary>
        /// 清空 NetService 的 UID、OpenID 进程内状态，后续请求 Header 不再携带身份字段。
        /// </summary>
        public void Clear()
        {
            NetService.SetUID(string.Empty);
            NetService.SetOpenID(string.Empty);
        }
    }
}
