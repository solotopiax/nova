/***************************************************************
 * (c) copyright 2026 - 2030, Solotopia
 * All Rights Reserved.
 * -------------------------------------------------------------
 * filename:  EditorUtil.Config.PanelEditing.cs
 * author:    taoye
 * created:   2026/10/13
 * descrip:   Config 各面板共用字段查阅、维度调整及批量编辑预演
 ***************************************************************/

using System;
using System.Collections.Generic;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using NovaFramework.Runtime;
using UnityEditor;
using UnityEngine;
using Coord = NovaFramework.Editor.EditorUtil.Config.DimensionProjector.Coord;
using Panel = NovaFramework.Editor.EditorUtil.Config.PanelEditing.EditablePanel;
using Axis = NovaFramework.Editor.EditorUtil.Config.DimensionProjector.DimensionAxis;

namespace NovaFramework.Editor
{
    public static partial class EditorUtil
    {
        public static partial class Config
        {
            /// <summary>领域边界由固定面板及已存配置类型限定；值转换复用 Unity 实际字段，不接受方法调用。</summary>
            internal static partial class PanelEditing
            {
                /// <summary>编辑域沿用既有维度面板名称；Custom 是全局域，资源密钥用独立掩码，不扩展公共投影枚举。</summary>
                internal enum EditablePanel
                {
                    AppConfigs, PrivacyConfigs, SDK, Kit, Namespace, HybridEditorConfigs,
                    YooAssetEditorConfigs, CDNEditorConfigs, Custom, ResourceEncryption,
                }

                /// <summary>已确认坐标与面板；SDK/Kit 使用配置实例的精确 FullName，其它面板禁止类型参数。</summary>
                [Serializable]
                internal sealed class Target
                {
                    [AgentActionRequired] public string platform;
                    [AgentActionRequired] public string channel;
                    [AgentActionRequired] public string developMode;
                    [AgentActionRequired] public string panel;
                    public string configType;
                }

                /// <summary>路径来自 Inspect 的相对字段路径；valueJson 是严格 JSON 值，不是代码。</summary>
                [Serializable]
                internal sealed class FieldEdit
                {
                    [AgentActionRequired] public string path;
                    [AgentActionRequired] public string valueJson;
                }

                /// <summary>维度开关先按既有全矩阵分裂/合并执行；随后字段编辑按最终掩码展开。</summary>
                [Serializable]
                internal sealed class Change
                {
                    [AgentActionRequired] public Target target;
                    public FieldEdit[] fields;
                    public DimensionEdit[] dimensions;
                }

                /// <summary>一个显式维度轴及目标状态；减维时来源取 target 的该轴值。</summary>
                [Serializable]
                internal sealed class DimensionEdit
                {
                    [AgentActionRequired] public string axis;
                    public bool enabled;
                }

                /// <summary>只输出序列化地址和变化性质，禁止把字段值或单密钥哈希写入公开计划。</summary>
                [Serializable]
                internal sealed class Difference
                {
                    public string path;
                    public string kind;
                }

                /// <summary>预演只拥有独立 ConfigMaster 工作副本，销毁后不改变正式资产。</summary>
                internal sealed class Preview : IDisposable
                {
                    internal ConfigMasterSO WorkingCopy;
                    internal string BeforeHash;
                    internal string ExpectedHash;
                    internal Difference[] Differences;
                    internal Change[] Changes;
                    internal Coord SaveCoordinate;

                    /// <summary>释放独立工作副本；可以重复调用，不销毁真实资产。</summary>
                    public void Dispose()
                    {
                        if (WorkingCopy != null) UnityEngine.Object.DestroyImmediate(WorkingCopy);
                        WorkingCopy = null;
                    }
                }

