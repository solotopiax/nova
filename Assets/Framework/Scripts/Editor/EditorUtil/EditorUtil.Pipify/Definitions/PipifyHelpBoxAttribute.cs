/***************************************************************
 * (c) copyright 2026 - 2030, Solotopia
 * All Rights Reserved.
 * -------------------------------------------------------------
 * filename:  PipifyHelpBoxAttribute.cs
 * author:    Codex
 * created:   2026/7/24
 * descrip:   Pipify 参数类型的通用 HelpBox 元数据
 ***************************************************************/

using System;

namespace NovaFramework.Editor
{
    /// <summary>
    /// 在 Pipify 参数区域顶部绘制说明 HelpBox。
    /// </summary>
    [AttributeUsage(AttributeTargets.Class, AllowMultiple = false, Inherited = false)]
    public sealed class PipifyHelpBoxAttribute : Attribute
    {
        public PipifyHelpBoxAttribute(params string[] messages)
        {
            Messages = messages ?? Array.Empty<string>();
        }

        public string[] Messages { get; }
    }
}
