/***************************************************************
 * (c) copyright 2026 - 2030, Solotopia
 * All Rights Reserved.
 * -------------------------------------------------------------
 * filename:  DefaultSafeAreaProvider.cs
 * author:    taoye
 * created:   2026/04/24
 * descrip:   基于 UnityEngine.Screen.safeArea 的默认安全区域提供者
 ***************************************************************/

using UnityEngine;

namespace NovaFramework.Runtime
{
    /// <summary>
    /// 基于 UnityEngine.Screen.safeArea 的默认安全区域提供者。
    /// 在非 WebGL 平台或未注入自定义提供者时使用。
    /// </summary>
    public sealed class DefaultSafeAreaProvider : ISafeAreaProvider
    {
        /// <summary>
        /// 获取当前平台的安全区域原始数据（使用 Screen.safeArea）。
        /// </summary>
        /// <returns>安全区域原始数据。</returns>
        public SafeAreaData GetSafeArea()
        {
            Rect area = Screen.safeArea;
            return new SafeAreaData
            {
                Left = area.xMin,
                Right = area.xMax,
                Top = area.yMin,
                Bottom = area.yMax,
                PixelRatio = 1.0f,
                ScreenHeight = Screen.height
            };
        }
    }

    // ─────────────────────────────────────────────────────────────
    // 三方平台安全区域提供者参考实现（待落实时取消注释并拆为独立类）
    // ─────────────────────────────────────────────────────────────
    //
    // /// <summary>
    // /// 抖音 DYSDK WebGL 平台安全区域提供者。
    // /// 依赖：NOVA_DYSDK 宏 + StarkSDKSpace.StarkSDK 包。
    // /// </summary>
    // public sealed class DYSDKSafeAreaProvider : ISafeAreaProvider
    // {
    //     public SafeAreaData GetSafeArea()
    //     {
    //         var info = StarkSDKSpace.StarkSDK.API.GetSystemInfo();
    //         var sdkSafeArea = info.safeArea;
    //         return new SafeAreaData
    //         {
    //             Left = (float)sdkSafeArea.left,
    //             Right = (float)sdkSafeArea.right,
    //             Top = (float)sdkSafeArea.top,
    //             Bottom = (float)sdkSafeArea.bottom,
    //             PixelRatio = (float)info.pixelRatio,
    //             ScreenHeight = (float)info.screenHeight
    //         };
    //     }
    // }
    //
    // /// <summary>
    // /// 微信 WXSDK WebGL 平台安全区域提供者。
    // /// 依赖：NOVA_WXSDK 宏 + WeChatWASM.WX 包。
    // /// </summary>
    // public sealed class WXSDKSafeAreaProvider : ISafeAreaProvider
    // {
    //     public SafeAreaData GetSafeArea()
    //     {
    //         var info = WeChatWASM.WX.GetSystemInfoSync();
    //         var sdkSafeArea = info.safeArea;
    //         return new SafeAreaData
    //         {
    //             Left = (float)sdkSafeArea.left,
    //             Right = (float)sdkSafeArea.right,
    //             Top = (float)sdkSafeArea.top,
    //             Bottom = (float)sdkSafeArea.bottom,
    //             PixelRatio = (float)info.pixelRatio,
    //             ScreenHeight = (float)info.screenHeight
    //         };
    //     }
    // }
}
