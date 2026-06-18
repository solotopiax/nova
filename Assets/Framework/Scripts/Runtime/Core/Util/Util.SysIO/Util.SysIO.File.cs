/***************************************************************
 * (c) copyright 2026 - 2030, Solotopia
 * All Rights Reserved.
 * -------------------------------------------------------------
 * filename:  Util.SysIO.File.cs
 * author:    taoye
 * created:   2024/9/24
 * descrip:   系统IO工具-文件相关
 ***************************************************************/

using System.IO;
using System.Threading.Tasks;
using Cysharp.Threading.Tasks;
using UnityEngine;

namespace NovaFramework.Runtime
{
    public static partial class Util
    {
        public static partial class SysIO
        {
            public static class File
            {
                /// <summary>
                /// 异步方式读取文件字节流内容。
                /// </summary>
                /// <param name="filePath">文件路径。</param>
                /// <returns>字节流。</returns>
                public static async UniTask<byte[]> ReadAllBytesAsync(string filePath)
                {
                    if (string.IsNullOrEmpty(filePath))
                    {
                        Log.Fatal(LogTag.SysIO, "Util.SysIO.File.ReadAllBytesAsync filePath 无效。");
                        return null;
                    }

                    if (SysIO.File.Exists(filePath))
                    {
                        if (Application.platform == RuntimePlatform.WebGLPlayer)
                        {
                            return SysIO.File.ReadAllBytesSync(filePath);
                        }
                        using (var fileStream = SysIO.File.OpenRead(filePath))
                        {
                            long length = fileStream.Length;
                            if (length > int.MaxValue)
                            {
                                Log.Fatal(LogTag.SysIO, "Util.SysIO.File.ReadAllBytesAsync 文件过大，超过 2GB 限制。filePath={0}", filePath);
                                return null;
                            }

                            var fileData = new byte[length];
                            int bytesRead = 0;
                            int remaining = (int)length;
                            while (remaining > 0)
                            {
                                int read = await fileStream.ReadAsync(fileData, bytesRead, remaining);
                                if (read == 0) break;
                                bytesRead += read;
                                remaining -= read;
                            }
                            return fileData;
                        }
                    }
                    
                    return null;
                }

                /// <summary>
                /// 异步方式写入字节流内容到文件。
                /// </summary>
                /// <param name="filePath">文件路径。</param>
                /// <param name="fileData">字节流数据。</param>
                public static async UniTask WriteAllBytesAsync(string filePath, byte[] fileData)
                {
                    if (string.IsNullOrEmpty(filePath))
                    {
                        Log.Fatal(LogTag.SysIO, "Util.SysIO.File.WriteAllBytesAsync filePath 无效。");
                        return;
                    }

                    // 检查并创建目录
                    string directoryPath = SysIO.Path.GetDirectoryName(filePath);
                    if (!string.IsNullOrEmpty(directoryPath))
                    {
                        SysIO.Directory.CreateIfNotExist(directoryPath);
                    }

                    if (Application.platform == RuntimePlatform.WebGLPlayer)
                    {
                        SysIO.File.WriteAllBytesSync(filePath, fileData);
                    }
                    else
                    {
                        if (fileData != null)
                        {
                            using (var fileStream = new System.IO.FileStream(filePath, System.IO.FileMode.Create, System.IO.FileAccess.Write))
                            {
                                await fileStream.WriteAsync(fileData, 0, fileData.Length);
                            }
                        }
                    }
                    
                    SysIO.WebGLSyncFs();
                }
                
                /// <summary>
                /// 异步方式读取文件文本内容。
                /// </summary>
                /// <param name="filePath">文件路径。</param>
                /// <returns>文本。</returns>
                public static async UniTask<string> ReadAllTextAsync(string filePath)
                {
                    if (string.IsNullOrEmpty(filePath))
                    {
                        Log.Fatal(LogTag.SysIO, "Util.SysIO.File.ReadAllTextAsync filePath 无效。");
                        return null;
                    }

                    if (SysIO.File.Exists(filePath))
                    {
                        if (Application.platform == RuntimePlatform.WebGLPlayer)
                        {
                            return SysIO.File.ReadAllTextSync(filePath);
                        }
                        using (var stream = SysIO.File.OpenText(filePath))
                        {
                            return await stream.ReadToEndAsync();
                        }    
                    }

                    return null;
                }
                
