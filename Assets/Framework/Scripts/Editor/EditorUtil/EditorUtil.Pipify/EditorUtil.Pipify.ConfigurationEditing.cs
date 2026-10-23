/***************************************************************
 * (c) copyright 2026 - 2030, Solotopia
 * All Rights Reserved.
 * -------------------------------------------------------------
 * filename:  EditorUtil.Pipify.ConfigurationEditing.cs
 * author:    Codex
 * created:   2026/10/13
 * descrip:   Pipify Batch 共享配置预演、草稿保护与精确保存
 ***************************************************************/

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Runtime.Serialization;
using System.Security.Cryptography;
using System.Text;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using NovaFramework.Runtime;
using UnityEditor;
using UnityEngine;

namespace NovaFramework.Editor
{
    public static partial class EditorUtil
    {
        public static partial class Pipify
        {
            /// <summary>配置服务不执行 Step；窗口和 Action 复用真实 Registry、字段规则及目标保存。</summary>
            internal static partial class ConfigurationEditing
            {
                private static readonly List<Func<PipifySettingsSO, bool>> s_DraftGuards = new();
                private static readonly List<Action<PipifySettingsSO>> s_SavedListeners = new();

                /// <summary>省略包装保留原描述；存在包装时 value="" 明确清空。</summary>
                [Serializable]
                public sealed class TextValue { [AgentActionRequired] public string value; }

                /// <summary>精确公开字段名与唯一基础 JSON token，不接受属性路径或任意参数 JSON。</summary>
                [Serializable]
                public sealed class FieldEdit { [AgentActionRequired] public string fieldName; [AgentActionRequired] public string valueJson; }

                /// <summary>sourceIndex 指向原 Batch 的条目；省略表示用注册 Step 新增条目。</summary>
                [Serializable]
                public sealed class StepEdit { public int? sourceIndex; [AgentActionRequired] public string stepId; public FieldEdit[] edits; }

                /// <summary>steps 缺省保留，存在时完整替换目标顺序；空数组明确移除全部条目。</summary>
                [Serializable]
                public class BatchChange
                {
                    [AgentActionRequired] public string settingsGuid;
                    [AgentActionRequired] public string mode;
                    [AgentActionRequired] public string batchName;
                    public string sourceBatchName;
                    public TextValue description;
                    public StepEdit[] steps;
                }

                /// <summary>只含类型与已脱敏字段信息，不输出原始 ParamsJson、方法名或秘密摘要。</summary>
                [Serializable]
                internal sealed class FieldView
                {
                    public string fieldName;
                    public string fieldType;
                    public bool editable;
                    public bool visible;
                    public bool stateKnown;
                    public bool sensitive;
                    public bool hasValue;
                    public string valueJson;
                    public string effectiveValueJson;
                    public string valueSource;
                    public string[] options;
                    public string defaultSource;
                    public string dependsOn;
                    public int[] visibleAnyOf;
                }

                /// <summary>只读展示 Step 真实身份、支持状态和参数字段 Schema。</summary>
                [Serializable]
                internal sealed class StepView
                {
                    public int? sourceIndex;
                    public string stepId;
                    public string displayName;
                    public bool registered;
                    public string paramsType;
                    public FieldView[] fields;
                }

                /// <summary>工作副本生命周期由调用者 using 管理；FrozenChange 仅留在进程内，不作为公开载荷。</summary>
                internal sealed class Preview : IDisposable
                {
                    internal PipifySettingsSO WorkingCopy;
                    internal string BeforeHash;
                    internal string ExpectedHash;
                    internal string ModelHash;
                    internal bool Changed;
                    internal bool AndroidSigningRisk;
                    internal int TargetBatchIndex;
                    internal string[] TargetStepIds;
                    internal string[] RequestedFieldAddresses;
                    internal BatchChange FrozenChange;
                    internal Dictionary<int, string> NewDefaults = new();

                    /// <summary>销毁未持久化副本，不修改真实资产。</summary>
                    public void Dispose() { if (WorkingCopy != null) UnityEngine.Object.DestroyImmediate(WorkingCopy); }
                }

                /// <summary>保存错误保留写入边界，避免失败后错误声明真实资产未变。</summary>
                internal sealed class SaveException : Exception
                {
                    internal bool WriteStarted { get; }
                    /// <summary>仅将稳定阶段暴露给上层，原异常不会成为公开参数输出。</summary>
                    internal SaveException(bool started, Exception inner) : base("Pipify 保存失败。", inner) { WriteStarted = started; }
                }

