/***************************************************************
 * (c) copyright 2026 - 2030, Solotopia
 * All Rights Reserved.
 * -------------------------------------------------------------
 * filename:  PipifyStepAttribute.cs
 * author:    taoye
 * created:   2026/5/10
 * descrip:   标记静态方法为 Pipify 原子操作的特性
 ***************************************************************/

using System;

namespace NovaFramework.Editor
{
    /// <summary>
    /// 标记一个静态方法为 Pipify 原子操作（Step）。
    /// 被 PipifyRegistry 通过 TypeCache 扫描注册；Id 在整个工程中必须唯一。
    /// </summary>
    [AttributeUsage(AttributeTargets.Method, Inherited = false, AllowMultiple = false)]
    public sealed class PipifyStepAttribute : Attribute
    {
        /// <summary>
        /// 稳定 Id（重命名 DisplayName 不影响存档）。
        /// </summary>
        public string Id { get; }

        /// <summary>
        /// UI 展示名。
        /// </summary>
        public string DisplayName { get; }

        /// <summary>
        /// UI 分组名（如 "HybridCLR" / "导出" / "打包"）。
        /// </summary>
        public string Category { get; }

        /// <summary>
        /// 参数类型（[Serializable] class，可由 Util.Json 序列化）。null 表示无参。
        /// 使用命名参数赋值：[PipifyStep("id", "名", "组", ParamsType = typeof(XxxParams))]
        /// </summary>
        public Type ParamsType { get; set; }

        /// <summary>
        /// 构造标注特性。
        /// </summary>
        /// <param name="id">稳定 Id。</param>
        /// <param name="displayName">UI 展示名。</param>
        /// <param name="category">UI 分组名。</param>
        public PipifyStepAttribute(string id, string displayName, string category)
        {
            Id = id;
            DisplayName = displayName;
            Category = category;
        }
    }
}
