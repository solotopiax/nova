/***************************************************************
 * (c) copyright 2026 - 2030, Solotopia
 * All Rights Reserved.
 * -------------------------------------------------------------
 * filename:  AndroidResolveAiStoreContract.cs
 * author:    taoye
 * created:   2026/11/16
 * descrip:   Android正常导入中五类AI Store缺省冷态的有限角色合同
 ***************************************************************/
using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Runtime.CompilerServices;
using System.Security.Cryptography;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading;
using NovaFramework.Runtime;
using UnityEditor;
using UnityEngine;
using Object = UnityEngine.Object;
using Path = System.IO.Path;
using PackageInfo = UnityEditor.PackageManager.PackageInfo;

namespace NovaFramework.Editor
{
    /// <summary>只读取无cctor的Store入口与既有单例backing；创建只可由正规导入中的厂商回调自然完成。</summary>
    internal static class AndroidResolveAiStoreContract
    {
        private const BindingFlags Fields = BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.Static | BindingFlags.DeclaredOnly;
        private static readonly string Domain = Guid.NewGuid().ToString("N");
        private static readonly ConditionalWeakTable<object, Identity> Identities = new ConditionalWeakTable<object, Identity>();
        private static readonly int[] Ids = { 59, 62, 63, 64, 67 };
        private static readonly string[] Families = { "Animate", "Image", "Mesh", "Pbr", "Sound" };
        private static readonly string[] Stores = { "AIAnimateStore", "AIImageStore", "AIMeshStore", "AIMaterialStore", "AISoundStore" };
        private static readonly string[] Settings = { "AnimateGeneratorSettings", "TextureGeneratorSettings", "MeshGeneratorSettings", "MaterialGeneratorSettings", "SoundGeneratorSettings" };
        private static readonly string[] SettingsGuids = { "8f2c39d77ee043afaec6cf75d69446f3", "0355ba069dae4d24bf2e318d15442628", "d769a8ac0a06d41e5a1948f2dc39d395", "f2009558327c478886451e9bd1d60f8b", "9b3b1723af7a4f699d968712f2d25b7c" };

        /// <summary>只分配本域弱身份，不调用厂商相等、getter或哈希。</summary>
        private sealed class Identity { internal readonly string value = Guid.NewGuid().ToString("N"); }

        /// <summary>真实单例backing直接持有的非资产对象；原完整对象行另由主合同保留。</summary>
        [Serializable]
        internal sealed class OwnedObject
        {
            public string role;
            public string id;
            public string type;
            public bool dirty;
            public int hideFlags;
        }

        /// <summary>单一已审Store的有限事实；仅冷态设置要求准确默认文本，已有文件的完整字节、长度和UTC写入时刻均受保护。</summary>
        [Serializable]
        internal sealed class StoreState
        {
            public int id;
            public string family;
            public string sourceSha256;
            public bool initialized;
            public string settingsPath;
            public string settingsFileSha256;
            public string settingsScriptGuid;
            public long settingsFileLength;
            public long settingsFileWriteUtcTicks;
            public string[] roleRows;
            public string[] dataRows;
            public string[] retainedRows = Array.Empty<string>();
            public OwnedObject[] ownedObjects;
            public string quittingHandler;
            public int listenerCount;
            public int middlewareCount;
            public string sha256;
        }

        /// <summary>新计划独立冻结五类Store及原退出订阅，不将其动态身份混入其它外围摘要。</summary>
        [Serializable]
        internal sealed class Snapshot
        {
            public int version = 1;
            public string domain;
            public StoreState[] stores;
            public string[] quittingHandlers;
            public string sha256;
        }

        /// <summary>固定五项编号不接受请求输入；其它回调不能借此移出外围保护。</summary>
        internal static bool IsStoreId(int id) => Array.IndexOf(Ids, id) >= 0;

        /// <summary>从当前自然已加载回调捕获完整角色集合；缺失模块不加载、不创建或预热。</summary>
        internal static bool TryCapture(CancellationToken token, out Snapshot snapshot, out string error)
        {
            snapshot = null;
            error = null;
            try
            {
                using var sourceRead = AndroidRefreshSourceIdentityContract.BeginReadScope();
                var states = new List<StoreState>();
                for (int i = 0; i < Ids.Length; i++)
                {
                    Type callback = Find("Unity.AI." + Families[i] + ".Services.Stores.Actions.AssetReferenceReimportMonitor", false);
                    if (callback == null) continue;
                    Require(AndroidResolveRefreshCallbacks.GetDispatchMethods(callback).Length != 0, "AI Store回调不再参与正常派发。");
                    states.Add(CaptureStore(Ids[i], callback, token));
                }
                snapshot = new Snapshot { domain = Domain, stores = states.ToArray(), quittingHandlers = CaptureQuittingHandlers(token) };
                snapshot.sha256 = ComputeHash(snapshot);
                Require(IsValid(snapshot), "AI Store角色快照结构不完整。");
                return true;
            }
            catch (OperationCanceledException) { throw; }
            catch (Exception exception)
            { snapshot = null; error = "AI Store缺省冷态合同未闭合：" + exception.Message; return false; }
        }

        /// <summary>供既有自然支路检查复用单项捕获，不调用Store getter或序列化厂商对象。</summary>
        internal static bool TryCaptureStore(int id, Type callback, CancellationToken token, out StoreState state, out string error)
        {
            state = null;
            error = null;
            try { using var sourceRead = AndroidRefreshSourceIdentityContract.BeginReadScope(); state = CaptureStore(id, callback, token); return true; }
            catch (OperationCanceledException) { throw; }
            catch (Exception exception) { error = "AI Store缺省状态未闭合：" + exception.Message; return false; }
        }

        /// <summary>验证新计划和同域完整事实，摘要不替代固定角色、空结果与设置缺失条件。</summary>
        internal static bool IsValid(Snapshot snapshot) => snapshot != null && snapshot.version == 1 && snapshot.domain == Domain &&
            snapshot.stores != null && snapshot.stores.Length <= Ids.Length && snapshot.quittingHandlers != null &&
            snapshot.quittingHandlers.Length <= 4096 && snapshot.stores.All(item => item != null) && snapshot.stores.Select(item => item.id).Distinct().Count() == snapshot.stores.Length &&
            snapshot.stores.Select(item => item.id).SequenceEqual(snapshot.stores.Select(item => item.id).OrderBy(value => value)) &&
            snapshot.stores.All(IsValidStore) && snapshot.stores.Where(item => item.quittingHandler != null)
                .All(item => snapshot.quittingHandlers.Count(value => value == item.quittingHandler) == 1) && snapshot.sha256 == ComputeHash(snapshot);

