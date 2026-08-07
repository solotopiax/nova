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
    /// 标注参数类型时在参数区域顶部绘制 HelpBox；标注字段时紧跟该字段绘制 HelpBox。
    /// </summary>
    [AttributeUsage(AttributeTargets.Class | AttributeTargets.Field, AllowMultiple = false, Inherited = false)]
    public sealed class PipifyHelpBoxAttribute : Attribute
    {
        public PipifyHelpBoxAttribute(params string[] messages)
        {
            Messages = messages ?? Array.Empty<string>();
        }

        public string[] Messages { get; }
    }
}
