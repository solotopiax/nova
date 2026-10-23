/***************************************************************
 * (c) copyright 2026 - 2030, Solotopia
 * All Rights Reserved.
 * -------------------------------------------------------------
 * filename:  PlayerBuiltinUsageBuildContract.cs
 * author:    taoye
 * created:   2026/10/16
 * descrip:   三个 Unity 内置构建 usage 回调的准确输入、正常缓存与报告材料
 ***************************************************************/

using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Runtime.CompilerServices;
using UnityEditor;
using UnityEditor.Build.Reporting;
using UnityEngine;

namespace NovaFramework.Editor
{
    /// <summary>限定当前 CoreModule 的 Audio/UI/Insights，不发送事件、不修改设置、不推断远端送达。</summary>
    internal static class PlayerBuiltinUsageBuildContract
    {
        internal const string AudioMarker = "BuiltinUsage=AudioRandom";
        internal const string UiMarker = "BuiltinUsage=UI";
        internal const string InsightsMarker = "BuiltinUsage=Insights";
        private const string InsightsType = "UnityEditor.InsightsEditor.EditorAnalytics.InsightsEditorAnalytic";
        private const BindingFlags Static = BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Static;

        /// <summary>准确配置和自然缓存状态；命令行与云项目仅保留摘要。</summary>
        [Serializable]
        internal sealed class Snapshot
        {
            public bool audio;
            public bool ui;
            public bool insights;
            public bool analyticsEnabled;
            public bool audioDisabled;
            public bool diagnosticsEnabled;
            public string commandLineSha256;
            public string cloudProjectSha256;
            public bool commandCreateProject;
            public bool hasCheckedCreateProject;
            public bool isCreateProject;
            public string predicate;
            public int predicateOwner;
            public string debuggerSubscriber;
        }

        /// <summary>只复制报告中的准确本地 payload 输入；没有 native 返回值、ack 或发送成功字段。</summary>
        [Serializable]
        internal sealed class ReportMaterial
        {
            public string reportGuid;
            public string sessionGuid;
            public int buildType;
            public int packedGroups;
            public string packedRowsSha256;
            public bool audioAttemptExpected;
            public bool uiAttemptExpected;
            public bool insightsCallExpected;
            public int audioCount;
            public int visualTreeCount;
            public int styleSheetCount;
            public int themeStyleSheetCount;
            public int panelSettingsCount;
            public bool insightsCreateProject;
            public bool insightsDiagnostics;
            public string cloudProjectSha256;
        }

        /// <summary>可纯函数测试的报告记录；仅 GUID、准确类型分类参与计数。</summary>
        internal sealed class AssetRow
        {
            public string guid;
            public string kind;
            public bool audio;
        }

        /// <summary>捕获准确 backing 与配置，不调用任何 usage 发送入口或变更配置。</summary>
        /// <param name="inputs">只有三个准确 marker 会启用对应合同。</param>
        /// <returns>本域真实缓存和输入；inactive 时没有读取额外状态。</returns>
        internal static Snapshot Capture(IEnumerable<string> inputs)
        {
            var snapshot = new Snapshot { audio = inputs.Contains(AudioMarker), ui = inputs.Contains(UiMarker), insights = inputs.Contains(InsightsMarker) };
            if (!snapshot.audio && !snapshot.ui && !snapshot.insights) return snapshot;
            snapshot.analyticsEnabled = EditorAnalytics.enabled;
            snapshot.audioDisabled = snapshot.audio && (bool)ReadProperty(typeof(AudioSettings), "unityAudioDisabled", null);
            Type debugger = CoreType("UnityEditor.DebuggerEventListHandler");
            Delegate subscriber = Field(debugger, "analyticSent", null).GetValue(null) as Delegate;
            snapshot.debuggerSubscriber = DelegateIdentity(subscriber);
            if (subscriber != null) throw new InvalidOperationException("Unity usage 存在未审 analyticSent 下游委托；保留订阅且不执行构建。");
            if (snapshot.insights)
            {
                Type type = CoreType(InsightsType), cache = CoreType(InsightsType + "+<>c");
                snapshot.hasCheckedCreateProject = (bool)Field(type, "s_HasCheckedCreateProjectFlag", typeof(bool)).GetValue(null);
                snapshot.isCreateProject = (bool)Field(type, "s_IsCreateProjectFlagSet", typeof(bool)).GetValue(null);
                string[] args = Environment.GetCommandLineArgs();
                snapshot.commandLineSha256 = Hash(NovaFramework.Runtime.Util.Json.Serialize(args));
                snapshot.commandCreateProject = args.Any(argument => argument == "-createproject");
                snapshot.cloudProjectSha256 = Hash(CloudProjectSettings.projectId ?? string.Empty);
                snapshot.diagnosticsEnabled = (bool)ReadProperty(CoreType("UnityEditor.EngineDiagnostics.EngineDiagnosticsSettings"), "enabled", null);
                // 已审 <>c .cctor 只 new 无字段对象；只读 backing 可自然初始化此纯内存 owner，不调用 predicate。
                object owner = Field(cache, "<>9", cache).GetValue(null);
                Delegate predicate = Field(cache, "<>9__8_0", typeof(Predicate<string>)).GetValue(null) as Delegate;
                if (owner == null || owner.GetType() != cache || predicate != null &&
                    (predicate.GetInvocationList().Length != 1 || !ReferenceEquals(predicate.Target, owner) || predicate.Method.DeclaringType != cache || predicate.Method.Name != "<PopulateCommonData>b__8_0"))
                    throw new InvalidOperationException("Insights 自然命令行缓存委托身份未知；不调用或替换。");
                snapshot.predicateOwner = RuntimeHelpers.GetHashCode(owner);
                snapshot.predicate = DelegateIdentity(predicate);
            }
            return snapshot;
        }

