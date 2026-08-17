/***************************************************************
 * (c) copyright 2026 - 2030, Solotopia
 * All Rights Reserved.
 * -------------------------------------------------------------
 * filename:  NativeBuildProcessor.cs
 * author:    taoye
 * created:   2026/8/7
 * descrip:   Native 模块平台构建配置注入
 ***************************************************************/

using NovaFramework.Runtime;
using UnityEditor.Build.Reporting;

namespace NovaFramework.Editor
{
    /// <summary>
    /// Native 模块构建处理器：声明 Android 通知权限，并链接 iOS 原生能力所需系统 Framework。
    /// 不注入 Push capability、aps-environment 或 APNs 后台模式。
    /// </summary>
    public sealed class NativeBuildProcessor : NovaSDKBuildProcessor
    {
        /// <summary>
        /// Android 构建前向受控主 Manifest 声明 POST_NOTIFICATIONS。
        /// </summary>
        /// <param name="report">构建报告。</param>
        /// <param name="context">Nova 构建上下文。</param>
        public override void OnPreprocessBuildOnAndroid(BuildReport report, NovaBuildContext context)
        {
            context.AddManifestRules(new ManifestRuleSet
            {
                Permissions = new[]
                {
                    new PermissionRule("android.permission.POST_NOTIFICATIONS"),
                },
            });
            Log.Debug(LogTag.Editor, "[Native] Android Manifest 已声明 POST_NOTIFICATIONS。");
        }

#if UNITY_IOS
        /// <summary>
        /// iOS 构建后将原生桥接依赖链接到 UnityFramework，并设置 Swift 源文件的语言版本。
        /// </summary>
        /// <param name="report">构建报告。</param>
        /// <param name="context">Nova 构建上下文。</param>
        public override void OnPostprocessBuildOniOS(BuildReport report, NovaBuildContext context)
        {
#if UNITY_2019_3_OR_NEWER
            string frameworkTargetGuid = context.XProj.GetUnityFrameworkTargetGuid();
#else
            string frameworkTargetGuid = context.TargetGuid;
#endif
            XcodeHelper.Project.AddFramework(
                context.XProj,
                frameworkTargetGuid,
                "UserNotifications.framework");
            XcodeHelper.Project.AddFramework(
                context.XProj,
                frameworkTargetGuid,
                "StoreKit.framework");
            XcodeHelper.Project.SetBuildProperty(
                context.XProj,
                frameworkTargetGuid,
                "SWIFT_VERSION",
                "5.0");
            Log.Debug(LogTag.Editor, "[Native] iOS 已向 UnityFramework 链接 UserNotifications.framework、StoreKit.framework 并设置 Swift 5。");
        }
#endif
    }
}
