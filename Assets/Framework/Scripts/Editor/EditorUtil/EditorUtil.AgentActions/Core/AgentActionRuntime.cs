/***************************************************************
 * (c) copyright 2026 - 2030, Solotopia
 * All Rights Reserved.
 * -------------------------------------------------------------
 * filename:  AgentActionRuntime.cs
 * author:    taoye
 * created:   2026/8/20
 * descrip:   Agent Action 内核运行时服务与 Unity 主线程边界
 ***************************************************************/

using System;
using System.Threading;
using UnityEditor;

namespace NovaFramework.Editor
{
    internal interface IAgentActionClock
    {
        DateTime UtcNow { get; }
    }

    internal interface IAgentActionIdGenerator
    {
        string NewId();
    }

    [InitializeOnLoad]
    internal static class AgentActionRuntime
    {
        private static readonly int s_MainThreadId;

        /// <summary>
        /// 在 Unity 主线程初始化生产运行时服务。
        /// </summary>
        static AgentActionRuntime()
        {
            s_MainThreadId = Thread.CurrentThread.ManagedThreadId;
            Clock = new SystemClock();
            IdGenerator = new GuidIdGenerator();
            PlanStore = new AgentActionPlanStore(Clock, IdGenerator, 256, TimeSpan.FromMinutes(30));
            OperationStore = new AgentActionOperationStore(Clock, IdGenerator);
        }

        internal static IAgentActionClock Clock { get; private set; }

        internal static IAgentActionIdGenerator IdGenerator { get; private set; }

        internal static AgentActionPlanStore PlanStore { get; private set; }

        internal static AgentActionOperationStore OperationStore { get; private set; }

        /// <summary>
        /// Agent Action 会访问 Unity Editor API，只允许在 Unity 主线程进入。
        /// </summary>
        internal static bool IsMainThread => Thread.CurrentThread.ManagedThreadId == s_MainThreadId;

        /// <summary>
        /// 为定向测试替换确定性时钟、ID 和存储；生产代码不应调用。
        /// </summary>
        internal static void ConfigureForTests(
            IAgentActionClock clock,
            IAgentActionIdGenerator idGenerator,
            AgentActionPlanStore planStore,
            AgentActionOperationStore operationStore)
        {
            Clock = clock ?? throw new ArgumentNullException(nameof(clock));
            IdGenerator = idGenerator ?? throw new ArgumentNullException(nameof(idGenerator));
            PlanStore = planStore ?? throw new ArgumentNullException(nameof(planStore));
            OperationStore = operationStore ?? throw new ArgumentNullException(nameof(operationStore));
        }

        private sealed class SystemClock : IAgentActionClock
        {
            public DateTime UtcNow => DateTime.UtcNow;
        }

        private sealed class GuidIdGenerator : IAgentActionIdGenerator
        {
            /// <summary>
            /// 生成无分隔符的随机 ID。
            /// </summary>
            public string NewId() => Guid.NewGuid().ToString("N");
        }
    }
}
