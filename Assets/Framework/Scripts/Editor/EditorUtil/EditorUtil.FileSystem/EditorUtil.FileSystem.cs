/***************************************************************
 * (c) copyright 2026 - 2030, Solotopia
 * All Rights Reserved.
 * -------------------------------------------------------------
 * filename:  EditorUtil.FileSystem.cs
 * author:    taoye
 * created:   2026/1/14
 * descrip:   编辑器文件系统工具
 ***************************************************************/

using System;
using System.Linq;
using NovaFramework.Runtime;
using UnityEditor;
using UnityEngine;

namespace NovaFramework.Editor
{
    public static partial class EditorUtil
    {
        /// <summary>
        /// 文件系统工具。
        /// </summary>
        public static class FileSystem
        {
            /// <summary>
            /// 打开文件夹。
            /// </summary>
            /// <param name="path">文件夹路径。</param>
            /// <exception cref="Exception"></exception>
            public static void OpenFolder(string path)
            {
                try
                {
                    if (string.IsNullOrEmpty(path))
                    {
                        throw new ArgumentException("路径为空。", nameof(path));
                    }

                    // 如果 path 是文件，取其所在目录
                    if (Util.SysIO.File.Exists(path))
                    {
                        path = Util.SysIO.Path.GetDirectoryName(path);
                    }

                    if (!Util.SysIO.Directory.Exists(path))
                    {
                        throw new ArgumentException($"路径不存在: {path}。", nameof(path));
                    }

                    EditorUtility.RevealInFinder(path + "/");
                }
                catch (Exception e)
                {
                    Log.Error(LogTag.Editor, e.Message);
                }
            }

            /// <summary>
            /// 打开文件。
            /// </summary>
            /// <param name="path">文件路径。</param>
            /// <exception cref="Exception"></exception>
            public static void OpenFile(string path)
            {
                try
                {
                    if (string.IsNullOrEmpty(path))
                    {
                        throw new ArgumentException("路径为空。", nameof(path));
                    }

                    if (!Util.SysIO.File.Exists(path))
                    {
                        throw new ArgumentException($"文件不存在: {path}。", nameof(path));
                    }

                    EditorUtility.OpenWithDefaultApp(path);
                }
                catch (Exception e)
                {
                    Log.Error(LogTag.Editor, e.Message);
                }
            }

            /// <summary>
            /// 获取工程相对目录。
            /// 如果指定路径存在，则返回其所在目录；否则返回工程根目录（相对路径）。
            /// </summary>
            /// <param name="property">参考路径属性。</param>
            /// <returns>工程相对路径（相对路径）。</returns>
            public static string GetProjectRelativePath(SerializedProperty property)
            {
                string defaultDir = string.Empty;
                if (property != null && !string.IsNullOrEmpty(property.stringValue))
                {
                    string path = property.stringValue;
                    string dir = Util.SysIO.Directory.Exists(path) ? path : Util.SysIO.Directory.GetPath(path);
                    if (Util.SysIO.Directory.Exists(dir))
                    {
                        defaultDir = dir;
                    }
                }

                defaultDir = GetProjectRelativePath(defaultDir);

                if (property != null && string.IsNullOrEmpty(property.stringValue))
                {
                    property.stringValue = defaultDir;
                    property.serializedObject.ApplyModifiedProperties();
                }

                return defaultDir;
            }

            /// <summary>
            /// 获取工程相对路径。
            /// </summary>
            /// <param name="path">路径。</param>
            /// <returns>工程相对路径。</returns>
            public static string GetProjectRelativePath(string path)
            {
                if (string.IsNullOrEmpty(path))
                {
                    return string.Empty;
                }

                if (Util.SysIO.Path.IsPathRooted(path))
                {
                    string relativePath = UnityEditor.FileUtil.GetProjectRelativePath(path);
                    if (!string.IsNullOrEmpty(relativePath))
                    {
                        return relativePath.Replace('\\', '/');
                    }
                }

                return path.Replace('\\', '/');
            }

            /// <summary>
            /// 获取工程绝对路径。
            /// </summary>
            /// <param name="path">路径。</param>
            /// <returns>工程绝对路径。</returns>
            public static string GetProjectFullPath(string path)
            {
                if (string.IsNullOrEmpty(path))
                {
                    return string.Empty;
                }

                if (!Util.SysIO.Path.IsPathRooted(path))
                {
                    return Util.SysIO.Path.GetFullPath(path);
                }

                return path.Replace('\\', '/');
            }