        /// <summary>写前全部相同；写后只有真实imported非空时才接纳缺省冷态创建，原已初始化实例不得替换。</summary>
        internal static bool TryValidateTransition(Snapshot before, Snapshot after, bool afterExecution, bool observedImportedAssets, out string error)
        {
            error = null;
            if (!IsValid(before) || !IsValid(after) || !before.stores.Select(item => item.id).SequenceEqual(after.stores.Select(item => item.id)))
            { error = "AI Store快照无效、跨域或参与者集合变化。"; return false; }
            if (!afterExecution)
            { if (before.sha256 != after.sha256) error = "AI Store写前完整状态发生变化。"; return error == null; }
            var additions = new List<string>();
            for (int i = 0; i < before.stores.Length; i++)
            {
                StoreState old = before.stores[i], current = after.stores[i];
                if (old.initialized || !observedImportedAssets)
                {
                    if (old.sha256 != current.sha256) { error = "AI Store已有状态变化，或没有真实导入却发生初始化。"; return false; }
                    continue;
                }
                if (!current.initialized || old.family != current.family || old.sourceSha256 != current.sourceSha256 ||
                    old.settingsPath != current.settingsPath || old.settingsFileSha256 != current.settingsFileSha256 ||
                    old.settingsScriptGuid != current.settingsScriptGuid || old.settingsFileLength != current.settingsFileLength ||
                    old.settingsFileWriteUtcTicks != current.settingsFileWriteUtcTicks)
                { error = "真实导入后的AI Store缺省创建尚未完成或输入漂移。"; return false; }
                foreach (OwnedObject owned in old.ownedObjects)
                {
                    OwnedObject retained = current.ownedObjects.SingleOrDefault(item => item.role == owned.role);
                    if (!SameOwned(owned, retained))
                    { error = "AI Store初始化替换了原单例角色或改变其脏状态。"; return false; }
                }
                if (!old.retainedRows.All(row => current.retainedRows.Contains(row, StringComparer.Ordinal)))
                { error = "AI Store初始化改变了已有单例、设置Session字段或持久映射身份。"; return false; }
                if (old.quittingHandler == null) additions.Add(current.quittingHandler);
                else if (old.quittingHandler != current.quittingHandler)
                { error = "AI Store初始化替换了原设置退出订阅。"; return false; }
            }
            if (after.quittingHandlers.Length != before.quittingHandlers.Length + additions.Count ||
                !after.quittingHandlers.Take(before.quittingHandlers.Length).SequenceEqual(before.quittingHandlers) ||
                !after.quittingHandlers.Skip(before.quittingHandlers.Length).OrderBy(value => value, StringComparer.Ordinal)
                    .SequenceEqual(additions.OrderBy(value => value, StringComparer.Ordinal)))
            { error = "Editor退出订阅增量不是本次新建设置实例的准确OnEditorQuitting集合。"; return false; }
            return true;
        }

        /// <summary>Verify必须绑定Execute实际后态，不能再次从Plan冷态继承创建或替换许可。</summary>
        internal static bool SameAfter(Snapshot receiptAfter, Snapshot current) => IsValid(receiptAfter) && IsValid(current) && receiptAfter.sha256 == current.sha256;

        /// <summary>只从比较副本移除已证冷态创建的准确新增行；任何旧行、重复、脏状态或无主对象仍受原保护。</summary>
        internal static bool TryGetComparableRows(Snapshot before, Snapshot after, string[] beforeRows, string[] afterRows,
            bool afterExecution, bool observedImportedAssets, out string[] comparableAfter, out string error)
        {
            comparableAfter = afterRows;
            if (!TryValidateTransition(before, after, afterExecution, observedImportedAssets, out error) || beforeRows == null || afterRows == null) return false;
            var additions = new HashSet<string>(StringComparer.Ordinal);
            for (int i = 0; i < before.stores.Length; i++)
            {
                if (before.stores[i].initialized || !after.stores[i].initialized) continue;
                foreach (OwnedObject owned in after.stores[i].ownedObjects)
                {
                    string row = ObjectRow(owned);
                    OwnedObject existing = before.stores[i].ownedObjects.SingleOrDefault(item => item.role == owned.role);
                    if (existing != null)
                    {
                        if (!SameOwned(existing, owned) || beforeRows.Count(value => value == row) != 1 || afterRows.Count(value => value == row) != 1)
                        { error = "AI Store原单例对象行丢失、重复或发生变化。"; return false; }
                        continue;
                    }
                    if (beforeRows.Any(value => value.StartsWith(owned.id + "|", StringComparison.Ordinal)) ||
                        afterRows.Count(value => value == row) != 1 || !additions.Add(row))
                    { error = "AI Store新增角色与原完整对象行不一致、重复或复用了旧对象。"; return false; }
                }
            }
            comparableAfter = afterRows.Where(row => !additions.Contains(row)).ToArray();
            return true;
        }

        /// <summary>计划只声明准确单例和内存订阅创建，不授予UserSettings文件写入。</summary>
        internal static string[] GetDeclaredEffects(Snapshot snapshot) => snapshot.stores.Where(item => !item.initialized)
            .Select(item => "Unity AI " + item.family + "：仅真实导入时自然建立缺省Store，复用已有ObjectPersistence和" + Settings[Array.IndexOf(Ids, item.id)] +
                "，仅缺失角色可创建及增加准确内存/退出订阅；原单例、脏状态和订阅须保留；" + item.settingsPath + (item.settingsFileSha256 == "absent" ? "须保持不存在" : "原文件字节、长度和写入时刻须保持不变") +
                "，不授予保存、网络或生成任务").ToArray();

        /// <summary>完整新快照摘要包含实际角色和原订阅顺序，不改变旧回执摘要。</summary>
        internal static string ComputeHash(Snapshot snapshot) => GenerateActionCommon.ComputeTextHash(Util.Json.Serialize(new
        { snapshot.version, snapshot.domain, snapshot.stores, snapshot.quittingHandlers }));

        /// <summary>完整单项摘要保留原始数据字段行、身份和Unity脏状态。</summary>
        internal static string ComputeStoreHash(StoreState state) => GenerateActionCommon.ComputeTextHash(Util.Json.Serialize(new
        { state.id, state.family, state.sourceSha256, state.initialized, state.settingsPath, state.settingsFileSha256,
            state.settingsScriptGuid, state.settingsFileLength, state.settingsFileWriteUtcTicks,
            state.roleRows, state.dataRows, state.retainedRows, state.ownedObjects, state.quittingHandler, state.listenerCount, state.middlewareCount }));