        /// <summary>把官方唯一正常缓存变化投影为固定后验；其余配置和 owner 身份保留。</summary>
        /// <param name="snapshot">原始实际材料。</param>
        /// <returns>不会修改原材料的不变量摘要。</returns>
        internal static string InvariantHash(Snapshot snapshot)
        {
            if (snapshot == null) throw new InvalidOperationException("Unity usage 状态材料缺失。");
            Snapshot clone = NovaFramework.Runtime.Util.Json.Deserialize<Snapshot>(NovaFramework.Runtime.Util.Json.Serialize(snapshot));
            if (clone.insights && !clone.hasCheckedCreateProject)
            {
                clone.hasCheckedCreateProject = true;
                clone.isCreateProject = clone.commandCreateProject;
                clone.predicate = ExpectedPredicateIdentity();
            }
            return Hash(NovaFramework.Runtime.Util.Json.Serialize(clone));
        }

        /// <summary>正式构建前比较完整真实值，不能通过正常缓存投影绕过新输入。</summary>
        /// <param name="before">Plan 原值。</param>
        /// <param name="current">立即执行前当前值。</param>
        /// <returns>实际值完整一致时为 true。</returns>
        internal static bool BeforeEquals(Snapshot before, Snapshot current) => before != null && current != null && NovaFramework.Runtime.Util.Json.Serialize(before) == NovaFramework.Runtime.Util.Json.Serialize(current);

        /// <summary>验证实际缓存后验和配置保持；不重新发送或初始化本应由回调设置的标志。</summary>
        /// <param name="before">Plan 或回执实际原值。</param>
        /// <param name="after">构建链返回后的实际值。</param>
        /// <param name="current">当前只读值。</param>
        /// <param name="error">具体材料不足或漂移原因。</param>
        /// <returns>唯一允许的自然变化及其他不变量满足时返回 true。</returns>
        internal static bool TryValidateAfter(Snapshot before, Snapshot after, Snapshot current, out string error)
        {
            error = null;
            if (before == null || after == null || current == null) error = "Unity usage before/after/current 材料缺失。";
            else if (InvariantHash(before) != InvariantHash(after) || InvariantHash(after) != InvariantHash(current) || !BeforeEquals(after, current)) error = "Unity usage 配置、缓存或委托状态漂移。";
            else if (before.insights && (!after.hasCheckedCreateProject || !current.hasCheckedCreateProject || !before.hasCheckedCreateProject &&
                (after.isCreateProject != before.commandCreateProject || after.predicate != ExpectedPredicateIdentity()))) error = "Insights 官方正常缓存后验未出现；不手动补值。";
            return error == null;
        }

