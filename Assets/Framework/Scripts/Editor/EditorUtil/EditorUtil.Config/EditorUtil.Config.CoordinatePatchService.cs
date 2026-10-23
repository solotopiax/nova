/***************************************************************
 * (c) copyright 2026 - 2030, Solotopia
 * All Rights Reserved.
 * -------------------------------------------------------------
 * filename:  EditorUtil.Config.CoordinatePatchService.cs
 * author:    taoye
 * created:   2026/10/12
 * descrip:   固定字段三维配置批量编辑预演与精确写集
 ***************************************************************/

using System;
using System.Collections.Generic;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using Microsoft.CSharp;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using NovaFramework.Runtime;
using UnityEditor;
using UnityEngine;
using Coord = NovaFramework.Editor.EditorUtil.Config.DimensionProjector.Coord;

namespace NovaFramework.Editor
{
    public static partial class EditorUtil
    {
        public static partial class Config
        {
            /// <summary>固定字段的三维配置批量编辑服务；全部预演在工作副本，领域实现可被多个入口复用。</summary>
            internal static class CoordinatePatchService
            {
                internal enum Field { Namespace, AppID, AppAesKey, AppAesIV, CustomConfigCmdName, CustomName }

                /// <summary>一个明确坐标、固定字段和字符串值；空字符串表示清空，null 不接受。</summary>
                internal readonly struct Edit
                {
                    internal readonly Coord Coordinate;
                    internal readonly Field Target;
                    internal readonly string Value;

                    /// <summary>冻结明确坐标、白名单字段及字符串值；合法性由 TryValidateEdit 校验。</summary>
                    internal Edit(Coord coordinate, Field target, string value)
                    {
                        Coordinate = coordinate;
                        Target = target;
                        Value = value;
                    }
                }

                /// <summary>公开物理写集只包含地址和字段名；不输出业务值或逐项密钥哈希。</summary>
                [Serializable]
                internal sealed class PhysicalChange
                {
                    public string platform;
                    public string channel;
                    public string developMode;
                    public string field;
                }

                /// <summary>仍需补齐的配置问题；只输出路径及严重度，避免校验消息携带敏感内容。</summary>
                [Serializable]
                internal sealed class RemainingIssue
                {
                    public string platform;
                    public string channel;
                    public string developMode;
                    public string path;
                    public string severity;
                }

                /// <summary>内存预演结果；Dispose 总是销毁工作副本，原资产永不被预演修改。</summary>
                internal sealed class Preview : IDisposable
                {
                    internal ConfigMasterSO WorkingCopy;
                    internal Edit[] Edits;
                    internal string BeforeStateHash;
                    internal string ExpectedStateHash;
                    internal PhysicalChange[] Changes;
                    internal RemainingIssue[] RemainingIssues;
                    internal string[] CoordinateChanges;

                    /// <summary>销毁本次预演工作副本，可重复调用；从不销毁原资产。</summary>
                    public void Dispose()
                    {
                        if (WorkingCopy != null) UnityEngine.Object.DestroyImmediate(WorkingCopy);
                        WorkingCopy = null;
                    }
                }

                /// <summary>验证固定字段的新值；Namespace 与既有源码生成器共用 CSharpCodeProvider 的标识符规则。</summary>
                internal static bool TryValidateEdit(Edit edit, out string error)
                {
                    error = null;
                    Coord c = edit.Coordinate;
                    if (c.Platform == PlatformType.None || c.Channel == ChannelType.None ||
                        !Enum.IsDefined(typeof(PlatformType), c.Platform) ||
                        !Enum.IsDefined(typeof(ChannelType), c.Channel) ||
                        !Enum.IsDefined(typeof(DevelopMode), c.Mode) || !Enum.IsDefined(typeof(Field), edit.Target))
                    {
                        error = "配置编辑需要有效的明确三维坐标和固定字段。";
                        return false;
                    }
                    if (edit.Value == null || edit.Value.Length > 4096 || edit.Value.Any(char.IsControl))
                    {
                        error = "字段值必须是不超过 4096 字符、无控制字符的字符串；清空请使用空字符串。";
                        return false;
                    }
                    if ((edit.Target == Field.AppAesKey || edit.Target == Field.AppAesIV) &&
                        edit.Value.Length > 0 && Encoding.UTF8.GetByteCount(edit.Value) != 16)
                    {
                        error = "AppAesKey 和 AppAesIV 非空时必须各为 16 个 UTF-8 字节。";
                        return false;
                    }
                    if (edit.Target == Field.Namespace && edit.Value.Length > 0)
                    {
                        using (var provider = new CSharpCodeProvider())
                        {
                            if (edit.Value.Split('.').Any(part => !provider.IsValidIdentifier(part)))
                            {
                                error = "Namespace 必须是有效的 C# 命名空间，或明确使用空字符串清空。";
                                return false;
                            }
                        }
                    }
                    return true;
                }

