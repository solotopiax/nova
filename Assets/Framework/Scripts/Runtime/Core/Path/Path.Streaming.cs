/***************************************************************
 * (c) copyright 2026 - 2030, Solotopia
 * All Rights Reserved.
 * -------------------------------------------------------------
 * filename:  Path.Streaming.cs
 * author:    taoye
 * created:   2026-05-17
 * descrip:   路径工具 —— 只读路径（StreamingAssets）相关路径
 ***************************************************************/

using UnityEngine;

namespace NovaFramework.Runtime
{
    public static partial class Path
    {
        /// <summary>
        /// 只读路径（StreamingAssets）相关路径信息。
        /// </summary>
        public static class Streaming
        {
            /// <summary>
            /// 只读路径根目录的绝对路径（Application.streamingAssetsPath 已统一覆盖各平台差异）。
            /// </summary>
            public static readonly string FolderFullPath = NormalizeSeparator(Application.streamingAssetsPath);

            /// <summary>
            /// 获取只读路径下指定相对路径的绝对路径。
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
