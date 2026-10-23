/***************************************************************
 * (c) copyright 2026 - 2030, Solotopia
 * All Rights Reserved.
 * -------------------------------------------------------------
 * filename:  MemoryProtectionLegacyConfigMigration.cs
 * author:    Codex
 * created:   2026/10/21
 * descrip:   将旧隐私内存防护字段安全迁移到独立包扩展配置
 ***************************************************************/

using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Text.RegularExpressions;
using Newtonsoft.Json.Linq;
using NovaFramework.Runtime;
using UnityEditor;
using UnityEngine;

namespace NovaFramework.Editor
{
    /// <summary>
    /// 只读检查旧 YAML 的位置、数量和坐标，再通过 SerializedObject 保存；不手写 YAML、不覆盖已有扩展键。
    /// </summary>
    [InitializeOnLoad]
    public static class MemoryProtectionLegacyConfigMigration
    {
        private const string DetectionField = "EnableMemoryTamperDetection";
        private const string RotationField = "EnableMemoryKeyRotation";
        private static bool s_Queued;

        private sealed class LegacyPrivacy
        {
            internal string PropertyPath;
            internal int Platform;
            internal int Channel;
            internal int Mode;
            internal bool HasCoordinates;
            internal bool Detection = true;
            internal bool Rotation = true;
            internal int FieldCount;
        }

        private sealed class PendingWrite
        {
            internal SerializedProperty Property;
            internal string Json;
        }

        /// <summary>首次加载独立包时排队迁移，等待 Editor 完成导入后再处理项目配置。</summary>
        static MemoryProtectionLegacyConfigMigration()
        {
            QueueMigration();
        }

        /// <summary>合并导入与脚本域重载通知，避免在资产导入回调中直接保存配置。</summary>
        internal static void QueueMigration()
        {
            if (s_Queued) return;
            s_Queued = true;
            EditorApplication.delayCall += RunQueuedMigration;
        }

        /// <summary>在 Editor 空闲时执行迁移；编译、导入与 Play 期间继续等待。</summary>
        private static void RunQueuedMigration()
        {
            s_Queued = false;
            if (EditorApplication.isCompiling || EditorApplication.isUpdating || EditorApplication.isPlayingOrWillChangePlaymode)
            {
                QueueMigration();
                return;
            }
            MigrateProjectAssets();
        }

        /// <summary>
        /// 扫描项目 Assets 内的 ConfigMasterSO 和 ConfigRuntimeSO；缺少旧字段时完全不写入。
        /// 脏资产、只读资产、非标准 YAML、数量或坐标不匹配时保守跳过并报告资产路径。
        /// </summary>
        /// <returns>本轮已写入新扩展配置的资产数量。</returns>
        public static int MigrateProjectAssets()
        {
            HashSet<string> paths = new(StringComparer.Ordinal);
            foreach (string filter in new[] { "t:ConfigMasterSO", "t:ConfigRuntimeSO" })
            {
                foreach (string guid in AssetDatabase.FindAssets(filter, new[] { "Assets" }))
                    paths.Add(AssetDatabase.GUIDToAssetPath(guid));
            }
            int migrated = 0;
            foreach (string path in paths)
            {
                try
                {
                    if (TryMigrateAsset(path)) migrated++;
                }
                catch (Exception exception)
                {
                    Debug.LogWarning($"[MemoryProtection] 旧隐私配置迁移已跳过：{path}。{exception.Message}");
                }
            }
            if (migrated > 0) Debug.Log($"[MemoryProtection] 已通过 Editor 保存链迁移 {migrated} 个配置资产的旧隐私开关。");
            return migrated;
        }

