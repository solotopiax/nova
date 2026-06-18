/***************************************************************
 * (c) copyright 2026 - 2030, Solotopia
 * All Rights Reserved.
 * -------------------------------------------------------------
 * filename:  DevelopMode.cs
 * author:    taoye
 * created:   2026/4/30
 * descrip:   开发模式类型
 ***************************************************************/

using System;

namespace NovaFramework.Runtime
{
    /// <summary>
    /// 开发模式类型。
    /// 用于区分当前运行环境为开发调试还是正式发布，
    /// 与 PlatformType、ChannelType 共同构成配置三维索引。
    /// </summary>
    [Serializable]
    public enum DevelopMode
    {
        /// <summary>
        /// 开发/调试环境。
        /// </summary>
        Debug,

        /// <summary>
        /// 正式发布环境。
        /// </summary>
        Release,
    }
}
