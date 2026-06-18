/***************************************************************
 * (c) copyright 2026 - 2030, Solotopia
 * All Rights Reserved.
 * -------------------------------------------------------------
 * filename:  Util.SysIO.Directory.cs
 * author:    taoye
 * created:   2024/9/24
 * descrip:   系统IO工具-目录相关
 ***************************************************************/

using System.IO;

namespace NovaFramework.Runtime
{
    public static partial class Util
    {
        public static partial class SysIO
        {
            public static class Directory
            {
                /// <summary>
                /// 创建目录。
                /// </summary>
                /// <param name="directoryPath">目录路径。</param>
                public static System.IO.DirectoryInfo Create(string directoryPath)
                {
                    if (string.IsNullOrEmpty(directoryPath))
                    {
                        Log.Fatal(LogTag.SysIO, "Util.SysIO.Directory.Create directoryPath 无效。");
                        return null;
                    }

                    if (!SysIO.Directory.Exists(directoryPath))
                    {
                        var info = System.IO.Directory.CreateDirectory(directoryPath);
                        SysIO.WebGLSyncFs();
                        return info;
                    }

                    return null;
                }
                
                /// <summary>
                /// 创建目录（如果不存在）。
                /// </summary>
                /// <param name="directoryPath">目录路径。</param>
                public static System.IO.DirectoryInfo CreateIfNotExist(string directoryPath)
                {
                    if (string.IsNullOrEmpty(directoryPath))
                    {
                        Log.Fatal(LogTag.SysIO, "Util.SysIO.Directory.CreateIfNotExist directoryPath 无效。");
                        return null;
                    }

                    if (!SysIO.Directory.Exists(directoryPath))
                    {
                        return SysIO.Directory.Create(directoryPath);
                    }

                    return null;
                }
                
                /// <summary>
                /// 删除目录。
                /// </summary>
                /// <param name="directoryPath">目录路径。</param>
                /// <param name="recursive">是否递归删除。
                /// true：递归删除该目录及其所有子目录和文件。
                /// false：如果目录包含任何文件或子目录，则抛出异常。
                /// </param>
                public static void Delete(string directoryPath, bool recursive = true)
                {
                    if (string.IsNullOrEmpty(directoryPath))
                    {
                        Log.Fatal(LogTag.SysIO, "Util.SysIO.Directory.Delete directoryPath 无效。");
                        return;
                    }

                    if (SysIO.Directory.Exists(directoryPath))
                    {
                        System.IO.Directory.Delete(directoryPath, recursive);
                        string metaFilePath = $"{directoryPath}.meta";
                        if (SysIO.File.Exists(metaFilePath))
                        {
                            System.IO.File.Delete(metaFilePath);
                        }
                        SysIO.WebGLSyncFs();
                    }
                }

                /// <summary>
                /// 拷贝目录。
                /// </summary>
                /// <param name="sourceDirectoryPath">源目录。</param>
                /// <param name="destDirectoryPath">目标目录。</param>
                public static void Copy(string sourceDirectoryPath, string destDirectoryPath)
                {
                    if (string.IsNullOrEmpty(sourceDirectoryPath))
                    {
                        Log.Fatal(LogTag.SysIO, "Util.SysIO.Directory.Copy sourceDirectoryPath 无效。");
                        return;
                    }
                    if (string.IsNullOrEmpty(destDirectoryPath))
                    {
                        Log.Fatal(LogTag.SysIO, "Util.SysIO.Directory.Copy destDirectoryPath 无效。");
                        return;
                    }
                    
                    SysIO.Directory.CreateIfNotExist(destDirectoryPath);

                    string[] folderPaths = SysIO.Directory.GetDirectories(sourceDirectoryPath, "*", SearchOption.AllDirectories);
                    foreach (string folderPath in folderPaths)
                    {
                        SysIO.Directory.CreateIfNotExist(folderPath.Replace(sourceDirectoryPath, destDirectoryPath));
                    }

                    string[] filePaths = SysIO.Directory.GetFiles(sourceDirectoryPath, "*.*", SearchOption.AllDirectories);
                    foreach (string filePath in filePaths)
                    {
                        var dirPath = SysIO.Directory.GetPath(filePath);
                        var fileName = SysIO.File.GetName(filePath);
                        string newFilePath = System.IO.Path.Combine(dirPath.Replace(sourceDirectoryPath, destDirectoryPath), fileName).Replace("\\", "/");
                        SysIO.File.Copy(filePath, newFilePath);
                    }
                }
                