        /// <summary>按固定家族核实际来源、缺省持久边界、Store结构和全部已有数据字段；不物化任何getter。</summary>
        private static StoreState CaptureStore(int id, Type callback, CancellationToken token)
        {
            int index = Array.IndexOf(Ids, id);
            Require(index >= 0, "未知AI Store角色。");
            string prefix = "Unity.AI." + Families[index];
            var sources = new List<string>();
            Verify(callback, token, sources);
            Type shared = Owner(prefix + ".Services.SessionPersistence.SharedStore", token, sources);
            Type expected = Owner(prefix + ".Services.Stores." + Stores[index], token, sources);
            Type persistenceType = Owner(prefix + ".Services.SessionPersistence.ObjectPersistence", token, sources);
            Type settingsType = Owner(prefix + ".Services.SessionPersistence." + Settings[index], token, sources);
            Type baseStore = Owner("Unity.AI.Generators.Redux.Store", token, sources);
            Type queueType = Owner("Unity.AI.Generators.Redux.DispatchQueue", token, sources);
            Type apiType = Owner("Unity.AI.Generators.Redux.StoreApi", token, sources), innerType = Owner("Unity.AI.Generators.Redux.StoreInternalApi", token, sources);
            Type memory = Owner(prefix + ".Services.SessionPersistence.MemoryPersistence", token, sources);
            Owner("Unity.AI.ModelSelector.Services.Stores.Middleware.FilterCacheClearingMiddleware", token, sources);
            Owner("Unity.AI.Toolkit.Utility.SerializableDictionary`2", token, sources);
            Owner("Unity.AI.Generators.UI.PreviewSettings", token, sources);
            Require(shared.TypeInitializer == null, "SharedStore新增静态构造，不能安全读取冷态入口。");
            object store = Read(shared, "s_Store");
            object persistence = ExistingSingleton(persistenceType, token);
            object settings = ExistingSingleton(settingsType, token);
            string settingsPath = "UserSettings/AI." + Families[index] + "/" + Settings[index] + ".asset";
            CaptureSettingsFile(settingsType, index, settingsPath, store != null, token, sources, out string settingsSha, out long settingsLength, out long settingsWrite);
            var state = new StoreState { id = id, family = Families[index], sourceSha256 = SourceHash(sources),
                initialized = store != null, settingsPath = settingsPath, settingsFileSha256 = settingsSha, settingsScriptGuid = SettingsGuids[index],
                settingsFileLength = settingsLength, settingsFileWriteUtcTicks = settingsWrite, roleRows = Array.Empty<string>(),
                dataRows = Array.Empty<string>(), ownedObjects = Array.Empty<OwnedObject>() };
            var owned = new List<OwnedObject>();
            if (persistence != null) owned.Add(CaptureOwned("persistence", persistenceType, persistence));
            if (settings != null)
            {
                Require(Read(settingsType, "m_IsDirty", settings) is bool pendingSave && !pendingSave && Read(settingsType, "m_SaveTokenSource", settings) == null,
                    "AI设置已有待保存状态或保存任务，不取消或接管该工作。");
                owned.Add(CaptureOwned("settings", settingsType, settings));
                MethodInfo quitting = settingsType.GetMethod("OnEditorQuitting", BindingFlags.Instance | BindingFlags.NonPublic);
                Require(quitting != null && quitting.ReturnType == typeof(void) && quitting.GetParameters().Length == 0, "AI设置退出方法形状变化。");
                state.quittingHandler = MethodKey(quitting, settings);
                Require(CaptureQuittingHandlers(token).Count(value => value == state.quittingHandler) == 1, "AI设置没有准确唯一退出订阅。");
            }
            state.ownedObjects = owned.ToArray();
            state.retainedRows = CaptureRetainedRows(persistenceType, persistence, settingsType, settings, token);
            if (store == null)
            {
                if (settings != null) RequireDefaultSession(prefix, Read(settingsType, "m_Session", settings), token);
                if (persistence != null) RequireDefaultPersistence(prefix, Read(persistenceType, "data", persistence), token);
                var partial = new DataGraph(token);
                if (persistence != null) partial.Add("persistenceData", Read(persistenceType, "data", persistence));
                if (settings != null) partial.Add("settingsSession", Read(settingsType, "m_Session", settings));
                state.dataRows = partial.Rows.ToArray();
                state.roleRows = state.retainedRows.Length == 0 ? Array.Empty<string>() : new[] { "store=not-created" };
                state.sha256 = ComputeStoreHash(state);
                return state;
            }
            Require(store.GetType() == expected && persistence != null && settings != null, "AI Store或已有单例身份不完整。");
            Require(expected.BaseType == baseStore && Read(baseStore, "m_IsDispatching", store) is bool dispatching && !dispatching, "AI Store正在派发或继承不符。");
            Require(Read(settingsType, "m_IsDirty", settings) is bool dirty && !dirty && Read(settingsType, "m_SaveTokenSource", settings) == null,
                "AI设置已有待保存状态或保存任务，不取消或接管该工作。");
            object root = Read(baseStore, "m_State", store);
            Type redux = Owner("Unity.AI.Generators.Redux.ReduxState", token, sources);
            Require(root?.GetType() == redux && redux.BaseType == typeof(Dictionary<string, object>), "AI Redux根状态实现变化。");
            var dictionary = (Dictionary<string, object>)root;
            string[] keys = new[] { "sessions", "generationSettings", "generationResults", "modelSelector" }
                .Concat(Families[index] == "Image" ? new[] { "doodle" } : Array.Empty<string>()).OrderBy(value => value, StringComparer.Ordinal).ToArray();
            Require(dictionary.Keys.OrderBy(value => value, StringComparer.Ordinal).SequenceEqual(keys), "AI Store含API或未知Slice，不属于缺省初始化。");
            Type resultsType = Owner(prefix + ".Services.Stores.States.GenerationResults", token, sources);
            Require(dictionary["generationResults"]?.GetType() == resultsType, "AI生成结果Slice实际类型变化。");
            object results = Read(resultsType, "generationResults", dictionary["generationResults"]);
            Require(CollectionCount(results, typeof(Dictionary<,>)) == 0, "AI Store已有生成结果，不纳入缺省冷态合同。");
            object queue = Read(baseStore, "m_DispatchQueue", store);
            Require(queue?.GetType() == queueType && Read(queueType, "isProcessing", queue) is bool processing && !processing &&
                CollectionCount(Read(queueType, "m_Queue", queue), typeof(Queue<>)) == 0, "AI Redux仍有正常排队工作。");
            object api = Read(baseStore, "m_StoreApi", store), inner = Read(baseStore, "m_InternalApi", store);
            Require(api?.GetType() == apiType && inner?.GetType() == innerType && ReferenceEquals(Read(apiType, "<api>k__BackingField", api), inner) &&
                ReferenceEquals(Read(innerType, "<store>k__BackingField", inner), store), "AI Store调用链身份变化。");
            int count = Families[index] == "Image" ? 3 : 2;
            var listeners = Read(baseStore, "m_Listeners", store) as IList;
            Delegate dispose = Read(baseStore, "OnDispose", store) as Delegate;
            Type stateInterface = Owner("Unity.AI.Generators.Redux.IState", token, sources);
            Require(listeners != null && listeners.GetType() == typeof(List<>).MakeGenericType(typeof(Action<>).MakeGenericType(stateInterface)), "AI Store监听结构未知。");
            Require(listeners.Count == count && dispose != null && dispose.GetInvocationList().Length == count && Read(baseStore, "OnAction", store) == null &&
                CollectionCount(Read(baseStore, "m_ListenerWrappers", store), typeof(Dictionary<,>)) == 0, "AI Store含未知订阅或动作事件。");
            for (int i = 0; i < count; i++)
            {
                Require(listeners[i] is Delegate listener && DeclaringRoot(listener.Method.DeclaringType) == memory &&
                    listener.Method.Name.Contains("<Persist>") && ReferenceEquals(listener.Target, dispose.GetInvocationList()[i].Target) &&
                    DeclaringRoot(dispose.GetInvocationList()[i].Method.DeclaringType) == memory, "AI Store监听不是准确的MemoryPersistence初始化闭包。");
            }
            object middlewares = Read(apiType, "middlewares", api);
            int middlewareCount = Families[index] == "Mesh" ? 2 : 3;
            Require(CollectionCount(middlewares, typeof(Stack<>)) == middlewareCount, "AI Store含未知middleware数量。");
            foreach (object value in (IEnumerable)middlewares)
            {
                Require(value is Delegate handler, "AI middleware不是委托。");
                Type owner = DeclaringRoot(((Delegate)value).Method.DeclaringType);
                Require(owner == shared || owner.FullName == "Unity.AI.Generators.Redux.Thunks.ThunkMiddleware" ||
                    owner.FullName == "Unity.AI.ModelSelector.Services.Stores.Middleware.FilterCacheClearingMiddleware", "AI Store含未知middleware消费者。");
                Verify(owner, token, sources);
            }
            object data = Read(persistenceType, "data", persistence);
            var dataMap = data as IDictionary;
            Require(dataMap != null && dataMap.Keys.Cast<object>().All(key => key is string) &&
                dataMap.Keys.Cast<string>().OrderBy(value => value, StringComparer.Ordinal).SequenceEqual(
                    (Families[index] == "Image" ? new[] { "init", "modelSelector/init" } : new[] { "init" }).OrderBy(value => value, StringComparer.Ordinal)),
                "AI持久对象含恢复数据或未知键，不属于缺省初始化。");
            var roles = new List<string>();
            foreach (var pair in new[] { new KeyValuePair<string, object>("store", store), new KeyValuePair<string, object>("root", root),
                new KeyValuePair<string, object>("api", api), new KeyValuePair<string, object>("inner", inner), new KeyValuePair<string, object>("queue", queue),
                new KeyValuePair<string, object>("listeners", listeners), new KeyValuePair<string, object>("middlewares", middlewares) })
                roles.Add(pair.Key + "=" + Key(pair.Value));
            foreach (var pair in dictionary.OrderBy(pair => pair.Key, StringComparer.Ordinal)) roles.Add("slice:" + pair.Key + "=" + Key(pair.Value));
            foreach (Delegate listener in listeners) roles.Add("listener=" + DelegateKey(listener));
            roles.Add("dispose=" + string.Join(";", dispose.GetInvocationList().Select(DelegateKey)));
            state.roleRows = roles.ToArray();
            var graph = new DataGraph(token);
            graph.Add("state", root);
            graph.Add("settingsSession", Read(settingsType, "m_Session", settings));
            graph.Add("persistenceData", data);
            state.dataRows = graph.Rows.ToArray();
            state.listenerCount = count;
            state.middlewareCount = middlewareCount;
            state.sourceSha256 = SourceHash(sources);
            state.sha256 = ComputeStoreHash(state);
            return state;
        }