        /// <summary>从本轮实际 BuildReport 复制固定事件需要的输入，不调用 Analytic 或原生发送函数。</summary>
        /// <param name="state">准确构建前配置。</param>
        /// <param name="report">官方本轮报告。</param>
        /// <returns>真实报告输入与按已审代码推导的发送条件，不能视作送达凭证。</returns>
        internal static ReportMaterial CaptureReport(Snapshot state, BuildReport report)
        {
            if (state == null || report == null) throw new InvalidOperationException("Unity usage 真实报告或 before 缺失。");
            if (!state.audio && !state.ui && !state.insights) return null;
            var rows = new List<AssetRow>();
            Type audioType = typeof(AudioSettings).Assembly.GetType("UnityEngine.Audio.AudioRandomContainer", true);
            MethodInfo contents = typeof(PackedAssets).GetMethod("GetContents", BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance, null, Type.EmptyTypes, null);
            if (contents?.DeclaringType != typeof(PackedAssets) || contents.ReturnType != typeof(PackedAssetInfo[])) throw new InvalidOperationException("PackedAssets 只读内容 API 身份变化。");
            foreach (PackedAssets group in report.packedAssets)
                foreach (PackedAssetInfo info in (PackedAssetInfo[])contents.Invoke(group, null))
                {
                    string kind = null;
                    if (info.type == typeof(MonoBehaviour) && !info.sourceAssetGUID.Empty())
                    {
                        Type type = AssetDatabase.GetMainAssetTypeFromGUID(info.sourceAssetGUID);
                        if (type == typeof(UnityEngine.UIElements.VisualTreeAsset)) kind = "VisualTreeAsset";
                        else if (type == typeof(UnityEngine.UIElements.StyleSheet)) kind = "StyleSheet";
                        else if (type == typeof(UnityEngine.UIElements.ThemeStyleSheet)) kind = "ThemeStyleSheet";
                        else if (type == typeof(UnityEngine.UIElements.PanelSettings)) kind = "PanelSettings";
                    }
                    rows.Add(new AssetRow { guid = info.sourceAssetGUID.ToString(), kind = kind, audio = info.type == audioType });
                }
            ReportMaterial material = CountRows(rows);
            material.reportGuid = report.summary.guid.ToString();
            material.sessionGuid = ReadProperty(typeof(EditorApplication), "buildSessionGUID", null).ToString();
            material.buildType = Convert.ToInt32(ReadProperty(typeof(BuildSummary), "buildType", report.summary));
            material.packedGroups = report.packedAssets.Length;
            material.packedRowsSha256 = Hash(NovaFramework.Runtime.Util.Json.Serialize(rows));
            material.audioAttemptExpected = state.audio && state.analyticsEnabled && !state.audioDisabled && material.packedGroups != 0;
            material.uiAttemptExpected = state.ui && state.analyticsEnabled && material.packedGroups != 0;
            material.insightsCallExpected = state.insights;
            material.insightsCreateProject = state.hasCheckedCreateProject ? state.isCreateProject : state.commandCreateProject;
            material.insightsDiagnostics = state.diagnosticsEnabled;
            material.cloudProjectSha256 = state.cloudProjectSha256;
            return material;
        }

        /// <summary>核对报告材料与官方报告 GUID 和原配置，旧回执缺失时保持 partial；不重发事件。</summary>
        /// <param name="state">原配置。</param>
        /// <param name="material">构建链当时复制的 payload 输入。</param>
        /// <param name="reportGuid">Player 正式回执中的官方 GUID。</param>
        /// <param name="error">准确缺失或不一致原因。</param>
        /// <returns>本地报告范围与条件一致时返回 true，不表示送达。</returns>
        internal static bool TryValidateReport(Snapshot state, ReportMaterial material, string reportGuid, out string error)
        {
            error = null;
            if (state == null) error = "Unity usage 原状态缺失。";
            else if (!state.audio && !state.ui && !state.insights) { if (material != null) error = "inactive usage 含不属于本轮的报告材料。"; }
            else if (material == null || string.IsNullOrEmpty(reportGuid) || material.reportGuid != reportGuid || string.IsNullOrEmpty(material.sessionGuid) || material.packedRowsSha256?.Length != 64 || material.packedGroups < 0 ||
                material.audioCount < 0 || material.visualTreeCount < 0 || material.styleSheetCount < 0 || material.themeStyleSheetCount < 0 || material.panelSettingsCount < 0 ||
                material.packedGroups == 0 && (material.audioCount != 0 || material.visualTreeCount != 0 || material.styleSheetCount != 0 || material.themeStyleSheetCount != 0 || material.panelSettingsCount != 0) ||
                material.audioAttemptExpected != (state.audio && state.analyticsEnabled && !state.audioDisabled && material.packedGroups != 0) ||
                material.uiAttemptExpected != (state.ui && state.analyticsEnabled && material.packedGroups != 0) || material.insightsCallExpected != state.insights ||
                material.insightsCreateProject != (state.hasCheckedCreateProject ? state.isCreateProject : state.commandCreateProject) ||
                material.insightsDiagnostics != state.diagnosticsEnabled || material.cloudProjectSha256 != state.cloudProjectSha256)
                error = "Unity usage 本轮报告 payload 范围/条件材料缺失或不一致。";
            return error == null;
        }

