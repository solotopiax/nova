/***************************************************************
 * (c) copyright 2026 - 2030, Solotopia
 * All Rights Reserved.
 * -------------------------------------------------------------
 * filename:  ResourceKeyStore.cs
 * author:    taoye
 * created:   2026/10/9
 * descrip:   Editor ConfigMaster 资源密钥存储，不进入 ConfigRuntime
 ***************************************************************/

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Newtonsoft.Json;
using NovaFramework.Runtime;
using UnityEngine;
using Coord = NovaFramework.Editor.EditorUtil.Config.DimensionProjector.Coord;
using DimensionAxis = NovaFramework.Editor.EditorUtil.Config.DimensionProjector.DimensionAxis;
using IOPath = System.IO.Path;

namespace NovaFramework.Editor
{
    /// <summary>ConfigMaster 的 Editor 资源密钥配置；通过全局保存写入资产。</summary>
    [Serializable]
    public sealed class ResourceKeyStoreData
    {
        /// <summary>当前加密编号的独立三维掩码；密钥字典仍由各环境共用。</summary>
        public PanelDimensionMask DimensionMask = new PanelDimensionMask();
        /// <summary>客户端源码输出目录；使用 Assets 开头的项目相对路径，由项目组选择。</summary>
        public string ClientCodeOutputDirectory = string.Empty;
        /// <summary>上次生成的源码路径；用于安全迁移生成文件，不作为独立配置入口。</summary>
        [HideInInspector]
        public string GeneratedCodeAssetPath = string.Empty;
        /// <summary>按精确包裹名保存的密钥列表。</summary>
        public List<ResourcePackageKeys> Packages = new List<ResourcePackageKeys>();
    }

    /// <summary>单个 YooAsset 包裹的当前加密编号与保留解密密钥。</summary>
    [Serializable]
    public sealed class ResourcePackageKeys
    {
        /// <summary>大小写敏感的 YooAsset 包裹名。</summary>
        public string PackageName;
        /// <summary>构建加密使用的密钥编号；必须存在于 Keys。</summary>
        public string ActiveKeyId;
        /// <summary>按所勾选维度保存的当前编号；旧文件缺失此列表时沿用 ActiveKeyId。</summary>
        public List<ResourceActiveKeyOverride> ActiveKeyOverrides = new List<ResourceActiveKeyOverride>();
        /// <summary>当前与旧资源解密需要保留的全部密钥。</summary>
        public List<ResourceKeyEntry> Keys = new List<ResourceKeyEntry>();
    }

    /// <summary>一个逻辑维度组的加密编号；不重复存储真实密钥。</summary>
    [Serializable]
    public sealed class ResourceActiveKeyOverride
    {
        /// <summary>平台轴；未勾选时使用 None。</summary>
        public PlatformType Platform;
        /// <summary>渠道轴；未勾选时使用 None。</summary>
        public ChannelType Channel;
        /// <summary>开发模式轴；未勾选时使用 Debug 哨兵。</summary>
        public DevelopMode Mode;
        /// <summary>对应逻辑组的新资源加密编号。</summary>
        public string ActiveKeyId;
    }

    /// <summary>编号与 32 字节 Base64 密钥；不得输出到日志。</summary>
    [Serializable]
    public sealed class ResourceKeyEntry
    {
        /// <summary>包裹内唯一且大小写敏感的密钥编号。</summary>
        public string KeyId;
        /// <summary>恰好 32 字节密钥的 Base64 表示，不可记录日志。</summary>
        public string KeyBase64;
    }

    /// <summary>从激活且已保存的 ConfigMaster 读取资源密钥；窗口草稿不影响构建。</summary>
    public static class ResourceKeyStore
    {
        internal const string LegacyRelativePath = "ProjectSettings/NovaResourceKeys.json";

        /// <summary>只读加载激活配置；不读取窗口草稿或旧 JSON，不存在时明确失败。</summary>
        public static ResourceKeyStoreData Load()
        {
            ConfigMasterSO master = EditorUtil.Config.WorkspaceActive.Get();
            if (master == null) throw new InvalidDataException("请先绑定并保存 Config，再构建加密资源。");
            Validate(master.ResourceEncryption);
            return master.ResourceEncryption;
        }

