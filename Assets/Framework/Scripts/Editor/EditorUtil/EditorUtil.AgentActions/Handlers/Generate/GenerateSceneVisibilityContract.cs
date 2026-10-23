/***************************************************************
 * (c) copyright 2026 - 2030, Solotopia
 * All Rights Reserved.
 * -------------------------------------------------------------
 * filename:  GenerateSceneVisibilityContract.cs
 * author:    taoye
 * created:   2026/10/16
 * descrip:   Generate 专属原生场景可见性只读前后态合同
 ***************************************************************/
using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Security.Cryptography;
using System.Reflection;
using UnityEditor.SceneManagement;
using System.Text;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using UnityEditor;
using UnityEngine;
using UnityEngine.SceneManagement;
using Object = UnityEngine.Object;

namespace NovaFramework.Editor
{
    /// <summary>观察既存原生对象及公开查询实际产生的有限空缓存；不调用设置、创建、保存或反写。Player/Bundle 不使用此许可。</summary>
    internal static class GenerateSceneVisibilityContract
    {
        internal const string CoreSha256 = "14a25302095cb3c648b4b83b9936ba2df10454ae254da6440a1a968395f2d581";
        internal const string RuntimeCoreSha256 = "25281ab07e11130a45ede1e02cf3003666f9616cca68ae8ab9637f0ccffa33ad";
        internal const string CoreMvid = "11f76f7e-1291-4a87-8174-9af3fa5b621a";
        internal const string StateType = "UnityEditor.SceneVisibilityState";
        internal const string CacheRelativePath = "Library/SceneVisibilityState.asset";
        internal const int MaxRawBytes = 2 * 1024 * 1024;
        internal const int MaxGzipBytes = 32 * 1024;
        internal const int MaxSnapshotBytes = 96 * 1024;
        private const string ZeroGuid = "00000000000000000000000000000000";
        private static readonly string Domain = Guid.NewGuid().ToString("N");
        private static readonly Dictionary<string, WeakReference> Witnesses = new Dictionary<string, WeakReference>(StringComparer.Ordinal);
        private static readonly UTF8Encoding Utf8 = new UTF8Encoding(false, true);
        private static FirstReloadWitness s_FirstReloadWitness;
        private static readonly string[] Tables = { "m_SceneVisibilityData", "m_SceneVisibilityDataIsolated", "m_ScenePickingData" };

        /// <summary>完整原文的有界无损载体；rawSha 绑定原始字节，canonicalSha 绑定严格解析后的完整三表。</summary>
        [Serializable]
        internal sealed class Body
        {
            public int version = 1;
            public int rawLength;
            public string rawSha256;
            public string canonicalSha256;
            public string gzipBase64;
        }

        /// <summary>准确 Library 派生文件的真实观测，不解释其原生编码，也不手写该文件。</summary>
        [Serializable]
        internal sealed class CacheFile
        {
            public string path;
            public bool exists;
            public long length;
            public string sha256;
        }

        /// <summary>加载场景完整身份及保存状态；64位句柄使用十进制字符串避免 JSON 数字精度损失。</summary>
        [Serializable]
        internal sealed class SceneRow
        {
            public string handle;
            public string path;
            public string guid;
            public string name;
            public bool loaded;
            public bool dirty;
            public bool preview;
        }

        /// <summary>原场景 GameObject 准确身份与四个独立原生查询结果，不从 JSON 推断实际标记。</summary>
        [Serializable]
        internal sealed class ObjectRow
        {
            public ulong id;
            public string globalId;
            public string sceneHandle;
            public string name;
            public bool hidden;
            public bool hiddenSubtree;
            public bool pickingDisabled;
            public bool pickingDisabledSubtree;
            public bool preview;
            public int hideFlags;
            public string previewJsonSha256;
            public string previewJson;
            public PreviewComponent[] previewComponents;
        }

        /// <summary>原Preview对象全部组件的完整正文SHA/Native身份/标记，绝不调用其行为或Getter。</summary>
        [Serializable]
        internal sealed class PreviewComponent
        {
            public ulong id;
            public string type;
            public int hideFlags;
            public string jsonSha256;
            public string json;
            public string globalId;
            public string assemblySha256;
            public string assemblyMvid;
        }

        /// <summary>同进程原 Native 实例、完整三表/场景/标记与缓存文件；弱引用只用于同域 ReferenceEquals，不延长存活。</summary>
        [Serializable]
        internal sealed class Snapshot
        {
            public int version = 2;
            public string domain;
            public string process;
            public string coreSha256;
            public string coreMvid;
            public string runtimeCoreSha256;
            public ulong stateId;
            public string stateGlobalId;
            public string dirtyIdentity;
            public ulong managerId;
            public string managerGlobalId;
            public bool dirty;
            public ulong stageManagerId;
            public ulong mainStageId;
            public bool visibilityEnabled;
            public bool pickingEnabled;
            public bool isolated;
            public string activeScene;
            public SceneRow[] scenes;
            public ObjectRow[] objects;
            public Body body;
            public CacheFile cache;
            public Body observation;
            public OwnerRow[] owners;
            public InvalidRow[] invalidObjects;
            public Body queryBeforeBody;
            public bool queryBeforeDirty;
            public CacheFile queryBeforeCache;
            [NonSerialized, JsonIgnore] internal WeakReference stateReference;
            [NonSerialized, JsonIgnore] internal WeakReference managerReference;
            [NonSerialized, JsonIgnore] internal WeakReference stageManagerReference;
            [NonSerialized, JsonIgnore] internal WeakReference mainStageReference;
        }

        /// <summary>仅严格验证方法能创建的短命许可；调用方不能将任意 DTO 当成投影授权。</summary>
        internal sealed class ValidatedCapture
        {
            internal readonly Snapshot baseline;
            internal readonly Snapshot observed;
            internal readonly CompilationObservation compilationObservation;
            /// <summary>只由本类完成前置或后置校验后构造，不写 Unity 状态。</summary>
            /// <param name="baseline">真实原始计划。</param>
            /// <param name="observed">此次完整实际观测。</param>
            /// <param name="compilationObservation">仅正式新域校验完成后的完整只读后验。</param>
            private ValidatedCapture(Snapshot baseline, Snapshot observed, CompilationObservation compilationObservation = null) { this.baseline = baseline; this.observed = observed; this.compilationObservation = compilationObservation; }
            /// <summary>恰一次实际Capture后保留同一完整初次观察；不对任意DTO提供初次审批入口。</summary>
            /// <returns>本域原Native弱见证及完整查询跨度通过后的初次输入许可。</returns>
            internal static ValidatedCapture Initial()
            {
                Snapshot actual = Capture(); ValidateInitialCapture(actual);
                return new ValidatedCapture(actual, actual);
            }
            /// <summary>完整写前校验后封装，不提供任意未验证DTO构造入口。</summary>
            /// <param name="baseline">原始计划。</param>
            /// <param name="observed">真实写前观测。</param>
            /// <returns>完整精确相等才返回许可。</returns>
            internal static ValidatedCapture Before(Snapshot baseline, Snapshot observed)
            {
                if (!BeforeEquals(baseline, observed)) throw new InvalidOperationException("Generate SVS 写前完整 JSON/dirty/Native对象/场景/缓存文件漂移。");
                return new ValidatedCapture(baseline, observed);
            }
            /// <summary>真实scope后态完整校验后封装，不接受缺失或错误证据。</summary>
            /// <param name="baseline">原始完整计划。</param>
            /// <param name="after">真实scope后态。</param>
            /// <param name="observed">此次实际观测。</param>
            /// <param name="scopeError">原始捕获错误。</param>
            /// <returns>完整后验通过才返回许可。</returns>
            internal static ValidatedCapture After(Snapshot baseline, Snapshot after, Snapshot observed, string scopeError)
            {
                if (scopeError != null) throw new InvalidOperationException("Generate SVS scope后态失败：" + scopeError);
                if (!TryValidateAfter(baseline, after, observed, out string error)) throw new InvalidOperationException(error);
                return new ValidatedCapture(baseline, observed);
            }
            /// <summary>真实首次编译域后验全部通过后才构造内部许可，不能由任意DTO直接请求。</summary>
            /// <param name="baseline">原完整计划。</param><param name="after">真实Scope后态。</param><param name="observed">本次完整观测。</param><param name="token">原编译令牌。</param><param name="payloadHash">原载荷SHA。</param>
            /// <returns>完整正式后验通过后的短命许可。</returns>
            internal static ValidatedCapture AfterCompilation(Snapshot baseline, Snapshot after, Snapshot observed, string token, string payloadHash)
            {
                ReloadProof proof = after != null && observed != null && after.domain != observed.domain ? ReloadProof.Verify(token, payloadHash) : null;
                if (!ValidateAfterCore(baseline, after, observed, proof, out string error)) throw new InvalidOperationException(error);
                if (!NativeIdentityEquals(ExpandSnapshot(baseline), ExpandSnapshot(after)) || after.domain == observed.domain && !NativeIdentityEquals(ExpandSnapshot(after), ExpandSnapshot(observed))) throw new InvalidOperationException("Generate SVS 旧编译入口缺少独立原Observed，不接受新的场景重建或HDR恢复例外。");
                return new ValidatedCapture(baseline, observed, proof == null ? null : BuildCompilationObservation(after, observed, proof));
            }

            /// <summary>先复验原三态，再用原稳定Observed核当前域；不以较晚当前材料替代scopeAfter或恢复证据。</summary>
            /// <param name="baseline">原完整计划。</param><param name="after">真实原scopeAfter。</param><param name="originalObserved">原完整证据中的同域稳定后态。</param><param name="current">本次真实当前观察。</param><param name="token">原编译令牌。</param><param name="payloadHash">原payload完整SHA，包含原Final文件SHA绑定。</param>
            /// <returns>保留原基线、原scopeAfter见证key的短命许可。</returns>
            internal static ValidatedCapture AfterCompilationObserved(Snapshot baseline, Snapshot after, Snapshot originalObserved, Snapshot current, string token, string payloadHash)
            {
                if (current == null || current.domain != Domain || current.stateReference?.Target == null || current.managerReference?.Target == null || current.stageManagerReference?.Target == null || current.mainStageReference?.Target == null) throw new InvalidOperationException("Generate SVS Observed入口要求本域真实Capture及四个活弱见证，不能以历史DTO补造当前观察。");
                ValidateSnapshot(current); Snapshot live = ExpandSnapshot(current);
                if (!NativeIdentityEquals(live, live)) throw new InvalidOperationException("Generate SVS 当前活弱见证与原本域对象不符，不能读取编译证明补造许可。");
                if (!TryValidateAfter(baseline, after, originalObserved, out string error)) throw new InvalidOperationException(error);
                ReloadProof proof = null;
                if (originalObserved.domain == current?.domain)
                {
                    if (!BeforeEquals(originalObserved, current)) throw new InvalidOperationException("Generate SVS 原稳定Observed之后同域材料漂移。");
                }
                else
                {
                    proof = ReloadProof.Verify(token, payloadHash);
                    if (current == null || current.domain != Domain || proof.domain != Domain) throw new InvalidOperationException("Generate SVS 缺少真实首次编译域当前材料。");
                    ValidateFirstOrRepeatedReloadFromObserved(after, originalObserved, current, proof);
                }
                return new ValidatedCapture(baseline, current, proof == null ? null : BuildCompilationObservation(after, current, proof));
            }

        }

        /// <summary>只读捕获已经存在的唯一状态/manager和所有加载场景；不调用 instance/GetInstance/加载资产。</summary>
        /// <returns>完整受限自然分支；非空持久标记、未知结构或链接在执行前拒绝。</returns>
        internal static Snapshot Capture()
        {
            var assembly = typeof(SceneVisibilityManager).Assembly;
            if (assembly.ManifestModule.ModuleVersionId.ToString() != CoreMvid || HashFile(assembly.Location) != CoreSha256 || HashFile(typeof(GameObject).Assembly.Location) != RuntimeCoreSha256)
                throw new InvalidOperationException("Generate SVS CoreModule 指纹未审查。");
            Type type = assembly.GetType(StateType, true, false);
            Object[] states = Resources.FindObjectsOfTypeAll(type).Where(value => value != null && value.GetType() == type).ToArray();
            SceneVisibilityManager[] managers = Resources.FindObjectsOfTypeAll<SceneVisibilityManager>().Where(value => value != null && value.GetType() == typeof(SceneVisibilityManager)).ToArray();
            if (states.Length != 1 || managers.Length != 1) throw new InvalidOperationException("Generate SVS 要求唯一自然既存状态和 manager；不创建或初始化它们。");
            Object state = states[0]; SceneVisibilityManager manager = managers[0];
            // 先读无cctor的Singleton backing和已存在history，绝不调用可能CreateAndLoad的instance/Stage getter。
            Type navigationType = assembly.GetType("UnityEditor.SceneManagement.StageNavigationManager", true, false);
            FieldInfo instanceField = navigationType.BaseType.GetField("s_Instance", BindingFlags.Static | BindingFlags.NonPublic);
            if (navigationType.BaseType.TypeInitializer != null || instanceField == null) throw new InvalidOperationException("Generate SVS Stage singleton backing 未审。");
            Object[] navigation = Resources.FindObjectsOfTypeAll(navigationType).Where(value => value != null && value.GetType() == navigationType).ToArray();
            if (navigation.Length != 1 || !ReferenceEquals(instanceField.GetValue(null), navigation[0])) throw new InvalidOperationException("Generate SVS 缺少唯一自然Stage manager，不创建单例。");
            object history = navigationType.GetField("m_NavigationHistory", BindingFlags.Instance | BindingFlags.NonPublic)?.GetValue(navigation[0]);
            if (history == null) throw new InvalidOperationException("Generate SVS Stage history未自然初始化。");
            var stageList = history.GetType().GetField("m_History", BindingFlags.Instance | BindingFlags.NonPublic)?.GetValue(history) as System.Collections.IList;
            object indexValue = history.GetType().GetField("m_CurrentIndex", BindingFlags.Instance | BindingFlags.NonPublic)?.GetValue(history);
            if (!(indexValue is int stageIndex) || stageList == null || stageIndex < 0 || stageIndex >= stageList.Count || !(stageList[stageIndex] is Object mainStage) || mainStage == null || mainStage.GetType().FullName != "UnityEditor.SceneManagement.MainStage")
                throw new InvalidOperationException("Generate SVS 只支持自然Main Stage；不切换/创建/关闭Stage。");
            if (!EditorUtility.IsPersistent(state) || AssetDatabase.GetAssetPath(state) != "" || !AssetDatabase.TryGetGUIDAndLocalFileIdentifier(state, out string guid, out long fileId) || guid != ZeroGuid || fileId != 1)
                throw new InvalidOperationException("Generate SVS 原生状态持久身份不匹配。");
            // 公开四flag查询在实际Native中可能物化当前Preview空表行；完整保存真实查询前态，绝不先查询后倒填。
            Body queryBefore = EncodeBody(EditorJsonUtility.ToJson(state)); bool queryDirty = EditorUtility.IsDirty(state);
            CacheFile queryCache = CaptureCache(BuildActionCommon.ProjectRoot);
            CaptureInventory(manager, assembly, out SceneRow[] scenes, out ObjectRow[] objects, out OwnerRow[] owners, out InvalidRow[] invalidObjects);
            using (var process = System.Diagnostics.Process.GetCurrentProcess())
            {
                var result = new Snapshot { domain = Domain, process = process.Id.ToString(CultureInfo.InvariantCulture) + ":" + process.StartTime.ToUniversalTime().Ticks.ToString(CultureInfo.InvariantCulture), coreSha256 = CoreSha256, coreMvid = CoreMvid, runtimeCoreSha256 = RuntimeCoreSha256,
                    stateId = UnityEngine.EntityId.ToULong(state.GetEntityId()), stateGlobalId = GlobalObjectId.GetGlobalObjectIdSlow(state).ToString(), dirtyIdentity = StateType + "|" + guid + "|" + fileId.ToString(CultureInfo.InvariantCulture) + "|",
                    managerId = UnityEngine.EntityId.ToULong(manager.GetEntityId()), managerGlobalId = GlobalObjectId.GetGlobalObjectIdSlow(manager).ToString(), dirty = EditorUtility.IsDirty(state), stageManagerId = UnityEngine.EntityId.ToULong(navigation[0].GetEntityId()), mainStageId = UnityEngine.EntityId.ToULong(mainStage.GetEntityId()),
                    visibilityEnabled = (bool)typeof(SceneVisibilityManager).GetProperty("enableSceneVisibility", BindingFlags.Instance | BindingFlags.NonPublic).GetValue(manager), pickingEnabled = (bool)typeof(SceneVisibilityManager).GetProperty("enableScenePicking", BindingFlags.Instance | BindingFlags.NonPublic).GetValue(manager), isolated = manager.IsCurrentStageIsolated(), activeScene = SceneManager.GetActiveScene().handle.GetRawData().ToString(CultureInfo.InvariantCulture),
                    scenes = scenes, objects = objects, owners = owners, invalidObjects = invalidObjects, queryBeforeBody = queryBefore, queryBeforeDirty = queryDirty, queryBeforeCache = queryCache, body = EncodeBody(EditorJsonUtility.ToJson(state)), cache = CaptureCache(BuildActionCommon.ProjectRoot),
                    stateReference = new WeakReference(state), managerReference = new WeakReference(manager), stageManagerReference = new WeakReference(navigation[0]), mainStageReference = new WeakReference(mainStage) };
                ValidateSnapshot(result); PackSnapshot(result); RememberWitness(state, "state"); RememberWitness(manager, "manager"); RememberWitness(navigation[0], "stageManager"); RememberWitness(mainStage, "mainStage"); return result;
            }
        }

