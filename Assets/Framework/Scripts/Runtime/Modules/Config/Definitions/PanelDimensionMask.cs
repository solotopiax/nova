/***************************************************************
 * (c) copyright 2026 - 2030, Solotopia
 * All Rights Reserved.
 * -------------------------------------------------------------
 * filename:  PanelDimensionMask.cs
 * author:    taoye
 * created:   2026/6/1
 * descrip:   ConfigWindow 配置面板的维度启用掩码数据结构
 ***************************************************************/

using System;

namespace NovaFramework.Runtime
{
    /// <summary>
    /// ConfigWindow 单个配置面板的维度启用掩码；
    /// 记录该面板是否按 平台类型 / 渠道类型 / 开发模式 分别配置。
    /// 全部为 false 时表示全局唯一配置（不区分任何维度）。
    /// </summary>
    [Serializable]
    public sealed class PanelDimensionMask
    {
        /// <summary>
        /// 是否按平台类型分别配置；勾选后该面板数据随 PlatformType 维度独立存储。
        /// </summary>
        public bool ByPlatform;

        /// <summary>
        /// 是否按渠道类型分别配置；勾选后该面板数据随 ChannelType 维度独立存储。
        /// </summary>
        public bool ByChannel;

        /// <summary>
        /// 是否按开发模式分别配置；勾选后该面板数据随 DevelopMode 维度独立存储。
        /// </summary>
        public bool ByDevelopMode;

        /// <summary>
        /// 是否为全局唯一配置；当 ByPlatform、ByChannel、ByDevelopMode 均为 false 时返回 true，
        /// 表示该面板在所有维度下共享同一份配置数据。
        /// </summary>
        public bool IsGlobal => !ByPlatform && !ByChannel && !ByDevelopMode;
    }
}
