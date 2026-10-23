/***************************************************************
 * (c) copyright 2026 - 2030, Solotopia
 * All Rights Reserved.
 * -------------------------------------------------------------
 * filename:  HybridClrGenerateInputProjection.cs
 * author:    taoye
 * created:   2026/10/16
 * descrip:   WebGL Generate 的真实回调适配与输入拥有域合同
 ***************************************************************/

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using System.Threading;

namespace NovaFramework.Editor
{
    /// <summary>只从源输入摘要排除本轮准确生成拥有域；父树仍逐文件保护其余后代，任何现存符号链接均拒绝。</summary>
    internal sealed class HybridClrGenerateInputProjection
    {
        /// <summary>精确输出角色，不由用户提供过滤谓词；正式调用方从 SettingsUtil 与当前 Master 计算。</summary>
        [Serializable]
        internal sealed class Spec
        {
            public string projectRoot;
            public string[] directories;
            public string[] files;
            public string[] compiledArtifacts = Array.Empty<string>();
            public string compiledAssemblyName;
        }

        /// <summary>只读源路径投影记录，不把生成输出摘要伪装成未变输入。</summary>
        [Serializable]
        internal sealed class Row
        {
            public string path;
            public string kind;
            public string sha256;
        }

        private readonly string m_Project;
        private readonly string[] m_Directories;
        private readonly string[] m_Files;
        private readonly string[] m_CompiledArtifacts;
        private readonly string m_CompiledAssemblyName;

        /// <summary>验证并保存固定拥有角色；文件角色同时拥有其准确 meta，禁止项目根或关键源被输出覆盖。</summary>
        /// <param name="spec">调用方已由当前设置推导的准确输出目录与文件。</param>
        /// <param name="protectedFiles">场景、Master、Runtime 与配置等必须保留的准确文件。</param>
        internal HybridClrGenerateInputProjection(Spec spec, IEnumerable<string> protectedFiles)
        {
            if (spec == null || spec.directories == null || spec.files == null || protectedFiles == null)
                throw new ArgumentException("Generate 拥有域或受保护源缺失。");
            m_Project = Canonical(spec.projectRoot);
            if (!Directory.Exists(m_Project)) throw new InvalidOperationException("Generate 项目根不存在。");
            RejectLinks(m_Project);
            m_Directories = spec.directories.Select(Canonical).Distinct(StringComparer.Ordinal).OrderBy(value => value, StringComparer.Ordinal).ToArray();
            m_Files = spec.files.SelectMany(value => new[] { Canonical(value), Canonical(value) + ".meta" })
                .Distinct(StringComparer.Ordinal).OrderBy(value => value, StringComparer.Ordinal).ToArray();
            m_CompiledArtifacts = (spec.compiledArtifacts ?? Array.Empty<string>()).Select(Canonical).Distinct(StringComparer.Ordinal).OrderBy(value => value, StringComparer.Ordinal).ToArray();
            m_CompiledAssemblyName = spec.compiledAssemblyName;
            foreach (string output in m_Directories.Concat(m_Files).Concat(m_CompiledArtifacts))
            {
                if (!Within(output, m_Project) || output == m_Project ||
                    new[] { "Assets", "Packages", "ProjectSettings" }.Any(name => output == Path.Combine(m_Project, name)))
                    throw new InvalidOperationException("Generate 输出拥有域不得覆盖项目根或核心输入根：" + output);
                RejectLinks(output);
            }
            ValidateOwnedTrees(CancellationToken.None);
            foreach (string file in protectedFiles.Select(Canonical))
                if (Owns(file)) throw new InvalidOperationException("Generate 输出与受保护源相交：" + file);
            foreach (string directory in m_Directories)
                if (m_Files.Contains(directory, StringComparer.Ordinal)) throw new InvalidOperationException("Generate 目录与文件角色冲突。");
        }

