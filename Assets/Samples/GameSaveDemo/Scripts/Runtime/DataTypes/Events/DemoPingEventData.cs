/***************************************************************
 * (c) copyright 2026 - 2030, Solotopia
 * All Rights Reserved.
 * -------------------------------------------------------------
 * filename:  DemoPingEventData.cs
 * author:    taoye
 * created:   2026/05/23
 * descrip:   Demo 事件数据 — 用于 DemoEventView / DemoIntegrationEventNetworkView
 *            演示 Nova.Event.Subscribe / Fire / Unsubscribe 基本用法。
 ***************************************************************/

using NovaFramework.Runtime;

namespace NovaFramework.Kit.Network.GameSave.Samples.Runtime
{
    /// <summary>
    /// Demo Ping 事件数据，携带一条消息字符串，用于演示事件总线订阅/发布流程。
    /// 继承 EventData 并实现 IReference，由 ReferencePool 管理生命周期。
    /// </summary>
    public sealed class DemoPingEventData : EventData
    {
        /// <summary>
        /// 事件携带的消息内容。
        /// </summary>
        public string Message;

        /// <summary>
        /// 创建并填充 DemoPingEventData 实例（从引用池获取）。
        /// </summary>
        /// <param name="message">消息内容。</param>
        /// <returns>已填充的 DemoPingEventData 实例。</returns>
        public static DemoPingEventData Create(string message)
        {
            DemoPingEventData data = ReferencePool.Get<DemoPingEventData>();
            data.Message = message;
            return data;
        }

        /// <summary>
        /// 清理引用，将所有字段重置为默认值，确保回池后不携带旧数据。
        /// </summary>
        public override void Clear()
        {
            Message = null;
        }
    }
}