                /// <summary>
                /// 移动目录。
                /// </summary>
                /// <param name="sourceDirectoryPath">目录原始位置。</param>
                /// <param name="destDirectoryPath">目录目标位置。</param>
                public static void Move(string sourceDirectoryPath, string destDirectoryPath)
                {
                    if (string.IsNullOrEmpty(sourceDirectoryPath))
                    {
                        Log.Fatal(LogTag.SysIO, "Util.SysIO.Directory.Move sourceDirectoryPath 无效。");
                        return;
                    }
                    if (string.IsNullOrEmpty(destDirectoryPath))
                    {
                        Log.Fatal(LogTag.SysIO, "Util.SysIO.Directory.Move destDirectoryPath 无效。");
                        return;
                    }
                    
                    if (SysIO.Directory.Exists(sourceDirectoryPath))
                    {
                        SysIO.Directory.Delete(destDirectoryPath);
                        System.IO.File.SetAttributes(sourceDirectoryPath, System.IO.FileAttributes.Normal);
                        string parentDirectory = System.IO.Path.GetDirectoryName(destDirectoryPath);
                        if (!string.IsNullOrEmpty(parentDirectory) &&  !SysIO.Directory.Exists(parentDirectory))
                        {
                            System.IO.Directory.CreateDirectory(parentDirectory);
                        }
                        System.IO.Directory.Move(sourceDirectoryPath, destDirectoryPath);

                        string metaSourceFilePath = $"{sourceDirectoryPath}.meta";
                        string metaDestFilePath = $"{destDirectoryPath}.meta";
                        if (SysIO.File.Exists(metaSourceFilePath))
                        {
                            if (SysIO.File.Exists(metaDestFilePath))
                            {
                                System.IO.File.Delete(metaDestFilePath);   
                            }
                            System.IO.File.Move(metaSourceFilePath, metaDestFilePath);
                        }

                        SysIO.WebGLSyncFs();
                    }
                }
                
                /// <summary>
                /// 目录是否存在。
                /// </summary>
                /// <param name="directoryPath">目录路径。</param>
                /// <returns>是否存在。</returns>
                public static bool Exists(string directoryPath)
                {
                    if (string.IsNullOrEmpty(directoryPath))
                    {
                        Log.Fatal(LogTag.SysIO, "Util.SysIO.Directory.Exists directoryPath 无效。");
                        return false;
                    }

                    return System.IO.Directory.Exists(directoryPath);
                }

                /// <summary>
                /// 获取文件夹名称。
                /// </summary>
                /// <returns>文件夹名称。</returns>
                public static string GetName(string directoryPath)
                {
                    if (string.IsNullOrEmpty(directoryPath))
                    {
                        Log.Fatal(LogTag.SysIO, "Util.SysIO.Directory.GetName directoryPath 无效。");
                        return null;
                    }
                    
                    return System.IO.Path.GetFileName(directoryPath);
                }
                
                /// <summary>
                /// 获取指定文件所在目录路径。
                /// </summary>
                /// <param name="filePath">文件路径。</param>
                /// <returns>目录路径。</returns>
                public static string GetPath(string filePath)
                {
                    if (string.IsNullOrEmpty(filePath))
                    {
                        Log.Fatal(LogTag.SysIO, "Util.SysIO.Directory.GetPath filePath 无效。");
                        return null;
                    }
                    
                    return System.IO.Path.GetDirectoryName(filePath).Replace("\\", "/");
                }
                