                /// <summary>验证请求固定边界，尚未读取资产时只检查格式和严格值 token。</summary>
                internal static void Validate(Change[] changes)
                {
                    if (changes == null || changes.Length == 0 || changes.Length > 64)
                        throw new InvalidOperationException("需要 1 至 64 个面板编辑项。");
                    foreach (Change change in changes)
                    {
                        ValidateTarget(change?.target);
                        if ((change.fields == null || change.fields.Length == 0) &&
                            (change.dimensions == null || change.dimensions.Length == 0))
                            throw new InvalidOperationException("每个面板项至少需要字段或维度修改。");
                        if ((change.fields?.Length ?? 0) > 128 || (change.dimensions?.Length ?? 0) > 3)
                            throw new InvalidOperationException("面板编辑字段或维度数量超出范围。");
                        if (ParsePanel(change.target.panel) == Panel.Custom && (change.dimensions?.Length ?? 0) > 0)
                            throw new InvalidOperationException("Custom 是全局面板，不能修改维度。");
                        var paths = new HashSet<string>(StringComparer.Ordinal);
                        foreach (FieldEdit edit in change.fields ?? Array.Empty<FieldEdit>())
                        {
                            if (edit == null || string.IsNullOrWhiteSpace(edit.path) || edit.path.Length > 512 ||
                                edit.path.Any(char.IsControl) || !paths.Add(edit.path))
                                throw new InvalidOperationException("字段路径缺失、重复或无效。");
                            SerializedPropertyValues.Parse(edit.valueJson);
                        }
                        if (paths.Any(path => paths.Any(other => path != other && (path.StartsWith(other + ".", StringComparison.Ordinal) ||
                            path.StartsWith(other + "[", StringComparison.Ordinal)))))
                            throw new InvalidOperationException("字段父子路径不能同时修改。");
                        var axes = new HashSet<string>(StringComparer.Ordinal);
                        foreach (DimensionEdit edit in change.dimensions ?? Array.Empty<DimensionEdit>())
                            if (edit == null || !Exact(edit.axis, out Axis _) || !axes.Add(edit.axis))
                                throw new InvalidOperationException("维度轴缺失、重复或无效。");
                    }
                }

                /// <summary>只读描述真实面板字段、掩码与全部同组物理坐标；实际字段值按敏感规则脱敏。</summary>
                internal static object Inspect(ConfigMasterSO master, Target target)
                {
                    ValidateTarget(target);
                    using (var so = new SerializedObject(master))
                    {
                        so.Update();
                        Coord coordinate = Coordinate(target);
                        Panel panel = ParsePanel(target.panel);
                        PanelDimensionMask mask = Mask(master, panel, target.configType);
                        SerializedProperty root = Locate(master, so, target);
                        return new
                        {
                            target, stateSha256 = StateHash(master),
                            dimensionMask = new { mask.ByPlatform, mask.ByChannel, mask.ByDevelopMode },
                            physicalCoordinates = Coordinates(master).Where(c => SameGroup(mask, coordinate, c))
                                .Select(c => new { platform = c.Platform.ToString(), channel = c.Channel.ToString(), developMode = c.Mode.ToString() }).ToArray(),
                            fields = DescribeFields(root, panel),
                            saveBoundary = "ConfigMaster only; Runtime/export/build/external remain separate operations",
                        };
                    }
                }

