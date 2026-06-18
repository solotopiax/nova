/***************************************************************
 * (c) copyright 2026 - 2030, Solotopia
 * All Rights Reserved.
 * -------------------------------------------------------------
 * filename:  FacebookAdSetting.cs
 * author:    yingzheng
 * created:   2026/5/15
 * descrip:   Facebook 广告 GDPR 数据处理选项初始化
 ***************************************************************/

using UnityEngine;

namespace NovaFramework.SDK.MaxAdPlugin.Runtime
{
   internal sealed class FacebookAdSetting
    {

        public static void Initialize()
        
        {
#if (UNITY_ANDROID || UNITY_IOS) && !UNITY_EDITOR
#if UNITY_IOS
            if (MaxSdkUtils.CompareVersions(UnityEngine.iOS.Device.systemVersion, "14.5") != MaxSdkUtils.VersionComparisonResult.Lesser)
            {
                FacebookAdSetting.SetAdvertiserTrackingEnabled(true);
            }
#endif
            FacebookAdSetting.SetDataProcessingOptions(new string[] { "LDU" }, 0, 0);
#endif
        }

#if UNITY_IOS
        [System.Runtime.InteropServices.DllImport("__Internal")]
        private static extern void FBAdSettingsBridgeSetDetailedDataProcessingOptions(string[] dataProcessingOptions, int length, int country, int state);

        [System.Runtime.InteropServices.DllImport("__Internal")]
        private static extern void FBAdSettingsBridgeSetAdvertiserTrackingEnabled(bool advertiserTrackingEnabled);
#endif

        public static void SetDataProcessingOptions(string[] dataProcessingOptions, int country, int state)
        {
#if UNITY_IOS
            FBAdSettingsBridgeSetDetailedDataProcessingOptions(dataProcessingOptions, dataProcessingOptions.Length, country, state);
#endif
            
#if UNITY_ANDROID
            AndroidJavaClass adSettings = new AndroidJavaClass("com.facebook.ads.AdSettings");
            adSettings.CallStatic("setDataProcessingOptions", (object)dataProcessingOptions, country, state);
#endif
        }

        public static void SetAdvertiserTrackingEnabled(bool advertiserTrackingEnabled)
        {
#if UNITY_IOS
            FBAdSettingsBridgeSetAdvertiserTrackingEnabled(advertiserTrackingEnabled);
#endif
        }
    }
}