        /// <summary>逐路径只读拒绝符号链接，保存准确缓存文件的实际 SHA；不创建目录或文件。</summary>
        /// <param name="projectRoot">生产仅传项目根，测试使用自身临时根。</param>
        /// <returns>准确单文件状态，丢失文件也明确记录。</returns>
        internal static CacheFile CaptureCache(string projectRoot)
        {
            string root = Path.GetFullPath(projectRoot), path = Path.Combine(root, CacheRelativePath);
            string current = path;
            while (!string.IsNullOrEmpty(current))
            {
                try { if ((File.GetAttributes(current) & FileAttributes.ReparsePoint) != 0) throw new InvalidOperationException("Generate SVS 缓存路径含链接：" + current); }
                catch (IOException error) when (error is FileNotFoundException || error is DirectoryNotFoundException)
                {
                    string parent = Path.GetDirectoryName(current);
                    if (parent != null && Directory.Exists(parent) && Directory.EnumerateFileSystemEntries(parent).Any(entry => Path.GetFileName(entry) == Path.GetFileName(current)))
                        throw new InvalidOperationException("Generate SVS 路径条目存在但无法检查，拒绝悬空链接或未知文件状态：" + current, error);
                }
                current = Path.GetDirectoryName(current);
            }
            if (!Directory.Exists(root) || Directory.Exists(path)) throw new InvalidOperationException("Generate SVS 缓存项目根/文件角色无效。");
            bool exists = File.Exists(path);
            if (!exists) return new CacheFile { path = path };
            var info = new FileInfo(path); long length = info.Length; long modified = info.LastWriteTimeUtc.Ticks; string sha = HashFile(path); info.Refresh();
            if (!info.Exists || info.Length != length || info.LastWriteTimeUtc.Ticks != modified) throw new InvalidOperationException("Generate SVS 缓存文件在只读捕获中变化。");
            return new CacheFile { path = path, exists = true, length = length, sha256 = sha };
        }

        /// <summary>完整原文有界压缩并严格解析；不是摘要替代正文，未知字段和非空持久数据立即拒绝。</summary>
        /// <param name="json">原生序列化完整 JSON。</param>
        /// <returns>保留原始字节、长度、双 SHA 的无损载体。</returns>
        internal static Body EncodeBody(string json)
        {
            return EncodePayload(json, true);
        }

        /// <summary>完整载荷有界压缩；三表和观察信封分别按各自严格形状校验。</summary>
        /// <param name="json">完整实际原文。</param>
        /// <param name="nativeBody">是否原生三表。</param>
        /// <param name="observationType">其它已审固定DTO，默认完整观察结构。</param>
        /// <returns>不截断的完整原文信封。</returns>
        private static Body EncodePayload(string json, bool nativeBody, Type observationType = null)
        {
            byte[] raw = Utf8.GetBytes(json ?? throw new ArgumentNullException(nameof(json)));
            if (raw.Length == 0 || raw.Length > MaxRawBytes) throw new InvalidOperationException("Generate SVS 完整 JSON 超过2MiB；不截断。");
            JObject parsed = nativeBody ? ParseBody(json) : ParseObservation(json, observationType); byte[] compressed;
            using (var output = new MemoryStream()) { using (var gzip = new GZipStream(output, System.IO.Compression.CompressionLevel.Optimal, true)) gzip.Write(raw, 0, raw.Length); compressed = output.ToArray(); }
            if (compressed.Length > MaxGzipBytes) throw new InvalidOperationException("Generate SVS 无损 JSON 压缩体超过32KiB；不截断。");
            return new Body { rawLength = raw.Length, rawSha256 = Hash(raw), canonicalSha256 = Hash(Utf8.GetBytes(Canonical(parsed))), gzipBase64 = Convert.ToBase64String(compressed) };
        }

        /// <summary>完整有界解码并重核原文与规范 SHA；过期/损坏/额外字段材料不能升级成当前证据。</summary>
        /// <param name="body">原载体，不能为空。</param>
        /// <returns>严格三表对象，数组顺序保留。</returns>
        internal static JObject DecodeBody(Body body)
        {
            return DecodePayload(body, true);
        }

        /// <summary>严格单member解码完整载荷，重压缩精确比较并重新校验全部结构。</summary>
        /// <param name="body">有界完整信封。</param>
        /// <param name="nativeBody">是否原生三表。</param>
        /// <param name="observationType">其它已审固定DTO，默认完整观察结构。</param>
        /// <returns>完整严格树。</returns>
        private static JObject DecodePayload(Body body, bool nativeBody, Type observationType = null)
        {
            if (body == null || body.version != 1 || body.rawLength < 1 || body.rawLength > MaxRawBytes || string.IsNullOrEmpty(body.gzipBase64) || body.gzipBase64.Length > (MaxGzipBytes + 2) / 3 * 4) throw new InvalidOperationException("Generate SVS 完整原文载体缺失或越界。");
            byte[] compressed = Convert.FromBase64String(body.gzipBase64);
            if (compressed.Length > MaxGzipBytes || Convert.ToBase64String(compressed) != body.gzipBase64) throw new InvalidOperationException("Generate SVS gzip 编码不规范。");
            byte[] raw;
            using (var input = new MemoryStream(compressed)) using (var gzip = new GZipStream(input, CompressionMode.Decompress)) using (var output = new MemoryStream())
            {
                byte[] buffer = new byte[4096]; int read;
                while ((read = gzip.Read(buffer, 0, buffer.Length)) != 0) { if (output.Length + read > body.rawLength) throw new InvalidOperationException("Generate SVS 解压长度超过原完整边界。"); output.Write(buffer, 0, read); }
                raw = output.ToArray();
            }
            if (raw.Length != body.rawLength || Hash(raw) != body.rawSha256) throw new InvalidOperationException("Generate SVS 完整原文长度/SHA 不符。");
            // 同一BCL确定性重压缩精确比对，拒尾随字节、额外空member与非规范header，不递归Encode/Decode。
            using (var canonical = new MemoryStream())
            {
                using (var gzip = new GZipStream(canonical, System.IO.Compression.CompressionLevel.Optimal, true)) gzip.Write(raw, 0, raw.Length);
                if (!compressed.SequenceEqual(canonical.ToArray())) throw new InvalidOperationException("Generate SVS gzip不是唯一规范完整member。");
            }
            JObject parsed = nativeBody ? ParseBody(Utf8.GetString(raw)) : ParseObservation(Utf8.GetString(raw), observationType);
            if (Hash(Utf8.GetBytes(Canonical(parsed))) != body.canonicalSha256) throw new InvalidOperationException("Generate SVS 三表规范 SHA 不符。");
            return parsed;
        }

        /// <summary>仅解析本机证实的三张表与空持久数组；不推断 C++存储编码或未知结构。</summary>
        /// <param name="json">完整原文。</param>
        /// <returns>保留全部已知行和顺序的严格对象。</returns>
        private static JObject ParseBody(string json)
        {
            // 明确拒绝注释而不是让解析器忽略未知材料。
            using (var input = new StringReader(json)) using (var scan = new JsonTextReader(input) { DateParseHandling = DateParseHandling.None, MaxDepth = 16 })
                while (scan.Read()) if (scan.TokenType == JsonToken.Comment) throw new InvalidOperationException("Generate SVS JSON含未许可注释。");
            JObject root;
            using (var input = new StringReader(json)) using (var reader = new JsonTextReader(input) { DateParseHandling = DateParseHandling.None, MaxDepth = 16 })
            {
                root = JObject.Load(reader, new JsonLoadSettings { DuplicatePropertyNameHandling = DuplicatePropertyNameHandling.Error, CommentHandling = CommentHandling.Load });
                if (reader.Read()) throw new InvalidOperationException("Generate SVS JSON 尾随材料未知。");
            }
            RequireKeys(root, "SceneVisibilityState"); JObject state = root[StateType.Substring("UnityEditor.".Length)] as JObject; RequireKeys(state, Tables);
            foreach (string table in Tables)
            {
                JObject holder = state[table] as JObject; RequireKeys(holder, "m_SceneData");
                if (!(holder["m_SceneData"] is JArray rows) || rows.Count > 8192) throw new InvalidOperationException("Generate SVS 表行数/形状未知。");
                var keys = new HashSet<string>(StringComparer.Ordinal);
                foreach (JToken token in rows)
                {
                    JObject row = token as JObject; RequireKeys(row, "first", "second"); JObject first = row["first"] as JObject; JObject second = row["second"] as JObject;
                    RequireKeys(first, "guid", "handle"); RequireKeys(first["handle"] as JObject, "value"); RequireKeys(second, "m_SceneGUID", "m_GameObjectsPersistent", "m_HierarchiesPersistent");
                    if (first["guid"].Type != JTokenType.String || second["m_SceneGUID"].Type != JTokenType.String || !ValidGuid((string)first["guid"]) || (string)first["guid"] != (string)second["m_SceneGUID"] || first["handle"]["value"].Type != JTokenType.Integer || !long.TryParse(first["handle"]["value"].ToString(), NumberStyles.Integer, CultureInfo.InvariantCulture, out _)) throw new InvalidOperationException("Generate SVS key/GUID/64位句柄无效。");
                    if (!(second["m_GameObjectsPersistent"] is JArray go) || go.Count != 0 || !(second["m_HierarchiesPersistent"] is JArray hierarchy) || hierarchy.Count != 0) throw new InvalidOperationException("Generate SVS 当前仅支持自然空持久标记分支，不能替用户 ShowAll。");
                    if (!keys.Add(Canonical(first))) throw new InvalidOperationException("Generate SVS 原生表重复键。");
                }
            }
            return root;
        }

        /// <summary>检查完整 DTO 的有限自然分支与证据预算；不通过 JSON 替代实际四标记。</summary>
        /// <param name="value">真实或已保存的单次材料。</param>
        private static void ValidateSnapshot(Snapshot value)
        {
            value = ExpandSnapshot(value);
            if (value != null && Utf8.GetByteCount(JsonConvert.SerializeObject(value, Formatting.None)) > MaxRawBytes) throw new InvalidOperationException("Generate SVS 完整观察原文超过2MiB，不截断。");
            if (value == null || value.version != 2 || string.IsNullOrEmpty(value.domain) || string.IsNullOrEmpty(value.process) || value.coreSha256 != CoreSha256 || value.coreMvid != CoreMvid || value.runtimeCoreSha256 != RuntimeCoreSha256 || value.stateId == 0 || value.managerId == 0 || value.stageManagerId == 0 || value.mainStageId == 0 || string.IsNullOrEmpty(value.stateGlobalId) || string.IsNullOrEmpty(value.managerGlobalId) || value.dirtyIdentity != StateType + "|" + ZeroGuid + "|1|" || value.isolated || value.scenes == null || !value.scenes.Any(row => row != null && !row.preview) || value.objects == null || value.objects.Length > 4096)
                throw new InvalidOperationException("Generate SVS 原生身份、场景或自然非隔离分支不完整。");
            if (value.scenes.Any(row => row == null || !row.loaded || !ValidGuid(row.guid) || (row.preview ? row.guid != ZeroGuid || row.path != "" : row.dirty || row.guid == ZeroGuid || string.IsNullOrEmpty(row.path)) || !ulong.TryParse(row.handle, NumberStyles.None, CultureInfo.InvariantCulture, out _)) || value.scenes.Select(row => row.handle).Distinct(StringComparer.Ordinal).Count() != value.scenes.Length || !value.scenes.Any(row => row.handle == value.activeScene)) throw new InvalidOperationException("Generate SVS 加载场景身份或保存状态不符。");
            if (value.objects.Any(row => row == null || row.id == 0 || string.IsNullOrEmpty(row.globalId) || !value.scenes.Any(scene => scene.handle == row.sceneHandle) || row.hidden || row.hiddenSubtree || row.pickingDisabled || row.pickingDisabledSubtree || row.preview != value.scenes.Single(scene => scene.handle == row.sceneHandle).preview || (row.preview ? !IsSha256(row.previewJsonSha256) || row.previewComponents == null || row.previewComponents.Length == 0 || row.previewComponents.Any(component => component == null || component.id == 0 || (component.type != typeof(Transform).AssemblyQualifiedName && component.type != typeof(Light).AssemblyQualifiedName) || !IsSha256(component.jsonSha256)) || row.previewComponents.Select(component => component.id).Distinct().Count() != row.previewComponents.Length : row.previewJsonSha256 != null || row.previewJson != null || row.previewComponents != null)) || value.objects.Select(row => row.id).Distinct().Count() != value.objects.Length) throw new InvalidOperationException("Generate SVS 对象身份或四标记不是原始全部可见/可选择自然分支。");
            if (value.cache == null || string.IsNullOrEmpty(value.cache.path) || !Path.IsPathRooted(value.cache.path) || !value.cache.path.Replace('\\', '/').EndsWith("/" + CacheRelativePath, StringComparison.Ordinal) || value.cache.length < 0 || value.cache.exists != (value.cache.sha256 != null) || value.cache.exists && !IsSha256(value.cache.sha256) || !value.cache.exists && value.cache.length != 0) throw new InvalidOperationException("Generate SVS 准确缓存文件材料缺失。");
            DecodeBody(value.body); ValidateInventory(value);
            if (value.queryBeforeDirty != value.dirty || !Same(value.queryBeforeCache, value.cache)) throw new InvalidOperationException("Generate SVS 公开查询期间dirty或Library文件变化，缺少有限合同。");
            ValidateAppend(DecodeBody(value.queryBeforeBody), DecodeBody(value.body), AllowedPreviewHandles(value), "m_SceneVisibilityData");
            if (value.observation != null && Utf8.GetByteCount(JsonConvert.SerializeObject(value, Formatting.None)) > MaxSnapshotBytes) throw new InvalidOperationException("Generate SVS 单次完整材料超过96KiB；不截断，保留原8MiB总证据门。");
        }

