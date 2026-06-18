/***************************************************************
 * (c) copyright 2026 - 2030, Solotopia
 * All Rights Reserved.
 * -------------------------------------------------------------
 * filename:  DataTableMode.cs
 * author:    taoye
 * created:   2026/4/16
 * descrip:   数据表模式枚举
 ***************************************************************/

using UnityEngine;

namespace NovaFramework.Runtime
{
    /// <summary>
    /// 数据表模式（List / Map / One），由 Luban 生成的 TbXxx 类通过 ITable.Mode 属性返回。
    /// </summary>
    public enum DataTableMode
    {
        /// <summary>
        /// 列表模式，运行时反序列化为 List<T>。
        /// </summary>
        [InspectorName("List")]
        List,

        /// <summary>
        /// 映射模式，运行时反序列化为 Dictionary<TKey, T>。
        /// </summary>
        [InspectorName("Map")]
        Map,

        /// <summary>
        /// 单例模式，运行时反序列化为单个 T 实例。
        /// </summary>
        [InspectorName("One")]
        One,
    }
}
