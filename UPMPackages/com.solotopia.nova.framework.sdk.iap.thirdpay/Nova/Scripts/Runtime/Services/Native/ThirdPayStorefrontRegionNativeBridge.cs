/***************************************************************
 * (c) copyright 2026 - 2030, Solotopia
 * All Rights Reserved.
 * -------------------------------------------------------------
 * filename:  ThirdPayStorefrontRegionNativeBridge.cs
 * author:    yingzheng
 * created:   2026/8/28
 * descrip:   ThirdPay iOS StoreKit storefront 区域桥接
 ***************************************************************/

using System;

#if UNITY_IOS && !UNITY_EDITOR
using System.Runtime.InteropServices;
using AOT;
#endif

namespace NovaFramework.SDK.IAP.ThirdPay.Runtime
{
    internal static class ThirdPayStorefrontRegionNativeBridge
    {
#if UNITY_IOS && !UNITY_EDITOR
        /// <summary>
        /// iOS StoreKit storefront 原生回调。
        /// </summary>
        /// <param name="countryCode">App Store 国家或地区代码。</param>
        /// <param name="identifier">App Store storefront 标识。</param>
        [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
        private delegate void StorefrontRegionCallback(string countryCode, string identifier);

        /// <summary>
        /// 传递给 iOS 原生层的固定回调，避免委托被 GC。
        /// </summary>
        private static readonly StorefrontRegionCallback s_Callback = OnStorefrontRegion;

        /// <summary>
        /// 当前等待 iOS storefront 结果的托管回调。
        /// </summary>
        private static Action<string, string> s_ResultHandler;

        /// <summary>
        /// 调用 ThirdPay 包内 iOS 插件读取 StoreKit storefront。
        /// </summary>
        /// <param name="callback">原生层读取完成后的回调。</param>
        [DllImport("__Internal", CallingConvention = CallingConvention.Cdecl)]
        private static extern void NovaThirdPayGetStorefrontRegion(StorefrontRegionCallback callback);

        /// <summary>
        /// 请求 iOS 原生层读取 App Store storefront。
        /// </summary>
        /// <param name="resultHandler">读取完成后的托管回调。</param>
        internal static void Request(Action<string, string> resultHandler)
        {
            s_ResultHandler = resultHandler;
            try
            {
                NovaThirdPayGetStorefrontRegion(s_Callback);
            }
            catch
            {
                s_ResultHandler = null;
                throw;
            }
        }

        /// <summary>
        /// 接收 iOS 原生层返回的 storefront 信息。
        /// </summary>
        /// <param name="countryCode">App Store 国家或地区代码。</param>
        /// <param name="identifier">App Store storefront 标识。</param>
        [MonoPInvokeCallback(typeof(StorefrontRegionCallback))]
        private static void OnStorefrontRegion(string countryCode, string identifier)
        {
            Action<string, string> resultHandler = s_ResultHandler;
            s_ResultHandler = null;
            resultHandler?.Invoke(countryCode ?? string.Empty, identifier ?? string.Empty);
        }
#else
        /// <summary>
        /// 非 iOS 平台直接返回空 storefront 信息。
        /// </summary>
        /// <param name="resultHandler">读取完成后的托管回调。</param>
        internal static void Request(Action<string, string> resultHandler)
        {
            resultHandler?.Invoke(string.Empty, string.Empty);
        }
#endif
    }
}
