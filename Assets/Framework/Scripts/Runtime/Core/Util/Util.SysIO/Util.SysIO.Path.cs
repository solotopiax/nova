/***************************************************************
 * (c) copyright 2026 - 2030, Solotopia
 * All Rights Reserved.
 * -------------------------------------------------------------
 * filename:  Util.SysIO.Path.cs
 * author:    taoye
 * created:   2024/9/27
 * descrip:   系统IO工具-路径相关
 ***************************************************************/

namespace NovaFramework.Runtime
{
    public static partial class Util
    {
        public static partial class SysIO
        {
            public static class Path
            {
                /// <summary>
                /// 路径合并。
                /// </summary>
                /// <param name="paths">路径参数集合。</param>
                /// <returns>字符串。</returns>
                public static string Combine(params string[] paths)
                {
                    if (paths == null || paths.Length == 0)
                    {
                        Log.Fatal(LogTag.SysIO, "Util.SysIO.Path.Combine path 无效。");
                        return null;
                    }

                    return System.IO.Path.Combine(paths).Replace("\\", "/");
                }

                /// <summary>
                /// 获取完整路径。
                /// </summary>
                /// <param name="path">文件或文件夹的路径（可以是相对路径或绝对路径）。</param>
                /// <returns>字符串。</returns>
                public static string GetFullPath(string path)
                {
                    if (string.IsNullOrEmpty(path))
                    {
                        Log.Fatal(LogTag.SysIO, "Util.SysIO.Path.GetFullPath path 无效。");
                        return null;
                    }

                    return System.IO.Path.GetFullPath(path).Replace("\\", "/");
                }
                
                /// <summary>
                /// 获取相对路径。
                /// </summary>
                /// <param name="relativeTo">起始路径（希望相对路径从哪个目录开始计算，通常是一个文件夹路径）。</param>
                /// <param name="path">目标路径（希望获取到的目标文件或文件夹的路径，通常是一个完整的绝对路径）。</param>
                public static string GetRelativePath(string relativeTo, string path)
                {
                    if (string.IsNullOrEmpty(relativeTo))
                    {
                        Log.Fatal(LogTag.SysIO, "Util.SysIO.Path.GetRelativePath relativeTo 无效。");
                        return null;
                    }
                    if (string.IsNullOrEmpty(path))
                    {
                        Log.Fatal(LogTag.SysIO, "Util.SysIO.Path.GetRelativePath path 无效。");
                        return null;
                    }
                    
                    return System.IO.Path.GetRelativePath(relativeTo, path).Replace("\\", "/");
                }

                /// <summary>
                /// 获取路径的扩展名。
                /// </summary>
                /// <param name="path">路径。</param>
                /// <returns>字符串。</returns>
                public static string GetExtension(string path)
                {
                    if (string.IsNullOrEmpty(path))
                    {
                        Log.Fatal(LogTag.SysIO, "Util.SysIO.Path.GetExtension path 无效。");
                        return null;
                    }
                    
                    return System.IO.Path.GetExtension(path);
                }

                /// <summary>
                /// 是否是绝对路径。
                /// </summary>
                /// <param name="path">路径。</param>
                /// <returns>是否是绝对路径。</returns>
                public static bool IsPathRooted(string path)
                {
                    if (string.IsNullOrEmpty(path))
                    {
                        Log.Fatal(LogTag.SysIO, "Util.SysIO.Path.IsPathRooted path 无效。");
                        return false;
                    }

                    return System.IO.Path.IsPathRooted(path);
                }

                /// <summary>
                /// 获取指定路径的目录部分。
                /// </summary>
                /// <param name="path">路径。</param>
                /// <returns>目录路径。</returns>
                public static string GetDirectoryName(string path)
                {
                    if (string.IsNullOrEmpty(path))
                    {
                        Log.Fatal(LogTag.SysIO, "Util.SysIO.Path.GetDirectoryName path 无效。");
                        return null;
                    }

                    return System.IO.Path.GetDirectoryName(path)?.Replace("\\", "/");
                }

                /// <summary>
                /// 获取指定路径的文件名和扩展名。
                /// </summary>
                /// <param name="path">路径。</param>
                /// <returns>文件名和扩展名。</returns>
                public static string GetFileName(string path)
                {
                    if (string.IsNullOrEmpty(path))
                    {
                        Log.Fatal(LogTag.SysIO, "Util.SysIO.Path.GetFileName path 无效。");
                        return null;
                    }

                    return System.IO.Path.GetFileName(path);
                }

                /// <summary>
                /// 获取指定路径的文件名，不包括扩展名。
                /// </summary>
                /// <param name="path">路径。</param>
                /// <returns>文件名（不含扩展名）。</returns>
                public static string GetFileNameWithoutExtension(string path)
                {
                    if (string.IsNullOrEmpty(path))
                    {
                        Log.Fatal(LogTag.SysIO, "Util.SysIO.Path.GetFileNameWithoutExtension path 无效。");
                        return null;
                    }

                    return System.IO.Path.GetFileNameWithoutExtension(path);
                }

                /// <summary>
                /// 平台目录分隔符。
                /// </summary>
                public static readonly char DirectorySeparatorChar = System.IO.Path.DirectorySeparatorChar;

                /// <summary>
                /// PATH 环境变量分隔符。
                /// </summary>
                public static readonly char PathSeparator = System.IO.Path.PathSeparator;

                /// <summary>
                /// 修改路径的扩展名。
                /// </summary>
                /// <param name="path">文件路径。</param>
                /// <param name="extension">新扩展名（含 '.'）。</param>
                /// <returns>更改后的路径。</returns>
                public static string ChangeExtension(string path, string extension)
                {
                    if (string.IsNullOrEmpty(path))
                    {
                        Log.Fatal(LogTag.SysIO, "Util.SysIO.Path.ChangeExtension path 无效。");
                        return null;
                    }

                    return System.IO.Path.ChangeExtension(path, extension);
                }

            }
        }
    }
    
    
}


