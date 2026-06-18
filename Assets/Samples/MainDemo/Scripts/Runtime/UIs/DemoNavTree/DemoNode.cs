/***************************************************************
 * (c) copyright 2026 - 2030, Solotopia
 * All Rights Reserved.
 * -------------------------------------------------------------
 * filename:  DemoNode.cs
 * author:    taoye
 * created:   2026/05/22
 * descrip:   演示树导航节点 DTO。
 *            职责：承载单个树节点的数据（标题、路径、折叠状态、父子关系），
 *            无 Mono 依赖，不含校验逻辑（谁使用谁校验）。
 ***************************************************************/

using System;
using System.Collections.Generic;

namespace NovaFramework.Samples.Runtime
{
    /// <summary>
    /// 演示树导航节点数据传输对象。
    /// 纯数据容器，无构造校验；调用方负责有效性检查。
    /// </summary>
    public sealed class DemoNode
    {
        /// <summary>
        /// 节点显示名称，如"UI"。
        /// </summary>
        public string Title;

        /// <summary>
        /// 节点全路径，格式为"分类/标题"，用于叶子点击日志输出。
        /// </summary>
        public string Path;

        /// <summary>
        /// 是否为叶子节点（无子节点的终端节点）。
        /// </summary>
        public bool IsLeaf;

        /// <summary>
        /// 节点当前折叠展开状态；仅对分支节点有意义，叶子节点忽略此值。
        /// </summary>
        public bool IsExpanded;

        /// <summary>
        /// 父节点反向指针，根节点为 null，用于 RefreshVisibility 遍历祖先链。
        /// </summary>
        public DemoNode Parent;

        /// <summary>
        /// 子节点列表，叶子节点为 null 或空列表。
        /// </summary>
        public List<DemoNode> Children;

        /// <summary>
        /// 叶子节点点击回调，null 则忽略。
        /// 由 DemoTreeData.MakeViewLeaf 注入，调用 Nova.UI.OpenUIViewAsync 打开对应 View。
        /// </summary>
        public Action OpenCallback;
    }
}
