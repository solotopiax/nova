/***************************************************************
 * (c) copyright 2026 - 2030, Solotopia
 * All Rights Reserved.
 * -------------------------------------------------------------
 * filename:  AppManager.Methods.cs
 * author:    taoye
 * created:   2026/5/19
 * descrip:   App 管理器 —— 私有方法
 ***************************************************************/

using System;
using System.Threading;
using Cysharp.Threading.Tasks;

namespace NovaFramework.Runtime
{
    /// <summary>
    /// App 管理器。
    /// </summary>
    internal sealed partial class AppManager : AppManagerBase
    {
        /// <summary>
        /// 执行版本检查核心逻辑：HTTP GET 拉取 CDN 版本配置 JSON → 解析推荐 / 强制两个规则阈值 → 写入结果字段。
        /// HTTP 失败降级返回 NoDownload；JSON 解析异常降级返回 NoDownload。
        /// </summary>
        /// <param name="ct">取消令牌。</param>
        /// <returns>App 版本检查结果枚举值。</returns>
        private async UniTask<AppVersionResult> InnerCheckVersionAsync(CancellationToken ct)
        {
            string body = await TryGetVersionResponseBodyAsync(m_Config.AppDownloadCheckUrl, "主", ct);
            if (string.IsNullOrEmpty(body))
            {
                body = await TryGetVersionResponseBodyAsync(m_Config.AppDownloadCheckUrlFallback, "备用", ct);
            }

            if (string.IsNullOrEmpty(body))
            {
                return AppVersionResult.NoDownload;
            }

            return ParseVersionResult(body);
        }

        /// <summary>
        /// 读取单个版本检查地址的响应内容；地址为空、请求失败、超时或返回空内容时均返回 null。
        /// </summary>
        private async UniTask<string> TryGetVersionResponseBodyAsync(string url, string sourceLabel, CancellationToken ct)
        {
            if (!IsValidUrl(url))
            {
                Log.Warning(LogTag.App, "{0}版本检查地址未配置，跳过。", sourceLabel);
                return null;
            }

            HttpResponse response = null;
            try
            {
                response = await m_HttpManager.GetAsync(url, m_Config.TimeoutSeconds);
                if (response == null || !response.IsSuccess)
                {
                    Log.Warning(LogTag.App, "{0}版本检查接口请求失败：{1}", sourceLabel, response?.Error);
                    return null;
                }

                if (string.IsNullOrWhiteSpace(response.Body))
                {
                    Log.Warning(LogTag.App, "{0}版本检查接口返回内容为空。", sourceLabel);
                    return null;
                }

                return response.Body;
            }
            catch (OperationCanceledException) when (!ct.IsCancellationRequested)
            {
                Log.Warning(LogTag.App, "{0}版本检查接口请求超时，准备尝试下一个地址。", sourceLabel);
                return null;
            }
            catch (Exception ex)
            {
                Log.Warning(LogTag.App, "{0}版本检查接口请求异常，准备尝试下一个地址：{1}", sourceLabel, ex.Message);
                return null;
            }
            finally
            {
                if (response != null)
                {
                    ReferencePool.Put(response);
                }
            }
        }

        /// <summary>
        /// 解析服务端版本响应 JSON，返回 AppVersionResult。
        /// 优先级：强制更新规则 > 推荐更新规则。
        /// 命中强制规则返回 ForcedDownload；命中推荐规则返回 RecommendedDownload；其余返回 NoDownload。
        /// JSON 解析异常时降级返回 NoDownload 并 Log.Error。
        /// </summary>
        /// <param name="json">服务端返回的版本配置 JSON 文本。</param>
        /// <returns>App 版本检查结果枚举值。</returns>
        private AppVersionResult ParseVersionResult(string json)
        {
            try
            {
                AppVersionResponse resp = Util.Json.Deserialize<AppVersionResponse>(json);
                if (resp == null)
                {
                    Log.Error(LogTag.App, "大版本检查 JSON 解析结果为 null。");
                    return AppVersionResult.NoDownload;
                }

                ResetMatchedRuleState();

                string localVersion = UnityEngine.Application.version;

                if (m_Config.UseForcedDownloadRule && IsVersionGreater(resp.ForcedDownloadVersion, localVersion))
                {
                    ApplyMatchedRule(AppDownloadRule.Forced);
                    return AppVersionResult.ForcedDownload;
                }

                if (m_Config.UseRecommendedDownloadRule && IsVersionGreater(resp.RecommendedDownloadVersion, localVersion))
                {
                    ApplyMatchedRule(AppDownloadRule.Recommended);
                    return AppVersionResult.RecommendedDownload;
                }

                return AppVersionResult.NoDownload;
            }
            catch (System.Exception ex)
            {
                Log.Error(LogTag.App, "大版本检查 JSON 解析异常，降级返回 NoDownload：{0}", ex.Message);
                return AppVersionResult.NoDownload;
            }
        }

