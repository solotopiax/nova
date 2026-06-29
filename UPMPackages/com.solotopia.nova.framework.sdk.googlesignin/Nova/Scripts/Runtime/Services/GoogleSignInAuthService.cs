/***************************************************************
 * (c) copyright 2026 - 2030, Solotopia
 * All Rights Reserved.
 * -------------------------------------------------------------
 * filename:  GoogleSignInAuthService.cs
 * author:    Codex
 * created:   2026/6/25
 * descrip:   Google Sign-In auth service
 ***************************************************************/

using System;
using System.Threading;
using Cysharp.Threading.Tasks;

namespace NovaFramework.SDK.GoogleSignIn
{
    internal sealed class GoogleSignInAuthService
    {
        private readonly GoogleSignInPluginConfig m_Config;

        public GoogleSignInAuthService(GoogleSignInPluginConfig config)
        {
            m_Config = config ?? new GoogleSignInPluginConfig();
        }

        public async UniTask<GoogleSignInUserData> LoginAsync(CancellationToken ct)
        {
            ValidateConfigForCurrentPlatform();
            return await GoogleSignInNativeBridge.SignInAsync(m_Config, ct);
        }

        public async UniTask<GoogleSignInUserData> RestoreAsync(CancellationToken ct)
        {
            ValidateConfigForCurrentPlatform();
            return await GoogleSignInNativeBridge.RestoreAsync(m_Config, ct);
        }

        public UniTask SignOutAsync(CancellationToken ct)
        {
            return GoogleSignInNativeBridge.SignOutAsync(m_Config, ct);
        }

        private void ValidateConfigForCurrentPlatform()
        {
#if UNITY_ANDROID && !UNITY_EDITOR
            if (string.IsNullOrEmpty(m_Config.ClientId))
            {
                throw new InvalidOperationException("Google Web Client ID is empty.");
            }
#else
            throw new PlatformNotSupportedException("Google Sign-In is only supported on Android.");
#endif
        }
    }
}
