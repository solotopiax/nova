/***************************************************************
 * (c) copyright 2026 - 2030, Solotopia
 * All Rights Reserved.
 * -------------------------------------------------------------
 * filename:  PlayerFacebookBuildContract.cs
 * author:    taoye
 * created:   2026/10/16
 * descrip:   Facebook WebGL 构建回调自然绑定既有 Resources 配置的有限合同
 ***************************************************************/

using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Runtime.CompilerServices;
using System.Threading;
using UnityEditor;
using UnityEngine;
using Object = UnityEngine.Object;

namespace NovaFramework.Editor
{
    /// <summary>只读已有配置；自然 static null→既有 Resources 资产由官方回调完成，不预调用 getter 或创建配置。</summary>
    internal static class PlayerFacebookBuildContract
    {
        internal const string Marker = "FacebookNormalResourcesBinding=existing-unique-asset";
        private const BindingFlags StaticFields = BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Static;
        private const BindingFlags InstanceFields = BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance;

        /// <summary>只记录配置身份、摘要和准确分支布尔；AppId/Token/配置正文不进入计划或回执。</summary>
        [Serializable]
        internal sealed class Snapshot
        {
            public bool applicable;
            public string assetPath;
            public string assetIdentity;
            public ulong assetInstanceId;
            public string assetJsonSha256;
            public string assetSha256;
            public string metaSha256;
            public string dependencyHash;
            public bool assetDirty;
            public string[] resourceCandidates;
            public bool cachedPresent;
            public string cachedIdentity;
            public ulong cachedInstanceId;
            public bool selectedAppIdValid;
            public string changeCallbacksSha256;
            [NonSerialized, Newtonsoft.Json.JsonIgnore] public Object assetReference;
            [NonSerialized, Newtonsoft.Json.JsonIgnore] public Object cachedReference;
        }

        /// <summary>正式 Plan 直接只读加载唯一既有资产；完整 DLL 证明无 Awake/OnEnable/OnValidate/序列化回调。</summary>
        /// <param name="inputs">声明准确自然绑定 marker。</param>
        /// <param name="writes">声明 singleton 正常绑定和固定 warning，不声明 SDK 登录或外发。</param>
        /// <param name="token">已有资产文件哈希取消令牌。</param>
        internal static void Prepare(ICollection<string> inputs, ISet<string> writes, CancellationToken token)
        {
            Type type = SettingsType();
            string[] candidates = ResourceCandidates();
            if (candidates.Length != 1 || AssetDatabase.GetMainAssetTypeAtPath(candidates[0]) != type)
                throw new InvalidOperationException("Facebook Resources key 必须唯一且为已审准确 Settings 主资产；不创建或选择未知冲突对象。");
            Object before = Cached(type);
            string file = FileHash(candidates[0], token), meta = FileHash(candidates[0] + ".meta", token);
            Object asset = AssetDatabase.LoadAssetAtPath(candidates[0], type);
            if (asset == null || asset.GetType() != type || !ReferenceEquals(before, Cached(type)) || file != FileHash(candidates[0], token) || meta != FileHash(candidates[0] + ".meta", token))
                throw new InvalidOperationException("Facebook 只读资产 Load 的类型、singleton 或原文件发生未知变化。");
            Snapshot snapshot = CaptureLoaded(type, candidates, asset, token);
            if (snapshot.cachedPresent && !ReferenceEquals(snapshot.cachedReference, asset)) throw new InvalidOperationException("Facebook singleton 已归属另一配置；不抢占或重置。");
            inputs.Add(Marker);
            writes.Add("FacebookSettings.instance: official WebGL callback may naturally bind null to the unique existing Resources/FacebookSettings asset; no manual CreateInstance/getter, settings mutation, login or network call; unexpected official missing-resource fallback is retained as partial failure, not accepted");
            if (!snapshot.selectedAppIdValid)
                writes.Add("Unity Console: official fixed Facebook app ID missing warning only; no AppId/ClientToken value emitted");
        }

        /// <summary>仅捕获当前已加载的同一 Resources 资产和真实 singleton，不重载以制造执行后绑定。</summary>
        /// <param name="inputs">准确回调 marker。</param>
        /// <param name="token">文件哈希取消令牌。</param>
        /// <returns>真实 before/after，未涉及此回调时为明确 inactive。</returns>
        internal static Snapshot Capture(IEnumerable<string> inputs, CancellationToken token)
        {
            if (!inputs.Contains(Marker, StringComparer.Ordinal)) return new Snapshot();
            Type type = SettingsType();
            string[] paths = ResourceCandidates();
            if (paths.Length != 1 || AssetDatabase.GetMainAssetTypeAtPath(paths[0]) != type)
                throw new InvalidOperationException("Facebook Resources 配置候选集或主类型变化。");
            Object asset = Resources.FindObjectsOfTypeAll(type).SingleOrDefault(value => AssetDatabase.GetAssetPath(value) == paths[0]);
            if (asset == null) throw new InvalidOperationException("Facebook 已批准配置不再加载；不调用 getter/Load 来伪造 after。");
            return CaptureLoaded(type, paths, asset, token);
        }