        /// <summary>仅校验固定压缩wire的完整存储材料；不读取Native、不核当前域弱见证，也不授予执行或输入投影许可。</summary>
        /// <param name="raw">Generate计划或完整证据准确位置的原始Snapshot树；保留原gzip正文，允许存储器省略null引用字段。</param>
        /// <returns>无返回值；字段、来源、完整正文或原有预算不符时拒绝。</returns>
        internal static void ValidateStoredSnapshot(JObject raw)
        {
            if (raw == null || Utf8.GetByteCount(raw.ToString(Formatting.None)) > MaxSnapshotBytes) throw new InvalidOperationException("Generate SVS 存储wire缺失或超过96KiB。");
            ValidateStoredWireShape(raw, typeof(Snapshot));
            var settings = new JsonSerializerSettings { TypeNameHandling = TypeNameHandling.None, MetadataPropertyHandling = MetadataPropertyHandling.Ignore, MissingMemberHandling = MissingMemberHandling.Error, DateParseHandling = DateParseHandling.None, Culture = CultureInfo.InvariantCulture, MaxDepth = 64 };
            Snapshot value = raw.ToObject<Snapshot>(JsonSerializer.Create(settings));
            if (value?.body == null || value.observation == null) throw new InvalidOperationException("Generate SVS 存储材料必须保留完整body与observation，不接受inline或旧缺失材料。");
            ValidateSnapshot(value);
        }

        /// <summary>只容许固定Snapshot/Body/Cache形状中省略null引用；值字段必须显式存在，禁止DTO默认值补造证据。</summary>
        /// <param name="token">准确wire节点。</param><param name="type">固定已审存储DTO类型。</param>
        /// <returns>无返回值；未知字段、错类型或缺失值字段立即拒绝。</returns>
        private static void ValidateStoredWireShape(JToken token, Type type)
        {
            if (type != typeof(Snapshot) && type != typeof(Body) && type != typeof(CacheFile)) { ValidateDtoShape(token, type); return; }
            if (token?.Type == JTokenType.Null) return;
            if (!(token is JObject row)) throw new InvalidOperationException("Generate SVS 存储DTO必须是准确对象。");
            FieldInfo[] fields = type.GetFields(BindingFlags.Instance | BindingFlags.Public);
            if (row.Properties().Any(property => !fields.Any(field => field.Name == property.Name))) throw new InvalidOperationException("Generate SVS 存储DTO存在未知字段。");
            foreach (FieldInfo field in fields)
            {
                JToken child = row[field.Name];
                if (child == null) { if (field.FieldType.IsValueType || type == typeof(Body)) throw new InvalidOperationException("Generate SVS 存储字段缺失：" + field.Name); continue; }
                if (field.FieldType == typeof(Body) || field.FieldType == typeof(CacheFile)) ValidateStoredWireShape(child, field.FieldType);
                else ValidateDtoShape(child, field.FieldType);
            }
        }

        /// <summary>独立获取本域首次实际观察许可，初次公开查询暖化原文完整保留，不以第二次捕获掩盖。</summary>
        /// <returns>只有内部恰一次真实Capture及当前域/弱见证通过才返回的初次许可。</returns>
        internal static ValidatedCapture CaptureInitial() => ValidatedCapture.Initial();

        /// <summary>完整校验实际初次Capture及其当前域原Native弱见证；只验材料，不单独产生任何许可。</summary>
        /// <param name="actual">仅初次factory内部Capture返回的同一完整原材料；永久纯测试可构造自有材料验证拒绝边界。</param>
        private static void ValidateInitialCapture(Snapshot actual)
        {
            ValidateSnapshot(actual); Snapshot expanded = ExpandSnapshot(actual);
            if (expanded.domain != Domain || expanded.stateReference?.Target == null || expanded.managerReference?.Target == null || expanded.stageManagerReference?.Target == null || expanded.mainStageReference?.Target == null || !NativeIdentityEquals(expanded, expanded))
                throw new InvalidOperationException("Generate SVS 初次观察须属于当前实际Capture域及同一活Native弱见证，不能使用旧域或反序列化DTO补造。");
        }

        /// <summary>写前必须完整原样，包含 JSON原文、dirty和缓存文件；同域额外核真正托管引用。</summary>
        /// <param name="before">原计划。</param>
        /// <param name="current">当前真实捕获。</param>
        /// <returns>所有字段完全相等时为 true。</returns>
        internal static bool BeforeEquals(Snapshot before, Snapshot current)
        {
            try { ValidateSnapshot(before); ValidateSnapshot(current); before = ExpandSnapshot(before); current = ExpandSnapshot(current); return before.domain == current.domain && NativeIdentityEquals(before, current) && before.dirty == current.dirty && before.body.rawSha256 == current.queryBeforeBody.rawSha256 && before.body.rawSha256 == current.body.rawSha256 && Same(before.cache, current.cache); }
            catch (Exception) { return false; }
        }

        /// <summary>原 Plan与写前重捕完全相同后创建准确输入投影许可；不调用原生写方法。</summary>
        /// <param name="before">原始计划。</param>
        /// <param name="current">本次写前完整观测。</param>
        /// <returns>只有完整精确相等才返回的内部许可。</returns>
        internal static ValidatedCapture ApproveBefore(Snapshot before, Snapshot current)
        {
            return ValidatedCapture.Before(before, current);
        }

        /// <summary>校验真实before→scopeAfter→同域原Observed的有限转换；核心身份、全部原文及稳定后态逐项保持，不回写UI。</summary>
        /// <param name="before">完整原计划，旧 hash-only回执不能补造。</param>
        /// <param name="after">正式同步生成链释放 scope后立即取得的真实后态。</param>
        /// <param name="current">同域原稳定Observed；新域不能替代原恢复证据。</param>
        /// <param name="error">未知变化的具体原因。</param>
        /// <returns>同步有限转换与完整稳定后态通过时才为true；新编译域仍需独立正式证明。</returns>
        internal static bool TryValidateAfter(Snapshot before, Snapshot after, Snapshot current, out string error)
        {
            return ValidateAfterCore(before, after, current, null, out error);
        }

        /// <summary>明确消费正式编译首次重载证明的Generate专属后验，默认API仍拒跨域例外。</summary>
        /// <param name="before">原完整计划。</param><param name="after">真实Scope后态。</param><param name="current">当前真实观测。</param><param name="proof">仅真实编译记录产生的首次域证明。</param><param name="error">具体失败。</param>
        /// <returns>有限合同全部满足时为真。</returns>
        private static bool ValidateAfterCore(Snapshot before, Snapshot after, Snapshot current, ReloadProof proof, out string error)
        {
            error = null;
            try
            {
                ValidateSnapshot(before); ValidateSnapshot(after); ValidateSnapshot(current);
                before = ExpandSnapshot(before); after = ExpandSnapshot(after); current = ExpandSnapshot(current);
                if (before.domain != after.domain || (after.domain == current.domain ? !GenerateIdentityTransitionEquals(before, after, current) : !NativeIdentityEquals(before, after))) throw new InvalidOperationException("Generate SVS 同进程原Native对象/manager或完整场景对象四标记漂移。");
                if (!before.dirty && after.dirty) throw new InvalidOperationException("Generate SVS 出现未许可的新 dirty；不清理该标志。");
                ValidateAppend(DecodeBody(before.body), DecodeBody(after.queryBeforeBody), null, null);
                ValidateAppend(DecodeBody(before.body), DecodeBody(after.body), null, null);
                if (before.cache.path != after.cache.path || before.cache.exists && !after.cache.exists) throw new InvalidOperationException("Generate SVS 准确 Library 缓存身份改变或已有缓存丢失。");
                if (after.domain == current.domain)
                {
                    if (after.dirty != current.dirty || after.body.rawSha256 != current.queryBeforeBody.rawSha256 || after.body.rawSha256 != current.body.rawSha256 || !Same(after.cache, current.cache)) throw new InvalidOperationException("Generate SVS 同域scopeAfter之后完整对象/JSON/dirty/Library缓存漂移，保持partial。");
                }
                else
                {
                    if (proof == null || proof.domain != Domain || current.domain != Domain) throw new InvalidOperationException("Generate SVS 跨域缺少本次正式编译的首次重载域证明，不能借用后续reload。");
                    ValidateFirstOrRepeatedReload(after, current, proof);
                }
                return true;
            }
            catch (Exception exception) { error = "Generate SVS 有限只读后验未闭合：" + exception.Message; return false; }
        }

        /// <summary>只校验Generate三态的有限对象转换；完整原DTO保持不变，写前与旧跨域入口仍使用原严格比较。</summary>
        /// <param name="before">原计划完整观察。</param><param name="after">真实scope释放后的观察。</param><param name="observed">同域真实稳定观察，不以新域材料补造恢复。</param>
        /// <returns>原严格身份完全保持，或已保存场景整组重建及准确相机HDR恢复全部通过时为真。</returns>
        private static bool GenerateIdentityTransitionEquals(Snapshot before, Snapshot after, Snapshot observed)
        {
            if (NativeIdentityEquals(before, after) && NativeIdentityEquals(after, observed)) return true;
            if (before.domain != after.domain || after.domain != observed.domain || !GenerateCoreIdentityEquals(before, after) || !GenerateCoreIdentityEquals(after, observed) || !Same(after.objects, observed.objects)) return false;
            if (!Same(before.objects, after.objects) && !SavedSceneObjectsRebuilt(before, after)) return false;
            return InvalidObjectsRestored(before, after, observed);
        }

        /// <summary>显式保留原四角色弱见证、所有Native身份与场景/owner字段；不调用Native，也不投影或修改原观察。</summary>
        /// <param name="before">同域前态。</param><param name="after">同域后态。</param><returns>核心身份和全部场景/owner原样时为真。</returns>
        private static bool GenerateCoreIdentityEquals(Snapshot before, Snapshot after)
        {
            bool references = after.domain != Domain || SameWitness("state", before.stateId, before.stateReference, after.stateReference) && SameWitness("manager", before.managerId, before.managerReference, after.managerReference) && SameWitness("stageManager", before.stageManagerId, before.stageManagerReference, after.stageManagerReference) && SameWitness("mainStage", before.mainStageId, before.mainStageReference, after.mainStageReference);
            return before.domain == after.domain && references && before.process == after.process && before.coreSha256 == after.coreSha256 && before.coreMvid == after.coreMvid && before.runtimeCoreSha256 == after.runtimeCoreSha256 && before.stateId == after.stateId && before.stateGlobalId == after.stateGlobalId && before.dirtyIdentity == after.dirtyIdentity && before.managerId == after.managerId && before.managerGlobalId == after.managerGlobalId && before.stageManagerId == after.stageManagerId && before.mainStageId == after.mainStageId && before.visibilityEnabled == after.visibilityEnabled && before.pickingEnabled == after.pickingEnabled && before.isolated == after.isolated && before.activeScene == after.activeScene && Same(before.scenes, after.scenes) && Same(before.owners, after.owners);
        }

        /// <summary>非Preview仅按已保存场景的完整GlobalID双射；每场景保持全组原ID或全组disjoint重建，拒绝部分替换及跨场景旧ID交换。</summary>
        /// <param name="before">原加载场景与全部对象。</param><param name="after">真实后态全部对象。</param><returns>完整身份双射且非id字段逐一相同时为真。</returns>
        private static bool SavedSceneObjectsRebuilt(Snapshot before, Snapshot after)
        {
            if (!Same(before.objects.Where(row => row.preview).ToArray(), after.objects.Where(row => row.preview).ToArray())) return false;
            if (!before.objects.Select(row => row.id).SequenceEqual(before.objects.Select(row => row.id).OrderBy(id => id)) || !after.objects.Select(row => row.id).SequenceEqual(after.objects.Select(row => row.id).OrderBy(id => id))) return false;
            var oldIds = new HashSet<ulong>(before.objects.Select(row => row.id).Concat(before.invalidObjects.Select(row => row.id)).Concat(before.objects.Where(row => row.preview).SelectMany(row => row.previewComponents).Select(row => row.id)).Concat(before.invalidObjects.SelectMany(row => row.components).Select(row => row.id)).Concat(before.owners.Select(row => row.id)).Concat(new[] { before.stateId, before.managerId, before.stageManagerId, before.mainStageId }));
            foreach (SceneRow scene in before.scenes.Where(row => !row.preview))
            {
                if (ulong.Parse(scene.handle, CultureInfo.InvariantCulture).ToString(CultureInfo.InvariantCulture) != scene.handle || !scene.loaded || scene.dirty || !ValidGuid(scene.guid) || scene.guid == ZeroGuid || string.IsNullOrEmpty(scene.path)) return false;
                ObjectRow[] old = before.objects.Where(row => row.sceneHandle == scene.handle).ToArray(), next = after.objects.Where(row => row.sceneHandle == scene.handle).ToArray();
                if (old.Length != next.Length || old.Any(row => !SavedObjectGlobalId(row.globalId, scene.guid)) || next.Any(row => !SavedObjectGlobalId(row.globalId, scene.guid)) || old.Select(row => row.globalId).Distinct(StringComparer.Ordinal).Count() != old.Length || next.Select(row => row.globalId).Distinct(StringComparer.Ordinal).Count() != next.Length) return false;
                var byId = next.ToDictionary(row => row.globalId, StringComparer.Ordinal); int changed = 0;
                foreach (ObjectRow row in old)
                {
                    if (!byId.TryGetValue(row.globalId, out ObjectRow current) || !ObjectFieldsExceptIdEqual(row, current)) return false;
                    if (row.id != current.id) { if (oldIds.Contains(current.id)) return false; changed++; }
                }
                if (changed != 0 && changed != old.Length) return false;
            }
            return true;
        }

