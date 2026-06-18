/***************************************************************
 * (c) copyright 2026 - 2030, Solotopia
 * All Rights Reserved.
 * -------------------------------------------------------------
 * filename:  SDKUnavailableException.cs
 * author:    taoye
 * created:   2026/4/28
 * descrip:   SDK 插件不可用异常
 ***************************************************************/

using System;

namespace NovaFramework.Runtime
{
    /// <summary>
    /// SDK 插件不可用异常。
    /// 当 ISDKManager.Get 请求的 Plugin 处于以下任意状态时抛出：未启用、初始化失败、未注册。
    /// 调用方应使用 TryGet 做防御性查询，或确认 IsInitialized=true 且插件可用后再调用 Get。
    /// </summary>
    public sealed class SDKUnavailableException : Exception
    {
        /// <summary>
        /// 触发异常的 Plugin 具体类型。
        /// </summary>
        public Type PluginType { get; }

        /// <summary>
        /// 使用 Plugin 类型构造异常，消息由框架统一格式化。
        /// </summary>
        /// <param name="pluginType">不可用的 Plugin 具体类型，不可为 null。</param>
        public SDKUnavailableException(Type pluginType)
            : base(Txt.Format("SDK 插件 '{0}' 不可用（未启用/初始化失败/未注册）。", pluginType.FullName))
        {
            PluginType = pluginType;
        }

        /// <summary>
        /// 使用 Plugin 类型和内部异常构造，用于包装底层异常时保留原始调用链。
        /// </summary>
        /// <param name="pluginType">不可用的 Plugin 具体类型，不可为 null。</param>
        /// <param name="inner">引发当前异常的原始异常，可为 null。</param>
        public SDKUnavailableException(Type pluginType, Exception inner)
            : base(Txt.Format("SDK 插件 '{0}' 不可用（未启用/初始化失败/未注册）。", pluginType.FullName), inner)
        {
            PluginType = pluginType;
        }
    }
}
