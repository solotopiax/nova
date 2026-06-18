/***************************************************************
 * (c) copyright 2026 - 2030, Solotopia
 * All Rights Reserved.
 * -------------------------------------------------------------
 * filename:  FirebasePluginBuildProcessor.cs
 * author:    yingzheng
 * created:   2026/4/24
 * descrip:   构建时向框架注入 FirebasePlugin iOS 推送配置
 ***************************************************************/

using NovaFramework.Editor;
using NovaFramework.Runtime;
using UnityEditor.Build.Reporting;
#if UNITY_IOS
using UnityEditor.iOS.Xcode;
#endif

namespace NovaFramework.SDK.FirebasePlugin.Editor
{
    /// <summary>
    /// 构建处理器：Android 预处理注入 FCM Activity 名与 MessageForwardingService；
    /// iOS 构建后向 Xcode 工程注入 Firebase 推送所需的配置。
    /// </summary>
    public sealed class FirebasePluginBuildProcessor : NovaSDKBuildProcessor
    {
        /// <summary>
        /// Firebase FCM 自定义 Activity 完整类名。
        /// </summary>
        private const string c_ActivityName = "com.google.firebase.MessagingUnityPlayerActivity";

        /// <summary>
        /// Firebase FCM 消息转发 Service 完整类名。
        /// </summary>
        private const string c_ForwardingServiceName = "com.google.firebase.messaging.MessageForwardingService";

        /// <summary>
        /// 预处理回调优先级。Firebase 需早于 AF 注册 ActivityName，设为较小值确保优先执行。
        /// </summary>
        public override int PreprocessPriority => 1200;

        /// <summary>
        /// 后处理回调优先级。
        /// </summary>
        public override int PostprocessPriority => 1200;

        /// <summary>
        /// Android 平台构建预处理：注册 FCM Activity 名，并向 Manifest 注入 MessageForwardingService。
        /// </summary>
        /// <param name="report">构建报告。</param>
        /// <param name="context">Nova 构建上下文。</param>
        public override void OnPreprocessBuildOnAndroid(BuildReport report, NovaBuildContext context)
        {
            context.RegisterActivityName(c_ActivityName);

            context.AddManifestRules(new ManifestRuleSet
            {
                MetaDatas = new[]
                {
                    MetaDataRule.WithResource("com.google.firebase.messaging.default_notification_icon", "@drawable/ic_launcher_notification")
                },
                Services = new[]
                {
                    new ServiceRule(c_ForwardingServiceName, exported: true, ManifestRuleMode.Merge)
                    {
                        Permission = "android.permission.BIND_JOB_SERVICE",
                    }
                }
            });

            Log.Debug(LogTag.Editor, "[FirebasePlugin] Android 预处理完成：ActivityName 已注册，MessageForwardingService 已注入，default_notification_icon meta-data 已注入。");
        }

#if UNITY_IOS
        /// <summary>
        /// iOS 平台构建后回调：注入推送所需的 Plist、Entitlements、Capability 及 Framework 配置。
        /// </summary>
        /// <param name="report">构建报告。</param>
        /// <param name="context">Nova 构建上下文，包含已加载的 XProj / XPlist / XEntitlements。</param>
        public override void OnPostprocessBuildOniOS(BuildReport report, NovaBuildContext context)
        {
            XcodeHelper.Plist.EnsureArrayValue(context.XPlistDict, "UIBackgroundModes", "remote-notification");
            XcodeHelper.Entitlements.SetStringValue(context.XEntitlements, "aps-environment", "development");
            XcodeHelper.Project.AddCapability(context.XProj, context.TargetGuid, PBXCapabilityType.PushNotifications, context.RelativeEntitlementFilePath);
            XcodeHelper.Project.AddFramework(context.XProj, context.TargetGuid, "UserNotifications.framework");

            Log.Debug(LogTag.Editor, "[FirebasePlugin] iOS 推送配置已注入：UIBackgroundModes、aps-environment、PushNotifications Capability、UserNotifications.framework。");
        }
#endif
    }
}
