/***************************************************************
 * (c) copyright 2026 - 2030, Solotopia
 * All Rights Reserved.
 * -------------------------------------------------------------
 * filename:  IPushPlugin.cs
 * author:    taoye
 * created:   2026/4/28
 * descrip:   推送通知 SDK 插件接口
 ***************************************************************/

using System;
using System.Threading;
using Cysharp.Threading.Tasks;

namespace NovaFramework.Runtime
{
    /// <summary>
    /// 推送通知接口，抽象 Firebase Cloud Messaging / APNs 等平台推送能力。
    /// 推送令牌由平台异步下发，可能在初始化后若干秒才就绪；OnTokenRefreshed 事件在令牌更新时通知业务层。
    /// 所有方法主线程调用；实现内部可切后台线程等待平台回调，完成前须切回主线程。
    /// </summary>
    public interface IPushPlugin : ISDKPlugin
    {
        /// <summary>
        /// 异步获取当前设备的推送令牌。
        /// 若令牌尚未就绪则等待平台下发；网络异常时抛对应异常由调用方处理。
        /// </summary>
        /// <param name="ct">取消令牌，默认不取消。</param>
        /// <returns>推送令牌，含平台标识与令牌字符串。</returns>
        UniTask<PushToken> GetTokenAsync(CancellationToken ct = default);

        /// <summary>
        /// 订阅或取消订阅指定推送主题（Topic）。
        /// 主题订阅用于群发通知；subscribed=true 订阅，false 取消订阅。
        /// 同步发起请求，后台网络操作失败时 Plugin 实现层记录日志，不向调用方抛异常。
        /// </summary>
        /// <param name="topic">主题名称，由业务层定义（如 "news"、"events"）。</param>
        /// <param name="subscribed">true 订阅，false 取消订阅。</param>
        void SetTopicSubscribed(string topic, bool subscribed);

        /// <summary>
        /// 推送令牌刷新事件，在平台颁发新令牌时于主线程触发。
        /// 业务层应监听此事件并将新令牌上报至游戏服务器以维持推送有效性。
        /// </summary>
        event Action<PushToken> OnTokenRefreshed;
    }
}
