/***************************************************************
 * (c) copyright 2026 - 2030, Solotopia
 * All Rights Reserved.
 * -------------------------------------------------------------
 * filename:  AssetManager.Visitors.cs
 * author:    taoye
 * created:   2026/5/14
 * descrip:   AssetManager 字段定义
 ***************************************************************/

using System.Collections.Generic;
using System.Threading;
using YooAsset;

namespace NovaFramework.Runtime
{
    internal sealed partial class AssetManager : AssetManagerBase
    {
        /// <summary>
        /// 已注册的 YooAsset 包字典，键 = 包名。
        /// </summary>
        private readonly Dictionary<string, ResourcePackage> m_Packages = new();

        /// <summary>
        /// 已完成清单加载（LoadManifestAsync 完成）的包名集合，用于幂等判断。
        /// </summary>
        private readonly HashSet<string> m_ManifestLoadedPackages = new();

        /// <summary>
        /// 本次启动通过本地可启动版本、当前清单或内置清单完成离线恢复的包。
        /// 这些包跳过本轮补丁下载，且不得覆盖已有可启动版本记录。
        /// </summary>
        private readonly HashSet<string> m_OfflineRecoveredPackages = new();

        /// <summary>
        /// 当前进程已完成启动白名单检查的包名集合。
        /// </summary>
        private readonly HashSet<string> m_StartupWhitelistCheckedPackages = new();

        /// <summary>
        /// 当前进程已命中启动白名单的包名集合。
        /// </summary>
        private readonly HashSet<string> m_StartupWhitelistMatchedPackages = new();

        /// <summary>
        /// 每个包独立持有的远端 URL 轮换策略。
        /// </summary>
        private readonly Dictionary<string, AssetDownloadUrlPolicy> m_DownloadUrlPolicies = new();

        /// <summary>
        /// AssetManager 配置（Inspector 注入，Initialize 写入）。
        /// </summary>
        private AssetManagerConfig m_Config;

        /// <summary>
        /// 默认包名；从 Config.DefaultPackageName 或 Config.Packages[0] 取，BootstrapAsync 阶段写入。
        /// </summary>
        private string m_DefaultPackageName;

        /// <summary>
        /// AB 解密器实例；按 Config.DecryptorType 在 BootstrapAsync 阶段创建一次。
        /// </summary>
        private object m_Decryptor;

        /// <summary>
        /// Manager 生命周期取消源；Shutdown 时 Cancel，使所有进行中的异步操作尽快退出。
        /// </summary>
        private CancellationTokenSource m_Cts;

        /// <summary>
        /// HTTP 管理器，用于下载启动白名单文件；请求自动受现有 DoH 逻辑覆盖。
        /// </summary>
        private IHttpManager m_HttpManager;
    }
}
