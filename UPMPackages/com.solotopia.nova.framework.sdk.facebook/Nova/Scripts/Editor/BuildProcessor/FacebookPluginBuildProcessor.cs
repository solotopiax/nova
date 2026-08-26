/***************************************************************
 * (c) copyright 2026 - 2030, Solotopia
 * All Rights Reserved.
 * -------------------------------------------------------------
 * filename:  FacebookPluginBuildProcessor.cs
 * author:    yingzheng
 * created:   2026/6/24
 * descrip:   构建前将 Nova Facebook 配置写入 FacebookSettings 并调用官方 Manifest 生成器
 ***************************************************************/

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
        /// 本次 Android 预处理是否已成功将 Nova 配置写入 FacebookSettings。
        /// </summary>
        private bool m_AndroidSettingsApplied;

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
        /// Android 构建前写入 FacebookSettings。
        /// </summary>
        /// <param name="report">Unity 构建报告。</param>
        /// <param name="context">Nova 构建上下文。</param>
        public override void OnPreprocessBuildOnAndroid(BuildReport report, NovaBuildContext context)
        {
            m_AndroidSettingsApplied = ApplyFacebookSettings("Android");
        }

        /// <summary>
        /// Android Nova 预处理收口后调用 Facebook 官方 Manifest 生成器。
        /// </summary>
        /// <param name="report">Unity 构建报告。</param>
        /// <param name="context">Nova 构建上下文。</param>
        public override void OnAfterNovaPreprocessBuildOnAndroid(BuildReport report, NovaBuildContext context)
        {
            if (!m_AndroidSettingsApplied)
            {
                Log.Warning(LogTag.Editor, "[FacebookPluginBuildProcessor] 本次 Android FacebookSettings 未写入成功，跳过 Facebook 官方 Manifest 生成。");
                return;
            }

            if (!global::Facebook.Unity.Settings.FacebookSettings.IsValidAppId)
            {
                Log.Warning(LogTag.Editor, "[FacebookPluginBuildProcessor] Facebook AppId 为空，跳过 Facebook 官方 Manifest 生成。");
                return;
            }

            global::Facebook.Unity.Editor.ManifestMod.GenerateManifest();
            Log.Debug(LogTag.Editor, "[FacebookPluginBuildProcessor] Facebook 官方 Manifest 生成已执行。");
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
    }
}
