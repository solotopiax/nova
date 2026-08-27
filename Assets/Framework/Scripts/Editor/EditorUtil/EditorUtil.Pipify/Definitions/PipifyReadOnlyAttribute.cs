/***************************************************************
 * (c) copyright 2026 - 2030, Solotopia
 * All Rights Reserved.
 * -------------------------------------------------------------
 * filename:  PipifyReadOnlyAttribute.cs
 * author:    taoye
 * created:   2026/8/27
 * descrip:   PipifyWindow 参数字段只读绘制标记
 ***************************************************************/

using System;

namespace NovaFramework.Editor
{
    /// <summary>
    /// 标记由运行环境提供、只允许 PipifyWindow 展示而不允许用户编辑的参数字段。
    /// Runner 仍须在执行前同步或校验实际值，不能把 UI 只读当作执行安全边界。
    /// </summary>
    [AttributeUsage(AttributeTargets.Field, AllowMultiple = false, Inherited = false)]
    public sealed class PipifyReadOnlyAttribute : Attribute
    {
    }
}
