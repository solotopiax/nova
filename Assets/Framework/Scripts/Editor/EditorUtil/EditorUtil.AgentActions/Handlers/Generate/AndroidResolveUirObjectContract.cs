/***************************************************************
 * (c) copyright 2026 - 2030, Solotopia
 * All Rights Reserved.
 * -------------------------------------------------------------
 * filename:  AndroidResolveUirObjectContract.cs
 * author:    Codex
 * created:   2026/10/22
 * descrip:   精确绑定当前编辑器 UIR 派生资源及动态图集复制材质的只读归属合同
 ***************************************************************/
using System;
using System.Collections;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Runtime.CompilerServices;
using System.Text.RegularExpressions;
using System.Threading;
using NovaFramework.Runtime;
using UnityEditor;
using UnityEngine;
using UnityEngine.UIElements;
using Object = UnityEngine.Object;
using Path = System.IO.Path;

namespace NovaFramework.Editor
{
    /// <summary>只从已存在的 EditorPanel 字段识别有限派生资源及 Atlas Blit 子材质；同名用户对象和所有持久资产仍严格保护。</summary>
    internal static class AndroidResolveUirObjectContract
    {
        internal const string EngineHash = "a6e2aa385f64e530211e441ea7e877a953d08c6aad41c32bafa0ab37e11b0160";
        internal const string EngineMvid = "17d16488-848a-4256-9cbb-65d74bf2f5b3";
        internal const string EditorHash = "9bb68962a197695dc9cf12c37c38b3f5679304c6496e4655d6287319bd8b8b10";
        internal const string EditorMvid = "d572b22f-1d90-4477-9407-b36592117da9";
        private const string Prefix = "UnityEngine.UIElements.";
        private const BindingFlags Fields = BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.DeclaredOnly;
        private static readonly string s_Domain = Guid.NewGuid().ToString("N");
        private static readonly ConditionalWeakTable<object, Identity> s_Identities = new ConditionalWeakTable<object, Identity>();

        /// <summary>只为现存托管对象分配同域弱身份，不调用其业务相等、哈希或序列化方法。</summary>
        private sealed class Identity { internal readonly string value = Guid.NewGuid().ToString("N"); }

        /// <summary>当前合同域标识；用于同域自洽检查与纯 DTO 定向回归。</summary>
        internal static string CurrentDomain => s_Domain;

        /// <summary>有限角色事实；保留真实 dirty，名称只作字段归属证明之后的结构辅证。</summary>
        [Serializable]
        internal sealed class OwnedObject
        {
            public string id;
            public string role;
            public string owner;
            public string type;
            public string name;
            public int hideFlags;
            public bool dirty;
            public string shape;
        }

        /// <summary>保留模块、面板根及用户输入身份；不保存 Unity 引用，不替换原始全对象行。</summary>
        [Serializable]
        internal sealed class Snapshot
        {
            public int version = 1;
            public string domain;
            public string engineHash;
            public string engineMvid;
            public string editorHash;
            public string editorMvid;
            public string[] panelRows;
            public string[] inputRows;
            public OwnedObject[] ownedObjects;
            public string sha256;
        }

