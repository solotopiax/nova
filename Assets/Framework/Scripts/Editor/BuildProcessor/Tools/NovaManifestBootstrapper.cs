/***************************************************************
 * (c) copyright 2026 - 2030, Solotopia
 * All Rights Reserved.
 * -------------------------------------------------------------
 * filename:  NovaManifestBootstrapper.cs
 * author:    yingzheng
 * created:   2026/4/24
 * descrip:   Android Manifest 动态注入工具，从 PlayerSettings 读取构建时参数写入模板
 ***************************************************************/

using System.Xml;
using NovaFramework.Runtime;
using UnityEditor;

namespace NovaFramework.Editor
{
    /// <summary>
    /// Android Manifest 动态注入工具。
    /// 从 PlayerSettings 读取构建时参数（包名、版本号、屏幕方向等），写入 CustomAndroidManifest 模板。
    /// </summary>
    public static class NovaManifestBootstrapper
    {
        /// <summary>
        /// Activity configChanges 常量，覆盖所有常见配置变化类型。
        /// </summary>
        private const string c_ConfigChanges = "fontScale|keyboard|keyboardHidden|locale|mnc|mcc|navigation|orientation|screenLayout|screenSize|smallestScreenSize|uiMode|touchscreen";

        /// <summary>
        /// 将 PlayerSettings 中的动态构建参数注入到指定的 CustomAndroidManifest 实例。
        /// 注入内容包括：包名、版本号、versionName、activityName、exported、configChanges、screenOrientation。
        /// 须在所有 SDK 处理器执行完毕后调用，以确保 activityName 已由处理器注册。
        /// </summary>
        /// <param name="manifest">目标 CustomAndroidManifest 实例，不应为 null。</param>
        /// <param name="activityName">主 Activity 完整类名，由 NovaBuildContext.ActivityName 提供。</param>
        public static void Inject(CustomAndroidManifest manifest, string activityName)
        {
            manifest.ManifestElement.SetAttribute("package", PlayerSettings.applicationIdentifier);
            manifest.ManifestElement.Attributes.Append(manifest.CreateAndroidAttribute("versionCode", PlayerSettings.Android.bundleVersionCode.ToString()));
            manifest.ManifestElement.Attributes.Append(manifest.CreateAndroidAttribute("versionName", PlayerSettings.bundleVersion));

            var activity = manifest.GetActivityWithLaunchIntent() as XmlElement;
            if (activity == null)
            {
                Log.Warning(LogTag.Editor, "[NovaManifestBootstrapper] 未找到含 MAIN/LAUNCHER intent-filter 的主 Activity 节点，跳过 Activity 属性注入。");
                return;
            }

            var nameAttr = activity.Attributes["android:name"];
            if (nameAttr != null)
                nameAttr.Value = activityName;
            activity.Attributes.Append(manifest.CreateAndroidAttribute("exported", "true"));
            activity.Attributes.Append(manifest.CreateAndroidAttribute("configChanges", c_ConfigChanges));
            activity.Attributes.Append(manifest.CreateAndroidAttribute("screenOrientation", ToAndroidOrientation(PlayerSettings.defaultInterfaceOrientation)));
        }

        /// <summary>
        /// 将 Unity UIOrientation 枚举值转换为 Android Manifest screenOrientation 属性字符串。
        /// </summary>
        /// <param name="o">Unity UIOrientation 枚举值。</param>
        /// <returns>对应的 Android screenOrientation 字符串。</returns>
        private static string ToAndroidOrientation(UIOrientation o)
        {
            switch (o)
            {
                case UIOrientation.Portrait: return "portrait";
                case UIOrientation.PortraitUpsideDown: return "reversePortrait";
                case UIOrientation.LandscapeLeft: return "landscape";
                case UIOrientation.LandscapeRight: return "reverseLandscape";
                default: return "fullSensor";
            }
        }
    }
}
