/***************************************************************
 * (c) copyright 2026 - 2030, Solotopia
 * All Rights Reserved.
 * -------------------------------------------------------------
 * filename:  PipifyDynamicDefaultAttribute.cs
 * author:    taoye
 * created:   2026/5/19
 * descrip:   PipifyWindow 字段渲染特性 —— 字符串字段为空时显示动态默认值
 ***************************************************************/

using System;

namespace NovaFramework.Editor
{
    /// <summary>
    /// 标注一个 string 类型的参数字段：值为空时 PipifyWindow 应显示由指定无参静态方法返回的动态默认值占位。
    /// 显示态仅作为占位提示（不写回字段），用户聚焦或输入会清空占位；与 YooAsset Bundle Builder 默认版本号机制一致。
    /// </summary>
    [AttributeUsage(AttributeTargets.Field, AllowMultiple = false, Inherited = false)]
    public sealed class PipifyDynamicDefaultAttribute : Attribute
    {
        /// <summary>
        /// 默认值提供者所在类型。
        /// </summary>
        public Type ProviderType { get; }

        /// <summary>
        /// 默认值提供者方法名（须为无参 static，返回 string）。
        /// </summary>
        public string MethodName { get; }

        /// <summary>
        /// 构造动态默认值特性。
        /// </summary>
        /// <param name="providerType">提供者类型。</param>
        /// <param name="methodName">方法名（无参 static，返回 string）。</param>
        public PipifyDynamicDefaultAttribute(Type providerType, string methodName)
        {
            ProviderType = providerType;
            MethodName = methodName;
        }
    }
}