        /// <summary>兼容旧 Save 调用，但明确阻止绕过 Config 全局保存；不再写独立 JSON。</summary>
        [Obsolete("资源密钥已纳入 ConfigMaster，请通过 Config 顶部保存按钮保存。")]
        public static void Save(ResourceKeyStoreData data) => throw new InvalidOperationException(
            "资源密钥请通过 Config 顶部“保存”统一保存。");

        /// <summary>将旧项目 JSON 一次性迁入工作副本；保留已配置的新数据，不在此处删除旧文件。</summary>
        internal static void MigrateLegacy(ConfigMasterSO workingCopy)
        {
            if (workingCopy.ResourceKeysMigrated) return;
            string path = IOPath.Combine(IOPath.GetDirectoryName(Application.dataPath), LegacyRelativePath);
            if (File.Exists(path) && workingCopy.ResourceEncryption.Packages.Count == 0)
            {
                ResourceKeyStoreData data;
                try { data = JsonConvert.DeserializeObject<ResourceKeyStoreData>(File.ReadAllText(path)); }
                catch (JsonException) { throw new InvalidDataException("旧资源密钥配置格式无效，请检查文件结构。"); }
                Validate(data);
                data.ClientCodeOutputDirectory = "Assets/NovaGenerated/ResourceKeys";
                data.GeneratedCodeAssetPath = data.ClientCodeOutputDirectory + "/ResourceKeys.Generated.cs";
                workingCopy.ResourceEncryption = data;
            }
            workingCopy.ResourceKeysMigrated = true;
        }

        /// <summary>保存成功后仅在数据完全一致时移除已迁移 JSON，避免误删其他配置的密钥。</summary>
        internal static void CompleteLegacyMigration(ConfigMasterSO master)
        {
            string path = IOPath.Combine(IOPath.GetDirectoryName(Application.dataPath), LegacyRelativePath);
            if (!master.ResourceKeysMigrated || !File.Exists(path)) return;
            ResourceKeyStoreData legacy = JsonConvert.DeserializeObject<ResourceKeyStoreData>(File.ReadAllText(path));
            Validate(legacy);
            // 强制重新导入已保存资产，核对磁盘来源后才能删除旧 JSON；保存回调跳过写入时保留旧来源。
            string assetPath = UnityEditor.AssetDatabase.GetAssetPath(master);
            UnityEditor.AssetDatabase.ImportAsset(assetPath, UnityEditor.ImportAssetOptions.ForceUpdate | UnityEditor.ImportAssetOptions.ForceSynchronousImport);
            ConfigMasterSO persisted = UnityEditor.AssetDatabase.LoadAssetAtPath<ConfigMasterSO>(assetPath);
            if (persisted == null || !persisted.ResourceKeysMigrated) return;
            // 路径属于新配置；只比较迁移前原有的密钥、维度与当前编号。
            if (JsonConvert.SerializeObject(legacy.DimensionMask) == JsonConvert.SerializeObject(persisted.ResourceEncryption.DimensionMask) &&
                JsonConvert.SerializeObject(legacy.Packages) == JsonConvert.SerializeObject(persisted.ResourceEncryption.Packages))
                File.Delete(path);
        }

