/***************************************************************
 * (c) copyright 2026 - 2030, Solotopia
 * All Rights Reserved.
 * -------------------------------------------------------------
 * filename:  FacebookAuthPlugin.cs
 * author:    taoye
 * created:   2026/4/20
 * descrip:   Facebook 登录 SDK 插件，实现 I3rdAuthService 接口
 ***************************************************************/

using System.Threading;
using Cysharp.Threading.Tasks;
using NovaFramework.Runtime;

namespace NovaFramework.SDK.Facebook
{
    /// <summary>
    /// Facebook 登录 SDK 插件，继承 SDKPluginBase 并实现 I3rdAuthService。
    /// 由 SDKComponent 自动收集，负责对接 Facebook Login SDK 的登录流程。
    /// </summary>
    public sealed partial class FacebookAuthPlugin : SDKPluginBase, I3rdAuthService
    {
        /// <summary>
        /// 获取 SDK 唯一名称标识。
        /// </summary>
        public override string SDKName => "Facebook";

        /// <summary>
        /// 获取插件初始化优先级，值越小越先初始化。
        /// </summary>
        public override int Priority => 30;

        /// <summary>
        /// 获取当前用户是否已登录。
        /// </summary>
        public bool IsLoggedIn => false;

        /// <summary>
        /// 获取当前已登录用户的唯一标识，未登录时为 null。
        /// </summary>
        public string CurrentUserId => null;

        /// <summary>
        /// 异步初始化 Facebook Login SDK。
        /// </summary>
        /// <returns>初始化完成的异步任务。</returns>
        public override UniTask OnInitializeAsync()
        {
            // TODO: Facebook Login SDK 初始化
            return UniTask.CompletedTask;
        }

        /// <summary>
        /// 异步发起 Facebook 登录流程。
        /// </summary>
        /// <param name="provider">第三方平台标识，如 "Facebook"。</param>
        /// <param name="cancellationToken">用于取消异步操作的令牌。</param>
        /// <returns>包含登录结果及用户凭据的 AuthResult。</returns>
        public UniTask<AuthResult> LoginAsync(string provider, CancellationToken cancellationToken = default)
        {
            return UniTask.FromResult(new AuthResult { Success = false, ErrorMessage = "未实现。" });
        }

        /// <summary>
        /// 异步登出当前 Facebook 账号。
        /// </summary>
        /// <param name="cancellationToken">用于取消异步操作的令牌。</param>
        /// <returns>登出操作完成的异步任务。</returns>
        public UniTask LogoutAsync(CancellationToken cancellationToken = default)
        {
            return UniTask.CompletedTask;
        }
    }
}
