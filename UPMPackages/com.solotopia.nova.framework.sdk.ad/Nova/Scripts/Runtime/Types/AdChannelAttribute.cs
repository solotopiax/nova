/***************************************************************
 * (c) copyright 2026 - 2030, Solotopia
 * All Rights Reserved.
 * -------------------------------------------------------------
 * filename:  AdChannelAttribute.cs
 * author:    yingzheng
 * created:   2026/5/14
 * descrip:   标注广告渠道插件类与其配置类型的绑定关系
 ***************************************************************/

using System;
using NovaFramework.Runtime;

namespace NovaFramework.SDK.AdPlugin.Runtime
{
    /// <summary>
    /// 标注在 AdChannelPluginBase 派生类上，声明该渠道插件对应的配置类型。
    /// AdPlugin 通过此 Attribute 在 IAdChannelConfig 列表中找到匹配的配置实例，
    /// 再按 IAdChannelConfig.PluginType 反射创建渠道实例。
    /// </summary>
    [AttributeUsage(AttributeTargets.Class, Inherited = false, AllowMultiple = false)]
    public sealed class AdChannelAttribute : Attribute
    {
        /// <summary>
        /// 与该渠道插件绑定的配置类型；必须实现 IAdChannelConfig。
        /// </summary>
        public Type ConfigType { get; }

        /// <summary>
        /// 构造函数，绑定配置类型。
        /// </summary>
        /// <param name="configType">实现 IAdChannelConfig 的配置类型。</param>
        public AdChannelAttribute(Type configType) => ConfigType = configType;
    }
}