        /// <summary>原单例及设置Session必须复用；持久化AppData和Slice可按已审reducer复制，不把其旧包装身份误当新增Unity对象。</summary>
        private static string[] CaptureRetainedRows(Type persistenceType, object persistence, Type settingsType, object settings, CancellationToken token)
        {
            var rows = new List<string>();
            if (persistence != null)
            {
                rows.Add("persistence=" + Key(persistence));
                rows.Add("persistenceData=" + Key(Read(persistenceType, "data", persistence)));
            }
            if (settings != null)
            {
                rows.Add("settings=" + Key(settings));
                var graph = new DataGraph(token);
                graph.Add("settingsSession", Read(settingsType, "m_Session", settings));
                rows.AddRange(graph.Rows);
            }
            return rows.ToArray();
        }

        /// <summary>只接纳源码默认Session，空模型选择和预览缺省值均直接读字段，不构造厂商默认实例。</summary>
        private static void RequireDefaultSession(string prefix, object session, CancellationToken token)
        {
            RequireRecord(session, prefix + ".Services.Stores.States.Session", token, "settings");
            object settings = Read(session.GetType(), "settings", session);
            bool sound = prefix == "Unity.AI.Sound", pbr = prefix == "Unity.AI.Pbr";
            string[] names = sound ? new[] { "lastSelectedModelID", "previewSettings", "microphoneSettings" } :
                pbr ? new[] { "lastSelectedModels", "lastMaterialMappings", "previewSettings" } : new[] { "lastSelectedModels", "previewSettings" };
            RequireRecord(settings, prefix + ".Services.Stores.States.Settings", token, names);
            if (sound)
            {
                Require(string.IsNullOrEmpty(Read(settings.GetType(), "lastSelectedModelID", settings) as string), "AI部分冷态已有选定模型。");
                object microphone = Read(settings.GetType(), "microphoneSettings", settings);
                RequireRecord(microphone, prefix + ".Services.Stores.States.MicrophoneSettings", token, "microphoneName");
                Require(string.IsNullOrEmpty(Read(microphone.GetType(), "microphoneName", microphone) as string), "AI部分冷态已有麦克风设置。");
            }
            else RequireEmptyMap(Read(settings.GetType(), "lastSelectedModels", settings), token);
            if (pbr) RequireEmptyMap(Read(settings.GetType(), "lastMaterialMappings", settings), token);
            object preview = Read(settings.GetType(), "previewSettings", settings);
            RequireRecord(preview, "Unity.AI.Generators.UI.PreviewSettings", token, "sizeFactor");
            Require(Read(preview.GetType(), "sizeFactor", preview) is float factor && factor == 1f, "AI部分冷态已有非默认预览设置。");
        }

        /// <summary>只审有限已知init键及准确默认负载；序列化背书也不能藏有未知恢复数据。</summary>
        private static void RequireDefaultPersistence(string prefix, object data, CancellationToken token)
        {
            RequireSerializableMap(data, token);
            Type type = data.GetType();
            Require(type.GetGenericArguments().SequenceEqual(new[] { typeof(string), typeof(object) }), "AI持久映射键值类型变化。");
            var dictionary = (IDictionary)data;
            Require(dictionary.Count <= 2, "AI部分冷态含未知持久键。");
            var keys = new List<string>();
            foreach (DictionaryEntry entry in dictionary)
            {
                Require(entry.Key is string key && (key == "init" || prefix == "Unity.AI.Image" && key == "modelSelector/init"), "AI部分冷态含未知持久键。");
                string name = (string)entry.Key;
                keys.Add(name);
                RequireDefaultAppData(name == "init" ? prefix : "Unity.AI.ModelSelector", entry.Value, token);
            }
            IList serialized = SerializedEntries(data);
            Require(serialized.Count == 0 || serialized.Count == keys.Count, "AI持久映射的序列化背书与已有键不一致。");
            var serializedKeys = new List<string>();
            foreach (object pair in serialized)
            {
                Require(pair != null, "AI持久映射含空序列化记录。");
                object key = Read(pair.GetType(), "key", pair);
                Require(key is string text && keys.Contains(text, StringComparer.Ordinal) && !serializedKeys.Contains(text, StringComparer.Ordinal), "AI持久映射含未知或重复序列化键。");
                string name = (string)key;
                serializedKeys.Add(name);
                RequireDefaultAppData(name == "init" ? prefix : "Unity.AI.ModelSelector", Read(pair.GetType(), "value", pair), token);
            }
        }

