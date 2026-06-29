/***************************************************************
 * (c) copyright 2026 - 2030, Solotopia
 * All Rights Reserved.
 * -------------------------------------------------------------
 * filename:  FacebookPluginBuildProcessor.cs
 * author:    yingzheng
 * created:   2026/6/24
 * descrip:   构建前将 Nova Facebook 配置写入 FacebookSettings 并注入 Manifest 规则
 ***************************************************************/

using System.Collections.Generic;
using System.Globalization;
using NovaFramework.Editor;
using NovaFramework.Runtime;
using UnityEditor;
using UnityEditor.Build.Reporting;
using UnityEngine;

namespace NovaFramework.SDK.Facebook.Editor
{
    /// <summary>
    /// Facebook SDK 构建处理器。
    /// </summary>
    public sealed class FacebookPluginBuildProcessor : NovaSDKBuildProcessor
    {
        /// <summary>
        /// Facebook Unity Overlay Activity 主题。
        /// </summary>
        private const string c_UnityOverlayTheme = "@android:style/Theme.Translucent.NoTitleBar.Fullscreen";

        /// <summary>
        /// 获取构建预处理优先级。
        /// </summary>
        public override int PreprocessPriority => 600;

        /// <summary>
        /// 获取构建后处理优先级。
        /// </summary>
        public override int PostprocessPriority => 600;

#if UNITY_IOS
        /// <summary>
        /// iOS 动态库。
        /// </summary>
        private static readonly string[] s_DynamicLibrariesToEmbed =
        {
            "FBAEMKit.xcframework",
            "FBSDKCoreKit_Basics.xcframework",
            "FBSDKCoreKit.xcframework",
            "FBSDKGamingServicesKit.xcframework",
            "FBSDKLoginKit.xcframework",
            "FBSDKShareKit.xcframework",
        };
#endif

        /// <summary>
        /// Android 构建前写入 FacebookSettings 并注册 Manifest 规则。
        /// </summary>
        /// <param name="report">Unity 构建报告。</param>
        /// <param name="context">Nova 构建上下文。</param>
        public override void OnPreprocessBuildOnAndroid(BuildReport report, NovaBuildContext context)
        {
            if (!ApplyFacebookSettings("Android")) return;

            if (!global::Facebook.Unity.Settings.FacebookSettings.IsValidAppId)
            {
                Log.Warning(LogTag.Editor, "[FacebookPluginBuildProcessor] Facebook AppId 为空，跳过 Manifest 规则注入。");
                return;
            }

            context.AddManifestRules(BuildFacebookManifestRules());
            Log.Debug(LogTag.Editor, "[FacebookPluginBuildProcessor] Android Facebook Manifest 规则已注入。");
        }

        /// <summary>
        /// iOS 构建前写入 FacebookSettings。
        /// </summary>
        /// <param name="report">Unity 构建报告。</param>
        /// <param name="context">Nova 构建上下文。</param>
        public override void OnPreprocessBuildOniOS(BuildReport report, NovaBuildContext context)
        {
            ApplyFacebookSettings("iOS");
        }

#if UNITY_IOS
        /// <summary>
        /// iOS 动态库名单。
        /// </summary>
        /// <returns>动态库名单。</returns>
        protected override string[] GetEmbedXcframeworkNames() => s_DynamicLibrariesToEmbed;
#endif

        /// <summary>
        /// 将 Nova 配置写入 FacebookSettings。
        /// </summary>
        /// <param name="platform">当前构建平台。</param>
        /// <returns>是否写入成功。</returns>
        private static bool ApplyFacebookSettings(string platform)
        {
            FacebookPluginConfig config = GetSDKConfig<FacebookPluginConfig>();
            if (config == null) return false;

            global::Facebook.Unity.Settings.FacebookSettings settings = global::Facebook.Unity.Settings.FacebookSettings.Instance;
            if (settings == null)
            {
                Log.Warning(LogTag.Editor, "[FacebookPluginBuildProcessor] FacebookSettings.Instance 为空，跳过 FacebookSettings 注入。");
                return false;
            }

            string appName = Application.productName;
            string facebookId = config.FacebookAppId ?? string.Empty;
            string clientToken = config.FacebookClientToken ?? string.Empty;

            global::Facebook.Unity.Settings.FacebookSettings.AppIds.Clear();
            global::Facebook.Unity.Settings.FacebookSettings.AppIds.Add(facebookId);

            global::Facebook.Unity.Settings.FacebookSettings.AppLabels.Clear();
            global::Facebook.Unity.Settings.FacebookSettings.AppLabels.Add(appName);

            global::Facebook.Unity.Settings.FacebookSettings.ClientTokens.Clear();
            global::Facebook.Unity.Settings.FacebookSettings.ClientTokens.Add(clientToken);

            EditorUtility.SetDirty(settings);
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();

            Log.Debug(LogTag.Editor, $"[FacebookPluginBuildProcessor] {platform} FacebookSettings 已注入。");
            return true;
        }