                /// <summary>完整副本预演：沿用维度算法与统一归一，精确检查仅请求字段或面板维度发生变化。</summary>
                internal static Preview BuildPreview(ConfigMasterSO master, Change[] changes)
                {
                    Validate(changes);
                    var preview = new Preview { Changes = changes, SaveCoordinate = Coordinate(changes[0].target) };
                    string stage = "clone";
                    try
                    {
                        preview.WorkingCopy = UnityEngine.Object.Instantiate(master);
                        preview.WorkingCopy.hideFlags = HideFlags.DontSave;
                        ConfigMasterSO clone = preview.WorkingCopy;
                        if (!SchemaGuard.Validate(clone, out _)) throw new InvalidOperationException("ConfigMaster 结构版本不受支持。");
                        string before = Snapshot(clone);
                        using (var so = new SerializedObject(clone))
                        {
                            stage = "normalize-before";
                            DesignTimeEditing.NormalizeWorkingCopy(clone, so, preview.SaveCoordinate);
                            if (Snapshot(clone) != before)
                                throw new InvalidOperationException("配置需要先显式整理保存；面板编辑不能修复请求外结构。");
                            preview.BeforeHash = StateHash(clone);
                            stage = "dimensions";
                            var dimensionPanels = new HashSet<string>();
                            foreach (Change change in changes)
                            {
                                Panel panel = ParsePanel(change.target.panel);
                                string identity = panel + "|" + change.target.configType;
                                if ((change.dimensions?.Length ?? 0) > 0 && !dimensionPanels.Add(identity))
                                    throw new InvalidOperationException("同一面板的维度修改应合并为一个明确来源坐标。");
                                foreach (DimensionEdit dimension in change.dimensions ?? Array.Empty<DimensionEdit>())
                                {
                                    Exact(dimension.axis, out Axis axis);
                                    PanelDimensionMask mask = Mask(clone, panel, change.target.configType);
                                    bool current = axis == Axis.Platform ? mask.ByPlatform : axis == Axis.Channel ? mask.ByChannel : mask.ByDevelopMode;
                                    if (current == dimension.enabled) continue;
                                    if (panel == Panel.ResourceEncryption)
                                    {
                                        if (dimension.enabled) DimensionProjector.OnDimensionEnabled(clone.ResourceEncryption, Coordinate(change.target), axis);
                                        else DimensionProjector.OnDimensionDisabled(clone.ResourceEncryption, Coordinate(change.target), axis);
                                        so.Update();
                                    }
                                    else if (dimension.enabled) DimensionProjector.OnDimensionEnabled(clone, so, ProjectionPanel(panel), change.target.configType, Coordinate(change.target), axis);
                                    else DimensionProjector.OnDimensionDisabled(clone, so, ProjectionPanel(panel), change.target.configType, Coordinate(change.target), axis);
                                }
                            }
                            string afterDimensions = Snapshot(clone);
                            stage = "dimension-scope";
                            AssertDimensionScope(JObject.Parse(before), JObject.Parse(afterDimensions), changes);
                            var allowed = new HashSet<string>(StringComparer.Ordinal);
                            var desired = new Dictionary<string, string>(StringComparer.Ordinal);
                            foreach (Change change in changes)
                            {
                                stage = "locate-fields";
                                Target target = change.target;
                                Panel panel = ParsePanel(target.panel);
                                Coord coordinate = Coordinate(target);
                                PanelDimensionMask mask = Mask(clone, panel, target.configType);
                                SerializedProperty root = Locate(clone, so, target);
                                var exposed = new HashSet<string>(DescribeFields(root, panel).Where(f => f.editable).Select(f => f.path));
                                foreach (FieldEdit edit in change.fields ?? Array.Empty<FieldEdit>())
                                {
                                    if (!exposed.Contains(edit.path)) throw new InvalidOperationException("请求字段未在当前面板 Inspect 中暴露。");
                                    JToken value = SerializedPropertyValues.Parse(edit.valueJson);
                                    string key = panel + "|" + target.configType + "|" + GroupKey(mask, coordinate) + "|" + edit.path;
                                    string normalized = value.ToString(Formatting.None);
                                    if (desired.Keys.Any(oldKey => oldKey != key && (key.StartsWith(oldKey + ".", StringComparison.Ordinal) ||
                                        key.StartsWith(oldKey + "[", StringComparison.Ordinal) || oldKey.StartsWith(key + ".", StringComparison.Ordinal) ||
                                        oldKey.StartsWith(key + "[", StringComparison.Ordinal))))
                                        throw new InvalidOperationException("同一逻辑组包含重叠父子字段请求。");
                                    if (desired.TryGetValue(key, out string old) && old != normalized)
                                        throw new InvalidOperationException("同一逻辑组的同一字段出现冲突值。");
                                    desired[key] = normalized;
                                    SerializedProperty property = Field(root, panel, edit.path);
                                    if (property == null) throw new InvalidOperationException("实际序列化字段不存在。");
                                    // 物理写集来自当前 Unity 路径；每个同组格只放行请求的字段子树。
                                    foreach (Coord member in IsGlobal(panel) ? new[] { coordinate } : Coordinates(clone).Where(c => SameGroup(mask, coordinate, c)))
                                    {
                                        Target physical = CopyTarget(target, member);
                                        AddRequestedPaths(Field(Locate(clone, so, physical), panel, edit.path), value, allowed);
                                    }
                                    SerializedPropertyValues.Set(property, value);
                                }
                                stage = "apply-and-broadcast";
                                so.ApplyModifiedPropertiesWithoutUndo();
                                if (!IsGlobal(panel)) DimensionProjector.BroadcastWithinGroup(clone, so, ProjectionPanel(panel), target.configType, coordinate);
                                so.Update();
                            }
                            string permitted = Snapshot(clone);
                            stage = "field-scope";
                            AssertFieldScope(JObject.Parse(afterDimensions), JObject.Parse(permitted), allowed);
                            DesignTimeEditing.NormalizeWorkingCopy(clone, so, preview.SaveCoordinate);
                            stage = "normalize-after";
                            if (Snapshot(clone) != permitted)
                                throw new InvalidOperationException("保存归一引入了请求外变化，已拒绝。");
                            AssertRetainedResourceKeys(master.ResourceEncryption, clone.ResourceEncryption);
                            stage = "privacy-key-rotation";
                            AssertRetainedPrivacyCredentials(master, clone);
                        }
                        preview.ExpectedHash = StateHash(clone);
                        preview.Differences = Differences(JObject.Parse(before), JObject.Parse(Snapshot(clone))).ToArray();
                        return preview;
                    }
                    catch (Exception exception)
                    {
                        preview.Dispose();
                        if (stage == "privacy-key-rotation")
                            throw new InvalidOperationException("panel-stage:privacy-key-rotation:已有 Privacy AES Key/IV 不允许更换或清空；需要显式存档迁移契约。", exception);
                        throw new InvalidOperationException("panel-stage:" + stage + ":" + exception.GetType().Name, exception);
                    }
                }