        /// <summary>在正常初始化后的稳定主线程读取既存面板字段；不创建面板、不调用 UIR 惰性 getter 或重建入口。</summary>
        internal static bool TryCapture(CancellationToken token, out Snapshot snapshot, out string error)
        {
            snapshot = null;
            error = null;
            try
            {
                token.ThrowIfCancellationRequested();
                Require(Application.unityVersion == "6000.4.2f1" && !EditorApplication.isCompiling &&
                    !EditorApplication.isUpdating && !EditorApplication.isPlayingOrWillChangePlaymode, "UIR 合同要求当前版本稳定编辑态。");
                Assembly engine = typeof(VisualElement).Assembly;
                Assembly editor = AppDomain.CurrentDomain.GetAssemblies().Single(a => a.GetName().Name == "UnityEditor.UIElementsModule");
                VerifyModule(engine, "UnityEngine.UIElementsModule.dll", EngineHash, EngineMvid, token);
                VerifyModule(editor, "UnityEditor.UIElementsModule.dll", EditorHash, EditorMvid, token);
                Type panelType = editor.GetType("UnityEditor.UIElements.EditorPanel", true);
                Require(panelType.BaseType == engine.GetType(Prefix + "Panel", true), "EditorPanel 继承归属不符。");
                // 当前正常 Editor 初始化已建立缓存；该类型静态初始化只建立空容器，不可能凭此读取创造面板/manager。
                // 非空且每项均有既存 renderer/manager/allocator 才接受；空域不调用任何修复或初始化方法。
                object cacheValue = ReadStatic(engine.GetType(Prefix + "UIElementsUtility", true), "s_UIElementsCache");
                Require(cacheValue is IDictionary && cacheValue.GetType().IsGenericType &&
                    cacheValue.GetType().GetGenericTypeDefinition() == typeof(Dictionary<,>), "UIR panel cache 类型不符。");
                var cache = (IDictionary)cacheValue;
                Require(cache.Count > 0, "UI Toolkit 尚无既存面板初始化见证。");
                var panels = new List<string>();
                var inputs = new List<string>();
                var owned = new List<OwnedObject>();
                var managers = new List<object>();
                foreach (DictionaryEntry entry in cache)
                {
                    token.ThrowIfCancellationRequested();
                    object panel = entry.Value;
                    Require(panel?.GetType() == panelType && entry.Key?.GetType() == typeof(EntityId), "UIR cache 含未知面板或 key。");
                    object renderer = Read(panel, "panelRenderer");
                    object atlas = Read(panel, "m_Atlas");
                    RequireType(renderer, engine, "UIRRepaintUpdater");
                    RequireType(atlas, engine, "DynamicAtlas");
                    object manager = Read(renderer, "renderTreeManager");
                    RequireType(manager, engine, "UIR.RenderTreeManager");
                    object allocator = Read(manager, "shaderInfoAllocator");
                    RequireType(allocator, engine, "UIR.UIRVEShaderInfoAllocator");
                    managers.Add(manager);
                    string root = "panel=" + entry.Key + "|root=" + IdentityOf(panel);
                    panels.Add(root + "|renderer=" + IdentityOf(renderer) + "|atlas=" + IdentityOf(atlas));
                    var database = Read(atlas, "m_Database") as IDictionary;
                    Require(database != null && database.GetType().GetGenericTypeDefinition() == typeof(Dictionary<,>), "动态图集输入映射不符。");
                    foreach (DictionaryEntry input in database)
                    {
                        Require(input.Key is Texture2D && (Object)input.Key != null, "动态图集含未知输入资源。");
                        // 用户源纹理只记录为受保护输入，不作为任何豁免角色。
                        var texture = (Object)input.Key;
                        inputs.Add(root + "|input=" + texture.GetEntityId() + "|" + texture.GetType().FullName);
                    }
                    foreach (string slot in new[] { "point", "bilinear" })
                    {
                        object page = Read(atlas, slot == "point" ? "m_PointPage" : "m_BilinearPage");
                        if (page == null) continue;
                        RequireType(page, engine, "DynamicAtlasPage");
                        AddOwned(owned, Read(page, "<atlas>k__BackingField") as Object, "dynamic-atlas", root + "|slot=" + slot);
                        // 仅认同一既存 Atlas 页的私有 blitter 材质；不调用 BeginBlit 来制造惰性资源。
                        object blitter = Read(page, "m_Blitter");
                        RequireType(blitter, engine, "UIR.TextureBlitter");
                        Require(Read(blitter, "<disposed>k__BackingField") is bool disposed && !disposed, "Atlas Blit owner 已释放。");
                        AddOwned(owned, Read(blitter, "m_BlitMaterial") as Object, "atlas-blit-material", root + "|slot=" + slot + "-blit");
                    }
                    object storage = Read(allocator, "m_Storage");
                    if (storage != null)
                    {
                        RequireType(storage, engine, "UIR.ShaderInfoStorageRGBAFloat");
                        AddOwned(owned, Read(storage, "m_Texture") as Object, "shader-info-storage", root + "|slot=storage");
                    }
                }
                // 上述已存在的 manager/allocator 构造已必经这两个静态 owner，不以反射触发其冷初始化。
                Type shaders = engine.GetType(Prefix + "UIR.Shaders", true);
                Type allocatorType = engine.GetType(Prefix + "UIR.UIRVEShaderInfoAllocator", true);
                var material = ReadStatic(shaders, "s_DefaultMaterial") as Object;
                var defaultTexture = ReadStatic(allocatorType, "s_DefaultShaderInfoTexture") as Object;
                Require(material != null && defaultTexture != null && (int)ReadStatic(shaders, "s_RefCount") > 0 &&
                    (int)ReadStatic(allocatorType, "s_DefaultShaderInfoTextureRefCount") > 0, "UIR 共享资源初始化不完整。");
                Require(managers.All(manager => ReferenceEquals(Read(manager, "m_DefaultMat"), material)), "UIR 默认材质别名不一致。");
                AddOwned(owned, material, "default-material", "global|slot=default-material");
                AddOwned(owned, defaultTexture, "default-shader-info", "global|slot=default-shader-info");
                snapshot = new Snapshot { domain = s_Domain, engineHash = EngineHash, engineMvid = EngineMvid,
                    editorHash = EditorHash, editorMvid = EditorMvid, panelRows = panels.OrderBy(x => x, StringComparer.Ordinal).ToArray(),
                    inputRows = inputs.OrderBy(x => x, StringComparer.Ordinal).ToArray(),
                    ownedObjects = owned.OrderBy(RoleKey, StringComparer.Ordinal).ToArray() };
                snapshot.sha256 = ComputeHash(snapshot);
                Require(IsValid(snapshot), "UIR 角色重复、输入重叠或事实结构不完整。");
                return true;
            }
            catch (Exception exception) when (!(exception is OperationCanceledException))
            {
                snapshot = null;
                error = "UIR 只读归属未闭合：" + exception.Message;
                return false;
            }
        }