            /// <summary>
            /// 递归获取指定目录及其子目录下符合扩展名的所有文件的完整路径，支持排除特定前缀的文件（如临时文件 ~$）。
            /// </summary>
            /// <param name="directoryPath">要扫描的根目录完整路径。</param>
            /// <param name="searchPattern">文件匹配模式（如 "*.xlsx"、"*.json"、"*.*" 等），默认为 "*.*" 匹配所有文件。</param>
            /// <param name="excludePrefix">要排除的文件名前缀（如 "~$"），为空则不排除；默认为 null。</param>
            /// <returns>文件完整路径数组；目录不存在或为空时返回 null。</returns>
            public static string[] GetFiles(string directoryPath, string searchPattern = "*.*", string excludePrefix = null)
            {
                if (string.IsNullOrEmpty(directoryPath) || !Util.SysIO.Directory.Exists(directoryPath))
                {
                    return null;
                }
                string[] files = Util.SysIO.Directory.GetFiles(directoryPath, searchPattern, System.IO.SearchOption.AllDirectories);
                if (files == null || files.Length == 0)
                {
                    return null;
                }
                if (!string.IsNullOrEmpty(excludePrefix))
                {
                    files = files.Where(f => !Util.SysIO.Path.GetFileName(f).StartsWith(excludePrefix)).ToArray();
                }
                return files.Length > 0 ? files : null;
            }

            /// <summary>
            /// 判断指定路径是否位于某个父目录下。
            /// 路径在比较前统一替换反斜杠为正斜杠。
            /// </summary>
            /// <param name="filePath">待检查的文件路径。</param>
            /// <param name="parentDirPath">父目录路径（不含尾部斜杠）。</param>
            /// <returns>filePath 位于 parentDirPath 下时返回 true。</returns>
            public static bool IsSubPathOf(string filePath, string parentDirPath)
            {
                string normalizedFile = filePath.Replace('\\', '/');
                string normalizedParent = parentDirPath.Replace('\\', '/');
                if (!normalizedParent.EndsWith("/"))
                {
                    normalizedParent += "/";
                }
                return normalizedFile.StartsWith(normalizedParent);
            }

            /// <summary>
            /// 解析 Nova 模板文件路径，自动适配消费者（UPM Package）模式与开发者模式。
            /// <para>优先检查 Packages/com.solotopia.nova.framework/Templates/（消费者视角）；</para>
            /// <para>若不存在则回退到 Assets/Framework/Templates/（开发者模式）；</para>
            /// <para>若两者均不存在，返回 Packages 路径作为默认值。</para>
            /// </summary>
            /// <param name="templateFileName">模板文件名（如 "TableListTemplate.xlsx"）。</param>
            /// <returns>相对工程根目录的模板路径。</returns>
            public static string ResolveTemplatePath(string templateFileName)
            {
                const string c_PackageTemplateDirPath = "Packages/com.solotopia.nova.framework/Templates/";
                const string c_AssetsTemplateDirPath = "Assets/Framework/Templates/";

                string packagePath = c_PackageTemplateDirPath + templateFileName;
                string fullPackagePath = Util.SysIO.Path.GetFullPath(packagePath);
                if (Util.SysIO.File.Exists(fullPackagePath))
                {
                    return packagePath;
                }

                string assetsPath = c_AssetsTemplateDirPath + templateFileName;
                string fullAssetsPath = Util.SysIO.Path.GetFullPath(
                    Util.SysIO.Path.Combine(Application.dataPath, "../" + assetsPath));
                if (Util.SysIO.File.Exists(fullAssetsPath))
                {
                    return assetsPath;
                }

                // 两者均不存在，返回 Packages 路径作为默认值。
                return packagePath;
            }