        /// <summary>仅将已声明 singleton 绑定投影为准确既有资产，其他字段和完整候选集均保留。</summary>
        /// <param name="snapshot">实际 before/after。</param>
        /// <returns>独立副本不变量 hash，不修改原材料。</returns>
        internal static string InvariantHash(Snapshot snapshot)
        {
            if (snapshot == null) throw new InvalidOperationException("Facebook 绑定材料缺失。");
            Snapshot clone = NovaFramework.Runtime.Util.Json.Deserialize<Snapshot>(NovaFramework.Runtime.Util.Json.Serialize(snapshot));
            if (clone.applicable)
            {
                clone.cachedPresent = true;
                clone.cachedIdentity = clone.assetIdentity;
                clone.cachedInstanceId = clone.assetInstanceId;
            }
            return GenerateActionCommon.ComputeTextHash(NovaFramework.Runtime.Util.Json.Serialize(clone));
        }

        /// <summary>完整真实 before 和当前域对象引用写前相等，不能用自然绑定投影跳过用户新改动。</summary>
        /// <param name="expected">Plan 保留的实际状态。</param>
        /// <param name="current">即将正式构建的当前状态。</param>
        /// <returns>真实引用和值全部吻合时返回 true。</returns>
        internal static bool BeforeEquals(Snapshot expected, Snapshot current)
        {
            return expected != null && current != null && ReferenceEquals(expected.assetReference, current.assetReference) && ReferenceEquals(expected.cachedReference, current.cachedReference) &&
                NovaFramework.Runtime.Util.Json.Serialize(expected) == NovaFramework.Runtime.Util.Json.Serialize(current);
        }

        /// <summary>正式链后必须实际绑定到唯一原资产；原文件、JSON、dirty、候选集和未调用的变更委托保持不变。</summary>
        /// <param name="before">原 Plan 或回执实际 before。</param>
        /// <param name="after">正式链后实际读回。</param>
        /// <param name="current">Dispose/Verify 当前材料。</param>
        /// <param name="error">缺失、漂移或绑定失败的原因。</param>
        /// <returns>原不变量与准确实际绑定后验全满足时返回 true。</returns>
        internal static bool TryValidateAfter(Snapshot before, Snapshot after, Snapshot current, out string error)
        {
            error = null;
            if (before == null || after == null || current == null) error = "Facebook before/after/current 材料缺失。";
            else if (InvariantHash(before) != InvariantHash(after) || InvariantHash(after) != InvariantHash(current)) error = "Facebook 配置、候选集或其他 singleton 输入漂移。";
            else if (before.applicable && (before.cachedPresent && (before.cachedIdentity != before.assetIdentity || before.cachedInstanceId != before.assetInstanceId) || !after.cachedPresent || !current.cachedPresent || after.cachedIdentity != after.assetIdentity || current.cachedIdentity != current.assetIdentity ||
                after.cachedInstanceId != after.assetInstanceId || current.cachedInstanceId != current.assetInstanceId ||
                before.assetReference != null && !ReferenceEquals(before.assetReference, after.assetReference) ||
                before.cachedReference != null && !ReferenceEquals(before.cachedReference, after.cachedReference) ||
                after.cachedReference != null && !ReferenceEquals(after.cachedReference, current.cachedReference))) error = "Facebook 正常回调没有绑定到本轮准确原配置；不重放或补设置。";
            return error == null;
        }

        /// <summary>从准确对象的 backing 读取 IsValidAppId 所需字段，只输出有效性和整体摘要。</summary>
        /// <param name="type">完整 DLL 已审的准确 Settings 类型。</param>
        /// <param name="paths">唯一 Resources 候选。</param>
        /// <param name="asset">已合法加载的原对象。</param>
        /// <param name="token">文件哈希取消令牌。</param>
        /// <returns>不会泄露 ID/Token 原文的真实状态。</returns>
        private static Snapshot CaptureLoaded(Type type, string[] paths, Object asset, CancellationToken token)
        {
            if (asset.GetType() != type || !EditorUtility.IsPersistent(asset)) throw new InvalidOperationException("Facebook 仅支持准确已有持久配置。");
            Object cached = Cached(type);
            FieldInfo indexField = type.GetField("selectedAppIndex", InstanceFields), idsField = type.GetField("appIds", InstanceFields);
            if (indexField?.DeclaringType != type || indexField.FieldType != typeof(int) || idsField?.DeclaringType != type || idsField.FieldType != typeof(List<string>))
                throw new InvalidOperationException("Facebook AppId getter 所需 backing 身份改变。");
            var ids = idsField.GetValue(asset) as List<string>;
            int index = (int)indexField.GetValue(asset);
            if (ids == null || index < 0 || index >= ids.Count || ids[index] == null) throw new InvalidOperationException("Facebook 原选择索引/列表为空或越界，官方 getter 会抛异常；不补业务配置。");
            string selected = ids[index].Trim();
            var callbacks = type.GetField("onChangeCallbacks", StaticFields)?.GetValue(null) as IEnumerable;
            if (callbacks == null) throw new InvalidOperationException("Facebook 纯 static 回调表身份变化。");
            var callbackRows = new List<string>();
            foreach (object value in callbacks)
            {
                if (value is not Delegate callback) throw new InvalidOperationException("Facebook 变更表元素类型改变。");
                foreach (Delegate entry in callback.GetInvocationList()) callbackRows.Add(entry.Method.Module.ModuleVersionId.ToString("D") + "|" + entry.Method.MetadataToken + "|" + (entry.Target == null ? 0 : RuntimeHelpers.GetHashCode(entry.Target)));
            }
            return new Snapshot
            {
                applicable = true, assetPath = paths[0], assetIdentity = Identity(asset), assetInstanceId = UnityEngine.EntityId.ToULong(asset.GetEntityId()), assetReference = asset,
                assetJsonSha256 = GenerateActionCommon.ComputeTextHash(EditorJsonUtility.ToJson(asset)), assetSha256 = FileHash(paths[0], token), metaSha256 = FileHash(paths[0] + ".meta", token),
                dependencyHash = AssetDatabase.GetAssetDependencyHash(paths[0]).ToString(), assetDirty = EditorUtility.IsDirty(asset), resourceCandidates = paths,
                cachedPresent = cached != null, cachedIdentity = Identity(cached), cachedInstanceId = cached == null ? 0 : UnityEngine.EntityId.ToULong(cached.GetEntityId()), cachedReference = cached,
                selectedAppIdValid = selected.Length != 0 && selected != "0", changeCallbacksSha256 = GenerateActionCommon.ComputeTextHash(string.Join("\n", callbackRows)),
            };
        }

