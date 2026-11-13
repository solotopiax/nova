/***************************************************************
 * (c) copyright 2026 - 2030, Solotopia
 * All Rights Reserved.
 * -------------------------------------------------------------
 * filename:  EditorUtil.SpriteAtlasGeneration.cs
 * author:    taoye
 * created:   2026/11/12
 * descrip:   显式预演、按差异生成且保留输出身份的可选图集工具
 ***************************************************************/

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using UnityEditor;
using UnityEditor.U2D;
using UnityEngine;
using Object = UnityEngine.Object;
using RuntimeSpriteAtlas = UnityEngine.U2D.SpriteAtlas;

namespace NovaFramework.Editor
{
    public static partial class EditorUtil
    {
        /// <summary>
        /// 可选主图集生产入口。仅处理已保存配置明确声明的目录，不注册自动扫描、导入后处理或构建回调。
        /// 所有方法须在 Unity Editor 主线程调用；不修改源图片、全局 Sprite Packer 模式或 YooAsset 收集配置。
        /// </summary>
        public static partial class SpriteAtlasGeneration
        {
            /// <summary>输出目录内的唯一归属清单名；清单不授权接管任何未登记的现有资产。</summary>
            private const string c_ManifestName = "nova-sprite-atlas-manifest.json";
            /// <summary>当前归属清单版本，未知版本拒绝改写。</summary>
            private const int c_ManifestVersion = 1;
            /// <summary>同步写入期间的重入门，不使用 Unity 引用计数批锁。</summary>
            private static bool s_IsApplying;

            /// <summary>归属清单仅记录本配置曾经成功生成的输出；移除规则不会删除记录或资产。</summary>
            [Serializable]
            private sealed class Manifest
            {
                public int Version = c_ManifestVersion;
                public string OwnerGuid;
                public List<ManifestRecord> Outputs = new List<ManifestRecord>();
            }

            /// <summary>一个输出的精确所有权及最近成功输入和文件状态。</summary>
            [Serializable]
            private sealed class ManifestRecord
            {
                public string RuleId;
                public string OutputPath;
                public string Guid;
                public string SourceFingerprint;
                public string ContentFingerprint;
                public string FileHash;
                public string MetaHash;
            }

            /// <summary>预演期间解析出的真实规则；不向调用方暴露可变 Unity 对象。</summary>
            private sealed class ResolvedRule
            {
                public SpriteAtlasGenerationRule Rule;
                public SpriteAtlasGenerationEntry Entry;
                public Object[] Sprites;
                public ManifestRecord Previous;
                public string SourceFingerprint;
                public string ContentFingerprint;
                public List<string> Roots;
                public List<string> Exclusions;
            }

            /// <summary>一次完整只读核查的结果，应用时重新创建，避免依赖旧对象引用。</summary>
            private sealed class ResolvedPlan
            {
                public SpriteAtlasGenerationPlan PublicPlan;
                public SpriteAtlasGenerationSettingsSO Settings;
                public Manifest Manifest;
                public string ManifestPath;
                public string SettingsFileHash;
                public string SettingsMetaHash;
                public string SettingsModelHash;
                public SpritePackerMode PackerMode;
                public List<ResolvedRule> Rules = new List<ResolvedRule>();
            }

            /// <summary>
            /// 只读预演已保存配置，列出创建、原地更新、仅重新导入、无变化及保留输出。
            /// 不创建配置、目录、图集或清单。无归属输出、重复名称、危险路径、模式不匹配或遗留事务均抛错。
            /// </summary>
            /// <param name="settingsAssetPath">项目 Assets 下已保存的 SpriteAtlasGenerationSettingsSO 精确路径。</param>
            /// <returns>包含输入与目标指纹的不可变预演计划。</returns>
            public static SpriteAtlasGenerationPlan Preview(string settingsAssetPath)
            {
                EnsureIdle();
                return Resolve(settingsAssetPath).PublicPlan;
            }

            /// <summary>
            /// 应用一个新鲜预演；重新核对指纹后才写入，只处理有差异的输出并保持已有 GUID。
            /// 完全无变化时连清单与 meta 都不重写。异常会精确恢复本次输出，恢复不完整时保留事务并拒绝新生成。
            /// </summary>
            /// <param name="plan">本工具 Preview 返回的计划，不能为 null。</param>
            /// <returns>本次实际创建、保存和重新导入的图集数量及路径。</returns>
            public static SpriteAtlasGenerationResult Apply(SpriteAtlasGenerationPlan plan)
            {
                return Apply(plan, null);
            }

            /// <summary>
            /// 显式执行已选配置，内部执行新鲜预演与应用；用于项目主动加入的 Pipify 生产步骤。
            /// 仅在原生 Inspector 保存并启用配置后使用，不替代人工审核首次预演中的目录和差异。
            /// </summary>
            /// <param name="settingsAssetPath">已保存配置的精确项目路径。</param>
            /// <returns>本次图集生产结果。</returns>
            public static SpriteAtlasGenerationResult Generate(string settingsAssetPath)
            {
                return Apply(Preview(settingsAssetPath));
            }

