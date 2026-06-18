/***************************************************************
 * (c) copyright 2026 - 2030, Solotopia
 * All Rights Reserved.
 * -------------------------------------------------------------
 * filename:  MobileSubscriptionService.Methods.cs
 * author:    yingzheng
 * created:   2026/5/28
 * descrip:   MobileSubscriptionService 内部方法
 ***************************************************************/

using System;
using System.Threading;
using Cysharp.Threading.Tasks;
using NovaFramework.Runtime;

namespace NovaFramework.SDK.IAP.Mobile.Runtime
{
    internal sealed partial class MobileSubscriptionService
    {
        /// <summary>
        /// 订阅到期倒计时：等待到期时间（含 30 秒缓冲）后触发 RestoreAsync 刷新订阅状态。
        /// 30 秒缓冲确保服务端订阅状态已更新，避免刚好到期时拉到旧状态。
        /// </summary>
        /// <param name="tableId">订阅商品配置表行 ID，用于日志标识。</param>
        /// <param name="expireTimeMs">到期 Unix 毫秒时间戳。</param>
        /// <param name="ct">取消令牌，倒计时被取消或 Dispose 时中断。</param>
        private async UniTaskVoid RunCountdownAsync(long tableId, long expireTimeMs, CancellationToken ct)
        {
            long nowMs = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
            // 到期时间 + 30 秒缓冲，确保服务端订阅状态已更新
            long delayMs = expireTimeMs - nowMs + 30_000L;
            if (delayMs <= 0L)
            {
                delayMs = 1_000L;
            }

            await UniTask.Delay(TimeSpan.FromMilliseconds(delayMs), cancellationToken: ct);

            if (ct.IsCancellationRequested)
            {
                return;
            }

            Log.Debug(LogTag.IAPMobile, $"订阅倒计时到期，商品表ID={tableId}，触发恢复流程。");
            if (m_Hub.RestoreService != null)
            {
                await m_Hub.RestoreService.RestoreAsync(CancellationToken.None);
            }
        }
    }
}
