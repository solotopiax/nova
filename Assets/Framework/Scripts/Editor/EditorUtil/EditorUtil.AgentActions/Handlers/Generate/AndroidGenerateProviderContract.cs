/***************************************************************
 * (c) copyright 2026 - 2030, Solotopia
 * All Rights Reserved.
 * -------------------------------------------------------------
 * filename:  AndroidGenerateProviderContract.cs
 * author:    taoye
 * created:   2026/10/20
 * descrip:   Android Generate原配置提供者的来源与自然读取有限合同
 ***************************************************************/
using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using Path = System.IO.Path;
using System.Linq;
using System.Reflection;
using System.Runtime.CompilerServices;
using System.Threading;
using NovaFramework.Runtime;
using UnityEditor;
using UnityEngine;

namespace NovaFramework.Editor
{
    /// <summary>仅支持已保存路径提示和既存ExportTarget；不修复Globals、不导出或预热SDK单例。</summary>
    internal static class AndroidGenerateProviderContract
    {
        private const string Globals = "ProjectSettings/Nova/Globals.json";
        private static readonly string[][] Sources = {
            new[] { "Assets/Framework/Scripts/Editor/EditorUtil/EditorUtil.Config/EditorUtil.Config.WorkspaceActive.cs", "40b384c49c50076070653ef80cf6c37239194986aead9ad2cc8a3e041ad15f20" },
            new[] { "Assets/Framework/Scripts/Editor/EditorUtil/EditorUtil.Config/EditorUtil.Config.RuntimeProvider.cs", "61dfacd7dfbe804aad895c4559e9fa9648c36c4f731593120908874374b2e6f6" },
            new[] { "Assets/Framework/Scripts/Runtime/Modules/Config/ConfigRuntimeSO.cs", "5ffd7778b56aab2671a645a5995fedf119ec208b88adfa7a8779800366dd127a" },
        };

        /// <summary>准确来源与完整正文摘要；不保存配置正文、key或token。</summary>
        [Serializable] internal sealed class Snapshot
        {
            public string masterGuid;
            public string masterPath;
            public string runtimePath;
            public string runtimeGuid;
            public long runtimeLocalId;
            public ulong runtimeNativeId;
            public bool runtimeDirty;
            public string runtimeJsonSha256;
            public PlayerBuildAction.FileState[] files;
            public string[] sources;
        }

        /// <summary>先只读证明Get不会修复路径，再观察一次原provider读取；两端完整保持才返回有限证明。</summary>
        /// <param name="masterPath">Action已冻结的准确Master路径。</param><param name="token">原Action取消令牌。</param><returns>原引用、Native身份及完整源摘要。</returns>
        internal static Snapshot Capture(string masterPath, CancellationToken token)
        {
            if (EditorUserBuildSettings.activeBuildTarget != BuildTarget.Android) throw new InvalidOperationException("配置provider合同只支持真实Android活动域。");
            foreach (string[] source in Sources)
                if (GenerateActionCommon.ComputeFileHash(Path.Combine(BuildActionCommon.ProjectRoot, source[0]), token) != source[1])
                    throw new InvalidOperationException("Android配置provider已审源码漂移：" + source[0]);
            if (!EditorUtil.Config.WorkspaceActive.TryGetPersistedConfigMaster(out ConfigMasterSO master, out string guid, out string actualPath, out string error) || actualPath != masterPath)
                throw new InvalidOperationException(error ?? "Android provider的持久Master与Action不同。");
            ConfigRuntimeSO runtime = master.ExportTarget;
            if (runtime == null || runtime.GetType() != typeof(ConfigRuntimeSO) || runtime.Platform != PlatformType.Android)
                throw new InvalidOperationException("Android provider当前只支持既存ExportTarget原引用；布局加载分支尚无有限合同。");
            RequireNoSerializationCallbacks(runtime);
            Snapshot before = Read(masterPath, guid, runtime, token);
            ConfigRuntimeSO provided = EditorUtil.Config.RuntimeProvider.GetCurrent();
            Snapshot after = Read(masterPath, guid, runtime, token);
            if (!ReferenceEquals(runtime, provided) || !ReferenceEquals(master.ExportTarget, runtime) || !Equal(before, after))
                throw new InvalidOperationException("Android原provider读取改变了来源、正文、Native身份或dirty状态。");
            return before;
        }

        /// <summary>完整比较同一域读前/读后材料，不投影Native身份或dirty变化。</summary>
        /// <param name="before">原真实读取前态。</param><param name="after">同次真实读取后态。</param><returns>全部字段完整相等时为true。</returns>
        internal static bool Equal(Snapshot before, Snapshot after) => before != null && after != null && Util.Json.Serialize(before) == Util.Json.Serialize(after);

