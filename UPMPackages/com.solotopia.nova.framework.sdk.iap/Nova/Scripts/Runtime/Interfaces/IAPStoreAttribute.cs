/***************************************************************
 * (c) copyright 2026 - 2030, Solotopia
 * All Rights Reserved.
 * -------------------------------------------------------------
 * filename:  IAPStoreAttribute.cs
 * author:    yingzheng
 * created:   2026/5/22
 * descrip:   标记 IIAPInternalStore 实现，使其被 IAPPlugin 启动期反射扫描创建
 ***************************************************************/

using System;

namespace NovaFramework.SDK.IAP.Runtime
{
    /// <summary>
    /// 标记一个 IIAPInternalStore 实现，使其被 IAPPlugin 启动期反射扫描创建。
    /// 打上此特性的 public 非抽象类将在 OnInitializeAsync 阶段通过
    /// AppDomain.CurrentDomain.GetAssemblies() 自动发现并实例化，
    /// 无需核心包硬编码引用子包程序集。
    /// </summary>
    [AttributeUsage(AttributeTargets.Class, Inherited = false, AllowMultiple = false)]
    public sealed class IAPStoreAttribute : Attribute
    {
    }
}