        /// <summary>校验全部包裹、编号与 32 字节密钥；错误只报告位置和规则，不携带密钥内容。</summary>
        public static void Validate(ResourceKeyStoreData data)
        {
            if (data == null || data.Packages == null || data.DimensionMask == null)
                throw new InvalidDataException("资源密钥配置缺少 Packages 列表。");
            HashSet<string> packages = new HashSet<string>(StringComparer.Ordinal);
            for (int packageIndex = 0; packageIndex < data.Packages.Count; packageIndex++)
            {
                ResourcePackageKeys package = data.Packages[packageIndex];
                string position = $"第 {packageIndex + 1} 个包裹";
                if (package == null || !IsValidIdentifier(package.PackageName))
                    throw new InvalidDataException(position + "的包裹名须为 1～64 个非控制字符，且无首尾空白。");
                if (!packages.Add(package.PackageName))
                    throw new InvalidDataException(position + "的包裹名重复。");
                if (package.Keys == null) throw new InvalidDataException(position + "缺少 Keys 列表。");
                HashSet<string> keys = new HashSet<string>(StringComparer.Ordinal);
                for (int keyIndex = 0; keyIndex < package.Keys.Count; keyIndex++)
                {
                    ResourceKeyEntry key = package.Keys[keyIndex];
                    string keyPosition = position + $"第 {keyIndex + 1} 个密钥";
                    if (key == null || !IsValidIdentifier(key.KeyId))
                        throw new InvalidDataException(keyPosition + "的编号须为 1～64 个非控制字符，且无首尾空白。");
                    if (!keys.Add(key.KeyId)) throw new InvalidDataException(keyPosition + "的编号重复。");
                    byte[] bytes = null;
                    try
                    {
                        bytes = Convert.FromBase64String(key.KeyBase64 ?? string.Empty);
                        if (bytes.Length != 32) throw new InvalidDataException(keyPosition + "须为 32 字节 Base64 密钥。");
                    }
                    catch (FormatException) { throw new InvalidDataException(keyPosition + "须为 32 字节 Base64 密钥。"); }
                    finally { if (bytes != null) Array.Clear(bytes, 0, bytes.Length); }
                }
                if (package.Keys.Count > 0 && !keys.Contains(package.ActiveKeyId ?? string.Empty))
                    throw new InvalidDataException(position + "必须从已保留的密钥中选择当前加密编号。");
                if (package.Keys.Count == 0 && !string.IsNullOrEmpty(package.ActiveKeyId))
                    throw new InvalidDataException(position + "没有密钥，当前加密编号必须为空。");
                if (package.ActiveKeyOverrides == null)
                    throw new InvalidDataException(position + "缺少维度编号列表。");
                HashSet<string> groups = new HashSet<string>(StringComparer.Ordinal);
                foreach (ResourceActiveKeyOverride entry in package.ActiveKeyOverrides)
                {
                    if (entry == null || data.DimensionMask.IsGlobal)
                        throw new InvalidDataException(position + "的维度编号与维度开关不一致。");
                    Coord coord = new Coord(entry.Platform, entry.Channel, entry.Mode);
                    ValidateCoord(data.DimensionMask, coord);
                    Coord clipped = ClipCoord(data.DimensionMask, coord);
                    if (clipped.Platform != entry.Platform || clipped.Channel != entry.Channel || clipped.Mode != entry.Mode ||
                        !groups.Add($"{entry.Platform}/{entry.Channel}/{entry.Mode}"))
                        throw new InvalidDataException(position + "的维度编号坐标重复或未正确归组。");
                    if (package.Keys.Count > 0 ? !keys.Contains(entry.ActiveKeyId ?? string.Empty) : !string.IsNullOrEmpty(entry.ActiveKeyId))
                        throw new InvalidDataException(position + "的维度当前编号不在已保留密钥中。");
                }
            }
        }

        /// <summary>只读获取指定包裹；不存在时抛出不含密钥的配置错误。</summary>
        public static ResourcePackageKeys GetPackage(string packageName)
        {
            ResourcePackageKeys package = Load().Packages.FirstOrDefault(item => item.PackageName == packageName);
            return package ?? throw new InvalidDataException("资源包裹缺少密钥配置，请打开 Config 的资源加解密配置。");
        }

        /// <summary>只读获取构建时的当前加密密钥；空列表或未选当前编号时阻断构建。</summary>
        public static ResourceKeyEntry GetActiveKey(string packageName)
        {
            ResourceKeyStoreData data = Load();
            return GetActiveKey(data, packageName, GetBuildCoord(data.DimensionMask));
        }

        /// <summary>使用构建入口明确提供的坐标取当前编号；不依赖窗口当前正在编辑的坐标。</summary>
        public static ResourceKeyEntry GetActiveKey(string packageName, Coord coord)
        {
            return GetActiveKey(Load(), packageName, coord);
        }

