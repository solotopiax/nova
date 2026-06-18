/***************************************************************
 * (c) copyright 2026 - 2030, Solotopia
 * All Rights Reserved.
 * -------------------------------------------------------------
 * filename:  Path.Cache.cs
 * author:    taoye
 * created:   2026-05-17
 * descrip:   路径工具 —— Unity Caching 缓存路径
 ***************************************************************/

using UnityEngine;

namespace NovaFramework.Runtime
{
    public static partial class Path
    {
        /// <summary>
        /// Unity Caching 缓存路径信息。
        /// </summary>
        public static class Cache
        {
            /// <summary>
            /// 当前写入缓存目录的绝对路径（每次访问从 Unity Caching API 实时获取）。
            /// </summary>
            public static string FolderFullPath => NormalizeSeparator(UnityEngine.Caching.currentCacheForWriting.path);

            /// <summary>
            /// 获取缓存目录下指定相对路径的绝对路径。
            /// </summary>
            /// <param name="relativePath">相对于根目录的路径。</param>
            /// <returns>绝对路径字符串。</returns>
            public static string GetFileFullPath(string relativePath)
            {
                return NormalizeSeparator($"{FolderFullPath}/{relativePath}");
            }
        }
    }
}
