/***************************************************************
 * (c) copyright 2021 - 2025, Solotopia
 * All Rights Reserved.
 * -------------------------------------------------------------
 * filename:  Path.cs
 * author:    taoye
 * created:   2021/6/16
 * descrip:   路径工具 —— 平台标识
 ***************************************************************/

using UnityEngine;

namespace NovaFramework.Runtime
{
    /// <summary>
    /// 框架路径工具，各功能域路径定义于对应的 partial 文件中。
    /// </summary>
    public static partial class Path
    {
        /// <summary>
        /// 当前构建平台名称（iOS / Android / WebGL），与 AB 目录结构对应。
        /// </summary>
#if UNITY_IOS
        public static readonly string PlatformName = "iOS";
#elif UNITY_WEBGL
        public static readonly string PlatformName = "WebGL";
#else
        public static readonly string PlatformName = "Android";
#endif

        /// <summary>
        /// 将路径中的反斜杠替换为正斜杠。
        /// </summary>
        /// <param name="path">待规范化的路径。</param>
        /// <returns>规范化后的路径。</returns>
        internal static string NormalizeSeparator(string path)
        {
            return path?.Replace('\\', '/');
        }
    }
}