        /// <summary>纯 DTO 比较模块、面板、输入及角色；写前完整相等，写后只接受同角色且 dirty/形状保持的替换。</summary>
        internal static bool TryValidateTransition(Snapshot before, Snapshot after, bool allowRebuild, out string error)
        {
            error = null;
            if (!IsValid(before) || !IsValid(after)) error = "UIR 快照缺失、跨域或结构/摘要无效；不补造旧许可。";
            else if (!before.panelRows.SequenceEqual(after.panelRows) || !before.inputRows.SequenceEqual(after.inputRows)) error = "UIR 面板根或用户输入身份变化。";
            else if (!allowRebuild && before.sha256 != after.sha256) error = "UIR 写前完整状态变化。";
            else if (before.ownedObjects.Length != after.ownedObjects.Length) error = "UIR 角色数量变化。";
            else
            {
                for (int i = 0; i < before.ownedObjects.Length; i++)
                {
                    OwnedObject old = before.ownedObjects[i], current = after.ownedObjects[i];
                    if (RoleKey(old) != RoleKey(current) || old.type != current.type || old.hideFlags != current.hideFlags ||
                        old.dirty != current.dirty || old.shape != current.shape || old.id == current.id && old.name != current.name)
                    { error = "UIR owner、类型、dirty 或资源形状变化。"; break; }
                }
            }
            return error == null;
        }

