/***************************************************************
 * (c) copyright 2026 - 2030, Solotopia
 * All Rights Reserved.
 * -------------------------------------------------------------
 * filename:  HttpManager.Visitors.cs
 * author:    taoye
 * created:   2026/3/9
 * descrip:   HTTP管理器 —— 属性与字段
 ***************************************************************/

namespace NovaFramework.Runtime
{
    /// <summary>
    /// HTTP 管理器。
    /// </summary>
    internal sealed partial class HttpManager : HttpManagerBase
    {
        /// <summary>
        /// DoH 管理器接口，由初始化配置注入，用于 IP 解析。
        /// </summary>
        private IDoHManager m_DoHManager;

        /// <summary>
        /// HTTP 传输实现，由传输注册表创建。
        /// </summary>
        private IHttpTransport m_Transport;

        /// <summary>
        /// 默认网络连接超时时间（秒）。
        /// </summary>
        private float m_ConnectTimeout = 20f;

        /// <summary>
        /// 默认网络请求超时时间（秒）。
        /// </summary>
        private float m_RequestTimeout = 60f;

    }
}
