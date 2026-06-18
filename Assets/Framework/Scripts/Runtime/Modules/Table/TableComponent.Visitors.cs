/***************************************************************
 * (c) copyright 2026 - 2030, Solotopia
 * All Rights Reserved.
 * -------------------------------------------------------------
 * filename:  TableComponent.Visitors.cs
 * author:    taoye
 * created:   2026/2/5
 * descrip:   表格组件-访问器
 ***************************************************************/

using Cysharp.Threading.Tasks;
using UnityEngine;

namespace NovaFramework.Runtime
{
    /// <summary>
    /// 表格组件。
    /// </summary>
    public sealed partial class TableComponent : FrameworkComponent
    {
        /// <summary>
        /// 当前表格管理器类型名称。
        /// </summary>
        [Tooltip("表格管理器实现类全名")]
        [SerializeField]
        private string m_CurManagerTypeName = "NovaFramework.Runtime.TableManager";
        public string CurManagerTypeName => m_CurManagerTypeName;

        /// <summary>
        /// 设置。
        /// </summary>
        [Tooltip("表格数据路径与加载设置")]
        [SerializeField]
        private TableSettings m_Setting;

        /// <summary>
        /// 表格管理器实例。
        /// </summary>
        private ITableManager m_TableManager;

        /// <summary>
        /// 异步加载表格数据任务完成源。
        /// </summary>
        private UniTaskCompletionSource<bool> m_LoadTcs;

        /// <summary>
        /// 是否已加载完成。
        /// </summary>
        public bool IsLoadOver { get; private set; }

        /// <summary>
        /// 获取表格数量。
        /// </summary>
        public int Count => m_TableManager.Count;
    }
}