            /// <summary>
            /// 共享实际提交与事务逻辑；内部观察回调在输出真实写入后调用，异常同样进入恢复。
            /// 该重载供程序集内契约验证，不注册全局钩子，不改变公开生产入口。
            /// </summary>
            /// <param name="plan">已冻结的预演。</param>
            /// <param name="afterOutputWritten">每个输出提交后的内部观察点；允许为 null。</param>
            /// <param name="afterRecoveryImported">恢复阶段真实导入返回后的内部观察点；不替代原生导入。</param>
            /// <param name="afterBackupDeleted">真实删除单个备份后的内部观察点；不替代文件删除。</param>
            /// <returns>成功生成的结果。</returns>
            internal static SpriteAtlasGenerationResult Apply(SpriteAtlasGenerationPlan plan,
                Action<string> afterOutputWritten, Action<string> afterRecoveryImported = null,
                Action<string> afterBackupDeleted = null)
            {
                if (plan == null) throw new ArgumentNullException(nameof(plan));
                EnsureIdle();
                s_IsApplying = true;
                try
                {
                    ResolvedPlan current = Resolve(plan.SettingsAssetPath);
                    if (!string.Equals(plan.Fingerprint, current.PublicPlan.Fingerprint, StringComparison.Ordinal))
                        throw new InvalidOperationException("图集预演之后配置、源文件或输出已经变化，请重新预演。");
                    if (!current.PublicPlan.Enabled)
                        throw new InvalidOperationException("图集生成配置尚未启用，请在项目配置中明确启用后重新预演。");
                    if (!current.PublicPlan.HasChanges) return new SpriteAtlasGenerationResult(current.PublicPlan.Entries);
                    Commit(current, afterOutputWritten, afterRecoveryImported, afterBackupDeleted);
                    return new SpriteAtlasGenerationResult(current.PublicPlan.Entries);
                }
                finally
                {
                    s_IsApplying = false;
                }
            }

            /// <summary>拒绝重入、Play 或编译状态下的操作，不改变编辑器全局状态。</summary>
            private static void EnsureIdle()
            {
                if (s_IsApplying) throw new InvalidOperationException("已有图集生成或恢复正在执行，不能重入。");
                if (EditorApplication.isPlayingOrWillChangePlaymode || EditorApplication.isCompiling)
                    throw new InvalidOperationException("请等待 Unity 完成编译并退出 Play 后再处理图集。");
            }