                /// <summary>注册窗口草稿保护，不让领域层反向依赖窗口。</summary>
                internal static void RegisterDraftGuard(Func<PipifySettingsSO, bool> guard)
                {
                    if (guard == null) throw new ArgumentNullException(nameof(guard));
                    if (!s_DraftGuards.Contains(guard)) s_DraftGuards.Add(guard);
                }

                /// <summary>注册干净窗口的保存后刷新适配器。</summary>
                internal static void RegisterSavedListener(Action<PipifySettingsSO> listener)
                {
                    if (listener == null) throw new ArgumentNullException(nameof(listener));
                    if (!s_SavedListeners.Contains(listener)) s_SavedListeners.Add(listener);
                }

                /// <summary>适配器检测失败也拒绝覆盖，避免丢失未知草稿。</summary>
                internal static bool HasUnsavedDraft(PipifySettingsSO settings)
                {
                    foreach (var guard in s_DraftGuards) { try { if (guard(settings)) return true; } catch { return true; } }
                    return false;
                }

                /// <summary>保存通知只刷新展示；监听失败返回警告，不重复保存。</summary>
                internal static string[] NotifySaved(PipifySettingsSO settings)
                {
                    var warnings = new List<string>();
                    foreach (var listener in s_SavedListeners.ToArray())
                    { try { listener(settings); } catch { warnings.Add("Pipify 已保存，但一个窗口刷新失败，请重新打开窗口。"); } }
                    return warnings.ToArray();
                }

                /// <summary>窗口与 Action 共用单资产保存；不隐式保存其它脏资产。</summary>
                internal static void SaveTarget(PipifySettingsSO settings)
                {
                    if (settings == null || !EditorUtility.IsPersistent(settings)) throw new InvalidOperationException("需要真实 PipifySettings 资产。");
                    EditorUtility.SetDirty(settings);
                    AssetDatabase.SaveAssetIfDirty(settings);
                    if (EditorUtility.IsDirty(settings)) throw new InvalidOperationException("PipifySettings 保存后仍为脏态。");
                }

                /// <summary>提交前检查完成后才复制；写开始后的失败不自动回滚或重放。</summary>
                internal static void SavePreview(PipifySettingsSO settings, Preview preview, Action beforeCommit)
                {
                    bool started = false;
                    try
                    {
                        if (preview == null || preview.WorkingCopy == null || EditorUtility.IsPersistent(preview.WorkingCopy) || settings == preview.WorkingCopy)
                            throw new InvalidOperationException("需要独立 Pipify 工作副本。");
                        beforeCommit?.Invoke();
                        if (!preview.Changed) return;
                        HideFlags flags = settings.hideFlags;
                        string name = settings.name;
                        started = true;
                        EditorUtility.CopySerialized(preview.WorkingCopy, settings);
                        settings.hideFlags = flags;
                        settings.name = name;
                        SaveTarget(settings);
                    }
                    catch (Exception exception) { throw new SaveException(started, exception); }
                }

                /// <summary>仅摘要完整 Batch 序列化状态，不调用领域 Getter。</summary>
                internal static string StateHash(PipifySettingsSO settings) { return Hash(JsonUtility.ToJson(settings)); }

                /// <summary>生成整体摘要；禁止将单值秘密摘要作为公开证据。</summary>
                private static string Hash(string text)
                {
                    using var sha = SHA256.Create();
                    return BitConverter.ToString(sha.ComputeHash(Encoding.UTF8.GetBytes(text ?? string.Empty))).Replace("-", "").ToLowerInvariant();
                }

                /// <summary>冻结 Registry 身份及参数字段声明；不反射执行 Step 或构造参数类型。</summary>
                internal static string ModelHash()
                {
                    return Hash(string.Join("\n", Registry.GetAll().OrderBy(info => info.Id, StringComparer.Ordinal).Select(info =>
                        info.Id + "|" + info.Method.Module.ModuleVersionId + "|" + info.Method.MetadataToken + "|" +
                        info.ParamsType?.AssemblyQualifiedName + "|" + string.Join(",", info.ParamsType?.GetFields(BindingFlags.Public | BindingFlags.Instance)
                            .Select(field => field.Name + ":" + field.FieldType.AssemblyQualifiedName + ":" + field.MetadataToken) ?? Array.Empty<string>()))));
                }

