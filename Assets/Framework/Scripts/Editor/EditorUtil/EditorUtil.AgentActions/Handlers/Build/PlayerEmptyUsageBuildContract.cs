/***************************************************************
 * (c) copyright 2026 - 2030, Solotopia
 * All Rights Reserved.
 * -------------------------------------------------------------
 * filename:  PlayerEmptyUsageBuildContract.cs
 * author:    taoye
 * created:   2026/10/16
 * descrip:   SRP空global与Adaptive空metadata的正常usage有限合同
 ***************************************************************/

using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Runtime.CompilerServices;
using UnityEditor;
using UnityEditor.Build.Reporting;
using UnityEngine;
using UnityEngine.Rendering;

namespace NovaFramework.Editor
{
    /// <summary>不清表、不调用收集/发送/池Get；只核准确空分支和官方允许的纯内存后态。</summary>
    internal static class PlayerEmptyUsageBuildContract
    {
        internal const string SrpMarker = "EmptyUsage=SRPNullGlobal";
        internal const string AdaptiveMarker = "EmptyUsage=AdaptiveEmptyMetadata";
        private const BindingFlags Static = BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Static;
        private const BindingFlags Instance = BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance;

        /// <summary>池内现存空列表身份与capacity；不枚举任何实际业务payload。</summary>
        [Serializable]
        internal sealed class PoolRow { public int identity; public int capacity; }

        /// <summary>配置、容器准确身份及原始缓存材料，正式构建前使用完整值核对。</summary>
        [Serializable]
        internal sealed class Snapshot
        {
            public bool srp;
            public bool adaptive;
            public bool enabled;
            public bool globalNull;
            public int poolIdentity;
            public int stackIdentity;
            public int countAll;
            public PoolRow[] rows;
            public int metadataIdentity;
            public int addRequestsIdentity;
            public string metadataCacheSha256;
            public string comparison;
            public int comparisonOwner;
        }

        /// <summary>真实报告范围；不猜测缓存场景是否逐次重跑callback，也不声称native送达。</summary>
        [Serializable]
        internal sealed class ReportMaterial
        {
            public string reportGuid;
            public string platform;
            public bool analyticsEnabled;
            public bool srpNullGlobal;
            public bool adaptiveProvidersNull;
            public string adaptiveVersion;
        }

        /// <summary>读准确native global与backing；允许已审纯池/metadata cctor自然初始化，绝不主动清空或调用provider。</summary>
        /// <param name="inputs">准确两个分支marker。</param>
        /// <returns>真实自然状态；非空或未知输入直接拒绝。</returns>
        internal static Snapshot Capture(IEnumerable<string> inputs)
        {
            var state = new Snapshot { srp = inputs.Contains(SrpMarker), adaptive = inputs.Contains(AdaptiveMarker) };
            if (!state.srp && !state.adaptive) return state;
            state.enabled = EditorAnalytics.enabled;
            if (EditorUserBuildSettings.activeBuildTarget != BuildTarget.WebGL) throw new InvalidOperationException("空usage只支持当前自然WebGL域。");
            // 三个Core usage合同已约束同一SendAnalytic下游；这里也独立拒绝未知外部订阅。
            if (Read(typeof(EditorAnalytics).Assembly.GetType("UnityEditor.DebuggerEventListHandler", true), "analyticSent", null) != null) throw new InvalidOperationException("空usage存在未审analyticSent下游。");
            if (state.srp && state.enabled) CaptureSrp(state);
            if (state.adaptive && state.enabled) CaptureAdaptive(state);
            return state;
        }