            /// <summary>只读解析配置、声明范围、全部 Sprite 子资产和输出归属，并建立漂移指纹。</summary>
            /// <param name="settingsAssetPath">精确配置路径。</param>
            /// <returns>本次解析快照。</returns>
            private static ResolvedPlan Resolve(string settingsAssetPath)
            {
                string path = ValidateAssetPath(settingsAssetPath, false);
                SpriteAtlasGenerationSettingsSO settings = LoadSettings(path, out string settingsGuid);
                RequireNoRecovery(settingsGuid);
                var fingerprint = new StringBuilder();
                Append(fingerprint, path, settingsGuid, HashFile(path), HashFile(path + ".meta"),
                    EditorSettings.spritePackerMode.ToString(), HashText(JsonUtility.ToJson(settings)));
                if (!settings.Enabled)
                {
                    return new ResolvedPlan
                    {
                        Settings = settings,
                        PublicPlan = new SpriteAtlasGenerationPlan(path, settingsGuid, settings.OutputFolder,
                            false, HashText(fingerprint.ToString()), Array.Empty<SpriteAtlasGenerationEntry>()),
                    };
                }

                RequireFormat(settings.Format);
                string outputFolder = ValidateAssetPath(settings.OutputFolder, true);
                if (!AssetDatabase.IsValidFolder(outputFolder))
                    throw new InvalidOperationException("请先创建并导入已声明的图集输出目录：" + outputFolder);
                if (settings.Rules == null) throw new InvalidOperationException("图集规则列表不能为 null。");
                string manifestPath = outputFolder + "/" + c_ManifestName;
                Manifest manifest = ReadManifest(manifestPath, settingsGuid, outputFolder);
                Append(fingerprint, outputFolder, HashFile(manifestPath), HashFile(manifestPath + ".meta"));
                var resolved = new ResolvedPlan
                {
                    Settings = settings, Manifest = manifest, ManifestPath = manifestPath,
                    SettingsFileHash = HashFile(path), SettingsMetaHash = HashFile(path + ".meta"),
                    SettingsModelHash = HashText(JsonUtility.ToJson(settings)),
                    PackerMode = EditorSettings.spritePackerMode,
                };
                var ids = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
                var outputPaths = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
                var allSources = new List<string>();
                var active = new HashSet<string>(StringComparer.Ordinal);
                foreach (SpriteAtlasGenerationRule rule in settings.Rules)
                {
                    if (rule == null) throw new InvalidOperationException("图集规则不能包含空项。");
                    ValidateRule(rule, ids);
                    string outputPath = outputFolder + "/" + rule.AtlasName +
                        (settings.Format == SpriteAtlasGenerationFormat.V1 ? ".spriteatlas" : ".spriteatlasv2");
                    ValidateAssetPath(outputPath, false);
                    if (!outputPaths.Add(outputPath)) throw new InvalidOperationException("图集输出名称忽略大小写后重复：" + outputPath);
                    List<string> roots = ValidateRoots(rule, outputFolder, allSources);
                    List<string> exclusions = ValidateExclusions(rule, roots);
                    var sourceInfo = new List<SpriteAtlasGenerationSource>();
                    var sprites = new List<Object>();
                    string sourceFingerprint = ReadSources(roots, exclusions, sourceInfo, sprites);
                    string contentFingerprint = BuildContentFingerprint(rule, settings.Format, sourceInfo);
                    ManifestRecord previous = manifest.Outputs.SingleOrDefault(record =>
                        string.Equals(record.RuleId, rule.Id, StringComparison.Ordinal) &&
                        string.Equals(record.OutputPath, outputPath, StringComparison.Ordinal));
                    SpriteAtlasGenerationChange change = Classify(outputPath, previous, sourceFingerprint, contentFingerprint);
                    var entry = new SpriteAtlasGenerationEntry(rule.Id, outputPath, change, sourceInfo);
                    resolved.Rules.Add(new ResolvedRule
                    {
                        Rule = rule, Entry = entry, Sprites = sprites.ToArray(), Previous = previous,
                        SourceFingerprint = sourceFingerprint, ContentFingerprint = contentFingerprint,
                        Roots = roots, Exclusions = exclusions,
                    });
                    active.Add(outputPath);
                    Append(fingerprint, rule.Id, outputPath, sourceFingerprint, contentFingerprint,
                        change.ToString(), HashFile(outputPath), HashFile(outputPath + ".meta"));
                }

                var entries = resolved.Rules.Select(rule => rule.Entry).ToList();
                foreach (ManifestRecord retained in manifest.Outputs.Where(record => !active.Contains(record.OutputPath)))
                {
                    entries.Add(new SpriteAtlasGenerationEntry(retained.RuleId, retained.OutputPath,
                        SpriteAtlasGenerationChange.Retained, Array.Empty<SpriteAtlasGenerationSource>()));
                    Append(fingerprint, retained.RuleId, retained.OutputPath, HashFile(retained.OutputPath),
                        HashFile(retained.OutputPath + ".meta"));
                }
                resolved.PublicPlan = new SpriteAtlasGenerationPlan(path, settingsGuid, outputFolder, true,
                    HashText(fingerprint.ToString()), entries.OrderBy(entry => entry.OutputPath, StringComparer.Ordinal));
                return resolved;
            }

            /// <summary>读取已保存且未带未保存编辑的项目配置，保证磁盘文件是实际权威来源。</summary>
            /// <param name="path">精确配置路径。</param>
            /// <param name="guid">返回配置 GUID。</param>
            /// <returns>当前已保存配置。</returns>
            private static SpriteAtlasGenerationSettingsSO LoadSettings(string path, out string guid)
            {
                var settings = AssetDatabase.LoadAssetAtPath<SpriteAtlasGenerationSettingsSO>(path);
                guid = AssetDatabase.AssetPathToGUID(path);
                if (settings == null || string.IsNullOrEmpty(guid) || !File.Exists(Absolute(path)))
                    throw new InvalidOperationException("找不到已保存的图集生成配置：" + path);
                if (EditorUtility.IsDirty(settings))
                    throw new InvalidOperationException("图集生成配置有未保存编辑，请先保存配置：" + path);
                return settings;
            }

            /// <summary>验证当前项目已启用对应格式，不自动开启 Sprite Packer 或迁移其他图集。</summary>
            /// <param name="format">明确选择的格式。</param>
            private static void RequireFormat(SpriteAtlasGenerationFormat format)
            {
                SpritePackerMode mode = EditorSettings.spritePackerMode;
                bool supported = format == SpriteAtlasGenerationFormat.V1 &&
                    (mode == SpritePackerMode.BuildTimeOnlyAtlas || mode == SpritePackerMode.AlwaysOnAtlas) ||
                    format == SpriteAtlasGenerationFormat.V2 &&
                    (mode == SpritePackerMode.SpriteAtlasV2 || mode == SpritePackerMode.SpriteAtlasV2Build);
                if (!supported)
                    throw new InvalidOperationException($"图集格式 {format} 与项目当前 Sprite Packer 模式 {mode} 不匹配；工具不会自动切换或迁移。");
            }