                /// <summary>显式整理整份设计态配置；来源坐标决定合并保留分支，完整差异由独立 Action 展示确认。</summary>
                internal static Preview BuildNormalizationPreview(ConfigMasterSO master, Coord coordinate)
                {
                    if (master == null) throw new ArgumentNullException(nameof(master));
                    ValidateTarget(new Target { platform = coordinate.Platform.ToString(), channel = coordinate.Channel.ToString(),
                        developMode = coordinate.Mode.ToString(), panel = "AppConfigs" });
                    var preview = new Preview { SaveCoordinate = coordinate };
                    try
                    {
                        string before = Snapshot(master);
                        preview.BeforeHash = StateHash(master);
                        preview.WorkingCopy = UnityEngine.Object.Instantiate(master);
                        preview.WorkingCopy.hideFlags = HideFlags.DontSave;
                        if (!SchemaGuard.Validate(preview.WorkingCopy, out _)) throw new InvalidOperationException("配置版本不受支持。");
                        using (var so = new SerializedObject(preview.WorkingCopy))
                        {
                            DesignTimeEditing.NormalizeWorkingCopy(preview.WorkingCopy, so, coordinate);
                            string after = Snapshot(preview.WorkingCopy);
                            DesignTimeEditing.NormalizeWorkingCopy(preview.WorkingCopy, so, coordinate);
                            if (Snapshot(preview.WorkingCopy) != after) throw new InvalidOperationException("配置整理不能形成稳定状态。");
                            preview.Differences = Differences(JObject.Parse(before), JObject.Parse(after)).ToArray();
                        }
                        preview.ExpectedHash = StateHash(preview.WorkingCopy);
                        return preview;
                    }
                    catch { preview.Dispose(); throw; }
                }

                /// <summary>完整状态展开 Unity 管理引用为真实字段树；RID 是内存身份，不作为业务变化或公开证据。</summary>
                internal static string Snapshot(ConfigMasterSO master)
                {
                    JObject json = JObject.Parse(CoordinatePatchService.Snapshot(master));
                    var references = new Dictionary<long, JObject>();
                    if (json["references"]?["RefIds"] is JArray items)
                        foreach (JObject item in items)
                            if (item["rid"]?.Type == JTokenType.Integer) references[item["rid"].Value<long>()] = item;
                    json.Remove("references");
                    return ExpandReferences(json, references, new HashSet<long>()).ToString(Formatting.None);
                }

                /// <summary>计算完整语义字段树的单一 SHA-256，不能用于逐密钥猜测或回显。</summary>
                internal static string StateHash(ConfigMasterSO master)
                {
                    using (SHA256 sha = SHA256.Create())
                        return string.Concat(sha.ComputeHash(Encoding.UTF8.GetBytes(Snapshot(master))).Select(b => b.ToString("x2")));
                }

                /// <summary>递归展开真实管理引用数据；环、丢失引用或类型变化不可悄悄吞掉。</summary>
                private static JToken ExpandReferences(JToken value, Dictionary<long, JObject> references, HashSet<long> stack)
                {
                    if (value is JObject obj)
                    {
                        if (obj.Count == 1 && obj["rid"]?.Type == JTokenType.Integer)
                        {
                            long rid = obj["rid"].Value<long>();
                            if (rid == -2) return JValue.CreateNull();
                            if (!references.TryGetValue(rid, out JObject reference) || !stack.Add(rid))
                                throw new InvalidOperationException("配置管理引用缺失或含循环引用，无法建立受控写集。");
                            var result = (JObject)ExpandReferences(reference["data"] ?? new JObject(), references, stack);
                            result["_managedType"] = reference["type"]?.DeepClone();
                            stack.Remove(rid);
                            return result;
                        }
                        var expanded = new JObject();
                        foreach (JProperty property in obj.Properties()) expanded[property.Name] = ExpandReferences(property.Value, references, stack);
                        return expanded;
                    }
                    if (value is JArray array) return new JArray(array.Select(item => ExpandReferences(item, references, stack)));
                    return value.DeepClone();
                }