        /// <summary>先准备全部扩展写集，完成位置核对与 JSON 校验后只保存当前资产一次。</summary>
        /// <param name="path">项目 Assets 下的真实配置资产路径。</param>
        /// <returns>至少写入一份扩展配置并完成保存时返回 true。</returns>
        private static bool TryMigrateAsset(string path)
        {
            if (!path.StartsWith("Assets/", StringComparison.Ordinal) || !File.Exists(path)) return false;
            string yaml = File.ReadAllText(path);
            int totalLegacyFields = Regex.Matches(yaml,
                @"(?m)^\s*(?:EnableMemoryTamperDetection|EnableMemoryKeyRotation):").Count;
            if (totalLegacyFields == 0) return false;
            if (!yaml.StartsWith("%YAML", StringComparison.Ordinal) || Regex.Matches(yaml, @"(?m)^--- ").Count != 1)
                throw new InvalidOperationException("配置不是受支持的单对象 Unity 文本资产，未写入任何值。");

            UnityEngine.Object asset = AssetDatabase.LoadMainAssetAtPath(path);
            if (!(asset is ConfigMasterSO) && !(asset is ConfigRuntimeSO)) return false;
            if (EditorUtility.IsDirty(asset) || !AssetDatabase.IsOpenForEdit(path))
                throw new InvalidOperationException("资产存在未保存修改或不可写，请保存或恢复可写状态后重新导入。");

            List<LegacyPrivacy> records = ParseLegacyPrivacy(yaml, asset is ConfigMasterSO);
            int matchedFields = 0;
            foreach (LegacyPrivacy record in records) matchedFields += record.FieldCount;
            if (matchedFields != totalLegacyFields)
                throw new InvalidOperationException("旧开关字段未能全部匹配到隐私对象，未写入任何值。");

            using SerializedObject serialized = new(asset);
            List<string> actualPaths = GetPrivacyPropertyPaths(serialized, asset is ConfigMasterSO);
            if (records.Count != actualPaths.Count)
                throw new InvalidOperationException("YAML 与当前序列化对象的隐私配置数量不同，未写入任何值。");
            List<PendingWrite> writes = new();
            for (int index = 0; index < records.Count; index++)
            {
                LegacyPrivacy record = records[index];
                if (!string.Equals(record.PropertyPath, actualPaths[index], StringComparison.Ordinal) ||
                    !CoordinatesMatch(serialized, record))
                    throw new InvalidOperationException("隐私配置的顺序或坐标不同，未写入任何值。");
                SerializedProperty extensions = serialized.FindProperty(record.PropertyPath)?.FindPropertyRelative("ExtensionConfigsJson");
                if (extensions == null)
                    throw new InvalidOperationException("通用隐私扩展字段不存在，未写入任何值。");
                if (record.FieldCount == 0) continue;
                JObject root = string.IsNullOrWhiteSpace(extensions.stringValue)
                    ? new JObject()
                    : JObject.Parse(extensions.stringValue);
                // 只要新键已存在，就尊重已保存的新版本配置，即使其内容需要使用者另行修复。
                if (root.Property(MemoryProtectionConfigs.ExtensionKey, StringComparison.Ordinal) != null) continue;
                MemoryProtectionConfigs config = new()
                {
                    EnableMemoryTamperDetection = record.Detection,
                    EnableMemoryKeyRotation = record.Rotation,
                };
                writes.Add(new PendingWrite
                {
                    Property = extensions.Copy(),
                    Json = config.WriteToPrivacyJson(extensions.stringValue),
                });
            }
            if (writes.Count == 0) return false;
            foreach (PendingWrite write in writes) write.Property.stringValue = write.Json;
            serialized.ApplyModifiedPropertiesWithoutUndo();
            EditorUtility.SetDirty(asset);
            AssetDatabase.SaveAssetIfDirty(asset);
            return true;
        }

        /// <summary>按真实序列化顺序列出全部隐私对象，不调用会补建矩阵行或模式项的 getter。</summary>
        /// <param name="serialized">当前真实配置资产的序列化对象。</param>
        /// <param name="isMaster">是否为设计态矩阵配置。</param>
        /// <returns>当前资产全部隐私对象的稳定属性路径。</returns>
        private static List<string> GetPrivacyPropertyPaths(SerializedObject serialized, bool isMaster)
        {
            List<string> paths = new();
            if (!isMaster)
            {
                if (serialized.FindProperty("PrivacyConfigs") != null) paths.Add("PrivacyConfigs");
                return paths;
            }
            SerializedProperty entries = serialized.FindProperty("m_Entries");
            if (entries == null) return paths;
            for (int row = 0; row < entries.arraySize; row++)
            {
                SerializedProperty modes = entries.GetArrayElementAtIndex(row).FindPropertyRelative("PrivacyConfigsByMode");
                if (modes == null) continue;
                for (int mode = 0; mode < modes.arraySize; mode++)
                    paths.Add(modes.GetArrayElementAtIndex(mode).FindPropertyRelative("Config").propertyPath);
            }
            return paths;
        }

        /// <summary>校验 YAML 中记录的平台、渠道和模式与同一物理位置的序列化对象完全一致。</summary>
        /// <param name="serialized">当前真实资产的序列化对象。</param>
        /// <param name="record">旧 YAML 对应位置和坐标。</param>
        /// <returns>运行时单快照或矩阵三维坐标完全匹配时返回 true。</returns>
        private static bool CoordinatesMatch(SerializedObject serialized, LegacyPrivacy record)
        {
            if (!record.HasCoordinates) return true;
            string rowPath = record.PropertyPath.Substring(0, record.PropertyPath.IndexOf(".PrivacyConfigsByMode", StringComparison.Ordinal));
            string modePath = record.PropertyPath.Substring(0, record.PropertyPath.Length - ".Config".Length);
            return serialized.FindProperty(rowPath + ".Platform")?.intValue == record.Platform &&
                   serialized.FindProperty(rowPath + ".Channel")?.intValue == record.Channel &&
                   serialized.FindProperty(modePath + ".Mode")?.intValue == record.Mode;
        }

