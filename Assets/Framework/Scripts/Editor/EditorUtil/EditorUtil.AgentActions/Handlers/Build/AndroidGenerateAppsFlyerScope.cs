/***************************************************************
 * (c) copyright 2026 - 2030, Solotopia
 * All Rights Reserved.
 * -------------------------------------------------------------
 * filename:  AndroidGenerateAppsFlyerScope.cs
 * author:    taoye
 * created:   2026/10/20
 * descrip:   原AppsFlyer既存properties写入的准确阶段观察，导入尾链仍独立阻断
 ***************************************************************/
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading;
using Newtonsoft.Json;
using UnityEditor;
using UnityEditor.Build.Reporting;

namespace NovaFramework.Editor
{
    /// <summary>只观察原SDK foreach与原四数组，不调用SDK、ImportAsset、Refresh或厂商队列；本批不消解导入尾链义务。</summary>
    internal sealed class AndroidGenerateAppsFlyerScope : IDisposable, INovaBuildSdkPreObserver
    {
        internal const string PropertiesPath = "Assets/Plugins/Android/gradleTemplate.properties";
        internal const string TailUnbound = "AppsFlyer.generated-assets-and-parent-manifest.import-tail";
        private static AndroidGenerateAppsFlyerScope active;
        private static readonly UTF8Encoding Utf8 = new UTF8Encoding(false, true);

        /// <summary>只包含来源与完整摘要；原文件内容及SDK配置不进入收据。</summary>
        [Serializable] internal sealed class PropertyState
        {
            public string guid;
            public string mainType;
            public string importerType;
            public ulong importerId;
            public string importerJsonSha256;
            public string fileSha256;
            public long length;
            public string predictedSha256;
            public long predictedLength;
            public string metaSha256;
            public string directoryMetaSha256;
        }
        /// <summary>当前真实既存资产输入；缺失资产、新GUID、未知Importer保持阻断，不创建文件。</summary>
        [Serializable] internal sealed class Plan
        {
            public PropertyState properties;
            public string processorAssembly;
            public string processorMvid;
            public string processorDllSha256;
            public string[] preprocessOrder;
            public string[] unboundContracts;
        }
        /// <summary>原四数组完整独立副本；不删除或过滤Generate其它阶段的合法路径。</summary>
        [Serializable] internal sealed class ImportDispatch
        {
            public int ordinal;
            public string stage;
            public string[] imported;
            public string[] deleted;
            public string[] moved;
            public string[] movedFrom;
        }
        /// <summary>原SDK真实调用点材料；同步返回仅证明properties变换，不宣称Search/Version/Manifest尾部完成。</summary>
        [Serializable] internal sealed class Evidence
        {
            public bool sdkSequenceObserved;
            public bool appsFlyerBeforeObserved;
            public bool appsFlyerReturned;
            public bool disposed;
            public string failure;
            public string stageManifestSha256;
            public PropertyState before;
            public PropertyState after;
            public AndroidResolveRefreshContract.Snapshot refreshStageBefore;
            public string refreshStageFailure;
            public string[] sdkStages;
            public ImportDispatch[] imports;
            public string[] unboundContracts;
        }

        private Plan plan;
        private NovaAndroidParentBuildContract.FactorySnapshot factory;
        private NovaBuildParentObservers.SdkPreLease lease;
        private readonly CancellationToken token;
        private readonly int ownerThread;
        private readonly Evidence evidence = new Evidence { unboundContracts = new[] { TailUnbound } };
        private readonly List<string> sdkStages = new List<string>();
        private readonly List<ImportDispatch> imports = new List<ImportDispatch>();
        private BuildReport originalReport;
        private NovaBuildContext originalContext;
        private List<NovaSDKBuildProcessor> originalList;
        private NovaSDKBuildProcessor[] originalInstances;
        private NovaSDKBuildProcessor currentProcessor;
        private int nextIndex;
        private bool awaitingAfter;
        private bool disposed;
        private int capturedPathCount;
        private long capturedCharacters;

