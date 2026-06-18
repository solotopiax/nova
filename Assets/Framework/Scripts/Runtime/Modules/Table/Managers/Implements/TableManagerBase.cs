/***************************************************************
 * (c) copyright 2026 - 2030, Solotopia
 * All Rights Reserved.
 * -------------------------------------------------------------
 * filename:  TableManagerBase.cs
 * author:    taoye
 * created:   2026/2/5
 * descrip:   表格管理器基类
 ***************************************************************/

using System;
using Cysharp.Threading.Tasks;

namespace NovaFramework.Runtime
{
    /// <summary>
    /// 表格管理器基类。
    /// </summary>
    internal abstract class TableManagerBase : FrameworkManager, ITableManager
    {
        /// <summary>
        /// 管理器优先级（值越小越先 Update、越后 Shutdown）。
        /// </summary>
        public override int Priority => 14;

        /// <summary>
        /// 获取表格数量。
        /// </summary>
        public abstract int Count { get; }

        /// <summary>
        /// 构造方法。
        /// </summary>
        protected TableManagerBase()
        {
        }

        /// <summary>
        /// 初始化表格管理器。
        /// </summary>
        /// <param name="config">表格管理器配置。</param>
        public abstract void Initialize(TableManagerConfig config);

        /// <summary>
        /// 管理器轮询。
        /// </summary>
        public abstract override void Update();

        /// <summary>
        /// 关闭并清理管理器。
        /// </summary>
        public abstract override void Shutdown();

        /// <summary>
        /// 异步加载所有表格数据。
        /// </summary>
        /// <returns>是否加载成功。</returns>
        public abstract UniTask<bool> LoadTablesAsync();

        /// <summary>
        /// 同步加载所有表格数据。
        /// </summary>
        /// <returns>是否加载成功。</returns>
        public abstract bool LoadTablesSync();

        /// <summary>
        /// 是否包含指定类型的表格。
        /// </summary>
        /// <typeparam name="T">表格类型。</typeparam>
        /// <returns>是否存在。</returns>
        public abstract bool HasTable<T>() where T : class, ITable;

        /// <summary>
        /// 是否包含指定类型的表格。
        /// </summary>
        /// <param name="type">表格类型。</param>
        /// <returns>是否存在。</returns>
        public abstract bool HasTable(Type type);

        /// <summary>
        /// 获取指定类型的表格。
        /// </summary>
        /// <typeparam name="T">表格类型。</typeparam>
        /// <returns>表格实例，不存在时返回 null。</returns>
        public abstract T GetTable<T>() where T : class, ITable;

        /// <summary>
        /// 获取指定类型的表格。
        /// </summary>
        /// <param name="type">表格类型。</param>
        /// <returns>表格实例，不存在时返回 null。</returns>
        public abstract object GetTable(Type type);
    }
}
