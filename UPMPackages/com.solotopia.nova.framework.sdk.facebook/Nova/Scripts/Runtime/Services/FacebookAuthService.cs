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
            Log.Debug(LogTag.Facebook, $"初始化开始：已初始化={FB.IsInitialized}。");
            ct.ThrowIfCancellationRequested();
            if (FB.IsInitialized)
            {
                FB.ActivateApp();
                Log.Debug(LogTag.Facebook, "初始化复用现有 SDK，ActivateApp 已调用。");
                return;
            }

            var tcs = new UniTaskCompletionSource<bool>();
            FB.Init(
                () =>
                {
                    if (FB.IsInitialized)
                    {
                        FB.ActivateApp();
                        Log.Debug(LogTag.Facebook, "初始化回调成功，ActivateApp 已调用。");
                        tcs.TrySetResult(true);
                    }
                    else
                    {
                        Log.Warning(LogTag.Facebook, "初始化回调失败：FB.IsInitialized=false。");
                        tcs.TrySetException(new System.InvalidOperationException("Facebook SDK 初始化失败。"));
                    }
                },
                isGameShown => { UnityEngine.Time.timeScale = isGameShown ? 1 : 0; });

            try
            {
                await tcs.Task.AttachExternalCancellation(ct);
            }
            catch (System.OperationCanceledException)
            {
                Log.Debug(LogTag.Facebook, "初始化本地等待已取消；原厂操作不会因此撤销。");
                throw;
            }
        }

        /// <summary>
        /// 执行默认权限登录。
        /// </summary>
        /// <param name="provider">登录提供方。</param>
        /// <param name="ct">取消令牌。</param>
        /// <returns>登录结果。</returns>
        public UniTask<AuthResult> LoginAsync(string provider, CancellationToken ct)
        {
            return LoginWithPermissionsAsync(provider, DefaultReadPermissions, "登录", ct);
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
                Log.Debug(LogTag.Facebook, "好友权限已授予，复用当前登录凭据。");
                return UniTask.FromResult(new AuthResult
                {
                    Success = true,
                    Provider = provider,
                    UserId = token.UserId,
                    Token = token.TokenString
                });
            }

            return LoginWithPermissionsAsync(provider, FriendsReadPermissions, "好友授权", ct);
        }

        /// <summary>
        /// 记录授权调用、回调和同步异常，不改变原有登录结果。
        /// </summary>
        /// <param name="provider">登录提供方。</param>
        /// <param name="permissions">实际请求的读取权限。</param>
        /// <param name="operation">日志中的操作名称。</param>
        /// <param name="ct">仅取消本地等待的令牌。</param>
        /// <returns>保持原有成功与失败语义的登录结果。</returns>
        private static UniTask<AuthResult> LoginWithPermissionsAsync(string provider, List<string> permissions, string operation, CancellationToken ct)
        {
            Log.Debug(LogTag.Facebook, $"{operation}开始：permissions={string.Join(",", permissions)}。");
            try
            {
                ct.ThrowIfCancellationRequested();
                var tcs = new UniTaskCompletionSource<AuthResult>();
                FB.LogInWithReadPermissions(permissions, result =>
                {
                    AuthResult auth = BuildAuthResult(provider, result);
                    var token = result?.AccessToken ?? AccessToken.CurrentAccessToken;
                    Log.Debug(LogTag.Facebook, $"{operation}回调：成功={auth.Success}，用户取消={result?.Cancelled ?? false}，含用户ID={!string.IsNullOrEmpty(auth.UserId)}，含Token={!string.IsNullOrEmpty(auth.Token)}，public_profile={token?.Permissions?.Contains("public_profile") ?? false}，user_friends={token?.Permissions?.Contains("user_friends") ?? false}。");
                    if (!auth.Success)
                    {
                        Log.Warning(LogTag.Facebook, $"{operation}未成功：error={(auth.ErrorMessage?.Length > 240 ? auth.ErrorMessage.Substring(0, 237) + "..." : auth.ErrorMessage)}。");
                    }
                    tcs.TrySetResult(auth);
                });
                return tcs.Task.AttachExternalCancellation(ct);
            }
            catch (System.OperationCanceledException)
            {
                Log.Debug(LogTag.Facebook, $"{operation}调用已取消。");
                throw;
            }
            catch (System.Exception ex)
            {
                Log.Warning(LogTag.Facebook, $"{operation}异常：error={(ex.Message.Length > 240 ? ex.Message.Substring(0, 237) + "..." : ex.Message)}。");
                throw;
            }
        }

        /// <summary>
        /// 登出当前账号。
        /// </summary>
        public void Logout()
        {
            Log.Debug(LogTag.Facebook, $"登出调用：原厂登录态={FB.IsLoggedIn}。");
            if (FB.IsLoggedIn)
            {
                FB.LogOut();
            }
            Log.Debug(LogTag.Facebook, "原厂登出调用完成。");
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
