/***************************************************************
 * (c) copyright 2026 - 2030, Solotopia
 * All Rights Reserved.
 * -------------------------------------------------------------
 * filename:  PlayerBuildEventsContract.cs
 * author:    taoye
 * created:   2026/10/16
 * descrip:   当前原生 WebGL BuildEvents 的准确自然分支及本地后验
 ***************************************************************/

using System;
using System.Collections;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Reflection;
using System.Runtime.CompilerServices;
using UnityEditor;
using UnityEditor.Build.Reporting;
using UnityEditor.PackageManager;
using UnityEngine;
using PackageInfo = UnityEditor.PackageManager.PackageInfo;

namespace NovaFramework.Editor
{
    /// <summary>只读既有模块 backing，不预调用构建回调、权限 getter、平台注册或派生 provider。</summary>
    internal static class PlayerBuildEventsContract
    {
        internal const string Marker = "BuiltinUsage=BuildEventsWebGLBase";
        private const BindingFlags Static = BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Static;
        private const BindingFlags Instance = BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance;

        /// <summary>冻结原模块与 SceneView roster，并独立保留官方三项正常统计缓存的真实值。</summary>
        [Serializable]
        internal sealed class Snapshot
        {
            public bool applicable;
            public bool analyticsEnabled;
            public string culture;
            public string activePlatformGuid;
            public int moduleIdentity;
            public int targetIdentity;
            public int derivedProviderIdentity;
            public int moduleMapIdentity;
            public int derivedMapIdentity;
            public string sceneRosterSha256;
            public int sceneCount;
            public int scene2dCount;
            public int defaultBehaviorMode;
            public bool eventSent;
            public int cachedSceneCount;
            public int cached2dCount;
            [NonSerialized, Newtonsoft.Json.JsonIgnore] public object moduleReference;
            [NonSerialized, Newtonsoft.Json.JsonIgnore] public object targetReference;
            [NonSerialized, Newtonsoft.Json.JsonIgnore] public object providerReference;
        }

        /// <summary>真实报告的库选择与当前精确包映射摘要，不含本地绝对路径或 native ack。</summary>
        [Serializable]
        internal sealed class ReportMaterial
        {
            public string reportGuid;
            public int managedLibraryCount;
            public string managedLibraryPathsSha256;
            public int packageCount;
            public string sanitizedPackageIdsSha256;
            public bool packageAttemptExpected;
            public bool sceneAttemptExpected;
            public int sceneCount;
            public int scene2dCount;
            public bool default2d;
        }