                /// <summary>在 clone 上核对结构、同组冲突、广播、统一保存归一和全部物理写集；异常不回显值。</summary>
                internal static Preview BuildPreview(ConfigMasterSO master, IReadOnlyList<Edit> edits)
                {
                    if (master == null || edits == null || edits.Count == 0 || edits.Count > 384)
                        throw new InvalidOperationException("配置批量编辑需要 1 至 384 项固定字段修改。");
                    var result = new Preview();
                    ConfigMasterSO before = null;
                    try
                    {
                        result.Edits = edits.ToArray();
                        foreach (Edit edit in result.Edits)
                            if (!TryValidateEdit(edit, out string error)) throw new InvalidOperationException(error);
                        result.WorkingCopy = UnityEngine.Object.Instantiate(master);
                        result.WorkingCopy.hideFlags = HideFlags.DontSave;
                        if (!SchemaGuard.Validate(result.WorkingCopy, out _))
                            throw new InvalidOperationException("ConfigMaster 结构版本不受支持。");
                        string original = Snapshot(result.WorkingCopy);
                        using (var so = new SerializedObject(result.WorkingCopy))
                            DesignTimeEditing.NormalizeWorkingCopy(result.WorkingCopy, so, result.Edits[0].Coordinate);
                        if (Snapshot(result.WorkingCopy) != original)
                            throw new InvalidOperationException("配置需要先通过 ConfigWindow 统一保存整理；批量字段编辑不能连带修复其它面板或矩阵。");
                        before = UnityEngine.Object.Instantiate(result.WorkingCopy);
                        before.hideFlags = HideFlags.DontSave;
                        result.BeforeStateHash = StateHash(before);
                        Dictionary<string, Edit> grouped = GroupEdits(result.WorkingCopy, result.Edits);
                        using (var so = new SerializedObject(result.WorkingCopy))
                        {
                            foreach (Edit edit in grouped.OrderBy(item => item.Key, StringComparer.Ordinal).Select(item => item.Value))
                                ApplyEdit(result.WorkingCopy, so, edit);
                            string permitted = Snapshot(result.WorkingCopy);
                            DesignTimeEditing.NormalizeWorkingCopy(result.WorkingCopy, so, result.Edits[0].Coordinate);
                            if (Snapshot(result.WorkingCopy) != permitted)
                                throw new InvalidOperationException("保存归一将引入请求字段以外的序列化变化，已拒绝批量编辑。");
                        }
                        AssertOnlyRequestedLeafChanges(before, result.WorkingCopy, grouped);
                        result.Changes = BuildChanges(before, result.WorkingCopy, grouped);
                        string expected = Snapshot(result.WorkingCopy);
                        var issues = new List<RemainingIssue>();
                        foreach (Coord coordinate in AllCoordinates(result.WorkingCopy))
                        {
                            foreach (Validator.ValidationIssue issue in Validator.Validate(
                                result.WorkingCopy, coordinate.Platform, coordinate.Channel, coordinate.Mode))
                                issues.Add(new RemainingIssue
                                {
                                    platform = coordinate.Platform.ToString(), channel = coordinate.Channel.ToString(),
                                    developMode = coordinate.Mode.ToString(), path = issue.Path, severity = issue.Level.ToString(),
                                });
                        }
                        if (Snapshot(result.WorkingCopy) != expected)
                            throw new InvalidOperationException("配置校验需要补齐非目标结构，已停止批量字段编辑。");
                        result.ExpectedStateHash = StateHash(result.WorkingCopy);
                        result.RemainingIssues = issues.ToArray();
                        return result;
                    }
                    catch
                    {
                        result.Dispose();
                        throw;
                    }
                    finally
                    {
                        if (before != null) UnityEngine.Object.DestroyImmediate(before);
                    }
                }

