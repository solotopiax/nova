/***************************************************************
 * (c) copyright 2026 - 2030, Solotopia
 * All Rights Reserved.
 * -------------------------------------------------------------
 * filename:  EditorUtil.PlugPals.Visitors.cs
 * author:    taoye
 * created:   2026/4/21
 * descrip:   PlugPals 工具 —— 字段与常量
 ***************************************************************/

using System;
using System.Net.Http;

namespace NovaFramework.Editor
{
    public static partial class EditorUtil
    {
        public static partial class PlugPals
        {
            /// <summary>
            /// 全局复用的 HttpClient 实例，避免 socket 耗尽。
            /// </summary>
            private static readonly HttpClient s_HttpClient = new HttpClient { Timeout = TimeSpan.FromSeconds(15) };

            /// <summary>
            /// 是否已有待执行的 UPM Resolve 请求。
            /// </summary>
            private static bool s_IsResolvePackagesQueued;

            /// <summary>
            /// manifest.json 相对工程根目录路径。
            /// </summary>
            private const string c_ManifestRelativePath = "Packages/manifest.json";

            /// <summary>
            /// 更新日志本地缓存目录，相对工程根目录（永久缓存，命中直接打开）。
            /// </summary>
            internal const string c_ChangelogCacheRelDir = "Library/Nova/Changelog";

            /// <summary>
            /// tarball 临时下载目录，相对工程根目录（解压完后删除）。
            /// </summary>
            internal const string c_TarballCacheRelDir = "Library/Nova/Tarballs";

            /// <summary>
            /// tarball 内更新日志的固定路径。
            /// </summary>
            internal const string c_ChangelogTarEntry = "package/CHANGELOG.md";
        }
    }
}
