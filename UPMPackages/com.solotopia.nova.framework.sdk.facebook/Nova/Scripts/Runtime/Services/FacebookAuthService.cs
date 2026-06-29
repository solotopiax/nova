using System.Collections.Generic;
using System.Linq;
using System.Threading;
using Cysharp.Threading.Tasks;
using Facebook.Unity;
using NovaFramework.Runtime;

namespace NovaFramework.SDK.Facebook
{
    /// <summary>
    /// Facebook 登录与 SDK 初始化服务。
    /// </summary>
    public sealed class FacebookAuthService
    {
        /// <summary>
        /// 默认读取权限。
        /// </summary>
        private static readonly List<string> DefaultReadPermissions = new List<string> { "public_profile" };

        /// <summary>
        /// 好友读取权限。
        /// </summary>
        private static readonly List<string> FriendsReadPermissions = new List<string> { "public_profile", "user_friends" };

        /// <summary>
        /// 获取当前用户 ID。
        /// </summary>
        public string CurrentUserId => AccessToken.CurrentAccessToken?.UserId;

        /// <summary>
        /// 获取当前访问令牌。
        /// </summary>
        public string CurrentAccessToken => AccessToken.CurrentAccessToken?.TokenString;

        /// <summary>
        /// 初始化 Facebook SDK。
        /// </summary>
        /// <param name="ct">取消令牌。</param>
        public async UniTask InitializeAsync(CancellationToken ct)
        {
            ct.ThrowIfCancellationRequested();
            if (FB.IsInitialized)
            {
                FB.ActivateApp();
                return;
            }

            var tcs = new UniTaskCompletionSource<bool>();
            FB.Init(
                () =>
                {
                    if (FB.IsInitialized)
                    {
                        FB.ActivateApp();
                        tcs.TrySetResult(true);
                    }
                    else
                    {
                        tcs.TrySetException(new System.InvalidOperationException("Facebook SDK 初始化失败。"));
                    }
                },
                isGameShown => { UnityEngine.Time.timeScale = isGameShown ? 1 : 0; });

            await tcs.Task.AttachExternalCancellation(ct);
        }

        /// <summary>
        /// 执行默认权限登录。
        /// </summary>
        /// <param name="provider">登录提供方。</param>
        /// <param name="ct">取消令牌。</param>
        /// <returns>登录结果。</returns>
        public UniTask<AuthResult> LoginAsync(string provider, CancellationToken ct)
        {
            ct.ThrowIfCancellationRequested();
            var tcs = new UniTaskCompletionSource<AuthResult>();
            FB.LogInWithReadPermissions(DefaultReadPermissions, result => tcs.TrySetResult(BuildAuthResult(provider, result)));
            return tcs.Task.AttachExternalCancellation(ct);
        }

        /// <summary>
        /// 确保已授予好友权限。
        /// </summary>
        /// <param name="provider">登录提供方。</param>
        /// <param name="ct">取消令牌。</param>
        /// <returns>授权结果。</returns>
        public UniTask<AuthResult> EnsureFriendsPermissionAsync(string provider, CancellationToken ct)
        {
            ct.ThrowIfCancellationRequested();

            var token = AccessToken.CurrentAccessToken;
            if (token != null && token.Permissions != null && token.Permissions.Contains("user_friends"))
            {
                return UniTask.FromResult(new AuthResult
                {
                    Success = true,
                    Provider = provider,
                    UserId = token.UserId,
                    Token = token.TokenString
                });
            }

            var tcs = new UniTaskCompletionSource<AuthResult>();
            FB.LogInWithReadPermissions(FriendsReadPermissions, result => tcs.TrySetResult(BuildAuthResult(provider, result)));
            return tcs.Task.AttachExternalCancellation(ct);
        }

        /// <summary>
        /// 登出当前账号。
        /// </summary>
        public void Logout()
        {
            if (FB.IsLoggedIn)
            {
                FB.LogOut();
            }
        }

        /// <summary>
        /// 构造登录结果。
        /// </summary>
        /// <param name="provider">登录提供方。</param>
        /// <param name="result">Facebook 登录结果。</param>
        /// <returns>框架登录结果。</returns>
        private static AuthResult BuildAuthResult(string provider, ILoginResult result)
        {
            if (result == null)
            {
                return Failed(provider, "Facebook 登录结果为空。");
            }

            if (!string.IsNullOrEmpty(result.Error))
            {
                return Failed(provider, result.Error);
            }

            if (result.Cancelled)
            {
                return Failed(provider, "UserCancelled");
            }

            var token = result.AccessToken ?? AccessToken.CurrentAccessToken;
            if (token == null || string.IsNullOrEmpty(token.UserId) || string.IsNullOrEmpty(token.TokenString))
            {
                return Failed(provider, "Facebook AccessToken 为空。");
            }

            return new AuthResult
            {
                Success = true,
                Provider = string.IsNullOrEmpty(provider) ? "Facebook" : provider,
                UserId = token.UserId,
                Token = token.TokenString
            };
        }

        /// <summary>
        /// 构造失败结果。
        /// </summary>
        /// <param name="provider">登录提供方。</param>
        /// <param name="message">错误信息。</param>
        /// <returns>失败结果。</returns>
        private static AuthResult Failed(string provider, string message)
        {
            return new AuthResult
            {
                Success = false,
                Provider = string.IsNullOrEmpty(provider) ? "Facebook" : provider,
                ErrorMessage = message
            };
        }
    }
}
