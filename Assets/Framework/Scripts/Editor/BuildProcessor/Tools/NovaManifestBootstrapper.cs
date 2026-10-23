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
        public const string c_ConfigChanges = "fontScale|keyboard|keyboardHidden|locale|mnc|mcc|navigation|orientation|screenLayout|screenSize|smallestScreenSize|uiMode|touchscreen";

        /// <summary>
        /// 将 PlayerSettings 中的动态构建参数注入到指定的 CustomAndroidManifest 实例。
        /// 注入内容包括：包名、版本号、versionName、activityName、exported、configChanges、screenOrientation。
        /// 须在所有 SDK 处理器执行完毕后调用，以确保 activityName 已由处理器注册。
        /// </summary>
        /// <param name="manifest">目标 CustomAndroidManifest 实例，不应为 null。</param>
        /// <param name="activityName">主 Activity 完整类名，由 NovaBuildContext.ActivityName 提供。</param>
        public static void Inject(CustomAndroidManifest manifest, string activityName)
        {
            InjectCore(manifest, activityName, () => PlayerSettings.applicationIdentifier,
                () => PlayerSettings.Android.bundleVersionCode.ToString(), () => PlayerSettings.bundleVersion,
                () => PlayerSettings.defaultInterfaceOrientation, true);
        }

        /// <summary>使用已经冻结的纯值执行原Manifest注入算法；仅改传入XML树，不取Unity设置或保存文件。</summary>
        /// <param name="manifest">自有预测XML树。</param><param name="activityName">已按原Context先到先得规则冻结的最终Activity。</param>
        /// <param name="bundleId">原PlayerSettings包名。</param><param name="versionCode">按原文化格式冻结的版本号。</param><param name="versionName">原版本名。</param><param name="orientation">原屏幕方向。</param>
        internal static void InjectFrozen(CustomAndroidManifest manifest, string activityName, string bundleId, string versionCode, string versionName, UIOrientation orientation)
        {
            InjectCore(manifest, activityName, () => bundleId, () => versionCode, () => versionName, () => orientation, false);
        }

        /// <summary>原注入顺序唯一实现；正常入口逐次求值保持原getter及缺Activity早退顺序，预测入口不输出Unity日志。</summary>
        /// <param name="manifest">原领域XML对象。</param><param name="activityName">最终Context名称。</param><param name="bundleId">仅由本类两个入口提供的包名值源。</param><param name="versionCode">仅由本类两个入口提供的版本号值源。</param>
        /// <param name="versionName">仅由本类两个入口提供的版本名值源。</param><param name="orientation">仅在存在launcher且写原属性后才调用的方向值源。</param><param name="logMissing">正常入口保持原缺launcher警告。</param>
        private static void InjectCore(CustomAndroidManifest manifest, string activityName, System.Func<string> bundleId,
            System.Func<string> versionCode, System.Func<string> versionName, System.Func<UIOrientation> orientation, bool logMissing)
        {
            manifest.ManifestElement.SetAttribute("package", bundleId());
            manifest.ManifestElement.Attributes.Append(manifest.CreateAndroidAttribute("versionCode", versionCode()));
            manifest.ManifestElement.Attributes.Append(manifest.CreateAndroidAttribute("versionName", versionName()));
            var activity = manifest.GetActivityWithLaunchIntent() as XmlElement;
            if (activity == null)
            {
                if (logMissing) Log.Warning(LogTag.Editor, "[NovaManifestBootstrapper] 未找到含 MAIN/LAUNCHER intent-filter 的主 Activity 节点，跳过 Activity 属性注入。");
                return;
            }
            var nameAttr = activity.Attributes["android:name"];
            if (nameAttr != null) nameAttr.Value = activityName;
            activity.Attributes.Append(manifest.CreateAndroidAttribute("exported", "true"));
            activity.Attributes.Append(manifest.CreateAndroidAttribute("configChanges", c_ConfigChanges));
            activity.Attributes.Append(manifest.CreateAndroidAttribute("screenOrientation", ToAndroidOrientation(orientation())));
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
