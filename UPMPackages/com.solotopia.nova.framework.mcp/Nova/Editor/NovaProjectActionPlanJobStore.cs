/***************************************************************
 * (c) copyright 2026 - 2030, Solotopia
 * All Rights Reserved.
 * -------------------------------------------------------------
 * filename:  NovaProjectActionPlanJobStore.cs
 * author:    taoye
 * created:   2026/10/16
 * descrip:   长计划的有界传输收件箱；不保存请求或可执行状态
 ***************************************************************/

using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Newtonsoft.Json;

namespace NovaFramework.Mcp.Editor
{
    /// <summary>仅保存已脱敏的传输结果，领域计划仍由原 Provider 与 Core 管理。</summary>
    internal sealed class NovaProjectActionPlanJobStore
    {
        /// <summary>一条不含原请求的传输登记与脱敏响应；不是可执行 Plan 或领域回执。</summary>
        internal sealed class Record
        {
            public string RequestId;
            public string ActionId;
            public string RequestHash;
            public string DomainId;
            public string State;
            public DateTime ExpiresAtUtc;
            public string ResponseJson;
        }

        private readonly Dictionary<string, Record> m_Records;
        private readonly Func<DateTime> m_UtcNow;
        private readonly Action<string> m_Persist;
        private readonly string m_DomainId;
        private readonly int m_Capacity;
        private readonly TimeSpan m_Lifetime;
        private const int MaxResponseBytes = 2 * 1024 * 1024;

        /// <summary>恢复本进程会话的结果记录；损坏或超容量的原记录直接拒绝，不清空未知状态。</summary>
        internal NovaProjectActionPlanJobStore(string serialized, string domainId, Func<DateTime> utcNow,
            Action<string> persist, int capacity, TimeSpan lifetime)
        {
            if (string.IsNullOrEmpty(domainId)) throw new ArgumentException(nameof(domainId));
            m_UtcNow = utcNow ?? throw new ArgumentNullException(nameof(utcNow));
            m_Persist = persist ?? throw new ArgumentNullException(nameof(persist));
            m_DomainId = domainId;
            m_Capacity = capacity > 0 ? capacity : throw new ArgumentOutOfRangeException(nameof(capacity));
            m_Lifetime = lifetime > TimeSpan.Zero ? lifetime : throw new ArgumentOutOfRangeException(nameof(lifetime));
            Record[] records = string.IsNullOrEmpty(serialized) ? Array.Empty<Record>() :
                JsonConvert.DeserializeObject<Record[]>(serialized, new JsonSerializerSettings
                {
                    MissingMemberHandling = MissingMemberHandling.Error,
                    TypeNameHandling = TypeNameHandling.None,
                    MaxDepth = 16,
                });
            if (records == null || records.Length > capacity || records.Any(record => !IsValid(record)) ||
                records.Select(record => record.RequestId).Distinct(StringComparer.Ordinal).Count() != records.Length)
                throw new InvalidOperationException("长计划结果记录损坏；保留原会话数据并停止登记。");
            m_Records = records.ToDictionary(record => record.RequestId, StringComparer.Ordinal);
        }

        /// <summary>在当前有界记录中按请求编号去重；同号不同目标或内容拒绝，失效记录不自动重跑。</summary>
        internal bool TryBegin(string requestId, string actionId, string requestHash, out Record record,
            out bool created, out string error)
        {
            created = false;
            error = null;
            if (m_Records.TryGetValue(requestId, out record))
            {
                if (record.ActionId != actionId || record.RequestHash != requestHash)
                { error = "request_id_conflict"; return false; }
                Refresh(record);
                return true;
            }
            if (m_Records.Values.Any(item => IsActiveAfterRefresh(item)))
            { error = "action_busy"; return false; }
            if (m_Records.Count >= m_Capacity)
            { error = "plan_job_capacity"; return false; }
            record = new Record
            {
                RequestId = requestId, ActionId = actionId, RequestHash = requestHash,
                DomainId = m_DomainId, State = "queued", ExpiresAtUtc = m_UtcNow().Add(m_Lifetime),
            };
            m_Records.Add(requestId, record);
            try { Persist(); }
            catch { m_Records.Remove(requestId); throw; }
            created = true;
            return true;
        }