        /// <summary>从已加载配置取当前密钥；维度未配置时兼容旧文件的全局编号。</summary>
        internal static ResourceKeyEntry GetActiveKey(ResourceKeyStoreData data, string packageName, Coord coord)
        {
            ResourcePackageKeys package = data.Packages.FirstOrDefault(item => item.PackageName == packageName)
                ?? throw new InvalidDataException("资源包裹缺少密钥配置，请打开 Config 的资源加解密配置。");
            string id = ResolveActiveKeyId(data, package, coord);
            ResourceKeyEntry key = package.Keys.FirstOrDefault(item => item.KeyId == id);
            return key ?? throw new InvalidDataException("资源包裹缺少当前加密密钥，请打开 Config 的资源加解密配置。");
        }

        /// <summary>构建使用实际目标平台及激活 Config 已保存的渠道、模式；全局配置无需绑定 Master。</summary>
        internal static Coord GetBuildCoord(PanelDimensionMask mask, PlatformType? platform = null)
        {
            ConfigMasterSO master = mask.ByChannel || mask.ByDevelopMode ? EditorUtil.Config.WorkspaceActive.Get() : null;
            if ((mask.ByChannel || mask.ByDevelopMode) && master == null)
                throw new InvalidDataException("资源密钥按渠道或开发模式分别配置，请先绑定并保存 Config，再构建资源。");
            Coord coord = new Coord(platform ?? (mask.ByPlatform ? EditorUtil.Config.ActivePlatform.RequireCurrent("资源加密") : PlatformType.None),
                master != null ? master.CurrentChannel : ChannelType.None,
                master != null ? master.CurrentDevelopMode : DevelopMode.Debug);
            ValidateCoord(mask, coord);
            return coord;
        }

        /// <summary>按掩码解析当前编号；未单独保存的组合使用全局初始编号。</summary>
        internal static string ResolveActiveKeyId(ResourceKeyStoreData data, ResourcePackageKeys package, Coord coord)
        {
            ValidateCoord(data.DimensionMask, coord);
            if (data.DimensionMask.IsGlobal) return package.ActiveKeyId;
            Coord clipped = ClipCoord(data.DimensionMask, coord);
            ResourceActiveKeyOverride entry = package.ActiveKeyOverrides.FirstOrDefault(item =>
                item.Platform == clipped.Platform && item.Channel == clipped.Channel && item.Mode == clipped.Mode);
            return entry == null ? package.ActiveKeyId : entry.ActiveKeyId;
        }

        /// <summary>设置当前逻辑组编号，未勾选的维度自然共用同一条记录。</summary>
        internal static void SetActiveKeyId(ResourceKeyStoreData data, ResourcePackageKeys package, Coord coord, string id)
        {
            ValidateCoord(data.DimensionMask, coord);
            if (data.DimensionMask.IsGlobal) { package.ActiveKeyId = id; return; }
            // 原基准密钥被删除后，首次重新选择也恢复未单独配置组合的有效基准。
            if (string.IsNullOrEmpty(package.ActiveKeyId)) package.ActiveKeyId = id;
            Coord clipped = ClipCoord(data.DimensionMask, coord);
            ResourceActiveKeyOverride entry = package.ActiveKeyOverrides.FirstOrDefault(item =>
                item.Platform == clipped.Platform && item.Channel == clipped.Channel && item.Mode == clipped.Mode);
            if (entry == null)
            {
                entry = new ResourceActiveKeyOverride { Platform = clipped.Platform, Channel = clipped.Channel, Mode = clipped.Mode };
                package.ActiveKeyOverrides.Add(entry);
            }
            entry.ActiveKeyId = id;
        }

        /// <summary>空包新增首把密钥时初始化全部空逻辑组，避免其他环境留着无法保存的空编号。</summary>
        internal static void InitializeFirstActiveKey(ResourcePackageKeys package, string id)
        {
            package.ActiveKeyId = id;
            foreach (ResourceActiveKeyOverride entry in package.ActiveKeyOverrides)
                if (string.IsNullOrEmpty(entry.ActiveKeyId)) entry.ActiveKeyId = id;
        }