        /// <summary>仅读当前null global、准确ListPool私有backing和现存空列表，不调用ForEach或池Get。</summary>
        /// <param name="state">写入本轮独立snapshot。</param>
        private static void CaptureSrp(Snapshot state)
        {
            if (GraphicsSettings.currentRenderPipeline != null || GraphicsSettings.defaultRenderPipeline != null) throw new InvalidOperationException("SRP当前/默认pipeline非空，不读取或实例化设置。");
            PropertyInfo global = typeof(GraphicsSettings).GetProperty("currentRenderPipelineGlobalSettings", Static);
            if (global?.DeclaringType != typeof(GraphicsSettings) || global.PropertyType != typeof(RenderPipelineGlobalSettings)) throw new InvalidOperationException("SRP只读native global getter身份变化。");
            state.globalNull = (UnityEngine.Object)global.GetValue(null) == null;
            if (!state.globalNull) throw new InvalidOperationException("SRP当前global settings非空，Activator/设置payload超出空分支。");
            Type data = Find("UnityEditor.Rendering.RenderPipelineGraphicsSettingsAnalytics+Analytic+AnalyticsData");
            Type listType = typeof(List<>).MakeGenericType(data), poolRoot = Find("UnityEngine.Rendering.ListPool`1").MakeGenericType(data);
            object pool = Read(poolRoot, "s_Pool", null);
            Type poolType = Find("UnityEngine.Rendering.ObjectPool`1").MakeGenericType(listType);
            if (pool?.GetType() != poolType || Read(poolType, "m_ActionOnGet", pool) != null || !Equals(Read(poolType, "m_CollectionCheck", pool), true)) throw new InvalidOperationException("SRP pool准确身份/Get委托/检查配置变化。");
            Delegate release = Read(poolType, "m_ActionOnRelease", pool) as Delegate;
            Type clearType = poolRoot.GetNestedType("<>c", BindingFlags.Public | BindingFlags.NonPublic).MakeGenericType(data);
            object clearOwner = Read(clearType, "<>9", null);
            if (release == null || release.GetInvocationList().Length != 1 || release.Method.Name != "<.cctor>b__4_0" || release.Method.DeclaringType != clearType || !ReferenceEquals(release.Target, clearOwner)) throw new InvalidOperationException("SRP pool Release不再是准确原List.Clear委托。");
            object stack = Read(poolType, "m_Stack", pool);
            if (stack?.GetType() != typeof(Stack<>).MakeGenericType(listType)) throw new InvalidOperationException("SRP pool stack身份变化。");
            var rows = new List<PoolRow>();
            foreach (object item in (IEnumerable)stack)
            {
                if (item?.GetType() != listType || (int)listType.GetProperty("Count").GetValue(item) != 0) throw new InvalidOperationException("SRP pool含非空或未知列表；不清除原payload。");
                rows.Add(new PoolRow { identity = RuntimeHelpers.GetHashCode(item), capacity = (int)listType.GetProperty("Capacity").GetValue(item) });
            }
            state.poolIdentity = RuntimeHelpers.GetHashCode(pool); state.stackIdentity = RuntimeHelpers.GetHashCode(stack);
            state.countAll = (int)Read(poolType, "<countAll>k__BackingField", pool); state.rows = rows.ToArray();
            if (state.countAll < state.rows.Length) throw new InvalidOperationException("SRP pool计数形状无效。");
        }

        /// <summary>准确空Dictionary阻止所有provider getter；metadata cctor仅空容器、一次已知Play状态订阅和只读状态查询。</summary>
        /// <param name="state">写入本轮独立snapshot。</param>
        private static void CaptureAdaptive(Snapshot state)
        {
            Type store = Find("UnityEditor.AdaptivePerformance.Editor.Metadata.AdaptivePerformancePackageMetadataStore");
            object packages = Read(store, "s_Packages", null), requests = Read(store, "m_AddRequests", null);
            if (packages == null || !packages.GetType().IsGenericType || packages.GetType().GetGenericTypeDefinition() != typeof(Dictionary<,>) || ((IDictionary)packages).Count != 0 ||
                requests == null || !requests.GetType().IsGenericType || requests.GetType().GetGenericTypeDefinition() != typeof(List<>) || ((ICollection)requests).Count != 0 || Read(store, "s_SearchRequest", null) != null)
                throw new InvalidOperationException("Adaptive metadata有真实provider/请求；不清空、不调用getter或创建业务设置。");
            Type cache = store.GetNestedType("<>c", BindingFlags.Public | BindingFlags.NonPublic);
            object owner = Read(cache, "<>9", null);
            Delegate comparison = Read(cache, "<>9__37_0", null) as Delegate;
            if (owner == null || comparison != null && (comparison.GetInvocationList().Length != 1 || comparison.Method.DeclaringType != cache || comparison.Method.Name != "<GetLoadersForBuildTarget>b__37_0" || !ReferenceEquals(comparison.Target, owner))) throw new InvalidOperationException("Adaptive比较cache方法或owner未知。");
            state.metadataIdentity = RuntimeHelpers.GetHashCode(packages); state.addRequestsIdentity = RuntimeHelpers.GetHashCode(requests);
            state.metadataCacheSha256 = Hash(NovaFramework.Runtime.Util.Json.Serialize(Read(store, "s_CachedMDStoreInformation", null)));
            state.comparison = comparison == null ? "null" : "known"; state.comparisonOwner = RuntimeHelpers.GetHashCode(owner);
        }

        /// <summary>仅投影首次空池可能创建一空列表及可选comparison缓存；原容器/已存在列表仍受后验保护。</summary>
        /// <param name="state">真实原值。</param>
        /// <returns>独立不变量摘要。</returns>
        internal static string InvariantHash(Snapshot state)
        {
            if (state == null) throw new InvalidOperationException("空usage材料缺失。");
            Snapshot clone = NovaFramework.Runtime.Util.Json.Deserialize<Snapshot>(NovaFramework.Runtime.Util.Json.Serialize(state));
            if (clone.srp && clone.enabled && clone.rows != null && clone.rows.Length <= 1)
            {
                clone.countAll = clone.countAll - clone.rows.Length + 1;
                clone.rows = new[] { new PoolRow() };
            }
            if (clone.adaptive && clone.enabled) clone.comparison = "known";
            return Hash(NovaFramework.Runtime.Util.Json.Serialize(clone));
        }

        /// <summary>写前必须完整真实值相等，不能用正常pool/comparison投影跳过新状态。</summary>
        /// <param name="before">Plan原值。</param>
        /// <param name="current">执行前当前值。</param>
        /// <returns>完整值一致为true。</returns>
        internal static bool BeforeEquals(Snapshot before, Snapshot current) => before != null && current != null && NovaFramework.Runtime.Util.Json.Serialize(before) == NovaFramework.Runtime.Util.Json.Serialize(current);

