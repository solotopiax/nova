/***************************************************************
 * (c) copyright 2026 - 2030, Solotopia
 * All Rights Reserved.
 * -------------------------------------------------------------
 * filename:  AdPluginConfig.cs
 * author:    yingzheng
 * created:   2026/5/14
 * descrip:   AdPlugin 配置，声明启用的 IAdInternalPlugin 渠道列表
 ***************************************************************/

using System;
using NovaFramework.Runtime;
using UnityEngine;

namespace NovaFramework.SDK.AdPlugin.Runtime
{
    /// <summary>
    /// AdPlugin 配置，由 SDKManager 注入到 AdPlugin.OnInitializeAsync。
    /// ChannelConfigs 存储所有渠道的配置实例（含启用状态），
    /// AdPlugin 初始化时遍历此列表，按 IAdChannelConfig.PluginType 反射创建渠道实例。
    /// </summary>
    [Serializable]
    public sealed class AdPluginConfig : ISDKPluginConfig
    {
        /// <summary>
        /// 配置项在 ConfigWindow 左树中展示的中文名称，不可为空。
        /// </summary>
        public string DisplayName => "广告聚合";

        /// <summary>
        /// 渠道配置列表；每个元素对应一个渠道，存储该渠道的参数与启用状态，
        /// 同时承载竞价模式 / Banner ILRD 间隔 / 静音 / 加载重试等全局开关。
        /// AdPlugin 初始化时遍历此列表，按 IAdChannelConfig.PluginType 反射创建渠道实例。
        /// </summary>
        [SerializeField, Tooltip("广告渠道配置列表，每个渠道含启用状态、竞价开关、Banner ILRD 间隔、静音及加载重试等参数。")]
        private AdChannelConfigList m_ChannelConfigs = new AdChannelConfigList();

        /// <summary>
        /// 渠道配置访问入口，AdPlugin 通过 Items 遍历渠道，通过其余只读属性读取全局开关。
        /// </summary>
        public AdChannelConfigList ChannelConfigs => m_ChannelConfigs;

        /// <summary>
        /// 无参构造器；供 ConfigWindow SDKPluginScanner 通过 Activator 创建空实例使用。
        /// </summary>
        public AdPluginConfig() { }

    }
}
