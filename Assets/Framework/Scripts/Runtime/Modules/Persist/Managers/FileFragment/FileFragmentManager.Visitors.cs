/***************************************************************
 * (c) copyright 2026 - 2030, Solotopia
 * All Rights Reserved.
 * -------------------------------------------------------------
 * filename:  FileFragmentManager.Visitors.cs
 * author:    taoye
 * created:   2026/3/18
 * descrip:   文件片段持久化管理器 —— 属性与字段
 ***************************************************************/

using System.Collections.Generic;

namespace NovaFramework.Runtime
{
    internal sealed partial class FileFragmentManager : PersistManagerBase<FileFragmentManagerConfig>, IFileFragmentManager
    {
        /// <summary>
        /// 文件片段扩展名。
        /// </summary>
        private const string c_FileExtension = ".dat";

        /// <summary>
        /// 文件片段根目录绝对路径。
        /// </summary>
        private string m_RootFolderPath;

        /// <summary>
        /// 分类名 → 文件片段数据容器，<分类名, 数据容器>。
        /// </summary>
        private SortedDictionary<string, FileFragmentItemGroup> m_ItemGroups = new();

        /// <summary>
        /// 已加载到内存的分类集合（懒加载追踪）。
        /// </summary>
        private HashSet<string> m_LoadedFragments = new();

        /// <summary>
        /// 有未保存变更的分类集合（脏追踪）。
        /// </summary>
        private HashSet<string> m_DirtyFragments = new();

        /// <summary>
        /// 待删除的分类列表（RemoveAll 后标记，下次 Save 时删除文件）。
        /// </summary>
        private HashSet<string> m_PendingDeletes = new();

        /// <summary>
        /// Load 方法正在执行中的标记，防止并发重入。
        /// </summary>
        private bool m_IsLoading;
    }
}
