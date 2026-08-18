/***************************************************************
 * (c) copyright 2026 - 2030, Solotopia
 * All Rights Reserved.
 * -------------------------------------------------------------
 * filename:  FirebasePushTaskNetService.cs
 * author:    Codex
 * created:   2026/8/13
 * descrip:   Firebase push task network service
 ***************************************************************/

#if !UNITY_WEBGL
using System.Collections.Generic;
using Cysharp.Threading.Tasks;
using NovaFramework.Runtime;

namespace NovaFramework.SDK.FirebasePlugin.Runtime
{
    /// <summary>
    /// Firebase push task 业务网络 Service。
    /// 封装 PbNetCreatePushTasksReq 协议发送；当前协议没有按 task_key 返回结果，因此调用方按 NetResponse.IsSuccess 判断整批成功。
    /// </summary>
    public sealed class FirebasePushTaskNetService
    {
        /// <summary>
        /// 当前 Service 实例的调试模式覆盖值。
        /// 为 null 时沿用 NetService.IsDebugMode 全局开关。
        /// </summary>
        private bool? m_DebugModeOverride;

        /// <summary>
        /// 设置当前 Service 实例的调试模式覆盖。
        /// </summary>
        /// <param name="debugMode">是否启用调试模式。</param>
        public void SetDebugMode(bool debugMode)
        {
            m_DebugModeOverride = debugMode;
        }

        /// <summary>
        /// 批量创建或取消服务端 push task。
        /// </summary>
        /// <param name="cmdName">协议名，由 FirebasePluginConfig.PushCmdName 提供。</param>
        /// <param name="tasks">待发送任务快照。</param>
        /// <returns>协议响应。</returns>
        public async UniTask<NetResponse<PbNetCreatePushTasksResp>> Async(
            string cmdName,
            IReadOnlyList<FirebasePushTask> tasks)
        {
            var body = new PbNetCreatePushTasksReq
            {
                Head = NetBuilder.BuildHeader(),
            };

            if (tasks != null)
            {
                for (int i = 0; i < tasks.Count; i++)
                {
                    FirebasePushTask task = tasks[i];
                    if (task == null || !task.HasValidTaskKey())
                    {
                        continue;
                    }

                    body.Tasks.Add(BuildPushTaskMessage(task));
                }
            }

            Log.Info(LogTag.Firebase, $"Firebase push task 准备发送协议：PushCmdName={cmdName}，TaskCount={body.Tasks.Count}。");
            INetworkCmdRow cmdRow = Nova.Network?.ResolveNetCmdRow(cmdName);
            return await NetService.SendAsync(cmdRow, body, PbNetCreatePushTasksResp.Parser);
        }

        /// <summary>
        /// 将业务 push task 转换为协议 push task。
        /// Cancel=true 时服务端只允许 task_key 与 cancel 字段，trigger_time 和 template_id 必须保持默认值 0，避免被 protobuf 序列化。
        /// </summary>
        /// <param name="task">已通过 task_key 校验的业务 push task。</param>
        /// <returns>可加入 PbNetCreatePushTasksReq 的协议任务。</returns>
        private static PbPushTask BuildPushTaskMessage(FirebasePushTask task)
        {
            FirebasePushTask normalizedTask = task.CloneNormalized();
            if (normalizedTask.Cancel)
            {
                return new PbPushTask
                {
                    TaskKey = normalizedTask.TaskKey,
                    Cancel = true,
                };
            }

            return new PbPushTask
            {
                TaskKey = normalizedTask.TaskKey,
                TriggerTime = normalizedTask.TriggerTime,
                Cancel = false,
                TemplateId = normalizedTask.TemplateId,
            };
        }
    }
}
#endif
