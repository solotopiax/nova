/***************************************************************
 * (c) copyright 2026 - 2030, Solotopia
 * All Rights Reserved.
 * -------------------------------------------------------------
 * filename:  PipifyStepInfo.cs
 * author:    taoye
 * created:   2026/5/10
 * descrip:   PipifyRegistry 中一个 Step 的元信息记录
 ***************************************************************/

using System;
using System.Reflection;

namespace NovaFramework.Editor
{
    /// <summary>
    /// PipifyRegistry 中一个 Step 的元信息记录。
    /// Registry 扫描一次后缓存；Runner 通过 StepId 查找、拿到 Method 反射调用。
    /// </summary>
    public sealed class PipifyStepInfo
    {
        /// <summary>
        /// 稳定 Id。
        /// </summary>
        public string Id { get; }

        /// <summary>
        /// UI 展示名。
        /// </summary>
        public string DisplayName { get; }

        /// <summary>
        /// UI 分组名。
        /// </summary>
        public string Category { get; }

        /// <summary>
        /// 参数类型，null 表示无参。
        /// </summary>
        public Type ParamsType { get; }

        /// <summary>
        /// 反射调用入口（静态方法）。
        /// </summary>
        public MethodInfo Method { get; }

        /// <summary>
        /// 构造记录。
        /// </summary>
        /// <param name="id">稳定 Id。</param>
        /// <param name="displayName">UI 展示名。</param>
        /// <param name="category">UI 分组名。</param>
        /// <param name="paramsType">参数类型，可为 null。</param>
        /// <param name="method">反射入口方法。</param>
        public PipifyStepInfo(string id, string displayName, string category, Type paramsType, MethodInfo method)
        {
            Id = id;
            DisplayName = displayName;
            Category = category;
            ParamsType = paramsType;
            Method = method;
        }
    }
}