        /// <summary>只解析当前已知的 Unity 隐私配置布局，并为每个对象保留物理顺序及坐标。</summary>
        /// <param name="yaml">只读取得的原始 Unity YAML，不做任何文本替换或写回。</param>
        /// <param name="isMaster">是否解析设计态三维矩阵。</param>
        /// <returns>与 SerializedObject 一一核对的全部隐私记录。</returns>
        private static List<LegacyPrivacy> ParseLegacyPrivacy(string yaml, bool isMaster)
        {
            string[] lines = yaml.Replace("\r\n", "\n").Split('\n');
            List<LegacyPrivacy> records = new();
            if (!isMaster)
            {
                int privacy = FindLine(lines, 0, lines.Length, @"^  PrivacyConfigs:\s*$");
                if (privacy >= 0) records.Add(ReadPrivacy(lines, privacy + 1, FindBlockEnd(lines, privacy + 1, 2),
                    new LegacyPrivacy { PropertyPath = "PrivacyConfigs" }, 4));
                return records;
            }
            int entries = FindLine(lines, 0, lines.Length, @"^  m_Entries:\s*$");
            if (entries < 0) throw new InvalidOperationException("未找到标准设计态矩阵布局。");
            int entriesEnd = FindBlockEnd(lines, entries + 1, 2);
            int rowIndex = 0;
            for (int row = entries + 1; row < entriesEnd; row++)
            {
                Match platform = Regex.Match(lines[row], @"^  - Platform:\s*(-?\d+)\s*$");
                if (!platform.Success) continue;
                int nextRow = FindLine(lines, row + 1, entriesEnd, @"^  - Platform:");
                int rowEnd = nextRow >= 0 ? nextRow : entriesEnd;
                int channel = ReadInteger(lines, row + 1, rowEnd, @"^    Channel:\s*(-?\d+)\s*$");
                int privacy = FindLine(lines, row + 1, rowEnd, @"^    PrivacyConfigsByMode:\s*$");
                int modeIndex = 0;
                if (privacy >= 0)
                {
                    int privacyEnd = Math.Min(rowEnd, FindBlockEnd(lines, privacy + 1, 4));
                    for (int mode = privacy + 1; mode < privacyEnd; mode++)
                    {
                        Match modeValue = Regex.Match(lines[mode], @"^    - Mode:\s*(-?\d+)\s*$");
                        if (!modeValue.Success) continue;
                        int nextMode = FindLine(lines, mode + 1, privacyEnd, @"^    - Mode:");
                        int modeEnd = nextMode >= 0 ? nextMode : privacyEnd;
                        int config = FindLine(lines, mode + 1, modeEnd, @"^      Config:\s*$");
                        if (config < 0) throw new InvalidOperationException("隐私模式项缺少标准 Config 对象。");
                        records.Add(ReadPrivacy(lines, config + 1, modeEnd, new LegacyPrivacy
                        {
                            PropertyPath = $"m_Entries.Array.data[{rowIndex}].PrivacyConfigsByMode.Array.data[{modeIndex}].Config",
                            Platform = ParseInteger(platform.Groups[1].Value),
                            Channel = channel,
                            Mode = ParseInteger(modeValue.Groups[1].Value),
                            HasCoordinates = true,
                        }, 8));
                        modeIndex++;
                    }
                }
                rowIndex++;
                row = rowEnd - 1;
            }
            return records;
        }

        /// <summary>读取一个隐私对象的旧开关，仅接受明确布尔值；缺失的旧字段沿用原默认 true。</summary>
        /// <param name="lines">原始 YAML 行。</param>
        /// <param name="start">对象内容起始位置。</param>
        /// <param name="end">对象内容结束位置，不含此行。</param>
        /// <param name="record">记录的位置和坐标。</param>
        /// <param name="indent">当前对象直接子字段的缩进。</param>
        /// <returns>填充了旧字段与数量的记录。</returns>
        private static LegacyPrivacy ReadPrivacy(string[] lines, int start, int end, LegacyPrivacy record, int indent)
        {
            HashSet<string> seen = new(StringComparer.Ordinal);
            string prefix = new(' ', indent);
            for (int index = start; index < end; index++)
            {
                Match match = Regex.Match(lines[index], "^" + prefix + "(" + DetectionField + "|" + RotationField + @"):\s*(.*?)\s*$");
                if (!match.Success) continue;
                string field = match.Groups[1].Value;
                string value = match.Groups[2].Value;
                if (!seen.Add(field) || (value != "0" && value != "1" && value != "false" && value != "true"))
                    throw new InvalidOperationException("旧开关存在重复字段或无法识别的值，未写入任何值。");
                bool enabled = value == "1" || value == "true";
                if (field == DetectionField) record.Detection = enabled;
                else record.Rotation = enabled;
                record.FieldCount++;
            }
            return record;
        }

