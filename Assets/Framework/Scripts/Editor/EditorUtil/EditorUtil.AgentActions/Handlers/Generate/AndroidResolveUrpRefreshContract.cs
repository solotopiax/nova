/***************************************************************
 * (c) copyright 2026 - 2030, Solotopia
 * All Rights Reserved.
 * -------------------------------------------------------------
 * filename:  AndroidResolveUrpRefreshContract.cs
 * author:    taoye
 * created:   2026/11/16
 * descrip:   Android 模板域中已审 URP 的全局配置与材质自然支路合同
 ***************************************************************/
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Threading;
using UnityEditor;
using UnityEditor.Rendering;
using UnityEngine;
using UnityEngine.Rendering;

namespace NovaFramework.Editor
{
    /// <summary>只核已有 URP 对象及已审引擎原生只读映射，不调用 pipelineType、Ensure、CreateOrLoad 或重建入口。</summary>
    internal static class AndroidResolveUrpRefreshContract
    {
        private const string PipelineTypeName = "UnityEngine.Rendering.Universal.UniversalRenderPipeline";
        private const string AssetTypeName = "UnityEngine.Rendering.Universal.UniversalRenderPipelineAsset";

        /// <summary>完整受保护输入的摘要与当前已有对象；由外围快照转换成同域弱身份，不持久化 Unity 对象。</summary>
        internal sealed class State
        {
            internal RenderPipelineAsset Pipeline;
            internal UnityEngine.Object MaterialSettings;
            internal string Fingerprint, GlobalSettingsPath;
            internal bool GlobalSettingsRegistered;
        }

        /// <summary>按实际 6000.4 引擎条件判断是否会重建；原回调使用 InvariantCultureIgnoreCase 比较路径。</summary>
        internal static bool WouldRecreateGlobalSettings(string[] paths, bool registered, string settingsPath)
        {
            if (paths == null) return true;
            if (paths.Any(path => string.Equals(path, "ProjectSettings/GraphicsSettings.asset", StringComparison.InvariantCultureIgnoreCase))) return true;
            return registered && (string.IsNullOrEmpty(settingsPath) || paths.Any(path => string.Equals(path, settingsPath, StringComparison.InvariantCultureIgnoreCase)));
        }

