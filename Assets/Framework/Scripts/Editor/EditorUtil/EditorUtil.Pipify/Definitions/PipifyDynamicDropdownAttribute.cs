/***************************************************************
 * (c) copyright 2026 - 2030, Solotopia
 * All Rights Reserved.
 * -------------------------------------------------------------
 * filename:  PipifyDynamicDropdownAttribute.cs
 * author:    taoye
 * created:   2026/5/19
 * descrip:   PipifyWindow 字段渲染特性 —— 字符串字段以动态选项下拉框形式编辑
 ***************************************************************/

using System;

namespace NovaFramework.Editor
{
    /// <summary>
    /// 标注一个 string 类型的参数字段，PipifyWindow 应将其渲染为动态选项下拉框。
    /// 选项来源由 ProviderType.MethodName 静态无参方法在每次绘制时返回 string[] 提供，
    /// 适合需要根据工程实际配置动态生成选项的场景（如包名列表）。
    /// </summary>
    [AttributeUsage(AttributeTargets.Field, AllowMultiple = false, Inherited = false)]
    public sealed class PipifyDynamicDropdownAttribute : Attribute
    {
        /// <summary>
        /// 选项提供者所在类型。
        /// </summary>
        public Type ProviderType { get; }

        /// <summary>
        /// 静态无参方法名，返回 string[] 作为下拉选项。
        /// </summary>
        public string MethodName { get; }

        /// <summary>
        /// 构造动态下拉特性。
        /// </summary>
        /// <param name="providerType">选项提供者类型。</param>
        /// <param name="methodName">静态无参方法名。</param>
        public PipifyDynamicDropdownAttribute(Type providerType, string methodName)
        {
            ProviderType = providerType;
            MethodName = methodName;
        }
    }
}