                /// <summary>验证操作形状；所有名称与字段精确匹配，不通过 trim 偷改请求。</summary>
                internal static bool TryValidate(BatchChange change, out string error)
                {
                    error = null;
                    if (change == null || (change.mode != "create" && change.mode != "clone" && change.mode != "edit") ||
                        string.IsNullOrWhiteSpace(change.batchName) || change.batchName != change.batchName.Trim() || change.batchName.Length > 256 ||
                        (change.mode == "clone" && (string.IsNullOrWhiteSpace(change.sourceBatchName) || change.sourceBatchName == change.batchName)) ||
                        (change.sourceBatchName != null && (change.sourceBatchName != change.sourceBatchName.Trim() || change.sourceBatchName.Length > 256)) ||
                        (change.mode == "create" && change.sourceBatchName != null) || change.description != null && change.description.value == null ||
                        change.steps?.Length > 512)
                    { error = "Batch 配置需要精确的 create/clone/edit、非空名称及有效描述/条目。"; return false; }
                    if (change.steps != null)
                        foreach (StepEdit step in change.steps)
                        {
                            if (step == null || string.IsNullOrWhiteSpace(step.stepId) || step.sourceIndex < 0 || step.edits?.Length > 128)
                            { error = "每个 Step 需要注册 ID、非负来源索引和有效字段编辑。"; return false; }
                            var seen = new HashSet<string>(StringComparer.Ordinal);
                            foreach (FieldEdit edit in step.edits ?? Array.Empty<FieldEdit>())
                            {
                                if (edit == null || string.IsNullOrWhiteSpace(edit.fieldName) || !seen.Add(edit.fieldName))
                                { error = "字段编辑缺失、重复或字段名无效。"; return false; }
                                try { ReadValue(edit.valueJson); } catch { error = "字段值必须为一个有效的基础 JSON token。"; return false; }
                            }
                        }
                    return true;
                }

                /// <summary>统一唯一名称判定，窗口仍按原逻辑自动追加序号。</summary>
                internal static bool IsNameDuplicate(PipifySettingsSO settings, string name, int excludeIndex = -1)
                {
                    return settings != null && settings.Batches.Where((_, index) => index != excludeIndex).Any(batch => batch?.Name == name);
                }

                /// <summary>创建或克隆具名 Batch，复制时不共享条目或参数 JSON 容器。</summary>
                internal static Batch AddBatch(PipifySettingsSO settings, string name, Batch source = null)
                {
                    if (settings == null || string.IsNullOrWhiteSpace(name) || IsNameDuplicate(settings, name)) throw new InvalidOperationException("Batch 名称为空或重复。");
                    Batch batch = source == null ? new Batch() : Util.Json.Deserialize<Batch>(Util.Json.Serialize(source));
                    batch.Name = name;
                    settings.Batches.Add(batch);
                    return batch;
                }

                /// <summary>新 Step 使用现有默认工厂；受控调用仅允许框架已核实 DTO 构造器。</summary>
                internal static BatchItem CreateItem(PipifyStepInfo info, bool controlled = false)
                {
                    if (info == null || Registry.FindById(info.Id) != info) throw new InvalidOperationException("Step 必须来自当前 Registry。");
                    object parameters = null;
                    if (info.ParamsType != null)
                    {
                        if (controlled && !IsTrustedDefaultsType(info.ParamsType)) throw new InvalidOperationException("该 Package 新参数默认构造尚未受控封装；可先在窗口创建并保存条目。");
                        if (controlled && info.ParamsType == typeof(PipifySteps.ConfigExportParams))
                        {
                            // 预演不能调用会修复 Globals pathHint 的窗口 Getter；只读身份漂移必须明确阻断。
                            if (!Config.WorkspaceActive.TryGetPersistedConfigMaster(out ConfigMasterSO master, out _, out _, out string sourceError))
                                throw new InvalidOperationException(sourceError);
                            if (EditorUtility.IsDirty(master) || Config.DesignTimeEditing.HasUnsavedDraft(master)) throw new InvalidOperationException("Config 默认来源存在未保存草稿。");
                            ConfigMasterSO copy = UnityEngine.Object.Instantiate(master);
                            try { parameters = CreateDefaultParams(info, copy); }
                            finally { UnityEngine.Object.DestroyImmediate(copy); }
                        }
                        else parameters = CreateDefaultParams(info);
                    }
                    return new BatchItem { StepId = info.Id, ParamsJson = parameters == null ? string.Empty : Util.Json.Serialize(parameters) };
                }

