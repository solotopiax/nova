/***************************************************************
 * (c) copyright 2026 - 2030, Solotopia
 * All Rights Reserved.
 * -------------------------------------------------------------
 * filename:  AndroidResolveDefaultPackageCallbacks.cs
 * author:    taoye
 * created:   2026/11/16
 * descrip:   默认 AI、推理与版本控制包在 Android 模板域的自然支路合同
 ***************************************************************/
using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Threading;
using UnityEngine;

namespace NovaFramework.Editor
{
    /// <summary>只核已自然初始化的backing；不创建Store、配置单例、窗口或外部版本控制工作。</summary>
    internal static class AndroidResolveDefaultPackageCallbacks
    {
        /// <summary>稳定字段摘要与已有引用由父快照保存为同域弱身份，不持久化对象本身。</summary>
        internal sealed class State
        {
            internal string Digest;
            internal object[] References;
        }

        /// <summary>完整来源、正常派发完成见证及对应无写自然条件均成立时，捕获本次受保护状态。</summary>
        internal static bool TryCapture(int id, Type callback, CancellationToken token, out State state, out string error, bool independentAiStores = false, bool independentUvcs = false)
        {
            state = null; error = null;
            try
            {
                if (id == 69 && independentUvcs)
                    return AndroidResolveUvcsRefreshContract.TryCaptureCallback(callback, token, out state, out error);
                if (independentAiStores && AndroidResolveAiStoreContract.IsStoreId(id))
                {
                    Require(AndroidResolveAiStoreContract.TryCaptureStore(id, callback, token, out var store, out string storeError), storeError);
                    state = new State { Digest = store.sha256, References = Array.Empty<object>() };
                    return true;
                }
                string proof = "source-bound-safe-backing-v1";
                if (!independentAiStores)
                    Require(AndroidRefreshInitializationWitness.TryGetProof(callback, out proof, out string witnessError), witnessError);
                var rows = new List<string> { "witness=" + proof };
                var references = new List<object>();
                switch (id)
                {
                    case 58: CaptureEmptySet(callback.DeclaringType, "k_AnimateLoopComponents", rows, references, token, independentAiStores); break;
                    case 59: CaptureStore("Animate", rows, references, token); break;
                    case 60:
                        Type delegates = Owner("Unity.AI.Assistant.Editor.AssistantAssetModificationDelegates", token, rows);
                        Require(!independentAiStores || delegates.TypeInitializer == null, "AI删除委托owner新增cctor，不能安全读取冷态。");
                        object subscriptions = Field(delegates, "AssetDeletes", null);
                        Require(subscriptions == null, "AI Assistant有资产删除订阅者，当前模板删除支路尚未审核其UI/状态副作用。");
                        references.Add(subscriptions); rows.Add("assetDeletes=null"); break;
                    case 61: CaptureEmptySet(callback.DeclaringType, "k_Manipulators", rows, references, token, independentAiStores); break;
                    case 62: CaptureStore("Image", rows, references, token); break;
                    case 63: CaptureStore("Mesh", rows, references, token); break;
                    case 64: CaptureStore("Pbr", rows, references, token); break;
                    case 65: CaptureSearchDisabled(rows, references, token); break;
                    case 66: CaptureEmptySet(callback.DeclaringType, "k_SoundEnvelopeEditors", rows, references, token, independentAiStores); break;
                    case 67: CaptureStore("Sound", rows, references, token); break;
                    case 68:
                        Type window = Owner("Unity.InferenceEngine.Editor.Visualizer.Editor.ModelVisualizerWindow", token, rows);
                        AndroidResolvePeripheralRefreshContracts.VerifyModule(typeof(Resources), token);
                        Require(Resources.FindObjectsOfTypeAll(window).Length == 0, "Inference存在模型可视化窗口，资源刷新可能重建模型图；保留窗口并继续阻断。");
                        rows.Add("modelVisualizerWindows=0"); break;
                    case 69:
                        if (independentAiStores)
                        {
                            Require(AndroidRefreshInitializationWitness.TryGetProof(callback, out string uvcsProof, out string uvcsError),
                                "UVCS仍需真实四参派发完成证据；冷日志对象及配置订阅合同尚未闭合，不以五参空派发替代：" + uvcsError);
                            rows.Add("uvcs:four-parameter-proof=" + uvcsProof);
                        }
                        CapturePlasticDisabled(callback, rows, references);
                        break;
                    default: throw new InvalidOperationException("默认包回调没有独立自然合同。");
                }
                state = new State { Digest = GenerateActionCommon.ComputeTextHash(string.Join("\n", rows)), References = references.ToArray() };
                return true;
            }
            catch (OperationCanceledException) { throw; }
            catch (Exception exception)
            { error = exception is InvalidOperationException ? exception.Message : "默认包自然支路只读检查失败：" + exception.GetType().Name; return false; }
        }