                /// <summary>仅预演渠道与开发模式选择；平台由调用方核对活动构建目标，归一不得连带改配置值。</summary>
                /// <param name="master">已保存且干净的真实 ConfigMaster；预演只克隆，不修改原资产。</param>
                /// <param name="coordinate">明确有效的目标坐标；平台只作归一来源，不写入资产。</param>
                /// <returns>完整前后状态及仅含 CurrentChannel、CurrentDevelopMode 的实际叶子写集。</returns>
                internal static Preview BuildCoordinateSwitchPreview(ConfigMasterSO master, Coord coordinate)
                {
                    if (master == null || coordinate.Platform == PlatformType.None || coordinate.Channel == ChannelType.None ||
                        !Enum.IsDefined(typeof(PlatformType), coordinate.Platform) ||
                        !Enum.IsDefined(typeof(ChannelType), coordinate.Channel) || !Enum.IsDefined(typeof(DevelopMode), coordinate.Mode))
                        throw new InvalidOperationException("坐标切换需要有效的明确平台、渠道与开发模式。");
                    var result = new Preview();
                    try
                    {
                        result.WorkingCopy = UnityEngine.Object.Instantiate(master);
                        result.WorkingCopy.hideFlags = HideFlags.DontSave;
                        if (!SchemaGuard.Validate(result.WorkingCopy, out _))
                            throw new InvalidOperationException("ConfigMaster 结构版本不受支持。");
                        string original = Snapshot(result.WorkingCopy);
                        result.BeforeStateHash = StateHash(result.WorkingCopy);
                        result.WorkingCopy.CurrentChannel = coordinate.Channel;
                        result.WorkingCopy.CurrentDevelopMode = coordinate.Mode;
                        string selected = Snapshot(result.WorkingCopy);
                        using (var so = new SerializedObject(result.WorkingCopy))
                            DesignTimeEditing.NormalizeWorkingCopy(result.WorkingCopy, so, coordinate);
                        if (Snapshot(result.WorkingCopy) != selected)
                            throw new InvalidOperationException("坐标切换不能连带整理其他字段，请先独立归一并保存 ConfigMaster。");
                        JObject before = JObject.Parse(original);
                        JObject after = JObject.Parse(selected);
                        var permitted = new HashSet<string>(StringComparer.Ordinal)
                        {
                            nameof(ConfigMasterSO.CurrentChannel), nameof(ConfigMasterSO.CurrentDevelopMode),
                        };
                        AssertLeafChanges(before, after, permitted);
                        result.CoordinateChanges = permitted.Where(field => !JToken.DeepEquals(before[field], after[field]))
                            .OrderBy(field => field, StringComparer.Ordinal).ToArray();
                        result.Changes = Array.Empty<PhysicalChange>();
                        result.Edits = Array.Empty<Edit>();
                        result.RemainingIssues = Array.Empty<RemainingIssue>();
                        result.ExpectedStateHash = StateHash(result.WorkingCopy);
                        return result;
                    }
                    catch
                    {
                        result.Dispose();
                        throw;
                    }
                }

