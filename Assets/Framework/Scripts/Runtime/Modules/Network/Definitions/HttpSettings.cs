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
    /// HTTP 管理器配置，包含连接与请求的超时时间。
    /// </summary>
    [Serializable]
    public class HttpSettings
    {
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
