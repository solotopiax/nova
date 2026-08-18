/***************************************************************
 * (c) copyright 2026 - 2030, Solotopia
 * All Rights Reserved.
 * -------------------------------------------------------------
 * filename:  IFirebasePushTaskPlugin.cs
 * author:    Codex
 * created:   2026/8/13
 * descrip:   Firebase push task plugin interface
 ***************************************************************/

#if !UNITY_WEBGL
using System.Threading;
using Cysharp.Threading.Tasks;
using NovaFramework.Runtime;

namespace NovaFramework.SDK.FirebasePlugin.Runtime
{
    /// <summary>
    /// Firebase 业务 push task 接口。
    /// 业务层通过 Nova.SDK.Get/ TryGet 获取该接口后写入待推送任务；插件会先持久化缓存，再按配置批量发送协议。
    /// </summary>
    public interface IFirebasePushTaskPlugin : ISDKPlugin
    {
        /// <summary>
        /// 写入或覆盖一条 push task 缓存。
        /// task_key 是唯一主键；同一个 task_key 的新任务会覆盖旧任务，发送成功后才会删除仍匹配版本的缓存。
        /// </summary>
        /// <param name="task">待推送任务。</param>
        /// <param name="ct">取消令牌，仅作用于本地缓存写入等待。</param>
        /// <returns>本地缓存写入成功返回 true；参数无效或持久化不可用返回 false。</returns>
        UniTask<bool> QueuePushTaskAsync(FirebasePushTask task, CancellationToken ct = default);
    }
}
#endif