        /// <summary>准确解析保存场景GlobalObjectId_V1-2，场景GUID必须来自对应SceneRow；localID非零且数字为规范UInt64。</summary>
        /// <param name="value">实际完整GlobalID。</param><param name="sceneGuid">该64位场景句柄的已冻结非零GUID。</param><returns>全部字段准确时为真。</returns>
        private static bool SavedObjectGlobalId(string value, string sceneGuid)
        {
            string[] parts = value?.Split('-');
            return parts != null && parts.Length == 5 && parts[0] == "GlobalObjectId_V1" && parts[1] == "2" && parts[2] == sceneGuid && ulong.TryParse(parts[3], NumberStyles.None, CultureInfo.InvariantCulture, out ulong local) && local != 0 && local.ToString(CultureInfo.InvariantCulture) == parts[3] && ulong.TryParse(parts[4], NumberStyles.None, CultureInfo.InvariantCulture, out ulong prefab) && prefab.ToString(CultureInfo.InvariantCulture) == parts[4];
        }

        /// <summary>明确比较ObjectRow除NativeID之外的每个字段；不删除或归一化任何原字段。</summary>
        /// <param name="a">原对象行。</param><param name="b">按场景和GlobalID唯一配对的后态行。</param><returns>所有其余字段完全相同时为真。</returns>
        private static bool ObjectFieldsExceptIdEqual(ObjectRow a, ObjectRow b) => a.globalId == b.globalId && a.sceneHandle == b.sceneHandle && a.name == b.name && a.hidden == b.hidden && a.hiddenSubtree == b.hiddenSubtree && a.pickingDisabled == b.pickingDisabled && a.pickingDisabledSubtree == b.pickingDisabledSubtree && a.preview == b.preview && a.hideFlags == b.hideFlags && a.previewJsonSha256 == b.previewJsonSha256 && a.previewJson == b.previewJson && Same(a.previewComponents, b.previewComponents);

        /// <summary>所有无scene对象原样；只有同一已审SceneView主Camera完整原文中的HDR false→true→false可以临时改变。</summary>
        /// <param name="before">原完整无scene对象与owner。</param><param name="after">真实scope后完整对象。</param><param name="observed">同域真实恢复后完整对象。</param><returns>准确三态与所有其它原字段保持时为真。</returns>
        private static bool InvalidObjectsRestored(Snapshot before, Snapshot after, Snapshot observed)
        {
            if (!Same(before.invalidObjects, observed.invalidObjects) || before.invalidObjects.Length != after.invalidObjects.Length) return false;
            int transitions = 0;
            for (int index = 0; index < before.invalidObjects.Length; index++)
            {
                InvalidRow a = before.invalidObjects[index], b = after.invalidObjects[index];
                if (Same(a, b)) continue;
                if (++transitions != 1 || a.role != "SceneViewCamera" || b.role != a.role || a.ownerKey != b.ownerKey || a.id != b.id || a.globalId != b.globalId || a.name != b.name || a.hideFlags != b.hideFlags || a.json != b.json || a.hidden != b.hidden || a.hiddenSubtree != b.hiddenSubtree || a.pickingDisabled != b.pickingDisabled || a.pickingDisabledSubtree != b.pickingDisabledSubtree || a.components.Length != b.components.Length) return false;
                OwnerRow owner = before.owners.SingleOrDefault(row => row.role == "SceneView" && row.key == a.ownerKey);
                if (owner == null || owner.cameraGoId != a.id || owner.cameraId != a.components[1].id) return false;
                for (int component = 0; component < a.components.Length; component++)
                {
                    PreviewComponent x = a.components[component], y = b.components[component];
                    if (component != 1) { if (!Same(x, y)) return false; continue; }
                    if (x.id != y.id || x.globalId != y.globalId || x.type != y.type || x.type != typeof(Camera).AssemblyQualifiedName || x.hideFlags != y.hideFlags || x.assemblySha256 != y.assemblySha256 || x.assemblyMvid != y.assemblyMvid || !CameraHdrTransient(x.json, y.json)) return false;
                }
            }
            return true;
        }

        /// <summary>完整固定Camera序列化形状严格解析；原文只能恰一次HDR布尔false改true，其它字节全部保留。</summary>
        /// <param name="before">原主Camera完整原文。</param><param name="after">scope后同一Camera完整原文。</param><returns>完整已审形状和准确单布尔转换时为真。</returns>
        private static bool CameraHdrTransient(string before, string after)
        {
            foreach (string json in new[] { before, after })
            {
                using (var text = new StringReader(json)) using (var scan = new JsonTextReader(text) { DateParseHandling = DateParseHandling.None, MaxDepth = 16 })
                    while (scan.Read()) if (scan.TokenType == JsonToken.Comment) return false;
                JObject root;
                using (var text = new StringReader(json)) using (var reader = new JsonTextReader(text) { DateParseHandling = DateParseHandling.None, MaxDepth = 16 })
                {
                    root = JObject.Load(reader, new JsonLoadSettings { DuplicatePropertyNameHandling = DuplicatePropertyNameHandling.Error, CommentHandling = CommentHandling.Load });
                    if (reader.Read() || root.DescendantsAndSelf().Any(value => value.Type == JTokenType.Comment)) return false;
                }
                RequireKeys(root, "Camera"); JObject camera = root["Camera"] as JObject;
                string[] integers = { "m_ClearFlags", "m_projectionMatrixMode", "m_GateFitMode", "m_FOVAxisMode", "m_Iso", "m_BladeCount", "m_RenderingPath", "m_TargetDisplay", "m_TargetEye" };
                string[] numbers = { "m_ShutterSpeed", "m_Aperture", "m_FocusDistance", "m_FocalLength", "m_BarrelClipping", "m_Anamorphism", "near clip plane", "far clip plane", "field of view", "orthographic size", "m_Depth", "m_StereoConvergence", "m_StereoSeparation" };
                string[] booleans = { "m_Enabled", "orthographic", "m_HDR", "m_AllowMSAA", "m_AllowDynamicResolution", "m_ForceIntoRT", "m_OcclusionCulling" };
                string[] objects = { "m_BackGroundColor", "m_Curvature", "m_SensorSize", "m_LensShift", "m_NormalizedViewPortRect", "m_CullingMask", "m_TargetTexture" };
                RequireKeys(camera, integers.Concat(numbers).Concat(booleans).Concat(objects).Concat(new[] { "serializedVersion" }).ToArray());
                if (integers.Any(key => camera[key].Type != JTokenType.Integer) || numbers.Any(key => camera[key].Type != JTokenType.Float && camera[key].Type != JTokenType.Integer || !double.TryParse(camera[key].ToString(Formatting.None), NumberStyles.Float, CultureInfo.InvariantCulture, out double value) || double.IsNaN(value) || double.IsInfinity(value)) || booleans.Any(key => camera[key].Type != JTokenType.Boolean) || camera["serializedVersion"].Type != JTokenType.String || (string)camera["serializedVersion"] != "2") return false;
                foreach (string key in objects)
                {
                    JObject row = camera[key] as JObject;
                    string[] keys = key == "m_BackGroundColor" ? new[] { "r", "g", "b", "a" } : key == "m_NormalizedViewPortRect" ? new[] { "serializedVersion", "x", "y", "width", "height" } : key == "m_CullingMask" ? new[] { "serializedVersion", "m_Bits" } : key == "m_TargetTexture" ? new[] { "instanceID" } : new[] { "x", "y" };
                    RequireKeys(row, keys);
                    if (row.Properties().Any(property => property.Name == "serializedVersion" ? property.Value.Type != JTokenType.String || (string)property.Value != "2" : property.Name == "m_Bits" || property.Name == "instanceID" ? property.Value.Type != JTokenType.Integer : property.Value.Type != JTokenType.Integer && property.Value.Type != JTokenType.Float || !double.TryParse(property.Value.ToString(Formatting.None), NumberStyles.Float, CultureInfo.InvariantCulture, out double value) || double.IsNaN(value) || double.IsInfinity(value))) return false;
                }
            }
            const string original = "\"m_HDR\":false";
            int offset = before.IndexOf(original, StringComparison.Ordinal);
            return offset >= 0 && before.LastIndexOf(original, StringComparison.Ordinal) == offset && after == before.Substring(0, offset) + "\"m_HDR\":true" + before.Substring(offset + original.Length);
        }

        /// <summary>只有真实后验全部通过才签发本次 current 输入捕获许可，不能按后来 current伪造 scopeAfter。</summary>
        /// <param name="before">原始完整计划。</param>
        /// <param name="after">真实紧邻 scope后态。</param>
        /// <param name="current">此次实际完整观测。</param>
        /// <param name="scopeError">当时捕获失败时保留的错误；不忽略或补值。</param>
        /// <returns>仅本次准确输入捕获的许可。</returns>
        internal static ValidatedCapture ApproveAfter(Snapshot before, Snapshot after, Snapshot current, string scopeError)
        {
            return ValidatedCapture.After(before, after, current, scopeError);
        }

        /// <summary>只把准确 SVS dirty行转为显式历史受保护基线，所有其它行原样保留；基线不冒称当前 JSON。</summary>
        /// <param name="rows">本次实际全部持久 dirty行。</param>
        /// <param name="approved">严格前置/后置校验取得的短命许可。</param>
        /// <returns>保留其它行与顺序、追加准确标记的输入行。</returns>
        internal static string[] ProjectDirtyRows(string[] rows, ValidatedCapture approved)
        {
            if (rows == null || approved == null) throw new ArgumentNullException(rows == null ? nameof(rows) : nameof(approved));
            string prefix = "PreservedDirtyObject=" + approved.observed.dirtyIdentity + "|";
            string[] matching = rows.Where(row => row != null && row.StartsWith("PreservedDirtyObject=" + StateType + "|", StringComparison.Ordinal)).ToArray();
            if (approved.observed.dirty ? matching.Length != 1 || matching[0] != prefix + approved.observed.body.rawSha256 : matching.Length != 0) throw new InvalidOperationException("Generate SVS dirty行不是当前准确原生对象和完整 JSON SHA。");
            return rows.Where(row => !matching.Contains(row, StringComparer.Ordinal)).Concat(new[] { "GenerateSVSProtectedBaseline=" + approved.baseline.dirtyIdentity + "|" + approved.baseline.body.rawSha256 + "|originalDirty=" + approved.baseline.dirty.ToString() }).ToArray();
        }

        /// <summary>只接受原行完整保留的有序超序列；新增只能零GUID、非零Int64句柄、两空数组。</summary>
        /// <param name="before">完整前态解析树。</param>
        /// <param name="after">完整实际后态解析树。</param>
        /// <param name="allowedHandles">重载或公开查询时唯一已证当前Preview句柄；Generate同步跨度为null。</param>
        /// <param name="allowedTable">有限跨度准确可新增表名；Generate同步跨度为null。</param>
        private static void ValidateAppend(JObject before, JObject after, HashSet<long> allowedHandles, string allowedTable)
        {
            foreach (string table in Tables)
            {
                JArray old = (JArray)before["SceneVisibilityState"][table]["m_SceneData"], next = (JArray)after["SceneVisibilityState"][table]["m_SceneData"];
                int cursor = 0;
                foreach (JToken row in next)
                {
                    if (cursor < old.Count && JToken.DeepEquals(row, old[cursor])) { cursor++; continue; }
                    if (old.Any(value => JToken.DeepEquals(value["first"], row["first"]))) throw new InvalidOperationException("Generate SVS 原行改值或重排。");
                    if ((string)row["first"]["guid"] != ZeroGuid || (long)row["first"]["handle"]["value"] == 0 || allowedTable != null && (table != allowedTable || allowedHandles == null || !allowedHandles.Contains((long)row["first"]["handle"]["value"]))) throw new InvalidOperationException("Generate SVS 新增行不是零GUID非零64位handle空缓存。");
                }
                if (cursor != old.Count) throw new InvalidOperationException("Generate SVS 原行删除或顺序改变。");
            }
        }

        /// <summary>同进程原native身份与完整公开场景观测相等；跨脚本域不声称托管引用连续。</summary>
        /// <param name="before">先前实际材料。</param>
        /// <param name="current">当前实际材料。</param>
        /// <returns>原生ID/GlobalID/进程和实际四标记均一致，同域还需真实弱引用同对象。</returns>
        private static bool NativeIdentityEquals(Snapshot before, Snapshot current)
        {
            bool references = before.domain != current.domain || current.domain != Domain ||
                SameWitness("state", before.stateId, before.stateReference, current.stateReference) && SameWitness("manager", before.managerId, before.managerReference, current.managerReference) &&
                SameWitness("stageManager", before.stageManagerId, before.stageManagerReference, current.stageManagerReference) && SameWitness("mainStage", before.mainStageId, before.mainStageReference, current.mainStageReference);
            return references && before.process == current.process && before.coreSha256 == current.coreSha256 && before.coreMvid == current.coreMvid && before.stateId == current.stateId && before.stateGlobalId == current.stateGlobalId && before.dirtyIdentity == current.dirtyIdentity && before.managerId == current.managerId && before.managerGlobalId == current.managerGlobalId && before.stageManagerId == current.stageManagerId && before.mainStageId == current.mainStageId && before.visibilityEnabled == current.visibilityEnabled && before.pickingEnabled == current.pickingEnabled && before.isolated == current.isolated && before.activeScene == current.activeScene && Same(before.scenes, current.scenes) && Same(before.objects, current.objects) && Same(before.owners, current.owners) && Same(before.invalidObjects, current.invalidObjects);
        }