        /// <summary>定位指定缩进映射的末尾，保留 Unity 在相同缩进处书写的列表项。</summary>
        /// <param name="lines">原始 YAML 行。</param>
        /// <param name="start">映射内容起始行。</param>
        /// <param name="indent">映射键的缩进数量。</param>
        /// <returns>第一个不属于该映射的行索引。</returns>
        private static int FindBlockEnd(string[] lines, int start, int indent)
        {
            for (int index = start; index < lines.Length; index++)
            {
                string text = lines[index];
                if (string.IsNullOrWhiteSpace(text)) continue;
                string content = text.TrimStart(' ');
                int actualIndent = text.Length - content.Length;
                if (actualIndent < indent || (actualIndent == indent && !content.StartsWith("- ", StringComparison.Ordinal))) return index;
            }
            return lines.Length;
        }

        /// <summary>在受限块内寻找单行布局，不跨越已经确定的父对象边界。</summary>
        /// <param name="lines">原始 YAML 行。</param>
        /// <param name="start">起始行。</param>
        /// <param name="end">结束行，不含此行。</param>
        /// <param name="pattern">精确限定缩进的正则表达式。</param>
        /// <returns>匹配的行索引，未命中返回 -1。</returns>
        private static int FindLine(string[] lines, int start, int end, string pattern)
        {
            for (int index = start; index < end; index++) if (Regex.IsMatch(lines[index], pattern)) return index;
            return -1;
        }

        /// <summary>读取必须存在的单个坐标整数，无法辨认时中止该资产迁移。</summary>
        /// <param name="lines">原始 YAML 行。</param>
        /// <param name="start">块起始行。</param>
        /// <param name="end">块结束行。</param>
        /// <param name="pattern">第一个捕获组为坐标整数的正则表达式。</param>
        /// <returns>已核实的坐标值。</returns>
        private static int ReadInteger(string[] lines, int start, int end, string pattern)
        {
            int index = FindLine(lines, start, end, pattern);
            if (index < 0) throw new InvalidOperationException("旧隐私配置坐标缺失，未写入任何值。");
            return ParseInteger(Regex.Match(lines[index], pattern).Groups[1].Value);
        }

        /// <summary>使用固定文化解析 Unity 序列化的枚举整数，拒绝超出范围的文本。</summary>
        /// <param name="value">十进制坐标文本。</param>
        /// <returns>对应的整数值。</returns>
        private static int ParseInteger(string value)
        {
            return int.Parse(value, NumberStyles.Integer, CultureInfo.InvariantCulture);
        }
    }

    /// <summary>导入或移动项目配置后排队检查旧格式，不在导入回调内直接修改资产。</summary>
    internal sealed class MemoryProtectionLegacyConfigPostprocessor : AssetPostprocessor
    {
        /// <summary>仅项目 Assets 内的配置类资产变化可能触发检查，包内原始文件保持只读。</summary>
        /// <param name="importedAssets">本轮导入路径。</param>
        /// <param name="deletedAssets">本轮删除路径，不参与迁移。</param>
        /// <param name="movedAssets">本轮移动后的路径。</param>
        /// <param name="movedFromAssetPaths">本轮移动前的路径，不参与迁移。</param>
        private static void OnPostprocessAllAssets(string[] importedAssets, string[] deletedAssets, string[] movedAssets, string[] movedFromAssetPaths)
        {
            foreach (string[] paths in new[] { importedAssets, movedAssets })
            {
                foreach (string path in paths)
                {
                    if (path.StartsWith("Assets/", StringComparison.Ordinal) && path.EndsWith(".asset", StringComparison.OrdinalIgnoreCase))
                    {
                        Type type = AssetDatabase.GetMainAssetTypeAtPath(path);
                        if (type != typeof(ConfigMasterSO) && type != typeof(ConfigRuntimeSO)) continue;
                        MemoryProtectionLegacyConfigMigration.QueueMigration();
                        return;
                    }
                }
            }
        }
    }
}
