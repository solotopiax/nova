/***************************************************************
 * (c) copyright 2026 - 2030, Solotopia
 * All Rights Reserved.
 * -------------------------------------------------------------
 * filename:  Login.cs
 * author:    taoye
 * created:   2026/4/18
 * descrip:   登录业务网络 Service，自持 UID 状态，不继承基类
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
        /// cmdName 取自 ConfigWindow 配置的 LoginKitConfig.LoginCmdName，渠道由 BuildHeader 自动填充，业务侧只需提供 openId。
        /// </summary>
        /// <param name="uid">显式指定请求 Header 中的 Uid；传入非空值时优先使用此值填充，否则沿用 NetService.Uid（登录态自动写回值）。</param>
        /// <param name="openId">第三方平台返回的用户唯一标识。</param>
        /// <param name="forceNewAccount">是否强制注册新账号，默认 false。</param>
        /// <returns>包含登录响应数据或错误信息的 NetResponse。</returns>
        public UniTask<NetResponse<PbNetLoginResp>> Async(string uid, string openId, bool forceNewAccount = false)
        {
            LoginKitConfig config = Nova.Config.GetKitConfig<LoginKitConfig>();
            if (config == null)
            {
                throw new KitConfigMissingException(typeof(LoginKitConfig).FullName);
            }
            return SendAsync(Nova.Network.ResolveNetCmdRow(config.LoginCmdName), uid, openId, forceNewAccount);
        }

        /// <summary>
        /// 删除当前登录账号（业务入口，极简形态）。
        /// 身份由请求 Header.Uid（即 NetService.Uid，当前登录态 UID）识别，业务侧无需传参。
        /// 渠道由 BuildHeader 自动填充，业务侧无需传参。
        /// 删除成功后自动清空本地登录态（UID 与 NetService.Uid），防止继续以失效 Uid 发请求，语义等同登出。
        /// cmdName 取自 ConfigWindow 配置的 LoginKitConfig.DeleteCmdName。
        /// </summary>
        /// <returns>包含删除响应数据或错误信息的 NetResponse。</returns>
        public UniTask<NetResponse<PbNetDeleteResp>> DeleteAsync()
        {
            LoginKitConfig config = Nova.Config.GetKitConfig<LoginKitConfig>();
            if (config == null)
            {
                throw new KitConfigMissingException(typeof(LoginKitConfig).FullName);
            }
            return SendDeleteAsync(Nova.Network.ResolveNetCmdRow(config.DeleteCmdName));
        }

        /// <summary>
        /// 清空本实例 UID 与 NetService 静态 Uid 字段，后续请求 Header 不再携带 Uid。
        /// </summary>
        public void Clear()
        {
            UID = string.Empty;
            NetService.SetUid(string.Empty);
        }
    }
}