        /// <summary>只保存借用材料，不触发任何业务构造或Editor事件。</summary>
        private AndroidGenerateAppsFlyerScope(Plan expected, NovaAndroidParentBuildContract.FactorySnapshot batch, CancellationToken cancellation)
        { plan = expected; factory = batch; token = cancellation; ownerThread = Thread.CurrentThread.ManagedThreadId; }

        /// <summary>读取实际既存properties、GUID/meta/Importer及完整本次AF模块身份；不执行原写入或Import。</summary>
        internal static Plan CapturePlan(NovaAndroidParentBuildContract.FactorySnapshot factory, CancellationToken token)
        {
            NovaAndroidParentBuildContract.VerifyFactoryBefore(factory, token);
            Type type = factory.types.Single(value => value.FullName == NovaAndroidSdkLeafBuildContract.AppsFlyer);
            return new Plan { properties = CaptureProperty(token), processorAssembly = type.Assembly.FullName,
                processorMvid = type.Module.ModuleVersionId.ToString("D"), processorDllSha256 = GenerateActionCommon.ComputeFileHash(type.Assembly.Location, token),
                preprocessOrder = factory.preprocessOrder.ToArray(), unboundContracts = new[] { TailUnbound } };
        }

        /// <summary>原Generate入口前登记唯一SDK观察租约，核本域源和准确输入，不注册第二个AssetPostprocessor。</summary>
        internal static AndroidGenerateAppsFlyerScope Begin(Plan expected, NovaAndroidParentBuildContract.FactorySnapshot batch, CancellationToken cancellation)
        {
            if (!AgentActionRuntime.IsMainThread || active != null) throw new InvalidOperationException("AF观察要求原Unity主线程唯一作用域。");
            if (expected == null || JsonConvert.SerializeObject(expected) != JsonConvert.SerializeObject(CapturePlan(batch, cancellation))) throw new InvalidOperationException("AF真实properties/模块输入漂移。");
            var value = new AndroidGenerateAppsFlyerScope(expected, batch, cancellation);
            try { value.lease = NovaBuildParentObservers.RegisterSdkPre(value); active = value; return value; }
            catch { value.Dispose(); throw; }
        }

        /// <summary>只接受原foreach的实际before；AF基线在模板复制后、原AF写入前捕获。</summary>
        void INovaBuildSdkPreObserver.BeforeSdkPre(BuildReport report, NovaBuildContext context, List<NovaSDKBuildProcessor> processors, NovaSDKBuildProcessor processor)
        {
            try
            {
                RequireActive(); token.ThrowIfCancellationRequested(); RequireBatch(report, context, processors, processor);
                if (awaitingAfter || nextIndex >= plan.preprocessOrder.Length || processor.GetType().FullName != plan.preprocessOrder[nextIndex]) throw new InvalidOperationException("原SDK pre顺序或before/after配对无效。");
                currentProcessor = processor; awaitingAfter = true; sdkStages.Add("before|" + processor.GetType().FullName);
                if (processor.GetType().FullName == NovaAndroidSdkLeafBuildContract.AppsFlyer)
                {
                    if (evidence.appsFlyerBeforeObserved) throw new InvalidOperationException("原AF不得重复进入。");
                    evidence.before = CaptureProperty(token);
                    if (JsonConvert.SerializeObject(evidence.before) != JsonConvert.SerializeObject(plan.properties)) throw new InvalidOperationException("原AF到达前properties输入已变化。");
                    evidence.stageManifestSha256 = Hash(ReadBounded(System.IO.Path.Combine(BuildActionCommon.ProjectRoot, NovaAndroidParentBuildContract.ManifestPath), token));
                    if (!AndroidResolveRefreshContract.TryCaptureAppsFlyerPropertiesStage(token, out evidence.refreshStageBefore, out evidence.refreshStageFailure))
                        throw new InvalidOperationException("AF实际阶段导入前置合同未闭合：" + evidence.refreshStageFailure);
                    evidence.appsFlyerBeforeObserved = true;
                }
            }
            catch (Exception error) { evidence.failure = "sdk-before:" + error.GetType().Name; throw; }
        }

