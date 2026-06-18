/***************************************************************
 * (c) copyright 2026 - 2030, Solotopia
 * All Rights Reserved.
 * -------------------------------------------------------------
 * filename:  PlayerPrefsManager.Visitors.cs
 * author:    taoye
 * created:   2026/3/18
 * descrip:   PlayerPrefs 持久化管理器 —— 属性与字段
 ***************************************************************/

using System.Collections.Generic;

namespace NovaFramework.Runtime
{
    internal sealed partial class PlayerPrefsManager : PersistManagerBase<PlayerPrefsManagerConfig>, IPlayerPrefsManager
    {
        /// <summary>
        /// bool 真值的字符串表示。
        /// </summary>
        private const string c_BoolTrue = "1";

        /// <summary>
        /// bool 假值的字符串表示。
        /// </summary>
        private const string c_BoolFalse = "0";

        /// <summary>
        /// 全分类名称索引在 PlayerPrefs 中的键。
        /// </summary>
        private const string c_ClassifyIndexKey = "__pp_classifies__";

        /// <summary>
        /// 数据键分隔符。
        /// </summary>
        private const string c_KeySeparator = "::";

        /// <summary>
        /// 是否存在未落盘的脏数据。
        /// </summary>
        private bool m_IsDirty;

        /// <summary>
        /// 分类名 → 条目名集合，<分类名, 条目名集合>。使用 HashSet 确保 O(1) 查重。
        /// </summary>
        private SortedDictionary<string, HashSet<string>> m_ItemNameGroups = new();

        /// <summary>
        /// 有未落盘变更的分类集合。
        /// </summary>
        private HashSet<string> m_DirtyClassifies = new();
    }
}