                /// <summary>Namespace 以 Value 暴露；Override 坐标不是面板业务字段，不能借字段编辑改坐标。</summary>
                private static SerializedPropertyValues.FieldView[] DescribeFields(SerializedProperty root, Panel panel)
                {
                    SerializedPropertyValues.FieldView[] fields = SerializedPropertyValues.Describe(root);
                    if (panel == Panel.Namespace) { fields[0].path = "Value"; return new[] { fields[0] }; }
                    return fields.Where(field => !string.IsNullOrEmpty(field.path) &&
                        field.path != "Platform" && field.path != "Channel" && field.path != "DevelopMode" &&
                        !(panel == Panel.ResourceEncryption && (field.path == "DimensionMask" || field.path.StartsWith("DimensionMask.", StringComparison.Ordinal)))).ToArray();
                }

                /// <summary>相对字段由 Inspect 唯一确定，Namespace 使用真实字符串根而不是伪造子字段。</summary>
                private static SerializedProperty Field(SerializedProperty root, Panel panel, string path) =>
                    panel == Panel.Namespace && path == "Value" ? root : root.FindPropertyRelative(path);

                /// <summary>定位已有面板根；SDK/Kit 不补实例，顶层按掩码只选已有唯一 Override。</summary>
                private static SerializedProperty Locate(ConfigMasterSO master, SerializedObject so, Target target)
                {
                    Panel panel = ParsePanel(target.panel);
                    Coord coordinate = Coordinate(target);
                    if (IsGlobal(panel)) return so.FindProperty(panel.ToString());
                    if (panel == Panel.Namespace) return so.FindProperty(Mask(master, panel, null).IsGlobal ? "Namespace" :
                        OverridePath(master, panel, coordinate) + ".Value");
                    if (panel == Panel.HybridEditorConfigs || panel == Panel.YooAssetEditorConfigs || panel == Panel.CDNEditorConfigs)
                    {
                        if (Mask(master, panel, null).IsGlobal) return so.FindProperty(panel.ToString());
                        string path = OverridePath(master, panel, coordinate);
                        return so.FindProperty(path + (panel == Panel.CDNEditorConfigs ? ".Config" : string.Empty));
                    }
                    if (!master.TryGetEntry(coordinate.Platform, coordinate.Channel, out PlatformChannelEntry row))
                        throw new InvalidOperationException("目标配置矩阵行不存在。");
                    int rowIndex = master.EditorEntries.IndexOf(row);
                    string modes = panel == Panel.AppConfigs ? "AppConfigsByMode" : panel == Panel.PrivacyConfigs ? "PrivacyConfigsByMode" : panel == Panel.SDK ? "SDKConfigsByMode" : "KitConfigsByMode";
                    SerializedProperty list = so.FindProperty($"m_Entries.Array.data[{rowIndex}].{modes}");
                    SerializedProperty slot = null;
                    for (int i = 0; list != null && i < list.arraySize; i++)
                    {
                        SerializedProperty item = list.GetArrayElementAtIndex(i);
                        if (item.FindPropertyRelative("Mode").intValue != (int)coordinate.Mode) continue;
                        if (slot != null) throw new InvalidOperationException("目标 mode 存在重复槽。");
                        slot = item;
                    }
                    if (slot == null) throw new InvalidOperationException("目标 mode 配置槽不存在。");
                    if (panel == Panel.AppConfigs || panel == Panel.PrivacyConfigs) return slot.FindPropertyRelative("Config");
                    SerializedProperty configs = slot.FindPropertyRelative(panel == Panel.SDK ? "SDKConfigs" : "KitConfigs");
                    SerializedProperty found = null;
                    for (int i = 0; configs != null && i < configs.arraySize; i++)
                    {
                        SerializedProperty item = configs.GetArrayElementAtIndex(i);
                        if (item.managedReferenceValue?.GetType().FullName != target.configType) continue;
                        if (found != null) throw new InvalidOperationException("目标配置类型存在重复实例。");
                        found = item;
                    }
                    return found ?? throw new InvalidOperationException("目标 SDK/Kit 实例不存在，请先使用 ensure-plugin-instances。");
                }