        /// <summary>默认持久负载仅含空设置、空结果与空API缓存；唯一任务许可是源码使用的BCL完成哨兵。</summary>
        private static void RequireDefaultAppData(string prefix, object value, CancellationToken token)
        {
            bool selector = prefix == "Unity.AI.ModelSelector";
            RequireRecord(value, prefix + ".Services.SessionPersistence.AppData", token,
                selector ? new[] { "modelSelectorSlice", "apiState" } : new[] { "sessionSlice", "generationSettingsSlice", "generationResultsSlice", "apiState" });
            if (selector) RequireDefaultModelSelector(Read(value.GetType(), "modelSelectorSlice", value), token);
            else
            {
                RequireDefaultSession(prefix, Read(value.GetType(), "sessionSlice", value), token);
                foreach (string role in new[] { "generationSettings", "generationResults" })
                {
                    object slice = Read(value.GetType(), role + "Slice", value);
                    RequireRecord(slice, prefix + ".Services.Stores.States." + char.ToUpperInvariant(role[0]) + role.Substring(1), token, role);
                    RequireEmptyMap(Read(slice.GetType(), role, slice), token);
                }
            }
            object api = Read(value.GetType(), "apiState", value);
            if (api == null) return;
            RequireRecord(api, "Unity.AI.Generators.Redux.Toolkit.ApiState", token, "cachedResponses", "refetchOperations");
            RequireEmptyMap(Read(api.GetType(), "cachedResponses", api), token);
            Require(IsDefaultCompletedApiTask(Read(api.GetType(), "refetchOperations", api)), "AI部分冷态有实际API任务，不接管或等待该工作。");
        }

        /// <summary>已保存的模型选择器默认态没有发现结果、筛选值、环境或发现时间，不执行discover/fetch。</summary>
        private static void RequireDefaultModelSelector(object value, CancellationToken token)
        {
            const string prefix = "Unity.AI.ModelSelector.Services.Stores.States.";
            RequireRecord(value, prefix + "ModelSelector", token, "lastSelectedModelID", "lastUsedModels", "modelPopularityScore", "settings");
            Require(string.IsNullOrEmpty(Read(value.GetType(), "lastSelectedModelID", value) as string), "AI部分冷态已有模型选择。");
            RequireEmptyMap(Read(value.GetType(), "lastUsedModels", value), token);
            RequireEmptyMap(Read(value.GetType(), "modelPopularityScore", value), token);
            object settings = Read(value.GetType(), "settings", value);
            RequireRecord(settings, prefix + "Settings", token, "models", "environment", "lastModelDiscoveryTimestamp", "filters", "sortMode");
            object models = Read(settings.GetType(), "models", settings);
            Require(models?.GetType().IsGenericType == true && models.GetType().GetGenericTypeDefinition() == typeof(List<>) && ((IList)models).Count == 0,
                "AI部分冷态已有发现模型或未知模型列表。");
            Require(string.IsNullOrEmpty(Read(settings.GetType(), "environment", settings) as string) &&
                Read(settings.GetType(), "lastModelDiscoveryTimestamp", settings) is long timestamp && timestamp == 0,
                "AI部分冷态已有模型发现环境或时间。");
            object sort = Read(settings.GetType(), "sortMode", settings);
            Type sortType = Find(prefix + "SortMode");
            Require(sort?.GetType() == sortType && Convert.ToInt32(sort) == Convert.ToInt32(sortType.GetField("Name").GetRawConstantValue()), "AI部分冷态模型排序非默认。");
            object filters = Read(settings.GetType(), "filters", settings);
            string[] arrays = { "modalities", "operations", "capabilities", "consumers", "providers", "tags", "baseModelIds", "misc" };
            RequireRecord(filters, prefix + "ModelSelectorFilters", token, arrays.Concat(new[] { "searchQuery" }).ToArray());
            Require(string.IsNullOrEmpty(Read(filters.GetType(), "searchQuery", filters) as string), "AI部分冷态已有模型搜索条件。");
            foreach (string name in arrays)
            {
                object array = Read(filters.GetType(), name, filters);
                Type type = array?.GetType();
                Require(type?.IsGenericType == true && type.GetGenericTypeDefinition() == Find("Unity.AI.Generators.UI.Utilities.ImmutableArray`1"), "AI模型筛选数组类型变化。");
                Verify(type, token, new List<string>());
                Require(Read(type, "m_Items", array) is Array items && items.Length == 0, "AI部分冷态已有模型筛选项。");
            }
        }

        /// <summary>准确记录类型与全部实例字段必须等于已审源码形状，不以类型名前缀接受新字段。</summary>
        private static void RequireRecord(object value, string name, CancellationToken token, params string[] fields)
        {
            Type type = value?.GetType();
            Require(type == Find(name), "AI部分冷态记录类型变化：" + name);
            Verify(type, token, new List<string>());
            Require(type.GetFields(Fields).Where(field => !field.IsStatic).Select(field => field.Name).OrderBy(item => item, StringComparer.Ordinal)
                .SequenceEqual(fields.OrderBy(item => item, StringComparer.Ordinal)), "AI部分冷态记录新增或缺失字段：" + name);
        }

        /// <summary>准确SerializableDictionary的运行和序列化内容都必须为空。</summary>
        private static void RequireEmptyMap(object value, CancellationToken token)
        { RequireSerializableMap(value, token); Require(((IDictionary)value).Count == 0 && SerializedEntries(value).Count == 0, "AI部分冷态已有设置、生成结果或缓存数据。"); }

        /// <summary>只读取已审BCL字典派生实现，拒绝自定义映射回调。</summary>
        private static void RequireSerializableMap(object value, CancellationToken token)
        {
            Type type = value?.GetType();
            Require(type?.IsGenericType == true && type.GetGenericTypeDefinition() == Find("Unity.AI.Toolkit.Utility.SerializableDictionary`2") &&
                type.BaseType.IsGenericType && type.BaseType.GetGenericTypeDefinition() == typeof(Dictionary<,>), "AI部分冷态映射实现变化。");
            Verify(type, token, new List<string>());
        }

        /// <summary>读取已审序列化背书，不执行OnBeforeSerialize或OnAfterDeserialize。</summary>
        private static IList SerializedEntries(object value)
        {
            object entries = Read(value.GetType(), "serializedData", value);
            Require(entries?.GetType().IsGenericType == true && entries.GetType().GetGenericTypeDefinition() == typeof(List<>) && ((IList)entries).Count <= 256,
                "AI映射序列化背书不是有限BCL列表。");
            return (IList)entries;
        }

        /// <summary>只接受准确BCL完成哨兵，不把任意已完成、取消或排队任务视作默认态。</summary>
        internal static bool IsDefaultCompletedApiTask(object value) => ReferenceEquals(value, System.Threading.Tasks.Task.CompletedTask);