        /// <summary>只生成比较副本；每个角色须在完整原行恰好出现一次，原始收据材料保持不变。</summary>
        internal static bool TryGetComparableRows(Snapshot before, Snapshot after, string[] beforeObjectRows,
            string[] afterObjectRows, bool allowRebuild, out string[] beforeComparable, out string[] afterComparable, out string error)
        {
            beforeComparable = beforeObjectRows;
            afterComparable = afterObjectRows;
            if (!TryValidateTransition(before, after, allowRebuild, out error)) return false;
            if (!TryExtract(before, beforeObjectRows, out beforeComparable) || !TryExtract(after, afterObjectRows, out afterComparable))
            { error = "UIR 角色与原始完整对象行不一致。"; return false; }
            return true;
        }

        /// <summary>形成确定性完整事实摘要；不含摘要字段自身，不访问运行时对象。</summary>
        internal static string ComputeHash(Snapshot value) => GenerateActionCommon.ComputeTextHash(Util.Json.Serialize(new
        { value.version, value.domain, value.engineHash, value.engineMvid, value.editorHash, value.editorMvid, value.panelRows, value.inputRows, value.ownedObjects }));

        /// <summary>确认真实运行模块和本机标准组件位置均等于已审字节；不缓存磁盘身份或加载程序集。</summary>
        private static void VerifyModule(Assembly assembly, string filename, string hash, string mvid, CancellationToken token)
        {
            string expected = Path.GetFullPath(Path.Combine(EditorApplication.applicationContentsPath, "Resources/Scripting/Managed/UnityEngine", filename));
            Require(Path.GetFullPath(assembly.Location) == expected && assembly.ManifestModule.ModuleVersionId.ToString() == mvid &&
                GenerateActionCommon.ComputeFileHash(expected, token) == hash, "UIR 实际模块/DLL/MVID 漂移：" + filename);
        }

        /// <summary>对已被字段直接持有的对象核对非持久化、原生类型和有限创建形状，绝不以名称先行筛选。</summary>
        private static void AddOwned(List<OwnedObject> values, Object target, string role, string owner)
        {
            if (target == null) return;
            Require(!EditorUtility.IsPersistent(target) && string.IsNullOrEmpty(AssetDatabase.GetAssetPath(target)), "UIR 角色命中持久化或资产资源。");
            string shape;
            if (role == "default-material" || role == "atlas-blit-material")
            {
                Require(target.GetType() == typeof(Material), "UIR 派生材质原生类型不符。");
                Shader shader = ((Material)target).shader; // 固定原生只读元数据，不调用 Shaders.defaultMaterial 惰性 getter。
                string expectedShader = role == "atlas-blit-material" ? "Hidden/Internal-UIRAtlasBlitCopy" : "Hidden/Internal-UIRDefault";
                Require(shader != null && shader.name == expectedShader, "UIR 派生材质 shader 不符。");
                shape = "shader=" + shader.GetEntityId();
            }
            else if (role == "dynamic-atlas")
            {
                Require(target.GetType() == typeof(RenderTexture), "UIR 动态页原生类型不符。");
                var texture = (RenderTexture)target;
                Require(texture.width > 0 && texture.height > 0 && texture.depth == 0 && texture.format == RenderTextureFormat.ARGB32 &&
                    !texture.useMipMap && texture.filterMode == (owner.EndsWith("=point", StringComparison.Ordinal) ? FilterMode.Point : FilterMode.Bilinear), "UIR 动态页形状不符。");
                shape = texture.width + "x" + texture.height + "|ARGB32|depth0|" + texture.filterMode;
            }
            else
            {
                Require(target.GetType() == typeof(Texture2D), "UIR shader info 原生类型不符。");
                var texture = (Texture2D)target;
                Require(texture.width > 0 && texture.height > 0 && texture.format == TextureFormat.RGBAFloat &&
                    texture.filterMode == FilterMode.Point && texture.mipmapCount == 1, "UIR shader info 形状不符。");
                if (role == "default-shader-info") Require(texture.width == 64 && texture.height == 64, "UIR 默认纹理尺寸不符。");
                shape = texture.width + "x" + texture.height + "|RGBAFloat|Point|mip1";
            }
            var value = new OwnedObject { id = target.GetEntityId().ToString(), role = role, owner = owner, type = target.GetType().FullName,
                name = target.name, hideFlags = (int)target.hideFlags, dirty = EditorUtility.IsDirty(target), shape = shape };
            Require(ValidOwned(value), "UIR 角色元数据不属于已审创建分支。");
            values.Add(value);
        }

