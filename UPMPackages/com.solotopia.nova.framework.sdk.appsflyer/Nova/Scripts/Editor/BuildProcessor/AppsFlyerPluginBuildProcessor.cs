/***************************************************************
 * (c) copyright 2026 - 2030, Solotopia
 * All Rights Reserved.
 * -------------------------------------------------------------
 * filename:  AppsFlyerPluginBuildProcessor.cs
 * author:    yingzheng
 * created:   2026/4/23
 * descrip:   构建时向框架注入 AppsFlyerPlugin OneLink Android/iOS 配置
 ***************************************************************/

using NovaFramework.Editor;
using NovaFramework.Runtime;
using NovaFramework.SDK.AppsFlyerPlugin.Runtime;
using UnityEditor.Build.Reporting;
#if UNITY_IOS
using UnityEditor.iOS.Xcode;
#endif

namespace NovaFramework.SDK.AppsFlyerPlugin.Editor
{
    /// <summary>
    /// 构建处理器：从 ConfigRuntimeSO 读取 AppsFlyerPluginConfig，向框架注入 OneLink 相关 AndroidManifest / iOS 规则。
    /// </summary>
    public sealed class AppsFlyerPluginBuildProcessor : NovaSDKBuildProcessor
    {
        /// <summary>
        /// 预处理回调优先级。值越小越早执行；AF 依赖 Firebase 处理器已写入 context.ActivityName，故设为 300。
        /// </summary>
        public override int PreprocessPriority => 300;

        /// <summary>
        /// 后处理回调优先级，与预处理保持一致。
        /// </summary>
        public override int PostprocessPriority => 300;

        /// <summary>
        /// Android 平台构建前回调：从 ConfigRuntimeSO 读取 AppsFlyerPluginConfig，向框架注入 OneLink AndroidManifest 规则集。
        /// </summary>
        /// <param name="report">构建报告。</param>
        /// <param name="context">Nova 构建上下文，携带跨处理器共享数据（含 ActivityName）。</param>
        public override void OnPreprocessBuildOnAndroid(BuildReport report, NovaBuildContext context)
        {
            var config = GetSDKConfig<AppsFlyerPluginConfig>();
            if (config == null) return;

            string oneLinkHost = config.OneLinkHost;
            string oneLinkPathPrefix = config.OneLinkPathPrefix;
            string oneLinkScheme = config.OneLinkFallbackName;

            if (string.IsNullOrEmpty(oneLinkHost) && string.IsNullOrEmpty(oneLinkScheme))
            {
                Log.Debug(LogTag.Editor, "[AppsFlyerPlugin] OneLink 配置为空，跳过 Manifest 规则注入。");
                return;
            }

            var ruleSet = new ManifestRuleSet
            {
                Activities = new[]
                {
                    new ActivityRule(context.ActivityName, exported: true, ManifestRuleMode.Merge)
                    {
                        UseMainActivity = true,
                        IntentFilters = new[]
                        {
                            new IntentFilterRule
                            {
                                AutoVerify = true,
                                Mode = ManifestRuleMode.Replace,
                                Actions = new[] { "android.intent.action.VIEW" },
                                Categories = new[] { "android.intent.category.DEFAULT", "android.intent.category.BROWSABLE" },
                                Data = new IntentData { Scheme = "https", Host = oneLinkHost, PathPrefix = oneLinkPathPrefix }
                            },
                            new IntentFilterRule
                            {
                                Mode = ManifestRuleMode.Replace,
                                Actions = new[] { "android.intent.action.VIEW" },
                                Categories = new[] { "android.intent.category.DEFAULT", "android.intent.category.BROWSABLE" },
                                Data = new IntentData { Scheme = oneLinkScheme }
                            }
                        }
                    }
                }
            };

            context.AddManifestRules(ruleSet);

            Log.Debug(LogTag.Editor, $"[AppsFlyerPlugin] OneLink 规则已注入：Activity={context.ActivityName}, Host={oneLinkHost}, PathPrefix={oneLinkPathPrefix}, Scheme={oneLinkScheme}");
        }

#if UNITY_IOS
        /// <summary>
        /// iOS 平台构建后回调：从 ConfigRuntimeSO 读取 AppsFlyerPluginConfig，向 Xcode 工程注入 AF OneLink 所需的
        /// Associated Domains、SKAdNetwork 归因端点及 Application Identifier 配置。
        /// </summary>
        /// <param name="report">构建报告。</param>
        /// <param name="context">Nova 构建上下文，包含已加载的 XProj / XPlist / XEntitlements。</param>
        public override void OnPostprocessBuildOniOS(BuildReport report, NovaBuildContext context)
        {
            var config = GetSDKConfig<AppsFlyerPluginConfig>();
            if (config == null) return;

            XcodeHelper.Entitlements.SetStringValue(context.XEntitlements, "application-identifier", "$(TeamIdentifierPrefix)$(CFBundleIdentifier)");
            XcodeHelper.Plist.SetString(context.XPlistDict, "NSAdvertisingAttributionReportEndpoint", "https://appsflyer-skadnetwork.com/");

            string oneLinkHost = config.OneLinkHost;
            if (!string.IsNullOrEmpty(oneLinkHost))
            {
                XcodeHelper.Project.AddCapability(context.XProj, context.TargetGuid, PBXCapabilityType.AssociatedDomains, context.RelativeEntitlementFilePath);
                XcodeHelper.Entitlements.EnsureArrayValue(context.XEntitlements, "com.apple.developer.associated-domains", $"applinks:{oneLinkHost}");
                Log.Debug(LogTag.Editor, $"[AppsFlyerPlugin] iOS Associated Domains 已注入：host={oneLinkHost}");
            }
            else
            {
                Log.Warning(LogTag.Editor, "[AppsFlyerPlugin] iOS 后处理：OneLinkHost 为空，跳过 Associated Domains 注入。");
            }

            string oneLinkScheme = config.OneLinkFallbackName;
            if (!string.IsNullOrEmpty(oneLinkScheme))
            {
                XcodeHelper.Plist.AddOrModifyUrlScheme(context.XPlistDict, "appsflyer-unity-onelink", "appsflyer-unity-onelink", oneLinkScheme);
                Log.Debug(LogTag.Editor, $"[AppsFlyerPlugin] iOS CFBundleURLSchemes 已注入：scheme={oneLinkScheme}");
            }
            else
            {
                Log.Warning(LogTag.Editor, "[AppsFlyerPlugin] iOS 后处理：OneLinkFallbackName 为空，跳过 CFBundleURLSchemes 注入。");
            }

            Log.Debug(LogTag.Editor, "[AppsFlyerPlugin] iOS 后处理完成：application-identifier、AssociatedDomains Capability、SKAdNetwork 归因端点、CFBundleURLSchemes 已注入。");
        }
#endif

    }
}