        /// <summary>冻结所有旧逻辑组后原子切换维度；减维只合并编号，不删除任何解密密钥。</summary>
        internal static void ProjectDimension(ResourceKeyStoreData data, Coord current, DimensionAxis axis, bool enabled)
        {
            PanelDimensionMask old = data.DimensionMask;
            PanelDimensionMask next = new PanelDimensionMask { ByPlatform = old.ByPlatform, ByChannel = old.ByChannel, ByDevelopMode = old.ByDevelopMode };
            switch (axis)
            {
                case DimensionAxis.Platform: next.ByPlatform = enabled; break;
                case DimensionAxis.Channel: next.ByChannel = enabled; break;
                case DimensionAxis.DevelopMode: next.ByDevelopMode = enabled; break;
                default: throw new ArgumentOutOfRangeException(nameof(axis));
            }
            ValidateCoord(next, current);
            var plans = new List<List<ResourceActiveKeyOverride>>();
            foreach (ResourcePackageKeys package in data.Packages)
            {
                var entries = new List<ResourceActiveKeyOverride>();
                foreach (PlatformType platform in next.ByPlatform ? Enum.GetValues(typeof(PlatformType)).Cast<PlatformType>().Where(p => p != PlatformType.None) : new[] { PlatformType.None })
                foreach (ChannelType channel in next.ByChannel ? Enum.GetValues(typeof(ChannelType)).Cast<ChannelType>().Where(c => c != ChannelType.None) : new[] { ChannelType.None })
                foreach (DevelopMode mode in next.ByDevelopMode ? Enum.GetValues(typeof(DevelopMode)).Cast<DevelopMode>() : new[] { DevelopMode.Debug })
                {
                    // 旧轴已勾选且继续保留时逐组取值；新增或合并的轴以顶部当前值确定来源。
                    Coord source = new Coord(old.ByPlatform && next.ByPlatform ? platform : current.Platform,
                        old.ByChannel && next.ByChannel ? channel : current.Channel,
                        old.ByDevelopMode && next.ByDevelopMode ? mode : current.Mode);
                    entries.Add(new ResourceActiveKeyOverride { Platform = platform, Channel = channel, Mode = mode,
                        ActiveKeyId = ResolveActiveKeyId(data, package, source) });
                }
                plans.Add(entries);
            }
            for (int index = 0; index < data.Packages.Count; index++)
            {
                ResourcePackageKeys package = data.Packages[index];
                if (next.IsGlobal) { package.ActiveKeyId = plans[index][0].ActiveKeyId; package.ActiveKeyOverrides.Clear(); }
                else package.ActiveKeyOverrides = plans[index];
            }
            data.DimensionMask = next;
        }

        /// <summary>未勾选轴使用固定哨兵，保证相同逻辑组只有一条记录。</summary>
        private static Coord ClipCoord(PanelDimensionMask mask, Coord coord) => new Coord(
            mask.ByPlatform ? coord.Platform : PlatformType.None, mask.ByChannel ? coord.Channel : ChannelType.None,
            mask.ByDevelopMode ? coord.Mode : DevelopMode.Debug);

        /// <summary>拒绝已勾选轴的空坐标和未知枚举，避免误用其他环境的编号。</summary>
        private static void ValidateCoord(PanelDimensionMask mask, Coord coord)
        {
            if ((mask.ByPlatform && (coord.Platform == PlatformType.None || !Enum.IsDefined(typeof(PlatformType), coord.Platform))) ||
                (mask.ByChannel && (coord.Channel == ChannelType.None || !Enum.IsDefined(typeof(ChannelType), coord.Channel))) ||
                (mask.ByDevelopMode && !Enum.IsDefined(typeof(DevelopMode), coord.Mode)))
                throw new InvalidDataException("资源密钥维度坐标无效，请在 Config 顶部选择有效的平台、渠道和开发模式。");
        }

        /// <summary>检查包裹名或编号是否为可读、稳定的字符串；比较采用大小写敏感语义。</summary>
        public static bool IsValidIdentifier(string value)
        {
            return !string.IsNullOrWhiteSpace(value) && value.Length <= 64 && value == value.Trim() &&
                   !value.Any(char.IsControl);
        }
    }
}
