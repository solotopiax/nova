/***************************************************************
 * (c) copyright 2026 - 2030, Solotopia
 * All Rights Reserved.
 * -------------------------------------------------------------
 * filename:  AgentActionPlanStore.cs
 * author:    taoye
 * created:   2026/8/20
 * descrip:   有界、限时并支持原子消费的内存 Action 计划存储
 ***************************************************************/

using System;
using System.Collections.Generic;
using System.Linq;

namespace NovaFramework.Editor
{
    internal sealed class AgentActionStoredPlan
    {
        public EditorUtil.AgentActions.RegisteredAction Action;
        public object HandlerState;
        public DateTime ExpiresAtUtc;
        public long RegistryGeneration;
        public string OperationId;
    }

    internal sealed class AgentActionPlanStore
    {
        private readonly object m_Gate = new object();
        private readonly Dictionary<string, AgentActionStoredPlan> m_Plans =
            new Dictionary<string, AgentActionStoredPlan>(StringComparer.Ordinal);
        private readonly IAgentActionClock m_Clock;
        private readonly IAgentActionIdGenerator m_IdGenerator;
        private readonly int m_Capacity;
        private readonly TimeSpan m_Lifetime;

        /// <summary>
        /// 建立具有确定容量、TTL、时钟与 ID 来源的计划存储。
        /// </summary>
        public AgentActionPlanStore(
            IAgentActionClock clock,
            IAgentActionIdGenerator idGenerator,
            int capacity,
            TimeSpan lifetime)
        {
            m_Clock = clock ?? throw new ArgumentNullException(nameof(clock));
            m_IdGenerator = idGenerator ?? throw new ArgumentNullException(nameof(idGenerator));
            m_Capacity = capacity > 0 ? capacity : throw new ArgumentOutOfRangeException(nameof(capacity));
            m_Lifetime = lifetime > TimeSpan.Zero ? lifetime : throw new ArgumentOutOfRangeException(nameof(lifetime));
        }

        /// <summary>
        /// 添加一次性计划。容量只会通过清理过期项释放，不驱逐仍有效计划。
        /// </summary>
        public bool TryAdd(AgentActionStoredPlan plan, out string planId, out DateTime expiresAtUtc)
        {
            if (plan == null) throw new ArgumentNullException(nameof(plan));
            lock (m_Gate)
            {
                PruneExpiredLocked();
                if (m_Plans.Count >= m_Capacity)
                {
                    planId = null;
                    expiresAtUtc = default;
                    DisposeState(plan.HandlerState);
                    return false;
                }

                planId = CreateUniqueIdLocked();
                expiresAtUtc = m_Clock.UtcNow.Add(m_Lifetime);
                plan.ExpiresAtUtc = expiresAtUtc;
                m_Plans.Add(planId, plan);
                return true;
            }
        }

        /// <summary>
        /// 原子取出并消费计划；无论后续确认或执行结果如何都不能再次取得。
        /// </summary>
        public bool TryTake(string planId, out AgentActionStoredPlan plan)
        {
            lock (m_Gate)
            {
                PruneExpiredLocked();
                if (string.IsNullOrWhiteSpace(planId) || !m_Plans.TryGetValue(planId, out plan))
                {
                    plan = null;
                    return false;
                }

                m_Plans.Remove(planId);
                return true;
            }
        }

        /// <summary>
        /// 移除尚未执行的计划，并释放 Handler 提供的可清理状态。
        /// </summary>
        public void Remove(string planId)
        {
            lock (m_Gate)
            {
                if (string.IsNullOrWhiteSpace(planId) || !m_Plans.TryGetValue(planId, out AgentActionStoredPlan plan))
                {
                    return;
                }

                m_Plans.Remove(planId);
                DisposeState(plan.HandlerState);
            }
        }

        /// <summary>
        /// 在持有存储锁时清理过期计划及其可释放状态。
        /// </summary>
        private void PruneExpiredLocked()
        {
            DateTime now = m_Clock.UtcNow;
            string[] expired = m_Plans
                .Where(item => item.Value.ExpiresAtUtc <= now)
                .Select(item => item.Key)
                .ToArray();
            foreach (string id in expired)
            {
                AgentActionStoredPlan plan = m_Plans[id];
                m_Plans.Remove(id);
                DisposeState(plan.HandlerState);
            }
        }

        /// <summary>
        /// 在持有存储锁时生成当前存储内唯一的 Plan ID。
        /// </summary>
        private string CreateUniqueIdLocked()
        {
            for (int attempt = 0; attempt < 16; attempt++)
            {
                string id = m_IdGenerator.NewId();
                if (!string.IsNullOrWhiteSpace(id) && !m_Plans.ContainsKey(id))
                {
                    return id;
                }
            }
            throw new InvalidOperationException("无法生成唯一的 Action Plan ID。");
        }

        /// <summary>
        /// 释放 Handler 明确声明为 IDisposable 的临时计划状态。
        /// </summary>
        private static void DisposeState(object state)
        {
            if (state is IDisposable disposable)
            {
                disposable.Dispose();
            }
        }
    }
}
