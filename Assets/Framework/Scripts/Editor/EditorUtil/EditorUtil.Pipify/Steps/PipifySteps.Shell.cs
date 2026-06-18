/***************************************************************
 * (c) copyright 2026 - 2030, Solotopia
 * All Rights Reserved.
 * -------------------------------------------------------------
 * filename:  PipifySteps.Shell.cs
 * author:    taoye
 * created:   2026/5/19
 * descrip:   Pipify 内置 Step 合集 —— 系统外壳分组（1 个 Step）
 ***************************************************************/

using Cysharp.Threading.Tasks;
using NovaFramework.Runtime;
using UnityEditor;
using UnityEngine;

namespace NovaFramework.Editor
{
    /// <summary>
    /// Pipify 内置 Step 合集（partial）。
    /// 本文件收录系统外壳分组的原子操作：
    /// 在系统文件管理器中打开指定目录（macOS Finder / Windows 资源管理器）。
    /// </summary>
    internal static partial class PipifySteps
    {
        /// <summary>
        /// Step：在系统文件管理器中打开目标目录。
        /// 参数 Path 遵循项目根相对路径规范；空字符串或目标不存在时回退到项目根；
        /// 文件路径自动取所在目录。
        /// </summary>
        /// <param name="ctx">Runner 下发的运行时上下文。</param>
        /// <param name="p">打开文件夹参数。</param>
        /// <returns>完成的 UniTask。</returns>
        [PipifyStep("shell.open_folder", "打开文件夹", "系统外壳", ParamsType = typeof(OpenFolderParams))]
        internal static UniTask RunOpenFolder(PipifyContext ctx, OpenFolderParams p)
        {
            string projectRoot = Util.SysIO.Path.GetDirectoryName(Application.dataPath);
            string target = ResolveOpenFolderTarget(p != null ? p.Path : null, projectRoot);
            Log.Debug(LogTag.Editor, "[Pipify] 打开文件夹：{0}", target);
            EditorUtility.RevealInFinder(target);
            return UniTask.CompletedTask;
        }

        /// <summary>
        /// 解析 OpenFolder 目标路径：空/不存在回退项目根；相对路径基于项目根；文件路径取其所在目录。
        /// </summary>
        /// <param name="rawPath">用户配置的原始路径（可能为空、相对、绝对、或指向文件）。</param>
        /// <param name="projectRoot">项目根绝对路径，回退基准。</param>
        /// <returns>实际用于 RevealInFinder 的绝对路径。</returns>
        private static string ResolveOpenFolderTarget(string rawPath, string projectRoot)
        {
            if (string.IsNullOrWhiteSpace(rawPath))
            {
                return projectRoot;
            }

            string absolute = System.IO.Path.IsPathRooted(rawPath)
                ? rawPath
                : Util.SysIO.Path.GetFullPath(Util.SysIO.Path.Combine(projectRoot, rawPath));

            if (Util.SysIO.Directory.Exists(absolute))
            {
                return absolute;
            }
            if (System.IO.File.Exists(absolute))
            {
                return absolute;
            }

            string parent = Util.SysIO.Path.GetDirectoryName(absolute);
            if (!string.IsNullOrEmpty(parent) && Util.SysIO.Directory.Exists(parent))
            {
                Log.Warning(LogTag.Editor, "[Pipify] 目标路径不存在，回退到上级目录：{0}", parent);
                return parent;
            }

            Log.Warning(LogTag.Editor, "[Pipify] 目标路径不存在，回退到项目根：{0}", rawPath);
            return projectRoot;
        }
    }
}
