/***************************************************************
 * (c) copyright 2026 - 2030, Solotopia
 * All Rights Reserved.
 * -------------------------------------------------------------
 * filename:  TrackEvent.cs
 * author:    taoye
 * created:   2026/4/28
 * descrip:   通用自定义埋点事件数据类
 ***************************************************************/

using System.Collections.Generic;

namespace NovaFramework.Runtime
{
    /// <summary>
    /// 通用自定义埋点事件载荷，由 ITrackPlugin.TrackEvent 接受并上报至分析 SDK。
    /// 不变量：Name 非空；Parameters 可为 null，表示无附加属性。
    /// </summary>
    public sealed class TrackEvent
    {
        /// <summary>
        /// 事件名称，不可为空。各分析平台要求的命名规则由 Plugin 实现层负责转换。
        /// </summary>
        public string Name;

        /// <summary>
        /// 事件附加属性键值对，key 为属性名，value 为属性值（string/int/double/bool 等）。
        /// 为 null 表示无附加属性；Plugin 实现层负责将 object 值映射到平台类型。
        /// </summary>
        public Dictionary<string, object> Parameters;
    }
}