        /// <summary>固定结构字段图只读捕获，拒绝任务、Unity对象、指针和未知来源，绝不调用属性或序列化回调。</summary>
        private sealed class DataGraph
        {
            internal readonly List<string> Rows = new List<string>();
            private readonly HashSet<object> seen = new HashSet<object>(ReferenceComparer.Instance);
            private readonly CancellationToken token;
            /// <summary>绑定本次只读取消，不注册厂商工作。</summary>
            internal DataGraph(CancellationToken token) { this.token = token; }
            /// <summary>递归字段有界，集合只能是已审BCL容器或已审SerializableDictionary。</summary>
            internal void Add(string path, object value, bool defaultApiTask = false)
            {
                token.ThrowIfCancellationRequested();
                Require(Rows.Count < 8192 && path.Length < 2048, "AI缺省字段图容量超界。");
                if (value == null) { Rows.Add(path + "=null"); return; }
                Type type = value.GetType();
                if (value is System.Threading.Tasks.Task)
                {
                    Require(defaultApiTask && IsDefaultCompletedApiTask(value), "AI缺省字段出现非默认API完成哨兵的实际任务。");
                    Rows.Add(path + "=completed-api-sentinel:" + Key(value));
                    return;
                }
                if (value is string text) { Require(text.Length <= 4096, "AI字段文本超界。"); Rows.Add(path + "=string:" + Convert.ToBase64String(System.Text.Encoding.UTF8.GetBytes(text))); return; }
                if (type.IsPrimitive || type.IsEnum || value is decimal)
                { Rows.Add(path + "=" + type.FullName + ":" + Convert.ToString(value, System.Globalization.CultureInfo.InvariantCulture)); return; }
                Require(!(value is Delegate) && !(value is System.Threading.Tasks.Task) && !(value is Object) && !(value is IntPtr) && !(value is UIntPtr),
                    "AI缺省数据中出现任务、委托、Unity对象或指针。");
                if (!type.IsValueType)
                {
                    Rows.Add(path + "=ref:" + Key(value) + "|" + type.FullName);
                    if (!seen.Add(value)) return;
                }
                if (value is IDictionary dictionary)
                {
                    Require(IsCollection(type, typeof(Dictionary<,>)) && dictionary.Count <= 256, "AI数据映射不是有限BCL字典。");
                    if (type.IsGenericType && type.GetGenericTypeDefinition() != typeof(Dictionary<,>)) Verify(type, token, new List<string>());
                    var entries = new List<DictionaryEntry>();
                    foreach (DictionaryEntry entry in dictionary) entries.Add(entry);
                    foreach (DictionaryEntry entry in entries.OrderBy(entry => entry.Key as string, StringComparer.Ordinal))
                    { Require(entry.Key is string, "AI缺省数据包含非字符串非空键。"); Add(path + "[" + entry.Key + "]", entry.Value); }
                    if (type.IsGenericType && type.GetGenericTypeDefinition().FullName == "Unity.AI.Toolkit.Utility.SerializableDictionary`2")
                        Add(path + ".serializedData", SerializedEntries(value));
                    return;
                }
                if (value is IList list)
                {
                    Require(type.IsArray || type.IsGenericType && type.GetGenericTypeDefinition() == typeof(List<>), "AI数据列表不是准确BCL实现。");
                    Require(list.Count <= 256, "AI数据列表容量超界。");
                    for (int i = 0; i < list.Count; i++) Add(path + "[" + i + "]", list[i]);
                    return;
                }
                if (type.IsValueType && type.Assembly == typeof(Vector2).Assembly)
                    AndroidResolvePeripheralRefreshContracts.VerifyModule(type, token);
                else Verify(type, token, new List<string>());
                foreach (FieldInfo field in type.GetFields(BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic).OrderBy(field => field.Name, StringComparer.Ordinal))
                    Add(path + "." + field.Name, field.GetValue(value), type.FullName == "Unity.AI.Generators.Redux.Toolkit.ApiState" && field.Name == "refetchOperations");
            }
        }

        /// <summary>集合去重只用引用身份，不执行厂商Equals或GetHashCode。</summary>
        private sealed class ReferenceComparer : IEqualityComparer<object>
        {
            internal static readonly ReferenceComparer Instance = new ReferenceComparer();
            /// <summary>比较当前托管引用。</summary>
            public new bool Equals(object left, object right) => ReferenceEquals(left, right);
            /// <summary>只取运行时引用散列供局部遍历去重，不作为回执身份。</summary>
            public int GetHashCode(object value) => RuntimeHelpers.GetHashCode(value);
        }

        /// <summary>只接纳固定角色、完整形状和自洽摘要；设置默认容量上限仅用于冷态，已有角色和退出订阅同样完整绑定。</summary>
        private static bool IsValidStore(StoreState state)
        {
            if (state == null) return false;
            int index = Array.IndexOf(Ids, state.id);
            if (index < 0 || state.family != Families[index] || string.IsNullOrEmpty(state.sourceSha256) ||
                state.settingsPath != "UserSettings/AI." + Families[index] + "/" + Settings[index] + ".asset" || state.settingsScriptGuid != SettingsGuids[index] ||
                !(state.settingsFileSha256 == "absent" ? state.settingsFileLength == 0 && state.settingsFileWriteUtcTicks == 0 :
                    Regex.IsMatch(state.settingsFileSha256 ?? "", "\\A[0-9a-f]{64}\\z") && state.settingsFileLength > 0 &&
                    (state.initialized || state.settingsFileLength <= 8192) && state.settingsFileWriteUtcTicks > 0) ||
                state.roleRows == null || state.dataRows == null || state.retainedRows == null || state.retainedRows.Any(string.IsNullOrEmpty) ||
                state.retainedRows.Distinct(StringComparer.Ordinal).Count() != state.retainedRows.Length || state.ownedObjects == null || state.sha256 != ComputeStoreHash(state)) return false;
            string prefix = "Unity.AI." + Families[index] + ".Services.SessionPersistence.";
            if (state.ownedObjects.Length > 2 || state.ownedObjects.Any(item => item == null) ||
                !state.ownedObjects.Select(item => item.role).SequenceEqual(new[] { "persistence", "settings" }.Where(role => state.ownedObjects.Any(item => item.role == role))) ||
                state.ownedObjects.Any(item => item.type != prefix + (item.role == "persistence" ? "ObjectPersistence" : Settings[index])) ||
                state.ownedObjects.Select(item => item.id).Distinct().Count() != state.ownedObjects.Length ||
                !state.ownedObjects.All(item => long.TryParse(item.id, out long value) && value != 0 && value.ToString() == item.id &&
                    item.hideFlags == (int)(HideFlags.HideAndDontSave & ~HideFlags.NotEditable)) ||
                (state.ownedObjects.Any(item => item.role == "settings") ? string.IsNullOrEmpty(state.quittingHandler) : state.quittingHandler != null)) return false;
            if (!state.initialized) return state.listenerCount == 0 && state.middlewareCount == 0 &&
                (state.ownedObjects.Length == 0 ? state.roleRows.Length == 0 && state.dataRows.Length == 0 && state.retainedRows.Length == 0 :
                    state.roleRows.Length > 0 && state.dataRows.Length > 0 && state.retainedRows.Length > 0);
            return state.roleRows.Length > 0 && state.dataRows.Length > 0 && state.ownedObjects.Length == 2 &&
                state.ownedObjects.All(item => long.TryParse(item.id, out long value) && value != 0 && value.ToString() == item.id &&
                    item.hideFlags == (int)(HideFlags.HideAndDontSave & ~HideFlags.NotEditable)) &&
                state.ownedObjects.Select(item => item.id).Distinct().Count() == 2 && !string.IsNullOrEmpty(state.quittingHandler) &&
                state.listenerCount == (state.family == "Image" ? 3 : 2) && state.middlewareCount == (state.family == "Mesh" ? 2 : 3);
        }

