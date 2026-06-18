/***************************************************************
 * (c) copyright 2026 - 2030, Solotopia
 * All Rights Reserved.
 * -------------------------------------------------------------
 * filename:  SDKPluginEntry.cs
 * author:    taoye
 * created:   2026/4/28
 * descrip:   SDK 插件条目序列化结构
 ***************************************************************/

using System;
using UnityEngine;

namespace NovaFramework.Runtime
{
    /// <summary>
    /// SDK 插件条目，Inspector 序列化结构。
    /// 一个 TypeName 对应一个 Plugin，Editor 面板按直接子接口分组展示。
    /// IsMissing 为运行时标记，不序列化，由 Editor 与 Manager.Initialize 写入。
    /// </summary>
    [Serializable]
    public sealed class SDKPluginEntry
    {
        /// <summary>
        /// Plugin 类型完整限定名（AssemblyQualifiedName），用于运行时反射实例化。
        /// </summary>
        [SerializeField] public string TypeName;

        /// <summary>
        /// 是否在当前项目中启用此插件；默认 false，需 Inspector 显式勾选。
        /// </summary>
        [SerializeField] public bool Enabled;

        /// <summary>
        /// 初始化优先级，值越小越先调用；默认 100。
        /// </summary>
        [SerializeField] public int Priority;

        /// <summary>
        /// 运行时标记：Type.GetType(TypeName) 失败时为 true（UPM 包已卸载或类名变更）。
        /// Editor 面板红字提示并提供"清理 Missing"按钮；Manager.Initialize 跳过 Missing 条目并记录日志。
        /// 不参与序列化。
        /// </summary>
        [NonSerialized] public bool IsMissing;
    }
}