                /// <summary>顶层 Override 地址由既有掩码和真实列表产生；无命中拒绝，不能默默写全局回落。</summary>
                private static string OverridePath(ConfigMasterSO master, Panel panel, Coord coordinate)
                {
                    PanelDimensionMask mask = Mask(master, panel, null);
                    string list = panel + "Overrides";
                    using (var so = new SerializedObject(master))
                    {
                        SerializedProperty entries = so.FindProperty(list);
                        int found = -1;
                        for (int i = 0; entries != null && i < entries.arraySize; i++)
                        {
                            SerializedProperty entry = entries.GetArrayElementAtIndex(i);
                            if ((mask.ByPlatform && entry.FindPropertyRelative("Platform").intValue != (int)coordinate.Platform) ||
                                (mask.ByChannel && entry.FindPropertyRelative("Channel").intValue != (int)coordinate.Channel) ||
                                (mask.ByDevelopMode && entry.FindPropertyRelative("DevelopMode").intValue != (int)coordinate.Mode)) continue;
                            if (found >= 0) throw new InvalidOperationException("面板 Override 逻辑组存在重复条目。");
                            found = i;
                        }
                        if (found < 0) throw new InvalidOperationException("面板 Override 缺失，请先明确整理或切换维度建立规范结构。");
                        return $"{list}.Array.data[{found}]";
                    }
                }

                /// <summary>只读取得真实掩码，SDK/Kit 不调用会新增默认掩码的 Getter。</summary>
                private static PanelDimensionMask Mask(ConfigMasterSO master, Panel panel, string type)
                {
                    PanelDimensionMask result;
                    switch (panel)
                    {
                        case Panel.AppConfigs: result = master.AppConfigsMask; break;
                        case Panel.PrivacyConfigs: result = master.PrivacyConfigsMask; break;
                        case Panel.Namespace: result = master.NamespaceMask; break;
                        case Panel.HybridEditorConfigs: result = master.HybridEditorConfigsMask; break;
                        case Panel.YooAssetEditorConfigs: result = master.YooAssetEditorConfigsMask; break;
                        case Panel.CDNEditorConfigs: result = master.CDNEditorConfigsMask; break;
                        case Panel.Custom: result = new PanelDimensionMask(); break;
                        case Panel.ResourceEncryption: result = master.ResourceEncryption?.DimensionMask; break;
                        default:
                            List<TypedDimensionMask> entries = panel == Panel.SDK ? master.SDKMasks : master.KitMasks;
                            result = entries?.SingleOrDefault(item => item != null && item.TypeName == type)?.Mask; break;
                    }
                    return result ?? throw new InvalidOperationException("面板掩码不存在或配置类型不唯一。");
                }

                /// <summary>真实全矩阵坐标，不推断或补齐缺失物理行。</summary>
                private static IEnumerable<Coord> Coordinates(ConfigMasterSO master)
                {
                    foreach (PlatformChannelEntry row in master.GetAllEntries())
                        foreach (DevelopMode mode in Enum.GetValues(typeof(DevelopMode)))
                            yield return new Coord(row.Platform, row.Channel, mode);
                }

                /// <summary>判断未勾选轴共享的同一逻辑组。</summary>
                private static bool SameGroup(PanelDimensionMask mask, Coord a, Coord b) =>
                    (!mask.ByPlatform || a.Platform == b.Platform) && (!mask.ByChannel || a.Channel == b.Channel) && (!mask.ByDevelopMode || a.Mode == b.Mode);

                /// <summary>逻辑组键只包含已勾选轴，避免同组相互覆盖而漏检冲突。</summary>
                private static string GroupKey(PanelDimensionMask mask, Coord c) =>
                    (mask.ByPlatform ? c.Platform.ToString() : "*") + "/" + (mask.ByChannel ? c.Channel.ToString() : "*") + "/" + (mask.ByDevelopMode ? c.Mode.ToString() : "*");

                /// <summary>复制同一面板身份至明确物理坐标，只用于展开已经请求的逻辑写集。</summary>
                private static Target CopyTarget(Target target, Coord c) => new Target
                {
                    platform = c.Platform.ToString(), channel = c.Channel.ToString(), developMode = c.Mode.ToString(), panel = target.panel, configType = target.configType,
                };

                /// <summary>验证面板、实际枚举及 SDK/Kit 配置身份，不根据用户名称任意反射创建类型。</summary>
                private static void ValidateTarget(Target target)
                {
                    if (target == null || !Exact(target.platform, out PlatformType p) || p == PlatformType.None ||
                        !Exact(target.channel, out ChannelType c) || c == ChannelType.None || !Exact(target.developMode, out DevelopMode _))
                        throw new InvalidOperationException("面板需要有效且精确的三维坐标。");
                    Panel panel = ParsePanel(target.panel);
                    if (panel == Panel.SDK || panel == Panel.Kit)
                    {
                        if (string.IsNullOrWhiteSpace(target.configType) || target.configType.Length > 512)
                            throw new InvalidOperationException("SDK/Kit 必须提供当前实例的精确配置类型全名。");
                    }
                    else if (!string.IsNullOrEmpty(target.configType)) throw new InvalidOperationException("普通面板禁止配置类型参数。");
                }