        /// <summary>仅接受无活动组件的已初始化BCL HashSet，避免调用组件getter或刷新预览。</summary>
        private static void CaptureEmptySet(Type owner, string field, ICollection<string> rows, ICollection<object> references, CancellationToken token, bool requirePureInitializer)
        {
            VerifyOwner(owner, token, rows);
            if (requirePureInitializer) RequirePureSetInitializer(owner, field);
            object value = Field(owner, field, null);
            Type type = value?.GetType();
            Require(type != null && type.IsGenericType && type.GetGenericTypeDefinition() == typeof(HashSet<>) && type.GetGenericArguments()[0] == owner,
                "AI组件集合字段身份已变化。");
            Require(ConcreteCount(value, typeof(HashSet<>)) == 0, "AI组件或预览仍有活动实例，本次模板刷新可能重载其引用或UI；保留实例并继续阻断。");
            references.Add(value); rows.Add(owner.FullName + ":" + field + "=empty");
        }

        /// <summary>只放行源码已核且IL仅构造准确BCL HashSet并存入固定字段的初始化器，不扩成任意cctor白名单。</summary>
        private static void RequirePureSetInitializer(Type owner, string name)
        {
            if (owner.TypeInitializer == null) return;
            FieldInfo field = owner.GetField(name, BindingFlags.Static | BindingFlags.NonPublic | BindingFlags.Public | BindingFlags.DeclaredOnly);
            Require(field != null && field.FieldType.IsGenericType && field.FieldType.GetGenericTypeDefinition() == typeof(HashSet<>) &&
                field.FieldType.GetGenericArguments()[0] == owner, "AI集合初始化字段类型变化。");
            byte[] il = owner.TypeInitializer.GetMethodBody()?.GetILAsByteArray();
            int at = 0;
            while (il != null && at < il.Length && il[at] == 0) at++;
            Require(il != null && at + 11 <= il.Length && il[at] == 0x73 && il[at + 5] == 0x80 &&
                BitConverter.ToInt32(il, at + 6) == field.MetadataToken && il[at + 10] == 0x2a &&
                il.Skip(at + 11).All(value => value == 0), "AI集合cctor不再是单一BCL空集合初始化。");
            MethodBase constructor = owner.Module.ResolveMethod(BitConverter.ToInt32(il, at + 1), owner.GetGenericArguments(), null);
            Require(constructor is ConstructorInfo && constructor.DeclaringType == field.FieldType && constructor.GetParameters().Length == 0,
                "AI集合构造入口不是准确HashSet空构造。");
        }

