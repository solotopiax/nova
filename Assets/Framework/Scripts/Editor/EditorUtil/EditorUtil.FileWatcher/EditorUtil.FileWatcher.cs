/***************************************************************
 * (c) copyright 2026 - 2030, Solotopia
 * All Rights Reserved.
 * -------------------------------------------------------------
 * filename:  EditorUtil.FileWatcher.cs
 * author:    taoye
 * created:   2026/4/16
 * descrip:   文件变动监控器
 ***************************************************************/

using System;
using System.Collections.Generic;
using SIO = System.IO;
using NovaFramework.Runtime;
using UnityEditor;

namespace NovaFramework.Editor
{
    public static partial class EditorUtil
    {
        /// <summary>
        /// 文件变动监控器，全局单例管理 SIO.FileSystemWatcher 实例，监控指定目录变更。
        /// </summary>
        [InitializeOnLoad]
        public static class FileWatcher
        {
            /// <summary>
            /// 已注册的监控器集合（监控目录路径 -> SIO.FileSystemWatcher）。
            /// </summary>
            private static readonly Dictionary<string, SIO.FileSystemWatcher> s_Watchers = new Dictionary<string, SIO.FileSystemWatcher>();

            /// <summary>
            /// 文件变更回调（监控目录路径 -> 回调列表，使用 ID 去重）。
            /// </summary>
            private static readonly Dictionary<string, List<CallbackEntry>> s_Callbacks = new Dictionary<string, List<CallbackEntry>>();

            /// <summary>
            /// 是否有待处理的变更通知。
            /// </summary>
            private static volatile bool s_HasPendingChange;

            /// <summary>
            /// 待处理变更的目录路径集合。
            /// </summary>
            private static readonly HashSet<string> s_PendingChangeDirs = new HashSet<string>();

            /// <summary>
            /// 同步锁。
            /// </summary>
            private static readonly object s_Lock = new object();

            /// <summary>
            /// 静态构造方法，注册 EditorApplication 回调。
            /// </summary>
            static FileWatcher()
            {
                EditorApplication.update += OnEditorUpdate;
                AssemblyReloadEvents.beforeAssemblyReload += DisposeAll;
            }

            /// <summary>
            /// 注册对指定目录的监控。
            /// 若 callbackId 为 null，使用 onChange 的引用做去重（要求传入具名方法）。
            /// 若 callbackId 非空，使用 ID 字符串做去重（允许传入 lambda）。
            /// </summary>
            /// <param name="dirPath">目录完整路径。</param>
            /// <param name="onChange">文件变更时的回调（在主线程执行）。</param>
            /// <param name="callbackId">回调唯一标识（可选），用于 lambda 场景的去重。</param>
            public static void Watch(string dirPath, Action onChange, string callbackId = null)
            {
                if (string.IsNullOrEmpty(dirPath) || !Util.SysIO.Directory.Exists(dirPath))
                {
                    return;
                }

                string normalizedPath = NormalizePath(dirPath);

                if (!s_Callbacks.TryGetValue(normalizedPath, out List<CallbackEntry> callbacks))
                {
                    callbacks = new List<CallbackEntry>();
                    s_Callbacks[normalizedPath] = callbacks;
                }

                string id = callbackId ?? onChange.Method.Name + "@" + (onChange.Target?.GetHashCode().ToString() ?? "static");
                for (int i = 0; i < callbacks.Count; i++)
                {
                    if (callbacks[i].Id == id)
                    {
                        return;
                    }
                }
                callbacks.Add(new CallbackEntry(id, onChange));

                if (s_Watchers.ContainsKey(normalizedPath))
                {
                    return;
                }

                try
                {
                    SIO.FileSystemWatcher watcher = new SIO.FileSystemWatcher
                    {
                        Path = dirPath,
                        Filter = "*.*",
                        NotifyFilter = SIO.NotifyFilters.LastWrite | SIO.NotifyFilters.FileName | SIO.NotifyFilters.CreationTime,
                        EnableRaisingEvents = true,
                        IncludeSubdirectories = false,
                    };

                    watcher.Changed += (sender, e) => OnFileChanged(normalizedPath);
                    watcher.Created += (sender, e) => OnFileChanged(normalizedPath);
                    watcher.Deleted += (sender, e) => OnFileChanged(normalizedPath);

                    s_Watchers[normalizedPath] = watcher;
                }
                catch (Exception e)
                {
                    Log.Warning(LogTag.Editor, "创建 FileSystemWatcher 失败（{0}）：{1}", dirPath, e.Message);
                }
            }