        /// <summary>只由原SDK正常返回点核同一实例和properties原算法结果，不调用或重放SDK。</summary>
        void INovaBuildSdkPreObserver.AfterSdkPre(BuildReport report, NovaBuildContext context, List<NovaSDKBuildProcessor> processors, NovaSDKBuildProcessor processor)
        {
            try
            {
                RequireActive(); token.ThrowIfCancellationRequested(); RequireBatch(report, context, processors, processor);
                if (!awaitingAfter || !ReferenceEquals(currentProcessor, processor)) throw new InvalidOperationException("原SDK after缺before或实例已替换。");
                if (processor.GetType().FullName == NovaAndroidSdkLeafBuildContract.AppsFlyer)
                {
                    evidence.after = CaptureProperty(token);
                    if (!evidence.appsFlyerBeforeObserved || !MatchesTransform(plan.properties, evidence.after)) throw new InvalidOperationException("原AF properties字节/身份不符合首匹配替换算法。");
                    evidence.appsFlyerReturned = true;
                }
                sdkStages.Add("after|" + processor.GetType().FullName); awaitingAfter = false; currentProcessor = null; nextIndex++;
                evidence.sdkSequenceObserved = nextIndex == plan.preprocessOrder.Length;
            }
            catch (Exception error) { evidence.failure = "sdk-after:" + error.GetType().Name; throw; }
        }

        /// <summary>复用唯一既有观察器转来的完整原数组；无scope直接返回，观察失败只记真实失败不修改Unity输入。</summary>
        internal static void ObserveImports(string[] imported, string[] deleted, string[] moved, string[] movedFrom)
        {
            AndroidGenerateAppsFlyerScope current = active; if (current == null) return;
            try
            {
                current.RequireActive();
                if (imported == null || deleted == null || moved == null || movedFrom == null || moved.Length != movedFrom.Length || current.imports.Count >= 256 || imported.Length + deleted.Length + moved.Length + movedFrom.Length > 8192 || imported.Concat(deleted).Concat(moved).Concat(movedFrom).Any(path => path == null || path.Length > 4096)) throw new InvalidOperationException("AF真实导入四数组无效/超预算，不截断。");
                int count = imported.Length + deleted.Length + moved.Length + movedFrom.Length;
                long characters = imported.Concat(deleted).Concat(moved).Concat(movedFrom).Sum(path => (long)path.Length);
                if (current.capturedPathCount + count > 8192 || current.capturedCharacters + characters > 512 * 1024) throw new InvalidOperationException("AF累计四数组超完整证据预算，不截断或过滤。");
                current.capturedPathCount += count; current.capturedCharacters += characters;
                current.imports.Add(new ImportDispatch { ordinal = current.imports.Count + 1, stage = current.evidence.appsFlyerReturned ? "after-appsflyer" : current.awaitingAfter && current.currentProcessor?.GetType().FullName == NovaAndroidSdkLeafBuildContract.AppsFlyer ? "in-appsflyer" : "other-generate-stage",
                    imported = imported.ToArray(), deleted = deleted.ToArray(), moved = moved.ToArray(), movedFrom = movedFrom.ToArray() });
            }
            catch (Exception error) { current.evidence.failure = current.evidence.failure ?? "import-arrays:" + error.GetType().Name; }
        }

        /// <summary>核原Shared、report、Context、完整已排序类型与同一列表；不读取SDK业务getter。</summary>
        private void RequireBatch(BuildReport report, NovaBuildContext context, List<NovaSDKBuildProcessor> processors, NovaSDKBuildProcessor processor)
        {
            if (report == null || context == null || processors == null || processor == null || context.Target != BuildTarget.Android || !ReferenceEquals(context.Report, report) || !ReferenceEquals(NovaBuildShared.Context, context) || !ReferenceEquals(NovaBuildShared.Processors, processors) || processors.Count != plan.preprocessOrder.Length || !processors.Select(value => value?.GetType().FullName).SequenceEqual(plan.preprocessOrder, StringComparer.Ordinal) || processors.Select(value => value.GetType()).Any(type => !factory.types.Contains(type)) || processors.Any(value => processors.Count(item => ReferenceEquals(item, value)) != 1)) throw new InvalidOperationException("AF原report/Context/完整SDK列表不符。");
            if (originalReport == null) { originalReport = report; originalContext = context; originalList = processors; originalInstances = processors.ToArray(); }
            if (!ReferenceEquals(originalReport, report) || !ReferenceEquals(originalContext, context) || !ReferenceEquals(originalList, processors) || processors.Where((value, index) => !ReferenceEquals(value, originalInstances[index])).Any() || nextIndex >= processors.Count || !ReferenceEquals(processors[nextIndex], processor)) throw new InvalidOperationException("AF原批次引用或当前SDK已替换。");
        }

