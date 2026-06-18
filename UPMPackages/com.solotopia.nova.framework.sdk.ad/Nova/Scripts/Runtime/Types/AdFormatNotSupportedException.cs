/***************************************************************
 * (c) copyright 2026 - 2030, Solotopia
 * All Rights Reserved.
 * -------------------------------------------------------------
 * filename:  AdFormatNotSupportedException.cs
 * author:    taoye
 * created:   2026/4/28
 * descrip:   广告格式不支持异常
 ***************************************************************/

using System;
using NovaFramework.Runtime;

namespace NovaFramework.SDK.AdPlugin.Runtime
{
    /// <summary>
    /// 广告格式不支持异常。
    /// 派生渠道 OnRequestAsync / OnShowAsync 内部 switch 落入 default 分支时抛出，
    /// 用于诊断"基类已注册槽位但派生类未实现该 format 处理"的不一致情况；正常调用路径不抛此异常。
    /// </summary>
    public sealed class AdFormatNotSupportedException : Exception
    {
        /// <summary>
        /// 触发异常的 Plugin 友好名称（ISDKPlugin.Name）。
        /// </summary>
        public string PluginName { get; }

        /// <summary>
        /// 不受支持的广告格式。
        /// </summary>
        public AdFormat Format { get; }

        /// <summary>
        /// 使用 Plugin 名称和广告格式构造异常，消息由框架统一格式化。
        /// </summary>
        /// <param name="pluginName">插件友好名称，用于错误消息。</param>
        /// <param name="format">不受支持的广告格式枚举值。</param>
        public AdFormatNotSupportedException(string pluginName, AdFormat format)
            : base(Txt.Format("AdPlugin '{0}' 不支持广告格式 '{1}'。", pluginName, format))
        {
            PluginName = pluginName;
            Format = format;
        }

        /// <summary>
        /// 使用 Plugin 名称、广告格式和内部异常构造，保留原始调用链。
        /// </summary>
        /// <param name="pluginName">插件友好名称，用于错误消息。</param>
        /// <param name="format">不受支持的广告格式枚举值。</param>
        /// <param name="inner">引发当前异常的原始异常，可为 null。</param>
        public AdFormatNotSupportedException(string pluginName, AdFormat format, Exception inner)
            : base(Txt.Format("AdPlugin '{0}' 不支持广告格式 '{1}'。", pluginName, format), inner)
        {
            PluginName = pluginName;
            Format = format;
        }
    }
}