        /// <summary>准确读取既有自然注册模块和现存窗口，不通过 getter 初始化模块或派生对象。</summary>
        /// <param name="inputs">只在准确 marker 存在时启用。</param>
        /// <returns>本域实际对象和原缓存。</returns>
        internal static Snapshot Capture(IEnumerable<string> inputs)
        {
            if (!inputs.Contains(Marker)) return new Snapshot();
            if (EditorUserBuildSettings.activeBuildTarget != BuildTarget.WebGL) throw new InvalidOperationException("BuildEvents 只支持当前自然 WebGL 域。");
            Assembly core = typeof(BuildPipeline).Assembly;
            MethodInfo moduleName = core.GetType("UnityEditor.BuildTargetDiscovery", true).GetMethod("GetModuleNameForBuildTarget", Static, null, new[] { typeof(BuildTarget) }, null);
            if (moduleName?.ReturnType != typeof(string) || (string)moduleName.Invoke(null, new object[] { BuildTarget.WebGL }) != "WebGL") throw new InvalidOperationException("WebGL 原生模块名映射改变。");
            Type manager = core.GetType("UnityEditor.Modules.ModuleManager", true);
            var modules = ReadField(manager, "s_PlatformModules", null) as IDictionary;
            if (!(ReadField(manager, "s_PlatformModulesInitialized", null) is bool ready) || !ready || modules == null)
                throw new InvalidOperationException("WebGL 模块尚未自然注册/初始化；不调用平台注册或 getter 制造状态。");
            RequireDictionary(modules);
            if (!modules.Contains("WebGL")) throw new InvalidOperationException("WebGL 已注册模块不存在；不触发初始化。");
            object module = modules["WebGL"];
            Type moduleType = module?.GetType();
            if (moduleType?.FullName != "UnityEditor.WebGL.TargetExtension" || moduleType.Assembly.GetName().Name != "UnityEditor.WebGL.Extensions") throw new InvalidOperationException("WebGL 模块实例身份未知。");
            object target = ReadField(moduleType, "m_WebGlBuildTarget", module), provider = ReadField(moduleType, "m_DerivedBuildTargetExtensionsProvider", module);
            if (target?.GetType().FullName != "UnityEditor.WebGL.WebGLBuildTarget" || target.GetType().Assembly != moduleType.Assembly || provider?.GetType() != core.GetType("UnityEditor.Modules.DerivedBuildTargetExtensionsProvider", true)) throw new InvalidOperationException("WebGL 目标/provider backing 身份未知。");
            if (!(ReadField(provider.GetType(), "m_DerivedBuildTargetExtensions", provider) is IDictionary derived)) throw new InvalidOperationException("WebGL 派生模块 backing 缺失。");
            RequireDictionary(derived);
            if (derived.Count != 0 ||
                !Equals(ReadField(provider.GetType(), "m_BuildTarget", provider), BuildTarget.WebGL)) throw new InvalidOperationException("WebGL 存在实际派生 provider 或目标改变；不清空、不调用未知权限实现。");
            Type properties = moduleType.Assembly.GetType("UnityEditor.WebGL.WebGLPlatformProperties", true);
            if (properties.GetInterfaces().Any(value => value.FullName == "UnityEditor.IBuildPlatformProperties")) throw new InvalidOperationException("当前 WebGL 属性已实现权限接口，超出自然 null 合同。");
            var views = Resources.FindObjectsOfTypeAll<SceneView>().OrderBy(value => UnityEngine.EntityId.ToULong(value.GetEntityId())).ToArray();
            string roster = string.Join("\n", views.Select(value => UnityEngine.EntityId.ToULong(value.GetEntityId()) + "|" + value.in2DMode));
            Type callback = core.GetType("UnityEditor.BuildEventsHandlerPostProcess", true);
            return new Snapshot
            {
                applicable = true, analyticsEnabled = EditorAnalytics.enabled, culture = CultureInfo.CurrentCulture.Name,
                activePlatformGuid = typeof(EditorUserBuildSettings).GetProperty("activePlatformGuid", Static).GetValue(null).ToString(),
                moduleIdentity = RuntimeHelpers.GetHashCode(module), targetIdentity = RuntimeHelpers.GetHashCode(target), derivedProviderIdentity = RuntimeHelpers.GetHashCode(provider), moduleMapIdentity = RuntimeHelpers.GetHashCode(modules), derivedMapIdentity = RuntimeHelpers.GetHashCode(derived),
                moduleReference = module, targetReference = target, providerReference = provider, sceneRosterSha256 = Hash(roster), sceneCount = views.Length, scene2dCount = views.Count(value => value.in2DMode), defaultBehaviorMode = (int)EditorSettings.defaultBehaviorMode,
                eventSent = (bool)ReadField(callback, "s_EventSent", null), cachedSceneCount = (int)ReadField(callback, "s_NumOfSceneViews", null), cached2dCount = (int)ReadField(callback, "s_NumOf2dSceneViews", null),
            };
        }

        /// <summary>只投影官方实际 SceneView 统计缓存的明确正常后态，窗口/配置/模块均保持。</summary>
        /// <param name="state">真实状态。</param>
        /// <returns>独立副本的不变量摘要。</returns>
        internal static string InvariantHash(Snapshot state)
        {
            if (state == null) throw new InvalidOperationException("BuildEvents 状态材料缺失。");
            Snapshot clone = NovaFramework.Runtime.Util.Json.Deserialize<Snapshot>(NovaFramework.Runtime.Util.Json.Serialize(state));
            if (clone.applicable) { clone.eventSent = true; clone.cachedSceneCount = clone.sceneCount; clone.cached2dCount = clone.scene2dCount; }
            return Hash(NovaFramework.Runtime.Util.Json.Serialize(clone));
        }

