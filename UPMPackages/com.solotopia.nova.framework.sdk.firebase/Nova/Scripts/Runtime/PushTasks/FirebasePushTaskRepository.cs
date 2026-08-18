/***************************************************************
 * (c) copyright 2026 - 2030, Solotopia
 * All Rights Reserved.
 * -------------------------------------------------------------
 * filename:  FirebasePushTaskRepository.cs
 * author:    Codex
 * created:   2026/8/13
 * descrip:   Firebase push task local repository
 ***************************************************************/

#if !UNITY_WEBGL
using System;
using System.Collections.Generic;
using NovaFramework.Runtime;

namespace NovaFramework.SDK.FirebasePlugin.Runtime
{
    /// <summary>
    /// Firebase push task 本地缓存仓储。
    /// 使用 IFileFragmentManager 持久化，分类名固定为 FirebasePushTasks，item 名使用 task_key。
    /// </summary>
    internal sealed class FirebasePushTaskRepository
    {
        /// <summary>
        /// push task 缓存分类名。
        /// </summary>
        private const string c_PersistClassify = "FirebasePushTasks";

        /// <summary>
        /// 文件片段持久化管理器。
        /// </summary>
        private readonly IFileFragmentManager m_PersistManager;

        /// <summary>
        /// 创建缓存仓储。
        /// </summary>
        /// <param name="persistManager">文件片段持久化管理器。</param>
        public FirebasePushTaskRepository(IFileFragmentManager persistManager)
        {
            m_PersistManager = persistManager;
        }

        /// <summary>
        /// 创建基于框架文件片段管理器的仓储。
        /// 管理器不可用时返回 null，由调用方决定是否跳过缓存写入。
        /// </summary>
        /// <returns>仓储实例，失败返回 null。</returns>
        public static FirebasePushTaskRepository TryCreate()
        {
            try
            {
                IFileFragmentManager persistManager = FrameworkManagersGroup.GetManager<IFileFragmentManager>();
                return persistManager == null ? null : new FirebasePushTaskRepository(persistManager);
            }
            catch (Exception ex)
            {
                Log.Warning(LogTag.Firebase, $"获取文件片段持久化管理器失败，Firebase push task 无法缓存：{ex.Message}");
                return null;
            }
        }

        /// <summary>
        /// 写入或覆盖缓存任务。
        /// </summary>
        /// <param name="task">待缓存任务。</param>
        /// <param name="cacheVersion">本次写入版本。</param>
        /// <returns>写入成功返回 true。</returns>
        public bool Upsert(FirebasePushTask task, int cacheVersion)
        {
            if (m_PersistManager == null || task == null || !task.HasValidTaskKey())
            {
                return false;
            }

            FirebasePushTask normalizedTask = task.CloneNormalized();
            var entry = new FirebasePushTaskCacheEntry
            {
                CacheVersion = cacheVersion,
                Task = normalizedTask,
            };

            m_PersistManager.SetObject(c_PersistClassify, normalizedTask.TaskKey, entry);
            return m_PersistManager.Save(c_PersistClassify);
        }

        /// <summary>
        /// 获取当前缓存数量。
        /// </summary>
        /// <returns>缓存数量。</returns>
        public int Count()
        {
            return m_PersistManager?.Count(c_PersistClassify) ?? 0;
        }

        /// <summary>
        /// 获取当前缓存快照。
        /// 快照只复制可用任务；损坏或 task_key 为空的条目会被跳过。
        /// </summary>
        /// <returns>缓存快照列表。</returns>
        public List<FirebasePushTaskSnapshotItem> GetSnapshot()
        {
            var snapshot = new List<FirebasePushTaskSnapshotItem>();
            if (m_PersistManager == null)
            {
                return snapshot;
            }

            string[] taskKeys = m_PersistManager.GetAllItemNames(c_PersistClassify);
            for (int i = 0; i < taskKeys.Length; i++)
            {
                string taskKey = taskKeys[i];
                FirebasePushTaskCacheEntry entry = GetEntry(taskKey);
                FirebasePushTask task = entry?.Task?.CloneNormalized();
                if (entry == null || task == null || !task.HasValidTaskKey())
                {
                    continue;
                }

                snapshot.Add(new FirebasePushTaskSnapshotItem(taskKey, entry.CacheVersion, task));
            }

            return snapshot;
        }

        /// <summary>
        /// 删除发送成功快照中仍然匹配当前版本的缓存条目。
        /// 如果发送过程中同一个 task_key 被业务覆盖，当前版本会不同，因此不会删除新任务。
        /// </summary>
        /// <param name="snapshot">发送成功的快照。</param>
        /// <returns>实际删除的条目数量。</returns>
        public int RemoveSucceededSnapshotItems(IReadOnlyList<FirebasePushTaskSnapshotItem> snapshot)
        {
            if (m_PersistManager == null || snapshot == null || snapshot.Count == 0)
            {
                return 0;
            }

            int removedCount = 0;
            for (int i = 0; i < snapshot.Count; i++)
            {
                FirebasePushTaskSnapshotItem snapshotItem = snapshot[i];
                FirebasePushTaskCacheEntry currentEntry = GetEntry(snapshotItem.TaskKey);
                if (currentEntry != null && currentEntry.CacheVersion == snapshotItem.CacheVersion)
                {
                    if (m_PersistManager.RemoveItem(c_PersistClassify, snapshotItem.TaskKey))
                    {
                        removedCount++;
                    }
                }
            }

            if (removedCount > 0)
            {
                m_PersistManager.Save();
            }

            return removedCount;
        }

        /// <summary>
        /// 读取单条缓存。
        /// </summary>
        /// <param name="taskKey">缓存主键。</param>
        /// <returns>缓存条目，不存在或反序列化失败返回 null。</returns>
        private FirebasePushTaskCacheEntry GetEntry(string taskKey)
        {
            if (string.IsNullOrEmpty(taskKey))
            {
                return null;
            }

            return m_PersistManager.GetObject<FirebasePushTaskCacheEntry>(c_PersistClassify, taskKey, null);
        }
    }
}
#endif
