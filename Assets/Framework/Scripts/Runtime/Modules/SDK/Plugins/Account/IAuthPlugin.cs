/***************************************************************
 * (c) copyright 2026 - 2030, Solotopia
 * All Rights Reserved.
 * -------------------------------------------------------------
 * filename:  IAuthPlugin.cs
 * author:    taoye
 * created:   2026/4/28
 * descrip:   第三方登录 SDK 插件接口
 ***************************************************************/

using System.Threading;
using Cysharp.Threading.Tasks;

namespace NovaFramework.Runtime
{
    /// <summary>
    /// 第三方账号登录接口，抽象 Google、Apple、Facebook 等平台的 OAuth 登录能力。
    /// 所有方法主线程调用；实现内部可切后台线程等待平台回调，完成前须切回主线程。
    /// 取消令牌触发时抛 OperationCanceledException；登录失败时 AuthResult.Success=false。
    /// </summary>
    public interface IAuthPlugin : ISDKPlugin
    {
        /// <summary>
        /// 当前用户是否已登录。
        /// 主线程只读；登录成功后为 true，注销后为 false。
        /// </summary>
        bool IsLoggedIn { get; }

        /// <summary>
        /// 当前已登录用户的平台用户 ID。
        /// 未登录时返回 null 或空字符串；登录成功后由实现层填充。
        /// </summary>
        string CurrentUserId { get; }

        /// <summary>
        /// 异步发起第三方登录流程。
        /// provider 标识登录平台（如 "google"、"apple"、"facebook"），由实现层解析。
        /// 若用户取消登录，AuthResult.Success=false 且 ErrorMessage 包含"UserCancelled"语义标记。
        /// </summary>
        /// <param name="provider">登录提供商标识，大小写不敏感；实现层负责映射到平台 SDK。</param>
        /// <param name="ct">取消令牌，默认不取消。</param>
        /// <returns>登录结果，含成功标志、用户 ID、平台令牌等字段。</returns>
        UniTask<AuthResult> LoginAsync(string provider, CancellationToken ct = default);

        /// <summary>
        /// 异步注销当前登录用户，清除本地令牌与会话。
        /// 未登录时调用为幂等操作，不抛异常。
        /// </summary>
        /// <param name="ct">取消令牌，默认不取消。</param>
        /// <returns>注销完成的异步任务。</returns>
        UniTask LogoutAsync(CancellationToken ct = default);
    }
}
