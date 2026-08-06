/***************************************************************
 * (c) copyright 2026 - 2030, Solotopia
 * All Rights Reserved.
 * -------------------------------------------------------------
 * filename:  PlugPalsWindow.Definitions.cs
 * author:    taoye
 * created:   2026/4/8
 * descrip:   PlugPals 窗口内部请求状态类型定义
 ***************************************************************/

using System.Collections.Generic;
using UnityEditor;

namespace NovaFramework.Editor
{
    public sealed partial class PlugPalsWindow : EditorWindow
    {
        /// <summary>
        /// 单个 registry 请求结果，用于并发请求完成后按来源渐进更新窗口状态。
        /// </summary>
        private sealed class RegistryFetchResult
        {
            /// <summary>
            /// 是否来自内部云 registry。
            /// </summary>
            public bool IsInternal;

            /// <summary>
            /// registry 对应的包展示条目；失败时为空列表。
            /// </summary>
            public List<EditorUtil.PlugPals.PackageDisplayEntry> Entries;

            /// <summary>
            /// 请求或解析失败信息；成功时为空。
            /// </summary>
            public string ErrorMessage;
        }
    }
}