        /// <summary>
        /// 构建 Facebook AndroidManifest 规则。
        /// </summary>
        /// <returns>Manifest 规则集。</returns>
        private static ManifestRuleSet BuildFacebookManifestRules()
        {
            string appId = global::Facebook.Unity.Settings.FacebookSettings.AppId;
            string clientToken = global::Facebook.Unity.Settings.FacebookSettings.ClientToken ?? string.Empty;
            string appIdValue = "fb" + appId;
            string autoLogEnabled = global::Facebook.Unity.Settings.FacebookSettings.AutoLogAppEventsEnabled.ToString().ToLowerInvariant();
            string advertiserIdCollectionEnabled = global::Facebook.Unity.Settings.FacebookSettings.AdvertiserIDCollectionEnabled.ToString().ToLowerInvariant();
            string contentProviderAuthority = string.Format(
                CultureInfo.InvariantCulture,
                global::Facebook.Unity.Editor.ManifestMod.FacebookContentProviderAuthFormat,
                appId);

            return new ManifestRuleSet
            {
                MetaDatas = new[]
                {
                    new MetaDataRule(global::Facebook.Unity.Editor.ManifestMod.ApplicationIdMetaDataName, appIdValue, ManifestRuleMode.Replace),
                    new MetaDataRule(global::Facebook.Unity.Editor.ManifestMod.ClientTokenMetaDataName, clientToken, ManifestRuleMode.Replace),
                    new MetaDataRule(global::Facebook.Unity.Editor.ManifestMod.AutoLogAppEventsEnabled, autoLogEnabled, ManifestRuleMode.Replace),
                    new MetaDataRule(global::Facebook.Unity.Editor.ManifestMod.AdvertiserIDCollectionEnabled, advertiserIdCollectionEnabled, ManifestRuleMode.Replace),
                },
                Providers = new[]
                {
                    new ProviderRule(
                        global::Facebook.Unity.Editor.ManifestMod.FacebookContentProviderName,
                        contentProviderAuthority,
                        exported: true,
                        ManifestRuleMode.Replace),
                },
                Activities = BuildFacebookActivityRules(),
            };
        }

        /// <summary>
        /// 构建 Facebook Activity 规则。
        /// </summary>
        /// <returns>Activity 规则数组。</returns>
        private static ActivityRule[] BuildFacebookActivityRules()
        {
            var activities = new List<ActivityRule>
            {
                CreateUnityOverlayActivity(global::Facebook.Unity.Editor.ManifestMod.UnityLoginActivityName),
                CreateUnityOverlayActivity(global::Facebook.Unity.Editor.ManifestMod.UnityDialogsActivityName),
                CreateUnityOverlayActivity(global::Facebook.Unity.Editor.ManifestMod.UnityGamingServicesFriendFinderActivityName),
                CreateAppLinkActivity(),
                new ActivityRule(global::Facebook.Unity.Editor.ManifestMod.DeepLinkingActivityName, exported: true, ManifestRuleMode.Replace),
                new ActivityRule(global::Facebook.Unity.Editor.ManifestMod.UnityGameRequestActivityName, exported: false, ManifestRuleMode.Replace),
                new ActivityRule(global::Facebook.Unity.Editor.ManifestMod.FacebookActivityName, mode: ManifestRuleMode.Remove),
            };

            return activities.ToArray();
        }

        /// <summary>
        /// 创建 Facebook Unity Overlay Activity 规则。
        /// </summary>
        /// <param name="activityName">Activity 名称。</param>
        /// <returns>Activity 规则。</returns>
        private static ActivityRule CreateUnityOverlayActivity(string activityName)
        {
            return new ActivityRule(activityName, exported: false, ManifestRuleMode.Replace)
            {
                ConfigChanges = NovaManifestBootstrapper.c_ConfigChanges,
                Theme = c_UnityOverlayTheme,
            };
        }

        /// <summary>
        /// 创建 Facebook App Link Activity 规则。
        /// </summary>
        /// <returns>Activity 规则。</returns>
        private static ActivityRule CreateAppLinkActivity()
        {
            List<string> schemes = GetSelectedAppLinkSchemes();
            var filters = new List<IntentFilterRule>();
            foreach (string scheme in schemes)
            {
                filters.Add(new IntentFilterRule
                {
                    Actions = new[] { "android.intent.action.VIEW" },
                    Categories = new[] { "android.intent.category.DEFAULT" },
                    Data = new IntentData { Scheme = scheme },
                });
            }

            return new ActivityRule(global::Facebook.Unity.Editor.ManifestMod.AppLinkActivityName, exported: true, ManifestRuleMode.Replace)
            {
                IntentFilters = filters.ToArray(),
            };
        }

        /// <summary>
        /// 获取当前 AppLink scheme 列表。
        /// </summary>
        /// <returns>AppLink scheme 列表。</returns>
        private static List<string> GetSelectedAppLinkSchemes()
        {
            int selectedIndex = global::Facebook.Unity.Settings.FacebookSettings.SelectedAppIndex;
            List<global::Facebook.Unity.Settings.FacebookSettings.UrlSchemes> appLinkSchemes = global::Facebook.Unity.Settings.FacebookSettings.AppLinkSchemes;
            if (appLinkSchemes == null || selectedIndex < 0 || selectedIndex >= appLinkSchemes.Count)
            {
                return new List<string>();
            }

            return appLinkSchemes[selectedIndex].Schemes ?? new List<string>();
        }
    }
}