                /// <summary>忽略 Unity clone 的名称和 hideFlags 身份字段，保留所有业务序列化内容及顺序。</summary>
                internal static string Snapshot(ConfigMasterSO master)
                {
                    if (master == null) throw new ArgumentNullException(nameof(master));
                    JObject serialized = JObject.Parse(EditorJsonUtility.ToJson(master));
                    // Unity 6000.4 将 ScriptableObject 包在 MonoBehaviour 节点内；只解包已知的唯一根。
                    JObject json = serialized;
                    if (serialized.Count == 1 && serialized["MonoBehaviour"] is JObject wrapped) json = wrapped;
                    json.Remove("m_Name");
                    json.Remove("m_ObjectHideFlags");
                    return json.ToString(Formatting.None);
                }

                /// <summary>计算整份配置状态指纹；不生成单字段或单个密钥摘要。</summary>
                internal static string StateHash(ConfigMasterSO master)
                {
                    using (SHA256 sha = SHA256.Create())
                        return string.Concat(sha.ComputeHash(Encoding.UTF8.GetBytes(Snapshot(master)))
                            .Select(value => value.ToString("x2")));
                }

                /// <summary>按实际掩码归并固定字段；同逻辑组同字段不同值拒绝，不采用最后一项覆盖。</summary>
                private static Dictionary<string, Edit> GroupEdits(ConfigMasterSO master, IEnumerable<Edit> edits)
                {
                    var groups = new Dictionary<string, Edit>(StringComparer.Ordinal);
                    foreach (Edit edit in edits)
                    {
                        ReadValue(master, edit.Coordinate, edit.Target);
                        string key = GroupKey(master, edit.Coordinate, edit.Target);
                        if (groups.TryGetValue(key, out Edit old) && !string.Equals(old.Value, edit.Value, StringComparison.Ordinal))
                            throw new InvalidOperationException("同一逻辑组的同一字段出现冲突值，请明确唯一目标值。");
                        groups[key] = edit;
                    }
                    return groups;
                }

                /// <summary>逻辑组键只含掩码选中的轴和固定字段，未选轴共享。</summary>
                private static string GroupKey(ConfigMasterSO master, Coord coordinate, Field field)
                {
                    PanelDimensionMask mask = field == Field.Namespace ? master.NamespaceMask : master.AppConfigsMask;
                    if (mask == null) throw new InvalidOperationException("配置面板维度掩码缺失。");
                    return field + "|" + (mask.ByPlatform ? coordinate.Platform.ToString() : "*") + "|" +
                           (mask.ByChannel ? coordinate.Channel.ToString() : "*") + "|" +
                           (mask.ByDevelopMode ? coordinate.Mode.ToString() : "*");
                }

                /// <summary>只读枚举真实矩阵物理坐标；重复或缺失结构由预演归一检查拒绝。</summary>
                private static IEnumerable<Coord> AllCoordinates(ConfigMasterSO master)
                {
                    foreach (PlatformChannelEntry entry in master.GetAllEntries())
                    {
                        if (entry == null) throw new InvalidOperationException("ConfigMaster 矩阵存在空行。");
                        foreach (DevelopMode mode in Enum.GetValues(typeof(DevelopMode)))
                            yield return new Coord(entry.Platform, entry.Channel, mode);
                    }
                }

                /// <summary>直接读取已存在的 mode 配置，不调用会补结构的 getter。</summary>
                private static AppConfigs ReadApp(ConfigMasterSO master, Coord coordinate)
                {
                    if (!master.TryGetEntry(coordinate.Platform, coordinate.Channel, out PlatformChannelEntry entry) ||
                        entry.AppConfigsByMode == null)
                        throw new InvalidOperationException("目标坐标缺少应用配置矩阵行。");
                    DevelopModeAppConfigsEntry slot = entry.AppConfigsByMode.SingleOrDefault(item => item != null && item.Mode == coordinate.Mode);
                    if (slot?.Config == null) throw new InvalidOperationException("目标坐标缺少应用配置 mode 或实例。");
                    return slot.Config;
                }