        /// <summary>冻结 Resources.Load 非泛型的完整同名 key 候选，不只搜索 Settings 类型以漏掉类型冲突。</summary>
        /// <returns>以准确资源 key 归属的所有主资产路径。</returns>
        private static string[] ResourceCandidates() => AssetDatabase.GetAllAssetPaths().Where(IsSettingsResourcePath).Where(path => !AssetDatabase.IsValidFolder(path)).OrderBy(path => path, StringComparer.Ordinal).ToArray();

        /// <summary>匹配不带扩展名的 Resources key，兼顾多层 Resources 与大小写冲突。</summary>
        /// <param name="path">AssetDatabase 实际路径。</param>
        /// <returns>可能被 Resources.Load("FacebookSettings") 消费时返回 true。</returns>
        internal static bool IsSettingsResourcePath(string path)
        {
            if (string.IsNullOrEmpty(path)) return false;
            int root = path.LastIndexOf("/Resources/", StringComparison.OrdinalIgnoreCase);
            if (root < 0) return false;
            string relative = path.Substring(root + "/Resources/".Length);
            return relative.IndexOf('/') < 0 && string.Equals(Path.GetFileNameWithoutExtension(relative), "FacebookSettings", StringComparison.OrdinalIgnoreCase);
        }

        /// <summary>只按准确全名定位当前已加载 Settings 类型，不加载程序集。</summary>
        /// <returns>唯一已加载类型。</returns>
        private static Type SettingsType() => AppDomain.CurrentDomain.GetAssemblies().Select(assembly => assembly.GetType("Facebook.Unity.Settings.FacebookSettings", false)).Where(type => type != null).SingleOrDefault() ?? throw new InvalidOperationException("Facebook Settings 类型未加载。");

        /// <summary>读取已审纯 static backing；不调用 NullableInstance/Instance/Resources.Load。</summary>
        /// <param name="type">准确 Settings 类型。</param>
        /// <returns>真实 cached instance 或 null。</returns>
        private static Object Cached(Type type)
        {
            FieldInfo field = type.GetField("instance", StaticFields);
            if (field?.DeclaringType != type || field.FieldType != type) throw new InvalidOperationException("Facebook singleton backing 身份变化。");
            return field.GetValue(null) as Object;
        }

        /// <summary>对象身份使用稳定 GUID/fileID，暂存对象不得冒作持久配置。</summary>
        /// <param name="value">原对象或 null。</param>
        /// <returns>没有配置正文的身份。</returns>
        private static string Identity(Object value) => value == null ? "null" : AssetDatabase.TryGetGUIDAndLocalFileIdentifier(value, out string guid, out long fileId) ? value.GetType().FullName + "|" + guid + "|" + fileId + "|" + AssetDatabase.GetAssetPath(value) : value.GetType().FullName + "|nonpersistent|" + UnityEngine.EntityId.ToULong(value.GetEntityId());

        /// <summary>仅经 Unity 映射读取已有资产或 meta 摘要。</summary>
        /// <param name="path">准确 Unity 资产路径。</param>
        /// <param name="token">取消令牌。</param>
        /// <returns>原文件 SHA。</returns>
        private static string FileHash(string path, CancellationToken token)
        {
            string physical = FileUtil.GetPhysicalPath(path);
            return GenerateActionCommon.ComputeFileHash(Path.GetFullPath(Path.IsPathRooted(physical) ? physical : Path.Combine(BuildActionCommon.ProjectRoot, string.IsNullOrEmpty(physical) ? path : physical)), token);
        }
    }
}