        /// <summary>仅捕获原已存在文件和准确Importer，不创建资产或清dirty；正文不进入计划。</summary>
        private static PropertyState CaptureProperty(CancellationToken token)
        {
            string full = System.IO.Path.Combine(BuildActionCommon.ProjectRoot, PropertiesPath), directory = System.IO.Path.GetDirectoryName(full);
            if (!Directory.Exists(directory)) throw new InvalidOperationException("AF仅支持已存在Android目录，不产生新目录/GUID。");
            byte[] bytes = ReadBounded(full, token), meta = ReadBounded(full + ".meta", token), parentMeta = ReadBounded(directory + ".meta", token);
            AssetImporter importer = AssetImporter.GetAtPath(PropertiesPath); Type main = AssetDatabase.GetMainAssetTypeAtPath(PropertiesPath); string guid = AssetDatabase.AssetPathToGUID(PropertiesPath);
            if (importer == null || importer.GetType().FullName != "UnityEditor.DefaultImporter" || importer.GetType().Assembly != typeof(AssetImporter).Assembly || main != typeof(DefaultAsset) || guid.Length != 32 || AssetDatabase.GUIDToAssetPath(guid) != PropertiesPath || EditorUtility.IsDirty(importer)) throw new InvalidOperationException("AF既存properties的GUID/DefaultImporter/主类型或dirty状态未知。");
            string importerJson = EditorJsonUtility.ToJson(importer); if (importerJson.Length > 65536) throw new InvalidOperationException("AF importer完整材料超预算。");
            byte[] predicted = PredictBytes(bytes);
            if (Hash(ReadBounded(full, token)) != Hash(bytes) || Hash(ReadBounded(full + ".meta", token)) != Hash(meta) || Hash(ReadBounded(directory + ".meta", token)) != Hash(parentMeta)) throw new InvalidOperationException("AF输入捕获中变化。");
            return new PropertyState { guid = guid, mainType = main.AssemblyQualifiedName, importerType = importer.GetType().AssemblyQualifiedName, importerId = UnityEngine.EntityId.ToULong(importer.GetEntityId()), importerJsonSha256 = GenerateActionCommon.ComputeTextHash(importerJson), fileSha256 = Hash(bytes), length = bytes.Length, predictedSha256 = Hash(predicted), predictedLength = predicted.Length, metaSha256 = Hash(meta), directoryMetaSha256 = Hash(parentMeta) };
        }

        /// <summary>纯字节预测原首个TrimStart键替换或追加及平台换行；仅接受准确UTF8，不写盘或执行Import。</summary>
        internal static byte[] PredictBytes(byte[] bytes)
        {
            if (bytes == null || bytes.Length > 1024 * 1024) throw new InvalidOperationException("AF properties预测缺完整输入或超1MiB。");
            int offset = bytes.Length >= 3 && bytes[0] == 0xef && bytes[1] == 0xbb && bytes[2] == 0xbf ? 3 : 0;
            string text = Utf8.GetString(bytes, offset, bytes.Length - offset); var lines = new List<string>();
            using (var reader = new StringReader(text)) { string line; while ((line = reader.ReadLine()) != null) lines.Add(line); }
            const string key = "android.uniquePackageNames="; int index = lines.FindIndex(line => line.TrimStart().StartsWith(key, StringComparison.Ordinal));
            if (index >= 0) lines[index] = key + "false"; else lines.Add(key + "false");
            return Utf8.GetBytes(string.Join(Environment.NewLine, lines) + Environment.NewLine);
        }