        /// <summary>自洽检查拒绝未知角色、重复身份、跨面板 owner 和用户输入复用，不将摘要视为真实运行归属的替代。</summary>
        private static bool IsValid(Snapshot value)
        {
            if (value == null || value.version != 1 || value.domain != s_Domain || value.engineHash != EngineHash || value.engineMvid != EngineMvid ||
                value.editorHash != EditorHash || value.editorMvid != EditorMvid || value.panelRows == null || value.panelRows.Length == 0 || value.inputRows == null ||
                value.ownedObjects == null || value.ownedObjects.Any(x => x == null || !ValidOwned(x)) ||
                value.panelRows.Any(string.IsNullOrEmpty) || value.inputRows.Any(string.IsNullOrEmpty) ||
                value.panelRows.Distinct(StringComparer.Ordinal).Count() != value.panelRows.Length || value.inputRows.Distinct(StringComparer.Ordinal).Count() != value.inputRows.Length ||
                value.ownedObjects.Select(x => x.id).Distinct(StringComparer.Ordinal).Count() != value.ownedObjects.Length ||
                value.ownedObjects.Select(RoleKey).Distinct(StringComparer.Ordinal).Count() != value.ownedObjects.Length ||
                !value.ownedObjects.Select(RoleKey).SequenceEqual(value.ownedObjects.Select(RoleKey).OrderBy(x => x, StringComparer.Ordinal)) ||
                value.ownedObjects.Count(x => x.role == "default-material") != 1 || value.ownedObjects.Count(x => x.role == "default-shader-info") != 1) return false;
            foreach (OwnedObject owned in value.ownedObjects)
            {
                if (owned.owner.StartsWith("panel=", StringComparison.Ordinal))
                {
                    string root = owned.owner.Substring(0, owned.owner.LastIndexOf("|slot=", StringComparison.Ordinal));
                    if (!value.panelRows.Any(row => row.StartsWith(root + "|renderer=", StringComparison.Ordinal))) return false;
                }
                if (owned.role == "atlas-blit-material" && !value.ownedObjects.Any(parent => parent.role == "dynamic-atlas" &&
                    parent.owner == owned.owner.Substring(0, owned.owner.Length - "-blit".Length))) return false;
                if (value.inputRows.Any(row => row.Contains("|input=" + owned.id + "|"))) return false;
            }
            return value.sha256 == ComputeHash(value);
        }