        /// <summary>只按路径分量判断准确文件或目录后代，邻接前缀不获得排除权。</summary>
        /// <param name="path">待分类的路径。</param>
        /// <returns>路径属于固定拥有域时为 true。</returns>
        internal bool Owns(string path)
        {
            string full = Canonical(path);
            return m_Files.Contains(full, StringComparer.Ordinal) || m_CompiledArtifacts.Contains(full, StringComparer.Ordinal) || m_Directories.Any(root => Within(full, root));
        }

        /// <summary>复制准确拥有角色供回执持久化；不向调用方暴露内部数组。</summary>
        /// <returns>各数组的独立副本及准确生成程序集名。</returns>
        internal Spec ExportSpec() => new Spec { projectRoot = m_Project, directories = m_Directories.ToArray(), files = m_Files.Where(path => !path.EndsWith(".meta", StringComparison.Ordinal)).ToArray(), compiledArtifacts = m_CompiledArtifacts.ToArray(), compiledAssemblyName = m_CompiledAssemblyName };

        /// <summary>取得稳定角色材料，用于确认设置仍推导出相同拥有域；不记录当前生成物内容。</summary>
        /// <returns>项目根、目录角色和文件角色的确定性文本。</returns>
        internal string Identity() => m_Project + "\nD\n" + string.Join("\n", m_Directories) + "\nF\n" + string.Join("\n", m_Files) + "\nC\n" + m_CompiledAssemblyName + "\n" + string.Join("\n", m_CompiledArtifacts);

        /// <summary>读取根集合的保留内容；即使源根是生成目录的祖先，也只排除精确拥有子树。</summary>
        /// <param name="roots">原输入捕获已经列出的文件或目录根。</param>
        /// <param name="token">枚举和读取文件时观察的取消令牌。</param>
        /// <returns>排除输出角色后的稳定源记录；输出角色自身不产生输入行。</returns>
        internal Row[] Capture(IEnumerable<string> roots, CancellationToken token)
        {
            if (roots == null) throw new ArgumentNullException(nameof(roots));
            ValidateOwnedTrees(token);
            var rows = new List<Row>();
            foreach (string root in roots.Select(Canonical).Distinct(StringComparer.Ordinal).OrderBy(value => value, StringComparer.Ordinal))
            {
                token.ThrowIfCancellationRequested();
                RejectLinks(root);
                if (Owns(root)) continue;
                bool scaffold = IsScaffold(root);
                if (File.Exists(root))
                {
                    if (scaffold) throw new InvalidOperationException("生成拥有域祖先出现文件冲突：" + root);
                    rows.Add(new Row { path = root, kind = "file", sha256 = Hash(File.ReadAllBytes(root)) });
                    continue;
                }
                if (!Directory.Exists(root))
                {
                    rows.Add(new Row { path = root, kind = scaffold ? "directory-projection" : "absent", sha256 = scaffold ? Hash(Array.Empty<byte>()) : null });
                    continue;
                }
                var content = new List<string>();
                var pending = new Stack<string>(); pending.Push(root);
                while (pending.Count != 0)
                {
                    token.ThrowIfCancellationRequested();
                    string current = pending.Pop();
                    foreach (string child in Directory.GetDirectories(current, "*", SearchOption.TopDirectoryOnly))
                    {
                        RejectLinks(child);
                        if (Owns(child)) continue;
                        if (!IsScaffold(child)) content.Add("D|" + Path.GetRelativePath(root, child));
                        pending.Push(child);
                    }
                    foreach (string file in Directory.GetFiles(current, "*", SearchOption.TopDirectoryOnly))
                    {
                        token.ThrowIfCancellationRequested(); RejectLinks(file);
                        if (Owns(file)) continue;
                        if (IsScaffold(file)) throw new InvalidOperationException("生成目录祖先出现文件冲突：" + file);
                        content.Add("F|" + Path.GetRelativePath(root, file) + "|" + Hash(File.ReadAllBytes(file)));
                    }
                }
                rows.Add(new Row { path = root, kind = scaffold ? "directory-projection" : "directory", sha256 = Hash(Encoding.UTF8.GetBytes(string.Join("\n", content.OrderBy(value => value, StringComparer.Ordinal)))) });
            }
            return rows.ToArray();
        }

