/***************************************************************
 * (c) copyright 2026 - 2030, Solotopia
 * All Rights Reserved.
 * -------------------------------------------------------------
 * filename:  NativeManager.AppInstall.Android.cs
 * author:    taoye
 * created:   2026/10/13
 * descrip:   独立 Android APK 安装能力
 ***************************************************************/

#if UNITY_ANDROID && !UNITY_EDITOR
using System;
using System.Threading;
using Cysharp.Threading.Tasks;
using UnityEngine;

namespace NovaFramework.Runtime
{
    internal sealed partial class NativeManager
    {
        private const string c_AppInstallerBridgeClass = "com.solotopia.nova.nativebridge.NovaNativeAppInstallerBridge";
        private const string c_AppInstallerCallbackClass = c_AppInstallerBridgeClass + "$Callback";

        /// <summary>Android JNI 完成回调只汇总当前操作 TCS；取消后的迟到结果不改状态。</summary>
        [UnityEngine.Scripting.Preserve]
        private sealed class AndroidAppInstallCallback : AndroidJavaProxy
        {
            private readonly UniTaskCompletionSource<NativeAppInstallResult> m_Pending;
            private bool m_Active = true;
            /// <summary>建立本次独占安装操作的回调代理。</summary>
            /// <param name="pending">只属于本次调用的结果完成源。</param>
            internal AndroidAppInstallCallback(UniTaskCompletionSource<NativeAppInstallResult> pending) : base(c_AppInstallerCallbackClass)
            { m_Pending = pending; }
            /// <summary>Java 回调映射明确结果；未知平台值只能视为失败。</summary>
            /// <param name="status">Java 与公开枚举一致的最终状态数字。</param>
            /// <param name="message">技术错误描述。</param>
            [UnityEngine.Scripting.Preserve]
            public void onComplete(int status, string message) => CompleteAsync(status, message).Forget();

            /// <summary>Android 界面线程切回 Unity PlayerLoop，只有仍有效的本次代理可以完成等待。</summary>
            /// <param name="status">Java 最终状态数字。</param>
            /// <param name="message">技术错误描述。</param>
            private async UniTaskVoid CompleteAsync(int status, string message)
            {
                await UniTask.SwitchToMainThread();
                if (!m_Active) return;
                var mapped = Enum.IsDefined(typeof(NativeAppInstallStatus), status) ? (NativeAppInstallStatus)status : NativeAppInstallStatus.Failed;
                m_Pending.TrySetResult(new NativeAppInstallResult(mapped, message));
            }

            /// <summary>结束操作后丢弃迟到 JNI 回调并释放未完成等待。</summary>
            internal void Deactivate() { m_Active = false; m_Pending.TrySetCanceled(); }
        }

        /// <summary>在 Unity 主线程发起 JNI；cancel 只触发本次 requestId 的原生放弃，finally 解除代理持有。</summary>
        /// <param name="path">托管已初验的受控本地文件。</param>
        /// <param name="ct">Manager 与调用方合并取消令牌。</param>
        /// <returns>系统最终安装状态；过程用户确认由原生层处理。</returns>
        private async UniTask<NativeAppInstallResult> InstallApkAndroidAsync(string path, CancellationToken ct)
        {
            string requestId = Guid.NewGuid().ToString("N");
            var pending = new UniTaskCompletionSource<NativeAppInstallResult>();
            var callback = new AndroidAppInstallCallback(pending);
            using var bridge = new AndroidJavaClass(c_AppInstallerBridgeClass);
            using AndroidJavaObject activity = GetCurrentActivity();
            try
            {
                ct.ThrowIfCancellationRequested();
                bridge.CallStatic("install", activity, requestId, Application.persistentDataPath, path, callback);
                return await pending.Task.AttachExternalCancellation(ct);
            }
            catch (OperationCanceledException) { throw; }
            catch (Exception exception)
            { return new NativeAppInstallResult(NativeAppInstallStatus.Failed, exception.Message); }
            finally
            {
                await UniTask.SwitchToMainThread();
                callback.Deactivate();
                try { bridge.CallStatic("cancel", requestId); }
                catch (Exception exception) { Log.Warning(LogTag.Base, "清理 APK 安装桥接失败：{0}", exception.Message); }
                GC.KeepAlive(callback);
            }
        }

        /// <summary>按 Unity 官方 JNI 入口读取当前 Activity；不把 Activity 引用长期存于 Manager。</summary>
        /// <returns>当前 Unity Activity 的独立 JNI 引用，调用方负责 Dispose。</returns>
        private static AndroidJavaObject GetCurrentActivity()
        {
            using var unity = new AndroidJavaClass("com.unity3d.player.UnityPlayer");
            return unity.GetStatic<AndroidJavaObject>("currentActivity");
        }

        /// <summary>初始化只清理本框架上次进程遗留的暂存与 session，不打开系统界面。</summary>
        private void InitializeAppInstallerAndroid()
        {
            using var bridge = new AndroidJavaClass(c_AppInstallerBridgeClass);
            using AndroidJavaObject activity = GetCurrentActivity();
            bridge.CallStatic("initialize", activity);
        }
    }
}
#endif