                /// <summary>固定字段取值；Namespace 用既有只读维度解析器，其余显式 switch。</summary>
                private static string ReadValue(ConfigMasterSO master, Coord coordinate, Field field)
                {
                    if (field == Field.Namespace)
                    {
                        ReadApp(master, coordinate);
                        return DimensionalResolver.ResolveNamespace(master, coordinate.Platform, coordinate.Channel, coordinate.Mode);
                    }
                    AppConfigs app = ReadApp(master, coordinate);
                    switch (field)
                    {
                        case Field.AppID: return app.AppID ?? string.Empty;
                        case Field.AppAesKey: return app.AppAesKey ?? string.Empty;
                        case Field.AppAesIV: return app.AppAesIV ?? string.Empty;
                        case Field.CustomConfigCmdName: return app.CustomConfigCmdName ?? string.Empty;
                        case Field.CustomName: return app.CustomName ?? string.Empty;
                        default: throw new InvalidOperationException("未知配置编辑字段。");
                    }
                }

                /// <summary>显式修改一个工作副本字段，再复用领域广播；不编辑维度开关或运行态快照。</summary>
                private static void ApplyEdit(ConfigMasterSO master, SerializedObject so, Edit edit)
                {
                    if (ReadValue(master, edit.Coordinate, edit.Target) == edit.Value) return;
                    if (edit.Target == Field.Namespace)
                    {
                        if (master.NamespaceMask.IsGlobal) master.Namespace = edit.Value;
                        else DimensionProjector.SetNamespaceAtCoord(master, edit.Coordinate, edit.Value);
                        DimensionProjector.BroadcastWithinGroup(master, so, DimensionProjector.PanelKind.Namespace, null, edit.Coordinate);
                    }
                    else
                    {
                        AppConfigs app = ReadApp(master, edit.Coordinate);
                        switch (edit.Target)
                        {
                            case Field.AppID: app.AppID = edit.Value; break;
                            case Field.AppAesKey: app.AppAesKey = edit.Value; break;
                            case Field.AppAesIV: app.AppAesIV = edit.Value; break;
                            case Field.CustomConfigCmdName: app.CustomConfigCmdName = edit.Value; break;
                            case Field.CustomName: app.CustomName = edit.Value; break;
                            default: throw new InvalidOperationException("未知配置编辑字段。");
                        }
                        DimensionProjector.BroadcastWithinGroup(master, so, DimensionProjector.PanelKind.AppConfigs, null, edit.Coordinate);
                    }
                    so.Update();
                }

                /// <summary>把真实序列化叶子逐项对照；请求外字段、数组长度、对象属性或 Override 新增全部拒绝。</summary>
                private static void AssertOnlyRequestedLeafChanges(
                    ConfigMasterSO before, ConfigMasterSO after, Dictionary<string, Edit> grouped)
                {
                    JObject left = JObject.Parse(Snapshot(before));
                    JObject right = JObject.Parse(Snapshot(after));
                    var allowed = new HashSet<string>(StringComparer.Ordinal);
                    JArray rows = left["m_Entries"] as JArray;
                    if (rows == null) throw new InvalidOperationException("无法识别 ConfigMaster 真实序列化矩阵。");
                    foreach (JObject row in rows)
                    {
                        PlatformType platform = row["Platform"].ToObject<PlatformType>();
                        ChannelType channel = row["Channel"].ToObject<ChannelType>();
                        JArray modes = row["AppConfigsByMode"] as JArray;
                        if (modes == null) throw new InvalidOperationException("无法识别应用配置序列化 mode。");
                        foreach (JObject mode in modes)
                        {
                            Coord coordinate = new Coord(platform, channel, mode["Mode"].ToObject<DevelopMode>());
                            JObject app = mode["Config"] as JObject;
                            if (app == null) throw new InvalidOperationException("应用配置序列化实例缺失。");
                            foreach (Field field in Enum.GetValues(typeof(Field)))
                                if (field != Field.Namespace && grouped.ContainsKey(GroupKey(before, coordinate, field)))
                                {
                                    JToken value = app[field.ToString()];
                                    if (value == null) throw new InvalidOperationException("固定字段未找到真实序列化叶子。");
                                    allowed.Add(value.Path);
                                }
                        }
                    }
                    if (before.NamespaceMask.IsGlobal)
                    {
                        if (grouped.Values.Any(edit => edit.Target == Field.Namespace))
                            allowed.Add(left["Namespace"].Path);
                    }
                    else
                    {
                        JArray overrides = left["NamespaceOverrides"] as JArray;
                        if (overrides == null) throw new InvalidOperationException("Namespace Override 序列化结构缺失。");
                        foreach (JObject item in overrides)
                        {
                            Coord coordinate = new Coord(item["Platform"].ToObject<PlatformType>(),
                                item["Channel"].ToObject<ChannelType>(), item["DevelopMode"].ToObject<DevelopMode>());
                            if (grouped.ContainsKey(GroupKey(before, coordinate, Field.Namespace))) allowed.Add(item["Value"].Path);
                        }
                    }
                    AssertLeafChanges(left, right, allowed);
                }

