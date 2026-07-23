/***************************************************************
 * (c) copyright 2026 - 2030, Solotopia
 * All Rights Reserved.
 * -------------------------------------------------------------
 * filename:  EditorUtil.FileSystem.OutputApplier.cs
 * author:    taoye
 * created:   2026/7/17
 * descrip:   通用文件输出事务：批量替换或删除文件，并在失败时逆序回滚
 * input:     暂存根目录、已生成文件、正式目标路径与精确删除路径
 * output:    全部应用后的正式文件，或恢复到应用前状态的目标文件
 * boundary:  不解释模块、Excel、Luban 或产物集合等业务语义
 * failure:   应用失败时回滚；回滚不完整时抛出包含全部异常的聚合异常
 ***************************************************************/

using System;
using System.Collections.Generic;
using System.IO;
using IOPath = System.IO.Path;

namespace NovaFramework.Editor
{
    public static partial class EditorUtil
    {
        public static partial class FileSystem
        {
            /// <summary>
            /// 把一批已验证文件替换或精确删除操作作为一个补偿式事务应用。
            /// 文件系统不支持跨文件原子提交，因此失败时通过备份逆序恢复。
            /// </summary>
            internal sealed class OutputApplier : IDisposable
            {
                private readonly string m_BackupRoot;
                private readonly List<Entry> m_Entries = new List<Entry>();
                private readonly HashSet<string> m_Targets =
                    new HashSet<string>(StringComparer.OrdinalIgnoreCase);
                private bool m_Applied;

                internal OutputApplier(string tempRoot)
                {
                    if (string.IsNullOrWhiteSpace(tempRoot))
                    {
                        throw new ArgumentException("Output transaction root cannot be empty.", nameof(tempRoot));
                    }

                    StagingRoot = IOPath.Combine(IOPath.GetFullPath(tempRoot), "_publish");
                    m_BackupRoot = IOPath.Combine(StagingRoot, "backup");
                    Directory.CreateDirectory(StagingRoot);
                }

                internal string StagingRoot { get; }

                internal void AddReplacement(string stagedPath, string targetPath)
                {
                    if (string.IsNullOrWhiteSpace(stagedPath) || !System.IO.File.Exists(stagedPath))
                    {
                        throw new FileNotFoundException(
                            $"Output transaction staged file does not exist: {stagedPath}",
                            stagedPath);
                    }

                    m_Entries.Add(CreateEntry(stagedPath, targetPath, false));
                }

                internal void AddDeletion(string targetPath)
                {
                    m_Entries.Add(CreateEntry(null, targetPath, true));
                }

                internal void Apply(
                    Action<string, string> replace = null,
                    Action<string> delete = null,
                    Action<string, string> restore = null)
                {
                    if (m_Applied)
                    {
                        throw new InvalidOperationException("Output transaction has already been applied.");
                    }

                    replace ??= ReplaceFile;
                    delete ??= System.IO.File.Delete;
                    restore ??= ReplaceFile;
                    PrepareBackups();

                    Exception applyException;
                    try
                    {
                        for (int i = 0; i < m_Entries.Count; i++)
                        {
                            Entry entry = m_Entries[i];
                            entry.Applied = true;
                            if (entry.IsDelete)
                            {
                                if (System.IO.File.Exists(entry.TargetPath))
                                {
                                    delete(entry.TargetPath);
                                }
                            }
                            else
                            {
                                replace(entry.StagedPath, entry.TargetPath);
                            }
                        }

                        m_Applied = true;
                        DeleteBackupRoot();
                        return;
                    }
                    catch (Exception exception)
                    {
                        applyException = exception;
                    }

                    List<Exception> rollbackExceptions = Rollback(restore);
                    if (rollbackExceptions.Count == 0)
                    {
                        DeleteBackupRoot();
                        throw applyException;
                    }

                    var allExceptions = new List<Exception> { applyException };
                    allExceptions.AddRange(rollbackExceptions);
                    throw new AggregateException(
                        "Output transaction failed and rollback was incomplete.",
                        allExceptions);
                }

