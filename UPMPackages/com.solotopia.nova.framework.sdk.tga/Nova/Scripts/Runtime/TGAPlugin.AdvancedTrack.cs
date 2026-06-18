/***************************************************************
 * (c) copyright 2026 - 2030, Solotopia
 * All Rights Reserved.
 * -------------------------------------------------------------
 * filename:  TGAPlugin.AdvancedTrack.cs
 * author:    yingzheng
 * created:   2026/4/20
 * descrip:   TGAPlugin 高级事件上报（首次/可更新/可覆写）
 ***************************************************************/

#if !UNITY_WEBGL
using System.Collections.Generic;
using ThinkingData.Analytics;

namespace NovaFramework.SDK.TGAPlugin.Runtime
{
    public sealed partial class TGAPlugin
    {
        /// <summary>
        /// 上报首次事件，同一 firstCheckId 只上报一次。
        /// firstCheckId 为 null 时由 SDK 自动生成去重标识。
        /// </summary>
        /// <param name="eventName">事件名称。</param>
        /// <param name="parameters">事件属性字典。</param>
        /// <param name="firstCheckId">去重标识，为 null 时由 SDK 自动生成。</param>
        public void TrackFirst(string eventName, Dictionary<string, object> parameters, string firstCheckId = null)
        {
            var firstEvent = string.IsNullOrEmpty(firstCheckId) ? new TDFirstEventModel(eventName) : new TDFirstEventModel(eventName, firstCheckId);
            firstEvent.Properties = parameters;
            TDAnalytics.Track(firstEvent);
        }

        /// <summary>
        /// 上报可更新事件，相同 eventId 的后续上报将覆盖属性。
        /// </summary>
        /// <param name="eventName">事件名称。</param>
        /// <param name="parameters">事件属性字典。</param>
        /// <param name="eventId">事件唯一标识，用于匹配更新。</param>
        public void TrackUpdatable(string eventName, Dictionary<string, object> parameters, string eventId)
        {
            var updatableEvent = new TDUpdatableEventModel(eventName, eventId);
            updatableEvent.Properties = parameters;
            TDAnalytics.Track(updatableEvent);
        }

        /// <summary>
        /// 上报可覆写事件，相同 eventId 的后续上报将完整替换原记录。
        /// </summary>
        /// <param name="eventName">事件名称。</param>
        /// <param name="parameters">事件属性字典。</param>
        /// <param name="eventId">事件唯一标识，用于匹配覆写。</param>
        public void TrackOverwritable(string eventName, Dictionary<string, object> parameters, string eventId)
        {
            var overwritableEvent = new TDOverwritableEventModel(eventName, eventId);
            overwritableEvent.Properties = parameters;
            TDAnalytics.Track(overwritableEvent);
        }
    }
}
#endif
