/***************************************************************
 * (c) copyright 2026 - 2030, Solotopia
 * All Rights Reserved.
 * -------------------------------------------------------------
 * filename:  IAuthPlugin.cs
 * author:    taoye
 * created:   2026/4/28
 * descrip:   第三方登录接口
 ***************************************************************/

using System.Threading;
using Cysharp.Threading.Tasks;

namespace NovaFramework.Runtime
{
    /// <summary>
    /// 第三方登录接口。
    /// </summary>
    public interface IAuthPlugin : ISDKPlugin
    {
        /// <summary>
        /// 登录状态。
        /// </summary>
        bool IsLoggedIn { get; }

        /// <summary>
        /// 发起登录。
        /// </summary>
        /// <param name="provider">登录提供方。</param>
        /// <param name="ct">取消令牌。</param>
        /// <returns>登录结果。</returns>
        UniTask<AuthResult> LoginAsync(string provider, CancellationToken ct = default);

        /// <summary>
        /// 登出账号。
        /// </summary>
        /// <param name="ct">取消令牌。</param>
        /// <returns>登出任务。</returns>
        UniTask LogoutAsync(CancellationToken ct = default);
    }
}