        /// <summary>不调用SharedStore.Store；只接受已自然创建、链路准确、无派发队列且生成结果为空的实际Store。</summary>
        private static void CaptureStore(string family, ICollection<string> rows, ICollection<object> references, CancellationToken token)
        {
            string prefix = "Unity.AI." + family;
            Type shared = Owner(prefix + ".Services.SessionPersistence.SharedStore", token, rows);
            string storeName = family == "Pbr" ? "AIMaterialStore" : "AI" + family + "Store";
            Type expectedStore = Owner(prefix + ".Services.Stores." + storeName, token, rows);
            object store = Field(shared, "s_Store", null);
            Require(store != null && store.GetType() == expectedStore, "AI " + family + " Store尚未正常初始化；不调用Store getter制造可执行状态。");
            Type baseStore = Owner("Unity.AI.Generators.Redux.Store", token, rows);
            Require(expectedStore.BaseType == baseStore && Field(baseStore, "m_IsDispatching", store) is bool dispatching && !dispatching,
                "AI Store正在派发或继承身份已变化。");
            Type apiType = Owner("Unity.AI.Generators.Redux.StoreApi", token, rows);
            Type internalType = Owner("Unity.AI.Generators.Redux.StoreInternalApi", token, rows);
            object api = Field(baseStore, "m_StoreApi", store), inner = Field(baseStore, "m_InternalApi", store);
            Require(api?.GetType() == apiType && inner?.GetType() == internalType && ReferenceEquals(Field(apiType, "<api>k__BackingField", api), inner) &&
                ReferenceEquals(Field(internalType, "<store>k__BackingField", inner), store), "AI Store.State的真实调用链已被替换。");
            Type redux = Owner("Unity.AI.Generators.Redux.ReduxState", token, rows);
            object root = Field(baseStore, "m_State", store);
            Require(root?.GetType() == redux && redux.BaseType == typeof(Dictionary<string, object>), "AI State不是已审Redux字典。");
            var dictionary = (Dictionary<string, object>)root;
            bool hasSlice = dictionary.TryGetValue("generationResults", out object slice);
            Require(dictionary.Count <= 64 && hasSlice, "AI生成结果slice缺失或State容量未知。");
            Type expectedSlice = Owner(prefix + ".Services.Stores.States.GenerationResults", token, rows);
            Require(slice?.GetType() == expectedSlice, "AI生成结果slice的实际类型已变化。");
            object results = Field(expectedSlice, "generationResults", slice);
            Type serializable = Owner("Unity.AI.Toolkit.Utility.SerializableDictionary`2", token, rows);
            Type assetReference = Owner("Unity.AI.Toolkit.Asset.AssetReference", token, rows);
            Type resultType = results?.GetType();
            Require(resultType != null && resultType.IsGenericType && resultType.GetGenericTypeDefinition() == serializable && resultType.GetGenericArguments()[0] == assetReference &&
                resultType.BaseType.IsGenericType && resultType.BaseType.GetGenericTypeDefinition() == typeof(Dictionary<,>) && ConcreteCount(results, typeof(Dictionary<,>)) == 0,
                "AI已有生成结果或字典实现未知，模板导入可能派发状态更新/持久化；保留数据并继续阻断。");
            object queue = Field(baseStore, "m_DispatchQueue", store);
            Type queueType = Owner("Unity.AI.Generators.Redux.DispatchQueue", token, rows);
            Require(queue?.GetType() == queueType && Field(queueType, "isProcessing", queue) is bool processing && !processing,
                "AI Redux队列正在处理或实现变化。");
            object items = Field(queueType, "m_Queue", queue);
            Require(ConcreteCount(items, typeof(Queue<>)) == 0, "AI Redux仍有待派发工作；等待正常收尾，不清空或推进队列。");
            foreach (object value in new[] { store, api, inner, root, slice, results, queue, items }) references.Add(value);
            foreach (var pair in dictionary.OrderBy(pair => pair.Key, StringComparer.Ordinal))
            { rows.Add("slice=" + GenerateActionCommon.ComputeTextHash(pair.Key)); references.Add(pair.Value); }
            rows.Add("store:" + family + "=initialized-empty-results-idle-queue");
        }

        /// <summary>只读取已存在的ScriptableSingleton backing；搜索关闭时回调在排队/模型检测前立即返回。</summary>
        private static void CaptureSearchDisabled(ICollection<string> rows, ICollection<object> references, CancellationToken token)
        {
            Type settings = Owner("Unity.AI.Search.Editor.Knowledge.AssetKnowledgeSettings", token, rows);
            Type parent = settings.BaseType;
            Require(parent != null && parent.IsGenericType && parent.GetGenericTypeDefinition() == typeof(UnityEditor.ScriptableSingleton<>), "AI Search配置单例继承已变化。");
            AndroidResolvePeripheralRefreshContracts.VerifyModule(parent, token);
            object instance = Field(parent, "s_Instance", null);
            Require(instance is UnityEngine.Object existing && existing != null && instance.GetType() == settings,
                "AI Search配置尚未正常初始化；不调用instance/SearchUsable getter或创建配置。");
            Require(Field(settings, "searchEnabled", instance) is bool enabled && !enabled,
                "AI Search已启用，本次资源刷新会启动异步知识队列/依赖扫描；该额外写入尚未纳入模板合同。");
            references.Add(instance); rows.Add("searchEnabled=false");
        }

