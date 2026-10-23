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
using System;
using System.Runtime.ExceptionServices;
using UnityEditor;
using UnityEditor.SceneManagement;
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
            EditorBuildSettingsScene[] scenes = EditorBuildSettings.scenes;
            INativeBuildPreviewObserver observer = NativeBuildPreviewObservers.Current();
            observer?.QueryStarted(this, report, context, scenes);
            bool installerEnabled;
            try { installerEnabled = HasEnabledAppInstaller(scenes); }
            catch (Exception error)
            {
                if (observer != null) ExceptionDispatchInfo.Capture(NativeBuildPreviewObservers.RecordFailure(observer, "query", -1, error)).Throw();
                throw;
            }
            observer?.QueryCompleted(installerEnabled);
            if (installerEnabled)
            {
                context.AddManifestRules(new ManifestRuleSet
                { Permissions = new[] { new PermissionRule("android.permission.REQUEST_INSTALL_PACKAGES") } });
                Log.Debug(LogTag.Editor, "[Native] 已开启显式 APK 安装，声明 REQUEST_INSTALL_PACKAGES。");
            }
        }

        /// <summary>只读检查实际启用的 Build Scene；仅运行拓扑中开启的 Native 配置才要求安装来源权限。</summary>
        /// <param name="scenes">当前正式构建场景列表；禁用场景不生效。</param>
        /// <returns>是否有激活且启用的 NativeComponent 开启显式 APK 安装总门。</returns>
        internal static bool HasEnabledAppInstaller(EditorBuildSettingsScene[] scenes)
        {
            INativeBuildPreviewObserver observer = NativeBuildPreviewObservers.Current();
            EditorBuildSettingsScene[] entries = scenes ?? Array.Empty<EditorBuildSettingsScene>();
            for (int entryIndex = 0; entryIndex < entries.Length; entryIndex++)
            {
                EditorBuildSettingsScene entry = entries[entryIndex];
                if (!entry.enabled || string.IsNullOrEmpty(entry.path)) continue;
                observer?.BeforeOpen(entryIndex, entry.path);
                UnityEngine.SceneManagement.Scene scene;
                try { scene = EditorSceneManager.OpenPreviewScene(entry.path); }
                catch (Exception error)
                {
                    if (observer != null) ExceptionDispatchInfo.Capture(NativeBuildPreviewObservers.RecordFailure(observer, "open", entryIndex, error)).Throw();
                    throw;
                }
                Exception scanFailure = null;
                try
                {
                    observer?.Opened(entryIndex, entry.path, scene);
                    foreach (var root in scene.GetRootGameObjects())
                        foreach (NativeComponent component in root.GetComponentsInChildren<NativeComponent>(true))
                        {
                            if (observer == null)
                            {
                                if (component.enabled && component.gameObject.activeInHierarchy &&
                                    component.NativeManagerConfig?.EnableAndroidAppInstall == true) return true;
                                continue;
                            }
                            bool enabled = component.enabled;
                            bool activeEvaluated = enabled;
                            bool active = activeEvaluated && component.gameObject.activeInHierarchy;
                            bool configEvaluated = activeEvaluated && active;
                            var config = configEvaluated ? component.NativeManagerConfig : null;
                            bool configNonNull = configEvaluated && config != null;
                            bool installEvaluated = configNonNull;
                            bool installEnabled = installEvaluated && config.EnableAndroidAppInstall;
                            observer.CandidateRead(entryIndex, component, enabled, activeEvaluated, active,
                                configEvaluated, configNonNull, installEvaluated, installEnabled);
                            if (installEnabled) { observer.SceneScanCompleted(entryIndex, scene, true); return true; }
                        }
                    observer?.SceneScanCompleted(entryIndex, scene, false);
                }
                catch (Exception error) { scanFailure = error; throw; }
                finally
                {
                    if (observer == null) EditorSceneManager.ClosePreviewScene(scene);
                    else CloseObservedPreview(observer, entryIndex, scene, scanFailure);
                }
            }
            return false;
        }

        /// <summary>先完成原关闭，再传播观察异常；Opened和BeforeClose失败均不能跳过原finally。</summary>
        /// <param name="observer">本次准确观察者。</param><param name="entryIndex">原数组索引。</param><param name="scene">原Open真正返回的场景。</param><param name="scanFailure">原try真实异常，正常或return时为null。</param>
        private static void CloseObservedPreview(INativeBuildPreviewObserver observer, int entryIndex,
            UnityEngine.SceneManagement.Scene scene, Exception scanFailure)
        {
            Exception beforeFailure = null;
            try { observer.BeforeClose(entryIndex, scene); }
            catch (Exception error) { beforeFailure = error; }
            bool closed;
            try { closed = EditorSceneManager.ClosePreviewScene(scene); }
            catch (Exception error)
            {
                Exception failure = NativeBuildPreviewObservers.RecordFailure(observer, "close", entryIndex, error);
                if (scanFailure != null || beforeFailure != null)
                {
                    var closeFailures = new System.Collections.Generic.List<Exception>();
                    if (scanFailure != null) closeFailures.Add(scanFailure);
                    if (beforeFailure != null) closeFailures.Add(beforeFailure);
                    closeFailures.Add(failure); failure = new AggregateException("Native预览关闭与先前阶段失败。", closeFailures);
                }
                ExceptionDispatchInfo.Capture(failure).Throw(); throw;
            }
            Exception afterFailure = null;
            try { observer.Closed(entryIndex, scene, closed); }
            catch (Exception error) { afterFailure = error; }
            if (beforeFailure == null && afterFailure == null) return;
            var failures = new System.Collections.Generic.List<Exception>();
            if (scanFailure != null) failures.Add(scanFailure);
            if (beforeFailure != null) failures.Add(beforeFailure);
            if (afterFailure != null) failures.Add(afterFailure);
            ExceptionDispatchInfo.Capture(failures.Count == 1 ? failures[0] : new AggregateException("Native预览观察失败，原关闭已执行。", failures)).Throw();
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