                /// <summary>
                /// 异步方式写入文本内容到文件。
                /// </summary>
                /// <param name="filePath">文件路径。</param>
                /// <param name="fileText">文本内容。</param>
                public static async UniTask WriteAllTextAsync(string filePath, string fileText)
                {
                    if (string.IsNullOrEmpty(filePath))
                    {
                        Log.Fatal(LogTag.SysIO, "Util.SysIO.File.WriteAllTextAsync filePath 无效。");
                        return;
                    }

                    // 检查并创建目录
                    string directoryPath = SysIO.Path.GetDirectoryName(filePath);
                    if (!string.IsNullOrEmpty(directoryPath))
                    {
                        SysIO.Directory.CreateIfNotExist(directoryPath);
                    }

                    if (Application.platform == RuntimePlatform.WebGLPlayer)
                    {
                        SysIO.File.WriteAllTextSync(filePath, fileText);
                    }
                    else
                    {
                        using (var stream = SysIO.File.CreateText(filePath))
                        {
                            await stream.WriteAsync(fileText);
                        }
                    }
                    
                    SysIO.WebGLSyncFs();
                }
                
                /// <summary>
                /// 同步方式读取文件字节流内容。
                /// </summary>
                /// <param name="filePath">文件路径。</param>
                /// <returns>字节流。</returns>
                public static byte[] ReadAllBytesSync(string filePath)
                {
                    if (string.IsNullOrEmpty(filePath))
                    {
                        Log.Fatal(LogTag.SysIO, "Util.SysIO.File.ReadAllBytesSync filePath 无效。");
                        return null;
                    }

                    if (SysIO.File.Exists(filePath))
                    {
                        return System.IO.File.ReadAllBytes(filePath);  
                    }
                    
                    return null;
                }

                /// <summary>
                /// 同步方式写入字节流内容到文件。
                /// </summary>
                /// <param name="filePath">文件路径。</param>
                /// <param name="fileData">字节流数据。</param>
                public static void WriteAllBytesSync(string filePath, byte[] fileData)
                {
                    if (string.IsNullOrEmpty(filePath))
                    {
                        Log.Fatal(LogTag.SysIO, "Util.SysIO.File.WriteAllBytesSync filePath 无效。");
                        return;
                    }

                    // 检查并创建目录
                    string directoryPath = SysIO.Path.GetDirectoryName(filePath);
                    if (!string.IsNullOrEmpty(directoryPath))
                    {
                        SysIO.Directory.CreateIfNotExist(directoryPath);
                    }
                    
                    System.IO.File.WriteAllBytes(filePath, fileData);
                    
                    SysIO.WebGLSyncFs();
                }
                
                /// <summary>
                /// 同步方式读取文件文本内容。
                /// </summary>
                /// <param name="filePath">文件路径。</param>
                /// <returns>文本。</returns>
                public static string ReadAllTextSync(string filePath)
                {
                    if (string.IsNullOrEmpty(filePath))
                    {
                        Log.Fatal(LogTag.SysIO, "Util.SysIO.File.ReadAllTextSync filePath 无效。");
                        return null;
                    }

                    if (SysIO.File.Exists(filePath))
                    {
                        return System.IO.File.ReadAllText(filePath, new System.Text.UTF8Encoding(false));   
                    }

                    return null;
                }
                
                /// <summary>
                /// 同步方式写入文本内容到文件。
                /// </summary>
                /// <param name="filePath">文件路径。</param>
                /// <param name="fileText">文本内容。</param>
                public static void WriteAllTextSync(string filePath, string fileText)
                {
                    if (string.IsNullOrEmpty(filePath))
                    {
                        Log.Fatal(LogTag.SysIO, "Util.SysIO.File.WriteAllTextSync filePath 无效。");
                        return;
                    }

                    // 检查并创建目录
                    string directoryPath = SysIO.Path.GetDirectoryName(filePath);
                    if (!string.IsNullOrEmpty(directoryPath))
                    {
                        SysIO.Directory.CreateIfNotExist(directoryPath);
                    }

                    System.IO.File.WriteAllText(filePath, fileText, new System.Text.UTF8Encoding(false));

                    SysIO.WebGLSyncFs();
                }

