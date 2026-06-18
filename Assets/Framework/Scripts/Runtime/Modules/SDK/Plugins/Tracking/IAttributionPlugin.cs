/***************************************************************
 * (c) copyright 2026 - 2030, Solotopia
 * All Rights Reserved.
 * -------------------------------------------------------------
 * filename:  IAttributionPlugin.cs
 * author:    taoye
 * created:   2026/4/28
 * descrip:   归因 SDK 插件接口
 ***************************************************************/

using System;
using System.Collections.Generic;
using System.Threading;
using Cysharp.Threading.Tasks;

namespace NovaFramework.Runtime
{
    /// <summary>
    /// 归因打点接口：用户身份 + 事件上报 + 归因数据获取（AppsFlyer / Adjust 等）。
    /// 归因数据通常在安装后首次启动时由平台服务器异步返回，回调时间不确定。
    /// 所有方法主线程调用；实现内部可在后台线程等待 Native 回调，完成前须切回主线程。
    /// </summary>
    public interface IAttributionPlugin : ISDKPlugin
    {
        /// <summary>
        /// 设置当前用户 ID，用于将事件与归因数据关联至同一用户。
        /// 应在用户登录成功后立即调用；未登录时传空字符串或 null 以清空。
        /// </summary>
        /// <param name="userId">用户唯一标识，由业务层管理格式与长度约束。</param>
        void SetUserId(string userId);

        /// <summary>
        /// 上报一条自定义归因事件。
        /// 主线程调用；实现层将事件名与参数按平台协议序列化并发送。
        /// </summary>
        /// <param name="evt">事件载荷，Name 不可为空；Parameters 可为 null 表示无附加属性。</param>
        void TrackEvent(TrackEvent evt);

        /// <summary>
        /// 以事件名和参数字典上报一条自定义归因事件。
        /// 主线程调用；parameters 可为 null 或空字典，表示无附加属性。
        /// 各平台对键名长度与值类型有差异，由实现层负责转换与截断。
        /// </summary>
        /// <param name="eventName">事件名称，不可为空。</param>
        /// <param name="parameters">附加参数字典，键为属性名，值支持基础类型；可为 null。</param>
        void TrackEvent(string eventName, Dictionary<string, object> parameters);

        /// <summary>
        /// 异步获取归因数据。
        /// 若数据已缓存则立即返回；否则等待平台服务器下发，可能耗时数秒。
        /// 取消令牌触发后抛 OperationCanceledException；超时策略由调用方通过 CancellationToken 控制。
        /// </summary>
        /// <param name="ct">取消令牌，默认不取消。</param>
        /// <returns>归因数据载荷；平台数据缺失时各字段可为 null。</returns>
        UniTask<AttributionData> GetAttributionAsync(CancellationToken ct = default);

        /// <summary>
        /// 归因数据就绪事件，在平台首次下发或更新数据时触发。
        /// 事件在主线程触发；订阅方无需切换线程。
        /// </summary>
        event Action<AttributionData> OnAttributionResolved;
    }
}