            /// <summary>
            /// 删除指定路径的内容。
            /// 若路径为目录，则清空目录内所有文件（保留目录结构）；若路径为文件，则删除该文件。
            /// 路径不存在时静默跳过。
            /// </summary>
            /// <param name="path">目录路径或文件路径。</param>
            public static void DeletePath(string path)
            {
                if (string.IsNullOrEmpty(path))
                {
                    return;
                }

                if (Util.SysIO.Directory.Exists(path))
                {
                    string[] files = Util.SysIO.Directory.GetFiles(path, "*.*", System.IO.SearchOption.AllDirectories);
                    if (files != null)
                    {
                        foreach (string file in files)
                        {
                            Util.SysIO.File.Delete(file);
                        }
                    }
                }
                else if (Util.SysIO.File.Exists(path))
                {
                    Util.SysIO.File.Delete(path);
                }
            }

            /// <summary>
            /// 在工程根目录下查找第一个 .sln 文件，返回其绝对路径；未找到则返回空字符串。
            /// </summary>
            /// <returns>.sln 文件的绝对路径，未找到时返回空字符串。</returns>
            public static string GetScriptsProjectFilePath()
            {
                string projectRoot = Util.SysIO.Path.GetFullPath(Util.SysIO.Path.Combine(Application.dataPath, ".."));
                string[] files = Util.SysIO.Directory.GetFiles(projectRoot, "*.sln", System.IO.SearchOption.TopDirectoryOnly);
                if (files != null && files.Length > 0)
                {
                    return files[0];
                }
                return string.Empty;
            }

            /// <summary>
            /// 延迟执行 AssetDatabase 刷新（下一帧），避免与 IDE/Unity 更新 .csproj 时产生 Sharing violation。
            /// 建议在批量写入资源后仅调用一次。
            /// </summary>
            public static void RefreshDelayed()
            {
                EditorApplication.delayCall += () => { AssetDatabase.Refresh(); };
            }

            /// <summary>
            /// 使用 SQLiteStudio 可视化工具打开指定 SQLite 数据库文件（仅 Windows Editor）。
            /// 查找路径：{ProjectRoot}/Tools/SQLiteStudio/SQLiteStudio.exe
            /// 若未找到，弹出引导弹窗提示用户下载并放置到 Tools/SQLiteStudio/ 目录。
            /// </summary>
            /// <param name="databasePath">数据库文件的绝对路径。</param>
            public static void OpenSQLiteStudio(string databasePath)
            {
#if UNITY_EDITOR_WIN
                try
                {
                    if (string.IsNullOrEmpty(databasePath))
                    {
                        Log.Error(LogTag.Editor, "数据库路径为空。");
                        return;
                    }

                    string exePath = FindSQLiteStudioExe();

                    if (string.IsNullOrEmpty(exePath))
                    {
                        string targetDir = Util.SysIO.Path.GetFullPath(
                            Util.SysIO.Path.Combine(Application.dataPath, "../Tools/SQLiteStudio"));
                        if (EditorUtility.DisplayDialog(
                            "SQLiteStudio 未找到",
                            $"请前往 https://sqlitestudio.pl 下载便携版（ZIP），解压到：\n{targetDir}",
                            "打开下载页面", "取消"))
                        {
                            Application.OpenURL("https://sqlitestudio.pl");
                        }
                        return;
                    }

                    string sanitizedDbPath = Util.SysIO.Path.GetFullPath(databasePath);
                    EditorUtil.ProcessRunner.RunAsync(
                        exePath,
                        Txt.Format("\"{0}\"", sanitizedDbPath),
                        workingDirectory: Util.SysIO.Path.GetDirectoryName(exePath));
                }
                catch (Exception e)
                {
                    Log.Error(LogTag.Editor, e.Message);
                }
#else
                Log.Warning(LogTag.Editor, "SQLiteStudio 可视化工具仅支持 Windows Editor。");
#endif
            }

            /// <summary>
            /// 查找 SQLiteStudio.exe 路径。
            /// 唯一查找路径：{ProjectRoot}/Tools/SQLiteStudio/SQLiteStudio.exe
            /// </summary>
            /// <returns>找到时返回完整路径，否则返回 null。</returns>
            private static string FindSQLiteStudioExe()
            {
                const string c_ExeName = "SQLiteStudio.exe";
                string projectRoot = Util.SysIO.Path.GetFullPath(
                    Util.SysIO.Path.Combine(Application.dataPath, ".."));

                string path = Util.SysIO.Path.Combine(projectRoot, "Tools", "SQLiteStudio", c_ExeName);
                return Util.SysIO.File.Exists(path) ? path : null;
            }
        }   
    }
}
