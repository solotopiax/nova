/***************************************************************
 * (c) copyright 2026 - 2030, Solotopia
 * All Rights Reserved.
 * -------------------------------------------------------------
 * filename:  ThirdPayPluginBuildProcessor.cs
 * author:    yingzheng
 * created:   2026/11/16
 * descrip:   ThirdPay 自有 iOS 浏览器的构建接线
 ***************************************************************/

using NovaFramework.Editor;
using UnityEditor.Build.Reporting;
#if UNITY_IOS
using UnityEditor.iOS.Xcode;
#endif

namespace NovaFramework.SDK.IAP.ThirdPay.Editor
{
    /// <summary>
    /// 复用 Nova 构建处理链，为自有 iOS 系统浏览器链接 framework。
    /// </summary>
    public sealed class ThirdPayPluginBuildProcessor : NovaSDKBuildProcessor
    {
        /// <summary>
        /// 接在公共原生基础接线后处理，由 Nova 统一保存工程。
        /// </summary>
        public override int PostprocessPriority => 300;

#if UNITY_IOS
        /// <summary>
        /// 只链接系统浏览器 framework，不修改项目的回跳协议配置。
        /// </summary>
        public override void OnPostprocessBuildOniOS(BuildReport report, NovaBuildContext context)
        {
            XcodeHelper.Project.AddFramework(context.XProj, context.XProj.GetUnityFrameworkTargetGuid(), "SafariServices.framework");
        }
#endif
    }
}