                /// <summary>只接受固定面板枚举精确名称。</summary>
                private static Panel ParsePanel(string value)
                {
                    if (!Exact(value, out Panel result)) throw new InvalidOperationException("未知 Config 面板。");
                    return result;
                }

                /// <summary>两个顶层域不做矩阵广播；资源密钥维度单独交给既有密钥投影算法。</summary>
                private static bool IsGlobal(Panel panel) => panel == Panel.Custom || panel == Panel.ResourceEncryption;

                /// <summary>只将已有维度域的精确名称交给公共投影器，新增全局域不得通过此入口。</summary>
                private static DimensionProjector.PanelKind ProjectionPanel(Panel panel)
                {
                    if (IsGlobal(panel)) throw new InvalidOperationException("全局域没有维度投影。");
                    return (DimensionProjector.PanelKind)Enum.Parse(typeof(DimensionProjector.PanelKind), panel.ToString(), false);
                }

                /// <summary>复合字段只放行请求提供的叶子；完整数组替换才放行数组子树，避免广播掩盖未请求兄弟字段变化。</summary>
                private static void AddRequestedPaths(SerializedProperty property, JToken value, HashSet<string> allowed)
                {
                    if (property == null) throw new InvalidOperationException("请求物理字段不存在。");
                    if (property.propertyType == SerializedPropertyType.Generic && !property.isArray && value is JObject fields)
                    {
                        foreach (JProperty field in fields.Properties())
                            AddRequestedPaths(property.FindPropertyRelative(field.Name), field.Value, allowed);
                    }
                    else allowed.Add(property.propertyPath);
                }

                /// <summary>常规编辑必须保留全部旧解密密钥及原字节；轮换使用新编号，不借列表替换销毁已有资源的解密能力。</summary>
                private static void AssertRetainedResourceKeys(ResourceKeyStoreData before, ResourceKeyStoreData after)
                {
                    foreach (ResourcePackageKeys package in before.Packages)
                        foreach (ResourceKeyEntry key in package.Keys)
                        {
                            ResourcePackageKeys retained = after.Packages.SingleOrDefault(item => item.PackageName == package.PackageName);
                            ResourceKeyEntry entry = retained?.Keys.SingleOrDefault(item => item.KeyId == key.KeyId);
                            if (entry == null || entry.KeyBase64 != key.KeyBase64)
                                throw new InvalidOperationException("不能删除旧解密密钥或在旧编号下替换密钥；轮换必须新增编号并保留历史密钥。");
                        }
                }

                /// <summary>逐物理坐标保留已有可用 Privacy AES 凭据；字段与维度广播都不能替代存档轮换事务。</summary>
                /// <param name="before">尚未写入的真实 Master。</param>
                /// <param name="after">字段、维度广播及归一完成后的独立副本。</param>
                private static void AssertRetainedPrivacyCredentials(ConfigMasterSO before, ConfigMasterSO after)
                {
                    foreach (PlatformChannelEntry row in before.GetAllEntries())
                    {
                        after.TryGetEntry(row.Platform, row.Channel, out PlatformChannelEntry changedRow);
                        foreach (DevelopModePrivacyConfigsEntry mode in row.PrivacyConfigsByMode)
                        {
                            PrivacyConfigs original = mode.Config;
                            // 尚未形成有效凭据的初始配置保留逐步填写语义，正式导出仍按现 Validator 校验。
                            if (original == null || Encoding.UTF8.GetByteCount(original.AESKey ?? string.Empty) != 16 ||
                                Encoding.UTF8.GetByteCount(original.AESIV ?? string.Empty) != 16) continue;
                            PrivacyConfigs changed = changedRow?.PrivacyConfigsByMode?.SingleOrDefault(item => item.Mode == mode.Mode)?.Config;
                            if (changed == null || original.AESKey != changed.AESKey || original.AESIV != changed.AESIV)
                                throw new InvalidOperationException("已有 Privacy AES Key/IV 不能通过普通面板编辑更换或清空；需要显式存档迁移契约。");
                        }
                    }
                }

                /// <summary>把已经验证的精确名称转成领域坐标。</summary>
                internal static Coord Coordinate(Target target)
                {
                    Exact(target.platform, out PlatformType p); Exact(target.channel, out ChannelType c); Exact(target.developMode, out DevelopMode m);
                    return new Coord(p, c, m);
                }

                /// <summary>枚举只接受精确名字，拒绝数字字符串和未声明值。</summary>
                private static bool Exact<T>(string value, out T result) where T : struct =>
                    Enum.TryParse(value, false, out result) && Enum.IsDefined(typeof(T), result) && result.ToString() == value;

