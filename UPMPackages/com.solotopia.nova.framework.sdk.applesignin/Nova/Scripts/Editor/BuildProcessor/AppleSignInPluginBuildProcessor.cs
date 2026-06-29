/***************************************************************
 * (c) copyright 2026 - 2030, Solotopia
 * All Rights Reserved.
 * -------------------------------------------------------------
 * filename:  AppleSignInPluginBuildProcessor.cs
 * author:    Codex
 * created:   2026/6/25
 * descrip:   Apple 登录插件构建处理器
 ***************************************************************/

using NovaFramework.Editor;
using NovaFramework.Runtime;
using UnityEditor.Build.Reporting;

#if UNITY_IOS
using System;
using System.Reflection;
using UnityEditor.iOS.Xcode;
#endif

namespace NovaFramework.SDK.AppleSignIn.Editor
{
    /// <summary>
    /// Apple 登录插件构建处理器。
    /// </summary>
    public sealed class AppleSignInPluginBuildProcessor : NovaSDKBuildProcessor
    {
        /// <summary>
        /// 获取构建预处理优先级。
        /// </summary>
        public override int PreprocessPriority => 630;

        /// <summary>
        /// 获取构建后处理优先级。
        /// </summary>
        public override int PostprocessPriority => 630;

        /// <summary>
        /// iOS 构建前校验 Apple 登录配置。
        /// </summary>
        /// <param name="report">Unity 构建报告。</param>
        /// <param name="context">Nova 构建上下文。</param>
        public override void OnPreprocessBuildOniOS(BuildReport report, NovaBuildContext context)
        {
            AppleSignInPluginConfig config = GetSDKConfig<AppleSignInPluginConfig>();
            if (config == null) return;

            Log.Debug(LogTag.Editor, "[AppleSignInPluginBuildProcessor] iOS Apple 登录配置校验完成。");
        }

#if UNITY_IOS
        /// <summary>
        /// iOS 构建后写入 Apple 登录能力。
        /// </summary>
        /// <param name="report">Unity 构建报告。</param>
        /// <param name="context">Nova 构建上下文。</param>
        public override void OnPostprocessBuildOniOS(BuildReport report, NovaBuildContext context)
        {
            AppleSignInPluginConfig config = GetSDKConfig<AppleSignInPluginConfig>();
            if (config == null) return;

            EnsureAppleSignInEntitlement(context);
            EnsureAuthenticationServicesFramework(context);
            EnsureAppleSignInCapability(context);

            Log.Debug(LogTag.Editor, "[AppleSignInPluginBuildProcessor] iOS Apple 登录能力已注入。");
        }

        /// <summary>
        /// 确保 Apple 登录 entitlement。
        /// </summary>
        /// <param name="context">Nova 构建上下文。</param>
        private static void EnsureAppleSignInEntitlement(NovaBuildContext context)
        {
            if (context.XEntitlements == null)
            {
                Log.Warning(LogTag.Editor, "[AppleSignInPluginBuildProcessor] XEntitlements 为空，跳过 Apple 登录 entitlement。");
                return;
            }

            var plistArray = new PlistElementArray();
            plistArray.AddString("Default");
            context.XEntitlements.root["com.apple.developer.applesignin"] = plistArray;
        }

        /// <summary>
        /// 确保 AuthenticationServices 框架。
        /// </summary>
        /// <param name="context">Nova 构建上下文。</param>
        private static void EnsureAuthenticationServicesFramework(NovaBuildContext context)
        {
            if (context.XProj == null)
            {
                Log.Warning(LogTag.Editor, "[AppleSignInPluginBuildProcessor] XProj 为空，跳过 AuthenticationServices.framework。");
                return;
            }

#if UNITY_2019_3_OR_NEWER
            string frameworkTargetGuid = context.XProj.GetUnityFrameworkTargetGuid();
#else
            string frameworkTargetGuid = context.TargetGuid;
#endif
            context.XProj.AddFrameworkToProject(frameworkTargetGuid, "AuthenticationServices.framework", true);
        }

        /// <summary>
        /// 确保 Apple 登录 capability。
        /// </summary>
        /// <param name="context">Nova 构建上下文。</param>
        private static void EnsureAppleSignInCapability(NovaBuildContext context)
        {
            if (context.XProj == null || string.IsNullOrEmpty(context.TargetGuid))
            {
                Log.Warning(LogTag.Editor, "[AppleSignInPluginBuildProcessor] XProj 或 TargetGuid 为空，跳过 Apple 登录 capability。");
                return;
            }

            if (string.IsNullOrEmpty(context.RelativeEntitlementFilePath))
            {
                Log.Warning(LogTag.Editor, "[AppleSignInPluginBuildProcessor] Entitlements 路径为空，跳过 Apple 登录 capability。");
                return;
            }

            context.XProj.AddCapability(context.TargetGuid, CreateEmptyCapability(), context.RelativeEntitlementFilePath);
        }

        /// <summary>
        /// 创建兼容 AppleAuth 的空能力。
        /// </summary>
        /// <returns>空能力类型。</returns>
        private static PBXCapabilityType CreateEmptyCapability()
        {
            const BindingFlags c_BindingFlags = BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance;

            ConstructorInfo constructorInfo = typeof(PBXCapabilityType)
                .GetConstructor(c_BindingFlags, null, new[] { typeof(bool), typeof(string), typeof(bool) }, null);

            if (constructorInfo != null)
            {
                return (PBXCapabilityType)constructorInfo.Invoke(new object[] { true, string.Empty, true });
            }

            constructorInfo = typeof(PBXCapabilityType)
                .GetConstructor(c_BindingFlags, null, new[] { typeof(string), typeof(bool), typeof(string), typeof(bool) }, null);

            if (constructorInfo != null)
            {
                return (PBXCapabilityType)constructorInfo.Invoke(new object[] { "com.lupidan.apple-signin-unity.empty", true, string.Empty, true });
            }

            throw new InvalidOperationException("当前 Unity 版本无法创建 Apple 登录 capability。");
        }
#endif
    }
}
