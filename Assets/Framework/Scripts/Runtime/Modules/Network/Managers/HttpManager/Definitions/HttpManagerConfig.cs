/***************************************************************
 * (c) copyright 2026 - 2030, Solotopia
 * All Rights Reserved.
 * -------------------------------------------------------------
 * filename:  HttpManagerConfig.cs
 * author:    taoye
 * created:   2026/3/9
 * descrip:   HTTP管理器配置
 ***************************************************************/

namespace NovaFramework.Runtime
{
    /// <summary>
    /// HTTP 管理器配置。
    /// </summary>
    public class HttpManagerConfig
    {
        /// <summary>
        /// 默认网络连接超时时间（秒），默认 20 秒。
        /// </summary>
        public float ConnectTimeout = 20f;

        /// <summary>
        /// 默认网络请求超时时间（秒），默认 60 秒。
        /// </summary>
        public float RequestTimeout = 60f;

        /// <summary>
        /// DoH 管理器接口引用，由 NetworkComponent 注入，用于 IP 解析（若平台支持）。
        /// </summary>
        public IDoHManager DoHManager;
    }
}