            /// <summary>校验规则标识、文件名和已明确支持的打包参数，不替项目猜测默认目录。</summary>
            /// <param name="rule">当前规则。</param>
            /// <param name="ids">此前出现的规则标识。</param>
            private static void ValidateRule(SpriteAtlasGenerationRule rule, HashSet<string> ids)
            {
                if (string.IsNullOrEmpty(rule.Id) || rule.Id.Any(character =>
                        !(character >= 'a' && character <= 'z') && !(character >= 'A' && character <= 'Z') &&
                        !(character >= '0' && character <= '9') && character != '_' && character != '-'))
                    throw new InvalidOperationException("规则 Id 只允许字母、数字、下划线和连字符。");
                if (!ids.Add(rule.Id)) throw new InvalidOperationException("规则 Id 忽略大小写后重复：" + rule.Id);
                if (string.IsNullOrWhiteSpace(rule.AtlasName) || rule.AtlasName != rule.AtlasName.Trim() ||
                    !IsPortableSegment(rule.AtlasName) || rule.AtlasName.IndexOfAny(
                        new[] { '/', '\\', ':', '*', '?', '"', '<', '>', '|', '\0' }) >= 0 ||
                    rule.AtlasName.EndsWith(".spriteatlas", StringComparison.OrdinalIgnoreCase) ||
                    rule.AtlasName.EndsWith(".spriteatlasv2", StringComparison.OrdinalIgnoreCase))
                    throw new InvalidOperationException("图集文件名不合法：" + rule.AtlasName);
                if (rule.Padding != 2 && rule.Padding != 4 && rule.Padding != 8 && rule.Padding != 16 && rule.Padding != 32)
                    throw new InvalidOperationException("图集 Padding 只支持 2、4、8、16、32。");
            }

            /// <summary>验证所有显式源根，并拒绝源根互相覆盖或源与输出互相包含。</summary>
            /// <param name="rule">当前规则。</param>
            /// <param name="outputFolder">已声明输出根。</param>
            /// <param name="allSources">所有已声明规则的源根。</param>
            /// <returns>规范且有序的源根。</returns>
            private static List<string> ValidateRoots(SpriteAtlasGenerationRule rule, string outputFolder,
                List<string> allSources)
            {
                if (rule.SourceFolders == null || rule.SourceFolders.Count == 0)
                    throw new InvalidOperationException("规则必须明确声明至少一个源图目录：" + rule.Id);
                var roots = new List<string>();
                foreach (string value in rule.SourceFolders)
                {
                    string root = ValidateAssetPath(value, true);
                    if (!AssetDatabase.IsValidFolder(root)) throw new InvalidOperationException("源图目录未导入或不存在：" + root);
                    if (Overlaps(root, outputFolder)) throw new InvalidOperationException("源图与图集输出目录不能互相包含：" + root);
                    if (allSources.Any(existing => Overlaps(existing, root)))
                        throw new InvalidOperationException("图集源目录不能重复或互相包含：" + root);
                    allSources.Add(root);
                    roots.Add(root);
                }
                roots.Sort(StringComparer.Ordinal);
                return roots;
            }

            /// <summary>将完整路径排除项限制到本规则已声明源根内，不接受通配符或任意外部路径。</summary>
            /// <param name="rule">当前规则。</param>
            /// <param name="roots">本规则源根。</param>
            /// <returns>规范且去重的排除路径。</returns>
            private static List<string> ValidateExclusions(SpriteAtlasGenerationRule rule, List<string> roots)
            {
                if (rule.ExcludedPaths == null) throw new InvalidOperationException("ExcludedPaths 不能为 null。");
                var exclusions = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
                foreach (string value in rule.ExcludedPaths)
                {
                    string path = ValidateAssetPath(value, false);
                    if (!roots.Any(root => IsWithin(path, root)))
                        throw new InvalidOperationException("排除路径必须位于本规则源图目录内：" + path);
                    if (!exclusions.Add(path)) throw new InvalidOperationException("排除路径重复：" + path);
                }
                return exclusions.OrderBy(path => path, StringComparer.Ordinal).ToList();
            }

