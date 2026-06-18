/***************************************************************
 * (c) copyright 2026 - 2030, Solotopia
 * All Rights Reserved.
 * -------------------------------------------------------------
 * filename:  FirebaseReportNetService.cs
 * author:    yingzheng
 * created:   2026/5/28
 * descrip:   Firebase 标识上报业务网络 Service，封装 PbNetReportFirebaseReq 协议的发送逻辑
 ***************************************************************/

#if !UNITY_WEBGL
using Cysharp.Threading.Tasks;
using NovaFramework.Runtime;

namespace NovaFramework.SDK.FirebasePlugin.Runtime
{
    /// <summary>
    /// Firebase 标识上报业务网络 Service。
    /// 封装"上报 Firebase 推送令牌 / Analytics 实例 ID"协议的发送逻辑，
    /// 通过 NetService.SendAsync 完成 Protobuf 序列化、AES 加密、HTTP 请求及解析全流程。
    /// 服务端按"只更新非空字段"语义处理，客户端传空串即跳过该字段。
    /// 由 FirebasePlugin 内部持有实例。
    /// </summary>
    public sealed class FirebaseReportNetService
    {
        /// <summary>
        /// 当前 Service 实例的调试模式覆盖值。
        /// 为 null 时沿用 NetService.IsDebugMode 全局开关。
        /// </summary>
        private bool? m_DebugModeOverride;

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
        /// 上报 Firebase 标识（业务入口）。
        /// Header 由 NetBuilder.BuildHeader() 自动填充；调用方只需提供需要更新的字段，传空串表示不更新。
        /// </summary>
        /// <param name="cmdName">协议名，由 Plugin 从 FirebasePluginConfig.ReportCmdName 取出后传入。</param>
        /// <param name="firebasePushToken">Firebase 云推送令牌；空串表示不更新。</param>
        /// <param name="firebaseAnalyticsInstanceId">Firebase 客户端实例化 ID；空串表示不更新。</param>
        /// <returns>包含上报响应数据或错误信息的 NetResponse。</returns>
        public async UniTask<NetResponse<PbNetReportFirebaseResp>> Async(string cmdName, string firebasePushToken, string firebaseAnalyticsInstanceId)
        {
            var body = new PbNetReportFirebaseReq
            {
                Head = NetBuilder.BuildHeader(),
                FirebasePushToken = firebasePushToken ?? string.Empty,
                FirebaseAnalyticsInstanceId = firebaseAnalyticsInstanceId ?? string.Empty,
            };
            INetworkCmdRow cmdRow = Nova.Network?.ResolveNetCmdRow(cmdName);
            return await NetService.SendAsync(cmdRow, body, PbNetReportFirebaseResp.Parser, m_DebugModeOverride);
        }
    }
}
#endif
