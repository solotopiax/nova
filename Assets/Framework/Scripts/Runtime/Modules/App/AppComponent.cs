/***************************************************************
 * (c) copyright 2026 - 2030, Solotopia
 * All Rights Reserved.
 * -------------------------------------------------------------
 * filename:  AppComponent.cs
 * author:    taoye
 * created:   2026/5/19
 * descrip:   App 组件
 ***************************************************************/

using System;
using System.Threading;
using Cysharp.Threading.Tasks;
using UnityEngine;

namespace NovaFramework.Runtime
{
    /// <summary>
    /// App 组件。
    /// 桥接 IAppManager，向 Procedure 层暴露大版本检查、APK 下载、跳商店与启动配置加载能力。
    /// </summary>
    [DisallowMultipleComponent]
    public sealed partial class AppComponent : FrameworkComponent
    {
        /// <summary>
        /// 唤醒：反射创建 IAppManager。
        /// </summary>
        protected override void Awake()
        {
            base.Awake();
            m_AppManager = Util.TypeCreator.Create<IAppManager>(m_CurManagerTypeName);
            if (m_AppManager == null)
            {
                throw new InvalidOperationException("AppManager 无效。");
            }
        }

        /// <summary>
        /// Start：初始化 Manager（Awake 已完成，可安全获取跨组件依赖）。
        /// </summary>
        private void Start()
        {
            m_AppManager.Initialize(new AppManagerConfig
            {
                AppDownloadCheckUrl = ResolvePrimaryCheckUrl(),
                AppDownloadCheckUrlFallback = ResolveFallbackCheckUrl(),
                TimeoutSeconds = m_TimeoutSeconds,
                DownloadRoute = m_DownloadRoute,
                PrimaryDownloadUrl = m_PrimaryDownloadUrl,
                FallbackDownloadUrl = m_FallbackDownloadUrl,
                AndroidStoreUrl = m_AndroidStoreUrl,
                AppStoreUrl = m_AppStoreUrl,
                UseRecommendedDownloadRule = m_UseRecommendedDownloadRule,
                UseForcedDownloadRule = m_UseForcedDownloadRule,
            });
        }

        /// <summary>
        /// 根据当前场景快照中的开发模式，解析当前应使用的主版本检查地址。
        /// </summary>
        private string ResolvePrimaryCheckUrl()
        {
            return DevelopMode == DevelopMode.Debug
                ? m_AppDownloadCheckUrlDebug
                : m_AppDownloadCheckUrlRelease;
        }

        /// <summary>
        /// 根据当前场景快照中的开发模式，解析当前应使用的备用版本检查地址。
        /// </summary>
        private string ResolveFallbackCheckUrl()
        {
            return DevelopMode == DevelopMode.Debug
                ? m_AppDownloadCheckUrlFallbackDebug
                : m_AppDownloadCheckUrlFallbackRelease;
        }

        /// <summary>
        /// 检查 App 大版本，转发至 IAppManager.CheckAsync。
        /// </summary>
        /// <param name="ct">取消令牌。</param>
        /// <returns>App 版本检查结果。</returns>
        public UniTask<AppVersionResult> CheckAsync(CancellationToken ct = default)
        {
            return m_AppManager.CheckAsync(ct);
        }

        /// <summary>
        /// 触发 APK 下载，转发至 IAppManager.DownloadAsync。
        /// </summary>
        /// <param name="ct">取消令牌。</param>
        /// <returns>下载完成的 APK 文件本地绝对路径；失败时 throw。</returns>
        public UniTask<string> DownloadAsync(CancellationToken ct = default)
        {
            return m_AppManager.DownloadAsync(ct);
        }

        /// <summary>
        /// 跳转应用商店，转发至 IAppManager.OpenStoreAsync。
        /// </summary>
        /// <param name="ct">取消令牌。</param>
        /// <returns>true 表示已成功打开商店；false 表示跳转失败。</returns>
        public UniTask<bool> OpenStoreAsync(CancellationToken ct = default)
        {
            return m_AppManager.OpenStoreAsync(ct);
        }
    }
}
