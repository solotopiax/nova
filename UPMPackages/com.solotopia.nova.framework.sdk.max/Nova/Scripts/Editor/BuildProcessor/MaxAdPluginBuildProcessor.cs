/***************************************************************
 * (c) copyright 2026 - 2030, Solotopia
 * All Rights Reserved.
 * -------------------------------------------------------------
 * filename:  MaxAdPluginBuildProcessor.cs
 * author:    yingzheng
 * created:   2026/5/18
 * descrip:   构建预处理：把 MaxAdChannelConfig 的 SdkKey 与 AdMob AppId 写入 AppLovinSettings.asset
 ***************************************************************/

using NovaFramework.Editor;
using NovaFramework.Runtime;
using NovaFramework.SDK.AdPlugin.Runtime;
using NovaFramework.SDK.MaxAdPlugin.Runtime;
using UnityEditor;
using UnityEditor.Build.Reporting;

namespace NovaFramework.SDK.MaxAdPlugin.Editor
{
    /// <summary>
    /// MAX SDK 构建预处理器。
    /// 从 ConfigRuntimeSO -> AdPluginConfig -> MaxAdChannelConfig 读取 SdkKey 与 AdMob AppId，
    /// 在 Android / iOS 预处理时写入 AppLovinSettings.asset 并保存刷新。
    /// iOS 后处理由基类 NovaSDKBuildProcessor 通过 GetEmbedXcframeworkNames 名单托管，自动 Embed MAX 依赖的 xcframework。
    /// </summary>
    public sealed class MaxAdPluginBuildProcessor : NovaSDKBuildProcessor
    {
        /// <summary>
        /// 预处理优先级，沿用 Solar 老实现的 600；值越小越早执行。
        /// </summary>
        public override int PreprocessPriority => 600;

        /// <summary>
        /// 后处理优先级，与预处理保持一致。
        /// </summary>
        public override int PostprocessPriority => 600;

        /// <summary>
        /// Android 平台预处理：写入 SdkKey 与 AdMobAndroidAppId。
        /// </summary>
        /// <param name="report">Unity 构建报告。</param>
        /// <param name="context">Nova 构建上下文。</param>
        public override void OnPreprocessBuildOnAndroid(BuildReport report, NovaBuildContext context)
        {
            ApplyMaxSettings(isAndroid: true);
        }

        /// <summary>
        /// iOS 平台预处理：写入 SdkKey 与 AdMobIosAppId。
        /// </summary>
        /// <param name="report">Unity 构建报告。</param>
        /// <param name="context">Nova 构建上下文。</param>
        public override void OnPreprocessBuildOniOS(BuildReport report, NovaBuildContext context)
        {
            ApplyMaxSettings(isAndroid: false);
        }

        /// <summary>
        /// 通用注入逻辑：解析 MaxAdChannelConfig，写入 AppLovinSettings 后保存刷新。
        /// </summary>
        /// <param name="isAndroid">true 写入 AdMobAndroidAppId；false 写入 AdMobIosAppId。</param>
        private void ApplyMaxSettings(bool isAndroid)
        {
            AdPluginConfig adConfig = GetSDKConfig<AdPluginConfig>();
            if (adConfig == null) return;

            MaxAdChannelConfig maxConfig = null;
            foreach (IAdChannelConfig channelConfig in adConfig.ChannelConfigs.Items)
            {
                if (channelConfig is MaxAdChannelConfig hit)
                {
                    maxConfig = hit;
                    break;
                }
            }
            if (maxConfig == null)
            {
                Log.Warning(LogTag.Editor, "[MaxAdPluginBuildProcessor] AdPluginConfig 中未配置 MaxAdChannelConfig，跳过 AppLovinSettings 注入。");
                return;
            }

            AppLovinSettings settings = AppLovinSettings.Instance;
            if (settings == null)
            {
                Log.Warning(LogTag.Editor, "[MaxAdPluginBuildProcessor] AppLovinSettings.Instance 为空，跳过 AppLovinSettings 注入。");
                return;
            }

            settings.SdkKey = maxConfig.AppKey;
            if (isAndroid)
            {
                settings.AdMobAndroidAppId = maxConfig.AdMobAppIdAndroid;
            }
            else
            {
                settings.AdMobIosAppId = maxConfig.AdMobAppIdIOS;
            }

            settings.SaveAsync();
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();

            string platform = isAndroid ? "Android" : "iOS";
            Log.Debug(LogTag.Editor, $"[MaxAdPluginBuildProcessor] {platform} AppLovinSettings 已注入：SdkKey 与 AdMob AppId 已写入 AppLovinSettings.asset。");
        }

#if UNITY_IOS
        /// <summary>
        /// MAX SDK 依赖的 xcframework 列表，构建后处理时由基类 EmbedDynamicLibraries 在 Pods 目录递归搜索并 Embed 到 Unity-iPhone target。
        /// 列表对齐 Solar BuildProcessorMax 老实现。
        /// </summary>
        private static readonly string[] s_DynamicLibrariesToEmbed =
        {
            "DTBiOSSDK.xcframework",
            "IASDKCore.xcframework",
            "OMSDK_Appodeal.xcframework",
            "OMSDK_Pubmatic.xcframework",
            "OMSDK_Pubnativenet.xcframework",
            "OMSDK_Smaato.xcframework",
        };

        /// <summary>
        /// 返回 MAX SDK 依赖的 xcframework 名单，由基类 OnPostprocessBuildOniOS 默认实现负责 Embed。
        /// </summary>
        /// <returns>xcframework 名称数组。</returns>
        protected override string[] GetEmbedXcframeworkNames() => s_DynamicLibrariesToEmbed;
#endif
    }
}
