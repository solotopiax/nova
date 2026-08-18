/***************************************************************
 * (c) copyright 2026 - 2030, Solotopia
 * All Rights Reserved.
 * -------------------------------------------------------------
 * filename:  FirebasePushTaskCacheEntry.cs
 * author:    Codex
 * created:   2026/8/13
 * descrip:   Firebase push task cache DTO
 ***************************************************************/

namespace NovaFramework.SDK.FirebasePlugin.Runtime
{
    /// <summary>
    /// Firebase push task 本地缓存条目。
    /// CacheVersion 只用于区分发送快照和当前缓存是否仍是同一版本，防止发送中覆盖的新任务被误删。
    /// </summary>
    internal sealed class FirebasePushTaskCacheEntry
    {
        /// <summary>
        /// 缓存版本号。
        /// 每次写入同一个 task_key 都会生成新版本，发送成功后只删除版本仍匹配的条目。
        /// </summary>
        public int CacheVersion { get; set; }

        /// <summary>
        /// 缓存的 push task 数据。
        /// </summary>
        public FirebasePushTask Task { get; set; }
    }

    /// <summary>
    /// Firebase push task 发送快照条目。
    /// </summary>
    internal sealed class FirebasePushTaskSnapshotItem
    {
        /// <summary>
        /// 创建发送快照。
        /// </summary>
        /// <param name="taskKey">缓存主键。</param>
        /// <param name="cacheVersion">快照版本。</param>
        /// <param name="task">快照任务。</param>
        public FirebasePushTaskSnapshotItem(string taskKey, int cacheVersion, FirebasePushTask task)
        {
            TaskKey = taskKey;
            CacheVersion = cacheVersion;
            Task = task;
        }

        /// <summary>
        /// 缓存主键。
        /// </summary>
        public string TaskKey { get; }

        /// <summary>
        /// 快照时的缓存版本。
        /// </summary>
        public int CacheVersion { get; }

        /// <summary>
        /// 快照任务。
        /// </summary>
        public FirebasePushTask Task { get; }
    }

    /// <summary>
    /// 单次 push task flush 结果。
    /// </summary>
    internal enum FirebasePushTaskFlushResult
    {
        /// <summary>
        /// 没有可发送的缓存。
        /// </summary>
        Empty,

        /// <summary>
        /// 协议发送成功。
        /// </summary>
        Success,

        /// <summary>
        /// 协议发送失败或配置不可用。
        /// </summary>
        Failed,
    }
}