        /// <summary>写前核对完整实际缓存及同域模块对象引用，不用投影略过新状态。</summary>
        /// <param name="before">Plan 原状态。</param>
        /// <param name="current">立即执行前状态。</param>
        /// <returns>实际值和引用都相等时为 true。</returns>
        internal static bool BeforeEquals(Snapshot before, Snapshot current) => before != null && current != null &&
            ReferenceEquals(before.moduleReference, current.moduleReference) && ReferenceEquals(before.targetReference, current.targetReference) && ReferenceEquals(before.providerReference, current.providerReference) &&
            NovaFramework.Runtime.Util.Json.Serialize(before) == NovaFramework.Runtime.Util.Json.Serialize(current);

        /// <summary>只接受自然统计已更新且原窗口/模块/配置仍相同；不手动 reset 或回滚官方计数。</summary>
        /// <param name="before">原状态。</param>
        /// <param name="after">真实链后读回。</param>
        /// <param name="current">当前读回。</param>
        /// <param name="error">具体不足原因。</param>
        /// <returns>实际正常后验满足时为 true。</returns>
        internal static bool TryValidateAfter(Snapshot before, Snapshot after, Snapshot current, out string error)
        {
            error = null;
            if (before == null || after == null || current == null) error = "BuildEvents before/after/current 材料缺失。";
            else if (InvariantHash(before) != InvariantHash(after) || InvariantHash(after) != InvariantHash(current) ||
                NovaFramework.Runtime.Util.Json.Serialize(after) != NovaFramework.Runtime.Util.Json.Serialize(current)) error = "BuildEvents 原窗口、模块或配置漂移。";
            else if (before.applicable && (!after.eventSent || after.cachedSceneCount != after.sceneCount || after.cached2dCount != after.scene2dCount)) error = "BuildEvents 官方 SceneView 正常后验未出现；不补值。";
            else if (before.moduleReference != null && (!ReferenceEquals(before.moduleReference, after.moduleReference) || !ReferenceEquals(before.targetReference, after.targetReference) || !ReferenceEquals(before.providerReference, after.providerReference))) error = "BuildEvents 原模块运行对象变化。";
            else if (after.moduleReference != null && (!ReferenceEquals(after.moduleReference, current.moduleReference) || !ReferenceEquals(after.targetReference, current.targetReference) || !ReferenceEquals(after.providerReference, current.providerReference))) error = "BuildEvents 当前模块运行对象变化。";
            return error == null;
        }

        /// <summary>仅读真实报告 role 并调用已审只读包映射，复制官方 payload 的摘要范围。</summary>
        /// <param name="before">原场景和配置。</param>
        /// <param name="report">官方本轮报告。</param>
        /// <returns>没有发送成功字段的真实输入材料。</returns>
        internal static ReportMaterial CaptureReport(Snapshot before, BuildReport report)
        {
            if (before == null || report == null) throw new InvalidOperationException("BuildEvents 状态或报告缺失。");
            if (!before.applicable) return null;
            List<string> libraries = report.GetFiles().Where(file => IsManagedLibraryRole(file.role)).Select(file => file.path).ToList();
            MethodInfo mapping = typeof(PackageInfo).GetMethod("GetForAssemblyFilePaths", Static, null, new[] { typeof(List<string>) }, null);
            if (mapping?.DeclaringType != typeof(PackageInfo) || mapping.ReturnType != typeof(List<PackageInfo>)) throw new InvalidOperationException("BuildEvents 已审只读包映射身份变化。");
            var packages = (List<PackageInfo>)mapping.Invoke(null, new object[] { libraries });
            string[] ids = packages.Select(package => SanitizePackageId(package.source, package.packageId, package.name)).ToArray();
            return new ReportMaterial
            {
                reportGuid = report.summary.guid.ToString(), managedLibraryCount = libraries.Count, managedLibraryPathsSha256 = Hash(NovaFramework.Runtime.Util.Json.Serialize(libraries)),
                packageCount = ids.Length, sanitizedPackageIdsSha256 = Hash(NovaFramework.Runtime.Util.Json.Serialize(ids)), packageAttemptExpected = ids.Length != 0,
                sceneAttemptExpected = !before.eventSent || before.cachedSceneCount != before.sceneCount || before.cached2dCount != before.scene2dCount,
                sceneCount = before.sceneCount, scene2dCount = before.scene2dCount, default2d = before.defaultBehaviorMode == 1,
            };
        }

