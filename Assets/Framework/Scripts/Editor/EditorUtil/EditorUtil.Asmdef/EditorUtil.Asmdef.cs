/***************************************************************
 * (c) copyright 2026 - 2030, Solotopia
 * All Rights Reserved.
 * -------------------------------------------------------------
 * filename:  EditorUtil.Asmdef.cs
 * author:    taoye
 * created:   2026/4/7
 * descrip:   asmdef 命名空间解析工具
 ***************************************************************/

using System;
using Newtonsoft.Json.Linq;
using NovaFramework.Runtime;

namespace NovaFramework.Editor
{
    public static partial class EditorUtil
    {
        /// <summary>
        /// Assembly Definition 命名空间解析工具。
        /// 从指定路径向上逐级查找 .asmdef 文件并提取命名空间。
        /// </summary>
        public static class Asmdef
        {
            /// <summary>
            /// 从指定路径所在目录开始向上逐级查找 .asmdef 文件，
            /// 优先返回 rootNamespace 字段，若为空则返回 name 字段作为命名空间。
            /// </summary>
            /// <param name="path">起始文件或目录路径。</param>
            /// <returns>解析到的命名空间字符串。</returns>
            /// <exception cref="InvalidOperationException">未找到 .asmdef 文件时抛出。</exception>
            public static string ResolveNamespace(string path)
            {
                string fullPath = Util.SysIO.Path.GetFullPath(path).Replace('\\', '/');
                string dir = Util.SysIO.Directory.Exists(fullPath) ? fullPath : Util.SysIO.Path.GetDirectoryName(fullPath);
                dir = dir?.Replace('\\', '/');

                string projectRoot = FindProjectRoot(dir);

                while (!string.IsNullOrEmpty(dir) && dir.Length >= projectRoot.Length)
                {
                    string[] asmdefFiles = Util.SysIO.Directory.GetFiles(dir, "*.asmdef");
                    if (asmdefFiles != null && asmdefFiles.Length > 0)
                    {
                        return ParseNamespaceFromAsmdef(asmdefFiles[0]);
                    }

                    dir = Util.SysIO.Path.GetDirectoryName(dir)?.Replace('\\', '/');
                }

                throw new InvalidOperationException($"未找到 .asmdef 文件，路径: {path}");
            }

            /// <summary>
            /// 查找包含 Assets 目录的项目根路径。
            /// </summary>
            /// <param name="dir">起始目录路径。</param>
            /// <returns>项目根目录路径。</returns>
            /// <exception cref="InvalidOperationException">未找到项目根目录时抛出。</exception>
            private static string FindProjectRoot(string dir)
            {
                string current = dir;
                while (!string.IsNullOrEmpty(current))
                {
                    if (Util.SysIO.Directory.Exists(Util.SysIO.Path.Combine(current, "Assets")))
                    {
                        return current.Replace('\\', '/');
                    }
                    string parent = Util.SysIO.Path.GetDirectoryName(current);
                    if (parent == current)
                    {
                        break;
                    }
                    current = parent;
                }

                throw new InvalidOperationException($"未找到包含 Assets 的项目根目录，路径: {dir}");
            }

            /// <summary>
            /// 解析 .asmdef 文件中的命名空间，优先取 rootNamespace，其次取 name。
            /// </summary>
            /// <param name="asmdefPath">.asmdef 文件路径。</param>
            /// <returns>命名空间字符串。</returns>
            private static string ParseNamespaceFromAsmdef(string asmdefPath)
            {
                string json = Util.SysIO.File.ReadAllTextSync(asmdefPath);

                JObject jObject;
                try
                {
                    jObject = JObject.Parse(json);
                }
                catch (Exception e)
                {
                    throw new InvalidOperationException($"解析 .asmdef 文件 JSON 失败: {asmdefPath}，{e.Message}", e);
                }

                string rootNamespace = jObject["rootNamespace"]?.ToString();
                if (!string.IsNullOrEmpty(rootNamespace))
                {
                    return rootNamespace;
                }

                string name = jObject["name"]?.ToString();
                if (!string.IsNullOrEmpty(name))
                {
                    return name;
                }

                throw new InvalidOperationException($".asmdef 文件中 rootNamespace 和 name 均为空: {asmdefPath}");
            }
        }
    }
}