                /// <summary>仅已核实的框架参数构造器可生成自动化默认值，避免任意消费类型构造副作用。</summary>
                private static bool IsTrustedDefaultsType(Type type)
                {
                    return type == typeof(AssetBundleBuildArgs) || type == typeof(RawFileBuildArgs) ||
                        type == typeof(PipifySteps.PackageParams) || type == typeof(PipifySteps.GooglePlayUploadParams) ||
                        type == typeof(PipifySteps.ConfigExportParams) || type == typeof(PipifySteps.OpenFolderParams) ||
                        type == typeof(PipifySteps.FeishuWebhookParams) || type == typeof(PipifySteps.CdnDeployParams) ||
                        type == typeof(PipifySteps.CdnWhitelistDeployParams) || type == typeof(PipifySteps.CdnPurgeParams);
                }

                /// <summary>只在副本改变目标 Batch；其它 Batch 通过整体序列化逐项核对保持原样。</summary>
                internal static Preview BuildPreview(PipifySettingsSO settings, BatchChange change, IReadOnlyDictionary<int, string> frozenDefaults = null)
                {
                    if (!TryValidate(change, out string error)) throw new InvalidOperationException(error);
                    var preview = new Preview { WorkingCopy = UnityEngine.Object.Instantiate(settings), BeforeHash = StateHash(settings), ModelHash = ModelHash(), FrozenChange = Util.Json.Deserialize<BatchChange>(Util.Json.Serialize(change)) };
                    preview.WorkingCopy.name = settings.name;
                    preview.WorkingCopy.hideFlags = settings.hideFlags;
                    try
                    {
                        string sourceName = change.sourceBatchName ?? change.batchName;
                        Batch source = change.mode == "create" ? null : FindUnique(settings, sourceName);
                        int targetIndex = change.mode == "edit" ? settings.Batches.IndexOf(source) : -1;
                        Batch existing = change.mode == "edit" || !IsNameDuplicate(settings, change.batchName) ? null : FindUnique(settings, change.batchName);
                        if (change.mode == "edit" && IsNameDuplicate(settings, change.batchName, targetIndex)) throw new InvalidOperationException("目标 Batch 名称已被其它 Batch 使用。");
                        Batch target = change.mode == "edit" ? preview.WorkingCopy.Batches[targetIndex] :
                            existing == null ? AddBatch(preview.WorkingCopy, change.batchName, source) :
                            source == null ? new Batch { Name = change.batchName } : Util.Json.Deserialize<Batch>(Util.Json.Serialize(source));
                        target.Name = change.batchName;
                        if (change.description != null) target.Description = change.description.value;
                        if (change.steps != null)
                        {
                            var items = new List<BatchItem>();
                            for (int index = 0; index < change.steps.Length; index++)
                            {
                                StepEdit edit = change.steps[index];
                                PipifyStepInfo info = Registry.FindById(edit.stepId);
                                BatchItem item;
                                if (edit.sourceIndex.HasValue)
                                {
                                    int sourceIndex = edit.sourceIndex.Value;
                                    if (source == null || sourceIndex >= source.Items.Count || source.Items[sourceIndex]?.StepId != edit.stepId)
                                        throw new InvalidOperationException("Step 来源索引或注册 ID 不匹配。");
                                    item = new BatchItem { StepId = edit.stepId, ParamsJson = source.Items[sourceIndex].ParamsJson };
                                }
                                else
                                {
                                    if (info == null) throw new InvalidOperationException("新增 Step 不在当前 Registry。");
                                    item = frozenDefaults != null && frozenDefaults.TryGetValue(index, out string json) ? new BatchItem { StepId = edit.stepId, ParamsJson = json } : CreateItem(info, true);
                                    preview.NewDefaults[index] = item.ParamsJson;
                                }
                                if ((edit.edits?.Length ?? 0) > 0)
                                {
                                    if (info?.ParamsType == null) throw new InvalidOperationException("缺失或无参 Step 不可配置字段。");
                                    item.ParamsJson = ApplyFieldEdits(info, item.ParamsJson, edit.edits, out bool risk);
                                    preview.AndroidSigningRisk |= risk;
                                }
                                items.Add(item);
                            }
                            target.Items.Clear();
                            target.Items.AddRange(items);
                        }
                        if (existing != null && Util.Json.Serialize(target) != Util.Json.Serialize(existing))
                            throw new InvalidOperationException("目标名称已存在且配置与创建请求不同；请使用 edit 显式修改。");
                        preview.TargetBatchIndex = targetIndex >= 0 ? targetIndex : existing == null ? settings.Batches.Count : settings.Batches.IndexOf(existing);
                        preview.TargetStepIds = target.Items.Select(item => item?.StepId).ToArray();
                        string batchAddress = "m_Batches[" + preview.TargetBatchIndex + "]";
                        var addresses = new List<string> { batchAddress + ".m_Name" };
                        if (change.description != null) addresses.Add(batchAddress + ".m_Description");
                        if (change.steps != null)
                        {
                            addresses.Add(batchAddress + ".m_Items");
                            for (int index = 0; index < change.steps.Length; index++)
                                foreach (FieldEdit edit in change.steps[index].edits ?? Array.Empty<FieldEdit>())
                                    addresses.Add(batchAddress + ".m_Items[" + index + "].m_ParamsJson/" + edit.fieldName);
                        }
                        preview.RequestedFieldAddresses = addresses.ToArray();
                        for (int index = 0; index < settings.Batches.Count; index++)
                            if (index != targetIndex && Util.Json.Serialize(settings.Batches[index]) != Util.Json.Serialize(preview.WorkingCopy.Batches[index]))
                                throw new InvalidOperationException("预演改变了目标之外的 Batch。");
                        preview.ExpectedHash = StateHash(preview.WorkingCopy);
                        if (existing != null && preview.ExpectedHash == preview.BeforeHash)
                        {
                            preview.NewDefaults.Clear();
                            foreach (int index in Enumerable.Range(0, change.steps?.Length ?? 0).Where(index => !change.steps[index].sourceIndex.HasValue))
                                preview.NewDefaults[index] = existing.Items[index].ParamsJson;
                        }
                        preview.Changed = preview.BeforeHash != preview.ExpectedHash;
                        return preview;
                    }
                    catch { preview.Dispose(); throw; }
                }