                /// <summary>
                /// 同步读取文件全部文本（指定编码）。
                /// </summary>
                /// <param name="filePath">文件路径。</param>
                /// <param name="encoding">读取编码。</param>
                /// <returns>文本内容；文件不存在时返回 null。</returns>
                public static string ReadAllTextSync(string filePath, System.Text.Encoding encoding)
                {
                    if (string.IsNullOrEmpty(filePath))
                    {
                        Log.Fatal(LogTag.SysIO, "Util.SysIO.File.ReadAllTextSync filePath 无效。");
                        return null;
                    }

                    if (SysIO.File.Exists(filePath))
                    {
                        return System.IO.File.ReadAllText(filePath, encoding);
                    }

                    return null;
                }

                /// <summary>
                /// 同步写入文件全部文本（指定编码）。
                /// </summary>
                /// <param name="filePath">文件路径。</param>
                /// <param name="fileText">文本内容。</param>
                /// <param name="encoding">写入编码。</param>
                public static void WriteAllTextSync(string filePath, string fileText, System.Text.Encoding encoding)
                {
                    if (string.IsNullOrEmpty(filePath))
                    {
                        Log.Fatal(LogTag.SysIO, "Util.SysIO.File.WriteAllTextSync filePath 无效。");
                        return;
                    }

                    // 检查并创建目录
                    string directoryPath = SysIO.Path.GetDirectoryName(filePath);
                    if (!string.IsNullOrEmpty(directoryPath))
                    {
                        SysIO.Directory.CreateIfNotExist(directoryPath);
                    }

                    System.IO.File.WriteAllText(filePath, fileText, encoding);

                    SysIO.WebGLSyncFs();
                }

                /// <summary>
                /// 同步方式读取文件的文本行内容集合。
                /// </summary>
                /// <param name="filePath">文件路径。</param>
                /// <returns>文本行内容。</returns>
                public static string[] ReadAllLinesSync(string filePath)
                {
                    if (string.IsNullOrEmpty(filePath))
                    {
                        Log.Fatal(LogTag.SysIO, "Util.SysIO.File.ReadAllLinesSync filePath 无效。");
                        return null;
                    }

                    if (SysIO.File.Exists(filePath))
                    {
                        return System.IO.File.ReadAllLines(filePath, new System.Text.UTF8Encoding(false));
                    }

                    return null;
                }
                
                /// <summary>
                /// 同步方式写入文本行内容集合到文件。
                /// </summary>
                /// <param name="filePath">文件路径。</param>
                /// <param name="lines">文本行内容集合。</param>
                public static void WriteAllLinesSync(string filePath, string[] lines)
                {
                    if (string.IsNullOrEmpty(filePath))
                    {
                        Log.Fatal(LogTag.SysIO, "Util.SysIO.File.WriteAllLinesSync filePath 无效。");
                        return;
                    }

                    // 检查并创建目录
                    string directoryPath = SysIO.Path.GetDirectoryName(filePath);
                    if (!string.IsNullOrEmpty(directoryPath))
                    {
                        SysIO.Directory.CreateIfNotExist(directoryPath);
                    }

                    System.IO.File.WriteAllLines(filePath, lines, new System.Text.UTF8Encoding(false));
                    
                    SysIO.WebGLSyncFs();
                }
                
