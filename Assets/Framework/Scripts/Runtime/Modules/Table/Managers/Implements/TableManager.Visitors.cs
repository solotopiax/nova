/***************************************************************
 * (c) copyright 2026 - 2030, Solotopia
 * All Rights Reserved.
 * -------------------------------------------------------------
 * filename:  TableManager.Visitors.cs
 * author:    taoye
 * created:   2026/2/5
 * descrip:   表格管理器 -- 属性与字段
 ***************************************************************/

using System;
using System.Collections.Generic;

namespace NovaFramework.Runtime
{
    internal sealed partial class TableManager : TableManagerBase
    {
        /// <summary>
        /// 资源管理器，在 Initialize 中获取并缓存，供 DataReceiver 委托使用。
        /// </summary>
        private IAssetManager m_AssetManager;

        /// <summary>
        /// 表格单元设置列表。
        /// </summary>
        private List<TableUnitSetting> m_UnitSettings;

        /// <summary>
        /// TableTables 固定类名（与 Luban luban.conf 中 manager 名称一致）。
        /// </summary>
        private const string c_TableTablesClassName = "TableTables";

        /// <summary>
        /// 统一表格容器（Type -> ITable）。
        /// </summary>
        private readonly Dictionary<Type, ITable> m_Tables = new Dictionary<Type, ITable>();

        /// <summary>
        /// Luban 数据加载缓存，Phase 1 加载后暂存，Phase 2 构建后置 null。
        /// </summary>
        private LubanDataCache m_DataCache;

        /// <summary>
        /// 获取表格数量。
        /// </summary>
        public override int Count => m_Tables.Count;
    }
}
