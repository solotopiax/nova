/***************************************************************
 * (c) copyright 2026 - 2030, Solotopia
 * All Rights Reserved.
 * -------------------------------------------------------------
 * filename:  Path.Persistent.cs
 * author:    taoye
 * created:   2026-05-17
 * descrip:   路径工具 —— 可写路径（persistentDataPath）相关路径
 ***************************************************************/

using UnityEngine;

namespace NovaFramework.Runtime
{
    public static partial class Path
    {
        /// <summary>
        /// 可写路径（persistentDataPath）相关路径信息。
        /// </summary>
        public static class Persistent
        {
            /// <summary>
            /// 可写路径根目录的绝对路径。
            /// </summary>
            public static readonly string FolderFullPath = NormalizeSeparator(Application.persistentDataPath);

            /// <summary>
            /// 获取可写路径下指定相对路径的绝对路径。
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