        /// <summary>捕获当前精确 URP 来源、纯泛型 getter、原生全局配置映射和已完成的材质升级状态。</summary>
        /// <param name="token">本次只读遍历取消令牌。</param>
        /// <param name="state">成功时的已有对象与完整稳定输入；不创建缺失配置。</param>
        /// <param name="error">未审类型、待初始化或会有额外副作用时的具体阻断。</param>
        internal static bool TryCapture(CancellationToken token, out State state, out string error)
        {
            state = null;
            error = null;
            try
            {
                AndroidResolvePeripheralRefreshContracts.VerifyModule(typeof(GraphicsSettings), token);
                AndroidResolvePeripheralRefreshContracts.VerifyModule(typeof(AssetDatabase), token);
                AndroidResolvePeripheralRefreshContracts.VerifyModule(typeof(EditorGraphicsSettings), token);
                AndroidResolvePeripheralRefreshContracts.VerifyModule(typeof(QualitySettings), token);
                var rows = new List<string>
                {
                    ProjectFile("ProjectSettings/GraphicsSettings.asset", token), ProjectFile("ProjectSettings/QualitySettings.asset", token),
                    "quality=" + QualitySettings.GetQualityLevel()
                };
                var result = new State { Pipeline = GraphicsSettings.currentRenderPipeline };
                if (result.Pipeline == null)
                {
                    result.Fingerprint = GenerateActionCommon.ComputeTextHash(string.Join("\n", rows));
                    state = result; return true;
                }
                Type assetType = result.Pipeline.GetType();
                Require(assetType.FullName == AssetTypeName && assetType.Assembly.GetName().Name == "Unity.RenderPipelines.Universal.Runtime",
                    "当前RenderPipeline不是已审URP实现，模板刷新可能进入自定义pipelineType分支。");
                Require(AndroidRefreshSourceIdentityContract.TryVerifySourceAssembly(assetType, token, out string runtimeIdentity, out string runtimeError), runtimeError);
                Type pipelineType = assetType.Assembly.GetType(PipelineTypeName, false, false);
                Require(IsKnownPurePipelineGetter(assetType, pipelineType), "URP pipelineType不再是已审引擎泛型纯getter。");
                rows.Add("runtime=" + runtimeIdentity);
                string assetPath = AssetDatabase.GetAssetPath(result.Pipeline);
                Require(!string.IsNullOrEmpty(assetPath) && assetPath.StartsWith("Assets/", StringComparison.Ordinal), "URP管线资产不是既有项目资产。");
                rows.Add(ProjectFile(assetPath, token)); rows.Add(ProjectFile(assetPath + ".meta", token));

                // 已审 CoreModule 的查询直接返回原生 EntityId；避免 GetSettingsForRenderPipeline 物化资产对象和触发生命周期。
                MethodInfo query = typeof(EditorGraphicsSettings).GetMethod("Internal_GetSettingsEntityIdForRenderPipeline",
                    BindingFlags.Static | BindingFlags.NonPublic, null, new[] { typeof(string) }, null);
                Require(query != null && query.DeclaringType == typeof(EditorGraphicsSettings) && query.ReturnType == typeof(EntityId) && !query.IsGenericMethod,
                    "引擎全局配置只读EntityId查询签名已变化。");
                var entity = (EntityId)query.Invoke(null, new object[] { pipelineType.FullName });
                result.GlobalSettingsRegistered = entity != EntityId.None;
                result.GlobalSettingsPath = result.GlobalSettingsRegistered ? AssetDatabase.GetAssetPath(entity) : null;
                rows.Add("globalId=" + entity);
                if (result.GlobalSettingsRegistered)
                {
                    Require(!string.IsNullOrEmpty(result.GlobalSettingsPath) && result.GlobalSettingsPath.StartsWith("Assets/", StringComparison.Ordinal),
                        "URP已登记全局配置没有既有项目资产路径；刷新会重建或来源尚未适配。");
                    rows.Add(ProjectFile(result.GlobalSettingsPath, token)); rows.Add(ProjectFile(result.GlobalSettingsPath + ".meta", token));
                }
                result.MaterialSettings = CaptureMaterialReadiness(rows, token);
                result.Fingerprint = GenerateActionCommon.ComputeTextHash(string.Join("\n", rows));
                state = result; return true;
            }
            catch (OperationCanceledException) { throw; }
            catch (Exception exception)
            { error = exception is InvalidOperationException ? exception.Message : "URP自然支路只读核验失败：" + exception.GetType().Name; return false; }
        }

        /// <summary>从元数据证明虚拟调用目标正是固定 CoreModule 中 ldtoken T 的泛型 getter，不实际调用该属性。</summary>
        internal static bool IsKnownPurePipelineGetter(Type assetType, Type pipelineType)
        {
            if (assetType == null || pipelineType == null || assetType.BaseType == null || !assetType.BaseType.IsGenericType ||
                assetType.BaseType.GetGenericTypeDefinition() != typeof(RenderPipelineAsset<>) ||
                assetType.BaseType.GetGenericArguments()[0] != pipelineType || !typeof(RenderPipeline).IsAssignableFrom(pipelineType)) return false;
            MethodInfo getter = assetType.GetProperty("pipelineType", BindingFlags.Public | BindingFlags.Instance)?.GetGetMethod();
            return getter != null && getter.DeclaringType == assetType.BaseType && getter.Module == typeof(RenderPipelineAsset<>).Module &&
                getter.ReturnType == typeof(Type) && getter.GetParameters().Length == 0 && getter.IsVirtual && getter.IsFinal && !getter.IsStatic;
        }