        /// <summary>只保存既存对象的弱见证；同域同ID换实例或见证失效时拒绝，不延长Native存活。</summary>
        /// <param name="value">本次自然既存对象。</param>
        /// <param name="role">固定state或manager角色。</param>
        private static void RememberWitness(Object value, string role)
        {
            string key = role + "|" + UnityEngine.EntityId.ToULong(value.GetEntityId()).ToString(CultureInfo.InvariantCulture);
            if (Witnesses.TryGetValue(key, out WeakReference witness))
            { if (witness.Target == null || !ReferenceEquals(witness.Target, value)) throw new InvalidOperationException("Generate SVS 同域原生ID的托管见证失效或替换。"); }
            else { if (Witnesses.Count >= 64) throw new InvalidOperationException("Generate SVS 弱见证容量已满；不清理或重建原生对象。"); Witnesses.Add(key, new WeakReference(value)); }
        }

        /// <summary>完整材料经存储反序列化后仍借原域弱见证核真正引用；跨域由Native身份合同独立负责。</summary>
        /// <param name="role">固定原生角色。</param>
        /// <param name="id">原Native ID。</param>
        /// <param name="before">尚在内存时的原弱引用，可因JSON重读为空。</param>
        /// <param name="current">当前Capture弱引用，历史证据可为空。</param>
        /// <returns>同域原见证仍活且所有现有引用均为该对象时为true。</returns>
        private static bool SameWitness(string role, ulong id, WeakReference before, WeakReference current)
        {
            if (!Witnesses.TryGetValue(role + "|" + id.ToString(CultureInfo.InvariantCulture), out WeakReference witness) || witness.Target == null) return false;
            return (before == null || ReferenceEquals(witness.Target, before.Target)) && (current == null || ReferenceEquals(witness.Target, current.Target));
        }

        /// <summary>严格固定属性名集合；缺失、多余、null对象均拒绝。</summary>
        /// <param name="value">当前 JSON对象。</param>
        /// <param name="keys">唯一许可字段。</param>
        private static void RequireKeys(JObject value, params string[] keys)
        {
            if (value == null || !value.Properties().Select(property => property.Name).OrderBy(key => key, StringComparer.Ordinal).SequenceEqual(keys.OrderBy(key => key, StringComparer.Ordinal), StringComparer.Ordinal)) throw new InvalidOperationException("Generate SVS JSON 字段结构未知。");
        }

        /// <summary>验证完整小写SHA256元数据，不把空摘要当作完整正文。</summary>
        /// <param name="value">完整摘要。</param>
        /// <returns>准确64位十六进制时为true。</returns>
        private static bool IsSha256(string value) => value != null && value.Length == 64 && value.All(character => character >= '0' && character <= '9' || character >= 'a' && character <= 'f');

        /// <summary>验证原生GUID准确小写32位格式。</summary>
        /// <param name="value">完整GUID。</param>
        /// <returns>格式准确时为 true。</returns>
        private static bool ValidGuid(string value) => value != null && value.Length == 32 && value.All(character => character >= '0' && character <= '9' || character >= 'a' && character <= 'f');

        /// <summary>规范化对象key顺序但完整保留数组顺序和标量，不删除任何字段。</summary>
        /// <param name="value">严格解析后的完整树。</param>
        /// <returns>确定性完整 JSON。</returns>
        private static string Canonical(JToken value) => (value is JObject obj ? new JObject(obj.Properties().OrderBy(property => property.Name, StringComparer.Ordinal).Select(property => new JProperty(property.Name, JToken.Parse(Canonical(property.Value))))) : value is JArray array ? new JArray(array.Select(child => JToken.Parse(Canonical(child)))) : value.DeepClone()).ToString(Formatting.None);

        /// <summary>完整 DTO 值比较；不会投影某个未审字段。</summary>
        /// <param name="left">前值。</param>
        /// <param name="right">后值。</param>
        /// <returns>全部序列化字段相等时为 true。</returns>
        private static bool Same(object left, object right) => JsonConvert.SerializeObject(left, Formatting.None) == JsonConvert.SerializeObject(right, Formatting.None);

        /// <summary>只读流计算精确文件SHA；不修改该文件。</summary>
        /// <param name="path">固定实际文件。</param>
        /// <returns>小写SHA256。</returns>
        private static string HashFile(string path) { using (var input = File.OpenRead(path)) using (var sha = SHA256.Create()) return BitConverter.ToString(sha.ComputeHash(input)).Replace("-", "").ToLowerInvariant(); }

        /// <summary>完整字节计算SHA，不使用截断材料。</summary>
        /// <param name="bytes">完整字节。</param>
        /// <returns>小写SHA256。</returns>
        private static string Hash(byte[] bytes) { using (var sha = SHA256.Create()) return BitConverter.ToString(sha.ComputeHash(bytes)).Replace("-", "").ToLowerInvariant(); }

        /// <summary>准确既存Source owner与其有限Native子对象关系；保存字段，不调用创建或生命周期方法。</summary>
        [Serializable]
        internal sealed class OwnerRow
        {
            public string role;
            public ulong id;
            public string globalId;
            public string type;
            public int hideFlags;
            public string key;
            public string previewHandle;
            public ulong cameraId;
            public ulong cameraGoId;
            public ulong gizmoCameraId;
            public ulong gizmoCameraGoId;
            public ulong[] lightIds;
            public ulong[] lightGoIds;
            public ulong helperGoId;
        }

        /// <summary>无有效场景的既存对象；完整GO与全部组件原文只作证据，不调用业务或SDK行为。</summary>
        [Serializable]
        internal sealed class InvalidRow
        {
            public string role;
            public string ownerKey;
            public ulong id;
            public string globalId;
            public string name;
            public int hideFlags;
            public string json;
            public bool hidden;
            public bool hiddenSubtree;
            public bool pickingDisabled;
            public bool pickingDisabledSubtree;
            public PreviewComponent[] components;
        }

        /// <summary>统一无损压缩的完整场景、owner、查询跨度材料；不会以摘要代替Native原文。</summary>
        [Serializable]
        internal sealed class Observation
        {
            public SceneRow[] scenes;
            public ObjectRow[] objects;
            public OwnerRow[] owners;
            public InvalidRow[] invalidObjects;
            public Body queryBeforeBody;
            public bool queryBeforeDirty;
            public CacheFile queryBeforeCache;
        }

        /// <summary>由本次真实CompilationState首次域校验生成的短命证明，外部DTO不能反序列化获得许可。</summary>
        internal sealed class ReloadProof
        {
            internal readonly string domain;
            internal readonly GeneratedExportCompilationState.ReloadDomainEvidence evidence;
            /// <summary>只有真实SessionState新协议门通过后才能构造；DTO本身不提供许可。</summary>
            /// <param name="evidence">此次实际读取的完整编译证明。</param>
            private ReloadProof(GeneratedExportCompilationState.ReloadDomainEvidence evidence) { domain = Domain; this.evidence = evidence; }
            /// <summary>重核当前SessionState，不修改令牌、域或编译状态。</summary>
            /// <param name="token">原Receipt编译令牌。</param><param name="payloadHash">原Receipt载荷SHA。</param>
            /// <returns>只属于当前首次正式重载域的短命证明。</returns>
            internal static ReloadProof Verify(string token, string payloadHash)
            {
                if (!GeneratedExportCompilationState.TryCaptureCurrentReloadRecord(token, payloadHash, out GeneratedExportCompilationState.ReloadDomainEvidence evidence, out string error)) throw new InvalidOperationException(error);
                return new ReloadProof(evidence);
            }
        }

        /// <summary>只持有本域首次完整实际观察的自有字符串；不延长Native对象生命周期。</summary>
        private sealed class FirstReloadWitness
        {
            internal string key;
            internal string firstCurrentJson;
            internal int captureCount;
        }

        /// <summary>正式首次域的独立完整后验，仍需绑定原before/final指针并随真实Verify返回归档。</summary>
        [Serializable]
        internal sealed class CompilationObservation
        {
            public int version = 1;
            public GeneratedExportCompilationState.ReloadDomainEvidence compilation;
            public string scopeAfterSnapshotSha256;
            public Snapshot firstCurrent;
            public Snapshot current;
            public bool repeatedInSameDomain;
            public bool adapterVerified;
            public string adapterError;
            public HybridClrGenerateEvidenceStore.Reference sourceReference;
            public string certificate;
        }

        /// <summary>完整Scope材料与本次实际编译记录共同形成短命witness键，不能借另一Receipt或较晚域。</summary>
        /// <param name="after">原真实scope-after全部DTO。</param><param name="proof">已通过本次真实首次域门的许可。</param>
        /// <returns>准确载荷绑定SHA。</returns>
        private static string ReloadWitnessKey(Snapshot after, ReloadProof proof)
        {
            after = ExpandSnapshot(after);
            return Hash(Utf8.GetBytes(JsonConvert.SerializeObject(new { after, proof.evidence.record.token, proof.evidence.record.payloadHash, proof.evidence.record.scheduleNonce, proof.evidence.record.scheduledDomain, proof.evidence.record.firstReloadDomain }, Formatting.None)));
        }

        /// <summary>首个当前观察执行严格P→V转换；本域重复观察只许该首个post-query完整精确保持。</summary>
        /// <param name="after">原真实scope-after。</param><param name="current">本次实际完整观察。</param><param name="proof">真实首次域许可。</param>
        private static void ValidateFirstOrRepeatedReload(Snapshot after, Snapshot current, ReloadProof proof)
        {
            ValidateFirstOrRepeatedReloadFromObserved(after, after, current, proof);
        }

        /// <summary>复用同一个首次域见证槽；key仍绑定原scopeAfter，仅已复验原Observed可作为重载比较基线。</summary>
        /// <param name="after">不可改写的原scopeAfter。</param><param name="reloadBaseline">严格复验三态后的原稳定材料，旧入口传原after。</param><param name="current">真实新域当前观察。</param><param name="proof">本次正式首次域证明。</param>
        private static void ValidateFirstOrRepeatedReloadFromObserved(Snapshot after, Snapshot reloadBaseline, Snapshot current, ReloadProof proof)
        {
            string key = ReloadWitnessKey(after, proof);
            if (s_FirstReloadWitness == null)
            {
                if (!TryValidateReloadMaterial(reloadBaseline, current, out string error)) throw new InvalidOperationException(error);
                Snapshot frozen = JsonConvert.DeserializeObject<Snapshot>(JsonConvert.SerializeObject(ExpandSnapshot(current), Formatting.None)); PackSnapshot(frozen);
                s_FirstReloadWitness = new FirstReloadWitness { key = key, firstCurrentJson = JsonConvert.SerializeObject(frozen, Formatting.None), captureCount = 1 };
                return;
            }
            if (s_FirstReloadWitness.key != key) throw new InvalidOperationException("Generate SVS 本域已有不同Receipt/Scope的首次观察，不能替换。");
            Snapshot first = JsonConvert.DeserializeObject<Snapshot>(s_FirstReloadWitness.firstCurrentJson);
            if (!BeforeEquals(first, current)) throw new InvalidOperationException("Generate SVS 本域重复观察相对首次真实post-query发生完整变化，保持partial。");
            checked { s_FirstReloadWitness.captureCount++; }
        }

        /// <summary>复制完整首次与当前观察供真实Verify结果保留；不回写历史final或SessionState。</summary>
        /// <param name="after">原Scope完整材料。</param><param name="current">本次完整观察。</param><param name="proof">当前真实首次域证明。</param>
        /// <returns>未加原文件指针和最终证书的内部后验DTO。</returns>
        private static CompilationObservation BuildCompilationObservation(Snapshot after, Snapshot current, ReloadProof proof)
        {
            if (s_FirstReloadWitness == null || s_FirstReloadWitness.key != ReloadWitnessKey(after, proof)) throw new InvalidOperationException("Generate SVS 缺少本域首次实际观察。");
            Snapshot first = JsonConvert.DeserializeObject<Snapshot>(s_FirstReloadWitness.firstCurrentJson);
            return new CompilationObservation { compilation = proof.evidence, scopeAfterSnapshotSha256 = Hash(Utf8.GetBytes(JsonConvert.SerializeObject(after, Formatting.None))), firstCurrent = first, current = current, repeatedInSameDomain = s_FirstReloadWitness.captureCount > 1 };
        }

        /// <summary>将实际后验与原正式before/final完整指针共同绑定，保持原DataJson资产数组兼容。</summary>
        /// <param name="value">真实新域许可生成的完整后验。</param><param name="reference">原Receipt且Store已真实核验的不可变指针。</param>
        /// <returns>有界完整Evidence正文与SHA证书；调用者必须归档实际工具返回，journal不存正文。</returns>
        internal static string EncodeCompilationObservation(CompilationObservation value, HybridClrGenerateEvidenceStore.Reference reference)
        {
            if (value == null || value.version != 1 || value.compilation?.record == null || !value.compilation.record.IsCurrentFirstReload(value.compilation.record.token, value.compilation.record.payloadHash, value.compilation.currentDomain, value.compilation.observedUtcTicks) || !IsSha256(value.scopeAfterSnapshotSha256) || value.firstCurrent == null || value.current == null || value.firstCurrent.domain != value.current.domain || reference == null || reference.version != 1 || !Guid.TryParseExact(reference.id, "N", out _) || !reference.beforeWritten || !reference.finalWritten || reference.beforeLength < 1 || reference.beforeLength > HybridClrGenerateEvidenceStore.MaxRawBytes || reference.finalLength < 1 || reference.finalLength > HybridClrGenerateEvidenceStore.MaxRawBytes || !IsSha256(reference.beforeSha256) || !IsSha256(reference.finalSha256)) throw new InvalidOperationException("Generate SVS 正式后验或原文件指针缺失。");
            ValidateSnapshot(value.firstCurrent); ValidateSnapshot(value.current);
            var copy = JsonConvert.DeserializeObject<CompilationObservation>(JsonConvert.SerializeObject(value, Formatting.None));
            copy.sourceReference = JsonConvert.DeserializeObject<HybridClrGenerateEvidenceStore.Reference>(JsonConvert.SerializeObject(reference, Formatting.None)); copy.certificate = null;
            copy.certificate = Hash(Utf8.GetBytes(JsonConvert.SerializeObject(copy, Formatting.None)));
            string json = JsonConvert.SerializeObject(copy, Formatting.None);
            if (Utf8.GetByteCount(json) > 160 * 1024) throw new InvalidOperationException("Generate SVS 完整Verify后验超过160KiB，不截断材料或扩大Store。");
            return JsonConvert.SerializeObject(new CompilationEnvelope { codec = "nova-generate-svs-postcompile-gzip-v1", body = EncodePayload(json, false, typeof(CompilationObservation)) }, Formatting.None);
        }

        /// <summary>仅安全ASCII字段承载完整无损Verify正文，避免Gateway路径脱敏破坏原文SHA。</summary>
        [Serializable]
        internal sealed class CompilationEnvelope
        {
            public string codec;
            public Body body;
        }

