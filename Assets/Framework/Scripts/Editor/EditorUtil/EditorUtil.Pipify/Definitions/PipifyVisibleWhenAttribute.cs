/***************************************************************
 * (c) copyright 2026 - 2030, Solotopia
 * All Rights Reserved.
 * -------------------------------------------------------------
 * filename:  PipifyVisibleWhenAttribute.cs
 * author:    taoye
 * created:   2026/5/19
 * descrip:   PipifyWindow 字段渲染特性 —— 依赖另一字段的值来决定显隐
 ***************************************************************/

using System;

namespace NovaFramework.Editor
{
    /// <summary>
    /// 标注一个参数字段：仅当另一字段的当前值匹配 AnyOf 中任意一项时显隐。
    /// 仅支持枚举/整数字段作为依赖字段；AnyOf 比较以 Convert.ToInt32 后的整型值进行。
    /// </summary>
    [AttributeUsage(AttributeTargets.Field, AllowMultiple = false, Inherited = false)]
    public sealed class PipifyVisibleWhenAttribute : Attribute
    {
        /// <summary>
        /// 依赖的字段名（同一参数类内）。
        /// </summary>
        public string DependsOn { get; }

        /// <summary>
        /// 触发显示的允许值集合（整型化后的枚举/整数值）。
        /// </summary>
        public int[] AnyOf { get; }

        /// <summary>
        /// 构造显隐特性。
        /// </summary>
        /// <param name="dependsOn">依赖字段名。</param>
        /// <param name="anyOf">允许显示的整型化取值集合。</param>
        public PipifyVisibleWhenAttribute(string dependsOn, params int[] anyOf)
        {
            DependsOn = dependsOn;
            AnyOf = anyOf ?? Array.Empty<int>();
        }
    }
}
