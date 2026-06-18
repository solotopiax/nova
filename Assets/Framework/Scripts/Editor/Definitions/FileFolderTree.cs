/***************************************************************
 * (c) copyright 2026 - 2030, Solotopia
 * All Rights Reserved.
 * -------------------------------------------------------------
 * filename:  FileFolderTree.cs
 * author:    taoye
 * created:   2026/2/6
 * descrip:   文件目录树构建工具
 ***************************************************************/

using System.Collections.Generic;
using NovaFramework.Runtime;

namespace NovaFramework.Editor
{
    /// <summary>
    /// 文件目录树构建工具：扫描目录下指定扩展名的文件并构建层级目录树结构，支持排除特定前缀或模式的文件。
    /// </summary>
    public static class FileFolderTree
    {
        /// <summary>
        /// 根据根目录与所有文件完整路径构建层级目录树：每个目录层级对应一个 TreeNode，叶子为挂在该节点上的文件列表。
        /// </summary>
        /// <param name="rootPath">根目录路径（建议已去除末尾斜杠）。</param>
        /// <param name="fileFullPaths">所有文件的完整路径数组。</param>
        /// <returns>根节点（SegmentName 为空，FullPath 为 rootPath）；根节点下为按相对路径拆分的子节点与文件列表。</returns>
        public static TreeNode BuildTree(string rootPath, string[] fileFullPaths)
        {
            string rootNorm = rootPath.TrimEnd('/', '\\');
            var root = new TreeNode { SegmentName = "", FullPath = rootPath };
            foreach (string fullPath in fileFullPaths)
            {
                string relative = Util.SysIO.Path.GetRelativePath(rootNorm, fullPath);
                if (string.IsNullOrEmpty(relative))
                {
                    continue;
                }
                relative = relative.Replace('\\', '/');
                string[] parts = relative.Split('/');
                if (parts.Length == 0)
                {
                    continue;
                }
                TreeNode current = root;
                for (int i = 0; i < parts.Length - 1; i++)
                {
                    string segment = parts[i];
                    string parentPath = current.FullPath;
                    string childFullPath = string.IsNullOrEmpty(parentPath) ? segment : Util.SysIO.Path.Combine(parentPath, segment);
                    if (!current.ChildIndex.TryGetValue(segment, out TreeNode child))
                    {
                        child = new TreeNode { SegmentName = segment, FullPath = childFullPath };
                        current.Children.Add(child);
                        current.ChildIndex[segment] = child;
                    }
                    current = child;
                }
                current.FileFullPaths.Add(fullPath);
            }
            return root;
        }

        /// <summary>
        /// 目录树节点：表示一层目录，包含当前段名、完整路径、子节点列表以及直接挂在该节点下的文件完整路径列表（不含子目录中的文件）。
        /// </summary>
        public sealed class TreeNode
        {
            /// <summary>
            /// 当前层目录名（相对父节点的段名）；根节点为空串。
            /// </summary>
            public string SegmentName;

            /// <summary>
            /// 当前节点对应的目录完整路径。
            /// </summary>
            public string FullPath;

            /// <summary>
            /// 子目录节点列表。
            /// </summary>
            public readonly List<TreeNode> Children = new List<TreeNode>();

            /// <summary>
            /// 子节点名称索引，用于 O(1) 查找避免线性扫描。
            /// </summary>
            internal readonly Dictionary<string, TreeNode> ChildIndex = new Dictionary<string, TreeNode>();

            /// <summary>
            /// 直接属于当前目录（不含子目录）的文件完整路径列表。
            /// </summary>
            public readonly List<string> FileFullPaths = new List<string>();

            /// <summary>
            /// 递归统计当前节点及其所有子节点下的文件总数。
            /// </summary>
            /// <returns>文件总数。</returns>
            public int TotalFileCount()
            {
                int n = FileFullPaths.Count;
                foreach (var c in Children)
                {
                    n += c.TotalFileCount();
                }
                return n;
            }
        }
    }
}