        /// <summary>只读解码已返回的完整后验供归档核验，不产生执行许可或改写历史。</summary>
        /// <param name="json">单条Evidence去掉固定前缀后的完整信封。</param>
        /// <returns>原SHA与内部证书均精确的完整DTO；仍须另核原文件指针。</returns>
        internal static CompilationObservation DecodeCompilationObservation(string json)
        {
            if (json == null || Utf8.GetByteCount(json) > 64 * 1024) throw new InvalidOperationException("Generate SVS Verify信封越界。");
            var envelope = ParseObservation(json, typeof(CompilationEnvelope)).ToObject<CompilationEnvelope>();
            if (envelope.codec != "nova-generate-svs-postcompile-gzip-v1") throw new InvalidOperationException("Generate SVS Verify codec未审。");
            JObject tree = DecodePayload(envelope.body, false, typeof(CompilationObservation));
            if (envelope.body.rawLength > 160 * 1024) throw new InvalidOperationException("Generate SVS Verify完整正文越界。");
            var value = tree.ToObject<CompilationObservation>(); string certificate = value.certificate; value.certificate = null;
            if (!IsSha256(certificate) || Hash(Utf8.GetBytes(JsonConvert.SerializeObject(value, Formatting.None))) != certificate) throw new InvalidOperationException("Generate SVS Verify内部证书不符。");
            value.certificate = certificate; ValidateSnapshot(value.firstCurrent); ValidateSnapshot(value.current); return value;
        }

        /// <summary>正式Generate Verify专用入口；普通调用仍使用不接受跨域的旧入口。</summary>
        /// <param name="before">原真实计划。</param><param name="after">原真实Scope后态。</param><param name="current">此次实际完整观测。</param><param name="token">本次真实编译令牌。</param><param name="payloadHash">本次原Receipt载荷SHA。</param><param name="error">准确错误。</param>
        /// <returns>实际同域精确或本次首次正式重载有限转换通过时为真。</returns>
        internal static bool TryValidateAfterCompilation(Snapshot before, Snapshot after, Snapshot current, string token, string payloadHash, out string error)
        {
            try { ValidatedCapture.AfterCompilation(before, after, current, token, payloadHash); error = null; return true; }
            catch (Exception exception) { error = "Generate SVS 正式重载证明失败：" + exception.Message; return false; }
        }

        /// <summary>正式编译后的当前输入许可；保留原历史Scope，不把较晚current重写为Scope材料。</summary>
        /// <param name="before">原完整计划。</param><param name="after">真实Scope后态。</param><param name="current">当前真实观测。</param><param name="scopeError">原捕获错误。</param><param name="token">原Receipt令牌。</param><param name="payloadHash">原载荷SHA。</param>
        /// <returns>完整校验后的短命投影许可。</returns>
        internal static ValidatedCapture ApproveAfterCompilation(Snapshot before, Snapshot after, Snapshot current, string scopeError, string token, string payloadHash)
        {
            if (scopeError != null) throw new InvalidOperationException("Generate SVS 原Scope失败：" + scopeError);
            return ValidatedCapture.AfterCompilation(before, after, current, token, payloadHash);
        }

        /// <summary>正式Verify唯一Observed入口；消费原证书已绑定的真实三态，不改旧缺Observed入口的跨域行为。</summary>
        /// <param name="before">原完整计划。</param><param name="after">原真实scopeAfter。</param><param name="originalObserved">原Final中已签完整稳定后态。</param><param name="current">本次真实当前观察。</param><param name="scopeError">原捕获错误。</param><param name="token">原编译令牌。</param><param name="payloadHash">原载荷SHA。</param>
        /// <returns>全部严格历史与当前域验证通过后的短命许可。</returns>
        internal static ValidatedCapture ApproveAfterCompilationObserved(Snapshot before, Snapshot after, Snapshot originalObserved, Snapshot current, string scopeError, string token, string payloadHash)
        {
            if (scopeError != null) throw new InvalidOperationException("Generate SVS 原Scope失败：" + scopeError);
            return ValidatedCapture.AfterCompilationObserved(before, after, originalObserved, current, token, payloadHash);
        }

        /// <summary>将实际完整观察统一折叠为有界无损gzip；保留外层Native原文和准确Library身份。</summary>
        /// <param name="snapshot">刚完成校验的实际材料；仅改变自有DTO。</param>
        internal static void PackSnapshot(Snapshot snapshot)
        {
            if (snapshot == null || snapshot.observation != null) throw new InvalidOperationException("Generate SVS 观察材料不能重复封装。");
            var observation = new Observation { scenes = snapshot.scenes, objects = snapshot.objects, owners = snapshot.owners, invalidObjects = snapshot.invalidObjects, queryBeforeBody = snapshot.queryBeforeBody, queryBeforeDirty = snapshot.queryBeforeDirty, queryBeforeCache = snapshot.queryBeforeCache };
            snapshot.observation = EncodePayload(JsonConvert.SerializeObject(observation, Formatting.None), false);
            snapshot.scenes = null; snapshot.objects = null; snapshot.owners = null; snapshot.invalidObjects = null; snapshot.queryBeforeBody = null; snapshot.queryBeforeCache = null; snapshot.queryBeforeDirty = false;
            if (Utf8.GetByteCount(JsonConvert.SerializeObject(snapshot, Formatting.None)) > MaxSnapshotBytes) throw new InvalidOperationException("Generate SVS 完整材料超过96KiB，不截断或提高8MiB总上限。");
        }

        /// <summary>完整展开自己的短命DTO副本，历史压缩材料不被修改；拒混合字段或旧缺失材料。</summary>
        /// <param name="value">完整wire或完整inline DTO。</param>
        /// <returns>可逐字段校验的完整观察副本。</returns>
        internal static Snapshot ExpandSnapshot(Snapshot value)
        {
            if (value == null || value.observation == null) return value;
            if (value.scenes != null || value.objects != null || value.owners != null || value.invalidObjects != null || value.queryBeforeBody != null || value.queryBeforeCache != null || value.queryBeforeDirty) throw new InvalidOperationException("Generate SVS 压缩与inline观察混合，拒绝隐藏材料。");
            if (Utf8.GetByteCount(JsonConvert.SerializeObject(value, Formatting.None)) > MaxSnapshotBytes) throw new InvalidOperationException("Generate SVS wire材料越界。");
            var actual = DecodePayload(value.observation, false).ToObject<Observation>();
            var copy = (Snapshot)typeof(object).GetMethod("MemberwiseClone", BindingFlags.NonPublic | BindingFlags.Instance).Invoke(value, null);
            copy.observation = null; copy.scenes = actual.scenes; copy.objects = actual.objects; copy.owners = actual.owners; copy.invalidObjects = actual.invalidObjects; copy.queryBeforeBody = actual.queryBeforeBody; copy.queryBeforeDirty = actual.queryBeforeDirty; copy.queryBeforeCache = actual.queryBeforeCache;
            return copy;
        }

        /// <summary>严格观察信封解析；全部字段必须存在，未知字段、注释、重复键及尾随内容拒绝。</summary>
        /// <param name="json">完整无损观察原文。</param>
        /// <param name="observationType">已审准确DTO，默认原观察信封。</param>
        /// <returns>全字段树，Native组件原文仍为原始字符串。</returns>
        private static JObject ParseObservation(string json, Type observationType = null)
        {
            using (var input = new StringReader(json)) using (var scan = new JsonTextReader(input) { DateParseHandling = DateParseHandling.None, MaxDepth = 64 })
                while (scan.Read()) if (scan.TokenType == JsonToken.Comment) throw new InvalidOperationException("Generate SVS 观察含未知注释。");
            JObject root;
            using (var input = new StringReader(json)) using (var reader = new JsonTextReader(input) { DateParseHandling = DateParseHandling.None, MaxDepth = 64 })
            {
                root = JObject.Load(reader, new JsonLoadSettings { DuplicatePropertyNameHandling = DuplicatePropertyNameHandling.Error, CommentHandling = CommentHandling.Load });
                if (reader.Read()) throw new InvalidOperationException("Generate SVS 观察有尾随材料。");
            }
            ValidateDtoShape(root, observationType ?? typeof(Observation)); return root;
        }

        /// <summary>按固定自有DTO逐字段/数组/标量核完整结构，不对未知类型作反射式放行。</summary>
        /// <param name="token">实际完整树。</param><param name="type">本合同固定DTO或标量类型。</param>
        private static void ValidateDtoShape(JToken token, Type type)
        {
            if (token == null) throw new InvalidOperationException("Generate SVS 观察字段缺失。");
            if (token.Type == JTokenType.Null) { if (type.IsValueType) throw new InvalidOperationException("Generate SVS 值字段为null。"); return; }
            if (type == typeof(string)) { if (token.Type != JTokenType.String) throw new InvalidOperationException("Generate SVS 文本类型未知。"); return; }
            if (type == typeof(bool)) { if (token.Type != JTokenType.Boolean) throw new InvalidOperationException("Generate SVS 布尔类型未知。"); return; }
            if (type == typeof(int) || type == typeof(long)) { if (token.Type != JTokenType.Integer || type == typeof(int) && !int.TryParse(token.ToString(), out _) || type == typeof(long) && !long.TryParse(token.ToString(), out _)) throw new InvalidOperationException("Generate SVS 整数类型越界。"); return; }
            if (type == typeof(ulong)) { if (token.Type != JTokenType.Integer || !ulong.TryParse(token.ToString(), NumberStyles.None, CultureInfo.InvariantCulture, out _)) throw new InvalidOperationException("Generate SVS 64位对象身份越界。"); return; }
            if (type.IsArray) { if (!(token is JArray array) || array.Count > 8192) throw new InvalidOperationException("Generate SVS 观察数组越界。"); foreach (JToken child in array) ValidateDtoShape(child, type.GetElementType()); return; }
            if (type != typeof(Observation) && type != typeof(SceneRow) && type != typeof(ObjectRow) && type != typeof(OwnerRow) && type != typeof(InvalidRow) && type != typeof(PreviewComponent) && type != typeof(Body) && type != typeof(CacheFile) && type != typeof(Snapshot) && type != typeof(CompilationObservation) && type != typeof(CompilationEnvelope) && type != typeof(GeneratedExportCompilationState.ReloadDomainEvidence) && type != typeof(GeneratedExportCompilationState.Record) && type != typeof(HybridClrGenerateEvidenceStore.Reference)) throw new InvalidOperationException("Generate SVS DTO类型未审。");
            FieldInfo[] fields = type.GetFields(BindingFlags.Instance | BindingFlags.Public); RequireKeys(token as JObject, fields.Select(field => field.Name).ToArray());
            foreach (FieldInfo field in fields) ValidateDtoShape(token[field.Name], field.FieldType);
        }

        /// <summary>只读准确既存owner字段，不调用属性、构造或生命周期回调；字段缺失明确拒绝。</summary>
        /// <param name="value">实际已存在实例。</param><param name="name">已审准确字段。</param>
        /// <returns>实际backing值。</returns>
        private static object ReadField(object value, string name)
        {
            if (value == null) throw new InvalidOperationException("Generate SVS owner不存在。");
            FieldInfo field = value.GetType().GetField(name, BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
            if (field == null) throw new InvalidOperationException("Generate SVS owner字段缺失：" + name);
            return field.GetValue(value);
        }

        /// <summary>冻结实际既存SceneView及2D owner，不打开窗口、不创建helper或单例。</summary>
        /// <param name="core">已核指纹的EditorCore。</param><param name="inventory">实际完整非持久对象集合。</param>
        /// <returns>准确owner与子实例关系。</returns>
        private static OwnerRow[] CaptureOwners(Assembly core, GameObject[] inventory)
        {
            var rows = new List<OwnerRow>();
            foreach (SceneView view in Resources.FindObjectsOfTypeAll<SceneView>().Where(value => value != null).OrderBy(value => UnityEngine.EntityId.ToULong(value.GetEntityId())))
            {
                if (view.GetType() != typeof(SceneView)) throw new InvalidOperationException("Generate SVS 未审SceneView派生owner。");
                var camera = ReadField(view, "m_Camera") as Camera; var lights = ReadField(view, "m_Light") as Light[]; var custom = (Scene)ReadField(view, "m_CustomLightsScene"); object gizmo = ReadField(view, "m_OrientationGizmo");
                if (gizmo == null || gizmo.GetType().Assembly != core || gizmo.GetType().FullName != "SceneOrientationGizmo") throw new InvalidOperationException("Generate SVS 精确gizmo owner未自然初始化。");
                var gizmoCamera = ReadField(gizmo, "m_Camera") as Camera; string key = ReadField(view, "m_WindowGUID") as string;
                if (camera == null || gizmoCamera == null || lights == null || lights.Length != 3 || lights.Any(light => light == null) || string.IsNullOrEmpty(key) || !custom.IsValid() || !custom.isLoaded || !EditorSceneManager.IsPreviewScene(custom) || custom.name != "CustomLightsScene-SceneView" + key) throw new InvalidOperationException("Generate SVS SceneView有限source-owner关系不完整。");
                rows.Add(new OwnerRow { role = "SceneView", id = UnityEngine.EntityId.ToULong(view.GetEntityId()), globalId = GlobalObjectId.GetGlobalObjectIdSlow(view).ToString(), type = view.GetType().AssemblyQualifiedName, hideFlags = (int)view.hideFlags, key = key, previewHandle = custom.handle.GetRawData().ToString(CultureInfo.InvariantCulture), cameraId = UnityEngine.EntityId.ToULong(camera.GetEntityId()), cameraGoId = UnityEngine.EntityId.ToULong(camera.gameObject.GetEntityId()), gizmoCameraId = UnityEngine.EntityId.ToULong(gizmoCamera.GetEntityId()), gizmoCameraGoId = UnityEngine.EntityId.ToULong(gizmoCamera.gameObject.GetEntityId()), lightIds = lights.Select(light => UnityEngine.EntityId.ToULong(light.GetEntityId())).ToArray(), lightGoIds = lights.Select(light => UnityEngine.EntityId.ToULong(light.gameObject.GetEntityId())).ToArray() });
            }
            Component[] updaters = inventory.SelectMany(value => value.GetComponents<Component>()).Where(component => component != null && component.GetType().FullName == "UnityEngine.U2D.Animation.DeformationManagerUpdater").ToArray();
            if (updaters.Length != 0)
            {
                var pins = new Dictionary<string, string>(StringComparer.Ordinal);
                CheckFile(updaters[0].GetType().Assembly.Location, "bc8c905a443557b0a19f45732c684b3d5a910e777baa1d714c64ab8fc0e60cf6", pins);
                if (updaters[0].GetType().Assembly.ManifestModule.ModuleVersionId.ToString() != "1011aa73-cd92-4656-a2f8-71728e4998cf") throw new InvalidOperationException("Generate SVS 2D owner程序集未审。");
                CheckSource("Library/PackageCache/com.unity.2d.animation@011d329ac58b/Runtime/BatchedDeformation/DeformationManager.cs", "e544add9d19099325cf9a779d906a77be5b0b8d49ca493875a3a458e0f56d62e", pins);
                Type managerType = updaters[0].GetType().Assembly.GetType("UnityEngine.U2D.Animation.DeformationManager", true, false);
                Object[] managers = Resources.FindObjectsOfTypeAll(managerType).Where(value => value != null && value.GetType() == managerType).ToArray();
                FieldInfo instance = managerType.GetField("s_Instance", BindingFlags.Static | BindingFlags.NonPublic);
                if (managerType.TypeInitializer != null || managers.Length != 1 || instance == null || !ReferenceEquals(instance.GetValue(null), managers[0]) || updaters.Length != 1) throw new InvalidOperationException("Generate SVS 2D owner非唯一自然既存backing。");
                Object owner = managers[0]; var helper = ReadField(owner, "m_Helper") as GameObject;
                if (helper == null || helper != updaters[0].gameObject) throw new InvalidOperationException("Generate SVS 2D helper准确owner不匹配。");
                rows.Add(new OwnerRow { role = "DeformationManager", id = UnityEngine.EntityId.ToULong(owner.GetEntityId()), globalId = GlobalObjectId.GetGlobalObjectIdSlow(owner).ToString(), type = owner.GetType().AssemblyQualifiedName, hideFlags = (int)owner.hideFlags, key = "DeformationManager", helperGoId = UnityEngine.EntityId.ToULong(helper.GetEntityId()) });
            }
            return rows.OrderBy(row => row.role, StringComparer.Ordinal).ThenBy(row => row.id).ToArray();
        }