        /// <summary>已有Unity角色必须保持准确身份、类型、hideFlags和原脏状态。</summary>
        private static bool SameOwned(OwnedObject before, OwnedObject after) => before != null && after != null && before.role == after.role &&
            before.id == after.id && before.type == after.type && before.dirty == after.dirty && before.hideFlags == after.hideFlags;

        /// <summary>只接纳准确脚本绑定的原生单对象默认文本；不加载资产，不反序列化任意YAML或托管引用。</summary>
        internal static bool TryValidateSavedDefaultText(int id, string text, out string error)
        {
            error = null;
            int index = Array.IndexOf(Ids, id);
            if (index < 0 || text == null || text.Length > 8192 || text.Contains("\t"))
            { error = "AI设置不是有界的已审默认原生文本。"; return false; }
            if (text.StartsWith("\uFEFF", StringComparison.Ordinal)) text = text.Substring(1);
            string normalized = text.Replace("\r\n", "\n");
            if (normalized.Contains("\r")) { error = "AI设置含未知换行结构。"; return false; }
            normalized = string.Join("\n", normalized.TrimEnd('\n').Split('\n').Select(line => line.TrimEnd(' ')));
            string prefix = "Unity.AI." + Families[index];
            var expected = new List<string> { "%YAML 1.1", "%TAG !u! tag:unity3d.com,2011:", "--- !u!114 &1", "MonoBehaviour:",
                "  m_ObjectHideFlags: 53", "  m_CorrespondingSourceObject: {fileID: 0}", "  m_PrefabInstance: {fileID: 0}",
                "  m_PrefabAsset: {fileID: 0}", "  m_GameObject: {fileID: 0}", "  m_Enabled: 1", "  m_EditorHideFlags: 0",
                "  m_Script: {fileID: 11500000, guid: " + SettingsGuids[index] + ", type: 3}", "  m_Name:",
                "  m_EditorClassIdentifier: " + prefix + "::" + prefix + ".Services.SessionPersistence." + Settings[index], "  m_Session:", "    settings:" };
            if (Families[index] == "Sound") expected.Add("      lastSelectedModelID:");
            else { expected.Add("      lastSelectedModels:"); expected.Add("        serializedData: []"); }
            if (Families[index] == "Pbr") { expected.Add("      lastMaterialMappings:"); expected.Add("        serializedData: []"); }
            expected.Add("      previewSettings:"); expected.Add("        sizeFactor: 1");
            if (Families[index] == "Sound") { expected.Add("      microphoneSettings:"); expected.Add("        microphoneName:"); }
            else { expected.Add("  references:"); expected.Add("    version: 2"); expected.Add("    RefIds: []"); }
            if (normalized != string.Join("\n", expected))
            { error = "AI设置不是准确脚本、单对象和源码默认字段结构；已有数据、未知字段、托管引用或额外文档继续拒绝。"; return false; }
            return true;
        }

        /// <summary>冷态仅接纳缺失或严格默认的设置，已初始化态只冻结原文件；两者均核对源脚本GUID和AssetDatabase绑定。</summary>
        private static void CaptureSettingsFile(Type type, int index, string relative, bool initialized, CancellationToken token, ICollection<string> sources,
            out string sha256, out long length, out long writeUtcTicks)
        {
            sha256 = "absent";
            length = writeUtcTicks = 0;
            CustomAttributeData attribute = CustomAttributeData.GetCustomAttributes(type).SingleOrDefault(item => item.AttributeType == typeof(FilePathAttribute));
            Require(attribute != null && attribute.ConstructorArguments.Count == 2 && Equals(attribute.ConstructorArguments[0].Value, relative) &&
                Convert.ToInt32(attribute.ConstructorArguments[1].Value) == (int)FilePathAttribute.Location.ProjectFolder, "AI设置FilePath归属变化。");
            PackageInfo package = PackageInfo.FindForAssembly(type.Assembly);
            Require(package?.name == "com.unity.ai.assistant", "AI设置缺准确包归属。");
            string source = "Modules/Unity.AI." + Families[index] + "/Services/SessionPersistence/" + Settings[index] + ".cs";
            string meta = Path.Combine(package.resolvedPath, source + ".meta");
            Require(File.Exists(meta) && (File.GetAttributes(meta) & FileAttributes.ReparsePoint) == 0, "AI设置源脚本meta缺失或为链接。");
            MatchCollection guids = Regex.Matches(File.ReadAllText(meta), "(?m)^guid: ([0-9a-f]{32})\\r?$");
            Require(guids.Count == 1 && guids[0].Groups[1].Value == SettingsGuids[index] &&
                AssetDatabase.AssetPathToGUID(package.assetPath + "/" + source) == SettingsGuids[index], "AI设置源脚本GUID或实际绑定变化。");
            sources.Add("settings-script-meta|" + GenerateActionCommon.ComputeFileHash(meta, token));
            string path = GenerateActionCommon.ProjectRoot;
            foreach (string segment in relative.Split('/'))
            {
                path = Path.Combine(path, segment);
                if (File.Exists(path) || Directory.Exists(path)) Require((File.GetAttributes(path) & FileAttributes.ReparsePoint) == 0, "AI设置路径含链接。");
            }
            Require(!Directory.Exists(path), "AI设置目标是目录。");
            if (!File.Exists(path)) return;
            var before = new FileInfo(path);
            length = before.Length;
            writeUtcTicks = before.LastWriteTimeUtc.Ticks;
            byte[] bytes = null;
            if (initialized) sha256 = GenerateActionCommon.ComputeFileHash(path, token);
            else
            {
                Require(length > 0 && length <= 8192, "AI设置文件容量超出默认文本合同。");
                token.ThrowIfCancellationRequested();
                bytes = File.ReadAllBytes(path);
                string text = new UTF8Encoding(false, true).GetString(bytes);
                Require(TryValidateSavedDefaultText(Ids[index], text, out string error), error);
                using (SHA256 hash = SHA256.Create()) sha256 = BitConverter.ToString(hash.ComputeHash(bytes)).Replace("-", "").ToLowerInvariant();
            }
            var after = new FileInfo(path);
            Require(after.Length == length && (bytes == null || bytes.Length == length) && after.LastWriteTimeUtc.Ticks == writeUtcTicks, "AI设置在只读冻结期间变化。");
        }

        /// <summary>只读已审Core的泛型backing并核全局现存对象，禁止instance getter补造单例。</summary>
        private static object ExistingSingleton(Type type, CancellationToken token)
        {
            Type parent = type.BaseType;
            Require(parent != null && parent.IsGenericType && parent.GetGenericTypeDefinition() == typeof(ScriptableSingleton<>), "AI单例继承变化。");
            AndroidResolvePeripheralRefreshContracts.VerifyModule(parent, token);
            object value = Read(parent, "s_Instance");
            Object[] found = Resources.FindObjectsOfTypeAll(type);
            Require(value == null ? found.Length == 0 : found.Length == 1 && ReferenceEquals(value, found[0]) && value.GetType() == type, "AI单例backing与现存对象不一致。");
            return value;
        }

