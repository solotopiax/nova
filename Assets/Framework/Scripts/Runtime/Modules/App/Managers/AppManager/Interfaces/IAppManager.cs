/***************************************************************
 * (c) copyright 2026 - 2030, Solotopia
 * All Rights Reserved.
 * -------------------------------------------------------------
 * filename:  IAppManager.cs
 * author:    taoye
 * created:   2026/5/19
 * descrip:   App 管理器接口
 ***************************************************************/

using System.Threading;
using Cysharp.Threading.Tasks;

namespace NovaFramework.Runtime
{
    /// <summary>
    /// App 管理器接口：版本检查、强更弹窗、跳商店、下载地址路由。
    /// </summary>
    public interface IAppManager
    {
        /// <summary>
        /// 用配置初始化（业务侧调用）。
        /// </summary>
        /// <param name="config">AppManager 配置。</param>
        void Initialize(AppManagerConfig config);

        /// <summary>
        /// 检查 App 大版本，返回 NoDownload / RecommendedDownload / ForcedDownload。
        /// 网络异常或 JSON 解析失败时宽容降级，返回 NoDownload。
        /// </summary>
        /// <param name="ct">取消令牌。</param>
        /// <returns>App 版本检查结果。</returns>
        UniTask<AppVersionResult> CheckAsync(CancellationToken ct);

        /// <summary>
        /// 触发 App 下载（DownloadRoute=Apk 时）。
        /// 内部根据 AppManagerConfig 选择主备地址，调用 IHttpManager 完成下载，
        /// 写入 persistentDataPath 后由业务层决定是否安装。
        /// 仅当上次 CheckAsync 返回 ForcedDownload 或 RecommendedDownload 时调用有效。
        /// </summary>
        /// <param name="ct">取消令牌。</param>
        /// <returns>下载完成的 apk 文件本地绝对路径；失败时 throw。</returns>
        UniTask<string> DownloadAsync(CancellationToken ct);

        /// <summary>
        /// 跳转到应用商店（DownloadRoute=Store 时）。
        /// 内部使用 Util.AppStore 根据当前平台与 ChannelType 解析商店 deeplink，
        /// 跳转后由系统接管。
        /// 仅当上次 CheckAsync 返回 ForcedDownload 或 RecommendedDownload 时调用有效。
        /// </summary>
        /// <param name="ct">取消令牌。</param>
        /// <returns>true 表示已成功打开商店；false 表示跳转失败。</returns>
        UniTask<bool> OpenStoreAsync(CancellationToken ct);

        /// <summary>
        /// 命中的更新规则。
        /// </summary>
        AppDownloadRule MatchedRule { get; }

        /// <summary>
        /// 强更场景需要跳转的商店地址（按平台从 AppStoreUrl / AndroidStoreUrl 解析），仅用于商店跳转。
        /// </summary>
        string TargetStoreUrl { get; }

        /// <summary>
        /// 强更场景使用的 APK 主下载地址（仅在 DownloadRoute=Apk 时写入），仅用于 APK 下载。
        /// </summary>
        string TargetDownloadUrl { get; }

    }
}
