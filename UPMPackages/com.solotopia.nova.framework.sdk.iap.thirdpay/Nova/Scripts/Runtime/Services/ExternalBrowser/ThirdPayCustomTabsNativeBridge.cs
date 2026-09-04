/***************************************************************
 * (c) copyright 2026 - 2030, Solotopia
 * All Rights Reserved.
 * -------------------------------------------------------------
 * filename:  ThirdPayCustomTabsNativeBridge.cs
 * author:    yingzheng
 * created:   2026/9/1
 * descrip:   ThirdPay Android Auth Tab / Custom Tabs 原生桥接
 ***************************************************************/

using System;

#if UNITY_ANDROID && !UNITY_EDITOR
using UnityEngine;
#endif

namespace NovaFramework.SDK.IAP.ThirdPay.Runtime
{
    /// <summary>
    /// Android 外部支付页实际打开方式。
    /// </summary>
    internal enum ThirdPayExternalBrowserLaunchMode
    {
        /// <summary>
        /// 打开失败，未提交到 Auth Tab 或 Custom Tabs。
        /// </summary>
        Failed = 0,

        /// <summary>
        /// 已通过 Android Auth Tab 打开。
        /// </summary>
        AuthTab = 1,

        /// <summary>
        /// 已通过 Android Custom Tabs 打开。
        /// </summary>
        CustomTabs = 2,
    }

    /// <summary>
    /// 调用 ThirdPay 包内 Android Java 插件打开 Auth Tab，不支持时回退 Custom Tabs。
    /// </summary>
    internal static class ThirdPayCustomTabsNativeBridge
    {
#if UNITY_ANDROID && !UNITY_EDITOR
        /// <summary>
        /// Android Auth Tab / Custom Tabs bridge 的完整类名。
        /// </summary>
        private const string c_AndroidBridgeClassName = "com.solotopia.nova.thirdpay.NovaThirdPayAuthTabActivity";
#endif

        /// <summary>
        /// 在 Android 上优先通过 Auth Tab 打开 URL；非 Android 平台返回 Failed。
        /// </summary>
        /// <param name="paymentUrl">ThirdPay 支付 URL。</param>
        /// <returns>Android 原生层返回的实际打开方式。</returns>
        public static ThirdPayExternalBrowserLaunchMode OpenUrlPreferAuthTab(string paymentUrl)
        {
#if UNITY_ANDROID && !UNITY_EDITOR
            if (string.IsNullOrEmpty(paymentUrl))
            {
                return ThirdPayExternalBrowserLaunchMode.Failed;
            }

            try
            {
                using AndroidJavaObject activity = GetAndroidActivity();
                using var bridge = new AndroidJavaClass(c_AndroidBridgeClassName);
                int result = bridge.CallStatic<int>("openUrlPreferAuthTab", activity, paymentUrl);
                return MapLaunchMode(result);
            }
            catch (Exception)
            {
                return ThirdPayExternalBrowserLaunchMode.Failed;
            }
#else
            return ThirdPayExternalBrowserLaunchMode.Failed;
#endif
        }

#if UNITY_ANDROID && !UNITY_EDITOR
        /// <summary>
        /// 获取 Unity 当前 Activity。
        /// </summary>
        /// <returns>UnityPlayer.currentActivity。</returns>
        private static AndroidJavaObject GetAndroidActivity()
        {
            using var unityPlayer = new AndroidJavaClass("com.unity3d.player.UnityPlayer");
            return unityPlayer.GetStatic<AndroidJavaObject>("currentActivity");
        }

        /// <summary>
        /// 将 Java bridge 返回的整数值映射为托管枚举。
        /// </summary>
        /// <param name="result">Java bridge 返回值，0 失败、1 Auth Tab、2 Custom Tabs。</param>
        /// <returns>托管层外部支付页打开方式。</returns>
        private static ThirdPayExternalBrowserLaunchMode MapLaunchMode(int result)
        {
            if (Enum.IsDefined(typeof(ThirdPayExternalBrowserLaunchMode), result))
            {
                return (ThirdPayExternalBrowserLaunchMode)result;
            }

            return ThirdPayExternalBrowserLaunchMode.Failed;
        }
#endif
    }
}