        /// <summary>单例角色只来自准确backing，不接受资产或其它同名对象。</summary>
        private static OwnedObject CaptureOwned(string role, Type type, object value)
        {
            var target = value as Object;
            Require(target != null && target.GetType() == type && !EditorUtility.IsPersistent(target) && string.IsNullOrEmpty(AssetDatabase.GetAssetPath(target)), "AI单例角色命中资产或未知对象。");
            Require(target.hideFlags == (HideFlags.HideAndDontSave & ~HideFlags.NotEditable), "AI单例不是已审Core缺省创建或准确保存文本的hideFlags。");
            return new OwnedObject { role = role, id = target.GetEntityId().ToString(), type = type.FullName, dirty = EditorUtility.IsDirty(target), hideFlags = (int)target.hideFlags };
        }

        /// <summary>读取已初始化EditorApplication真实退出订阅，原有顺序和每个目标身份全部保留。</summary>
        private static string[] CaptureQuittingHandlers(CancellationToken token)
        {
            AndroidResolvePeripheralRefreshContracts.VerifyModule(typeof(EditorApplication), token);
            Assembly core = typeof(EditorApplication).Assembly;
            Type trackerType = core.GetType("UnityEditor.EventWithPerformanceTracker`1", true).MakeGenericType(typeof(Action));
            object tracker = Read(typeof(EditorApplication), "m_QuittingEvent");
            Require(tracker?.GetType() == trackerType, "Editor退出事件不再使用已审性能事件容器。");
            object handle = Read(trackerType, "m_Delegate", tracker);
            if (handle == null) return Array.Empty<string>();
            Require(handle.GetType() == trackerType.GetField("m_Delegate", Fields).FieldType, "Editor退出事件委托包装类型变化。");
            Type entryType = core.GetType("UnityEditor.EventWithPerformanceTracker+Entry", true);
            object entry = Read(handle.GetType(), "m_DelegateOrList", handle);
            Require(entry?.GetType() == entryType, "Editor退出事件Entry结构变化。");
            object reference = Read(entryType, "Reference", entry);
            if (reference == null) return Array.Empty<string>();
            var delegates = new List<Delegate>();
            if (reference is Action single) delegates.AddRange(single.GetInvocationList());
            else
            {
                Require(reference is Array && reference.GetType() == entryType.MakeArrayType(), "Editor退出事件含未知委托列表。");
                var entries = (Array)reference;
                Require(entries.Length <= 4096, "Editor退出事件Entry容量超界。");
                foreach (object item in entries)
                {
                    object callback = Read(entryType, "Reference", item);
                    Require(callback is Action, "Editor退出事件Entry不是准确Action。");
                    delegates.AddRange(((Action)callback).GetInvocationList());
                }
            }
            Require(delegates.Count <= 4096, "Editor退出订阅容量超界。");
            // 不调用枚举器或StartTrackerAt，避免只读捕获创建原生性能跟踪句柄。
            return delegates.Select(DelegateKey).ToArray();
        }

        /// <summary>只读固定声明字段，静态字段只用于已审Core缓存、无cctor入口或已存在对象。</summary>
        private static object Read(Type type, string name, object target = null)
        {
            FieldInfo field = type.GetField(name, Fields);
            Require(field != null && field.IsStatic == (target == null), "AI角色字段缺失或静态形状变化：" + name);
            return field.GetValue(target);
        }

        /// <summary>只从自然已加载程序集定位唯一类型，不加载替代模块。</summary>
        private static Type Find(string name, bool required = true)
        {
            Type[] values = AppDomain.CurrentDomain.GetAssemblies().Select(assembly => assembly.GetType(name, false, false)).Where(type => type != null).ToArray();
            Require(values.Length <= 1 && (!required || values.Length == 1), "AI角色类型缺失或同名冲突：" + name);
            return values.SingleOrDefault();
        }

        /// <summary>每个owner复用完整已审编译来源，不用类型前缀代替来源合同。</summary>
        private static Type Owner(string name, CancellationToken token, ICollection<string> rows)
        { Type type = Find(name); Verify(type, token, rows); return type; }

        /// <summary>来源只保留精确审核身份，同一组按类型排序去重以免冷暖读取数量影响来源摘要。</summary>
        private static void Verify(Type type, CancellationToken token, ICollection<string> rows)
        {
            string identity = null, error = null;
            Require(type != null && AndroidRefreshSourceIdentityContract.TryVerifySourceAssembly(type, token, out identity, out error),
                "AI角色来源尚未审核：" + type?.FullName + "；" + error);
            rows.Add(type.Assembly.GetName().Name + "|" + identity);
        }

        /// <summary>冷暖两态按固定程序集集合去重，读取同一程序集内更多角色不构成来源变化。</summary>
        private static string SourceHash(IEnumerable<string> rows) => GenerateActionCommon.ComputeTextHash(string.Join("\n", rows.Distinct().OrderBy(value => value, StringComparer.Ordinal)));

        /// <summary>只读取不可覆写BCL容器Count，未知集合不调用其自定义实现。</summary>
        private static int CollectionCount(object value, Type definition)
        {
            Type type = value?.GetType();
            while (type != null && (!type.IsGenericType || type.GetGenericTypeDefinition() != definition)) type = type.BaseType;
            PropertyInfo count = type?.GetProperty("Count", BindingFlags.Public | BindingFlags.Instance | BindingFlags.DeclaredOnly);
            Require(count != null && (!count.GetMethod.IsVirtual || count.GetMethod.IsFinal), "AI集合缺准确BCL Count。");
            return (int)count.GetValue(value);
        }

        /// <summary>检查有限容器基类，不以名称接受未知集合。</summary>
        private static bool IsCollection(Type type, Type definition)
        { for (; type != null; type = type.BaseType) if (type.IsGenericType && type.GetGenericTypeDefinition() == definition) return true; return false; }

        /// <summary>取得编译器闭包所属的真实外层owner，泛型闭包仍归属同一已审源类型。</summary>
        private static Type DeclaringRoot(Type type)
        { while (type?.DeclaringType != null) type = type.DeclaringType; return type; }

        /// <summary>自有域内弱身份只用于当前对象，不保留强引用。</summary>
        private static string Key(object value) => value == null ? "null" : Domain + "|" + Identities.GetValue(value, _ => new Identity()).value;

        /// <summary>准确方法元数据和既有目标共同绑定委托身份，不执行委托。</summary>
        private static string MethodKey(MethodInfo method, object target) => method.Module.ModuleVersionId + "|" + method.MetadataToken + "|" + Key(target);

        /// <summary>准确委托目标和方法用于原订阅比较。</summary>
        private static string DelegateKey(Delegate value) => MethodKey(value.Method, value.Target);

        /// <summary>与主合同完整对象行保持同一格式，脏状态不可丢弃。</summary>
        private static string ObjectRow(OwnedObject value) => value.id + "|" + value.type + "|dirty=" + value.dirty;

        /// <summary>所有未知状态都具体拒绝，不通过物化或恢复推进。</summary>
        private static void Require(bool condition, string error) { if (!condition) throw new InvalidOperationException(error); }
    }
}
