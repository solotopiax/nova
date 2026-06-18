/***************************************************************
 * (c) copyright 2026 - 2030, Solotopia
 * All Rights Reserved.
 * -------------------------------------------------------------
 * filename:  ObjectPoolComponentInspector.Visitors.cs
 * author:    taoye
 * created:   2026/3/4
 * descrip:   对象池组件编辑器面板定制 —— 属性与字段
 ***************************************************************/

using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

namespace NovaFramework.Editor
{
    internal sealed partial class ObjectPoolComponentInspector : BaseComponentInspector
    {
        /// <summary>
        /// 当前对象池管理器类型名称。
        /// </summary>
        private SerializedProperty m_CurManagerTypeName;

        /// <summary>
        /// 对象池管理器所有类型名称。
        /// </summary>
        private List<string> m_ManagerTypeNames;

        /// <summary>
        /// 池列表搜索文本。
        /// </summary>
        private string m_SearchText = string.Empty;

        /// <summary>
        /// 对象列表分页大小。
        /// </summary>
        private const int c_PageSize = 50;

        /// <summary>
        /// 每个池的当前页码（从 0 开始）。
        /// 键为池的 FullName，值为当前页索引。
        /// </summary>
        private Dictionary<string, int> m_PageIndices;

        /// <summary>
        /// 对象信息表格各列固定宽度（像素）。
        /// Name 列由 EditorUtil.Draw.TableRow 动态计算，不在此定义。
        /// </summary>
        private const float c_ColLocked = 54f;
        private const float c_ColInUse = 54f;
        private const float c_ColFlag = 48f;
        private const float c_ColPriority = 64f;
        private const float c_ColLastUse = 74f;
        private const float c_ColExpire = 64f;

    }
}