                /// <summary>
                /// 删除文件。
                /// Win 下 SQLite 等场景可能因句柄延迟释放抛 IOException，内部走"GC.Collect + WaitForPendingFinalizers + 等待重试"兜底，
                /// 仍失败时记录 Log.Error，不再抛出，调用方按需通过随后再 Exists 判定真实状态。
                /// </summary>
                /// <param name="filePath">文件路径。</param>
                public static void Delete(string filePath)
                {
                    if (string.IsNullOrEmpty(filePath))
                    {
                        Log.Fatal(LogTag.SysIO, "Util.SysIO.File.Delete filePath 无效。");
                        return;
                    }

                    if (!SysIO.File.Exists(filePath))
                    {
                        return;
                    }

                    if (!TryDeleteWithRetry(filePath))
                    {
                        return;
                    }

#if UNITY_EDITOR
                    string metaFilePath = $"{filePath}.meta";
                    if (SysIO.File.Exists(metaFilePath))
                    {
                        TryDeleteWithRetry(metaFilePath);
                    }
#endif
                    SysIO.WebGLSyncFs();
                }

                /// <summary>
                /// 内部带 IOException 兜底重试的文件删除：首删失败时触发 GC 回收 native 句柄后短暂重试 2 次。
                /// 主要规避 Win 下 SQLite/FileStream 句柄释放与 unlink 之间的时序竞态（macOS unlink 不锁，正常情况下首次即成功）。
                /// </summary>
                /// <param name="filePath">绝对路径，调用前需确保 Exists。</param>
                /// <returns>实际删除成功返回 true；超过重试次数仍失败返回 false 并记录 Log.Error。</returns>
                private static bool TryDeleteWithRetry(string filePath)
                {
                    const int retryCount = 3;
                    const int retryDelayMs = 50;
                    System.Exception lastException = null;

                    for (int attempt = 0; attempt < retryCount; attempt++)
                    {
                        try
                        {
                            System.IO.File.Delete(filePath);
                            return true;
                        }
                        catch (System.IO.IOException ex)
                        {
                            lastException = ex;
                        }
                        catch (System.UnauthorizedAccessException ex)
                        {
                            lastException = ex;
                        }

                        // GC 兜底：强制回收 SQLiteConnection / FileStream finalizer 持有的 native 句柄
                        System.GC.Collect();
                        System.GC.WaitForPendingFinalizers();
                        System.GC.Collect();

                        if (attempt < retryCount - 1)
                        {
                            System.Threading.Thread.Sleep(retryDelayMs);
                        }
                    }

                    Log.Error(LogTag.SysIO, "Util.SysIO.File.Delete 失败（{0} 次重试后仍占用）：{1}，异常：{2}", retryCount, filePath, lastException?.Message);
                    return false;
                }

                /// <summary>
                /// 复制文件。
                /// </summary>
                /// <param name="sourceFilePath">文件原始位置。</param>
                /// <param name="destFilePath">文件目标位置。</param>
                public static void Copy(string sourceFilePath, string destFilePath)
                {
                    if (string.IsNullOrEmpty(sourceFilePath))
                    {
                        Log.Fatal(LogTag.SysIO, "Util.SysIO.File.Copy sourceFilePath 无效。");
                        return;
                    }
                    if (string.IsNullOrEmpty(destFilePath))
                    {
                        Log.Fatal(LogTag.SysIO, "Util.SysIO.File.Copy destFilePath 无效。");
                        return;
                    }

                    // 检查并创建目标目录
                    string destDirectoryPath = SysIO.Path.GetDirectoryName(destFilePath);
                    if (!string.IsNullOrEmpty(destDirectoryPath))
                    {
                        SysIO.Directory.CreateIfNotExist(destDirectoryPath);
                    }

                    if (SysIO.File.Exists(sourceFilePath))
                    {
                        System.IO.File.Copy(sourceFilePath, destFilePath, true);
#if UNITY_EDITOR
                        string metaSourceFilePath = $"{sourceFilePath}.meta";
                        string metaDestFilePath = $"{destFilePath}.meta";
                        if (SysIO.File.Exists(metaSourceFilePath))
                        {
                            if (SysIO.File.Exists(metaDestFilePath))
                            {
                                SysIO.File.Delete(metaDestFilePath);
                            }
                            System.IO.File.Copy(metaSourceFilePath, metaDestFilePath, true);
                        }
#endif
                        SysIO.WebGLSyncFs();
                    }

                }
                
