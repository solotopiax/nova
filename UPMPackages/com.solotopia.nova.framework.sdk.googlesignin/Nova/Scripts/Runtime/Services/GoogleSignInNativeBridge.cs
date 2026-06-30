/***************************************************************
 * (c) copyright 2026 - 2030, Solotopia
 * All Rights Reserved.
 * -------------------------------------------------------------
 * filename:  GoogleSignInNativeBridge.cs
 * author:    yingzheng
 * created:   2026/6/25
 * descrip:   Google Sign-In platform bridge
 ***************************************************************/

using System;
using System.Threading;
using Cysharp.Threading.Tasks;

#if UNITY_ANDROID && !UNITY_EDITOR
using UnityEngine;
#endif

namespace NovaFramework.SDK.GoogleSignIn
{
    internal static class GoogleSignInNativeBridge
    {
#if UNITY_ANDROID && !UNITY_EDITOR
        private const string c_AndroidBridgeClassName = "com.solotopia.nova.googlesignin.GoogleSignInBridge";
#endif

        public static UniTask<GoogleSignInUserData> SignInAsync(GoogleSignInPluginConfig config, CancellationToken ct)
        {
#if UNITY_ANDROID && !UNITY_EDITOR
            return SignInAndroidAsync(config, ct);
#else
            throw new PlatformNotSupportedException("Google Sign-In is only supported on Android.");
#endif
        }

        public static UniTask<GoogleSignInUserData> RestoreAsync(GoogleSignInPluginConfig config, CancellationToken ct)
        {
#if UNITY_ANDROID && !UNITY_EDITOR
            return RestoreAndroidAsync(config, ct);
#else
            throw new PlatformNotSupportedException("Google Sign-In is only supported on Android.");
#endif
        }

        public static UniTask SignOutAsync(GoogleSignInPluginConfig config, CancellationToken ct)
        {
#if UNITY_ANDROID && !UNITY_EDITOR
            return SignOutAndroidAsync(ct);
#else
            return UniTask.CompletedTask;
#endif
        }

#if UNITY_ANDROID && !UNITY_EDITOR
        private static async UniTask<GoogleSignInUserData> SignInAndroidAsync(GoogleSignInPluginConfig config, CancellationToken ct)
        {
            AndroidJavaObject activity = GetAndroidActivity();
            var source = new UniTaskCompletionSource<GoogleSignInUserData>();
            var callback = new AndroidSignInCallback(source);
            using var bridge = new AndroidJavaClass(c_AndroidBridgeClassName);
            bridge.CallStatic(
                "signIn",
                activity,
                config.ClientId ?? string.Empty,
                config.RequestEmail,
                config.FilterByAuthorizedAccounts,
                config.AutoSelectEnabled,
                callback);
            return await AwaitResultOnMainThread(source.Task.AttachExternalCancellation(ct));
        }

        private static async UniTask<GoogleSignInUserData> RestoreAndroidAsync(GoogleSignInPluginConfig config, CancellationToken ct)
        {
            AndroidJavaObject activity = GetAndroidActivity();
            var source = new UniTaskCompletionSource<GoogleSignInUserData>();
            var callback = new AndroidSignInCallback(source);
            using var bridge = new AndroidJavaClass(c_AndroidBridgeClassName);
            bridge.CallStatic(
                "restore",
                activity,
                config.ClientId ?? string.Empty,
                config.RequestEmail,
                config.AutoSelectEnabled,
                callback);
            return await AwaitResultOnMainThread(source.Task.AttachExternalCancellation(ct));
        }

        private static UniTask SignOutAndroidAsync(CancellationToken ct)
        {
            AndroidJavaObject activity = GetAndroidActivity();
            using var bridge = new AndroidJavaClass(c_AndroidBridgeClassName);
            bridge.CallStatic("signOut", activity);
            return UniTask.CompletedTask;
        }

        private static AndroidJavaObject GetAndroidActivity()
        {
            using var unityPlayer = new AndroidJavaClass("com.unity3d.player.UnityPlayer");
            return unityPlayer.GetStatic<AndroidJavaObject>("currentActivity");
        }

        private static async UniTask<GoogleSignInUserData> AwaitResultOnMainThread(UniTask<GoogleSignInUserData> task)
        {
            try
            {
                GoogleSignInUserData result = await task;
                await UniTask.SwitchToMainThread();
                return result;
            }
            catch
            {
                await UniTask.SwitchToMainThread();
                throw;
            }
        }

        private sealed class AndroidSignInCallback : AndroidJavaProxy
        {
            private readonly UniTaskCompletionSource<GoogleSignInUserData> m_Source;

            public AndroidSignInCallback(UniTaskCompletionSource<GoogleSignInUserData> source)
                : base("com.solotopia.nova.googlesignin.GoogleSignInBridgeCallback")
            {
                m_Source = source;
            }

            public void onSuccess(string userId, string idToken, string email, string displayName, string avatarUrl)
            {
                m_Source.TrySetResult(new GoogleSignInUserData(userId, idToken, email, displayName, avatarUrl));
            }

            public void onError(string error)
            {
                m_Source.TrySetException(new InvalidOperationException(string.IsNullOrEmpty(error) ? "Google Android Sign-In failed." : error));
            }
        }
#endif
    }
}