        /// <summary>读取同一目标的结果；重载和超时只改变传输状态，不恢复任何领域调用。</summary>
        internal Record Get(string requestId, string actionId)
        {
            if (!m_Records.TryGetValue(requestId, out Record record) || record.ActionId != actionId) return null;
            Refresh(record);
            return record;
        }

        /// <summary>当前仅允许一项未结束计划；失效记录仍占容量，需调用方明确忘记才能释放。</summary>
        internal bool HasActive()
        {
            return m_Records.Values.Any(item => IsActiveAfterRefresh(item));
        }

        /// <summary>仅允许同域、未过期且尚未开始的登记进入运行态，重复调度不会再次进入 Provider。</summary>
        internal bool TryMarkRunning(string requestId, string actionId)
        {
            Record record = Get(requestId, actionId);
            if (record == null || record.State != "queued") return false;
            record.State = "running";
            Persist();
            return true;
        }

        /// <summary>保存原网关已脱敏响应；超大响应明确失败，不截断后伪称完整，也不重跑原计划。</summary>
        internal void Complete(string requestId, string actionId, string responseJson)
        {
            Record record = Get(requestId, actionId);
            if (record == null || record.State != "running") return;
            if (Encoding.UTF8.GetByteCount(responseJson ?? string.Empty) > MaxResponseBytes)
            {
                record.State = "response_too_large";
                record.ResponseJson = null;
            }
            else
            {
                record.State = "completed";
                record.ResponseJson = responseJson;
            }
            Persist();
        }

        /// <summary>只删除已结束的传输记录；不会消费、移除或重放 Core Plan 与 Operation。</summary>
        internal bool Forget(string requestId, string actionId)
        {
            Record record = Get(requestId, actionId);
            if (record == null || record.State == "queued" || record.State == "running") return false;
            m_Records.Remove(requestId);
            try { Persist(); }
            catch { m_Records.Add(requestId, record); throw; }
            return true;
        }

        /// <summary>按当前时钟刷新状态后判断活动性，避免一个过期登记永久阻断新计划。</summary>
        private bool IsActiveAfterRefresh(Record record)
        {
            Refresh(record);
            return record.State == "queued" || record.State == "running";
        }

        /// <summary>过期清除结果但保留去重墓碑；域变化统一中断，保留旧响应只供诊断且不可执行。</summary>
        private void Refresh(Record record)
        {
            string previous = record.State;
            if (m_UtcNow() >= record.ExpiresAtUtc)
            { record.State = "expired"; record.ResponseJson = null; }
            else if (record.DomainId != m_DomainId && record.State != "interrupted")
                record.State = "interrupted";
            if (record.State != previous) Persist();
        }

        /// <summary>将完整自有收件箱写到调用方提供的会话存储；不序列化请求、凭据或 HandlerState。</summary>
        private void Persist()
        {
            m_Persist(JsonConvert.SerializeObject(m_Records.Values.OrderBy(item => item.RequestId, StringComparer.Ordinal)));
        }

        /// <summary>恢复时拒绝未知结构和状态；请求编号为调用方生成的 N 格式 GUID。</summary>
        private static bool IsValid(Record record)
        {
            return record != null && Guid.TryParseExact(record.RequestId, "N", out _) &&
                   !string.IsNullOrEmpty(record.ActionId) && !string.IsNullOrEmpty(record.RequestHash) &&
                   !string.IsNullOrEmpty(record.DomainId) && record.ExpiresAtUtc.Kind == DateTimeKind.Utc &&
                   new[] { "queued", "running", "completed", "interrupted", "expired", "response_too_large" }.Contains(record.State) &&
                   Encoding.UTF8.GetByteCount(record.ResponseJson ?? string.Empty) <= MaxResponseBytes;
        }
    }
}