                /// <summary>
                /// 移动文件。
                /// 如果文件存在会被覆盖。
                /// </summary>
                /// <param name="sourceFilePath">文件原始位置。</param>
                /// <param name="destFilePath">文件目标位置。</param>
                public static void Move(string sourceFilePath, string destFilePath)
                {
                    if (string.IsNullOrEmpty(sourceFilePath))
                    {
                        Log.Fatal(LogTag.SysIO, "Util.SysIO.File.Move sourceFilePath 无效。");
                        return;
                    }
                    if (string.IsNullOrEmpty(destFilePath))
                    {
                        Log.Fatal(LogTag.SysIO, "Util.SysIO.File.Move destFilePath 无效。");
                        return;
                    }

                    // 检查并创建目标目录
                    string destDirectoryPath = SysIO.Path.GetDirectoryName(destFilePath);
                    if (!string.IsNullOrEmpty(destDirectoryPath))
                    {
                        SysIO.Directory.CreateIfNotExist(destDirectoryPath);
                    }

                    if (SysIO.File.Exists(sourceFilePath))
                    {
                        if (SysIO.File.Exists(destFilePath))
                        {
                            SysIO.File.Delete(destFilePath);   
                        }
                        System.IO.File.Move(sourceFilePath, destFilePath);
#if UNITY_EDITOR
                        string metaSourceFilePath = $"{sourceFilePath}.meta";
                        string metaDestFilePath = $"{destFilePath}.meta";
                        if (SysIO.File.Exists(metaSourceFilePath))
                        {
                            if (SysIO.File.Exists(metaDestFilePath))
                            {
                                SysIO.File.Delete(metaDestFilePath);
                            }
                            System.IO.File.Move(metaSourceFilePath, metaDestFilePath);
                        }
#endif
                        SysIO.WebGLSyncFs();
                    }
                }

                /// <summary>
                /// 文件是否存在。
                /// </summary>
                /// <param name="filePath">文件路径。</param>
                /// <returns>是否成功。</returns>
                public static bool Exists(string filePath)
                {
                    if (string.IsNullOrEmpty(filePath))
                    {
                        Log.Fatal(LogTag.SysIO, "Util.SysIO.File.Exists filePath 无效。");
                        return false;
                    }

                    return System.IO.File.Exists(filePath);
                }

                /// <summary>
                /// 获取文件名称。
                /// </summary>
                /// <param name="includeFileExtension">包括文件扩展名。</param>
                /// <returns>文件名称。</returns>
                public static string GetName(string filePath, bool includeFileExtension = true)
                {
                    if (string.IsNullOrEmpty(filePath))
                    {
                        Log.Fatal(LogTag.SysIO, "Util.SysIO.File.GetName filePath 无效。");
                        return null;
                    }
                    
                    return includeFileExtension ? SysIO.Path.GetFileName(filePath) : SysIO.Path.GetFileNameWithoutExtension(filePath);
                }

                /// <summary>
                /// 打开现有文件以进行读取。
                /// </summary>
                /// <param name="filePath">文件路径。</param>
                /// <returns>文件流。</returns>
                public static System.IO.FileStream OpenRead(string filePath)
                {
                    if (string.IsNullOrEmpty(filePath))
                    {
                        Log.Fatal(LogTag.SysIO, "Util.SysIO.File.OpenRead filePath 无效。");
                        return null;
                    }

                    return System.IO.File.OpenRead(filePath);
                }

                /// <summary>
                /// 打开现有文件或创建新文件以进行写入。
                /// </summary>
                /// <param name="filePath">文件路径。</param>
                /// <returns>文件流。</returns>
                public static System.IO.FileStream OpenWrite(string filePath)
                {
                    if (string.IsNullOrEmpty(filePath))
                    {
                        Log.Fatal(LogTag.SysIO, "Util.SysIO.File.OpenWrite filePath 无效。");
                        return null;
                    }

                    // 检查并创建目录
                    string directoryPath = SysIO.Path.GetDirectoryName(filePath);
                    if (!string.IsNullOrEmpty(directoryPath))
                    {
                        SysIO.Directory.CreateIfNotExist(directoryPath);
                    }

                    return System.IO.File.OpenWrite(filePath);
                }

