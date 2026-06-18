/***************************************************************
 * (c) copyright 2026 - 2030, Solotopia
 * All Rights Reserved.
 * -------------------------------------------------------------
 * filename:  ITableManager.cs
 * author:    taoye
 * created:   2026/2/5
 * descrip:   表格管理器接口
 ***************************************************************/

using System;
using Cysharp.Threading.Tasks;

namespace NovaFramework.Runtime
{
    /// <summary>
    /// 表格管理器接口。
    /// </summary>
    public interface ITableManager
    {
        /// <summary>
        /// 获取表格数量。
        /// </summary>
        int Count { get; }

        /// <summary>
        /// 初始化表格管理器。
        /// </summary>
        /// <param name="config">表格管理器配置。</param>
        void Initialize(TableManagerConfig config);

        /// <summary>
        /// 异步加载所有表格数据。
        /// </summary>
        /// <returns>是否加载成功。</returns>
        UniTask<bool> LoadTablesAsync();

        /// <summary>
        /// 同步加载所有表格数据。
        /// </summary>
        /// <returns>是否加载成功。</returns>
        bool LoadTablesSync();

        /// <summary>
        /// 是否包含指定类型的表格。
        /// </summary>
        /// <typeparam name="T">表格类型。</typeparam>
        /// <returns>是否存在。</returns>
        bool HasTable<T>() where T : class, ITable;

        /// <summary>
        /// 是否包含指定类型的表格。
        /// </summary>
        /// <param name="type">表格类型。</param>
        /// <returns>是否存在。</returns>
        bool HasTable(Type type);

        /// <summary>
        /// 获取指定类型的表格。
        /// </summary>
        /// <typeparam name="T">表格类型。</typeparam>
        /// <returns>表格实例，不存在时返回 null。</returns>
        T GetTable<T>() where T : class, ITable;

        /// <summary>
        /// 获取指定类型的表格。
        /// </summary>
        /// <param name="type">表格类型。</param>
        /// <returns>表格实例，不存在时返回 null。</returns>
        object GetTable(Type type);
    }
}