        /// <summary>允许创建输出所需的准确祖先目录脚手架；其任何非拥有后代仍参与摘要。</summary>
        /// <param name="path">当前准确路径。</param>
        /// <returns>它是固定输出的祖先目录时为 true。</returns>
        private bool IsScaffold(string path) => m_Directories.Concat(m_Files).Concat(m_CompiledArtifacts).Any(output => output != path && Within(output, path));

        /// <summary>规范化非空路径文本，不解析或接受符号链接。</summary>
        /// <param name="path">非空路径。</param>
        /// <returns>绝对且无尾部分隔符的路径。</returns>
        private static string Canonical(string path)
        {
            if (string.IsNullOrWhiteSpace(path)) throw new ArgumentException("Generate 路径为空。");
            return Path.GetFullPath(path).TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
        }

        /// <summary>按精确分量比较包含关系。</summary>
        /// <param name="path">候选路径。</param>
        /// <param name="root">准确根路径。</param>
        /// <returns>候选为根或其后代时为 true。</returns>
        private static bool Within(string path, string root) => path == root || path.StartsWith(root + Path.DirectorySeparatorChar, StringComparison.Ordinal);

        /// <summary>排除拥有域之前遍历全部现存输出后代，只枚举准确树且绝不跟随链接。</summary>
        /// <param name="token">逐项枚举时观察的取消令牌。</param>
        /// <returns>无返回值；目录、文件或悬空链接均以异常拒绝。</returns>
        internal void ValidateOwnedTrees(CancellationToken token)
        {
            foreach (string file in m_Files.Concat(m_CompiledArtifacts)) { token.ThrowIfCancellationRequested(); RejectLinks(file); if (Directory.Exists(file)) throw new InvalidOperationException("生成文件角色出现目录：" + file); }
            foreach (string root in m_Directories)
            {
                token.ThrowIfCancellationRequested(); RejectLinks(root);
                if (File.Exists(root)) throw new InvalidOperationException("生成目录角色出现文件：" + root);
                if (!Directory.Exists(root)) continue;
                var pending = new Stack<string>(); pending.Push(root);
                while (pending.Count != 0)
                    foreach (string entry in Directory.EnumerateFileSystemEntries(pending.Pop()))
                    {
                        token.ThrowIfCancellationRequested(); RejectLinks(entry);
                        if (Directory.Exists(entry)) pending.Push(entry);
                    }
            }
        }

        /// <summary>拒绝每个祖先和叶子的链接；GetAttributes 直接检查悬空链接，不先以 Exists 遗漏它。</summary>
        /// <param name="path">待检查路径。</param>
        /// <returns>无返回值；缺失的普通叶子允许由官方创建，任一链接被拒绝。</returns>
        private static void RejectLinks(string path)
        {
            for (string current = path; !string.IsNullOrEmpty(current); current = Path.GetDirectoryName(current))
            {
                try
                {
                    if ((File.GetAttributes(current) & FileAttributes.ReparsePoint) != 0)
                        throw new InvalidOperationException("Generate 输入或输出包含符号链接：" + current);
                }
                catch (FileNotFoundException) { }
                catch (DirectoryNotFoundException) { }
            }
        }

        /// <summary>计算只读内容摘要。</summary>
        /// <param name="bytes">实际读取的字节。</param>
        /// <returns>SHA-256 小写十六进制。</returns>
        private static string Hash(byte[] bytes)
        {
            using (SHA256 sha = SHA256.Create()) return BitConverter.ToString(sha.ComputeHash(bytes)).Replace("-", "").ToLowerInvariant();
        }
    }
}
