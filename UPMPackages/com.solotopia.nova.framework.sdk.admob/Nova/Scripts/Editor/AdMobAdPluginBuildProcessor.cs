/***************************************************************
 * (c) copyright 2026 - 2030, Solotopia
 * All Rights Reserved.
 * -------------------------------------------------------------
 * filename:  AdMobAdPluginBuildProcessor.cs
 * author:    Codex
 * created:   2026/10/13
 * descrip:   复用 Nova 构建链把内嵌 AdMob App ID 写入项目的官方配置资产
 ***************************************************************/

using System;
using NovaFramework.Editor;
using NovaFramework.Runtime;
using NovaFramework.SDK.AdPlugin.Runtime;
using NovaFramework.SDK.AdMobAdPlugin.Runtime;
using UnityEditor;
using UnityEditor.Build;
using UnityEditor.Build.Reporting;
using UnityEngine;

namespace NovaFramework.SDK.AdMobAdPlugin.Editor
{
    /// <summary>在 Nova 的 -1 预处理链内注入官方 GoogleMobileAdsSettings，先于官方 callbackOrder=0 消费；不修改厂商源码或 resolver。</summary>
    public sealed class AdMobAdPluginBuildProcessor : NovaSDKBuildProcessor
    {
        /// <summary>在本轮 Nova 普通 SDK 配置预处理时执行。</summary>
        public override int PreprocessPriority => 600;

        /// <summary>从激活的 ConfigRuntimeSO 写目标 Android App ID；缺失和重复启用渠道阻断构建。</summary>
        /// <param name="report">当前构建报告。</param>
        /// <param name="context">统一 Nova 构建上下文。</param>
        public override void OnPreprocessBuildOnAndroid(BuildReport report, NovaBuildContext context) => ApplySettings(true);

        /// <summary>从激活的 ConfigRuntimeSO 写目标 iOS App ID，保留官方 plist 和依赖处理链。</summary>
        /// <param name="report">当前构建报告。</param>
        /// <param name="context">统一 Nova 构建上下文。</param>
        public override void OnPreprocessBuildOniOS(BuildReport report, NovaBuildContext context) => ApplySettings(false);

        /// <summary>仅处理一项已启用 AdMob 配置；目标 App ID 通过 SerializedObject 写入项目资产，不写 SDK 包。</summary>
        /// <param name="android">当前是否 Android 构建。</param>
        private void ApplySettings(bool android)
        {
            AdPluginConfig adConfig = GetSDKConfig<AdPluginConfig>();
            if (adConfig == null) return;
            AdMobAdChannelConfig selected = null;
            foreach (IAdChannelConfig channel in adConfig.ChannelConfigs.Items)
            {
                if (!(channel is AdMobAdChannelConfig config) || !config.Enabled) continue;
                if (selected != null) throw new BuildFailedException("当前配置启用了多项独立 AdMob 渠道；请保留一项，避免多个原厂初始化和 App ID 冲突。");
                selected = config;
            }
            if (selected == null) return;
            string appId = (android ? selected.AppIdAndroid : selected.AppIdIOS)?.Trim();
            if (string.IsNullOrEmpty(appId) || !System.Text.RegularExpressions.Regex.IsMatch(appId, @"^ca-app-pub-\d{16}~\d{10}$"))
                throw new BuildFailedException("目标平台 AdMob App ID 缺失或格式不正确；App ID 使用 ~，不能填带 / 的广告位 ID。");
#if NOVA_GOOGLE_MOBILE_ADS
            // 官方类为 internal，没有公共 Settings API；只解析当前已核版本的确切类型和项目资产。
            Type settingsType = Type.GetType("GoogleMobileAds.Editor.GoogleMobileAdsSettings, GoogleMobileAds.Editor", false);
            if (settingsType == null) throw new BuildFailedException("已安装 Google Mobile Ads SDK 缺少当前版本 Settings 类型，请核对匹配的官方 11.5.x SDK。");
            const string assetPath = "Assets/GoogleMobileAds/Resources/GoogleMobileAdsSettings.asset";
            UnityEngine.Object existing = AssetDatabase.LoadMainAssetAtPath(assetPath);
            if (existing != null && existing.GetType() != settingsType) throw new BuildFailedException("GoogleMobileAdsSettings 正式路径已有不同类型资产，不自动覆盖。");
            ScriptableObject settings = existing as ScriptableObject;
            if (settings == null)
            {
                EnsureFolder("Assets/GoogleMobileAds");
                EnsureFolder("Assets/GoogleMobileAds/Resources");
                settings = ScriptableObject.CreateInstance(settingsType);
                AssetDatabase.CreateAsset(settings, assetPath);
            }
            var serialized = new SerializedObject(settings);
            SerializedProperty property = serialized.FindProperty(android ? "adMobAndroidAppId" : "adMobIOSAppId");
            if (property == null) throw new BuildFailedException("官方 Settings 序列化字段已变化，请核对匹配 SDK 适配后再构建。");
            property.stringValue = appId;
            serialized.ApplyModifiedPropertiesWithoutUndo();
            EditorUtility.SetDirty(settings);
            AssetDatabase.SaveAssetIfDirty(settings);
#else
            throw new BuildFailedException("独立 AdMob 已启用但未安装匹配 Google Mobile Ads Unity SDK；请使用既有 UPM 安装流程接入官方 SDK 后再构建。");
#endif
        }

#if NOVA_GOOGLE_MOBILE_ADS
        /// <summary>使用 Unity Editor API 创建项目官方 Settings 所需目录，不手写资产或 meta。</summary>
        /// <param name="path">当前项目 Assets 下的规范目录。</param>
        private static void EnsureFolder(string path)
        {
            if (AssetDatabase.IsValidFolder(path)) return;
            int slash = path.LastIndexOf('/');
            AssetDatabase.CreateFolder(path.Substring(0, slash), path.Substring(slash + 1));
        }
#endif
    }
}