                internal static void ReplaceFile(string stagedPath, string targetPath)
                {
                    if (string.IsNullOrWhiteSpace(stagedPath) || !System.IO.File.Exists(stagedPath))
                    {
                        throw new FileNotFoundException(
                            $"Replacement source does not exist: {stagedPath}",
                            stagedPath);
                    }

                    if (string.IsNullOrWhiteSpace(targetPath))
                    {
                        throw new ArgumentException("Replacement target cannot be empty.", nameof(targetPath));
                    }

                    string fullTargetPath = IOPath.GetFullPath(targetPath);
                    string targetDirectory = IOPath.GetDirectoryName(fullTargetPath);
                    if (!string.IsNullOrEmpty(targetDirectory))
                    {
                        Directory.CreateDirectory(targetDirectory);
                    }

                    string temporaryPath = fullTargetPath + ".tmp";
                    try
                    {
                        if (System.IO.File.Exists(temporaryPath))
                        {
                            System.IO.File.Delete(temporaryPath);
                        }

                        System.IO.File.Copy(stagedPath, temporaryPath, true);
                        if (System.IO.File.Exists(fullTargetPath))
                        {
                            System.IO.File.Replace(temporaryPath, fullTargetPath, null);
                        }
                        else
                        {
                            System.IO.File.Move(temporaryPath, fullTargetPath);
                        }
                    }
                    finally
                    {
                        if (System.IO.File.Exists(temporaryPath))
                        {
                            System.IO.File.Delete(temporaryPath);
                        }
                    }
                }

                public void Dispose()
                {
                    if (Directory.Exists(StagingRoot))
                    {
                        Directory.Delete(StagingRoot, true);
                    }
                }

                private Entry CreateEntry(string stagedPath, string targetPath, bool isDelete)
                {
                    if (string.IsNullOrWhiteSpace(targetPath))
                    {
                        throw new ArgumentException("Output transaction target cannot be empty.", nameof(targetPath));
                    }

                    string fullTargetPath = IOPath.GetFullPath(targetPath);
                    if (!m_Targets.Add(fullTargetPath))
                    {
                        throw new InvalidDataException(
                            $"Duplicate output transaction target: {fullTargetPath}");
                    }

                    return new Entry
                    {
                        StagedPath = string.IsNullOrEmpty(stagedPath) ? null : IOPath.GetFullPath(stagedPath),
                        TargetPath = fullTargetPath,
                        IsDelete = isDelete,
                    };
                }

                private void PrepareBackups()
                {
                    if (Directory.Exists(m_BackupRoot))
                    {
                        Directory.Delete(m_BackupRoot, true);
                    }

                    Directory.CreateDirectory(m_BackupRoot);
                    for (int i = 0; i < m_Entries.Count; i++)
                    {
                        Entry entry = m_Entries[i];
                        entry.OriginalExisted = System.IO.File.Exists(entry.TargetPath);
                        if (!entry.OriginalExisted)
                        {
                            continue;
                        }

                        entry.BackupPath = IOPath.Combine(m_BackupRoot, i.ToString("D4") + ".bak");
                        System.IO.File.Copy(entry.TargetPath, entry.BackupPath, true);
                    }
                }

                private List<Exception> Rollback(Action<string, string> restore)
                {
                    var exceptions = new List<Exception>();
                    for (int i = m_Entries.Count - 1; i >= 0; i--)
                    {
                        Entry entry = m_Entries[i];
                        if (!entry.Applied)
                        {
                            continue;
                        }

                        try
                        {
                            if (entry.OriginalExisted)
                            {
                                restore(entry.BackupPath, entry.TargetPath);
                            }
                            else if (System.IO.File.Exists(entry.TargetPath))
                            {
                                System.IO.File.Delete(entry.TargetPath);
                            }
                        }
                        catch (Exception exception)
                        {
                            exceptions.Add(exception);
                        }
                    }

                    return exceptions;
                }

                private void DeleteBackupRoot()
                {
                    if (Directory.Exists(m_BackupRoot))
                    {
                        Directory.Delete(m_BackupRoot, true);
                    }
                }

                private sealed class Entry
                {
                    internal string StagedPath;
                    internal string TargetPath;
                    internal string BackupPath;
                    internal bool IsDelete;
                    internal bool OriginalExisted;
                    internal bool Applied;
                }
            }
        }
    }
}
