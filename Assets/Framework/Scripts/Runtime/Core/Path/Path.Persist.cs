/***************************************************************
 * (c) copyright 2026 - 2030, Solotopia
 * All Rights Reserved.
 * -------------------------------------------------------------
 * filename:  Path.Persist.cs
 * author:    taoye
 * created:   2026/3/18
 * descrip:   路径工具 —— 持久化存储相关路径
 ***************************************************************/

using UnityEngine;

namespace NovaFramework.Runtime
{
    public static partial class Path
    {
        /// <summary>
        /// 持久化存储相关路径信息。
        /// </summary>
        public static class Persist
        {
            /// <summary>
            /// 文件片段持久化路径信息。
            /// </summary>
            public static class FileFragment
            {
                /// <summary>
                /// 文件片段根目录的绝对路径。
                /// </summary>
                public static readonly string FolderFullPath = $"{Application.persistentDataPath}/Persist/FileFragment";
            }

            /// <summary>
            /// SQLite 数据库持久化路径信息。
            /// </summary>
            public static class SQLite
            {
                /// <summary>
                /// SQLite 数据库文件所在目录的绝对路径。
                /// </summary>
                public static readonly string FolderFullPath = $"{Application.persistentDataPath}/Persist/SQLite";

                /// <summary>
                /// SQLite 数据库文件的绝对路径。
                /// </summary>
                public static readonly string FileFullPath = $"{Application.persistentDataPath}/Persist/SQLite/game.db";
            }
        }
    }
}