        /// <summary>内容必须是原预测，Importer重建的Native ID只作诊断；GUID/meta/设置和父目录meta仍精确保持。</summary>
        internal static bool MatchesTransform(PropertyState before, PropertyState after) => before != null && after != null && before.predictedLength > 0 && before.predictedSha256?.Length == 64 && before.guid?.Length == 32 && !string.IsNullOrEmpty(before.mainType) && !string.IsNullOrEmpty(before.importerType) && before.importerJsonSha256?.Length == 64 && before.metaSha256?.Length == 64 && before.directoryMetaSha256?.Length == 64 && before.predictedSha256 == after.fileSha256 && before.predictedLength == after.length && before.guid == after.guid && before.mainType == after.mainType && before.importerType == after.importerType && before.importerJsonSha256 == after.importerJsonSha256 && before.metaSha256 == after.metaSha256 && before.directoryMetaSha256 == after.directoryMetaSha256;

        /// <summary>固定1MiB文件及全部祖先拒绝链接；不递归目录、不保存原文。</summary>
        private static byte[] ReadBounded(string path, CancellationToken token)
        {
            token.ThrowIfCancellationRequested();
            for (string current = System.IO.Path.GetFullPath(path); !string.IsNullOrEmpty(current); current = System.IO.Path.GetDirectoryName(current))
                if ((File.GetAttributes(current) & FileAttributes.ReparsePoint) != 0) throw new InvalidOperationException("AF既存角色或祖先含未审链接。");
            var info = new FileInfo(path); if (!info.Exists || info.Length > 1024 * 1024) throw new InvalidOperationException("AF既存文件缺失/超1MiB。");
            byte[] bytes = File.ReadAllBytes(path); if (bytes.Length > 1024 * 1024) throw new InvalidOperationException("AF读取窗口超预算。"); return bytes;
        }
        /// <summary>只保完整字节摘要，不输出配置或文件原文。</summary>
        private static string Hash(byte[] bytes) { using (var sha = System.Security.Cryptography.SHA256.Create()) return BitConverter.ToString(sha.ComputeHash(bytes)).Replace("-", "").ToLowerInvariant(); }
        /// <summary>所有真实callback均核原主线程、当前租约及scope状态；不将旧缓存引用当现租约。</summary>
        private void RequireActive() { RequireOwner(); if (disposed || !ReferenceEquals(active, this) || lease == null || !lease.IsCurrent || evidence.failure != null) throw new InvalidOperationException("AF观察租约已结束或失败。"); }
        /// <summary>释放后允许原线程复制已取得历史，不重采较晚状态。</summary>
        internal Evidence CaptureEvidence() { RequireOwner(); evidence.sdkStages = sdkStages.ToArray(); evidence.imports = imports.ToArray(); return JsonConvert.DeserializeObject<Evidence>(JsonConvert.SerializeObject(evidence)); }
        /// <summary>仅确认同步真实边界；导入尾链仍明确未绑定，不能据此授予Action可执行。</summary>
        internal Evidence CompleteObservation()
        { RequireActive(); token.ThrowIfCancellationRequested(); if (!evidence.sdkSequenceObserved || !evidence.appsFlyerReturned || awaitingAfter) throw new InvalidOperationException("AF原SDK调用历史不完整。"); return CaptureEvidence(); }
        /// <summary>限定登记主线程。</summary>
        private void RequireOwner() { if (Thread.CurrentThread.ManagedThreadId != ownerThread) throw new InvalidOperationException("AF观察只能在原线程使用和释放。"); }
        /// <summary>只解除自身SDK租约/四数组登记，不清厂商队列、不恢复业务或原Shared对象。</summary>
        public void Dispose()
        {
            RequireOwner(); if (disposed) return;
            lease?.Dispose(); lease = null; if (ReferenceEquals(active, this)) active = null;
            disposed = true; evidence.disposed = true; originalReport = null; originalContext = null; originalList = null; originalInstances = null; currentProcessor = null; factory = null; plan = null;
        }
    }
}
