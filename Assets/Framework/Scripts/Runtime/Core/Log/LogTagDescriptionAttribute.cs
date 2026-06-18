/***************************************************************
 * (c) copyright 2026 - 2030, Solotopia
 * All Rights Reserved.
 * -------------------------------------------------------------
 * filename:  LogTagDescriptionAttribute.cs
 * author:    yingzheng
 * created:   2026/5/12
 * descrip:   LogTag 字段描述 Attribute，供运行时反射读取中文描述
 ***************************************************************/

using System;

namespace NovaFramework.Runtime
{
    /// <summary>
    /// 标注在 LogTag 常量字段上，提供可供运行时反射读取的中文描述文字。
    /// 由 TagFilterPanelControl 在构建标签树时读取，用于行显示名和分类 Tab 名称。
    /// </summary>
    [AttributeUsage(AttributeTargets.Field, AllowMultiple = false, Inherited = false)]
    public sealed class LogTagDescriptionAttribute : Attribute
    {
        /// <summary>
        /// 标签的中文描述文字。
        /// </summary>
        public string Description { get; }

        /// <summary>
        /// 构造函数，接受标签中文描述。
        /// </summary>
        /// <param name="description">标签中文描述文字。</param>
        public LogTagDescriptionAttribute(string description)
        {
            Description = description;
        }
    }
}
