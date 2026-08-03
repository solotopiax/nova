/***************************************************************
 * (c) copyright 2026 - 2030, Solotopia
 * All Rights Reserved.
 * -------------------------------------------------------------
 * filename:  HttpSettings.cs
 * author:    taoye
 * created:   2026/3/11
 * descrip:   HTTP 管理器配置
 ***************************************************************/

using System;

namespace NovaFramework.Runtime
{
    /// <summary>
    /// HTTP 管理器配置，包含 BestHTTP 网络埋点开关以及连接与请求超时时间。
    /// </summary>
    [Serializable]
    public class HttpSettings
    {
        /// <summary>
        /// 是否将 Best HTTP 产生的结构化网络遥测转发到 Nova 通用埋点插件。
        /// 仅在安装了支持遥测契约的 Best HTTP 商业库时生效。
        /// </summary>
        public bool EnableBestHttpTelemetry = true;

        /// <summary>
        /// HTTP 连接超时时间（秒）。
        /// </summary>
        public float ConnectTimeout = 20f;

        /// <summary>
        /// HTTP 请求超时时间（秒）。
        /// </summary>
        public float RequestTimeout = 60f;
    }
}
