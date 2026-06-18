/***************************************************************
 * (c) copyright 2026 - 2030, Solotopia
 * All Rights Reserved.
 * -------------------------------------------------------------
 * filename:  AppManager.cs
 * author:    taoye
 * created:   2026/5/19
 * descrip:   App 管理器
 ***************************************************************/

using System;
using System.Threading;
using Cysharp.Threading.Tasks;

namespace NovaFramework.Runtime
{
    /// <summary>
    /// App 管理器。
    /// 负责大版本检查、强更弹窗决策、跳商店、下载地址路由。
    /// 通过 FrameworkManagersGroup 获取跨组件依赖，不持有 Component 引用。
    /// </summary>
    internal sealed partial class AppManager : AppManagerBase
    {
        /// <summary>
        /// 反射创建用无参构造器。
        /// </summary>
        public AppManager()
        {
        }

        /// <summary>
        /// 用配置初始化，获取跨组件依赖并写入字段。
        /// </summary>
        /// <param name="config">AppManager 配置，不能为 null。</param>
        public override void Initialize(AppManagerConfig config)
        {
            if (config == null)
            {
                throw new ArgumentNullException(nameof(config), "AppManager 初始化失败：config 不能为 null。");
            }

            m_HttpManager = FrameworkManagersGroup.GetManager<IHttpManager>();
            if (m_HttpManager == null)
            {
                throw new InvalidOperationException("AppManager 初始化失败：IHttpManager 无效。");
            }

            m_Config = config;
        }

        /// <summary>
        /// 管理器轮询（暂无逻辑）。
        /// </summary>
        public override void Update()
        {
        }

        /// <summary>
        /// 关闭并清理管理器，重置所有状态字段。
        /// </summary>
        public override void Shutdown()
        {
            m_HttpManager = null;
            m_Config = null;
            ResetMatchedRuleState();
        }

        /// <summary>
        /// 检查 App 大版本。
        /// 流程：HTTP 拉取版本配置 → 命中规则 → 按 DownloadRoute 写入 TargetStoreUrl 或 TargetDownloadUrl。
        /// 网络/解析异常时宽容降级返回 NoDownload。
        /// </summary>
        /// <param name="ct">取消令牌。</param>
        /// <returns>App 版本检查结果枚举值。</returns>
        public override async UniTask<AppVersionResult> CheckAsync(CancellationToken ct)
        {
            ResetMatchedRuleState();

            if (m_Config == null)
            {
                Log.Warning(LogTag.App, "AppManager 尚未初始化，跳过大版本检查。");
                return AppVersionResult.NoDownload;
            }

            if (!IsValidUrl(m_Config.AppDownloadCheckUrl) && !IsValidUrl(m_Config.AppDownloadCheckUrlFallback))
            {
                Log.Warning(LogTag.App, "AppDownloadCheckUrl 与 AppDownloadCheckUrlFallback 均未配置，跳过大版本检查。");
                return AppVersionResult.NoDownload;
            }

            try
            {
                return await InnerCheckVersionAsync(ct);
            }
            catch (OperationCanceledException)
            {
                Log.Debug(LogTag.App, "CheckAsync 已取消。");
                return AppVersionResult.NoDownload;
            }
            catch (Exception ex)
            {
                Log.Warning(LogTag.App, "CheckAsync 网络异常，降级返回 NoDownload：{0}", ex.Message);
                return AppVersionResult.NoDownload;
            }
        }

        /// <summary>
        /// 命中的更新规则。
        /// </summary>
        public override AppDownloadRule MatchedRule => m_MatchedRule;

        /// <summary>
        /// 强更场景需要跳转的商店地址（按平台从 AppStoreUrl / AndroidStoreUrl 解析）。
        /// </summary>
        public override string TargetStoreUrl => m_TargetStoreUrl;

        /// <summary>
        /// 强更场景使用的 APK 主下载地址（仅在 DownloadRoute=Apk 时写入）。
        /// </summary>
        public override string TargetDownloadUrl => m_TargetDownloadUrl;

    }
}
