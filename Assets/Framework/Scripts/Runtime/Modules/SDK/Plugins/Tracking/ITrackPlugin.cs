/***************************************************************
 * (c) copyright 2026 - 2030, Solotopia
 * All Rights Reserved.
 * -------------------------------------------------------------
 * filename:  ITrackPlugin.cs
 * author:    taoye
 * created:   2026/4/28
 * descrip:   通用埋点 SDK 插件接口
 ***************************************************************/

using System.Collections.Generic;

namespace NovaFramework.Runtime
{
    /// <summary>
    /// 通用事件埋点接口。
    /// 实现方负责将用户属性与自定义事件上报至对应分析平台（如 TGA）。
    /// 所有方法主线程调用；实现内部可切后台线程，但不得向调用方抛出与线程相关的异常。
    /// </summary>
    public interface ITrackPlugin : ISDKPlugin
    {
        /// <summary>
        /// 设置当前用户 ID，用于跨会话关联行为数据。
        /// 应在用户登录成功后立即调用；未登录时传空字符串或 null 以清空。
        /// </summary>
        /// <param name="userId">用户唯一标识，由业务层管理格式与长度约束。</param>
        void SetUserId(string userId);

        /// <summary>
        /// 设置用户属性键值对，可多次调用以覆盖已有属性。
        /// 同一 key 多次赋值以最后一次为准；各平台对 key/value 长度有不同限制，由实现层处理截断。
        /// </summary>
        /// <param name="key">属性名称，不可为空。</param>
        /// <param name="value">属性值，各平台自行转换类型；不可为 null。</param>
        void SetUserProperty(string key, string value);

        /// <summary>
        /// 上报一条自定义事件。
        /// 主线程调用；实现层将事件名与参数按平台协议序列化并发送。
        /// </summary>
        /// <param name="evt">事件载荷，Name 不可为空；Parameters 可为 null 表示无附加属性。</param>
        void TrackEvent(TrackEvent evt);

        /// <summary>
        /// 以事件名和参数字典上报一条自定义事件。
        /// 主线程调用；parameters 可为 null 或空字典，表示无附加属性。
        /// 各平台对键名长度与值类型有差异，由实现层负责转换与截断。
        /// </summary>
        /// <param name="eventName">事件名称，不可为空。</param>
        /// <param name="parameters">附加参数字典，键为属性名，值支持基础类型；可为 null。</param>
        void TrackEvent(string eventName, Dictionary<string, object> parameters);
    }
}
