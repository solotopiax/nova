/***************************************************************
 * (c) copyright 2026 - 2030, Solotopia
 * All Rights Reserved.
 * -------------------------------------------------------------
 * filename:  ILubanTables.cs
 * author:    taoye
 * created:   2026/4/15
 * descrip:   Luban TableTables 总管理器接口
 ***************************************************************/

using System;
using System.Collections.Generic;

namespace NovaFramework.Runtime
{
    /// <summary>
    /// Luban TableTables 总管理器接口，框架层通过此接口操作 Game 层生成的 TableTables。
    /// </summary>
    public interface ILubanTables
    {
        /// <summary>
        /// 解析所有跨表引用。
        /// </summary>
        void ResolveRef();

        /// <summary>
        /// 获取所有 ITable 实例。
        /// </summary>
        /// <returns>所有 TbXxx 实例列表。</returns>
        IReadOnlyList<ITable> GetAllTables();

        /// <summary>
        /// 获取指定类型的 Luban 表实例。
        /// </summary>
        /// <typeparam name="T">Luban 表类型。</typeparam>
        /// <returns>表实例，不存在时返回 null。</returns>
        T GetTable<T>() where T : class, ITable;

        /// <summary>
        /// 根据类型获取 Luban 表实例。
        /// </summary>
        /// <param name="type">表类型。</param>
        /// <returns>表实例，不存在时返回 null。</returns>
        ITable GetTable(Type type);
    }
}
