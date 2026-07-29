/***************************************************************
 * (c) copyright 2026 - 2030, Solotopia
 * All Rights Reserved.
 * -------------------------------------------------------------
 * filename:  INetworkReadySignal.cs
 * author:    taoye
 * created:   2026/7/27
 * descrip:   框架内部 Network 路由就绪信号
 ***************************************************************/

using System.Threading;
using Cysharp.Threading.Tasks;

namespace NovaFramework.Runtime
{
    /// <summary>
    /// 框架内部 Network 路由就绪信号；不扩张项目自定义 INetworkManager 的公开实现契约。
    /// </summary>
    internal interface INetworkReadySignal
    {
        /// <summary>
        /// 等待 HostKey 与 NetCmd 路由成功构建；失败加载可重试，只有首次成功才完成信号。
        /// </summary>
        /// <param name="ct">等待生命周期取消令牌。</param>
        /// <returns>路由成功构建后完成的异步任务。</returns>
        UniTask WaitUntilReadyAsync(CancellationToken ct = default);
    }
}
