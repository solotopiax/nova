/***************************************************************
 * (c) copyright 2026 - 2030, Solotopia
 * All Rights Reserved.
 * -------------------------------------------------------------
 * filename:  SpriteAtlasGenerationDefinitions.cs
 * author:    taoye
 * created:   2026/11/12
 * descrip:   图集预演、源 Sprite 身份和生成结果的只读契约
 ***************************************************************/

using System;
using System.Collections.Generic;
using System.Linq;

namespace NovaFramework.Editor
{
    /// <summary>一个已声明输出相对上次成功生成的差异。</summary>
    public enum SpriteAtlasGenerationChange
    {
        /// <summary>输出尚不存在，且没有无归属资产占用目标。</summary>
        Create,
        /// <summary>图块引用或声明的打包设置变化，需要原地保存。</summary>
        Update,
        /// <summary>仅源内容、导入设置或路径变化，只重新导入图集。</summary>
        Reimport,
        /// <summary>输入与归属输出均未变化，不写入或重新导入。</summary>
        Unchanged,
        /// <summary>原规则已经移除或输出改名；保留旧输出，不自动删除。</summary>
        Retained,
    }

    /// <summary>一个真实 Sprite 子资产的身份；同名 Sprite 不会被自动改名。</summary>
    public sealed class SpriteAtlasGenerationSource
    {
        /// <summary>Sprite 所在源图片的精确项目路径。</summary>
        public string AssetPath { get; }
        /// <summary>源图片的 Unity GUID。</summary>
        public string Guid { get; }
        /// <summary>Sprite 子资产在源图片内的持久化 local file ID。</summary>
        public long LocalId { get; }
        /// <summary>运行时用于图集查询的原始 Sprite 名称。</summary>
        public string Name { get; }

        /// <summary>建立已经核对过的 Sprite 身份快照。</summary>
        /// <param name="assetPath">源图片路径。</param>
        /// <param name="guid">源图片 GUID。</param>
        /// <param name="localId">Sprite 子资产 ID。</param>
        /// <param name="name">原始名称。</param>
        internal SpriteAtlasGenerationSource(string assetPath, string guid, long localId, string name)
        {
            AssetPath = assetPath;
            Guid = guid;
            LocalId = localId;
            Name = name;
        }
    }

    /// <summary>单个输出的只读预演结果，不持有可以修改配置的对象。</summary>
    public sealed class SpriteAtlasGenerationEntry
    {
        /// <summary>稳定规则标识。</summary>
        public string RuleId { get; }
        /// <summary>本次输出或保留的精确路径。</summary>
        public string OutputPath { get; }
        /// <summary>本次差异类型。</summary>
        public SpriteAtlasGenerationChange Change { get; }
        /// <summary>本次实际选择的全部 Sprite 子资产；保留项为空。</summary>
        public IReadOnlyList<SpriteAtlasGenerationSource> Sources { get; }

        /// <summary>建立一个输出的不可变预演条目。</summary>
        /// <param name="ruleId">规则标识。</param>
        /// <param name="outputPath">输出路径。</param>
        /// <param name="change">本次差异。</param>
        /// <param name="sources">实际 Sprite 身份。</param>
        internal SpriteAtlasGenerationEntry(string ruleId, string outputPath,
            SpriteAtlasGenerationChange change, IEnumerable<SpriteAtlasGenerationSource> sources)
        {
            RuleId = ruleId;
            OutputPath = outputPath;
            Change = change;
            Sources = Array.AsReadOnly(sources.ToArray());
        }
    }

    /// <summary>
    /// 图集生成的不可变预演结果。Apply 会重新核对配置、源文件、meta、归属清单与输出文件，
    /// 任一输入漂移均拒绝执行；不能把旧预演直接用于新输入。
    /// </summary>
    public sealed class SpriteAtlasGenerationPlan
    {
        /// <summary>权威配置的精确项目路径。</summary>
        public string SettingsAssetPath { get; }
        /// <summary>权威配置的 Unity GUID。</summary>
        public string SettingsGuid { get; }
        /// <summary>已声明的输出目录；未启用配置允许为空。</summary>
        public string OutputFolder { get; }
        /// <summary>权威配置是否已明确启用。</summary>
        public bool Enabled { get; }
        /// <summary>本次配置、输入与目标状态的 SHA-256 指纹。</summary>
        public string Fingerprint { get; }
        /// <summary>按输出路径排序的差异条目。</summary>
        public IReadOnlyList<SpriteAtlasGenerationEntry> Entries { get; }
        /// <summary>是否需要创建、保存或重新导入至少一个图集。</summary>
        public bool HasChanges => Entries.Any(entry => entry.Change != SpriteAtlasGenerationChange.Unchanged &&
                                                       entry.Change != SpriteAtlasGenerationChange.Retained);

        /// <summary>建立已冻结的配置与差异快照。</summary>
        /// <param name="path">配置路径。</param>
        /// <param name="guid">配置 GUID。</param>
        /// <param name="outputFolder">输出目录。</param>
        /// <param name="enabled">启用状态。</param>
        /// <param name="fingerprint">本次指纹。</param>
        /// <param name="entries">差异条目。</param>
        internal SpriteAtlasGenerationPlan(string path, string guid, string outputFolder, bool enabled,
            string fingerprint, IEnumerable<SpriteAtlasGenerationEntry> entries)
        {
            SettingsAssetPath = path;
            SettingsGuid = guid;
            OutputFolder = outputFolder;
            Enabled = enabled;
            Fingerprint = fingerprint;
            Entries = Array.AsReadOnly(entries.ToArray());
        }
    }

    /// <summary>本次已经完成的图集生产结果；不代表资源包、Player 或设备运行验收。</summary>
    public sealed class SpriteAtlasGenerationResult
    {
        /// <summary>真正创建、保存或重新导入的图集路径。</summary>
        public IReadOnlyList<string> ChangedAssetPaths { get; }
        /// <summary>新建的图集数量。</summary>
        public int CreatedCount { get; }
        /// <summary>原地保存的图集数量。</summary>
        public int UpdatedCount { get; }
        /// <summary>仅重新导入的图集数量。</summary>
        public int ReimportedCount { get; }

        /// <summary>从本次成功应用的差异建立结果。</summary>
        /// <param name="entries">实际应用的差异条目。</param>
        internal SpriteAtlasGenerationResult(IEnumerable<SpriteAtlasGenerationEntry> entries)
        {
            SpriteAtlasGenerationEntry[] changed = entries.Where(entry =>
                entry.Change != SpriteAtlasGenerationChange.Unchanged &&
                entry.Change != SpriteAtlasGenerationChange.Retained).ToArray();
            ChangedAssetPaths = Array.AsReadOnly(changed.Select(entry => entry.OutputPath).ToArray());
            CreatedCount = changed.Count(entry => entry.Change == SpriteAtlasGenerationChange.Create);
            UpdatedCount = changed.Count(entry => entry.Change == SpriteAtlasGenerationChange.Update);
            ReimportedCount = changed.Count(entry => entry.Change == SpriteAtlasGenerationChange.Reimport);
        }
    }
}
