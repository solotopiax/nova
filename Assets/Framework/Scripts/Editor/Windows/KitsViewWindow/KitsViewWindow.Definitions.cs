/***************************************************************
 * (c) copyright 2026 - 2030, Solotopia
 * All Rights Reserved.
 * -------------------------------------------------------------
 * filename:  KitsViewWindow.Definitions.cs
 * author:    taoye
 * created:   2026/4/22
 * descrip:   KitsView 窗口嵌套类型定义
 ***************************************************************/

using System.Collections.Generic;
using UnityEditor;

namespace NovaFramework.Editor
{
    internal sealed partial class KitsViewWindow : EditorWindow
    {
        /// <summary>
        /// Kit 包信息条目，从 package.json 解析而来。
        /// </summary>
        private sealed class KitEntry
        {
            /// <summary>
            /// 包名（如 com.solotopia.nova.framework.kit.network）。
            /// </summary>
            public string Name;

            /// <summary>
            /// 显示名称（displayName 字段）。
            /// </summary>
            public string DisplayName;

            /// <summary>
            /// 版本号。
            /// </summary>
            public string Version;

            /// <summary>
            /// 包描述。
            /// </summary>
            public string Description;

            /// <summary>
            /// 依赖列表 <包名, 版本号>。
            /// </summary>
            public List<KeyValuePair<string, string>> Dependencies;

            /// <summary>
            /// 包目录的绝对路径。
            /// </summary>
            public string PackagePath;

            /// <summary>
            /// Proto 文件信息列表（文件名 + 内容）。
            /// </summary>
            public List<ProtoFileEntry> ProtoFiles;

            /// <summary>
            /// 条目展开状态。
            /// </summary>
            public bool IsExpanded;
        }

        /// <summary>
        /// Proto 文件条目。
        /// </summary>
        private sealed class ProtoFileEntry
        {
            /// <summary>
            /// 文件名（不含路径）。
            /// </summary>
            public string FileName;

            /// <summary>
            /// 文件完整内容。
            /// </summary>
            public string Content;

            /// <summary>
            /// 内容展开状态。
            /// </summary>
            public bool IsExpanded;
        }
    }
}