        /// <summary>验证有限角色的纯元数据；调用者还须有实际字段引用证明，不能把同名对象填入角色。</summary>
        private static bool ValidOwned(OwnedObject value)
        {
            if (value == null || !long.TryParse(value.id, NumberStyles.Integer, CultureInfo.InvariantCulture, out long id) || id == 0 ||
                id.ToString(CultureInfo.InvariantCulture) != value.id || value.name == null || value.name.Length > 128 || string.IsNullOrEmpty(value.shape)) return false;
            bool panelOwner = Regex.IsMatch(value.owner ?? string.Empty, @"\Apanel=-?[0-9]+\|root=[a-f0-9]{32}\|slot=(?:point|bilinear|storage)\z", RegexOptions.CultureInvariant);
            bool blitOwner = Regex.IsMatch(value.owner ?? string.Empty, @"\Apanel=-?[0-9]+\|root=[a-f0-9]{32}\|slot=(?:point-blit|bilinear-blit)\z", RegexOptions.CultureInvariant);
            switch (value.role)
            {
                case "dynamic-atlas": return panelOwner && !value.owner.EndsWith("=storage", StringComparison.Ordinal) && value.type == typeof(RenderTexture).FullName &&
                    value.hideFlags == (int)HideFlags.HideAndDontSave && Regex.IsMatch(value.name, @"\AUIR Dynamic Atlas Page [0-9]+\z", RegexOptions.CultureInvariant);
                case "shader-info-storage": return panelOwner && value.owner.EndsWith("=storage", StringComparison.Ordinal) && value.type == typeof(Texture2D).FullName &&
                    value.hideFlags == (int)HideFlags.HideAndDontSave && Regex.IsMatch(value.name, @"\AUIR Shader Info [0-9]+\z", RegexOptions.CultureInvariant);
                case "default-shader-info": return value.owner == "global|slot=default-shader-info" && value.type == typeof(Texture2D).FullName &&
                    value.hideFlags == (int)HideFlags.HideAndDontSave && value.name == "DefaultShaderInfoTexFloat";
                case "default-material": return value.owner == "global|slot=default-material" && value.type == typeof(Material).FullName &&
                    value.hideFlags == (int)HideFlags.DontSave && value.name == "Hidden/Internal-UIRDefault";
                case "atlas-blit-material": return blitOwner && value.type == typeof(Material).FullName &&
                    value.hideFlags == (int)HideFlags.DontSaveInEditor && value.name == "Hidden/Internal-UIRAtlasBlitCopy";
                default: return false;
            }
        }

        /// <summary>每个角色须在原始全量行中恰好一次；只移除比较副本中的该准确行。</summary>
        private static bool TryExtract(Snapshot snapshot, string[] rows, out string[] comparable)
        {
            comparable = rows;
            if (rows == null) return false;
            var roleRows = new HashSet<string>(snapshot.ownedObjects.Select(x => x.id + "|" + x.type + "|dirty=" + x.dirty), StringComparer.Ordinal);
            if (rows.Count(roleRows.Contains) != roleRows.Count || rows.Where(roleRows.Contains).Distinct(StringComparer.Ordinal).Count() != roleRows.Count) return false;
            comparable = rows.Where(row => !roleRows.Contains(row)).ToArray();
            return true;
        }

        /// <summary>返回有限角色的稳定 owner 键，不使用随重建变化的 manager 或资源身份。</summary>
        private static string RoleKey(OwnedObject value) => value.owner + "|role=" + value.role;

        /// <summary>只沿声明字段读取当前实例，不回退 getter 或执行业务方法。</summary>
        private static object Read(object target, string name)
        {
            Require(target != null, "UIR 字段 owner 不存在。");
            for (Type type = target.GetType(); type != null; type = type.BaseType)
            {
                FieldInfo field = type.GetField(name, Fields | BindingFlags.Instance);
                if (field != null) return field.GetValue(target);
            }
            throw new MissingFieldException(target.GetType().FullName, name);
        }

        /// <summary>仅由已完成正常初始化的固定 owner 调用；不访问属性或运行类构造器。</summary>
        private static object ReadStatic(Type type, string name) => type.GetField(name, Fields | BindingFlags.Static)?.GetValue(null) ??
            throw new MissingFieldException(type.FullName, name);

        /// <summary>实际实例必须来自当前已绑定引擎模块中的精确类型，不以同名类型替代。</summary>
        private static void RequireType(object value, Assembly engine, string name) => Require(value?.GetType() == engine.GetType(Prefix + name, true), "UIR owner 类型不符：" + name);

        /// <summary>取得仅属于当前合同域的弱身份，不持有调用方资源的强引用。</summary>
        private static string IdentityOf(object value) => s_Identities.GetValue(value, ignored => new Identity()).value;

        /// <summary>遇到未知事实立即拒绝，避免通过清缓存、改脏状态或初始化对象制造可接纳状态。</summary>
        private static void Require(bool condition, string error)
        {
            if (!condition) throw new InvalidOperationException(error);
        }
    }
}
