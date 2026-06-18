/***************************************************************
 * (c) copyright 2026 - 2030, Solotopia
 * All Rights Reserved.
 * -------------------------------------------------------------
 * filename:  AppManagerBase.cs
 * author:    taoye
 * created:   2026/5/19
 * descrip:   App 管理器基类
 ***************************************************************/

using System.Threading;
using Cysharp.Threading.Tasks;

namespace NovaFramework.Runtime
{
    /// <summary>
    /// App 管理器基类。
    /// 继承 FrameworkManager，声明全部 abstract 成员，不写业务逻辑。
    /// Priority = 11，在 HotfixManager（Priority=13）之前初始化，保证版本检查早于热更。
    /// </summary>
    internal abstract class AppManagerBase : FrameworkManager, IAppManager
    {
        /// <summary>
        /// 管理器优先级（值越小越先 Update、越后 Shutdown）。
        /// </summary>
        public override int Priority => 11;

        /// <summary>
        /// 用配置初始化。
        /// </summary>
        /// <param name="config">AppManager 配置。</param>
        public abstract void Initialize(AppManagerConfig config);

        /// <summary>
        /// 管理器轮询。
        /// </summary>
        public abstract override void Update();

        /// <summary>
        /// 关闭并清理管理器。
        /// </summary>
        public abstract override void Shutdown();

        /// <summary>
        /// 检查 App 大版本，返回 NoDownload / RecommendedDownload / ForcedDownload。
        /// </summary>
        /// <param name="ct">取消令牌。</param>
        /// <returns>App 版本检查结果枚举值。</returns>
        public abstract UniTask<AppVersionResult> CheckAsync(CancellationToken ct);

        /// <summary>
        /// 触发 App 下载（DownloadRoute=Apk 时）。
        /// </summary>
        /// <param name="ct">取消令牌。</param>
        /// <returns>下载完成的 apk 文件本地绝对路径；失败时 throw。</returns>
        public abstract UniTask<string> DownloadAsync(CancellationToken ct);

        /// <summary>
        /// 跳转到应用商店（DownloadRoute=Store 时）。
        /// </summary>
        /// <param name="ct">取消令牌。</param>
        /// <returns>true 表示已成功打开商店；false 表示跳转失败。</returns>
        public abstract UniTask<bool> OpenStoreAsync(CancellationToken ct);

        /// <summary>
        /// 命中的更新规则。
        /// </summary>
        public abstract AppDownloadRule MatchedRule { get; }

        /// <summary>
        /// 强更场景需要跳转的商店地址（按平台从 AppStoreUrl / AndroidStoreUrl 解析），仅用于商店跳转。
        /// </summary>
        public abstract string TargetStoreUrl { get; }

        /// <summary>
        /// 强更场景使用的 APK 主下载地址（仅在 DownloadRoute=Apk 时写入），仅用于 APK 下载。
        /// </summary>
        public abstract string TargetDownloadUrl { get; }

    }
}
