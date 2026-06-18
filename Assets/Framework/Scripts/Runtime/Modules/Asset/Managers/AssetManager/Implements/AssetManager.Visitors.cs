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
    }
}
