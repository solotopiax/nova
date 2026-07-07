/***************************************************************
 * (c) copyright 2026 - 2030, Solotopia
 * All Rights Reserved.
 * -------------------------------------------------------------
 * filename:  PipifyPasswordAttribute.cs
 * author:    taoye
 * created:   2026/7/6
 * descrip:   PipifyWindow 字符串字段密码输入标记
 ***************************************************************/

using System;

namespace NovaFramework.Editor
{
    /// <summary>
    /// 标记字符串参数字段在 PipifyWindow 中使用 PasswordField 绘制。
    /// 存储值仍然是普通序列化字符串，确保 CLI 参数覆盖逻辑继续可用。
    /// </summary>
    [AttributeUsage(AttributeTargets.Field, AllowMultiple = false, Inherited = false)]
    public sealed class PipifyPasswordAttribute : Attribute
    {
    }
}
