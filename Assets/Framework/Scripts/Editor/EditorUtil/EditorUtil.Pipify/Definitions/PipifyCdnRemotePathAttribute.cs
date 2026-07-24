/***************************************************************
 * (c) copyright 2026 - 2030, Solotopia
 * All Rights Reserved.
 * -------------------------------------------------------------
 * filename:  PipifyCdnRemotePathAttribute.cs
 * author:    Codex
 * created:   2026/7/23
 * descrip:   Pipify CDN 云端位置分段绘制标记
 ***************************************************************/

using System;

namespace NovaFramework.Editor
{
    /// <summary>
    /// 标记 CDN 云端位置后缀字段；PipifyWindow 会在输入框前显示当前 Config 的只读 OSS 前缀。
    /// </summary>
    [AttributeUsage(AttributeTargets.Field)]
    public sealed class PipifyCdnRemotePathAttribute : Attribute
    {
    }
}