        /// <summary>严格复现 UI 仅 VisualTree 按 GUID 去重、其他类型逐条计数及 Audio 逐条计数。</summary>
        /// <param name="rows">已分类的真实报告记录或纯测试 DTO。</param>
        /// <returns>没有远端状态的确定性计数。</returns>
        internal static ReportMaterial CountRows(IEnumerable<AssetRow> rows)
        {
            var result = new ReportMaterial();
            var trees = new HashSet<string>(StringComparer.Ordinal);
            foreach (AssetRow row in rows)
            {
                if (row.audio) result.audioCount++;
                switch (row.kind)
                {
                    case "VisualTreeAsset": if (trees.Add(row.guid)) result.visualTreeCount++; break;
                    case "StyleSheet": result.styleSheetCount++; break;
                    case "ThemeStyleSheet": result.themeStyleSheetCount++; break;
                    case "PanelSettings": result.panelSettingsCount++; break;
                }
            }
            return result;
        }

        /// <summary>只接受 CoreModule 中的准确内部类型，不加载其他程序集。</summary>
        /// <param name="name">已审完整类型名。</param>
        /// <returns>准确已加载类型。</returns>
        private static Type CoreType(string name) => typeof(EditorAnalytics).Assembly.GetType(name, true);

        /// <summary>读取准确 static backing 身份，未知形状直接拒绝。</summary>
        /// <param name="owner">已审声明类型。</param>
        /// <param name="name">准确字段名。</param>
        /// <param name="expected">需要匹配的类型，null 表示由委托形状检查。</param>
        /// <returns>只读反射字段。</returns>
        private static FieldInfo Field(Type owner, string name, Type expected)
        {
            FieldInfo field = owner.GetField(name, Static);
            if (field?.DeclaringType != owner || expected != null && field.FieldType != expected) throw new InvalidOperationException("Unity usage backing 身份变化：" + owner.FullName + "." + name);
            return field;
        }

        /// <summary>仅读取已审 native/报告 getter；不调用 private callback、发送函数或 setter。</summary>
        /// <param name="owner">准确声明类型。</param>
        /// <param name="name">本合同固定 getter 名。</param>
        /// <param name="instance">报告值或 static null。</param>
        /// <returns>已审 getter 的真实返回值。</returns>
        private static object ReadProperty(Type owner, string name, object instance)
        {
            PropertyInfo property = owner.GetProperty(name, BindingFlags.Public | BindingFlags.NonPublic | (instance == null ? BindingFlags.Static : BindingFlags.Instance));
            if (property?.DeclaringType != owner || property.GetIndexParameters().Length != 0 || property.GetGetMethod(true) == null) throw new InvalidOperationException("Unity usage 只读 getter 身份变化：" + owner.FullName + "." + name);
            return property.GetValue(instance);
        }

        /// <summary>唯一已审 predicate 签名摘要，不调用 predicate 或改变缓存。</summary>
        /// <returns>正常缓存方法的稳定身份。</returns>
        private static string ExpectedPredicateIdentity() => InsightsType + "+<>c|<PopulateCommonData>b__8_0";

        /// <summary>只有准确方法签名进入材料；完整 owner 身份另行保存，不输出参数值。</summary>
        /// <param name="value">真实 delegate 或 null。</param>
        /// <returns>可比对签名列表。</returns>
        private static string DelegateIdentity(Delegate value) => value == null ? "null" : string.Join(";", value.GetInvocationList().Select(entry => entry.Method.DeclaringType.FullName + "|" + entry.Method.Name));

        /// <summary>复用既有摘要实现，不在材料保存敏感输入原文。</summary>
        /// <param name="value">驻内存的输入。</param>
        /// <returns>SHA256 摘要。</returns>
        private static string Hash(string value) => GenerateActionCommon.ComputeTextHash(value);
    }
}