                /// <summary>
                /// 打开文件以进行读/写操作，指定共享选项。
                /// </summary>
                /// <param name="filePath">文件路径。</param>
                /// <param name="mode">打开模式。</param>
                /// <param name="access">访问权限。</param>
                /// <param name="share">共享选项。</param>
                /// <returns>文件流。</returns>
                public static System.IO.FileStream Open(string filePath, FileMode mode, FileAccess access, FileShare share)
                {
                    if (string.IsNullOrEmpty(filePath))
                    {
                        Log.Fatal(LogTag.SysIO, "Util.SysIO.File.Open filePath 无效。");
                        return null;
                    }

                    return System.IO.File.Open(filePath, mode, access, share);
                }

                /// <summary>
                /// 创建或打开文件以进行写入 UTF-8 编码的文本。
                /// </summary>
                /// <param name="filePath">文件路径。</param>
                /// <returns>StreamWriter。</returns>
                public static System.IO.StreamWriter CreateText(string filePath)
                {
                    if (string.IsNullOrEmpty(filePath))
                    {
                        Log.Fatal(LogTag.SysIO, "Util.SysIO.File.CreateText filePath 无效。");
                        return null;
                    }

                    // 检查并创建目录
                    string directoryPath = SysIO.Path.GetDirectoryName(filePath);
                    if (!string.IsNullOrEmpty(directoryPath))
                    {
                        SysIO.Directory.CreateIfNotExist(directoryPath);
                    }

                    return new System.IO.StreamWriter(filePath, false, new System.Text.UTF8Encoding(false));
                }

                /// <summary>
                /// 打开现有文件以进行读取 UTF-8 编码的文本。
                /// </summary>
                /// <param name="filePath">文件路径。</param>
                /// <returns>StreamReader。</returns>
                public static System.IO.StreamReader OpenText(string filePath)
                {
                    if (string.IsNullOrEmpty(filePath))
                    {
                        Log.Fatal(LogTag.SysIO, "Util.SysIO.File.OpenText filePath 无效。");
                        return null;
                    }

                    if (SysIO.File.Exists(filePath))
                    {
                        return System.IO.File.OpenText(filePath);
                    }

                    return null;
                }

                /// <summary>
                /// 复制文件。
                /// </summary>
                /// <param name="sourceFilePath">源文件路径。</param>
                /// <param name="destFilePath">目标文件路径。</param>
                /// <param name="overwrite">是否覆盖现有文件。</param>
                public static void Copy(string sourceFilePath, string destFilePath, bool overwrite)
                {
                    if (string.IsNullOrEmpty(sourceFilePath))
                    {
                        Log.Fatal(LogTag.SysIO, "Util.SysIO.File.Copy sourceFilePath 无效。");
                        return;
                    }
                    if (string.IsNullOrEmpty(destFilePath))
                    {
                        Log.Fatal(LogTag.SysIO, "Util.SysIO.File.Copy destFilePath 无效。");
                        return;
                    }

                    // 检查并创建目标目录
                    string destDirectoryPath = SysIO.Path.GetDirectoryName(destFilePath);
                    if (!string.IsNullOrEmpty(destDirectoryPath))
                    {
                        SysIO.Directory.CreateIfNotExist(destDirectoryPath);
                    }

                    if (SysIO.File.Exists(sourceFilePath))
                    {
                        System.IO.File.Copy(sourceFilePath, destFilePath, overwrite);
#if UNITY_EDITOR
                        string metaSourceFilePath = $"{sourceFilePath}.meta";
                        string metaDestFilePath = $"{destFilePath}.meta";
                        if (SysIO.File.Exists(metaSourceFilePath))
                        {
                            if (SysIO.File.Exists(metaDestFilePath))
                            {
                                SysIO.File.Delete(metaDestFilePath);
                            }
                            System.IO.File.Copy(metaSourceFilePath, metaDestFilePath, overwrite);
                        }
#endif
                        SysIO.WebGLSyncFs();
                    }
                }
                
            }
        }   
    }

}