                /// <summary>维度投影只允许目标面板根及其掩码变化，拒绝波及其它面板或引擎身份。</summary>
                private static void AssertDimensionScope(JObject before, JObject after, Change[] changes)
                {
                    JObject retainedBefore = (JObject)before.DeepClone();
                    JObject retainedAfter = (JObject)after.DeepClone();
                    foreach (Change change in changes.Where(c => (c.dimensions?.Length ?? 0) > 0))
                    {
                        Panel panel = ParsePanel(change.target.panel);
                        RemoveDimensionDomain(retainedBefore, panel, change.target.configType);
                        RemoveDimensionDomain(retainedAfter, panel, change.target.configType);
                    }
                    if (!JToken.DeepEquals(retainedBefore, retainedAfter))
                        throw new InvalidOperationException("维度投影修改了请求外类型、面板或矩阵身份。");
                }

                /// <summary>只从审查副本剔除明确面板域；SDK/Kit 按精确管理类型剔除，保留其它类型、列表顺序和坐标。</summary>
                private static void RemoveDimensionDomain(JObject snapshot, Panel panel, string configType)
                {
                    bool typed = panel == Panel.SDK || panel == Panel.Kit;
                    if (panel == Panel.ResourceEncryption) { snapshot.Remove("ResourceEncryption"); return; }
                    if (typed)
                    {
                        if (snapshot[panel + "Masks"] is JArray masks)
                            foreach (JToken item in masks.Where(item => (string)item["TypeName"] == configType).ToArray()) item.Remove();
                    }
                    else snapshot.Remove(panel + "Mask");
                    if (typed || panel == Panel.AppConfigs || panel == Panel.PrivacyConfigs)
                    {
                        if (snapshot["m_Entries"] is JArray entries)
                            foreach (JObject row in entries.OfType<JObject>())
                                if (row[panel + "ConfigsByMode"] is JArray typedModes && typed)
                                {
                                    foreach (JObject mode in typedModes.OfType<JObject>())
                                        if (mode[panel + "Configs"] is JArray values)
                                            foreach (JToken value in values.Where(value => value is JObject &&
                                                ((string)value["_managedType"]?["ns"] + "." + (string)value["_managedType"]?["class"]).TrimStart('.') == configType).ToArray()) value.Remove();
                                }
                                else if (row[panel + "ByMode"] is JArray modes)
                                    foreach (JObject mode in modes.OfType<JObject>()) mode.Remove("Config");
                    }
                    else
                    {
                        snapshot.Remove(panel.ToString());
                        snapshot.Remove(panel + "Overrides");
                    }
                }

                /// <summary>请求字段精确子树是唯一写集；数组修改只能发生在明确请求的数组内。</summary>
                private static void AssertFieldScope(JObject before, JObject after, HashSet<string> allowed)
                {
                    string JsonPath(string path) => System.Text.RegularExpressions.Regex.Replace(path, @"\.Array\.data\[(\d+)\]", "[$1]");
                    string[] paths = allowed.Select(JsonPath).ToArray();
                    foreach (Difference difference in Differences(before, after))
                        if (!paths.Any(path => difference.path == path || difference.path.StartsWith(path + ".", StringComparison.Ordinal) || difference.path.StartsWith(path + "[", StringComparison.Ordinal)))
                            throw new InvalidOperationException("保存将修改请求字段之外的序列化内容。");
                }

                /// <summary>逐叶比较完整 JSON；结构变化也记录准确地址，但公开输出不包含值。</summary>
                private static IEnumerable<Difference> Differences(JToken before, JToken after)
                {
                    if (JToken.DeepEquals(before, after)) yield break;
                    if (before is JObject left && after is JObject right)
                    {
                        foreach (string name in left.Properties().Select(p => p.Name).Union(right.Properties().Select(p => p.Name)).OrderBy(n => n, StringComparer.Ordinal))
                        {
                            JToken a = left[name], b = right[name];
                            if (a == null || b == null) { yield return new Difference { path = (a ?? b).Path, kind = "structure" }; continue; }
                            foreach (Difference item in Differences(a, b)) yield return item;
                        }
                        yield break;
                    }
                    if (before is JArray oldArray && after is JArray newArray && oldArray.Count == newArray.Count)
                    {
                        for (int i = 0; i < oldArray.Count; i++) foreach (Difference item in Differences(oldArray[i], newArray[i])) yield return item;
                        yield break;
                    }
                    yield return new Difference { path = before.Path, kind = before is JValue && after is JValue ? "value" : "structure" };
                }
            }
        }
    }
}
