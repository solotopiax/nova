/***************************************************************
 * (c) copyright 2026 - 2030, Solotopia
 * All Rights Reserved.
 * -------------------------------------------------------------
 * filename:  PipifyDropdownAttribute.cs
 * author:    taoye
 * created:   2026/5/19
 * descrip:   PipifyWindow 字段渲染特性 —— 字符串字段以接口实现类下拉框形式编辑
 ***************************************************************/

using System;

namespace NovaFramework.Editor
{
    /// <summary>
    /// 标注一个 string 类型的参数字段，PipifyWindow 应将其渲染为下拉框。
    /// 选项来源于 EditorAssemblyUtility 收集的 InterfaceType 全部可实例化实现类型，存储值为 Type.FullName。
    /// </summary>
    [AttributeUsage(AttributeTargets.Field, AllowMultiple = false, Inherited = false)]
    public sealed class PipifyDropdownAttribute : Attribute
    {
        /// <summary>
        /// 用于收集实现类型的接口（或基类）类型。
        /// </summary>
        public Type InterfaceType { get; }

        /// <summary>
        /// 构造下拉特性。
        /// </summary>
        /// <param name="interfaceType">用于收集实现类型的接口或基类。</param>
        public PipifyDropdownAttribute(Type interfaceType)
        {
            InterfaceType = interfaceType;
        }
    }
}