            /// <summary>
            /// 取消对指定目录的监控。
            /// </summary>
            /// <param name="dirPath">目录完整路径。</param>
            /// <param name="onChange">要移除的回调。</param>
            /// <param name="callbackId">注册时使用的回调唯一标识（可选）。</param>
            public static void Unwatch(string dirPath, Action onChange, string callbackId = null)
            {
                string normalizedPath = NormalizePath(dirPath);

                if (s_Callbacks.TryGetValue(normalizedPath, out List<CallbackEntry> callbacks))
                {
                    string id = callbackId ?? onChange.Method.Name + "@" + (onChange.Target?.GetHashCode().ToString() ?? "static");
                    callbacks.RemoveAll(e => e.Id == id);
                    if (callbacks.Count == 0)
                    {
                        s_Callbacks.Remove(normalizedPath);
                        DisposeWatcher(normalizedPath);
                    }
                }
            }

            /// <summary>
            /// 释放指定目录的监控器。
            /// </summary>
            private static void DisposeWatcher(string normalizedPath)
            {
                if (s_Watchers.TryGetValue(normalizedPath, out SIO.FileSystemWatcher watcher))
                {
                    watcher.EnableRaisingEvents = false;
                    watcher.Dispose();
                    s_Watchers.Remove(normalizedPath);
                }
            }

            /// <summary>
            /// 释放所有监控器。
            /// </summary>
            private static void DisposeAll()
            {
                foreach (var kvp in s_Watchers)
                {
                    kvp.Value.EnableRaisingEvents = false;
                    kvp.Value.Dispose();
                }
                s_Watchers.Clear();
                s_Callbacks.Clear();
                lock (s_Lock)
                {
                    s_PendingChangeDirs.Clear();
                    s_HasPendingChange = false;
                }
            }

            /// <summary>
            /// SIO.FileSystemWatcher 回调（线程池线程）。
            /// </summary>
            private static void OnFileChanged(string normalizedPath)
            {
                lock (s_Lock)
                {
                    s_PendingChangeDirs.Add(normalizedPath);
                    s_HasPendingChange = true;
                }
            }

            /// <summary>
            /// EditorApplication.update 回调（主线程），处理待处理的变更通知。
            /// </summary>
            private static void OnEditorUpdate()
            {
                if (!s_HasPendingChange)
                {
                    return;
                }

                HashSet<string> dirs;
                lock (s_Lock)
                {
                    dirs = new HashSet<string>(s_PendingChangeDirs);
                    s_PendingChangeDirs.Clear();
                    s_HasPendingChange = false;
                }

                foreach (string dir in dirs)
                {
                    if (s_Callbacks.TryGetValue(dir, out List<CallbackEntry> callbacks))
                    {
                        List<CallbackEntry> snapshot = new List<CallbackEntry>(callbacks);
                        foreach (CallbackEntry entry in snapshot)
                        {
                            try
                            {
                                entry.Callback?.Invoke();
                            }
                            catch (Exception e)
                            {
                                Log.Error(LogTag.Editor, "FileWatcher 回调执行失败：{0}", e.Message);
                            }
                        }
                    }
                }
            }

            /// <summary>
            /// 规范化路径。
            /// </summary>
            private static string NormalizePath(string path)
            {
                return path?.Trim().Replace('\\', '/').TrimEnd('/') ?? "";
            }

            /// <summary>
            /// 回调条目，通过 ID 字符串实现去重，解决 lambda 引用不等的问题。
            /// </summary>
            private readonly struct CallbackEntry
            {
                /// <summary>
                /// 回调唯一标识。
                /// </summary>
                public readonly string Id;

                /// <summary>
                /// 回调委托。
                /// </summary>
                public readonly Action Callback;

                /// <summary>
                /// 构造回调条目。
                /// </summary>
                /// <param name="id">唯一标识。</param>
                /// <param name="callback">回调委托。</param>
                public CallbackEntry(string id, Action callback)
                {
                    Id = id;
                    Callback = callback;
                }
            }
        }
    }
}
