/***************************************************************
 * (c) copyright 2026 - 2030, Solotopia
 * All Rights Reserved.
 * -------------------------------------------------------------
 * filename:  ThirdPayGoogleBillingConfigNativeBridge.cs
 * author:    yingzheng
 * created:   2026/8/25
 * descrip:   Google Play Billing 商店地区代码原生桥接
 ***************************************************************/

using System.Threading;
using Cysharp.Threading.Tasks;

#if UNITY_ANDROID && !UNITY_EDITOR
using UnityEngine;
#endif

namespace NovaFramework.SDK.IAP.ThirdPay.Runtime
{
    /// <summary>
    /// 通过 Android BillingClient 读取 Google Play Billing 商店地区代码。
    /// </summary>
    internal static class ThirdPayGoogleBillingConfigNativeBridge
    {
#if UNITY_ANDROID && !UNITY_EDITOR
        private const string c_AndroidBridgeClassName =
            "com.solotopia.nova.thirdpay.NovaThirdPayBillingBridge";
#endif

        /// <summary>
        /// 异步读取当前 Google Play Billing 商店地区代码；非 Android 平台返回空字符串。
        /// </summary>
        /// <param name="ct">取消令牌。</param>
        /// <returns>Google Play Billing 返回的地区代码；读取失败时返回空字符串。</returns>
        public static UniTask<string> GetBillingCountryCodeAsync(CancellationToken ct)
        {
#if UNITY_ANDROID && !UNITY_EDITOR
            return GetAndroidAsync(ct);
#else
            ct.ThrowIfCancellationRequested();
            return UniTask.FromResult(string.Empty);
#endif
        }

#if UNITY_ANDROID && !UNITY_EDITOR
        /// <summary>
        /// 调用 Android bridge 并把 Java 回调转换为 UniTask。
        /// </summary>
        /// <param name="ct">取消令牌。</param>
        /// <returns>Android bridge 返回的商店地区代码。</returns>
        private static async UniTask<string> GetAndroidAsync(CancellationToken ct)
        {
            ct.ThrowIfCancellationRequested();
            AndroidJavaObject activity = GetAndroidActivity();
            var source = new UniTaskCompletionSource<string>();
            var callback = new AndroidBillingConfigCallback(source);
            using var bridge = new AndroidJavaClass(c_AndroidBridgeClassName);
            bridge.CallStatic("getBillingCountryCode", activity, callback);

            try
            {
                string countryCode = await source.Task.AttachExternalCancellation(ct);
                await UniTask.SwitchToMainThread();
                return countryCode ?? string.Empty;
            }
            catch
            {
                await UniTask.SwitchToMainThread();
                throw;
            }
        }

        /// <summary>
        /// 获取 Unity 当前 Activity。
        /// </summary>
        /// <returns>当前 Unity Activity。</returns>
        private static AndroidJavaObject GetAndroidActivity()
        {
            using var unityPlayer = new AndroidJavaClass("com.unity3d.player.UnityPlayer");
            return unityPlayer.GetStatic<AndroidJavaObject>("currentActivity");
        }

        /// <summary>
        /// 接收 Java BillingConfig 回调的代理对象。
        /// </summary>
        private sealed class AndroidBillingConfigCallback : AndroidJavaProxy
        {
            private readonly UniTaskCompletionSource<string> m_Source;

            /// <summary>
            /// 创建地区代码回调代理。
            /// </summary>
            /// <param name="source">异步结果源。</param>
            public AndroidBillingConfigCallback(UniTaskCompletionSource<string> source)
                : base("com.solotopia.nova.thirdpay.NovaThirdPayBillingBridge$Callback")
            {
                m_Source = source;
            }

            /// <summary>
            /// 接收 Android bridge 返回的地区代码。
            /// </summary>
            /// <param name="countryCode">Google Play Billing 商店地区代码。</param>
            public void onCountryCodeReceived(string countryCode)
            {
                m_Source.TrySetResult(countryCode ?? string.Empty);
            }
        }
#endif
    }
}