            /// <summary>在显式源根内只读遍历 TextureImporter 的 Sprite 子资产，保留多切图与真实持久化身份。</summary>
            /// <param name="roots">已声明源根。</param>
            /// <param name="exclusions">完整路径排除项。</param>
            /// <param name="sources">返回全部选中 Sprite 的身份。</param>
            /// <param name="sprites">返回用于打包的实际 Sprite 对象。</param>
            /// <returns>源文件、meta、路径及 Sprite 身份的指纹。</returns>
            private static string ReadSources(List<string> roots, List<string> exclusions,
                List<SpriteAtlasGenerationSource> sources, List<Object> sprites)
            {
                var sourceFingerprint = new StringBuilder();
                var names = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
                IEnumerable<string> files = roots.SelectMany(root => EnumerateSourceFiles(root, exclusions))
                    .OrderBy(path => path, StringComparer.Ordinal);
                foreach (string path in files)
                {
                    ValidateAssetPath(path, false);
                    if (exclusions.Any(exclusion => IsWithin(path, exclusion))) continue;
                    var importer = AssetImporter.GetAtPath(path) as TextureImporter;
                    if (importer == null || importer.textureType != TextureImporterType.Sprite) continue;
                    string guid = AssetDatabase.AssetPathToGUID(path);
                    if (string.IsNullOrEmpty(guid) || !string.Equals(AssetDatabase.GUIDToAssetPath(guid), path, StringComparison.Ordinal))
                        throw new InvalidOperationException("源图片路径大小写或导入身份不一致：" + path);
                    if (EditorUtility.IsDirty(importer)) throw new InvalidOperationException("源图片导入设置尚未保存：" + path);
                    Sprite[] members = AssetDatabase.LoadAllAssetsAtPath(path).OfType<Sprite>()
                        .OrderBy(sprite => sprite.name, StringComparer.Ordinal).ToArray();
                    if (members.Length == 0) throw new InvalidOperationException("Sprite 图片没有可用切图，请先完成导入：" + path);
                    Append(sourceFingerprint, path, guid, HashFile(path), HashFile(path + ".meta"));
                    foreach (Sprite sprite in members)
                    {
                        if (string.IsNullOrEmpty(sprite.name) || !names.Add(sprite.name))
                            throw new InvalidOperationException("同一图集内 Sprite 名称为空或忽略大小写后重复：" + sprite.name + "（" + path + "）");
                        if (!AssetDatabase.TryGetGUIDAndLocalFileIdentifier(sprite, out string spriteGuid, out long localId) ||
                            !string.Equals(spriteGuid, guid, StringComparison.Ordinal))
                            throw new InvalidOperationException("无法取得稳定 Sprite 子资产身份：" + path);
                        var source = new SpriteAtlasGenerationSource(path, spriteGuid, localId, sprite.name);
                        sources.Add(source);
                        sprites.Add(sprite);
                        Append(sourceFingerprint, spriteGuid, localId.ToString(System.Globalization.CultureInfo.InvariantCulture), sprite.name);
                    }
                }
                return HashText(sourceFingerprint.ToString());
            }

            /// <summary>逐层验证目录后再深入，避免递归枚举越过软链或陷入循环；排除目录不进入。</summary>
            /// <param name="root">已声明的精确源根。</param>
            /// <param name="exclusions">完整路径排除项。</param>
            /// <returns>范围内非 meta 文件的精确项目路径。</returns>
            private static IEnumerable<string> EnumerateSourceFiles(string root, List<string> exclusions)
            {
                var pending = new Stack<string>();
                pending.Push(root);
                while (pending.Count > 0)
                {
                    string directory = pending.Pop();
                    if (exclusions.Any(exclusion => IsWithin(directory, exclusion))) continue;
                    ValidateAssetPath(directory, true);
                    foreach (string child in Directory.EnumerateFileSystemEntries(Absolute(directory)))
                    {
                        string path = ToAssetPath(child);
                        if (exclusions.Any(exclusion => IsWithin(path, exclusion))) continue;
                        ValidateAssetPath(path, false);
                        if (Directory.Exists(child)) pending.Push(path);
                        else if (!path.EndsWith(".meta", StringComparison.OrdinalIgnoreCase)) yield return path;
                    }
                }
            }

            /// <summary>仅将作者设置与 Sprite 持久化身份纳入内容指纹；源像素改变只要求重新导入。</summary>
            /// <param name="rule">作者设置。</param>
            /// <param name="format">作者格式。</param>
            /// <param name="sources">实际 Sprite 身份。</param>
            /// <returns>可决定是否需要保存作者文件的指纹。</returns>
            private static string BuildContentFingerprint(SpriteAtlasGenerationRule rule,
                SpriteAtlasGenerationFormat format, List<SpriteAtlasGenerationSource> sources)
            {
                var content = new StringBuilder();
                Append(content, ((int)format).ToString(), rule.IncludeInBuild.ToString(), rule.Padding.ToString(),
                    rule.AllowRotation.ToString(), rule.TightPacking.ToString(), rule.GenerateMipMaps.ToString());
                foreach (SpriteAtlasGenerationSource source in sources.OrderBy(source => source.Guid, StringComparer.Ordinal)
                             .ThenBy(source => source.LocalId))
                    Append(content, source.Guid, source.LocalId.ToString(System.Globalization.CultureInfo.InvariantCulture), source.Name);
                return HashText(content.ToString());
            }