                /// <summary>递归检查固定形状 JSON；只允许白名单叶子替换，消息不输出旧值、新值或 JSON。</summary>
                private static void AssertLeafChanges(JToken before, JToken after, HashSet<string> allowed)
                {
                    if (JToken.DeepEquals(before, after)) return;
                    if (before is JObject oldObject && after is JObject newObject)
                    {
                        if (!oldObject.Properties().Select(item => item.Name).OrderBy(item => item, StringComparer.Ordinal)
                            .SequenceEqual(newObject.Properties().Select(item => item.Name).OrderBy(item => item, StringComparer.Ordinal)))
                            throw new InvalidOperationException("批量编辑引入了非目标序列化属性变化。");
                        foreach (JProperty property in oldObject.Properties())
                            AssertLeafChanges(property.Value, newObject[property.Name], allowed);
                        return;
                    }
                    if (before is JArray oldArray && after is JArray newArray)
                    {
                        if (oldArray.Count != newArray.Count)
                            throw new InvalidOperationException("批量编辑引入了数组或 Override 结构变化，请先在 ConfigWindow 整理保存。");
                        for (int i = 0; i < oldArray.Count; i++) AssertLeafChanges(oldArray[i], newArray[i], allowed);
                        return;
                    }
                    if (!(before is JValue) || !(after is JValue) || !allowed.Contains(before.Path))
                        throw new InvalidOperationException("批量编辑引入了请求字段之外的真实序列化叶子变化。");
                }

                /// <summary>比较全部物理坐标的六个固定字段；任何请求逻辑组之外的变化立即拒绝。</summary>
                private static PhysicalChange[] BuildChanges(ConfigMasterSO before, ConfigMasterSO after, Dictionary<string, Edit> grouped)
                {
                    var changes = new List<PhysicalChange>();
                    foreach (Coord coordinate in AllCoordinates(after))
                    {
                        foreach (Field field in Enum.GetValues(typeof(Field)))
                        {
                            string oldValue = ReadValue(before, coordinate, field);
                            string newValue = ReadValue(after, coordinate, field);
                            string key = GroupKey(after, coordinate, field);
                            if (grouped.TryGetValue(key, out Edit edit) && !string.Equals(newValue, edit.Value, StringComparison.Ordinal))
                                throw new InvalidOperationException("物理坐标的目标字段未达到明确请求值。");
                            if (string.Equals(oldValue, newValue, StringComparison.Ordinal)) continue;
                            if (!grouped.ContainsKey(key)) throw new InvalidOperationException("请求范围之外的物理字段发生变化。");
                            changes.Add(new PhysicalChange
                            {
                                platform = coordinate.Platform.ToString(), channel = coordinate.Channel.ToString(),
                                developMode = coordinate.Mode.ToString(), field = field.ToString(),
                            });
                        }
                    }
                    return changes.OrderBy(item => item.platform, StringComparer.Ordinal).ThenBy(item => item.channel, StringComparer.Ordinal)
                        .ThenBy(item => item.developMode, StringComparer.Ordinal).ThenBy(item => item.field, StringComparer.Ordinal).ToArray();
                }
            }
        }
    }
}
