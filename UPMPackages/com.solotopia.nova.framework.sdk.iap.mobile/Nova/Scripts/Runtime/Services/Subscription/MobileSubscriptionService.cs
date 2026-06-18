/***************************************************************
 * (c) copyright 2026 - 2030, Solotopia
 * All Rights Reserved.
 * -------------------------------------------------------------
 * filename:  MobileSubscriptionService.cs
 * author:    yingzheng
 * created:   2026/5/25
 * descrip:   订阅到期时间持久化读写与倒计时自动触发
 ***************************************************************/

using System;
using System.Threading;
using Cysharp.Threading.Tasks;
using NovaFramework.Runtime;

namespace NovaFramework.SDK.IAP.Mobile.Runtime
{
    /// <summary>
    /// 订阅到期时间服务。
    /// 提供订阅到期时间戳的持久化读写，并在验单成功后启动倒计时，
    /// 到期时自动触发 MobileRestoreService.RestoreAsync 刷新订阅状态。
    /// </summary>
    internal sealed partial class MobileSubscriptionService
    {
        /// <summary>
        /// 构造 MobileSubscriptionService。
        /// </summary>
        /// <param name="hub">服务容器，持有共享外部依赖与其他服务引用。</param>
        internal MobileSubscriptionService(MobileServiceHub hub)
        {
            m_Hub = hub;
        }

        /// <summary>
        /// 将订阅到期时间戳写入统一存档 SubscriptionExpireMs 字典并启动倒计时。
        /// expireTimeMs &lt;= 0 时写入 0 表示已过期，不启动倒计时。
        /// </summary>
        /// <param name="tableId">订阅商品配置表行 ID。</param>
        /// <param name="expireTimeMs">服务端下发的到期 Unix 毫秒时间戳；&lt;= 0 表示已过期。</param>
        internal void SaveExpireTime(long tableId, long expireTimeMs)
        {
            var persist = m_Hub.Store?.PersistData;
            if (persist == null)
            {
                return;
            }

            persist.SubscriptionExpireMs[tableId] = expireTimeMs > 0L ? expireTimeMs : 0L;
            m_Hub.Store.SavePersistDataInternal();
            if (expireTimeMs > 0L)
            {
                StartCountdown(tableId, expireTimeMs);
            }
        }

        /// <summary>
        /// 从统一存档读取订阅到期时间戳（毫秒）。
        /// </summary>
        /// <param name="tableId">订阅商品配置表行 ID。</param>
        /// <returns>到期 Unix 毫秒时间戳；未存档时返回 0。</returns>
        internal long GetExpireTimeMs(long tableId)
        {
            var persist = m_Hub.Store?.PersistData;
            if (persist?.SubscriptionExpireMs == null)
            {
                return 0L;
            }

            return persist.SubscriptionExpireMs.TryGetValue(tableId, out long ms) ? ms : 0L;
        }

        /// <summary>
        /// 启动订阅到期倒计时。到期后自动触发 RestoreAsync 刷新订阅状态。
        /// 新倒计时启动时取消旧的（同一 tableId 同时只有一个倒计时生效）。
        /// </summary>
        /// <param name="tableId">订阅商品配置表行 ID。</param>
        /// <param name="expireTimeMs">到期 Unix 毫秒时间戳。</param>
        internal void StartCountdown(long tableId, long expireTimeMs)
        {
            // 取消旧倒计时
            m_CountdownCts?.Cancel();
            m_CountdownCts?.Dispose();
            // 创建新的取消源
            m_CountdownCts = new CancellationTokenSource();
            var ct = m_CountdownCts.Token;
            // 异步倒计时，不阻塞调用方
            RunCountdownAsync(tableId, expireTimeMs, ct).Forget();
        }

        /// <summary>
        /// 释放倒计时资源。
        /// </summary>
        internal void Dispose()
        {
            // 中断正在运行的倒计时
            m_CountdownCts?.Cancel();
            // 释放 CTS 资源
            m_CountdownCts?.Dispose();
            // 防止二次 Cancel
            m_CountdownCts = null;
        }
    }
}