                /// <summary>精确定位唯一 Batch，拒绝历史重复名或缺失项。</summary>
                private static Batch FindUnique(PipifySettingsSO settings, string name)
                {
                    Batch[] matches = settings.Batches.Where(batch => batch?.Name == name).ToArray();
                    if (matches.Length != 1) throw new InvalidOperationException("Batch 缺失或名称不唯一。");
                    return matches[0];
                }

                /// <summary>严格读取保存的 JSON 对象；重复字段、注释、额外内容和元数据均拒绝。</summary>
                private static JObject ReadParams(string json)
                {
                    if (string.IsNullOrEmpty(json)) throw new InvalidOperationException("旧空参数需要先在窗口初始化并保存。");
                    using var reader = new JsonTextReader(new StringReader(json)) { DateParseHandling = DateParseHandling.None, MaxDepth = 32 };
                    JToken token = JToken.Load(reader, new JsonLoadSettings { DuplicatePropertyNameHandling = DuplicatePropertyNameHandling.Error, CommentHandling = CommentHandling.Load });
                    if (!(token is JObject obj) || reader.Read() || obj.DescendantsAndSelf().Any(node => node.Type == JTokenType.Comment) ||
                        obj.DescendantsAndSelf().OfType<JProperty>().Any(property => property.Name.StartsWith("$", StringComparison.Ordinal)))
                        throw new InvalidOperationException("参数 JSON 不是可安全编辑的普通对象。");
                    return obj;
                }

                /// <summary>不执行参数构造器或属性 setter；只把已保存的基础字段读进规则状态。</summary>
                private static object BuildRuleInstance(Type type, JObject obj)
                {
                    // 未核实 Package 类型不分配实例，避免其终结器或运行时特殊类型形成额外执行入口。
                    if (!IsTrustedDefaultsType(type)) return null;
                    object instance = FormatterServices.GetUninitializedObject(type);
                    foreach (FieldInfo field in type.GetFields(BindingFlags.Public | BindingFlags.Instance).Where(SupportsField))
                    {
                        field.SetValue(instance, ReadSavedField(field, obj));
                    }
                    PipifySteps.SynchronizeActivePlatform(instance);
                    return instance;
                }

