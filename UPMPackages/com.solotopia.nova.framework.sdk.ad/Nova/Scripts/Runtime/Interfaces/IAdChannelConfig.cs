/***************************************************************
 * (c) copyright 2026 - 2030, Solotopia
 * All Rights Reserved.
 * -------------------------------------------------------------
 * filename:  IAdChannelConfig.cs
 * author:    yingzheng
 * created:   2026/5/14
 * descrip:   广告渠道配置接口，存储渠道参数并声明对应的渠道插件类型
 ***************************************************************/

using System;
using System.Collections.Generic;
using NovaFramework.Runtime;

namespace NovaFramework.SDK.AdPlugin.Runtime
{
    /// <summary>
    /// 广告渠道配置接口。
    /// 每个渠道包提供一个实现此接口的 [Serializable] 配置类，
    /// AdPluginConfig 以 [SerializeReference] List 存储所有渠道配置，
    /// AdPlugin 初始化时遍历列表，按 PluginType 反射创建渠道实例。
    /// </summary>
    public interface IAdChannelConfig
    {
        /// <summary>
        /// 渠道枚举标识，用于 Inspector 显示和日志识别。
        /// </summary>
        AdChannelType Channel { get; }

        /// <summary>
        /// 对应的渠道插件 C# 类型，AdPlugin 初始化时用于反射创建实例。
        /// </summary>
        Type PluginType { get; }

        /// <summary>
        /// 是否启用此渠道；false 时 AdPlugin 跳过创建。
        /// </summary>
        bool Enabled { get; }

        /// <summary>
        /// 激励视频广告位 ID 列表；空列表表示不启用该格式。
        /// </summary>
        IReadOnlyList<string> RVPlacementIds { get; }

        /// <summary>
        /// 插屏广告位 ID 列表；空列表表示不启用该格式。
        /// </summary>
        IReadOnlyList<string> InterPlacementIds { get; }

        /// <summary>
        /// Banner 广告位 ID 列表；空列表表示不启用该格式。
        /// </summary>
        IReadOnlyList<string> BannerPlacementIds { get; }

        /// <summary>
        /// AppOpen 广告位 ID 列表；空列表表示不启用该格式。
        /// </summary>
        IReadOnlyList<string> AppOpenPlacementIds { get; }
    }
}