        /// <summary>仅接受正常 Editor update 已完成首次检查、版本匹配且没有待保存材质的 backing 状态。</summary>
        private static UnityEngine.Object CaptureMaterialReadiness(ICollection<string> rows, CancellationToken token)
        {
            Type material = FindEditorOwner("UnityEditor.Rendering.Universal.MaterialPostprocessor", token);
            Type reimporter = FindEditorOwner("UnityEditor.Rendering.Universal.MaterialReimporter", token);
            Type settings = FindEditorOwner("UnityEditor.Rendering.Universal.UniversalProjectSettings", token);
            Require(ReadField(reimporter, "s_NeedToCheckProjSettingExistence", typeof(bool), null) is bool first && !first,
                "URP尚未自然完成首次材质检查；等待正常编辑器刷新后重新Plan，不调用升级入口。");
            Require(ReadField(material, "s_NeedsSavingAssets", typeof(bool), null) is bool pending && !pending,
                "URP仍有待保存材质；等待正常升级/保存完成后重新Plan。");
            object instance = ReadField(settings, "s_Instance", settings, null);
            Require(instance is UnityEngine.Object existing && existing != null && existing.GetType() == settings,
                "URP材质设置尚未由正常初始化创建；不调用instance或CreateOrLoad放行。");
            int version = (int)ReadField(settings, "m_LastMaterialVersion", typeof(int), instance);
            Array upgraders = ReadField(material, "k_Upgraders", null, null) as Array;
            Require(upgraders != null && upgraders.Rank == 1 && upgraders.Length == 10 && version == upgraders.Length,
                "URP材质升级版本未就绪或升级表已变化；模板解析不执行全量材质迁移。");
            for (int index = 0; index < upgraders.Length; index++)
            {
                Require(upgraders.GetValue(index) is Delegate callback && callback.Target == null && callback.GetInvocationList().Length == 1 &&
                    callback.Method.DeclaringType == material && callback.Method.Name == "UpgradeV" + (index + 1) && callback.Method.ReturnType == typeof(void),
                    "URP材质升级委托不是已审固定表。");
            }
            rows.Add("materialVersion=" + version); rows.Add("materialPending=false|firstCheck=false");
            rows.Add(ProjectFile("ProjectSettings/URPProjectSettings.asset", token));
            return (UnityEngine.Object)instance;
        }

        /// <summary>只读取唯一实际加载的URP Editor类型，复用完整来源合同，不加载可选程序集。</summary>
        private static Type FindEditorOwner(string name, CancellationToken token)
        {
            Type[] types = AppDomain.CurrentDomain.GetAssemblies().Select(assembly => assembly.GetType(name, false, false)).Where(type => type != null).ToArray();
            Require(types.Length == 1 && types[0].Assembly.GetName().Name == "Unity.RenderPipelines.Universal.Editor",
                "URP Editor owner缺失或存在同名冲突。");
            Require(AndroidRefreshSourceIdentityContract.TryVerifySourceAssembly(types[0], token, out _, out string error), error);
            return types[0];
        }

        /// <summary>精确读取已审字段及其类型，不调用属性、静态方法或配置加载入口。</summary>
        private static object ReadField(Type owner, string name, Type expected, object instance)
        {
            FieldInfo field = owner.GetField(name, BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.DeclaredOnly |
                (instance == null ? BindingFlags.Static : BindingFlags.Instance));
            Require(field != null && (expected == null || field.FieldType == expected), "URP backing字段身份已变化：" + name);
            return field.GetValue(instance);
        }

        /// <summary>冻结既有项目文件及精确路径，不通过加载资产补齐缺失来源。</summary>
        private static string ProjectFile(string path, CancellationToken token)
        {
            Require(GenerateActionCommon.TryResolveProjectPath(path, "URP稳定输入", out string full, out string error), error);
            Require(File.Exists(full), "URP稳定输入文件缺失：" + path);
            return path + "|" + GenerateActionCommon.ComputeFileHash(full, token);
        }

        /// <summary>未知状态直接阻断，不重建管线、修改设置或推进升级工作。</summary>
        private static void Require(bool condition, string error)
        { if (!condition) throw new InvalidOperationException(error ?? "URP刷新合同不完整。"); }
    }
}