                /// <summary>基础保存 token 转为规则值；未保存数值不推测构造器初值。</summary>
                private static object ReadSavedField(FieldInfo field, JObject obj)
                {
                    JToken value = obj[field.Name];
                    if (value == null || value.Type == JTokenType.Null)
                    {
                        if (field.FieldType == typeof(string)) return null;
                        throw new InvalidOperationException("参数缺失非字符串字段，无法安全确定默认状态。");
                    }
                    if (field.FieldType.IsEnum && value.Type == JTokenType.Integer)
                    {
                        object result = Enum.ToObject(field.FieldType, value.Value<long>());
                        if (!Enum.IsDefined(field.FieldType, result)) throw new InvalidOperationException("已保存枚举值未定义。");
                        return result;
                    }
                    return ConvertValue(field, value);
                }

                /// <summary>未核实 Package 参数只读取字段 token 判断特性，不分配其类型实例。</summary>
                private static bool IsSnapshotVisible(FieldInfo field, Type type, JObject obj)
                {
                    return IsVisible(field, type, name =>
                    {
                        FieldInfo dependency = type.GetField(name, BindingFlags.Public | BindingFlags.Instance);
                        if (dependency == null || !SupportsField(dependency)) throw new InvalidOperationException("参数条件依赖未受支持。");
                        return ReadSavedField(dependency, obj);
                    }, new HashSet<string>());
                }

                /// <summary>仅替换请求字段 token，保留未请求字段及复杂数据；最终状态统一核对显隐、只读及候选。</summary>
                private static string ApplyFieldEdits(PipifyStepInfo info, string json, FieldEdit[] edits, out bool signingRisk)
                {
                    if (!SupportsParamsFields(info.ParamsType)) throw new InvalidOperationException("参数自定义序列化契约未受控。");
                    JObject obj = ReadParams(json);
                    object before = BuildRuleInstance(info.ParamsType, obj);
                    var fields = info.ParamsType.GetFields(BindingFlags.Public | BindingFlags.Instance).ToDictionary(field => field.Name, StringComparer.Ordinal);
                    foreach (FieldEdit edit in edits)
                    {
                        if (!fields.TryGetValue(edit.fieldName, out FieldInfo field) || !SupportsField(field) || field.GetCustomAttribute<PipifyReadOnlyAttribute>() != null)
                            throw new InvalidOperationException("字段不存在、不受支持或只读。");
                        object value = ConvertValue(field, ReadValue(edit.valueJson));
                        obj[field.Name] = JToken.FromObject(value);
                    }
                    object after = BuildRuleInstance(info.ParamsType, obj);
                    foreach (FieldEdit edit in edits)
                    {
                        FieldInfo field = fields[edit.fieldName];
                        if (IsReadOnly(field, after) || !(after == null ? IsSnapshotVisible(field, info.ParamsType, obj) : IsFieldVisible(field, after, info.ParamsType)))
                            throw new InvalidOperationException("目标状态中的字段隐藏或只读。");
                        object value = ConvertValue(field, ReadValue(edit.valueJson));
                        if (field.FieldType.IsEnum && !GetEnumValues(field, after).Contains((Enum)value)) throw new InvalidOperationException("枚举不在当前环境允许选项中。");
                        var dropdown = field.GetCustomAttribute<PipifyDropdownAttribute>();
                        if (dropdown != null && !string.IsNullOrEmpty((string)value) && !(GetDropdownOptions(dropdown.InterfaceType) ?? Array.Empty<string>()).Contains((string)value, StringComparer.Ordinal))
                            throw new InvalidOperationException("接口类型不在当前合法候选中。");
                        var dynamic = field.GetCustomAttribute<PipifyDynamicDropdownAttribute>();
                        if (dynamic != null)
                        {
                            string[] options = ResolveDynamicDropdown(dynamic);
                            if (options == null || options.Length > 0 && !options.Contains((string)value, StringComparer.Ordinal)) throw new InvalidOperationException("动态选项未受控或目标值不可用。");
                        }
                    }
                    signingRisk = ShouldConfirmAndroidRisk(before as PipifySteps.PackageParams, after as PipifySteps.PackageParams, EditorUserBuildSettings.exportAsGoogleAndroidProject);
                    return JToken.DeepEquals(ReadParams(json), obj) ? json : obj.ToString(Formatting.Indented);
                }

