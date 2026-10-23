/***************************************************************
 * (c) copyright 2026 - 2030, Solotopia
 * All Rights Reserved.
 * -------------------------------------------------------------
 * filename:  PlayerFiniteAdditionalBuildContract.cs
 * author:    taoye
 * created:   2026/10/16
 * descrip:   WebGL 五个准确回调的自然配置、池与正常 usage 合同
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
    /// <summary>五个准确 WebGL 回调的有限自然分支；只捕获输入和正常后态，不调用回调、发送、池或设置初始化入口。</summary>
    internal static class PlayerFiniteAdditionalBuildContract
    {
        internal const string Basic = "FiniteAdditional=AdaptiveBasic";
        internal const string General = "FiniteAdditional=AdaptiveGeneral";
        internal const string Stripper = "FiniteAdditional=GlobalStripper";
        internal const string BuildTarget = "FiniteAdditional=BuildTargetUsage";
        internal const string Input = "FiniteAdditional=InputBuildUsage";
        private const BindingFlags Static = BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Static;
        private const BindingFlags Instance = BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance;

        /// <summary>原有空列表的准确身份与容量；从不保存或清除原 payload。</summary>
        [Serializable]
        internal sealed class PoolRow { public int identity; public int capacity; }

        /// <summary>仅包含五个准确 marker 启用的实际输入和有限私有正常状态。</summary>
        [Serializable]
        internal sealed class Snapshot
        {
            public bool basic, general, stripper, buildTarget, input;
            public bool analyticsEnabled;
            public string adaptivePreloadedSha256;
            public bool stripperGlobalDirty, stripperGraphicsDirty;
            public int rosterCount, poolIdentity, listIdentity, poolCountAll, listCapacity;
            public PoolRow fresh;
            public PoolRow[] poolRows;
            public int activeQualityLevels, totalQualityLevels;
            public int inputManagerIdentity, inputRuntimeIdentity;
            public ulong inputSettingsIdentity, inputActionsIdentity;
            public string inputSettingsSha256, inputFeaturesSha256, inputSettingsRosterSha256, inputActionsPathSha256;
        }

        /// <summary>实际报告绑定的本地 usage 输入范围，不表示原生返回成功或远端送达。</summary>
        [Serializable]
        internal sealed class ReportMaterial
        {
            public string reportGuid, platform, inputSettingsSha256, inputFeaturesSha256, inputActionsPathSha256;
            public bool analyticsEnabled, buildTarget, input, inputRuntimePresent;
            public int activeQualityLevels, totalQualityLevels;
        }

        /// <summary>冻结已存在配置、准确 backing 与公开只读输入；不加载 provider、创建默认设置或借出列表。</summary>
        /// <param name="inputs">正式身份核对后加入的五个准确 marker。</param>
        /// <returns>本域实际快照；非审阅分支抛出具体原因。</returns>
        internal static Snapshot Capture(IEnumerable<string> inputs)
        {
            var state = new Snapshot { basic = inputs.Contains(Basic), general = inputs.Contains(General), stripper = inputs.Contains(Stripper), buildTarget = inputs.Contains(BuildTarget), input = inputs.Contains(Input) };
            if (!state.basic && !state.general && !state.stripper && !state.buildTarget && !state.input) return state;
            if (EditorUserBuildSettings.activeBuildTarget != UnityEditor.BuildTarget.WebGL) throw new InvalidOperationException("五项有限合同只支持真实 WebGL 活动域。");
            if (state.basic || state.general) CaptureAdaptive(state);
            if (state.stripper) CaptureStripper(state);
            if (state.buildTarget || state.input)
            {
                state.analyticsEnabled = UnityEditor.EditorAnalytics.enabled;
                Type debugger = typeof(UnityEditor.Editor).Assembly.GetType("UnityEditor.DebuggerEventListHandler", true);
                if (Read(debugger, "analyticSent", null) != null) throw new InvalidOperationException("五项 usage 存在未知 analyticSent 订阅。");
            }
            if (state.buildTarget) CaptureBuildTarget(state);
            if (state.input) CaptureInput(state);
            return state;
        }

        /// <summary>没有活动 profile 与两类配置时，预处理自然早退；后处理只能过滤原预加载表，因此原表不得含空槽或设置型。</summary>
        /// <param name="state">写入只读预加载表摘要。</param>
        private static void CaptureAdaptive(Snapshot state)
        {
            Type runtimeGeneral = Find("UnityEngine.AdaptivePerformance.AdaptivePerformanceGeneralSettings");
            if (!Equals(Read(runtimeGeneral, "k_SettingsKey", null), "com.unity.adaptiveperformance.loader_settings")) throw new InvalidOperationException("Adaptive 实际配置 key 已变化。");
            PropertyInfo profile = typeof(EditorUserBuildSettings).GetProperty("activeBuildProfile", Static);
            if (profile?.DeclaringType != typeof(EditorUserBuildSettings) || !ReferenceEquals(profile.GetValue(null), null)) throw new InvalidOperationException("Adaptive 活动 Build Profile 非空或 native getter 身份变化。");
            EditorBuildSettings.TryGetConfigObject("com.unity.adaptivePerformance.basic.provider_settings", out UnityEngine.Object basic);
            EditorBuildSettings.TryGetConfigObject("com.unity.adaptiveperformance.loader_settings", out UnityEngine.Object general);
            UnityEngine.Object[] preloaded = PlayerSettings.GetPreloadedAssets();
            string[] types = preloaded?.Select(value => ReferenceEquals(value, null) ? "null" : value == null ? "destroyed" : value.GetType().FullName).ToArray();
            if (!TryValidateAdaptiveInputs(ReferenceEquals(basic, null), ReferenceEquals(general, null), types, out string error)) throw new InvalidOperationException(error);
            state.adaptivePreloadedSha256 = Hash(string.Join("|", preloaded.Select(value => UnityEngine.EntityId.ToULong(value.GetEntityId()).ToString())));
        }

        /// <summary>纯数据校验拒绝真实 Adaptive 设置及会被 CleanOldSettings 删除的空槽；不以拟构建目标代替活动配置。</summary>
        /// <param name="basicNull">真实 Basic 配置槽是否为 managed null。</param>
        /// <param name="generalNull">真实 General 配置槽是否为 managed null。</param>
        /// <param name="types">按原顺序保留的预加载类型，空槽使用明确标记。</param>
        /// <param name="error">具体拒绝原因。</param>
        /// <returns>自然无配置且无删除效果时为 true。</returns>
        internal static bool TryValidateAdaptiveInputs(bool basicNull, bool generalNull, string[] types, out string error)
        {
            error = null;
            if (!basicNull || !generalNull) error = "Adaptive 已有 Basic/General 配置，provider 与预加载写入未进入此空分支。";
            else if (types == null || types.Any(value => value == "null" || value == "destroyed" || value == "UnityEngine.AdaptivePerformance.Basic.BasicProviderSettings" || value == "UnityEngine.AdaptivePerformance.AdaptivePerformanceGeneralSettings" || string.IsNullOrEmpty(value))) error = "Adaptive CleanOldSettings 将删除预加载空槽或设置；保留实际输入，不预清表。";
            return error == null;
        }

        /// <summary>完整全 null 管线 roster 阻止所有资产写；仅捕获原标量及已审 ListPool 的准确空列表 backing。</summary>
        /// <param name="state">写入真实池拓扑、计数与标量。</param>
        private static void CaptureStripper(Snapshot state)
        {
            if (GraphicsSettings.currentRenderPipeline != null || GraphicsSettings.defaultRenderPipeline != null || CurrentGlobal() != null) throw new InvalidOperationException("GlobalSettingsStripper 当前管线/global 非空，资产副作用不属于 builtin 分支。");
            MethodInfo group = typeof(BuildPipeline).GetMethod("GetBuildTargetGroupName", Static, null, new[] { typeof(UnityEditor.BuildTarget) }, null);
            MethodInfo all = typeof(QualitySettings).GetMethod("GetAllRenderPipelineAssetsForPlatform", Static, null, new[] { typeof(string), typeof(List<RenderPipelineAsset>).MakeByRefType() }, null);
            if (group?.ReturnType != typeof(string) || all?.ReturnType != typeof(void)) throw new InvalidOperationException("GlobalSettingsStripper roster API 身份改变。");
            var roster = new List<RenderPipelineAsset>(); object[] args = { (string)group.Invoke(null, new object[] { UnityEditor.BuildTarget.WebGL }), roster }; all.Invoke(null, args);
            if (!ReferenceEquals(args[1], roster) || roster.Any(value => !ReferenceEquals(value, null))) throw new InvalidOperationException("GlobalSettingsStripper 完整 roster 不是全 managed null。");
            state.rosterCount = roster.Count;
            Type stripper = typeof(UnityEditor.Editor).Assembly.GetType("UnityEditor.Build.Rendering.RenderPipelineGlobalSettingsStripper", true);
            state.stripperGlobalDirty = (bool)Read(stripper, "s_IsCurrentRenderPipelineGlobalsSettingsDirty", null); state.stripperGraphicsDirty = (bool)Read(stripper, "s_IsGraphicsSettingsDirty", null);
            Type collection = typeof(UnityEngine.Pool.CollectionPool<,>).MakeGenericType(typeof(List<RenderPipelineAsset>), typeof(RenderPipelineAsset));
            object pool = Read(collection, "s_Pool", null); Type poolType = typeof(UnityEngine.Pool.ObjectPool<List<RenderPipelineAsset>>);
            if (pool?.GetType() != poolType || Read(poolType, "m_ActionOnGet", pool) != null || Read(poolType, "m_ActionOnDestroy", pool) != null || !Equals(Read(poolType, "m_CollectionCheck", pool), true) || !Equals(Read(poolType, "m_MaxSize", pool), 10000)) throw new InvalidOperationException("GlobalSettingsStripper pool 类型、配置或外部委托变化。");
            Type compiler = collection.GetNestedType("<>c", BindingFlags.NonPublic).MakeGenericType(typeof(List<RenderPipelineAsset>), typeof(RenderPipelineAsset));
            object owner = Read(compiler, "<>9", null);
            RequirePoolDelegate(Read(poolType, "m_CreateFunc", pool) as Delegate, compiler, owner, "<.cctor>b__5_0"); RequirePoolDelegate(Read(poolType, "m_ActionOnRelease", pool) as Delegate, compiler, owner, "<.cctor>b__5_1");
            var list = Read(poolType, "m_List", pool) as List<List<RenderPipelineAsset>>;
            if (list == null || list.GetType() != typeof(List<List<RenderPipelineAsset>>)) throw new InvalidOperationException("GlobalSettingsStripper pool backing 列表形状变化。");
            state.poolIdentity = RuntimeHelpers.GetHashCode(pool); state.listIdentity = RuntimeHelpers.GetHashCode(list); state.listCapacity = list.Capacity;
            if (list.Any(value => ReferenceEquals(value, null))) throw new InvalidOperationException("GlobalSettingsStripper pool 含未知空列表条目。");
            state.poolRows = list.Select(PoolItem).ToArray(); state.fresh = PoolItem(Read(poolType, "m_FreshlyReleased", pool) as List<RenderPipelineAsset>);
            state.poolCountAll = (int)Read(poolType, "<CountAll>k__BackingField", pool);
            var identities = state.poolRows.Select(row => row.identity).Concat(state.fresh == null ? Array.Empty<int>() : new[] { state.fresh.identity }).ToArray();
            if (identities.Distinct().Count() != identities.Length || state.poolCountAll < identities.Length) throw new InvalidOperationException("GlobalSettingsStripper pool 原计数或列表归属不一致。");
        }

        /// <summary>核对两个准确编译器委托，不调用 create、clear 或外部回调。</summary>
        /// <param name="value">现存单播委托。</param>
        /// <param name="type">准确泛型编译器 owner 类型。</param>
        /// <param name="owner">准确现存 owner。</param>
        /// <param name="method">固定已审方法名。</param>
        private static void RequirePoolDelegate(Delegate value, Type type, object owner, string method)
        {
            if (value == null || value.GetInvocationList().Length != 1 || value.Method.DeclaringType != type || value.Method.Name != method || !ReferenceEquals(value.Target, owner)) throw new InvalidOperationException("GlobalSettingsStripper pool 委托不属于准确纯 List 构造/Clear 分支。");
        }

        /// <summary>复制准确空列表的身份和容量；池中的 null 或 payload 都拒绝。</summary>
        /// <param name="list">现存池列表，fresh 空槽可为 null。</param>
        /// <returns>空槽为 null；非空准确列表行。</returns>
        private static PoolRow PoolItem(List<RenderPipelineAsset> list)
        {
            if (list == null) return null;
            if (list.GetType() != typeof(List<RenderPipelineAsset>) || list.Count != 0) throw new InvalidOperationException("GlobalSettingsStripper pool 包含原 payload 或未知列表实现，不清除或接管。");
            return new PoolRow { identity = RuntimeHelpers.GetHashCode(list), capacity = list.Capacity };
        }

        /// <summary>冻结 BuildTarget analytic 实际 native 只读字段；内置管线不调用用户 pipeline 类型代码。</summary>
        /// <param name="state">写入质量级数量。</param>
        private static void CaptureBuildTarget(Snapshot state)
        {
            if (GraphicsSettings.currentRenderPipeline != null) throw new InvalidOperationException("BuildTarget analytic 非 builtin pipeline。");
            MethodInfo count = typeof(QualitySettings).GetMethod("GetActiveQualityLevelsForPlatformCount", Static, null, new[] { typeof(string) }, null);
            if (count?.ReturnType != typeof(int)) throw new InvalidOperationException("BuildTarget analytic quality count API 改变。");
            state.activeQualityLevels = (int)count.Invoke(null, new object[] { "WebGL" }); state.totalQualityLevels = QualitySettings.count;
        }

        /// <summary>仅接纳正常已初始化 InputSystem 和准确 native runtime；冻结设置/feature flags/现存设置对象，拒绝自定义运行时。</summary>
        /// <param name="state">写入准确身份与 payload 输入摘要。</param>
        private static void CaptureInput(Snapshot state)
        {
            Type system = Find("UnityEngine.InputSystem.InputSystem");
            if (!system.IsDefined(typeof(InitializeOnLoadAttribute), false)) throw new InvalidOperationException("InputSystem 正常初始化身份改变。");
            object manager = Read(system, "s_Manager", null);
            if (manager == null) return;
            Type managerType = system.Assembly.GetType("UnityEngine.InputSystem.InputManager", true);
            if (manager.GetType() != managerType) throw new InvalidOperationException("InputBuild manager 身份改变。");
            state.inputManagerIdentity = RuntimeHelpers.GetHashCode(manager);
            object runtime = Read(managerType, "m_Runtime", manager);
            if (runtime == null) return;
            Type native = system.Assembly.GetType("UnityEngine.InputSystem.LowLevel.NativeInputRuntime", true);
            if (runtime.GetType() != native || !ReferenceEquals(Read(native, "instance", null), runtime)) throw new InvalidOperationException("InputBuild 存在未审自定义 runtime，不发送或替换。");
            state.inputRuntimeIdentity = RuntimeHelpers.GetHashCode(runtime);
            Type settingsType = system.Assembly.GetType("UnityEngine.InputSystem.InputSettings", true);
            var settings = Read(managerType, "m_Settings", manager) as UnityEngine.Object;
            if (settings == null || settings.GetType() != settingsType) throw new InvalidOperationException("InputBuild 没有准确现存设置，不创建默认设置。");
            state.inputSettingsIdentity = UnityEngine.EntityId.ToULong(settings.GetEntityId()); state.inputSettingsSha256 = Hash(EditorJsonUtility.ToJson(settings));
            var flags = Read(settingsType, "m_FeatureFlags", settings) as HashSet<string>;
            if (flags != null && !ReferenceEquals(flags.Comparer, EqualityComparer<string>.Default)) throw new InvalidOperationException("InputBuild feature flags 含未知 comparer。");
            state.inputFeaturesSha256 = flags == null ? "null" : Hash(NovaFramework.Runtime.Util.Json.Serialize(flags.OrderBy(value => value, StringComparer.Ordinal).ToArray()));
            var actions = Read(managerType, "m_Actions", manager) as UnityEngine.Object;
            state.inputActionsIdentity = actions == null ? 0 : UnityEngine.EntityId.ToULong(actions.GetEntityId()); state.inputActionsPathSha256 = Hash(actions == null ? string.Empty : AssetDatabase.GetAssetPath(actions));
            var settingsRoster = Resources.FindObjectsOfTypeAll(settingsType);
            if (settingsRoster.Any(value => value.GetType() != settingsType)) throw new InvalidOperationException("InputBuild 存在未知派生设置，不序列化或调用其生命周期。");
            state.inputSettingsRosterSha256 = Hash(string.Join("|", settingsRoster.OrderBy(value => UnityEngine.EntityId.ToULong(value.GetEntityId())).Select(value => UnityEngine.EntityId.ToULong(value.GetEntityId()) + ":" + Hash(EditorJsonUtility.ToJson(value)) + ":" + EditorUtility.IsDirty(value))));
        }

        /// <summary>只调用准确引擎 global native 只读 getter，不实例化设置。</summary>
        /// <returns>真实 global 对象。</returns>
        private static UnityEngine.Object CurrentGlobal()
        {
            PropertyInfo field = typeof(GraphicsSettings).GetProperty("currentRenderPipelineGlobalSettings", Static);
            if (field?.DeclaringType != typeof(GraphicsSettings)) throw new InvalidOperationException("Global getter 身份改变。");
            return (UnityEngine.Object)field.GetValue(null);
        }

        /// <summary>只投影两标量 reset 与一次正常池借还；其余输入全部参与合同摘要，实际池变化另按前后拓扑验证。</summary>
        /// <param name="state">实际快照。</param>
        /// <returns>稳定语义输入摘要。</returns>
        internal static string InvariantHash(Snapshot state)
        {
            if (state == null) throw new InvalidOperationException("五项有限合同快照缺失。");
            Snapshot copy = NovaFramework.Runtime.Util.Json.Deserialize<Snapshot>(NovaFramework.Runtime.Util.Json.Serialize(state));
            if (copy.stripper) { copy.stripperGlobalDirty = false; copy.stripperGraphicsDirty = false; copy.fresh = null; copy.poolRows = null; copy.poolCountAll = 0; }
            return Hash(NovaFramework.Runtime.Util.Json.Serialize(copy));
        }

        /// <summary>写前完整实态一致，不能把后验投影用来跳过池或原标量变化。</summary>
        /// <param name="before">原计划。</param>
        /// <param name="current">写前当前值。</param>
        /// <returns>完整序列化值相同为 true。</returns>
        internal static bool BeforeEquals(Snapshot before, Snapshot current) => before != null && current != null && NovaFramework.Runtime.Util.Json.Serialize(before) == NovaFramework.Runtime.Util.Json.Serialize(current);

        /// <summary>真实链后必须两标量 false；池仅允许零次或一次官方借还，Verify 当前值完整等于原 after。</summary>
        /// <param name="before">原计划实态。</param>
        /// <param name="after">实际构建后实态。</param>
        /// <param name="current">当前只读实态。</param>
        /// <param name="error">漂移或缺失原因。</param>
        /// <returns>全部正常边界成立时为 true。</returns>
        internal static bool TryValidateAfter(Snapshot before, Snapshot after, Snapshot current, out string error)
        {
            error = null;
            if (before == null || after == null || current == null) error = "五项有限合同 before/after/current 缺失。";
            else if (InvariantHash(before) != InvariantHash(after) || !BeforeEquals(after, current)) error = "五项有限合同输入、对象或当前状态漂移。";
            else if (before.stripper && (after.stripperGlobalDirty || after.stripperGraphicsDirty)) error = "GlobalSettingsStripper 正常标量 reset 未完成。";
            else if (before.stripper && !TryValidatePoolAfter(before, after, out error)) { }
            return error == null;
        }

        /// <summary>按 ObjectPool.Get/Release 精确拓扑接受未执行或一次借还，不抹掉旧列表、容量或活跃借用。</summary>
        /// <param name="before">原池材料。</param>
        /// <param name="after">真实后池材料。</param>
        /// <param name="error">具体不符原因。</param>
        /// <returns>准确零次或一次拓扑变化为 true。</returns>
        internal static bool TryValidatePoolAfter(Snapshot before, Snapshot after, out string error)
        {
            error = null;
            if (before.poolRows == null || after.poolRows == null || before.poolRows.Any(row => row == null) || after.poolRows.Any(row => row == null)) { error = "GlobalSettingsStripper pool 列表材料缺失。"; return false; }
            if (RowsEqual(before.poolRows, after.poolRows) && RowEqual(before.fresh, after.fresh) && before.poolCountAll == after.poolCountAll) return true;
            PoolRow selected = before.fresh ?? before.poolRows.LastOrDefault();
            PoolRow[] remaining = before.fresh != null || before.poolRows.Length == 0 ? before.poolRows : before.poolRows.Take(before.poolRows.Length - 1).ToArray();
            bool selectedValid = selected == null ? after.fresh != null && after.fresh.capacity >= before.rosterCount && after.poolCountAll == before.poolCountAll + 1 :
                after.fresh != null && after.fresh.identity == selected.identity && after.fresh.capacity >= selected.capacity && after.fresh.capacity >= before.rosterCount && after.poolCountAll == before.poolCountAll;
            if (!selectedValid || !RowsEqual(remaining, after.poolRows)) error = "GlobalSettingsStripper pool 超出一次准确空列表借还，保留未知状态。";
            return error == null;
        }

        /// <summary>按原顺序比较剩余列表身份和容量。</summary>
        /// <param name="left">原行集。</param>
        /// <param name="right">当前行集。</param>
        /// <returns>逐项完整一致为 true。</returns>
        private static bool RowsEqual(PoolRow[] left, PoolRow[] right) => left.Length == right.Length && left.Zip(right, RowEqual).All(value => value);

        /// <summary>比较单行，包括 fresh 原空槽。</summary>
        /// <param name="left">原行。</param>
        /// <param name="right">当前行。</param>
        /// <returns>同为空或身份与容量相同为 true。</returns>
        private static bool RowEqual(PoolRow left, PoolRow right) => left == null ? right == null : right != null && left.identity == right.identity && left.capacity == right.capacity;

        /// <summary>绑定本轮真实报告及已冻结 usage 输入，不构造 analytic 或默认 InputSettings。</summary>
        /// <param name="before">实际计划材料。</param>
        /// <param name="report">正式 BuildReport。</param>
        /// <returns>明确本地 payload 范围。</returns>
        internal static ReportMaterial CaptureReport(Snapshot before, BuildReport report)
        {
            if (before == null || report == null || report.summary.platform != UnityEditor.BuildTarget.WebGL) throw new InvalidOperationException("五项有限合同真实 WebGL 报告缺失。");
            return new ReportMaterial { reportGuid = report.summary.guid.ToString(), platform = "WebGL", analyticsEnabled = before.analyticsEnabled, buildTarget = before.buildTarget, input = before.input, inputRuntimePresent = before.inputRuntimeIdentity != 0,
                activeQualityLevels = before.activeQualityLevels, totalQualityLevels = before.totalQualityLevels, inputSettingsSha256 = before.inputSettingsSha256, inputFeaturesSha256 = before.inputFeaturesSha256, inputActionsPathSha256 = before.inputActionsPathSha256 };
        }

        /// <summary>只读验证原报告范围；缺失字段保持 partial，不重发事件。</summary>
        /// <param name="before">原计划输入。</param>
        /// <param name="material">真实链保存的报告范围。</param>
        /// <param name="guid">正式报告 GUID。</param>
        /// <param name="error">具体不足。</param>
        /// <returns>报告和输入全部绑定时为 true。</returns>
        internal static bool TryValidateReport(Snapshot before, ReportMaterial material, string guid, out string error)
        {
            error = null;
            if (before == null || material == null || string.IsNullOrEmpty(guid) || material.reportGuid != guid || material.platform != "WebGL" || material.analyticsEnabled != before.analyticsEnabled || material.buildTarget != before.buildTarget || material.input != before.input || material.inputRuntimePresent != (before.inputRuntimeIdentity != 0) || material.activeQualityLevels != before.activeQualityLevels || material.totalQualityLevels != before.totalQualityLevels || material.inputSettingsSha256 != before.inputSettingsSha256 || material.inputFeaturesSha256 != before.inputFeaturesSha256 || material.inputActionsPathSha256 != before.inputActionsPathSha256) error = "五项有限合同报告与实际输入不一致。";
            return error == null;
        }

        /// <summary>只定位已自然加载的唯一实际类型；转发同一 Type 去重。</summary>
        /// <param name="name">准确全名。</param>
        /// <returns>唯一实际 Type。</returns>
        private static Type Find(string name) => AppDomain.CurrentDomain.GetAssemblies().Select(assembly => assembly.GetType(name, false)).Where(value => value != null).Distinct().Single();

        /// <summary>只读取准确声明字段，不使用业务 getter。</summary>
        /// <param name="type">准确声明类型。</param>
        /// <param name="name">已审字段名。</param>
        /// <param name="owner">既有实例或 static null。</param>
        /// <returns>真实字段值。</returns>
        private static object Read(Type type, string name, object owner)
        {
            FieldInfo field = type.GetField(name, owner == null ? Static : Instance);
            if (field?.DeclaringType != type) throw new InvalidOperationException("五项有限合同 backing 身份变化：" + type.FullName + "." + name);
            return field.GetValue(owner);
        }

        /// <summary>只保存原始设置的摘要，不输出设置正文。</summary>
        /// <param name="value">驻内存原文。</param>
        /// <returns>稳定 SHA-256。</returns>
        private static string Hash(string value)
        {
            using (var sha = System.Security.Cryptography.SHA256.Create()) return BitConverter.ToString(sha.ComputeHash(System.Text.Encoding.UTF8.GetBytes(value))).Replace("-", string.Empty).ToLowerInvariant();
        }
    }
}
