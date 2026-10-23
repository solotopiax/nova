/***************************************************************
 * (c) copyright 2026 - 2030, Solotopia
 * All Rights Reserved.
 * -------------------------------------------------------------
 * filename:  PlayerTmpFontBuildContract.cs
 * author:    taoye
 * created:   2026/10/16
 * descrip:   Player TMP 单 atlas 清理与已加载字体精确登记的有限输入和后验合同
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
    /// <summary>只观察官方 TMP 清理及明确已加载字体的有限登记；不清字体、不调用 FontEngine、不改厂商字段或恢复原生字体面。</summary>
    internal static class PlayerTmpFontBuildContract
    {
        internal const string Marker = "TMPNormalFont=";
        internal const string RegistrationMarker = "TMPRegistrationFontPath=";
        private static readonly string ObservationDomain = Guid.NewGuid().ToString("N");
        private const BindingFlags InstanceFields = BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance;
        private const BindingFlags StaticFields = BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Static;

        /// <summary>固定字体与 atlas 的持久身份、正文和真正观察到的缓存/脏标志。</summary>
        [Serializable]
        internal sealed class FontState
        {
            public string path;
            public ulong fontInstanceId;
            public ulong atlasInstanceId;
            public ulong sourceInstanceId;
            public string fontIdentity;
            public string fontJsonSha256;
            public string assetSha256;
            public string metaSha256;
            public string sourceIdentity;
            public string sourceSha256;
            public string sourceMetaSha256;
            public string dependencyHash;
            public string atlasIdentity;
            public string atlasRawSha256;
            public bool fontDirty;
            public bool atlasDirty;
            public bool atlasBackingPresent;
            public bool lookupTablesDirty;
            public bool glyphLookupPresent;
            public string characterValuesSha256;
            public string scalarCacheSha256;
            public string[] characterKeys;
        }

        /// <summary>已审资源管理器、回调与缓存；仅明确允许的正常局部状态可被规整。</summary>
        [Serializable]
        internal sealed class RuntimeState
        {
            public bool managerPresent;
            public int managerIdentity;
            [NonSerialized, Newtonsoft.Json.JsonIgnore] public object managerReference;
            public int ownCameraCallbacks;
            public int ownCanvasCallbacks;
            public int ownRenderCallbacks;
            public string[] cameraBase;
            public string[] canvasBase;
            public string[] renderBase;
            public string[] updateQueue;
            public bool tmpProxyRefresh;
            public bool textCoreProxyRefresh;
            public PlayerTmpFontRegistrationContract.ResourceRow[] resourceRows;
            public PlayerTmpFontRegistrationContract.WeakRow[] weakRows;
            public string resourceCacheSha256;
            public bool resourceOwnRegistered;
            public string weakTargetsSha256;
            public bool weakOwnRegistered;
            public string settingsIdentity;
            public string settingsSha256;
            public string[] fontDelegates;
        }

        /// <summary>Plan 的真实 before 与构建后的真实 after；不携带原始字体、像素或配置正文。</summary>
        [Serializable]
        internal sealed class Snapshot
        {
            public FontState[] fonts;
            public PlayerTmpFontRegistrationContract.Font[] registrationFonts;
            public string registrationFilesSha256;
            public RuntimeState runtime;
            public string preparationDomain;
            public string preparationInputsSha256;
            public PlayerTmpFontObservationContract.PreparedFont[] preparationBindings;
            public PlayerTmpFontObservationContract.Observation[] observations;
        }

        /// <summary>合法加载已有资产并声明其已审生命周期；先阻止升级/重导入，再冻结正常清理候选。</summary>
        /// <param name="inputs">保留非清理字体输入与明确候选路径。</param>
        /// <param name="writes">声明计划加载和真正构建可能产生的准确正常内存效果。</param>
        /// <param name="token">只读文件哈希取消令牌。</param>
        internal static void Prepare(ICollection<string> inputs, ISet<string> writes, CancellationToken token)
        {
            Type type = TypeOf("TMPro.TMP_FontAsset");
            var markers = new List<string>();
            var preparationRows = new List<string>();
            foreach (string path in AssetDatabase.FindAssets("t:TMP_FontAsset").Select(AssetDatabase.GUIDToAssetPath).OrderBy(value => value, StringComparer.Ordinal))
            {
                Object font = LoadChecked(type, path, token);
                if (font == null) throw new InvalidOperationException("TMP 搜索结果不能加载为准确字体类型。" );
                int mode = Convert.ToInt32(Read(font, "m_AtlasPopulationMode"));
                bool clear = (bool)Read(font, "m_ClearDynamicDataOnBuild");
                if (mode != 1 && mode != 2 || !clear)
                {
                    string row = "TMPUnclearedFont=" + Identity(font) + "|" + ObjectHash(font);
                    inputs.Add(row); preparationRows.Add(row);
                    markers.Add(RegistrationMarker + path);
                    continue;
                }
                var atlases = Read(font, "m_AtlasTextures") as Texture2D[];
                var backing = Read(font, "m_AtlasTexture") as Texture2D;
                Texture2D actual = backing != null ? backing : atlases != null && atlases.Length > 0 ? atlases[0] : null;
                if (actual == null) throw new InvalidOperationException("TMP atlas getter 将空引用/越界；不主动修复。");
                if (actual.width == 0)
                {
                    string row = "TMPZeroWidthFont=" + Identity(font) + "|" + ObjectHash(font) + "|" + Identity(actual) + "|" + actual.width + "|" + actual.height;
                    inputs.Add(row); preparationRows.Add(row);
                    markers.Add(RegistrationMarker + path);
                    continue;
                }
                CaptureFont(font, path, token);
                markers.Add(Marker + path);
            }
            // 先完整验证再发布 marker，失败不会留下可被误认成已支持的半组候选。
            Capture(markers.Concat(preparationRows), token);
            foreach (string marker in markers) inputs.Add(marker);
            AddDeclaredWrites(writes, markers.Count != 0);
        }

        /// <summary>复用同一原写域文本；后态观察不再执行 Prepare 的 Load，但不改变已声明的正式链效果。</summary>
        /// <param name="writes">当前回调写集。</param>
        /// <param name="hasCandidates">原准备集合是否非空。</param>
        private static void AddDeclaredWrites(ISet<string> writes, bool hasCandidates)
        {
            writes.Add("TMP asset Load lifecycle (known version1.1.0/no feature import; managed lookup/resource caches and FontEngine current-face scratch; no save)");
            if (!hasCandidates) return;
            writes.Add("TMP exact already-loaded marked fonts: source-predicted hash initialization and monotonic ResourceManager map/first-dead weak registration; preserve original keys/winners/live slots and full nonclear font/source/atlas state; no strong reference retention or forced registration");
            writes.Add("TMP approved existing font/one readable1x1 Alpha8 atlas: official clear, lazy atlas reference, nonserialized lookup/control glyph caches; serialized font and asset/meta bytes preserved; native font/atlas may become dirty, preexisting dirty flags never cleared");
            writes.Add("TMP FontEngine.LoadFontFace/GetGlyphIndex/TryGetGlyphWithUnicodeValue/ResetAtlasTexture scratch (normal native memory effect; prior native face is not restored)");
            writes.Add("TMP exact EditorResourceManager normal singleton construction + own Camera/Canvas callbacks when absent; approved font Update queue and two proxy flags only; no reimport/refresh/SaveAssets");
            writes.Add("TMP Load/clear failure only: existing TMP_Settings getter may queue TMP_PackageResourceImporterWindow after1s; failed plan/build retains partial failure material and never claims async tail completion");
        }

        /// <summary>逐项核对冻结输入后只发布历史 baseline 行；实际 present/absent 保存在独立观察字段，不加载字体。</summary>
        /// <param name="inputs">本轮重新捕获的回调输入；只追加经过专用证明的准确 TMP 历史绑定。</param>
        /// <param name="writes">与原 Prepare 相同的已声明写域。</param>
        /// <param name="before">原 Plan 实际准备材料。</param>
        /// <param name="scopeAfter">真正 Scope after；后续调用必须提供，不能用较晚 current 替代。</param>
        /// <param name="token">当前磁盘与元数据核对取消令牌。</param>
        internal static void ObservePrepared(ICollection<string> inputs, ISet<string> writes, Snapshot before, Snapshot scopeAfter, CancellationToken token)
        {
            string[] historicalInputs = PlayerTmpFontObservationContract.ExpectedInputs(before?.preparationBindings);
            Snapshot actual = CaptureObserved(historicalInputs, before, scopeAfter, true, token);
            foreach (string row in historicalInputs) inputs.Add(row);
            AddDeclaredWrites(writes, actual.preparationBindings.Length != 0);
        }

        /// <summary>仅从已批准 marker 读取同一组真实对象和运行时状态；不调用 Clear 或任何 FontEngine API。</summary>
        /// <param name="inputs">明确 TMP 候选 marker 集合。</param>
        /// <param name="token">文件哈希取消令牌。</param>
        /// <returns>真实当前状态；没有候选时返回明确空组。</returns>
        internal static Snapshot Capture(IEnumerable<string> inputs, CancellationToken token) => CaptureCore(inputs, null, null, false, token);

        /// <summary>同域只观察原许可 marker 的现存对象；首次 Scope 可缺少 after，后续必须以真实 after 证明缺席。</summary>
        /// <param name="inputs">原明确 TMP marker 与历史分类行。</param>
        /// <param name="before">原 Prepare 实际完整快照。</param>
        /// <param name="scopeAfter">真正 Scope 结束材料；仅首次 Scope 采集允许为 null。</param>
        /// <param name="requireScope">回调后验/Verify 必须 true，首次 Scope 采集为 false。</param>
        /// <param name="token">当前磁盘与完整字体读取取消令牌。</param>
        /// <returns>本阶段真实 present/absent 与完整缓存，不填造已卸载对象。</returns>
        internal static Snapshot CaptureObserved(IEnumerable<string> inputs, Snapshot before, Snapshot scopeAfter, bool requireScope, CancellationToken token)
        {
            if (before == null || before.preparationDomain != ObservationDomain || before.preparationInputsSha256 == null || before.preparationBindings == null || before.observations == null)
                throw new InvalidOperationException("TMP 同域观察缺少原绑定或已跨域；新域规则尚未实证，保持 partial，不重新 Load。");
            if (requireScope && (scopeAfter == null || scopeAfter.preparationDomain != before.preparationDomain))
                throw new InvalidOperationException("TMP 后续观察缺少真正 Scope after；不以较晚 current 补造。");
            return CaptureCore(inputs, before, scopeAfter, requireScope, token);
        }

        /// <summary>统一完整采集实现；只有无冻结 before 的原准备路径建立 baseline，观察路径永不调用资产 Load。</summary>
        /// <param name="inputs">本次准确 TMP 输入与 marker。</param>
        /// <param name="before">观察时的原 Prepare baseline，建立 baseline 时为 null。</param>
        /// <param name="scopeAfter">真正 Scope 后态，首次 Scope 采集时为 null。</param>
        /// <param name="requireScope">后续观察是否必须具有原 Scope 缺席证明。</param>
        /// <param name="token">真实文件与字体读取取消令牌。</param>
        /// <returns>真实本阶段材料及明确历史准备绑定。</returns>
        private static Snapshot CaptureCore(IEnumerable<string> inputs, Snapshot before, Snapshot scopeAfter, bool requireScope, CancellationToken token)
        {
            if (inputs == null) throw new InvalidOperationException("TMP 输入 marker 缺失。");
            string[] material = inputs.ToArray();
            string[] paths = material.Where(value => value.StartsWith(Marker, StringComparison.Ordinal)).Select(value => value.Substring(Marker.Length)).Distinct(StringComparer.Ordinal).OrderBy(value => value, StringComparer.Ordinal).ToArray();
            string[] registrations = material.Where(value => value.StartsWith(RegistrationMarker, StringComparison.Ordinal)).Select(value => value.Substring(RegistrationMarker.Length)).Distinct(StringComparer.Ordinal).OrderBy(value => value, StringComparer.Ordinal).ToArray();
            if (paths.Length == 0 && registrations.Length == 0)
            {
                if (material.Any(IsPreparationInput)) throw new InvalidOperationException("TMP 有准备行但没有明确 marker；不丢弃未知或未绑定原行。");
                if (before != null && before.preparationBindings.Length != 0) throw new InvalidOperationException("TMP 原许可 marker 被移除。");
                return new Snapshot { fonts = Array.Empty<FontState>(), registrationFonts = Array.Empty<PlayerTmpFontRegistrationContract.Font>(),
                    preparationDomain = ObservationDomain, preparationInputsSha256 = TextHash(string.Empty), preparationBindings = Array.Empty<PlayerTmpFontObservationContract.PreparedFont>(), observations = Array.Empty<PlayerTmpFontObservationContract.Observation>() };
            }
            Type type = TypeOf("TMPro.TMP_FontAsset");
            string[] allPaths = paths.Concat(registrations).OrderBy(path => path, StringComparer.Ordinal).ToArray();
            string[] currentPaths = AssetDatabase.FindAssets("t:TMP_FontAsset").Select(AssetDatabase.GUIDToAssetPath).OrderBy(path => path, StringComparer.Ordinal).ToArray();
            if (before != null && !allPaths.SequenceEqual(currentPaths, StringComparer.Ordinal) || allPaths.Distinct(StringComparer.Ordinal).Count() != allPaths.Length ||
                allPaths.Any(path => AssetDatabase.GetMainAssetTypeAtPath(path) != type))
                throw new InvalidOperationException("TMP 完整资产路径/主类型集合与原许可 marker 不同；不忽略未知新增。");
            Object[] loaded = Resources.FindObjectsOfTypeAll(type);
            Object[] fonts = paths.Select(path => loaded.SingleOrDefault(font => AssetDatabase.GetAssetPath(font) == path) ??
                throw new InvalidOperationException("TMP 已批准字体不再是当前加载对象；不重载以制造后验。")).ToArray();
            Object[] eligible = loaded.Where(font => AssetDatabase.IsMainAsset(font) && (paths.Contains(AssetDatabase.GetAssetPath(font), StringComparer.Ordinal) || registrations.Contains(AssetDatabase.GetAssetPath(font), StringComparer.Ordinal))).OrderBy(font => Identity(font), StringComparer.Ordinal).ToArray();
            var memo = new Dictionary<string, string>(StringComparer.Ordinal);
            var result = new Snapshot
            {
                fonts = fonts.Select((font, index) => CaptureFont(font, paths[index], token)).ToArray(),
                registrationFonts = eligible.Select(font => CaptureRegistrationFont(font, fonts.Contains(font), token, memo)).ToArray(),
                registrationFilesSha256 = TextHash(string.Join("\n", paths.Concat(registrations).Distinct(StringComparer.Ordinal).OrderBy(path => path, StringComparer.Ordinal).Select(path => path + "|" + MemoFileHash(path, token, memo) + "|" + MemoFileHash(path + ".meta", token, memo)))),
                runtime = CaptureRuntime(fonts, eligible),
            };
            result.preparationDomain = ObservationDomain;
            result.preparationBindings = before == null ? allPaths.Select(path => CapturePreparedFont(eligible.Single(font => AssetDatabase.GetAssetPath(font) == path),
                path, paths.Contains(path, StringComparer.Ordinal), token, memo)).ToArray() :
                NovaFramework.Runtime.Util.Json.Deserialize<PlayerTmpFontObservationContract.PreparedFont[]>(NovaFramework.Runtime.Util.Json.Serialize(before.preparationBindings));
            string[] originalInputs = material.Where(IsPreparationInput).Distinct(StringComparer.Ordinal).OrderBy(value => value, StringComparer.Ordinal).ToArray();
            result.preparationInputsSha256 = TextHash(string.Join("\n", originalInputs));
            if (before != null && before.preparationInputsSha256 != result.preparationInputsSha256)
                throw new InvalidOperationException("TMP 原分类输入集合摘要漂移；不忽略原行。");
            result.observations = allPaths.Select(path => CapturePreparedObservation(eligible.SingleOrDefault(font => AssetDatabase.GetAssetPath(font) == path),
                path, paths.Contains(path, StringComparer.Ordinal), token, memo)).ToArray();
            if (!PlayerTmpFontObservationContract.TryValidate(result.preparationBindings, originalInputs, result.observations,
                    before?.runtime?.resourceRows ?? result.runtime.resourceRows, before?.runtime?.weakRows ?? result.runtime.weakRows,
                    result.runtime.resourceRows, result.runtime.weakRows, scopeAfter?.observations, scopeAfter?.runtime?.resourceRows, scopeAfter?.runtime?.weakRows, requireScope, out string observationError))
                throw new InvalidOperationException(observationError);
            if (!PlayerTmpFontRegistrationContract.TryValidateCapturedState(result.registrationFonts, result.runtime.resourceRows, result.runtime.weakRows, out string error))
                throw new InvalidOperationException(error);
            return result;
        }

        /// <summary>只规整官方明确允许的正常缓存/脏标志；保留字体正文、像素、磁盘、外部事件和配置输入。</summary>
        /// <param name="snapshot">真实 before、after 或当前状态。</param>
        /// <returns>独立不变量副本的哈希，不修改输入。</returns>
        internal static string InvariantHash(Snapshot snapshot)
        {
            if (snapshot?.fonts == null || snapshot.fonts.Any(font => font == null)) throw new InvalidOperationException("TMP 状态材料缺失。");
            Snapshot clone = NovaFramework.Runtime.Util.Json.Deserialize<Snapshot>(NovaFramework.Runtime.Util.Json.Serialize(snapshot));
            // 完整行仍在原快照与写前比较中；后验必须另过逐行 first-wins/首空槽证明，不能只凭此 hash。
            clone.registrationFonts = null;
            // 实际库存只能由独立观察合同证明；历史准备绑定和原域身份继续完整保留。
            clone.observations = null;
            foreach (FontState font in clone.fonts)
            {
                font.atlasBackingPresent = true;
                font.atlasDirty = false;
                font.fontDirty = false;
                font.lookupTablesDirty = false;
                font.characterKeys = null;
                font.characterValuesSha256 = null;
                font.scalarCacheSha256 = null;
                font.glyphLookupPresent = true;
            }
            if (clone.runtime != null)
            {
                clone.runtime.resourceRows = null;
                clone.runtime.weakRows = null;
            }
            // 只有实际正常 clear 组可产生管理器/队列/proxy 效果；registration-only 不共享这项投影。
            if (clone.runtime != null && clone.fonts.Length != 0)
            {
                clone.runtime.managerPresent = true;
                clone.runtime.managerIdentity = 0;
                clone.runtime.ownCameraCallbacks = 1;
                clone.runtime.ownCanvasCallbacks = 1;
                clone.runtime.ownRenderCallbacks = 0;
                clone.runtime.updateQueue = Array.Empty<string>();
                clone.runtime.tmpProxyRefresh = false;
                clone.runtime.textCoreProxyRefresh = false;
                clone.runtime.resourceOwnRegistered = true;
                clone.runtime.weakOwnRegistered = true;
            }
            return TextHash(NovaFramework.Runtime.Util.Json.Serialize(clone));
        }

        /// <summary>Execute 写前比较全部真实 before，不能用规整后哈希代替对用户新状态的保护。</summary>
        /// <param name="expected">Plan 实际 before。</param>
        /// <param name="current">Execute 紧邻构建的当前状态。</param>
        /// <returns>真实完整材料逐字段一致时返回 true。</returns>
        internal static bool BeforeEquals(Snapshot expected, Snapshot current)
        {
            return expected?.fonts != null && current?.fonts != null &&
                (expected.runtime == null && current.runtime == null || expected.runtime != null && current.runtime != null && ReferenceEquals(expected.runtime.managerReference, current.runtime.managerReference)) &&
                NovaFramework.Runtime.Util.Json.Serialize(expected) == NovaFramework.Runtime.Util.Json.Serialize(current);
        }

        /// <summary>核对本次正式链后状态；正常 singleton/清理可保留，其他输入或回调改变必须失败。</summary>
        /// <param name="before">Plan 或原回执保存的实际 before。</param>
        /// <param name="after">正式链后实际捕获的 after。</param>
        /// <param name="current">Dispose 或 Verify 当前状态。</param>
        /// <param name="error">缺失、漂移或后验不符的具体原因。</param>
        /// <returns>全组正常后验及所有不变量均吻合时返回 true。</returns>
        internal static bool TryValidateAfter(Snapshot before, Snapshot after, Snapshot current, out string error)
        {
            error = null;
            if (before?.fonts == null || after?.fonts == null || current?.fonts == null)
                error = "TMP before/after/current 材料缺失。";
            else if ((before.preparationBindings != null || after.preparationBindings != null || current.preparationBindings != null) &&
                !TryValidateObservationsAfter(before, after, current, out error))
                error = error ?? "TMP 原准备行与实际后态库存不符。";
            else if ((before.fonts.Length != 0 || before.runtime != null || after.runtime != null || current.runtime != null) &&
                (before.runtime == null || after.runtime == null || current.runtime == null ||
                !PlayerTmpFontRegistrationContract.TryValidate(before.registrationFonts, after.registrationFonts, current.registrationFonts,
                    before.runtime.resourceRows, after.runtime.resourceRows, current.runtime.resourceRows,
                    before.runtime.weakRows, after.runtime.weakRows, current.runtime.weakRows, out error)))
                error = error ?? "TMP 完整登记行或 runtime 材料缺失。";
            else if (before.fonts.Any(font => !before.registrationFonts.Any(registration => registration.cleared && registration.instanceId == font.fontInstanceId && registration.identity == font.fontIdentity)) ||
                after.fonts.Any(font => !after.registrationFonts.Any(registration => registration.cleared && registration.instanceId == font.fontInstanceId && registration.identity == font.fontIdentity)) ||
                current.fonts.Any(font => !current.registrationFonts.Any(registration => registration.cleared && registration.instanceId == font.fontInstanceId && registration.identity == font.fontIdentity)))
                error = "TMP 正常清理字体缺少同一实际实例的完整登记描述。";
            else if (before.runtime != null && (before.runtime.managerPresent && before.runtime.managerIdentity != after.runtime.managerIdentity ||
                after.runtime.managerIdentity != current.runtime.managerIdentity ||
                before.runtime.managerReference != null && !ReferenceEquals(before.runtime.managerReference, after.runtime.managerReference) ||
                after.runtime.managerReference != null && !ReferenceEquals(after.runtime.managerReference, current.runtime.managerReference)))
                error = "TMP 原 manager 或正式 after 的同域对象身份被替换。";
            else if (InvariantHash(before) != InvariantHash(after) || InvariantHash(after) != InvariantHash(current))
                error = "TMP 字体/atlas/磁盘/配置/外部事件或资源缓存不变量改变。";
            else if (after.fonts.Length != 0 && (after.runtime == null || current.runtime == null || !after.runtime.managerPresent || !current.runtime.managerPresent ||
                after.runtime.ownCameraCallbacks != 1 || after.runtime.ownCanvasCallbacks != 1 || after.runtime.ownRenderCallbacks != 0 ||
                current.runtime.ownCameraCallbacks != 1 || current.runtime.ownCanvasCallbacks != 1 || current.runtime.ownRenderCallbacks != 0 ||
                before.runtime == null || before.runtime.managerPresent && before.runtime.managerIdentity != after.runtime.managerIdentity ||
                after.runtime.managerIdentity != current.runtime.managerIdentity ||
                before.runtime.managerReference != null && !ReferenceEquals(before.runtime.managerReference, after.runtime.managerReference) ||
                after.runtime.managerReference != null && !ReferenceEquals(after.runtime.managerReference, current.runtime.managerReference) ||
                !after.runtime.resourceOwnRegistered || !current.runtime.resourceOwnRegistered || !after.runtime.weakOwnRegistered || !current.runtime.weakOwnRegistered ||
                after.fonts.Any(font => !font.atlasBackingPresent || !font.glyphLookupPresent || font.lookupTablesDirty || !ValidControlKeys(font.characterKeys)) ||
                current.fonts.Any(font => !font.atlasBackingPresent || !font.glyphLookupPresent || font.lookupTablesDirty || !ValidControlKeys(font.characterKeys)) ||
                !after.fonts.Select(font => font.characterValuesSha256).SequenceEqual(current.fonts.Select(font => font.characterValuesSha256), StringComparer.Ordinal) ||
                !after.fonts.Select(font => font.scalarCacheSha256).SequenceEqual(current.fonts.Select(font => font.scalarCacheSha256), StringComparer.Ordinal) ||
                !after.fonts.Select(font => font.atlasDirty).SequenceEqual(current.fonts.Select(font => font.atlasDirty)) ||
                !after.fonts.Select(font => font.fontDirty).SequenceEqual(current.fonts.Select(font => font.fontDirty)) ||
                before.fonts.Zip(after.fonts, (original, actual) => original.atlasDirty && !actual.atlasDirty || original.fontDirty && !actual.fontDirty).Any(value => value)))
                error = "TMP 正常 clear 的 singleton、唯一回调或控制字符 lookup 后验不符。";
            return error == null;
        }

        /// <summary>验证完整同域历史准备绑定和真实 Scope→current 库存；缺席不得从较晚 Capture 倒填到 Scope。</summary>
        /// <param name="before">原实际准备状态。</param>
        /// <param name="after">真正 Scope 后态。</param>
        /// <param name="current">本阶段只读观察。</param>
        /// <param name="error">原绑定、域、dirty 或现存/缺席证明不符原因。</param>
        /// <returns>原行完整且两个真实阶段满足原 clean/无引用缺席条件时为 true。</returns>
        private static bool TryValidateObservationsAfter(Snapshot before, Snapshot after, Snapshot current, out string error)
        {
            error = null;
            if (before.preparationDomain == null || before.preparationDomain != after.preparationDomain || after.preparationDomain != current.preparationDomain ||
                NovaFramework.Runtime.Util.Json.Serialize(before.preparationBindings) != NovaFramework.Runtime.Util.Json.Serialize(after.preparationBindings) ||
                NovaFramework.Runtime.Util.Json.Serialize(after.preparationBindings) != NovaFramework.Runtime.Util.Json.Serialize(current.preparationBindings) ||
                before.preparationInputsSha256 == null || before.preparationInputsSha256 != after.preparationInputsSha256 || after.preparationInputsSha256 != current.preparationInputsSha256 ||
                before.preparationBindings == null || before.preparationInputsSha256 != TextHash(string.Join("\n", PlayerTmpFontObservationContract.ExpectedInputs(before.preparationBindings))))
            { error = "TMP 同域历史准备绑定缺失或漂移；不扩展跨域缓存规则。"; return false; }
            if (before.observations == null || before.observations.Any(row => row == null || !row.present))
            { error = "TMP 原准备库存缺失或没有实际加载证据。"; return false; }
            if (before.preparationBindings?.Length == 0)
                return current.preparationBindings?.Length == 0 && after.preparationBindings?.Length == 0 && before.observations.Length == 0 && current.observations?.Length == 0 && after.observations?.Length == 0;
            return PlayerTmpFontObservationContract.TryValidate(before.preparationBindings, PlayerTmpFontObservationContract.ExpectedInputs(before.preparationBindings), before.observations,
                    before.runtime?.resourceRows, before.runtime?.weakRows, before.runtime?.resourceRows, before.runtime?.weakRows, null, null, null, false, out error) &&
                PlayerTmpFontObservationContract.TryValidate(before.preparationBindings, PlayerTmpFontObservationContract.ExpectedInputs(before.preparationBindings), after.observations,
                    before.runtime?.resourceRows, before.runtime?.weakRows, after.runtime?.resourceRows, after.runtime?.weakRows, null, null, null, false, out error) &&
                PlayerTmpFontObservationContract.TryValidate(before.preparationBindings, PlayerTmpFontObservationContract.ExpectedInputs(before.preparationBindings), current.observations,
                    before.runtime?.resourceRows, before.runtime?.weakRows, current.runtime?.resourceRows, current.runtime?.weakRows,
                    after.observations, after.runtime?.resourceRows, after.runtime?.weakRows, true, out error);
        }

        /// <summary>识别准备行及未知 TMP 准备前缀；TMPro. 是仍由完整回调集合保护的真实类型标签，不误当字体分类行。</summary>
        /// <param name="row">原回调输入行。</param>
        /// <returns>属于 TMP 准备命名空间时为 true。</returns>
        private static bool IsPreparationInput(string row) => row != null && row.StartsWith("TMP", StringComparison.Ordinal) && !row.StartsWith("TMPro.", StringComparison.Ordinal);

        /// <summary>读取当前已存在主字体的原 Prepare 分类和全部正文/文件绑定，不调用任何 lazy getter。</summary>
        /// <param name="font">本次同步库存中实际存在的主字体。</param>
        /// <param name="path">本轮原许可路径。</param>
        /// <param name="normal">是否原正常 clear marker。</param>
        /// <param name="token">真实文件摘要取消令牌。</param>
        /// <param name="memo">本次 Capture 的纯字符串摘要缓存。</param>
        /// <returns>原准备阶段的完整类型化绑定。</returns>
        private static PlayerTmpFontObservationContract.PreparedFont CapturePreparedFont(Object font, string path, bool normal, CancellationToken token, Dictionary<string, string> memo)
        {
            if (font == null || !AssetDatabase.IsMainAsset(font)) throw new InvalidOperationException("TMP 原准备字体不在实际已加载主资产库存。");
            var result = new PlayerTmpFontObservationContract.PreparedFont
            {
                path = path, identity = Identity(font), instanceId = UnityEngine.EntityId.ToULong(font.GetEntityId()), fontDirty = EditorUtility.IsDirty(font),
                fontJsonSha256 = MemoObjectHash(font, memo), assetSha256 = MemoFileHash(path, token, memo), metaSha256 = MemoFileHash(path + ".meta", token, memo),
                dependencyHash = AssetDatabase.GetAssetDependencyHash(path).ToString(), kind = normal ? PlayerTmpFontObservationContract.Normal : PlayerTmpFontObservationContract.Uncleared,
            };
            if (!normal && (Convert.ToInt32(Read(font, "m_AtlasPopulationMode")) == 1 || Convert.ToInt32(Read(font, "m_AtlasPopulationMode")) == 2) && (bool)Read(font, "m_ClearDynamicDataOnBuild"))
            {
                var atlas = Read(font, "m_AtlasTexture") as Texture2D;
                var atlases = Read(font, "m_AtlasTextures") as Texture2D[];
                atlas = atlas != null ? atlas : atlases != null && atlases.Length > 0 ? atlases[0] : null;
                if (atlas == null || atlas.width != 0) throw new InvalidOperationException("TMP 原非清理 marker 与实际分类不同。");
                result.kind = PlayerTmpFontObservationContract.ZeroWidth; result.atlasIdentity = Identity(atlas); result.atlasWidth = atlas.width; result.atlasHeight = atlas.height;
            }
            return result;
        }

        /// <summary>记录当前库存与真实磁盘；缺席仅填写磁盘字段，不拿原 JSON/hash/ID 冒充当前对象。</summary>
        /// <param name="font">本次同步实际现存字体，缺席为 null。</param>
        /// <param name="path">原许可路径，仍真实读取资产/meta/依赖。</param>
        /// <param name="normal">是否原 clear 分类。</param>
        /// <param name="token">真实读取取消令牌。</param>
        /// <param name="memo">本次同步纯摘要缓存。</param>
        /// <returns>明确 present/absent 的当前观察。</returns>
        private static PlayerTmpFontObservationContract.Observation CapturePreparedObservation(Object font, string path, bool normal, CancellationToken token, Dictionary<string, string> memo)
        {
            var row = new PlayerTmpFontObservationContract.Observation { path = path, present = font != null, assetSha256 = MemoFileHash(path, token, memo),
                metaSha256 = MemoFileHash(path + ".meta", token, memo), dependencyHash = AssetDatabase.GetAssetDependencyHash(path).ToString() };
            if (font != null)
            {
                PlayerTmpFontObservationContract.PreparedFont current = CapturePreparedFont(font, path, normal, token, memo);
                row.kind = current.kind; row.identity = current.identity; row.instanceId = current.instanceId; row.fontDirty = current.fontDirty;
                row.fontJsonSha256 = current.fontJsonSha256; row.atlasIdentity = current.atlasIdentity; row.atlasWidth = current.atlasWidth; row.atlasHeight = current.atlasHeight;
            }
            return row;
        }

        /// <summary>固定控制字符集合允许 FontEngine 根据源字体省略已有字形；结束符和 Tab 必须存在。</summary>
        /// <param name="keys">实际 lookup 的十进制键，不包含原字符值或字体正文。</param>
        /// <returns>只有已审非渲染字符、且包含3和9时返回 true。</returns>
        internal static bool ValidControlKeys(string[] keys)
        {
            string[] allowed = { "3", "9", "10", "11", "13", "1564", "8203", "8206", "8207", "8232", "8233", "8288" };
            return keys != null && keys.Distinct(StringComparer.Ordinal).Count() == keys.Length && keys.Contains("3") && keys.Contains("9") && keys.All(allowed.Contains);
        }

        /// <summary>只接受空序列表、可读零 atlas 及有效源引用；unitsPerEM 只读准确原生 backing，不调用 internal getter，不预清或改标志。</summary>
        /// <param name="font">已经合法加载的准确字体对象。</param>
        /// <param name="path">实际资产路径。</param>
        /// <param name="token">文件哈希取消令牌。</param>
        /// <returns>稳定输入与真实局部内存状态。</returns>
        private static FontState CaptureFont(Object font, string path, CancellationToken token)
        {
            if (font.GetType() != TypeOf("TMPro.TMP_FontAsset") || Convert.ToInt32(Read(font, "m_AtlasPopulationMode")) != 1 || !(bool)Read(font, "m_ClearDynamicDataOnBuild") ||
                (string)Read(font, "m_Version") != "1.1.0" || (bool)Read(font, "m_ShouldReimportFontFeatures") || Convert.ToInt32(Read(font, "m_AtlasPadding")) <= 0)
                throw new InvalidOperationException("TMP 仅支持自然 Dynamic/clear、version1.1.0、无升级/特征重导入的窄分支。");
            foreach (string name in new[] { "m_GlyphTable", "m_CharacterTable", "m_UsedGlyphRects", "m_GlyphsToRender", "m_GlyphsRendered" })
                if (Read(font, name) is ICollection items && items.Count != 0) throw new InvalidOperationException("TMP 非空 glyph/character/render 表尚未进入正常清理合同。" );
            var free = Read(font, "m_FreeGlyphRects") as IEnumerable;
            var rectangles = free == null ? Array.Empty<UnityEngine.TextCore.GlyphRect>() : free.Cast<UnityEngine.TextCore.GlyphRect>().ToArray();
            int bitmap = Convert.ToInt32(Enum.Parse(TypeOf("UnityEngine.TextCore.LowLevel.GlyphRasterModes"), "RASTER_MODE_BITMAP"));
            int modifier = (Convert.ToInt32(Read(font, "m_AtlasRenderMode")) & bitmap) == bitmap ? 0 : 1;
            if (rectangles.Length != 1 || rectangles[0].x != 0 || rectangles[0].y != 0 || rectangles[0].width != Convert.ToInt32(Read(font, "m_AtlasWidth")) - modifier || rectangles[0].height != Convert.ToInt32(Read(font, "m_AtlasHeight")) - modifier)
                throw new InvalidOperationException("TMP free rect 不是官方清理后的规范初始区域。");
            object face = Read(font, "m_FaceInfo");
            if (face?.GetType() != typeof(UnityEngine.TextCore.FaceInfo) ||
                !(Read(face, "m_UnitsPerEM") is int unitsPerEm) || unitsPerEm == 0)
                throw new InvalidOperationException("TMP FaceInfo 原生类型或 unitsPerEM backing 不满足已审合同。" );
            foreach (string name in new[] { "scale", "capLine", "meanLine", "strikethroughOffset" })
                if (Convert.ToDouble(face.GetType().GetProperty(name)?.GetValue(face) ?? throw new InvalidOperationException("FaceInfo 字段身份改变。")) == 0)
                    throw new InvalidOperationException("TMP FaceInfo 需要升级/补值，尚未进入合同。" );
            var atlases = Read(font, "m_AtlasTextures") as Texture2D[];
            var backing = Read(font, "m_AtlasTexture") as Texture2D;
            if (atlases == null || atlases.Length != 1 || atlases[0] == null || backing != null && backing != atlases[0] || Convert.ToInt32(Read(font, "m_AtlasTextureIndex")) != 0)
                throw new InvalidOperationException("TMP 需要多个 atlas、创建/销毁或不同缓存引用，尚未进入合同。" );
            Texture2D atlas = atlases[0];
            if (!atlas.isReadable || atlas.width != 1 || atlas.height != 1 || atlas.mipmapCount != 1 || atlas.format != TextureFormat.Alpha8 || !EditorUtility.IsPersistent(atlas) || AssetDatabase.GetAssetPath(atlas) != path)
                throw new InvalidOperationException("TMP 仅支持已有同资产可读1x1 Alpha8 单 atlas；不修改可读性或预清。");
            byte[] raw = atlas.GetRawTextureData();
            if (raw.Length != 1 || raw[0] != 0) throw new InvalidOperationException("TMP atlas 当前非1字节全零；原像素恢复合同尚未进入此分支。" );
            var source = Read(font, "m_SourceFontFile") as Font;
            if (source == null || source.GetType() != typeof(Font) || !EditorUtility.IsPersistent(source) || !string.IsNullOrEmpty(Read(font, "m_SourceFontFilePath") as string))
                throw new InvalidOperationException("TMP 缺少既有持久源 Font，或会读取任意路径/OS 字体。" );
            object feature = Read(font, "m_FontFeatureTable");
            foreach (string name in new[] { "m_LigatureSubstitutionRecords", "m_GlyphPairAdjustmentRecords", "m_MarkToBaseAdjustmentRecords", "m_MarkToMarkAdjustmentRecords" })
                if (Read(feature, name) is ICollection records && records.Count != 0) throw new InvalidOperationException("TMP 非空字体特征表尚未进入合同。" );
            var characterLookup = Read(font, "m_CharacterLookupDictionary") as IDictionary;
            var glyphLookup = Read(font, "m_GlyphLookupDictionary") as IDictionary;
            if (glyphLookup != null && glyphLookup.Count != 0)
                throw new InvalidOperationException("TMP 空序列表存在额外 glyph lookup；不消费未知缓存。");
            object kerning = Read(font, "m_KerningTable");
            if (kerning != null && Read(kerning, "kerningPairs") is ICollection legacyPairs && legacyPairs.Count != 0)
                throw new InvalidOperationException("TMP 存在旧 kerning 升级工作，尚未进入合同。");
            var characters = new List<string>();
            foreach (DictionaryEntry entry in characterLookup ?? new Hashtable())
            {
                if (entry.Value == null || entry.Value.GetType() != TypeOf("TMPro.TMP_Character") || !ReferenceEquals(Read(entry.Value, "m_TextAsset"), font) ||
                    Convert.ToUInt32(Read(entry.Value, "m_Unicode")) != Convert.ToUInt32(entry.Key) || Read(entry.Value, "m_Glyph") is not UnityEngine.TextCore.Glyph)
                    throw new InvalidOperationException("TMP 控制字符 lookup 存在其他字体/类型/缺失 glyph 归属。");
                var glyph = (UnityEngine.TextCore.Glyph)Read(entry.Value, "m_Glyph");
                if (glyph.glyphRect.width != 0 || glyph.glyphRect.height != 0 || glyph.atlasIndex != 0)
                    throw new InvalidOperationException("TMP 控制字符仍含渲染 atlas 区域，尚未进入合同。");
                characters.Add(Convert.ToUInt32(entry.Key) + "|" + NovaFramework.Runtime.Util.Json.Serialize(new { glyph.index, glyph.metrics, glyph.glyphRect, glyph.scale, glyph.atlasIndex }));
            }
            string sourcePath = AssetDatabase.GetAssetPath(source);
            return new FontState
            {
                path = path, fontInstanceId = UnityEngine.EntityId.ToULong(font.GetEntityId()), atlasInstanceId = UnityEngine.EntityId.ToULong(atlas.GetEntityId()), sourceInstanceId = UnityEngine.EntityId.ToULong(source.GetEntityId()), fontIdentity = Identity(font), fontJsonSha256 = ObjectHash(font), assetSha256 = FileHash(path, token), metaSha256 = FileHash(path + ".meta", token),
                sourceIdentity = Identity(source), sourceSha256 = FileHash(sourcePath, token), sourceMetaSha256 = FileHash(sourcePath + ".meta", token), dependencyHash = AssetDatabase.GetAssetDependencyHash(path).ToString(),
                atlasIdentity = Identity(atlas), atlasRawSha256 = BytesHash(raw), fontDirty = EditorUtility.IsDirty(font), atlasDirty = EditorUtility.IsDirty(atlas),
                atlasBackingPresent = backing != null, lookupTablesDirty = (bool)Read(font, "IsFontAssetLookupTablesDirty"),
                scalarCacheSha256 = TextHash(string.Join("|", new[] { "m_HashCode", "m_FamilyNameHashCode", "m_StyleNameHashCode", "m_MaterialHashCode", "m_EntityId", "m_InstanceID" }.Select(name => name + "=" + Read(font, name)))),
                glyphLookupPresent = glyphLookup != null, characterValuesSha256 = TextHash(string.Join("\n", characters.OrderBy(value => value, StringComparer.Ordinal))),
                characterKeys = characterLookup == null ? null : characterLookup.Keys.Cast<object>().Select(value => Convert.ToUInt32(value).ToString(System.Globalization.CultureInfo.InvariantCulture)).OrderBy(value => value, StringComparer.Ordinal).ToArray(),
            };
        }

        /// <summary>冻结自然单例、外部事件、两类资源缓存和失败路径设置；不调用 instance getter 或私有初始化。</summary>
        /// <param name="fonts">全部本轮批准清理字体。</param>
        /// <param name="eligible">仅当前实际已加载的明确 marker 字体；只在同步采集中临时使用，不保留强引用。</param>
        /// <returns>可核对正常构造/队列消费的实际状态。</returns>
        private static RuntimeState CaptureRuntime(Object[] fonts, Object[] eligible)
        {
            Type managerType = TypeOf("TMPro.TMP_EditorResourceManager");
            if (managerType.TypeInitializer != null) throw new InvalidOperationException("TMP manager 出现未知 static 初始化。");
            object manager = ReadStatic(managerType, "s_Instance");
            Type pipeline = typeof(UnityEngine.Rendering.RenderPipelineManager);
            if (ReadStatic(pipeline, "s_CurrentPipeline") != null) throw new InvalidOperationException("TMP 首版正常构造仅支持自然 Builtin currentPipeline=null。");
            string[] allowed = fonts.Select(Identity).OrderBy(value => value, StringComparer.Ordinal).ToArray();
            var result = new RuntimeState { managerPresent = manager != null, managerIdentity = manager == null ? 0 : RuntimeHelpers.GetHashCode(manager), managerReference = manager, updateQueue = Array.Empty<string>() };
            result.cameraBase = EventBase(typeof(Camera), "onPostRender", manager, "OnCameraPostRender", out result.ownCameraCallbacks);
            result.canvasBase = EventBase(typeof(Canvas), "willRenderCanvases", manager, "OnPreRenderCanvases", out result.ownCanvasCallbacks);
            result.renderBase = EventBase(pipeline, "endContextRendering", manager, "OnEndOfFrame", out result.ownRenderCallbacks);
            if (manager != null)
            {
                if (manager.GetType() != managerType || result.ownCameraCallbacks != 1 || result.ownCanvasCallbacks != 1 || result.ownRenderCallbacks != 0)
                    throw new InvalidOperationException("TMP 已有 manager 的准确事件归属不符。");
                foreach (string name in new[] { "m_ObjectReImportQueue", "m_ObjectReImportQueueLookup", "m_FontAssetDefinitionRefreshQueue", "m_FontAssetDefinitionRefreshQueueLookup" })
                    if (Count(Read(manager, name)) != 0) throw new InvalidOperationException("TMP 存在非本轮重导入/定义刷新工作；不消费或清队列。");
                result.updateQueue = ((IEnumerable)Read(manager, "m_ObjectUpdateQueue")).Cast<Object>().Select(Identity).OrderBy(value => value, StringComparer.Ordinal).ToArray();
                if (result.updateQueue.Distinct(StringComparer.Ordinal).Count() != result.updateQueue.Length || result.updateQueue.Any(value => !allowed.Contains(value, StringComparer.Ordinal)) ||
                    Count(Read(manager, "m_ObjectUpdateQueueLookup")) != result.updateQueue.Length ||
                    !((IEnumerable)Read(manager, "m_ObjectUpdateQueueLookup")).Cast<object>().Select(value => value.ToString()).OrderBy(value => value, StringComparer.Ordinal).SequenceEqual(
                        ((IEnumerable)Read(manager, "m_ObjectUpdateQueue")).Cast<Object>().Select(value => value.GetEntityId().ToString()).OrderBy(value => value, StringComparer.Ordinal), StringComparer.Ordinal))
                    throw new InvalidOperationException("TMP Update 队列包含其他对象或 list/lookup 不一致；不消费用户工作。");
            }
            Type fontType = TypeOf("TMPro.TMP_FontAsset");
            if ((string)ReadStatic(fontType, "s_DefaultMaterialSuffix") != " Atlas Material") throw new InvalidOperationException("TMP 材质 hash 后缀偏离已审默认值。");
            var delegates = new List<string>();
            foreach (string[] definition in new[]
            {
                new[] { "RegisterResourceForUpdate", "TMPro.TMP_EditorResourceManager", "RegisterResourceForUpdate" },
                new[] { "RegisterResourceForReimport", "TMPro.TMP_EditorResourceManager", "RegisterResourceForReimport" },
                new[] { "OnFontAssetTextureChanged", "TMPro.TMP_EditorResourceManager", "AddTextureToAsset" },
                new[] { "SetAtlasTextureIsReadable", "UnityEditor.TextCore.LowLevel.FontEngineEditorUtilities", "SetAtlasTextureIsReadable" },
                new[] { "GetSourceFontRef", "TMPro.TMP_EditorResourceManager", "GetSourceFontRef" },
                new[] { "SetSourceFontGUID", "TMPro.TMP_EditorResourceManager", "SetSourceFontGUID" },
            })
            {
                var chain = ReadStatic(fontType, definition[0]) as Delegate;
                Delegate[] entries = chain?.GetInvocationList();
                if (entries == null || entries.Length != 1 || !entries[0].Method.IsStatic || entries[0].Target != null || entries[0].Method.DeclaringType != TypeOf(definition[1]) || entries[0].Method.Name != definition[2])
                    throw new InvalidOperationException("TMP 编辑器委托含缺失、重复或未知下游。");
                delegates.Add(definition[0] + "|" + DelegateIdentity(entries[0]));
            }
            result.fontDelegates = delegates.ToArray();
            result.tmpProxyRefresh = (bool)ReadStatic(TypeOf("TMPro.EditorUtilities.TMP_PropertyDrawerUtilities"), "s_RefreshGlyphProxyLookup");
            result.textCoreProxyRefresh = (bool)ReadStatic(TypeOf("UnityEditor.TextCore.Text.TextCorePropertyDrawerUtilities"), "s_RefreshGlyphProxyLookup");
            result.resourceCacheSha256 = ResourceCacheHash(fonts, eligible, out result.resourceOwnRegistered, out result.resourceRows);
            result.weakTargetsSha256 = WeakTargetsHash(fonts, eligible, out result.weakOwnRegistered, out result.weakRows);
            Type settingsType = TypeOf("TMPro.TMP_Settings");
            Object settings = ReadStatic(settingsType, "s_Instance") as Object;
            if (settings != null && (settings.GetType() != settingsType || (string)Read(settings, "assetVersion") != "2"))
                throw new InvalidOperationException("TMP Settings 子类或旧版本尚未进入合同；可能存在先前加载安排的延迟 Importer UI，未证明异步队列结束。");
            result.settingsIdentity = Identity(settings);
            result.settingsSha256 = settings == null ? null : ObjectHash(settings);
            return result;
        }

        /// <summary>保留完整外部事件序列，只剥离同一自然 TMP manager 的准确已审方法。</summary>
        /// <param name="type">准确事件 owner。</param>
        /// <param name="fieldName">已审 backing field。</param>
        /// <param name="manager">当前自然 manager 或 null。</param>
        /// <param name="ownMethod">唯一可归属的正常方法名。</param>
        /// <param name="ownCount">实际本 manager 条目数。</param>
        /// <returns>所有其他事件的有序身份。</returns>
        private static string[] EventBase(Type type, string fieldName, object manager, string ownMethod, out int ownCount)
        {
            ownCount = 0;
            var values = new List<string>();
            foreach (Delegate entry in (ReadStatic(type, fieldName) as Delegate)?.GetInvocationList() ?? Array.Empty<Delegate>())
            {
                bool isManager = entry.Target != null && entry.Target.GetType() == TypeOf("TMPro.TMP_EditorResourceManager");
                if (isManager && (!ReferenceEquals(entry.Target, manager) || entry.Method.Name != ownMethod || entry.Method.DeclaringType != TypeOf("TMPro.TMP_EditorResourceManager")))
                    throw new InvalidOperationException("TMP 事件发现其他 manager 或未知方法归属。");
                if (isManager) ownCount++;
                else values.Add(DelegateIdentity(entry));
            }
            return values.ToArray();
        }

        /// <summary>保留三表全部行；有限字体的聚合投影必须同时经过独立 before/after 行证明。</summary>
        /// <param name="fonts">已批准正常清理字体。</param>
        /// <param name="eligible">当前实际已加载的准确 marker 字体。</param>
        /// <param name="ownRegistered">所有清理字体引用与必需 lookup 键均存在。</param>
        /// <param name="completeRows">三表完整真实行，包含原 winner 和所有未知外部项。</param>
        /// <returns>只投影有限候选后的未知外部完整摘要。</returns>
        private static string ResourceCacheHash(Object[] fonts, Object[] eligible, out bool ownRegistered, out PlayerTmpFontRegistrationContract.ResourceRow[] completeRows)
        {
            Type type = TypeOf("TMPro.TMP_ResourceManager");
            var rows = new List<PlayerTmpFontRegistrationContract.ResourceRow>();
            foreach (string name in new[] { PlayerTmpFontRegistrationContract.References, PlayerTmpFontRegistrationContract.Names, PlayerTmpFontRegistrationContract.Families })
            {
                if (ReadStatic(type, name) is not IDictionary dictionary) throw new InvalidOperationException("TMP 资源缓存实际类型改变。");
                foreach (DictionaryEntry entry in dictionary)
                {
                    bool isReference = name == PlayerTmpFontRegistrationContract.References;
                    Object reference = isReference ? Read(entry.Value, "fontAsset") as Object : entry.Value as Object;
                    if (reference == null || reference.GetType() != TypeOf("TMPro.TMP_FontAsset")) throw new InvalidOperationException("TMP 资源行指向已销毁或未知字体类型。");
                    var row = new PlayerTmpFontRegistrationContract.ResourceRow
                    {
                        table = name, key = Convert.ToString(entry.Key, System.Globalization.CultureInfo.InvariantCulture), fontIdentity = Identity(reference), fontInstanceId = UnityEngine.EntityId.ToULong(reference.GetEntityId()),
                        nameHash = isReference ? Convert.ToInt32(Read(entry.Value, "nameHashCode")) : 0,
                        familyHash = isReference ? Convert.ToInt32(Read(entry.Value, "familyNameHashCode")) : 0,
                        styleHash = isReference ? Convert.ToInt32(Read(entry.Value, "styleNameHashCode")) : 0,
                        familyStyleHash = isReference ? Convert.ToInt64(Read(entry.Value, "familyNameAndStyleHashCode")) : 0,
                    };
                    rows.Add(row);
                }
            }
            if (Count(ReadStatic(type, "s_FontAssetRemovalList")) != 0) throw new InvalidOperationException("TMP 存在旧移除工作。");
            completeRows = rows.OrderBy(row => row.table, StringComparer.Ordinal).ThenBy(row => row.key, StringComparer.Ordinal).ToArray();
            ownRegistered = fonts.All(font =>
            {
                var predicted = PredictRegistrationFont(font);
                return rows.Any(row => row.table == PlayerTmpFontRegistrationContract.References && row.key == predicted.entityKey && row.fontInstanceId == predicted.instanceId &&
                    row.nameHash == PlayerTmpFontRegistrationContract.EffectiveName(predicted) && row.familyHash == predicted.expectedFamilyHash && row.styleHash == predicted.expectedStyleHash && row.familyStyleHash == PlayerTmpFontRegistrationContract.FamilyStyle(predicted)) &&
                    rows.Any(row => row.table == PlayerTmpFontRegistrationContract.Names && row.key == PlayerTmpFontRegistrationContract.EffectiveName(predicted).ToString(System.Globalization.CultureInfo.InvariantCulture)) &&
                    rows.Any(row => row.table == PlayerTmpFontRegistrationContract.Families && row.key == PlayerTmpFontRegistrationContract.FamilyStyle(predicted).ToString(System.Globalization.CultureInfo.InvariantCulture));
            });
            var eligibleIds = new HashSet<ulong>(eligible.Select(font => UnityEngine.EntityId.ToULong(font.GetEntityId())));
            return TextHash(NovaFramework.Runtime.Util.Json.Serialize(completeRows.Where(row => !eligibleIds.Contains(row.fontInstanceId)).ToArray()));
        }

        /// <summary>读取每个真实弱槽和所有外部目标；不保留 WeakReference.Target 的强引用，不调用登记。</summary>
        /// <param name="fonts">已批准清理字体。</param>
        /// <param name="eligible">本次当前已加载的准确 marker 字体。</param>
        /// <param name="ownRegistered">清理字体各自恰好真实登记一次。</param>
        /// <param name="completeRows">含失效槽的完整有序行。</param>
        /// <returns>仅投影有限候选与失效槽后的未知存活外部目标摘要。</returns>
        private static string WeakTargetsHash(Object[] fonts, Object[] eligible, out bool ownRegistered, out PlayerTmpFontRegistrationContract.WeakRow[] completeRows)
        {
            var rows = new List<PlayerTmpFontRegistrationContract.WeakRow>();
            foreach (object weak in (IEnumerable)ReadStatic(TypeOf("TMPro.TMP_FontAsset"), "s_CallbackInstances"))
            {
                if (weak == null || !weak.GetType().IsGenericType || weak.GetType().GetGenericTypeDefinition() != typeof(WeakReference<>) || weak.GetType().GenericTypeArguments[0] != TypeOf("TMPro.TMP_FontAsset"))
                    throw new InvalidOperationException("TMP 弱登记容器元素类型改变。");
                object[] args = { null };
                bool alive = (bool)weak.GetType().GetMethod("TryGetTarget").Invoke(weak, args);
                Object target = alive ? args[0] as Object : null;
                if (alive && (target == null || target.GetType() != TypeOf("TMPro.TMP_FontAsset"))) throw new InvalidOperationException("TMP 活弱槽指向已销毁或未知对象，不能当作首空槽。");
                rows.Add(new PlayerTmpFontRegistrationContract.WeakRow { alive = alive, fontIdentity = alive ? Identity(target) : null, fontInstanceId = alive ? UnityEngine.EntityId.ToULong(target.GetEntityId()) : 0 });
            }
            completeRows = rows.ToArray();
            ownRegistered = fonts.All(font => rows.Count(row => row.alive && row.fontInstanceId == UnityEngine.EntityId.ToULong(font.GetEntityId())) == 1);
            var eligibleIds = new HashSet<ulong>(eligible.Select(font => UnityEngine.EntityId.ToULong(font.GetEntityId())));
            return TextHash(NovaFramework.Runtime.Util.Json.Serialize(rows.Where(row => row.alive && !eligibleIds.Contains(row.fontInstanceId)).ToArray()));
        }

        /// <summary>冻结当前已加载 marker 字体；非清理字体完整序列化、dirty、源与 atlas 均不可投影。</summary>
        /// <param name="font">本次同步只读采集中已存在的准确字体，不加载或持有到下一阶段。</param>
        /// <param name="cleared">是否已由独立单 atlas 正常清理合同保护。</param>
        /// <param name="token">只读文件哈希取消令牌。</param>
        /// <param name="memo">本次同步 Capture 的纯摘要缓存；不跨阶段、不含 Unity 引用。</param>
        /// <returns>只包含 ID、源预测数值和保护摘要的 DTO。</returns>
        private static PlayerTmpFontRegistrationContract.Font CaptureRegistrationFont(Object font, bool cleared, CancellationToken token, Dictionary<string, string> memo)
        {
            string path = AssetDatabase.GetAssetPath(font);
            if (font.GetType() != TypeOf("TMPro.TMP_FontAsset") || !EditorUtility.IsPersistent(font) || !AssetDatabase.IsMainAsset(font) || AssetDatabase.GetMainAssetTypeAtPath(path) != font.GetType())
                throw new InvalidOperationException("TMP 登记候选不是准确既有主字体资产。");
            var result = PredictRegistrationFont(font);
            result.cleared = cleared;
            var protection = new List<string> { path, MemoObjectHash(font, memo), MemoFileHash(path, token, memo), MemoFileHash(path + ".meta", token, memo), AssetDatabase.GetAssetDependencyHash(path).ToString() };
            if (!cleared)
            {
                protection.Add("dirty=" + EditorUtility.IsDirty(font));
                foreach (string name in new[] { "m_SourceFontFile", "m_SourceFontFile_EditorRef" })
                {
                    Object source = Read(font, name) as Object;
                    if (source != null && (source.GetType() != typeof(Font) || !EditorUtility.IsPersistent(source))) throw new InvalidOperationException("TMP 非清理字体含未知源 Font。");
                    string sourcePath = source == null ? null : AssetDatabase.GetAssetPath(source);
                    protection.Add(name + "|" + Identity(source) + "|" + (source == null ? "null" : UnityEngine.EntityId.ToULong(source.GetEntityId()) + "|" + MemoObjectHash(source, memo) + "|" + MemoFileHash(sourcePath, token, memo) + "|" + MemoFileHash(sourcePath + ".meta", token, memo)));
                }
                var atlases = Read(font, "m_AtlasTextures") as Texture2D[];
                if (atlases == null) throw new InvalidOperationException("TMP 非清理字体 atlas 数组未知。");
                Texture2D backing = Read(font, "m_AtlasTexture") as Texture2D;
                int mode = Convert.ToInt32(Read(font, "m_AtlasPopulationMode"));
                result.allowAtlasReference = (mode == 1 || mode == 2) && (bool)Read(font, "m_ClearDynamicDataOnBuild") && atlases.Length > 0 && atlases[0] != null && atlases[0].width == 0;
                result.atlasReferencePresent = backing != null;
                if (result.allowAtlasReference && backing != null && !ReferenceEquals(backing, atlases[0])) throw new InvalidOperationException("TMP 零宽 atlas backing 不是原数组首纹理。");
                for (int index = 0; index < atlases.Length; index++) protection.Add("atlas[" + index + "]=" + RegistrationAtlasHash(atlases[index], token, memo));
                // 仅已审零宽 getter 的 null→原数组首纹理可变化；真实 presence 另存 DTO 并单调验证。
                protection.Add("atlasBacking=" + RegistrationAtlasHash(result.allowAtlasReference ? atlases[0] : backing, token, memo));
            }
            result.protectionSha256 = TextHash(string.Join("\n", protection));
            return result;
        }

        /// <summary>只读 backing 与当前 name/FaceInfo，按已 pin 源预测所有登记键，不调用 TMP hash getter。</summary>
        /// <param name="font">已自然加载的准确字体对象。</param>
        /// <returns>当前 backing 与源预测值，不携带 Unity 引用。</returns>
        private static PlayerTmpFontRegistrationContract.Font PredictRegistrationFont(Object font)
        {
            object face = Read(font, "m_FaceInfo");
            string family = Read(face, "m_FamilyName") as string;
            string style = Read(face, "m_StyleName") as string;
            return new PlayerTmpFontRegistrationContract.Font
            {
                identity = Identity(font), instanceId = UnityEngine.EntityId.ToULong(font.GetEntityId()), entityKey = font.GetEntityId().ToString(),
                nameHash = Convert.ToInt32(Read(font, "m_HashCode")), familyHash = Convert.ToInt32(Read(font, "m_FamilyNameHashCode")),
                styleHash = Convert.ToInt32(Read(font, "m_StyleNameHashCode")), materialHash = Convert.ToInt32(Read(font, "m_MaterialHashCode")),
                expectedNameHash = PlayerTmpFontRegistrationContract.PredictHash(font.name), expectedFamilyHash = PlayerTmpFontRegistrationContract.PredictHash(family),
                expectedStyleHash = PlayerTmpFontRegistrationContract.PredictHash(style), expectedMaterialHash = PlayerTmpFontRegistrationContract.PredictHash(font.name + " Atlas Material", true),
            };
        }

        /// <summary>读取既有 atlas 的身份、序列化和原生内容 hash；不可读纹理绝不改 readable 或读像素。</summary>
        /// <param name="atlas">原 backing/数组中的 Texture2D 或 null。</param>
        /// <param name="token">只读文件哈希取消令牌。</param>
        /// <param name="memo">本次同步捕获中只含字符串摘要的局部缓存。</param>
        /// <returns>稳定保护摘要；可读纹理额外绑定完整原字节。</returns>
        private static string RegistrationAtlasHash(Texture2D atlas, CancellationToken token, Dictionary<string, string> memo)
        {
            if (atlas == null) return "null";
            string memoKey = "atlas:" + UnityEngine.EntityId.ToULong(atlas.GetEntityId());
            if (memo.TryGetValue(memoKey, out string cached)) return cached;
            if (atlas.GetType() != typeof(Texture2D) || !EditorUtility.IsPersistent(atlas)) throw new InvalidOperationException("TMP 非清理 atlas 不是既有持久 Texture2D。");
            string path = AssetDatabase.GetAssetPath(atlas);
            Hash128 content = atlas.imageContentsHash;
            if (!atlas.isReadable && !content.isValid) throw new InvalidOperationException("TMP 非清理不可读 atlas 缺少有效原生内容 hash；不能证明像素保护，不修改 readable。");
            string result = TextHash(NovaFramework.Runtime.Util.Json.Serialize(new
            {
                identity = Identity(atlas), id = UnityEngine.EntityId.ToULong(atlas.GetEntityId()), json = MemoObjectHash(atlas, memo), dirty = EditorUtility.IsDirty(atlas),
                atlas.width, atlas.height, atlas.format, atlas.mipmapCount, atlas.isReadable,
                nativeContentHash = content.ToString(), rawSha256 = atlas.isReadable ? BytesHash(atlas.GetRawTextureData()) : null,
                assetSha256 = MemoFileHash(path, token, memo), metaSha256 = MemoFileHash(path + ".meta", token, memo),
            }));
            memo.Add(memoKey, result);
            return result;
        }

        /// <summary>同次同步捕获对同一路径只读一次完整文件，不跨 before/after/current 复用摘要。</summary>
        /// <param name="path">准确既有文件路径。</param>
        /// <param name="token">当前取消令牌，每次调用都检查。</param>
        /// <param name="memo">本次 Capture 私有字符串字典，不持对象引用。</param>
        /// <returns>该次完整读取计算的文件 SHA。</returns>
        private static string MemoFileHash(string path, CancellationToken token, Dictionary<string, string> memo)
        {
            token.ThrowIfCancellationRequested();
            string key = "file:" + Physical(path);
            if (!memo.TryGetValue(key, out string hash)) { hash = FileHash(path, token); memo.Add(key, hash); }
            return hash;
        }

        /// <summary>同次同步捕获对同一原生 ID 只序列化一次完整对象；不会跨阶段复用或保留对象。</summary>
        /// <param name="value">当前已加载准确对象，只在本同步调用借用。</param>
        /// <param name="memo">本次 Capture 私有字符串字典。</param>
        /// <returns>完整 Editor JSON 摘要。</returns>
        private static string MemoObjectHash(Object value, Dictionary<string, string> memo)
        {
            string key = "json:" + UnityEngine.EntityId.ToULong(value.GetEntityId());
            if (!memo.TryGetValue(key, out string hash)) { hash = ObjectHash(value); memo.Add(key, hash); }
            return hash;
        }

        /// <summary>合法已有资产加载前先排除已知升级/特征导入，并观察生命周期错误；不把延迟 UI 当已结束。</summary>
        /// <param name="type">准确 TMP 类型。</param>
        /// <param name="path">已有资产路径。</param>
        /// <param name="token">文件哈希取消令牌。</param>
        /// <returns>Unity 实际加载的资产。</returns>
        private static Object LoadChecked(Type type, string path, CancellationToken token)
        {
            if (AssetDatabase.GetMainAssetTypeAtPath(path) != type) throw new InvalidOperationException("TMP 资产主类型不是已审准确字体，拒绝未知子类/子资产 Load 生命周期。");
            string before = FileHash(path, token), meta = FileHash(path + ".meta", token);
            Object existing = Resources.FindObjectsOfTypeAll(type).SingleOrDefault(font => AssetDatabase.GetAssetPath(font) == path);
            if (existing == null)
            {
                string text = File.ReadAllText(Physical(path));
                if (!HasSafeLoadScalars(text)) throw new InvalidOperationException("TMP 首次 Load 的版本/重导入标量未知；不主动升级或导入。");
            }
            int errors = 0;
            Application.LogCallback observer = (message, trace, kind) => { if (kind == LogType.Error || kind == LogType.Exception || kind == LogType.Assert) Interlocked.Increment(ref errors); };
            Object result;
            Application.logMessageReceivedThreaded += observer;
            try { result = AssetDatabase.LoadAssetAtPath(path, type); }
            finally { Application.logMessageReceivedThreaded -= observer; }
            if (before != FileHash(path, token) || meta != FileHash(path + ".meta", token)) throw new InvalidOperationException("TMP 资产 Load 生命周期改变磁盘正文；保留实际失败材料，不覆盖用户文件。");
            if (errors != 0) throw new InvalidOperationException("TMP 资产 Load 生命周期出现错误；可能存在 TMP_Settings 延迟 Importer UI，未证明队列结束，不继续构建。");
            if (result != null && result.GetType() != type) throw new InvalidOperationException("TMP Load 返回未知字体子类。");
            return result;
        }

        /// <summary>只读取受限的两个顶层 YAML 标量；不解析、重写或保存 Unity 资产。</summary>
        /// <param name="text">已有资产文件文本。</param>
        /// <returns>唯一版本1.1.0且特征重导入为0或已审默认缺省时返回 true。</returns>
        internal static bool HasSafeLoadScalars(string text)
        {
            if (text == null) return false;
            if (text.Length > 0 && text[0] == '\uFEFF') text = text.Substring(1);
            if (!text.StartsWith("%YAML", StringComparison.Ordinal)) return false;
            var versions = System.Text.RegularExpressions.Regex.Matches(text, "^  m_Version:[ \\t]*([^\\r\\n]*)", System.Text.RegularExpressions.RegexOptions.Multiline);
            var imports = System.Text.RegularExpressions.Regex.Matches(text, "^  m_ShouldReimportFontFeatures:[ \\t]*([^\\r\\n]*)", System.Text.RegularExpressions.RegexOptions.Multiline);
            string version = versions.Count == 1 ? versions[0].Groups[1].Value.Trim() : null;
            return (version == "1.1.0" || version == "\"1.1.0\"" || version == "'1.1.0'") && (imports.Count == 0 || imports.Count == 1 && imports[0].Groups[1].Value.Trim() == "0");
        }

        /// <summary>记录委托方法和实际 target 身份，不调用委托或输出正文。</summary>
        /// <param name="value">实际调用链条目。</param>
        /// <returns>模块、metadata token 和 target 的准确当前域身份。</returns>
        private static string DelegateIdentity(Delegate value) => value.Method.Module.ModuleVersionId.ToString("D") + "|" + value.Method.MetadataToken + "|" + value.Method.DeclaringType.FullName + "." + value.Method.Name + "|" + (value.Target == null ? "static" : RuntimeHelpers.GetHashCode(value.Target).ToString("x8"));

        /// <summary>只按准确全名查找已加载类型，不初始化或加载程序集。</summary>
        /// <param name="name">准确类型全名。</param>
        /// <returns>唯一已加载类型。</returns>
        private static Type TypeOf(string name) => AppDomain.CurrentDomain.GetAssemblies().Select(assembly => assembly.GetType(name, false)).Where(type => type != null).Distinct().SingleOrDefault() ?? throw new InvalidOperationException("TMP 合同类型不存在：" + name);

        /// <summary>沿继承链只读准确实例 backing，禁止替代为 vendor getter。</summary>
        /// <param name="owner">现有对象或值类型装箱。</param>
        /// <param name="name">准确字段名。</param>
        /// <returns>字段当前值。</returns>
        private static object Read(object owner, string name)
        {
            if (owner == null) throw new InvalidOperationException("TMP backing owner 为空。");
            for (Type type = owner.GetType(); type != null; type = type.BaseType)
            {
                FieldInfo field = type.GetField(name, InstanceFields | BindingFlags.DeclaredOnly);
                if (field != null) return field.GetValue(owner);
            }
            throw new InvalidOperationException("TMP backing 字段不存在：" + name);
        }

        /// <summary>只读取已审静态 backing；纯容器 cctor 属于显式正常内存初始化。</summary>
        /// <param name="type">已审准确类型。</param>
        /// <param name="name">准确字段名。</param>
        /// <returns>当前字段值。</returns>
        private static object ReadStatic(Type type, string name) => (type.GetField(name, StaticFields) ?? throw new InvalidOperationException("TMP static backing 不存在：" + name)).GetValue(null);

        /// <summary>仅使用 BCL 集合 Count，不执行厂商属性。</summary>
        /// <param name="value">List/Dictionary/HashSet backing。</param>
        /// <returns>当前元素数。</returns>
        private static int Count(object value)
        {
            Type type = value?.GetType();
            Type definition = type != null && type.IsGenericType ? type.GetGenericTypeDefinition() : null;
            if (definition != typeof(List<>) && definition != typeof(Dictionary<,>) && definition != typeof(HashSet<>)) throw new InvalidOperationException("TMP backing 集合不属于准确 BCL 类型。");
            return value is ICollection collection ? collection.Count : (int)type.GetProperty("Count").GetValue(value);
        }

        /// <summary>持久对象以 GUID/fileID 绑定，非持久缓存保留当前域实例身份。</summary>
        /// <param name="value">现有对象或 null。</param>
        /// <returns>不含对象正文的身份。</returns>
        private static string Identity(Object value) => value == null ? "null" : AssetDatabase.TryGetGUIDAndLocalFileIdentifier(value, out string guid, out long fileId) ? value.GetType().FullName + "|" + guid + "|" + fileId + "|" + AssetDatabase.GetAssetPath(value) : value.GetType().FullName + "|nonpersistent|" + UnityEngine.EntityId.ToULong(value.GetEntityId());

        /// <summary>序列化内存正文只进入摘要，不保存资产。</summary>
        /// <param name="value">现有准确对象。</param>
        /// <returns>序列化正文 SHA。</returns>
        private static string ObjectHash(Object value) => TextHash(EditorJsonUtility.ToJson(value));

        /// <summary>项目/包路径沿 Unity 映射解析为只读物理路径。</summary>
        /// <param name="path">已有 Unity 路径。</param>
        /// <returns>准确物理绝对路径。</returns>
        private static string Physical(string path)
        {
            string physical = Path.IsPathRooted(path) ? path : FileUtil.GetPhysicalPath(path);
            return Path.GetFullPath(Path.IsPathRooted(physical) ? physical : Path.Combine(BuildActionCommon.ProjectRoot, string.IsNullOrEmpty(physical) ? path : physical));
        }

        /// <summary>对已有文件读取摘要，不输出内容。</summary>
        /// <param name="path">准确资产或 meta 路径。</param>
        /// <param name="token">取消令牌。</param>
        /// <returns>文件 SHA。</returns>
        private static string FileHash(string path, CancellationToken token) => GenerateActionCommon.ComputeFileHash(Physical(path), token);

        /// <summary>对像素原字节只计算摘要。</summary>
        /// <param name="bytes">内存副本，不写回纹理。</param>
        /// <returns>小写十六进制 SHA。</returns>
        private static string BytesHash(byte[] bytes) { using (var hash = System.Security.Cryptography.SHA256.Create()) return BitConverter.ToString(hash.ComputeHash(bytes)).Replace("-", "").ToLowerInvariant(); }

        /// <summary>计算合同文本摘要，不输出正文。</summary>
        /// <param name="value">内部合同文本。</param>
        /// <returns>文本 SHA。</returns>
        private static string TextHash(string value) => GenerateActionCommon.ComputeTextHash(value);
    }
}
