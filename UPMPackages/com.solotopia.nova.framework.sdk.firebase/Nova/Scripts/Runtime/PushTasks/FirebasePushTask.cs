/***************************************************************
 * (c) copyright 2026 - 2030, Solotopia
 * All Rights Reserved.
 * -------------------------------------------------------------
 * filename:  FirebasePushTask.cs
 * author:    Codex
 * created:   2026/8/13
 * descrip:   Firebase push task DTO
 ***************************************************************/

namespace NovaFramework.SDK.FirebasePlugin.Runtime
{
    /// <summary>
    /// Firebase push task 数据。
    /// 字段与 pb_net_push.proto 中的 PbPushTask 保持一一对应，业务层只需要填写本 DTO。
    /// </summary>
    public sealed class FirebasePushTask
    {
        /// <summary>
        /// 业务自定义唯一主键。
        /// 同一个 TaskKey 的后续任务会覆盖之前缓存的任务。
        /// </summary>
        public string TaskKey { get; set; } = string.Empty;

        /// <summary>
        /// 触发时间，Unix 秒。
        /// </summary>
        public long TriggerTime { get; set; }

        /// <summary>
        /// 是否取消同 TaskKey 下尚未派发的服务端任务。
        /// 为 true 时协议层只发送 TaskKey 和 Cancel，TriggerTime 与 TemplateId 会被忽略。
        /// </summary>
        public bool Cancel { get; set; }

        /// <summary>
        /// 服务端消息模板 ID。
        /// </summary>
        public long TemplateId { get; set; }

        /// <summary>
        /// 创建一份规整后的副本，避免调用方在写入后继续修改对象影响缓存快照。
        /// </summary>
        /// <returns>规整后的 push task。</returns>
        internal FirebasePushTask CloneNormalized()
        {
            return new FirebasePushTask
            {
                TaskKey = TaskKey?.Trim() ?? string.Empty,
                TriggerTime = TriggerTime,
                Cancel = Cancel,
                TemplateId = TemplateId,
            };
        }

        /// <summary>
        /// 判断任务是否具备可作为缓存主键的 task_key。
        /// </summary>
        /// <returns>task_key 非空返回 true。</returns>
        internal bool HasValidTaskKey()
        {
            return !string.IsNullOrWhiteSpace(TaskKey);
        }
    }
}