        /// <summary>
        /// 命中更新规则后，统一回写状态和目标地址。
        /// </summary>
        /// <param name="rule">命中的规则。</param>
        private void ApplyMatchedRule(AppDownloadRule rule)
        {
            m_MatchedRule = rule;
            ApplyRouteTargets();
        }

        /// <summary>
        /// 根据当前下载路由，仅解析当前流程真正需要的目标地址。
        /// Store 只解析当前平台商店地址；Apk 只解析主下载地址。
        /// </summary>
        private void ApplyRouteTargets()
        {
            m_TargetStoreUrl = null;
            m_TargetDownloadUrl = null;

            switch (m_Config.DownloadRoute)
            {
                case AppDownloadRoute.Store:
                    m_TargetStoreUrl = ResolveStoreUrl();
                    break;
                case AppDownloadRoute.Apk:
                    m_TargetDownloadUrl = ResolvePrimaryDownloadUrl();
                    break;
                default:
                    Log.Warning(LogTag.App, "未知的 DownloadRoute：{0}", m_Config.DownloadRoute);
                    break;
            }
        }

        /// <summary>
        /// 清空上一次检查结果，避免 NoDownload 或失败路径残留旧状态。
        /// </summary>
        private void ResetMatchedRuleState()
        {
            m_MatchedRule = AppDownloadRule.None;
            m_TargetStoreUrl = null;
            m_TargetDownloadUrl = null;
        }

        /// <summary>
        /// 比较两个版本号，返回正数表示 remote > local，0 表示相等，负数表示不命中或 remote < local。
        /// 仅接受 System.Version 可解析的纯数字点分版本号。
        /// </summary>
        /// <param name="remote">远端版本号。</param>
        /// <param name="local">本地版本号。</param>
        /// <returns>比较结果。</returns>
        private static int CompareVersions(string remote, string local)
        {
            bool hasRemote = TryParseVersion(remote, "远端", out Version remoteVersion);
            bool hasLocal = TryParseVersion(local, "本地", out Version localVersion);
            if (!hasRemote && !hasLocal)
            {
                return 0;
            }

            if (!hasRemote || !hasLocal)
            {
                return -1;
            }

            return remoteVersion.CompareTo(localVersion);
        }

        /// <summary>
        /// 判断远端版本是否高于本地版本。
        /// </summary>
        /// <param name="remote">远端版本号。</param>
        /// <param name="local">本地版本号。</param>
        /// <returns>远端更高时返回 true。</returns>
        private static bool IsVersionGreater(string remote, string local)
        {
            return CompareVersions(remote, local) > 0;
        }

        /// <summary>
        /// 解析 System.Version 格式的版本号。
        /// </summary>
        /// <param name="version">待解析的版本字符串。</param>
        /// <param name="source">版本来源，用于日志定位。</param>
        /// <param name="parsedVersion">解析结果。</param>
        /// <returns>解析成功返回 true。</returns>
        private static bool TryParseVersion(string version, string source, out Version parsedVersion)
        {
            if (string.IsNullOrWhiteSpace(version))
            {
                Log.Warning(LogTag.App, "{0}版本号为空，跳过更新比较。", source);
                parsedVersion = null;
                return false;
            }

            if (Version.TryParse(version, out parsedVersion))
            {
                return true;
            }

            Log.Warning(LogTag.App, "{0}版本号格式非法，需为纯数字点分格式：{1}", source, version);
            return false;
        }

        /// <summary>
        /// 按当前平台从 AppManagerConfig 解析商店跳转地址（iOS 取 AppStoreUrl，其余平台取 AndroidStoreUrl）。
        /// </summary>
        /// <returns>当前平台的商店跳转地址；未配置时返回 string.Empty。</returns>
        private string ResolveStoreUrl()
        {
#if UNITY_IOS
            string storeUrl = m_Config.AppStoreUrl;
#else
            string storeUrl = m_Config.AndroidStoreUrl;
#endif
            if (string.IsNullOrEmpty(storeUrl))
            {
                Log.Error(LogTag.App, "当前平台商店地址未配置。");
                return string.Empty;
            }

            return storeUrl;
        }

        /// <summary>
        /// 解析 APK 主下载地址。
        /// 启动期只校验主下载地址；备用下载地址留给后续下载实现自行决定是否回退。
        /// </summary>
        /// <returns>主下载地址；未配置时返回 null。</returns>
        private string ResolvePrimaryDownloadUrl()
        {
            if (!string.IsNullOrEmpty(m_Config.PrimaryDownloadUrl))
            {
                return m_Config.PrimaryDownloadUrl;
            }

            Log.Error(LogTag.App, "DownloadRoute=Apk，但 PrimaryDownloadUrl 未配置。");
            return null;
        }

        /// <summary>
        /// 检查候选 URL 是否有效（非 null、非空白）。
        /// </summary>
        /// <param name="url">待检查的地址字符串。</param>
        /// <returns>有效时返回 true。</returns>
        private bool IsValidUrl(string url)
        {
            return !string.IsNullOrWhiteSpace(url);
        }
    }
}