            /// <summary>只允许修改精确归属且未被外部编辑的输出；手工图集或孤立 meta 均拒绝接管。</summary>
            /// <param name="path">当前目标路径。</param>
            /// <param name="previous">同规则同路径的最近成功记录。</param>
            /// <param name="sourceFingerprint">当前源指纹。</param>
            /// <param name="contentFingerprint">当前作者内容指纹。</param>
            /// <returns>本次差异。</returns>
            private static SpriteAtlasGenerationChange Classify(string path, ManifestRecord previous,
                string sourceFingerprint, string contentFingerprint)
            {
                bool fileExists = File.Exists(Absolute(path));
                bool metaExists = File.Exists(Absolute(path + ".meta"));
                if (previous == null)
                {
                    if (fileExists || metaExists || Directory.Exists(Absolute(path)))
                        throw new InvalidOperationException("目标已存在但不属于本配置，拒绝覆盖：" + path);
                    return SpriteAtlasGenerationChange.Create;
                }
                if (!fileExists || !metaExists || !string.Equals(AssetDatabase.AssetPathToGUID(path), previous.Guid, StringComparison.Ordinal))
                    throw new InvalidOperationException("已归属图集缺失或 GUID 已变化，拒绝重新创建或接管：" + path);
                Object atlas = AssetDatabase.LoadAssetAtPath<RuntimeSpriteAtlas>(path);
                AssetImporter importer = AssetImporter.GetAtPath(path);
                if (atlas == null || EditorUtility.IsDirty(atlas) || importer == null || EditorUtility.IsDirty(importer))
                    throw new InvalidOperationException("图集类型不正确或存在未保存编辑：" + path);
                if (!string.Equals(HashFile(path), previous.FileHash, StringComparison.Ordinal) ||
                    !string.Equals(HashFile(path + ".meta"), previous.MetaHash, StringComparison.Ordinal))
                    throw new InvalidOperationException("已归属图集被其他操作修改，请先核对归属清单与人工编辑：" + path);
                if (!string.Equals(contentFingerprint, previous.ContentFingerprint, StringComparison.Ordinal))
                    return SpriteAtlasGenerationChange.Update;
                return string.Equals(sourceFingerprint, previous.SourceFingerprint, StringComparison.Ordinal)
                    ? SpriteAtlasGenerationChange.Unchanged : SpriteAtlasGenerationChange.Reimport;
            }

            /// <summary>只读解析当前输出根的归属清单，并拒绝未知版本、其他配置或越界记录。</summary>
            /// <param name="path">固定清单路径。</param>
            /// <param name="ownerGuid">本配置 GUID。</param>
            /// <param name="outputFolder">本次声明输出根。</param>
            /// <returns>经过核对的归属记录。</returns>
            private static Manifest ReadManifest(string path, string ownerGuid, string outputFolder)
            {
                if (!File.Exists(Absolute(path)))
                {
                    if (Directory.Exists(Absolute(path)) || File.Exists(Absolute(path + ".meta")))
                        throw new InvalidOperationException("归属清单路径被占用或遗留孤立 meta：" + path);
                    return new Manifest { OwnerGuid = ownerGuid };
                }
                Manifest manifest;
                try { manifest = JsonUtility.FromJson<Manifest>(File.ReadAllText(Absolute(path))); }
                catch (Exception exception) { throw new InvalidOperationException("图集归属清单无法读取：" + path, exception); }
                if (manifest == null || manifest.Version != c_ManifestVersion || manifest.OwnerGuid != ownerGuid || manifest.Outputs == null)
                    throw new InvalidOperationException("图集归属清单版本或配置 GUID 不匹配：" + path);
                var paths = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
                foreach (ManifestRecord record in manifest.Outputs)
                {
                    if (record == null || string.IsNullOrEmpty(record.RuleId) || string.IsNullOrEmpty(record.Guid) ||
                        string.IsNullOrEmpty(record.ContentFingerprint) || string.IsNullOrEmpty(record.SourceFingerprint) ||
                        string.IsNullOrEmpty(record.FileHash) || string.IsNullOrEmpty(record.MetaHash))
                        throw new InvalidOperationException("图集归属清单记录不完整：" + path);
                    string output = ValidateAssetPath(record.OutputPath, false);
                    if (!IsWithin(output, outputFolder) || output == outputFolder ||
                        !string.Equals(Path.GetDirectoryName(output)?.Replace('\\', '/'), outputFolder, StringComparison.Ordinal) ||
                        !(output.EndsWith(".spriteatlas", StringComparison.Ordinal) || output.EndsWith(".spriteatlasv2", StringComparison.Ordinal)) ||
                        !paths.Add(output))
                        throw new InvalidOperationException("图集归属清单包含越界或重复输出：" + output);
                }
                return manifest;
            }