                /// <summary>
                /// 获取目录文件列表。
                /// </summary>
                /// <param name="directoryPath">目录路径。</param>
                /// <param name="searchPattern">搜索模块。</param>
                /// <param name="searchOption">搜索选项。</param>
                /// <returns>字符串。</returns>
                public static string[] GetFiles(string directoryPath, string searchPattern = "*", SearchOption searchOption = SearchOption.AllDirectories)
                {
                    if (string.IsNullOrEmpty(directoryPath))
                    {
                        Log.Fatal(LogTag.SysIO, "Util.SysIO.Directory.GetFiles 无效。");
                        return null;
                    }

                    if (SysIO.Directory.Exists(directoryPath))
                    {
                        string[] filesPaths = System.IO.Directory.GetFiles(directoryPath, searchPattern, searchOption);
                        for (int index = 0; index < filesPaths.Length; index++)
                        {
                            filesPaths[index] = filesPaths[index].Replace("\\", "/");
                        }
                        return filesPaths;
                    }
                    
                    return null;
                }
                
                /// <summary>
                /// 获取目录列表。
                /// </summary>
                /// <param name="directoryPath">目录路径。</param>
                /// <param name="searchPattern">搜索模块。</param>
                /// <param name="searchOption">搜索选项。</param>
                /// <returns>字符串。</returns>
                public static string[] GetDirectories(string directoryPath, string searchPattern = "*", SearchOption searchOption = SearchOption.AllDirectories)
                {
                    if (string.IsNullOrEmpty(directoryPath))
                    {
                        Log.Fatal(LogTag.SysIO, "Util.SysIO.Directory.GetDirectories 无效。");
                        return null;
                    }

                    if (SysIO.Directory.Exists(directoryPath))
                    {
                        string[] directoriesPaths = System.IO.Directory.GetDirectories(directoryPath, searchPattern, searchOption);
                        for (int index = 0; index < directoriesPaths.Length; index++)
                        {
                            directoriesPaths[index] = directoriesPaths[index].Replace("\\", "/");
                        }
                        return directoriesPaths;
                    }
                    
                    return null;
                }
                
                /// <summary>
                /// 获取目录中所有文件和子目录的路径。
                /// </summary>
                /// <param name="directoryPath">目录路径。</param>
                /// <param name="searchPattern">搜索模块。</param>
                /// <param name="searchOption">搜索选项。</param>
                /// <returns>字符串。</returns>
                public static string[] GetFileSystemEntries(string directoryPath, string searchPattern = "*", System.IO.SearchOption searchOption = SearchOption.AllDirectories)
                {
                    if (string.IsNullOrEmpty(directoryPath))
                    {
                        Log.Fatal(LogTag.SysIO, "Util.SysIO.Directory.GetFileSystemEntries directoryPath 无效。");
                        return null;
                    }

                    if (SysIO.Directory.Exists(directoryPath))
                    {
                        string[] paths = System.IO.Directory.GetFileSystemEntries(directoryPath, searchPattern, searchOption);
                        for (int index = 0; index < paths.Length; index++)
                        {
                            paths[index] = paths[index].Replace("\\", "/");
                        }
                        return paths;
                    }

                    return null;
                }

                /// <summary>
                /// 清空目录内所有文件（保留目录结构）。
                /// </summary>
                /// <param name="directoryPath">目录路径。</param>
                /// <param name="searchPattern">搜索模式。</param>
                /// <param name="searchOption">搜索选项。</param>
                public static void ClearFiles(string directoryPath, string searchPattern = "*.*", System.IO.SearchOption searchOption = System.IO.SearchOption.AllDirectories)
                {
                    if (string.IsNullOrEmpty(directoryPath))
                    {
                        Log.Fatal(LogTag.SysIO, "Util.SysIO.Directory.ClearFiles directoryPath 无效。");
                        return;
                    }

                    if (!Exists(directoryPath))
                    {
                        return;
                    }

                    string[] files = GetFiles(directoryPath, searchPattern, searchOption);
                    if (files == null)
                    {
                        return;
                    }

                    for (int i = 0; i < files.Length; i++)
                    {
                        SysIO.File.Delete(files[i]);
                    }
                }
            }
        }
    }
    

}


