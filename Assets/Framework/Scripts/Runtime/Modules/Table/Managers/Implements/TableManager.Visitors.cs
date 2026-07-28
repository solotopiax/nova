/***************************************************************
 * (c) copyright 2026 - 2030, Solotopia
 * All Rights Reserved.
 * -------------------------------------------------------------
 * filename:  TableManager.Visitors.cs
 * author:    taoye
 * created:   2026/2/5
 * descrip:   Table 管理器属性与字段
 ***************************************************************/

using System;
using System.Collections.Generic;

namespace NovaFramework.Runtime
{
    internal sealed partial class TableManager : TableManagerBase
    {
        private IAssetManager m_AssetManager;
        private readonly List<TableRuntimeBindingSetting> m_Bindings = new List<TableRuntimeBindingSetting>();
        private readonly Dictionary<Type, ITable> m_Tables = new Dictionary<Type, ITable>();

        /// <summary>
        /// 获取已构建的生成表数量。
        /// </summary>
        public override int Count => m_Tables.Count;
    }
}