        /// <summary>精确Native组件与少量已审无序列化回调的厂商组件全正文；不读取SDK设置/静态任务/账号。</summary>
        /// <param name="component">实际既存组件。</param><param name="files">本次局部文件SHA memo，仅保存字符串。</param>
        /// <returns>完整Native原文、类型、指纹及身份。</returns>
        private static PreviewComponent CaptureComponent(Component component, Dictionary<string, string> files)
        {
            if (component == null || component is ISerializationCallbackReceiver) throw new InvalidOperationException("Generate SVS missing或serialization-callback组件未审。");
            Type type = component.GetType(); string sha, mvid; Assembly assembly = type.Assembly;
            if (type == typeof(Transform) || type == typeof(Camera) || type == typeof(FlareLayer) || type == typeof(Light)) { sha = RuntimeCoreSha256; mvid = "e816434e-022e-42a6-bec8-8f93bd4bf5e5"; }
            else if (type.FullName == "UnityEngine.U2D.Animation.DeformationManagerUpdater") { sha = "bc8c905a443557b0a19f45732c684b3d5a910e777baa1d714c64ab8fc0e60cf6"; mvid = "1011aa73-cd92-4656-a2f8-71728e4998cf"; CheckSource("Library/PackageCache/com.unity.2d.animation@011d329ac58b/Runtime/BatchedDeformation/DeformationManagerUpdater.cs", "8cf0487f200cc3a915a4b9b262a853a073d79d2b6439f41da2d4d5489527d013", files); CheckSource("Library/PackageCache/com.unity.2d.animation@011d329ac58b/Runtime/BatchedDeformation/DeformationManager.cs", "e544add9d19099325cf9a779d906a77be5b0b8d49ca493875a3a458e0f56d62e", files); }
            else if (type.FullName == "AppLovinMax.Internal.MaxEventExecutor") { sha = "0fe4f91289157c65eafffb205149331c53f655708025ec6de79d5ae3f5fdcc99"; mvid = "513ee476-abfa-44ff-8b3f-83b2706b521f"; CheckSource("Library/PackageCache/com.applovin.mediation.ads@6c8fcb4e757f/Scripts/MaxEventExecutor.cs", "337206eba39aee0f3410c53332dc17b2b108c733c70a98f457afb7a696446aea", files); }
            else throw new InvalidOperationException("Generate SVS 未审无scene组件类型：" + type.FullName);
            CheckFile(assembly.Location, sha, files);
            if (assembly.ManifestModule.ModuleVersionId.ToString() != mvid) throw new InvalidOperationException("Generate SVS 组件MVID改变。");
            string json = EditorJsonUtility.ToJson(component);
            return new PreviewComponent { id = UnityEngine.EntityId.ToULong(component.GetEntityId()), globalId = GlobalObjectId.GetGlobalObjectIdSlow(component).ToString(), type = type.AssemblyQualifiedName, hideFlags = (int)component.hideFlags, json = json, jsonSha256 = Hash(Utf8.GetBytes(json)), assemblySha256 = sha, assemblyMvid = mvid };
        }

        /// <summary>精确项目内已审源指纹；只读，不下载或替换PackageCache。</summary>
        /// <param name="relative">固定源路径。</param><param name="sha">完整预期SHA。</param><param name="files">本次字符串memo。</param>
        private static void CheckSource(string relative, string sha, Dictionary<string, string> files) => CheckFile(Path.Combine(BuildActionCommon.ProjectRoot, relative), sha, files);

        /// <summary>本次捕获局部复用准确文件SHA，不持有对象或跨捕获缓存。</summary>
        /// <param name="path">准确已存在文件。</param><param name="sha">已审完整SHA。</param><param name="files">短命字符串memo。</param>
        private static void CheckFile(string path, string sha, Dictionary<string, string> files)
        {
            if (!files.TryGetValue(path, out string actual)) { actual = HashFile(path); files.Add(path, actual); }
            if (actual != sha) throw new InvalidOperationException("Generate SVS 已审源/DLL指纹改变：" + path);
        }

        /// <summary>完整归类主/Preview/既存无scene对象；未知对象不忽略、不删除、不触发初始化。</summary>
        /// <param name="manager">唯一自然既存manager。</param><param name="core">已审Core。</param><param name="scenes">完整场景。</param><param name="objects">完整场景对象及四flag。</param><param name="owners">实际Source-owner。</param><param name="invalidObjects">全部无scene对象。</param>
        private static void CaptureInventory(SceneVisibilityManager manager, Assembly core, out SceneRow[] scenes, out ObjectRow[] objects, out OwnerRow[] owners, out InvalidRow[] invalidObjects)
        {
            var sceneRows = new List<SceneRow>(); var objectRows = new List<ObjectRow>(); var invalidRows = new List<InvalidRow>(); var files = new Dictionary<string, string>(StringComparer.Ordinal);
            GameObject[] inventory = Resources.FindObjectsOfTypeAll<GameObject>().Where(value => value != null && !EditorUtility.IsPersistent(value)).ToArray(); owners = CaptureOwners(core, inventory);
            var all = new List<Scene>(); for (int index = 0; index < SceneManager.sceneCount; index++) all.Add(SceneManager.GetSceneAt(index));
            Scene[] previews = inventory.Where(value => value.scene.IsValid() && value.scene.isLoaded && EditorSceneManager.IsPreviewScene(value.scene)).Select(value => value.scene).GroupBy(scene => scene.handle.GetRawData()).Select(group => group.First()).ToArray();
            if (previews.Length != EditorSceneManager.previewSceneCount || all.Any(scene => EditorSceneManager.IsPreviewScene(scene))) throw new InvalidOperationException("Generate SVS Preview完整数量不符。");
            all.AddRange(previews);
            if (inventory.Any(value => value.scene.IsValid() && (!value.scene.isLoaded || !all.Any(scene => scene.handle.GetRawData() == value.scene.handle.GetRawData())))) throw new InvalidOperationException("Generate SVS 有有效但未加载/未归类场景对象。");
            foreach (Scene scene in all)
            {
                bool preview = EditorSceneManager.IsPreviewScene(scene); string handle = scene.handle.GetRawData().ToString(CultureInfo.InvariantCulture);
                if (!scene.IsValid() || !scene.isLoaded || (!preview && (string.IsNullOrEmpty(scene.path) || scene.isDirty)) || preview && scene.path != "") throw new InvalidOperationException("Generate SVS 主场景须已保存，Preview须无资产路径。");
                if (preview && owners.Count(owner => owner.role == "SceneView" && owner.previewHandle == handle) != 1) throw new InvalidOperationException("Generate SVS Preview没有准确唯一既存SceneView owner。");
                sceneRows.Add(new SceneRow { handle = handle, path = scene.path, guid = preview ? ZeroGuid : AssetDatabase.AssetPathToGUID(scene.path), name = scene.name, loaded = scene.isLoaded, dirty = scene.isDirty, preview = preview });
                GameObject[] hierarchy = scene.GetRootGameObjects().SelectMany(root => root.GetComponentsInChildren<Transform>(true)).Select(transform => transform.gameObject).ToArray();
                if (hierarchy.Select(value => UnityEngine.EntityId.ToULong(value.GetEntityId())).Distinct().Count() != hierarchy.Length || !hierarchy.Select(value => UnityEngine.EntityId.ToULong(value.GetEntityId())).OrderBy(id => id).SequenceEqual(inventory.Where(value => value.scene.IsValid() && value.scene.handle.GetRawData() == scene.handle.GetRawData()).Select(value => UnityEngine.EntityId.ToULong(value.GetEntityId())).OrderBy(id => id))) throw new InvalidOperationException("Generate SVS 场景层级与Resources完整集合不等。");
                foreach (GameObject value in hierarchy)
                {
                    Component[] actual = preview ? value.GetComponents<Component>() : null;
                    if (preview && (actual.Length != 2 || actual[0] == null || actual[1] == null || actual[0].GetType() != typeof(Transform) || actual[1].GetType() != typeof(Light))) throw new InvalidOperationException("Generate SVS Preview不是准确Transform+Light。");
                    string json = preview ? EditorJsonUtility.ToJson(value) : null;
                    objectRows.Add(new ObjectRow { id = UnityEngine.EntityId.ToULong(value.GetEntityId()), globalId = GlobalObjectId.GetGlobalObjectIdSlow(value).ToString(), sceneHandle = handle, name = value.name, hidden = manager.IsHidden(value, false), hiddenSubtree = manager.IsHidden(value, true), pickingDisabled = manager.IsPickingDisabled(value, false), pickingDisabledSubtree = manager.IsPickingDisabled(value, true), preview = preview, hideFlags = (int)value.hideFlags, previewJson = json, previewJsonSha256 = json == null ? null : Hash(Utf8.GetBytes(json)), previewComponents = preview ? actual.Select(component => CaptureComponent(component, files)).ToArray() : null });
                }
            }
            foreach (GameObject value in inventory.Where(go => !go.scene.IsValid()).OrderBy(go => UnityEngine.EntityId.ToULong(go.GetEntityId())))
            {
                if (value.scene.handle.GetRawData() != 0) throw new InvalidOperationException("Generate SVS 无效scene句柄不是0。");
                string role = null, ownerKey = null;
                foreach (OwnerRow owner in owners)
                {
                    string candidate = owner.cameraGoId == UnityEngine.EntityId.ToULong(value.GetEntityId()) ? "SceneViewCamera" : owner.gizmoCameraGoId == UnityEngine.EntityId.ToULong(value.GetEntityId()) ? "OrientationCamera" : owner.helperGoId == UnityEngine.EntityId.ToULong(value.GetEntityId()) ? "DeformationHelper" : null;
                    if (candidate != null) { if (role != null) throw new InvalidOperationException("Generate SVS 无scene对象owner重叠。"); role = candidate; ownerKey = owner.key; }
                }
                Component[] components = value.GetComponents<Component>();
                if (role == null && components.Length == 2 && components[1] != null && components[1].GetType().FullName == "AppLovinMax.Internal.MaxEventExecutor") { role = "ExistingMaxExecutor"; ownerKey = "stable-native"; }
                if (role == null && value.name == "InternalIdentityTransform" && (int)value.hideFlags == 125 && !value.activeSelf && components.Length == 1 && components[0] != null && components[0].GetType() == typeof(Transform)) { role = "ExistingInternalIdentity"; ownerKey = "stable-native"; }
                if (role == null) throw new InvalidOperationException("Generate SVS 无scene对象无已审Source-owner：" + UnityEngine.EntityId.ToULong(value.GetEntityId()));
                invalidRows.Add(new InvalidRow { role = role, ownerKey = ownerKey, id = UnityEngine.EntityId.ToULong(value.GetEntityId()), globalId = GlobalObjectId.GetGlobalObjectIdSlow(value).ToString(), name = value.name, hideFlags = (int)value.hideFlags, json = EditorJsonUtility.ToJson(value), hidden = manager.IsHidden(value, false), hiddenSubtree = manager.IsHidden(value, true), pickingDisabled = manager.IsPickingDisabled(value, false), pickingDisabledSubtree = manager.IsPickingDisabled(value, true), components = components.Select(component => CaptureComponent(component, files)).ToArray() });
            }
            scenes = sceneRows.OrderBy(row => row.handle, StringComparer.Ordinal).ToArray(); objects = objectRows.OrderBy(row => row.id).ToArray(); invalidObjects = invalidRows.OrderBy(row => row.role, StringComparer.Ordinal).ThenBy(row => row.ownerKey, StringComparer.Ordinal).ToArray();
        }