        /// <summary>接受未Gather原态或一次准确空池借还；不要求native注册成功，也不重放Gather。</summary>
        /// <param name="before">真实原值。</param>
        /// <param name="after">正式链后值。</param>
        /// <param name="current">当前值。</param>
        /// <param name="error">准确漂移原因。</param>
        /// <returns>唯一正常内存变化成立时为true。</returns>
        internal static bool TryValidateAfter(Snapshot before, Snapshot after, Snapshot current, out string error)
        {
            error = null;
            if (before == null || after == null || current == null) error = "空usage before/after/current缺失。";
            else if (InvariantHash(before) != InvariantHash(after) || InvariantHash(after) != InvariantHash(current) || !BeforeEquals(after, current)) error = "空usage配置、原容器或metadata发生未知变化。";
            else if (before.srp && before.enabled && (before.rows == null || after.rows == null || before.rows.Length != 0 && NovaFramework.Runtime.Util.Json.Serialize(before.rows) != NovaFramework.Runtime.Util.Json.Serialize(after.rows) ||
                before.rows.Length == 0 && (after.rows.Length > 1 || after.countAll != before.countAll + after.rows.Length || after.rows.Any(row => row.capacity != 0)))) error = "SRP原列表被替换或空池效果超出未Gather/一次借还。";
            else if (before.adaptive && before.enabled && (after.comparison != "null" && after.comparison != "known" || before.comparison == "known" && after.comparison != "known")) error = "Adaptive comparison缓存后验未知。";
            return error == null;
        }

        /// <summary>只复制本轮官方报告确定的有限空payload范围，不统计未观察的实际场景回调次数。</summary>
        /// <param name="before">原空状态。</param>
        /// <param name="report">真实报告。</param>
        /// <returns>明确本地范围材料或inactive null。</returns>
        internal static ReportMaterial CaptureReport(Snapshot before, BuildReport report)
        {
            if (before == null || report == null) throw new InvalidOperationException("空usage真实报告缺失。");
            if (!before.srp && !before.adaptive) return null;
            if (report.summary.platform != BuildTarget.WebGL) throw new InvalidOperationException("空usage报告不是当前WebGL。");
            return new ReportMaterial { reportGuid = report.summary.guid.ToString(), platform = "WebGL", analyticsEnabled = before.enabled, srpNullGlobal = before.srp && before.enabled, adaptiveProvidersNull = before.adaptive && before.enabled, adaptiveVersion = string.Empty };
        }

        /// <summary>旧或不属于本轮的报告材料保持partial，不补发送或造场景次数。</summary>
        /// <param name="before">原状态。</param>
        /// <param name="material">真实报告复制材料。</param>
        /// <param name="guid">正式回执GUID。</param>
        /// <param name="error">具体不足。</param>
        /// <returns>准确范围一致时为true。</returns>
        internal static bool TryValidateReport(Snapshot before, ReportMaterial material, string guid, out string error)
        {
            error = null;
            if (before == null) error = "空usage原状态缺失。";
            else if (!before.srp && !before.adaptive) { if (material != null) error = "inactive空usage含其他报告。"; }
            else if (material == null || string.IsNullOrEmpty(guid) || material.reportGuid != guid || material.platform != "WebGL" || material.analyticsEnabled != before.enabled || material.srpNullGlobal != (before.srp && before.enabled) || material.adaptiveProvidersNull != (before.adaptive && before.enabled) || material.adaptiveVersion != string.Empty) error = "空usage准确报告范围不一致。";
            return error == null;
        }

        /// <summary>只定位已加载准确类型；转发入口返回同一实际 Type 时去重，不加载程序集或实例，多个真实不同类型仍拒绝。</summary>
        /// <param name="name">合同固定的完整类型名；不按名称合并不同程序集中的实际类型。</param>
        /// <returns>去重后的唯一实际已加载 Type；缺失或多个真实不同 Type 时由 Single 拒绝。</returns>
        private static Type Find(string name) => AppDomain.CurrentDomain.GetAssemblies().Select(assembly => assembly.GetType(name, false)).Where(value => value != null).Distinct().Single();

        /// <summary>只读准确backing字段；不调用public provider或池入口。</summary>
        /// <param name="type">准确声明类型。</param>
        /// <param name="name">固定字段名。</param>
        /// <param name="instance">现存对象或static null。</param>
        /// <returns>真实字段值。</returns>
        private static object Read(Type type, string name, object instance)
        {
            FieldInfo field = type.GetField(name, instance == null ? Static : Instance);
            if (field?.DeclaringType != type) throw new InvalidOperationException("空usage backing身份变化：" + type.FullName + "." + name);
            return field.GetValue(instance);
        }

        /// <summary>只持久化摘要，业务cache原文不进入回执。</summary>
        /// <param name="value">驻内存的原值。</param>
        /// <returns>SHA256。</returns>
        private static string Hash(string value) => GenerateActionCommon.ComputeTextHash(value);
    }
}
