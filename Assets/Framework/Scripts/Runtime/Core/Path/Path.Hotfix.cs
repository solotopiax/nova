/***************************************************************
 * (c) copyright 2026 - 2030, Solotopia
 * All Rights Reserved.
 * -------------------------------------------------------------
 * filename:  Path.Hotfix.cs
 * author:    taoye
 * created:   2026/3/6
 * descrip:   路径工具 —— 热更新相关路径
 ***************************************************************/

using UnityEngine;

namespace NovaFramework.Runtime
{
    public static partial class Path
    {
        /// <summary>
        /// 热更新相关路径信息。
        /// </summary>
        public static class Hotfix
        {
            /// <summary>
            /// APK 包下载后保存的绝对路径。
            /// </summary>
            public static readonly string ApkDownloadedFullPath = $"{Application.persistentDataPath}/update.apk";
        }
    }
}