            /// <summary>仅使用已核对的 Unity 作者 API 原地保存，不删除或重命名源图片。</summary>
            /// <param name="settings">本次正式配置。</param>
            /// <param name="resolved">本次真实解析规则。</param>
            private static void WriteAtlas(SpriteAtlasGenerationSettingsSO settings, ResolvedRule resolved)
            {
                string path = resolved.Entry.OutputPath;
                if (resolved.Entry.Change == SpriteAtlasGenerationChange.Reimport)
                {
                    AssetDatabase.ImportAsset(path, ImportAssetOptions.ForceUpdate | ImportAssetOptions.ForceSynchronousImport);
                }
                else if (settings.Format == SpriteAtlasGenerationFormat.V1)
                {
                    RuntimeSpriteAtlas atlas = resolved.Previous == null
                        ? new RuntimeSpriteAtlas() : AssetDatabase.LoadAssetAtPath<RuntimeSpriteAtlas>(path);
                    try
                    {
                        atlas.name = Path.GetFileNameWithoutExtension(path);
                        atlas.Remove(atlas.GetPackables());
                        atlas.Add(resolved.Sprites);
                        SpriteAtlasPackingSettings packing = atlas.GetPackingSettings();
                        ConfigurePacking(ref packing, resolved.Rule);
                        atlas.SetPackingSettings(packing);
                        SpriteAtlasTextureSettings texture = atlas.GetTextureSettings();
                        texture.generateMipMaps = resolved.Rule.GenerateMipMaps;
                        atlas.SetTextureSettings(texture);
                        atlas.SetIncludeInBuild(resolved.Rule.IncludeInBuild);
                        if (resolved.Previous == null) AssetDatabase.CreateAsset(atlas, path);
                        EditorUtility.SetDirty(atlas);
                        AssetDatabase.SaveAssetIfDirty(atlas);
                        AssetDatabase.ImportAsset(path, ImportAssetOptions.ForceSynchronousImport);
                    }
                    finally
                    {
                        if (atlas != null && !EditorUtility.IsPersistent(atlas)) Object.DestroyImmediate(atlas);
                    }
                }
                else
                {
                    SpriteAtlasAsset authoring = resolved.Previous == null ? new SpriteAtlasAsset() : SpriteAtlasAsset.Load(path);
                    if (authoring == null) throw new InvalidOperationException("无法读取 V2 图集作者文件：" + path);
                    try
                    {
                        authoring.name = Path.GetFileNameWithoutExtension(path);
                        if (resolved.Previous != null)
                            authoring.Remove(AssetDatabase.LoadAssetAtPath<RuntimeSpriteAtlas>(path).GetPackables());
                        authoring.Add(resolved.Sprites);
                        SpriteAtlasAsset.Save(authoring, path);
                    }
                    finally { Object.DestroyImmediate(authoring); }
                    AssetDatabase.ImportAsset(path, ImportAssetOptions.ForceSynchronousImport);
                    var importer = AssetImporter.GetAtPath(path) as SpriteAtlasImporter;
                    if (importer == null) throw new InvalidOperationException("V2 图集没有匹配 SpriteAtlasImporter：" + path);
                    SpriteAtlasPackingSettings packing = importer.packingSettings;
                    ConfigurePacking(ref packing, resolved.Rule);
                    importer.packingSettings = packing;
                    SpriteAtlasTextureSettings texture = importer.textureSettings;
                    texture.generateMipMaps = resolved.Rule.GenerateMipMaps;
                    importer.textureSettings = texture;
                    importer.includeInBuild = resolved.Rule.IncludeInBuild;
                    importer.SaveAndReimport();
                }
                string guid = AssetDatabase.AssetPathToGUID(path);
                if (string.IsNullOrEmpty(guid) || resolved.Previous != null && guid != resolved.Previous.Guid ||
                    AssetDatabase.LoadAssetAtPath<RuntimeSpriteAtlas>(path) == null)
                    throw new InvalidOperationException("图集输出没有正确导入或已有 GUID 未保持：" + path);
            }

            /// <summary>将本配置拥有的最小打包字段写入对应 Unity 设置，不调整未声明的平台压缩参数。</summary>
            /// <param name="packing">当前格式的 Unity 打包设置。</param>
            /// <param name="rule">本次已校验规则。</param>
            private static void ConfigurePacking(ref SpriteAtlasPackingSettings packing, SpriteAtlasGenerationRule rule)
            {
                packing.padding = rule.Padding;
                packing.enableRotation = rule.AllowRotation;
                packing.enableTightPacking = rule.TightPacking;
            }

            /// <summary>验证严格项目路径、逐段精确大小写及软链，输出目录允许尚未存在。</summary>
            /// <param name="value">未经规范化的用户路径。</param>
            /// <param name="directory">路径是否应当表示目录。</param>
            /// <returns>原样通过核对的规范 Assets 子路径。</returns>
            private static string ValidateAssetPath(string value, bool directory)
            {
                if (string.IsNullOrWhiteSpace(value) || value != value.Trim() || !value.StartsWith("Assets/", StringComparison.Ordinal) ||
                    value.IndexOfAny(new[] { '\\', ':', '*', '?', '"', '<', '>', '|', '\0' }) >= 0)
                    throw new InvalidOperationException("路径必须是大小写准确、不含通配符的项目 Assets 子路径：" + value);
                string[] segments = value.Split('/');
                if (segments.Any(segment => !IsPortableSegment(segment)))
                    throw new InvalidOperationException("路径包含空段、相对跳转或不稳定的文件名：" + value);
                string current = ProjectRoot();
                foreach (string segment in segments)
                {
                    if (Directory.Exists(current))
                    {
                        string match = Directory.EnumerateFileSystemEntries(current).Select(Path.GetFileName)
                            .FirstOrDefault(name => string.Equals(name, segment, StringComparison.OrdinalIgnoreCase));
                        if (match != null && !string.Equals(match, segment, StringComparison.Ordinal))
                            throw new InvalidOperationException("路径大小写与磁盘不一致：" + value);
                    }
                    current = Path.Combine(current, segment);
                    if ((File.Exists(current) || Directory.Exists(current)) &&
                        (File.GetAttributes(current) & FileAttributes.ReparsePoint) != 0)
                        throw new InvalidOperationException("图集路径不能经过软链或重解析点：" + value);
                }
                if (directory && File.Exists(current)) throw new InvalidOperationException("目录路径被文件占用：" + value);
                return value;
            }