        /// <summary>完整观察内部关系与原文SHA核验；不将名称或类型单独作为新对象豁免。</summary>
        /// <param name="value">已展开的完整单次材料。</param>
        private static void ValidateInventory(Snapshot value)
        {
            if (value.owners == null || value.invalidObjects == null || value.owners.Length > 32 || value.invalidObjects.Length > 128 || value.queryBeforeBody == null || value.queryBeforeCache == null) throw new InvalidOperationException("Generate SVS 旧材料没有完整owner/查询跨度。");
            if (value.scenes.Any(scene => scene.preview && value.owners.Count(owner => owner != null && owner.role == "SceneView" && owner.previewHandle == scene.handle) != 1)) throw new InvalidOperationException("Generate SVS Preview没有唯一已审owner。");
            if (value.owners.Any(owner => owner == null || owner.id == 0 || string.IsNullOrEmpty(owner.globalId) || string.IsNullOrEmpty(owner.type) || string.IsNullOrEmpty(owner.key) || owner.role != "SceneView" && owner.role != "DeformationManager") || value.owners.Select(owner => owner.role + "|" + owner.key).Distinct().Count() != value.owners.Length) throw new InvalidOperationException("Generate SVS owner身份缺失或重复。");
            foreach (OwnerRow owner in value.owners)
            {
                if (owner.role == "SceneView")
                {
                    SceneRow preview = value.scenes.SingleOrDefault(scene => scene.preview && scene.handle == owner.previewHandle);
                    if (owner.type != typeof(SceneView).AssemblyQualifiedName || preview == null || preview.name != "CustomLightsScene-SceneView" + owner.key || owner.cameraId == 0 || owner.cameraGoId == 0 || owner.gizmoCameraId == 0 || owner.gizmoCameraGoId == 0 || owner.lightIds == null || owner.lightGoIds == null || owner.lightIds.Length != 3 || owner.lightGoIds.Length != 3 || owner.lightIds.Any(id => id == 0) || owner.lightGoIds.Distinct().Count() != 3 || owner.helperGoId != 0) throw new InvalidOperationException("Generate SVS SceneView owner完整关系不符。");
                    ObjectRow[] lights = value.objects.Where(row => row.preview && row.sceneHandle == owner.previewHandle).ToArray();
                    if (lights.Length != 3 || !lights.Select(row => row.id).OrderBy(id => id).SequenceEqual(owner.lightGoIds.OrderBy(id => id))) throw new InvalidOperationException("Generate SVS owner的Preview光源名单不相等。");
                    for (int index = 0; index < 3; index++) if (!lights.Single(row => row.id == owner.lightGoIds[index]).previewComponents.Any(component => component.id == owner.lightIds[index] && component.type == typeof(Light).AssemblyQualifiedName)) throw new InvalidOperationException("Generate SVS owner光源组件ID不匹配。");
                }
                else if (owner.type != "UnityEngine.U2D.Animation.DeformationManager, Unity.2D.Animation.Runtime, Version=0.0.0.0, Culture=neutral, PublicKeyToken=null" || owner.helperGoId == 0 || owner.cameraId != 0 || owner.cameraGoId != 0 || owner.gizmoCameraId != 0 || owner.gizmoCameraGoId != 0 || owner.lightIds != null || owner.lightGoIds != null || owner.previewHandle != null) throw new InvalidOperationException("Generate SVS Deform owner结构未知。");
            }
            foreach (ObjectRow row in value.objects.Where(row => row.preview))
            {
                if (string.IsNullOrEmpty(row.previewJson) || Hash(Utf8.GetBytes(row.previewJson)) != row.previewJsonSha256 || row.previewComponents.Length != 2 || row.hideFlags != 61 || row.previewComponents[0].type != typeof(Transform).AssemblyQualifiedName || row.previewComponents[1].type != typeof(Light).AssemblyQualifiedName) throw new InvalidOperationException("Generate SVS Preview完整正文缺失。");
                foreach (PreviewComponent component in row.previewComponents) { ValidateComponent(component); if (component.hideFlags != 61) throw new InvalidOperationException("Generate SVS Preview组件标记改变。"); }
            }
            foreach (InvalidRow row in value.invalidObjects)
            {
                if (row == null || row.id == 0 || string.IsNullOrEmpty(row.globalId) || string.IsNullOrEmpty(row.json) || row.hidden || row.hiddenSubtree || row.pickingDisabled || row.pickingDisabledSubtree || row.components == null || row.components.Length < 1 || row.components[0].type != typeof(Transform).AssemblyQualifiedName || row.hideFlags != (row.role == "ExistingInternalIdentity" ? 125 : 61)) throw new InvalidOperationException("Generate SVS 无scene对象完整证据或实际flag不符。");
                foreach (PreviewComponent component in row.components) { ValidateComponent(component); if (component.hideFlags != row.hideFlags) throw new InvalidOperationException("Generate SVS 无scene组件标记改变。"); }
                string[] names = row.components.Select(component => component.type.Split(',')[0]).ToArray();
                string[] expected = row.role == "SceneViewCamera" ? new[] { "UnityEngine.Transform", "UnityEngine.Camera", "UnityEngine.FlareLayer" } : row.role == "OrientationCamera" ? new[] { "UnityEngine.Transform", "UnityEngine.Camera" } : row.role == "DeformationHelper" ? new[] { "UnityEngine.Transform", "UnityEngine.U2D.Animation.DeformationManagerUpdater" } : row.role == "ExistingMaxExecutor" ? new[] { "UnityEngine.Transform", "AppLovinMax.Internal.MaxEventExecutor" } : row.role == "ExistingInternalIdentity" ? new[] { "UnityEngine.Transform" } : null;
                if (expected == null || !names.SequenceEqual(expected)) throw new InvalidOperationException("Generate SVS 无scene角色组件集合未知。");
                if (row.role.StartsWith("Existing", StringComparison.Ordinal)) { if (row.ownerKey != "stable-native") throw new InvalidOperationException("Generate SVS 固定Native角色owner未知。"); }
                else
                {
                    OwnerRow owner = value.owners.SingleOrDefault(item => item.key == row.ownerKey && item.role == (row.role == "DeformationHelper" ? "DeformationManager" : "SceneView"));
                    ulong expectedId = owner == null ? 0 : row.role == "SceneViewCamera" ? owner.cameraGoId : row.role == "OrientationCamera" ? owner.gizmoCameraGoId : owner.helperGoId;
                    if (row.id != expectedId || row.role != "DeformationHelper" && row.components[1].id != (row.role == "SceneViewCamera" ? owner.cameraId : owner.gizmoCameraId)) throw new InvalidOperationException("Generate SVS 无scene角色未绑定准确Source-owner。");
                }
            }
            if (value.invalidObjects.Select(row => row.id).Distinct().Count() != value.invalidObjects.Length || value.invalidObjects.Select(row => row.role + "|" + row.ownerKey).Distinct().Count() != value.invalidObjects.Length || value.invalidObjects.Any(row => value.objects.Any(item => item.id == row.id))) throw new InvalidOperationException("Generate SVS 完整对象名单重复。");
            foreach (OwnerRow owner in value.owners) if (owner.role == "SceneView" ? value.invalidObjects.Count(row => row.ownerKey == owner.key && (row.role == "SceneViewCamera" || row.role == "OrientationCamera")) != 2 : value.invalidObjects.Count(row => row.ownerKey == owner.key && row.role == "DeformationHelper") != 1) throw new InvalidOperationException("Generate SVS Source-owner子对象缺失。");
        }

        /// <summary>完整组件原文/类型/源码DLL身份校验；不接受只有摘要的旧回执。</summary>
        /// <param name="component">实际完整组件行。</param>
        private static void ValidateComponent(PreviewComponent component)
        {
            if (component == null || component.id == 0 || string.IsNullOrEmpty(component.globalId) || string.IsNullOrEmpty(component.json) || Hash(Utf8.GetBytes(component.json)) != component.jsonSha256) throw new InvalidOperationException("Generate SVS 组件完整原文或Native身份缺失。");
            string name = component.type?.Split(',')[0]; string sha, mvid;
            if (new[] { "UnityEngine.Transform", "UnityEngine.Camera", "UnityEngine.FlareLayer", "UnityEngine.Light" }.Contains(name)) { sha = RuntimeCoreSha256; mvid = "e816434e-022e-42a6-bec8-8f93bd4bf5e5"; if (component.type != name + ", UnityEngine.CoreModule, Version=0.0.0.0, Culture=neutral, PublicKeyToken=null") throw new InvalidOperationException("Generate SVS Native组件声明身份冲突。"); }
            else if (name == "UnityEngine.U2D.Animation.DeformationManagerUpdater") { sha = "bc8c905a443557b0a19f45732c684b3d5a910e777baa1d714c64ab8fc0e60cf6"; mvid = "1011aa73-cd92-4656-a2f8-71728e4998cf"; if (!component.type.StartsWith(name + ", Unity.2D.Animation.Runtime,", StringComparison.Ordinal)) throw new InvalidOperationException("Generate SVS Deform声明身份冲突。"); }
            else if (name == "AppLovinMax.Internal.MaxEventExecutor") { sha = "0fe4f91289157c65eafffb205149331c53f655708025ec6de79d5ae3f5fdcc99"; mvid = "513ee476-abfa-44ff-8b3f-83b2706b521f"; if (!component.type.StartsWith(name + ", MaxSdk.Scripts,", StringComparison.Ordinal)) throw new InvalidOperationException("Generate SVS Max声明身份冲突。"); }
            else throw new InvalidOperationException("Generate SVS 未审组件声明。");
            if (component.assemblySha256 != sha || component.assemblyMvid != mvid) throw new InvalidOperationException("Generate SVS 组件已审DLL指纹不符。");
        }

        /// <summary>只取已冻结SceneView实际Preview句柄，UInt64按位转有符号64位，绝不32位截断。</summary>
        /// <param name="value">完整展开实际材料。</param>
        /// <returns>唯一允许的新增空缓存键。</returns>
        private static HashSet<long> AllowedPreviewHandles(Snapshot value) => new HashSet<long>(value.owners.Where(owner => owner.role == "SceneView").Select(owner => unchecked((long)ulong.Parse(owner.previewHandle, CultureInfo.InvariantCulture))));

        /// <summary>首次正式重载仅允许源已证owner子对象重建；主场景、固定Native对象、完整正文与四flag原样。</summary>
        /// <param name="before">真实Scope后态。</param><param name="current">首次正式重载域实际观测。</param>
        /// <returns>精确有限关系保持时为真，不构造或修改任何对象。</returns>
        private static bool ReloadIdentityEquals(Snapshot before, Snapshot current)
        {
            Snapshot left = ExpandSnapshot(before), right = ExpandSnapshot(current);
            if (left.process != right.process || left.coreSha256 != right.coreSha256 || left.coreMvid != right.coreMvid || left.runtimeCoreSha256 != right.runtimeCoreSha256 || left.stateId != right.stateId || left.stateGlobalId != right.stateGlobalId || left.dirtyIdentity != right.dirtyIdentity || left.managerId != right.managerId || left.managerGlobalId != right.managerGlobalId || left.stageManagerId != right.stageManagerId || left.mainStageId != right.mainStageId || left.visibilityEnabled != right.visibilityEnabled || left.pickingEnabled != right.pickingEnabled || left.isolated != right.isolated || left.activeScene != right.activeScene) return false;
            if (!Same(left.scenes.Where(row => !row.preview).ToArray(), right.scenes.Where(row => !row.preview).ToArray()) || !Same(left.objects.Where(row => !row.preview).ToArray(), right.objects.Where(row => !row.preview).ToArray()) || left.owners.Length != right.owners.Length || left.invalidObjects.Length != right.invalidObjects.Length) return false;
            foreach (OwnerRow old in left.owners)
            {
                OwnerRow next = right.owners.SingleOrDefault(owner => owner.role == old.role && owner.key == old.key);
                if (next == null || old.id != next.id || old.globalId != next.globalId || old.type != next.type || old.hideFlags != next.hideFlags) return false;
                if (old.role == "SceneView")
                {
                    SceneRow a = left.scenes.Single(scene => scene.handle == old.previewHandle), b = right.scenes.Single(scene => scene.handle == next.previewHandle);
                    if (a.path != b.path || a.guid != b.guid || a.name != b.name || a.loaded != b.loaded || a.dirty != b.dirty || a.preview != b.preview) return false;
                    for (int index = 0; index < old.lightGoIds.Length; index++)
                    {
                        ObjectRow x = left.objects.Single(row => row.id == old.lightGoIds[index]), y = right.objects.Single(row => row.id == next.lightGoIds[index]);
                        if (x.globalId != y.globalId || x.name != y.name || x.hideFlags != y.hideFlags || x.previewJson != y.previewJson || x.hidden != y.hidden || x.hiddenSubtree != y.hiddenSubtree || x.pickingDisabled != y.pickingDisabled || x.pickingDisabledSubtree != y.pickingDisabledSubtree || !ComponentsEqual(x.previewComponents, y.previewComponents, true)) return false;
                    }
                }
            }
            foreach (InvalidRow old in left.invalidObjects)
            {
                InvalidRow next = right.invalidObjects.SingleOrDefault(row => row.role == old.role && row.ownerKey == old.ownerKey);
                if (next == null) return false;
                bool recreate = old.role == "SceneViewCamera" || old.role == "OrientationCamera" || old.role == "DeformationHelper";
                if (!recreate && old.id != next.id || old.globalId != next.globalId || old.name != next.name || old.hideFlags != next.hideFlags || old.json != next.json || old.hidden != next.hidden || old.hiddenSubtree != next.hiddenSubtree || old.pickingDisabled != next.pickingDisabled || old.pickingDisabledSubtree != next.pickingDisabledSubtree || !ComponentsEqual(old.components, next.components, recreate)) return false;
            }
            return true;
        }

        /// <summary>只检验完整重载材料的纯DTO门；本方法不产生执行许可，调用链仍必须另验真实首次编译Token。</summary>
        /// <param name="after">真实Scope后态。</param><param name="current">重载后真实完整材料。</param><param name="error">具体未知差异。</param>
        /// <returns>仅材料关系满足时为真，不能单独授权输入投影。</returns>
        internal static bool TryValidateReloadMaterial(Snapshot after, Snapshot current, out string error)
        {
            error = null;
            try
            {
                ValidateSnapshot(after); ValidateSnapshot(current); after = ExpandSnapshot(after); current = ExpandSnapshot(current);
                if (after.domain == current.domain || !ReloadIdentityEquals(after, current) || after.dirty != current.dirty || !Same(after.cache, current.cache)) throw new InvalidOperationException("Generate SVS 首次重载材料owner/正文/实际标记或准确Library缓存漂移。");
                ValidateAppend(DecodeBody(after.body), DecodeBody(current.queryBeforeBody), AllowedPreviewHandles(current), "m_ScenePickingData");
                ValidateAppend(DecodeBody(current.queryBeforeBody), DecodeBody(current.body), AllowedPreviewHandles(current), "m_SceneVisibilityData");
                return true;
            }
            catch (Exception exception) { error = exception.Message; return false; }
        }

        /// <summary>逐组件保留完整原文与所有类型/指纹/标记，只在准确owner重建角色放开NativeID。</summary>
        /// <param name="before">原全部组件顺序。</param><param name="current">当前全部组件顺序。</param><param name="recreate">该角色是否源已证正常重建。</param>
        /// <returns>有限差异之外全部字段相等时为真。</returns>
        private static bool ComponentsEqual(PreviewComponent[] before, PreviewComponent[] current, bool recreate)
        {
            if (before == null || current == null || before.Length != current.Length) return false;
            for (int index = 0; index < before.Length; index++)
            {
                PreviewComponent a = before[index], b = current[index];
                if (!recreate && a.id != b.id || a.globalId != b.globalId || a.type != b.type || a.hideFlags != b.hideFlags || a.json != b.json || a.jsonSha256 != b.jsonSha256 || a.assemblySha256 != b.assemblySha256 || a.assemblyMvid != b.assemblyMvid) return false;
            }
            return true;
        }

    }
}