        /// <summary>读取准确主资产与源文件；所有正文只计算SHA，不传输或输出。</summary>
        /// <param name="masterPath">已保存Master。</param><param name="masterGuid">持久GUID。</param><param name="runtime">原ExportTarget。</param><param name="token">取消令牌。</param><returns>完整小型来源材料。</returns>
        private static Snapshot Read(string masterPath, string masterGuid, ConfigRuntimeSO runtime, CancellationToken token)
        {
            string path = AssetDatabase.GetAssetPath(runtime);
            if (!EditorUtility.IsPersistent(runtime) || !AssetDatabase.IsMainAsset(runtime) || !path.StartsWith("Assets/", StringComparison.Ordinal) ||
                !AssetDatabase.TryGetGUIDAndLocalFileIdentifier(runtime, out string guid, out long localId))
                throw new InvalidOperationException("Android provider的Runtime不是既存Assets主资产。");
            PlayerBuildAction.FileState[] files = PlayerBuildAction.CapturePaths(new[] { Globals, masterPath, masterPath + ".meta", path, path + ".meta" }, token);
            ulong nativeId = UnityEngine.EntityId.ToULong(runtime.GetEntityId()); bool dirty = EditorUtility.IsDirty(runtime);
            string json = EditorJsonUtility.ToJson(runtime);
            if (System.Text.Encoding.UTF8.GetByteCount(json) > 2 * 1024 * 1024) throw new InvalidOperationException("Android provider完整Runtime超过2MiB，不截断。");
            if (nativeId != UnityEngine.EntityId.ToULong(runtime.GetEntityId()) || dirty != EditorUtility.IsDirty(runtime) || Util.Json.Serialize(files) !=
                Util.Json.Serialize(PlayerBuildAction.CapturePaths(new[] { Globals, masterPath, masterPath + ".meta", path, path + ".meta" }, token)))
                throw new InvalidOperationException("Android Runtime完整序列化改变了原文件、Native身份或dirty状态。");
            return new Snapshot { masterGuid = masterGuid, masterPath = masterPath, runtimePath = path, runtimeGuid = guid,
                runtimeLocalId = localId, runtimeNativeId = nativeId, runtimeDirty = dirty,
                runtimeJsonSha256 = GenerateActionCommon.ComputeTextHash(json), sources = Sources.Select(source => source[0] + "|" + source[1]).ToArray(),
                files = files };
        }

        /// <summary>只沿Unity实际序列化字段检查回调图；不调用业务getter、任意枚举器或自定义相等函数。</summary>
        /// <param name="root">即将序列化的原Runtime。</param>
        private static void RequireNoSerializationCallbacks(ConfigRuntimeSO root)
        {
            var seen = new HashSet<object>(ReferenceComparer.Instance); var pending = new Stack<(object value, int depth)>();
            pending.Push((root, 0)); int count = 0;
            while (pending.Count != 0)
            {
                var item = pending.Pop(); object value = item.value;
                if (value == null || !seen.Add(value)) continue;
                if (++count > 100000 || item.depth > 64) throw new InvalidOperationException("Android Runtime序列化图超过有限检查预算。");
                Type type = value.GetType();
                if (value is ISerializationCallbackReceiver) throw new InvalidOperationException("Android Runtime序列化图含未审自定义回调：" + type.FullName);
                if (type.IsPrimitive || type.IsEnum || type == typeof(string) || type == typeof(decimal)) continue;
                if (value is UnityEngine.Object && !ReferenceEquals(value, root)) continue;
                if (type.IsArray)
                {
                    foreach (object child in (Array)value) pending.Push((child, item.depth + 1));
                    continue;
                }
                if (type.IsGenericType && type.GetGenericTypeDefinition() == typeof(List<>))
                {
                    foreach (object child in (IList)value) pending.Push((child, item.depth + 1));
                    continue;
                }
                for (Type current = type; current != null && current != typeof(UnityEngine.Object); current = current.BaseType)
                    foreach (FieldInfo field in current.GetFields(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.DeclaredOnly))
                        if (!field.IsNotSerialized && (field.IsPublic || field.IsDefined(typeof(SerializeField), false) || field.IsDefined(typeof(SerializeReference), false)))
                            pending.Push((field.GetValue(value), item.depth + 1));
            }
        }

        /// <summary>仅使用CLR引用相等，避免序列化图中的业务GetHashCode/Equals被执行。</summary>
        private sealed class ReferenceComparer : IEqualityComparer<object>
        {
            internal static readonly ReferenceComparer Instance = new ReferenceComparer();
            /// <summary>按原对象引用比较，不调用业务Equals。</summary>
            /// <param name="left">左对象。</param><param name="right">右对象。</param><returns>引用相同时为true。</returns>
            public new bool Equals(object left, object right) => ReferenceEquals(left, right);
            /// <summary>取得CLR引用哈希，不调用业务GetHashCode。</summary>
            /// <param name="value">当前对象。</param><returns>引用哈希。</returns>
            public int GetHashCode(object value) => RuntimeHelpers.GetHashCode(value);
        }
    }
}