            /// <summary>拒绝跨平台不稳定的控制字符、尾部点或空白、相对段与 Windows 保留文件名。</summary>
            /// <param name="segment">单个路径段或不含扩展名的图集文件名。</param>
            /// <returns>可在支持平台上稳定表示时为 true。</returns>
            private static bool IsPortableSegment(string segment)
            {
                if (string.IsNullOrEmpty(segment) || segment == "." || segment == ".." || segment != segment.Trim() ||
                    segment.EndsWith(".", StringComparison.Ordinal) || segment.Any(char.IsControl)) return false;
                string stem = segment.Split('.')[0].ToUpperInvariant();
                if (stem == "CON" || stem == "PRN" || stem == "AUX" || stem == "NUL") return false;
                return !(stem.Length == 4 && (stem.StartsWith("COM", StringComparison.Ordinal) || stem.StartsWith("LPT", StringComparison.Ordinal)) &&
                    stem[3] >= '1' && stem[3] <= '9');
            }

            /// <summary>判断两个规范目录是否互相包含，跨平台忽略大小写。</summary>
            /// <param name="left">第一个路径。</param>
            /// <param name="right">第二个路径。</param>
            /// <returns>相等或存在包含关系时为 true。</returns>
            private static bool Overlaps(string left, string right) => IsWithin(left, right) || IsWithin(right, left);

            /// <summary>按完整路径边界判断包含，避免把相同前缀误当作子目录。</summary>
            /// <param name="path">目标路径。</param>
            /// <param name="root">根路径。</param>
            /// <returns>目标是根或根的后代时为 true。</returns>
            private static bool IsWithin(string path, string root) => string.Equals(path, root, StringComparison.OrdinalIgnoreCase) ||
                path.StartsWith(root + "/", StringComparison.OrdinalIgnoreCase);

            /// <summary>返回当前 Editor 工程根，不读取工作目录或猜测安装包缓存路径。</summary>
            /// <returns>当前工程绝对根目录。</returns>
            private static string ProjectRoot() => Directory.GetParent(Application.dataPath).FullName;

            /// <summary>将已经验证的项目相对路径映射到当前工程，不允许外部绝对路径。</summary>
            /// <param name="assetPath">已验证的 Assets 相对路径。</param>
            /// <returns>当前工程内的绝对路径。</returns>
            private static string Absolute(string assetPath) => Path.Combine(ProjectRoot(), assetPath);

            /// <summary>将声明根内遍历得到的绝对文件路径映射回精确项目路径。</summary>
            /// <param name="absolutePath">声明源根内的绝对路径。</param>
            /// <returns>正斜杠分隔的 Assets 路径。</returns>
            private static string ToAssetPath(string absolutePath) => absolutePath.Substring(ProjectRoot().Length + 1).Replace('\\', '/');

            /// <summary>读取一个文件的 SHA-256；不存在返回空串，目录占用由上层单独拒绝。</summary>
            /// <param name="assetPath">已验证的项目路径，可包含 .meta 后缀。</param>
            /// <returns>小写十六进制 Hash 或空串。</returns>
            private static string HashFile(string assetPath) => HashAbsoluteFile(Absolute(assetPath));

            /// <summary>读取已冻结绝对文件的 SHA-256，不改动其时间戳或内容。</summary>
            /// <param name="path">已经核对范围的绝对文件路径。</param>
            /// <returns>小写十六进制 Hash；文件不存在时为空串。</returns>
            private static string HashAbsoluteFile(string path)
            {
                if (!File.Exists(path)) return "";
                using (SHA256 hash = SHA256.Create())
                using (FileStream stream = File.OpenRead(path))
                    return BitConverter.ToString(hash.ComputeHash(stream)).Replace("-", "").ToLowerInvariant();
            }

            /// <summary>计算确定性 UTF-8 文本指纹。</summary>
            /// <param name="value">已按顺序拼接的文本。</param>
            /// <returns>SHA-256 小写十六进制。</returns>
            private static string HashText(string value)
            {
                using (SHA256 hash = SHA256.Create())
                    return BitConverter.ToString(hash.ComputeHash(Encoding.UTF8.GetBytes(value))).Replace("-", "").ToLowerInvariant();
            }

            /// <summary>采用长度前缀拼接指纹字段，避免路径或名称中的分隔符产生相同拼接。</summary>
            /// <param name="builder">当前指纹文本。</param>
            /// <param name="values">有序字段。</param>
            private static void Append(StringBuilder builder, params string[] values)
            {
                foreach (string value in values)
                {
                    string text = value ?? "";
                    builder.Append(text.Length).Append(':').Append(text);
                }
            }
        }
    }
}