        /// <summary>原代码按准确 role 选择，不按文件扩展名或大小写猜测。</summary>
        /// <param name="role">真实报告 role。</param>
        /// <returns>仅官方两种字面值为 true。</returns>
        internal static bool IsManagedLibraryRole(string role) => role == "ManagedLibrary" || role == "dll";

        /// <summary>复现原 Registry 保留包ID、其他来源仅名称与来源枚举的净化逻辑。</summary>
        /// <param name="source">准确包来源。</param>
        /// <param name="packageId">本机读取的原ID，不输出回执正文。</param>
        /// <param name="name">包名。</param>
        /// <returns>驻内存的官方净化值，调用方只保存整体 SHA。</returns>
        internal static string SanitizePackageId(PackageSource source, string packageId, string name) => source == PackageSource.Registry ? packageId : name + "@" + Enum.GetName(typeof(PackageSource), source).ToLower();

        /// <summary>校验真实报告材料的条件与冻结窗口数据，旧材料缺失仍 partial。</summary>
        /// <param name="before">原状态。</param>
        /// <param name="material">当时真实报告的复制材料。</param>
        /// <param name="guid">Player 正式回执 GUID。</param>
        /// <param name="error">具体错误。</param>
        /// <returns>本地条件一致时为 true，不能视作 native ack。</returns>
        internal static bool TryValidateReport(Snapshot before, ReportMaterial material, string guid, out string error)
        {
            error = null;
            if (before == null) error = "BuildEvents before 缺失。";
            else if (!before.applicable) { if (material != null) error = "inactive BuildEvents 含其他报告。"; }
            else if (material == null || string.IsNullOrEmpty(guid) || material.reportGuid != guid || material.managedLibraryCount < 0 || material.packageCount < 0 || material.packageCount > material.managedLibraryCount ||
                material.managedLibraryPathsSha256?.Length != 64 || material.sanitizedPackageIdsSha256?.Length != 64 || material.packageAttemptExpected != (material.packageCount != 0) ||
                material.sceneAttemptExpected != (!before.eventSent || before.cachedSceneCount != before.sceneCount || before.cached2dCount != before.scene2dCount) ||
                material.sceneCount != before.sceneCount || material.scene2dCount != before.scene2dCount || material.default2d != (before.defaultBehaviorMode == 1)) error = "BuildEvents 报告来源或准确 payload 条件不一致。";
            return error == null;
        }

        /// <summary>只读准确 backing；不调用模块/权限/派生 provider getter。</summary>
        /// <param name="type">准确声明类型。</param>
        /// <param name="name">已审字段名。</param>
        /// <param name="instance">现存对象或 static null。</param>
        /// <returns>真实字段值。</returns>
        private static object ReadField(Type type, string name, object instance)
        {
            FieldInfo field = type.GetField(name, instance == null ? Static : Instance);
            if (field?.DeclaringType != type) throw new InvalidOperationException("BuildEvents backing 身份变化：" + type.FullName + "." + name);
            return field.GetValue(instance);
        }

        /// <summary>仅枚举 BCL 准确 Dictionary，拒绝带未知接口方法的用户子类。</summary>
        /// <param name="value">真实 backing 字典。</param>
        private static void RequireDictionary(IDictionary value)
        {
            Type type = value.GetType();
            if (!type.IsGenericType || type.GetGenericTypeDefinition() != typeof(Dictionary<,>)) throw new InvalidOperationException("BuildEvents backing 不是准确 Dictionary。");
        }

        /// <summary>只保存输入摘要，不输出路径、项目ID或 native 诊断事件原文。</summary>
        /// <param name="value">驻内存的原输入。</param>
        /// <returns>SHA256。</returns>
        private static string Hash(string value) => GenerateActionCommon.ComputeTextHash(value);
    }
}