        /// <summary>仅接受已正常初始化且完整禁用的VCS早退；启用分支会消费保存队列及发起工作区操作。</summary>
        private static void CapturePlasticDisabled(Type callback, ICollection<string> rows, ICollection<object> references)
        {
            object enabled = Field(callback, "mIsEnabled", null), workspace = Field(callback, "mWkPath", null), processor = Field(callback, "mUVCSAssetsProcessor", null);
            Require(enabled is bool active && !active && workspace == null && processor == null,
                "UVCS回调已启用或存在工作区处理器；移动/签出/删除/添加及全局保存队列超出模板写集，不关闭插件或清空队列放行。");
            object repaint = Field(callback, "mIsRepaintNeededAfterAssetDatabaseRefresh", null), automaticAdd = Field(callback, "<IsAutomaticAddEnabled>k__BackingField", null);
            Require(repaint is bool && automaticAdd is bool, "UVCS禁用状态字段身份已变化。");
            rows.Add("uvcs:disabled|repaint=" + repaint + "|autoAdd=" + automaticAdd);
            references.Add(workspace); references.Add(processor); references.Add(Field(callback, "mLog", null));
        }

        /// <summary>只执行精确BCL集合的不可覆写Count getter；不调用vendor集合或未知接口实现。</summary>
        private static int ConcreteCount(object value, Type genericDefinition)
        {
            Type type = value?.GetType();
            while (type != null && (!type.IsGenericType || type.GetGenericTypeDefinition() != genericDefinition)) type = type.BaseType;
            PropertyInfo count = type?.GetProperty("Count", BindingFlags.Public | BindingFlags.Instance | BindingFlags.DeclaredOnly);
            Require(count?.PropertyType == typeof(int) && count.GetGetMethod() != null && (!count.GetGetMethod().IsVirtual || count.GetGetMethod().IsFinal),
                "集合没有已审BCL Count字段入口。");
            return (int)count.GetValue(value);
        }

        /// <summary>精确读取声明字段，不执行getter、构造或可选加载。</summary>
        private static object Field(Type type, string name, object instance)
        {
            FieldInfo field = type.GetField(name, BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.DeclaredOnly |
                (instance == null ? BindingFlags.Static : BindingFlags.Instance));
            Require(field != null, "默认包backing字段缺失：" + name); return field.GetValue(instance);
        }

        /// <summary>只匹配当前已加载的唯一类型，来源由完整已审编译输入核验，身份进入本次状态摘要。</summary>
        private static Type Owner(string name, CancellationToken token, ICollection<string> rows)
        {
            Type[] types = AppDomain.CurrentDomain.GetAssemblies().Select(assembly => assembly.GetType(name, false, false)).Where(type => type != null).ToArray();
            Require(types.Length == 1, "默认包owner缺失或同名冲突：" + name);
            VerifyOwner(types[0], token, rows); return types[0];
        }

        /// <summary>复用可移植源码合同，不把名称或当前DLL自签当作已审来源。</summary>
        private static void VerifyOwner(Type type, CancellationToken token, ICollection<string> rows)
        {
            Require(type != null, "默认包owner为空。");
            bool valid = AndroidRefreshSourceIdentityContract.TryVerifySourceAssembly(type, token, out string identity, out string error);
            Require(valid, error);
            rows.Add("source=" + identity);
        }

        /// <summary>不满足真实自然条件时停止，不修改用户数据、插件开关、队列或全局偏好。</summary>
        private static void Require(bool condition, string error)
        { if (!condition) throw new InvalidOperationException(error ?? "默认包自然合同不完整。"); }
    }
}
