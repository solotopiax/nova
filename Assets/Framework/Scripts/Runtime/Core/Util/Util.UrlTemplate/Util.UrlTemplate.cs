/***************************************************************
 * (c) copyright 2026 - 2030, Solotopia
 * All Rights Reserved.
 * -------------------------------------------------------------
 * filename:  Util.UrlTemplate.cs
 * author:    Codex
 * created:   2026/7/24
 * descrip:   启动期远端 URL 模板解析工具
 ***************************************************************/

namespace NovaFramework.Runtime
{
    public static partial class Util
    {
        /// <summary>
        /// 启动期远端 URL 模板解析工具。
        /// </summary>
        internal static class UrlTemplate
        {
            /// <summary>
            /// 替换平台、渠道、包名与应用版本占位符；未知占位符保持原样。
            /// </summary>
            internal static string Resolve(
                string template,
                PlatformType platform,
                ChannelType channel,
                string package,
                string version)
            {
                if (template == null)
                {
                    return null;
                }

                return template
                    .Replace("{Platform}", platform.ToString())
                    .Replace("{Channel}", channel.ToString())
                    .Replace("{Package}", package ?? string.Empty)
                    .Replace("{Version}", version ?? string.Empty);
            }

            /// <summary>
            /// 通过编译宏解析启动期平台，不依赖 ConfigRuntimeSO。
            /// </summary>
            internal static PlatformType ResolveRuntimePlatform()
            {
#if UNITY_ANDROID
                return PlatformType.Android;
#elif UNITY_IOS
                return PlatformType.iOS;
#elif UNITY_WEBGL
                return PlatformType.WebGL;
#else
                return PlatformType.None;
#endif
            }
        }
    }
}
