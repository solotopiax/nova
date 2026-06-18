/***************************************************************
 * (c) copyright 2026 - 2030, Solotopia
 * All Rights Reserved.
 * -------------------------------------------------------------
 * filename:  MemoryRowData.cs
 * author:    yingzheng
 * created:   2026/6/12
 * descrip:   Nova Runtime Debugger 运行时代码。
 ***************************************************************/
namespace NovaFramework.Runtime
{
    /// <summary>
    /// MemoryRowView 的数据容器，承载虚拟滚动单行的三列独立数据。
    /// Summary 模式：Name = 类型名，Type = Count 数字字符串，Size = 格式化内存大小。
    /// 分类模式：Name = 对象名，Type = 类型名，Size = 格式化内存大小。
    /// </summary>
    public struct MemoryRowData
    {
        /// <summary>
        /// 名称列：Summary 模式为类型名；分类模式为对象名。
        /// </summary>
        public string Name;

        /// <summary>
        /// 类型列：Summary 模式为 Count 数量字符串；分类模式为类型名。
        /// </summary>
        public string Type;

        /// <summary>
        /// 大小列：格式化后的内存大小字符串，如 "1.23 MB"。
        /// </summary>
        public string Size;

        /// <summary>
        /// 是否为重复项（分类模式下，与前一条记录同名同类同大小）。
        /// true 时 MemoryRowView 将 Name 列文字颜色设为黄色。
        /// </summary>
        public bool Highlight;
    }
}