                /// <summary>只读生成脱敏 Schema；保存参数只读 token，不执行默认构造或 Runner 迁移。</summary>
                internal static StepView InspectStep(PipifyStepInfo info, BatchItem item = null, int? index = null)
                {
                    var view = new StepView { sourceIndex = index, stepId = item?.StepId ?? info?.Id, displayName = info?.DisplayName, registered = info != null, paramsType = info?.ParamsType?.FullName, fields = Array.Empty<FieldView>() };
                    if (info?.ParamsType == null) return view;
                    bool modelSupported = SupportsParamsFields(info.ParamsType);
                    JObject obj = null;
                    object instance = null;
                    bool stateValid = false;
                    if (!string.IsNullOrEmpty(item?.ParamsJson))
                    {
                        try { obj = ReadParams(item.ParamsJson); instance = BuildRuleInstance(info.ParamsType, obj); stateValid = true; }
                        catch { }
                    }
                    view.fields = info.ParamsType.GetFields(BindingFlags.Public | BindingFlags.Instance).Select(field =>
                    {
                        bool secret = IsSensitive(field);
                        JToken value = obj?[field.Name];
                        var condition = field.GetCustomAttribute<PipifyVisibleWhenAttribute>();
                        var dynamicDefault = field.GetCustomAttribute<PipifyDynamicDefaultAttribute>();
                        var dropdown = field.GetCustomAttribute<PipifyDropdownAttribute>();
                        var dynamic = field.GetCustomAttribute<PipifyDynamicDropdownAttribute>();
                        bool known = false;
                        object currentValue = null;
                        bool visible = true;
                        if (modelSupported && stateValid && SupportsField(field))
                        {
                            try
                            {
                                // 保存 token 与 Runner 当前平台同步值分开；读取不表示同步值已经保存。
                                currentValue = ReadSavedField(field, obj);
                                visible = instance == null ? IsSnapshotVisible(field, info.ParamsType, obj) : IsFieldVisible(field, instance, info.ParamsType);
                                known = true;
                            }
                            catch { }
                        }
                        string[] options = field.FieldType.IsEnum ? (instance == null ? Enum.GetNames(field.FieldType) : GetEnumValues(field, instance).Select(v => v.ToString()).ToArray()) :
                            dropdown != null ? GetDropdownOptions(dropdown.InterfaceType) : dynamic != null ? ResolveDynamicDropdown(dynamic) : null;
                        return new FieldView { fieldName = field.Name, fieldType = field.FieldType.FullName, editable = modelSupported && SupportsField(field) &&
                            (instance == null ? field.GetCustomAttribute<PipifyReadOnlyAttribute>() == null : !IsReadOnly(field, instance)),
                            visible = visible, stateKnown = known,
                            sensitive = secret, hasValue = value != null && value.Type != JTokenType.Null && (value.Type != JTokenType.String || !string.IsNullOrEmpty(value.Value<string>())),
                            valueJson = !secret && SupportsField(field) && known ?
                                (field.FieldType.IsEnum ? JsonConvert.SerializeObject(currentValue?.ToString()) : JsonConvert.SerializeObject(currentValue)) : null,
                            effectiveValueJson = !secret && SupportsField(field) && known && instance != null ?
                                JsonConvert.SerializeObject(field.FieldType.IsEnum ? field.GetValue(instance)?.ToString() : field.GetValue(instance)) : null,
                            valueSource = known ? "stored-params-json; effectiveValueJson includes current Runner platform synchronization" : "unknown; no saved token",
                            options = secret ? null : options, defaultSource = dynamicDefault == null ? "new-step-constructor;not-evaluated" : "dynamic-placeholder;stored-empty-preserved",
                            dependsOn = condition?.DependsOn, visibleAnyOf = condition?.AnyOf };
                    }).ToArray();
                    return view;
                }

                /// <summary>密码特性及常见凭据字段均脱敏，不能因缺少 UI 特性而泄漏凭据。</summary>
                private static bool IsSensitive(FieldInfo field)
                {
                    string name = field.Name.ToLowerInvariant();
                    return field.GetCustomAttribute<PipifyPasswordAttribute>() != null || name.Contains("password") || name.Contains("secret") || name.Contains("token") ||
                        name.Contains("credential") || name.Contains("webhook") || name.Contains("privatekey") || name.Contains("accesskey") || name.EndsWith("pass", StringComparison.Ordinal);
                }
            }
        }
    }
}
