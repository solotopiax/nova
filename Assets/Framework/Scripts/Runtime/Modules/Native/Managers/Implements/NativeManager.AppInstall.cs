/***************************************************************
 * (c) copyright 2026 - 2030, Solotopia
 * All Rights Reserved.
 * -------------------------------------------------------------
 * filename:  NativeManager.AppInstall.cs
 * author:    taoye
 * created:   2026/10/13
 * descrip:   独立 Android APK 安装能力
 ***************************************************************/

using System;
using System.Threading;
using Cysharp.Threading.Tasks;
using UnityEngine;

namespace NovaFramework.Runtime
{
    internal sealed partial class NativeManager : INativeAppInstaller
    {
        private CancellationTokenSource m_AppInstallCancellation;
        private int m_AppInstallVersion;

        /// <summary>查询操作系统来源安装许可；默认总门关闭及非 Android/API28 平台返回 false。</summary>
        /// <param name="ct">取消当前查询。</param>
        /// <returns>当前系统是否允许显式安装请求。</returns>
        public async UniTask<bool> CanRequestPackageInstallsAsync(CancellationToken ct = default)
        {
            EnsureInitialized(); ct.ThrowIfCancellationRequested();
            bool enabled = m_Config.EnableAndroidAppInstall;
            int version = m_AppInstallVersion;
            if (!enabled) return false;
#if UNITY_ANDROID && !UNITY_EDITOR
            await UniTask.SwitchToMainThread(ct);
            EnsureInitialized();
            if (version != m_AppInstallVersion) throw new OperationCanceledException();
            using var bridge = new AndroidJavaClass(c_AppInstallerBridgeClass);
            using AndroidJavaObject activity = GetCurrentActivity();
            return bridge.CallStatic<bool>("canRequestPackageInstalls", activity);
#else
            return false;
#endif
        }

        /// <summary>显式打开此应用来源安装许可设置，不在初始化或安装失败时自动弹出。</summary>
        /// <returns>是否成功发起精准设置跳转。</returns>
        public async UniTask<bool> OpenPackageInstallSettingsAsync()
        {
            EnsureInitialized();
            bool enabled = m_Config.EnableAndroidAppInstall;
            int version = m_AppInstallVersion;
            if (!enabled) return false;
#if UNITY_ANDROID && !UNITY_EDITOR
            await UniTask.SwitchToMainThread();
            EnsureInitialized();
            if (version != m_AppInstallVersion) throw new OperationCanceledException();
            using var bridge = new AndroidJavaClass(c_AppInstallerBridgeClass);
            using AndroidJavaObject activity = GetCurrentActivity();
            return bridge.CallStatic<bool>("openPackageInstallSettings", activity);
#else
            return false;
#endif
        }

        /// <summary>显式启动自身 APK 安装；冻结路径与令牌，拒同 Manager 并发，取消/关闭清理未完成 session。</summary>
        /// <param name="apkPath">persistentDataPath 内既有文件绝对路径；签名及身份在原生暂存件上另验。</param>
        /// <param name="ct">当前安装取消令牌；已成功安装不能回滚。</param>
        /// <returns>能力门或系统最终状态，不保证系统确认界面发起后安装成功。</returns>
        public async UniTask<NativeAppInstallResult> InstallApkAsync(string apkPath, CancellationToken ct = default)
        {
            EnsureInitialized(); ct.ThrowIfCancellationRequested();
            bool enabled = m_Config.EnableAndroidAppInstall;
            int version = m_AppInstallVersion;
            if (!enabled) return new NativeAppInstallResult(NativeAppInstallStatus.Disabled);
#if UNITY_ANDROID && !UNITY_EDITOR
            await UniTask.SwitchToMainThread(ct);
            EnsureInitialized();
            if (version != m_AppInstallVersion) throw new OperationCanceledException();
            if (m_AppInstallCancellation != null) throw new InvalidOperationException("已有 APK 安装操作正在进行。");
            string path = NativeApkInstallPath.Validate(Application.persistentDataPath, apkPath);
            var operation = CancellationTokenSource.CreateLinkedTokenSource(ct);
            m_AppInstallCancellation = operation;
            try { return await InstallApkAndroidAsync(path, operation.Token); }
            finally
            {
                if (ReferenceEquals(m_AppInstallCancellation, operation)) m_AppInstallCancellation = null;
                operation.Dispose();
            }
#else
            return new NativeAppInstallResult(NativeAppInstallStatus.Unsupported);
#endif
        }

        /// <summary>Manager 重新初始化或关闭取消本次等待；Android 桥接另负责放弃 session 和临时件。</summary>
        private void CancelAppInstall() { m_AppInstallVersion++; m_AppInstallCancellation?.Cancel(); }
    }
}
