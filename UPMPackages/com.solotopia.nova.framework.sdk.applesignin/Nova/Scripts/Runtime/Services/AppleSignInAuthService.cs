/***************************************************************
 * (c) copyright 2026 - 2030, Solotopia
 * All Rights Reserved.
 * -------------------------------------------------------------
 * filename:  AppleSignInAuthService.cs
 * author:    yingzheng
 * created:   2026/6/25
 * descrip:   Apple 登录认证服务
 ***************************************************************/

using System;
using System.Threading;
using AppleAuth;
using AppleAuth.Enums;
using AppleAuth.Interfaces;
using AppleAuth.Native;
using Cysharp.Threading.Tasks;

namespace NovaFramework.SDK.AppleSignIn
{
    /// <summary>
    /// Apple 登录认证服务。
    /// </summary>
    internal sealed class AppleSignInAuthService
    {
        /// <summary>
        /// 运行时配置。
        /// </summary>
        private readonly AppleSignInPluginConfig m_Config;

        /// <summary>
        /// AppleAuth 管理器。
        /// </summary>
        private IAppleAuthManager m_AppleAuthManager;

        /// <summary>
        /// 创建认证服务。
        /// </summary>
        /// <param name="config">运行时配置。</param>
        public AppleSignInAuthService(AppleSignInPluginConfig config)
        {
            m_Config = config ?? new AppleSignInPluginConfig();
        }

        /// <summary>
        /// 发起交互式登录。
        /// </summary>
        /// <param name="ct">取消令牌。</param>
        /// <returns>Apple 用户数据。</returns>
        public async UniTask<AppleSignInUserData> LoginAsync(CancellationToken ct)
        {
            IAppleAuthManager manager = GetOrCreateManager();
            LoginOptions options = BuildLoginOptions();
            var args = new AppleAuthLoginArgs(options, null, null);

            ICredential credential = await WaitForCredentialAsync(
                (successCallback, errorCallback) => manager.LoginWithAppleId(args, successCallback, errorCallback),
                manager,
                ct);

            return BuildUserData(credential);
        }

        /// <summary>
        /// 获取或创建 AppleAuth 管理器。
        /// </summary>
        /// <returns>AppleAuth 管理器。</returns>
        private IAppleAuthManager GetOrCreateManager()
        {
            if (!AppleAuthManager.IsCurrentPlatformSupported)
            {
                throw new PlatformNotSupportedException("当前平台不支持 Apple 登录。");
            }

            return m_AppleAuthManager ??= new AppleAuthManager(new PayloadDeserializer());
        }

        /// <summary>
        /// 构建登录权限选项。
        /// </summary>
        /// <returns>登录权限选项。</returns>
        private LoginOptions BuildLoginOptions()
        {
            LoginOptions options = LoginOptions.None;
            if (m_Config.RequestFullName)
            {
                options |= LoginOptions.IncludeFullName;
            }

            return options;
        }

        /// <summary>
        /// 等待 AppleAuth 回调完成。
        /// </summary>
        /// <param name="startRequest">请求启动回调。</param>
        /// <param name="manager">AppleAuth 管理器。</param>
        /// <param name="ct">取消令牌。</param>
        /// <returns>Apple 凭据。</returns>
        private static async UniTask<ICredential> WaitForCredentialAsync(
            Action<Action<ICredential>, Action<IAppleError>> startRequest,
            IAppleAuthManager manager,
            CancellationToken ct)
        {
            ICredential credential = null;
            Exception exception = null;
            bool completed = false;

            try
            {
                startRequest(
                    value =>
                    {
                        credential = value;
                        completed = true;
                    },
                    error =>
                    {
                        exception = new AppleSignInException(FormatError(error));
                        completed = true;
                    });
            }
            catch (Exception ex)
            {
                exception = ex;
                completed = true;
            }

            while (!completed)
            {
                ct.ThrowIfCancellationRequested();
                manager.Update();
                await UniTask.Yield(PlayerLoopTiming.Update, ct);
            }

            if (exception != null)
            {
                throw exception;
            }

            return credential;
        }

        /// <summary>
        /// 构建 Apple 用户数据。
        /// </summary>
        /// <param name="credential">Apple 凭据。</param>
        /// <returns>Apple 用户数据。</returns>
        private static AppleSignInUserData BuildUserData(ICredential credential)
        {
            if (credential is not IAppleIDCredential appleIdCredential)
            {
                throw new InvalidOperationException("Apple 登录未返回 AppleIDCredential。");
            }

            return new AppleSignInUserData(
                appleIdCredential.User,
                BuildFullName(appleIdCredential.FullName));
        }

        /// <summary>
        /// 拼接姓名。
        /// </summary>
        /// <param name="personName">姓名组件。</param>
        /// <returns>姓名文本。</returns>
        private static string BuildFullName(IPersonName personName)
        {
            if (personName == null)
            {
                return null;
            }

            string[] parts =
            {
                personName.NamePrefix,
                personName.GivenName,
                personName.MiddleName,
                personName.FamilyName,
                personName.NameSuffix
            };
            return string.Join(" ", Array.FindAll(parts, value => !string.IsNullOrEmpty(value)));
        }

        /// <summary>
        /// 格式化 AppleAuth 错误。
        /// </summary>
        /// <param name="error">AppleAuth 错误。</param>
        /// <returns>错误文本。</returns>
        private static string FormatError(IAppleError error)
        {
            if (error == null)
            {
                return "Apple 登录失败。";
            }

            return string.IsNullOrEmpty(error.LocalizedDescription)
                ? $"Apple 登录失败：{error.Domain}({error.Code})。"
                : $"Apple 登录失败：{error.LocalizedDescription}";
        }

        /// <summary>
        /// Apple 登录异常。
        /// </summary>
        private sealed class AppleSignInException : Exception
        {
            /// <summary>
            /// 创建 Apple 登录异常。
            /// </summary>
            /// <param name="message">错误信息。</param>
            public AppleSignInException(string message) : base(message) { }
        }
    }
}
