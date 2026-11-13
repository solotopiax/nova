/***************************************************************
 * (c) copyright 2026 - 2030, Solotopia
 * All Rights Reserved.
 * -------------------------------------------------------------
 * filename:  EditorUtil.UIBindings.cs
 * author:    taoye
 * created:   2026/11/12
 * descrip:   普通 Prefab 的 UIView 字段只读预演、精确保存与失败恢复
 ***************************************************************/

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Security.Cryptography;
using System.Text;
using System.Text.RegularExpressions;
using NovaFramework.Runtime;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using Object = UnityEngine.Object;
using PackageInfo = UnityEditor.PackageManager.PackageInfo;
using PackageSource = UnityEditor.PackageManager.PackageSource;
using ReflectionField = System.Reflection.FieldInfo;
using Path = System.IO.Path;

namespace NovaFramework.Editor
{
    public static partial class EditorUtil
    {
        public static partial class UIBindings
        {
            private const BindingFlags MemberFlags = BindingFlags.Instance | BindingFlags.Static |
                BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.DeclaredOnly;

            /// <summary>一次查询内共享实际资产、对象身份与业务脚本，不实例化或加载临时场景。</summary>
            private sealed class Context
            {
                internal string Path;
                internal string AbsolutePath;
                internal GameObject Root;
                internal Object[] Objects;
                internal Dictionary<string, Object> ById;
                internal UIView View;
                internal string ScriptPath;
                internal MonoScript Script;
            }

            /// <summary>只读列出实际对象与业务字段；不创建文件、不 Apply 序列化缓存、不保存资产。</summary>
            /// <param name="prefabPath">项目自有 Assets 内已有普通 Prefab。</param>
            /// <returns>精确身份、逐段层级名、类型、当前引用与写入限制。</returns>
            /// <exception cref="InvalidOperationException">目标不是支持的普通 Prefab，或有 Missing Script 等身份风险。</exception>
            public static Inspection Inspect(string prefabPath)
            {
                Context context = LoadContext(prefabPath);
                var views = new List<ViewInfo>();
                foreach (UIView view in context.Objects.OfType<UIView>())
                {
                    SetView(context, ObjectId(view));
                    using (var serialized = new SerializedObject(view))
                    {
                        serialized.Update();
                        views.Add(new ViewInfo
                        {
                            ObjectId = ObjectId(view), TypeName = view.GetType().AssemblyQualifiedName,
                            ScriptPath = context.ScriptPath, BlockedReason = WriteBlockReason(context),
                            Fields = BusinessFields(view.GetType()).Where(IsObjectField).Select(field =>
                            {
                                SerializedProperty property = serialized.FindProperty(field.Name);
                                if (property == null || property.propertyType != SerializedPropertyType.ObjectReference) return null;
                                return new FieldInfo
                                {
                                    FieldName = field.Name, TypeName = field.FieldType.AssemblyQualifiedName,
                                    CurrentObjectId = ObjectId(property.objectReferenceValue),
                                    MissingReference = property.objectReferenceValue == null && property.objectReferenceEntityIdValue != default,
                                };
                            }).Where(field => field != null).ToArray(),
                        });
                    }
                }
                return new Inspection
                {
                    PrefabPath = context.Path, PrefabGuid = AssetDatabase.AssetPathToGUID(context.Path), Views = views.ToArray(),
                    Objects = context.Objects.Select(value => new ObjectInfo
                    {
                        ObjectId = ObjectId(value), TypeName = value.GetType().AssemblyQualifiedName,
                        HierarchyNames = HierarchyNames(value is GameObject gameObject ? gameObject.transform : ((Component)value).transform),
                    }).ToArray(),
                };
            }

            /// <summary>整批校验既有直接引用字段；空字段可绑定，已有相同引用视为无变化，其他已有引用明确拒绝。</summary>
            /// <param name="request">同一 Prefab、同一 UIView 与一至一百二十八个精确字段。</param>
            /// <returns>只在当前进程内单次可用的不可变计划；不修改实际对象或磁盘。</returns>
            /// <exception cref="InvalidOperationException">字段、类型、引用、草稿或对象身份存在冲突。</exception>
            public static BindingPlan Preview(BindingRequest request)
            {
                BindingRequest input = CopyRequest(request);
                Context context = Resolve(input);
                AssertWritable(context);
                AssertOwnedGeneratedCompiled(context);
                int changes = ValidateBindings(context, input);
                return new BindingPlan { Input = input, Before = Capture(context), ChangeCount = changes };
            }

            /// <summary>复核计划完整快照，再一次性应用并保存指定引用；未列出的字段、层级和全部监听不修改。</summary>
            /// <param name="plan">Preview 返回且未消费的单次计划。</param>
            /// <returns>实际变化数量与可供只读复核的保存回执。</returns>
            /// <exception cref="InvalidOperationException">计划已用、来源漂移、已有引用冲突或保存未成功。</exception>
            /// <exception cref="AggregateException">写入失败且恢复不完整，消息包含仍需保留的恢复目录。</exception>
            public static BindingReceipt Apply(BindingPlan plan) => ApplyCore(plan, SavePrefab);

            /// <summary>内部事务实现允许框架单元测试注入保存失败；公开调用始终使用 Unity 正常保存路径。</summary>
            /// <param name="plan">已校验的完整计划。</param>
            /// <param name="save">内部唯一保存步骤；不作为用户输入或 Action 参数暴露。</param>
            /// <returns>成功保存或无变化的回执。</returns>
            internal static BindingReceipt ApplyCore(BindingPlan plan, Action<GameObject> save)
            {
                if (plan == null) throw new ArgumentNullException(nameof(plan));
                if (save == null) throw new ArgumentNullException(nameof(save));
                if (plan.Consumed != 0) throw new InvalidOperationException("绑定计划已消费，请重新 Preview；不能重放。 ");
                plan.Consumed = 1;
                Context context = Resolve(plan.Input);
                AssertWritable(context);
                AssertSnapshot(context, plan.Before);
                AssertOwnedGeneratedCompiled(context);
                int changes = ValidateBindings(context, plan.Input);
                if (changes != plan.ChangeCount) throw new InvalidOperationException("绑定目标变化，请重新预演。 ");
                if (changes == 0) return CreateReceipt(context, plan.Input, 0);

                string recoveryRoot = Path.Combine(ProjectRoot(), "Library", "Nova", "UIBindings", Guid.NewGuid().ToString("N"));
                Directory.CreateDirectory(recoveryRoot);
                string backup = Path.Combine(recoveryRoot, "before.prefab");
                try { File.Copy(context.AbsolutePath, backup, false); }
                catch { Directory.Delete(recoveryRoot, true); throw; }
                try
                {
                    using (var serialized = new SerializedObject(context.View))
                    {
                        serialized.Update();
                        foreach (Binding binding in plan.Input.Bindings)
                            serialized.FindProperty(binding.FieldName).objectReferenceValue = context.ById[binding.TargetObjectId];
                        serialized.ApplyModifiedPropertiesWithoutUndo();
                    }
                    save(context.Root);
                    if (context.Objects.Any(EditorUtility.IsDirty)) throw new InvalidOperationException("保存后仍有未保存状态，开始恢复原 Prefab。 ");
                    if (ValidateBindings(context, plan.Input) != 0) throw new InvalidOperationException("保存后的字段未精确匹配完整计划。 ");
                    BindingReceipt receipt = CreateReceipt(context, plan.Input, changes);
                    Directory.Delete(recoveryRoot, true);
                    return receipt;
                }
                catch (Exception failure)
                {
                    try
                    {
                        // 只恢复保存前的原始字节，不解析或拼写 Prefab YAML。
                        File.Copy(backup, context.AbsolutePath, true);
                        AssetDatabase.ImportAsset(context.Path, ImportAssetOptions.ForceSynchronousImport | ImportAssetOptions.ForceUpdate);
                        Context restored = Resolve(plan.Input);
                        AssertSnapshot(restored, plan.Before);
                        if (restored.Objects.Any(EditorUtility.IsDirty)) throw new InvalidOperationException("恢复后的 Prefab 仍有脏态。 ");
                        Directory.Delete(recoveryRoot, true);
                    }
                    catch (Exception restoreFailure)
                    {
                        var aggregate = new AggregateException("绑定失败且恢复不完整；请保留恢复目录：" + recoveryRoot, failure, restoreFailure);
                        aggregate.Data[RecoveryPathDataKey] = recoveryRoot;
                        throw aggregate;
                    }
                    throw;
                }
            }

            /// <summary>只读核对完整保存字节、脚本来源及全部请求字段；不保存、恢复或重放绑定。</summary>
            /// <param name="receipt">本次 Apply 返回的保存回执。</param>
            /// <returns>资产及所有精确字段仍满足回执时为 true；无效、漂移或缺失时为 false。</returns>
            public static bool Verify(BindingReceipt receipt)
            {
                try
                {
                    if (receipt == null) return false;
                    Context context = Resolve(receipt.Request);
                    return context.ScriptPath == receipt.ScriptPath &&
                        FileHash(context.AbsolutePath) == receipt.PrefabHash && FileHash(context.AbsolutePath + ".meta") == receipt.PrefabMetaHash &&
                        FileHash(Absolute(context.ScriptPath)) == receipt.ScriptHash && FileHash(Absolute(context.ScriptPath) + ".meta") == receipt.ScriptMetaHash &&
                        SourcesEqual(CaptureBindingSources(context), receipt.Sources) &&
                        ValidateBindings(context, receipt.Request) == 0 && !context.Objects.Any(EditorUtility.IsDirty);
                }
                catch (Exception) { return false; }
            }

            /// <summary>复制并严格核验输入，拒绝空、重复或无法精确识别的字段和对象。</summary>
            private static BindingRequest CopyRequest(BindingRequest request)
            {
                if (request == null) throw new ArgumentNullException(nameof(request));
                if (request.Bindings == null || request.Bindings.Length < 1 || request.Bindings.Length > 128)
                    throw new InvalidOperationException("Bindings 必须包含一至一百二十八个明确字段。 ");
                var names = new HashSet<string>(StringComparer.Ordinal);
                var bindings = new Binding[request.Bindings.Length];
                for (int index = 0; index < bindings.Length; index++)
                {
                    Binding value = request.Bindings[index];
                    if (value == null || !ValidIdentifier(value.FieldName) || SystemField(value.FieldName) || !names.Add(value.FieldName) ||
                        string.IsNullOrEmpty(value.TargetObjectId) || !GlobalObjectId.TryParse(value.TargetObjectId, out _) ||
                        string.IsNullOrEmpty(value.TypeName) || value.TypeName.Length > 2048)
                        throw new InvalidOperationException("每项必须包含唯一字段名、Inspect 返回的精确对象标识和完整类型。 ");
                    bindings[index] = new Binding { FieldName = value.FieldName, TargetObjectId = value.TargetObjectId, TypeName = value.TypeName };
                }
                if (string.IsNullOrEmpty(request.ViewObjectId) || !GlobalObjectId.TryParse(request.ViewObjectId, out _))
                    throw new InvalidOperationException("ViewObjectId 必须是 Inspect 返回的业务 UIView 标识。 ");
                return new BindingRequest { PrefabPath = request.PrefabPath, ViewObjectId = request.ViewObjectId, Bindings = bindings };
            }

            /// <summary>加载已存在普通 Prefab 的持久对象；变体、嵌套和缺失脚本保持显式受阻。</summary>
            private static Context LoadContext(string path)
            {
                string absolute = OwnedAbsolute(path);
                if (!path.EndsWith(".prefab", StringComparison.Ordinal) || !File.Exists(absolute) || !File.Exists(absolute + ".meta"))
                    throw new InvalidOperationException("目标必须是已保存且有元数据的项目自有普通 Prefab。 ");
                GameObject root = AssetDatabase.LoadAssetAtPath<GameObject>(path);
                if (root == null || PrefabUtility.GetPrefabAssetType(root) != PrefabAssetType.Regular)
                    throw new InvalidOperationException("批量绑定工具仅支持普通 Prefab；变体请沿用现有专用 Prefab 编辑方式。 ");
                Transform[] transforms = root.GetComponentsInChildren<Transform>(true);
                if (transforms.Any(transform => PrefabUtility.IsAnyPrefabInstanceRoot(transform.gameObject)))
                    throw new InvalidOperationException("批量绑定工具不修改嵌套 Prefab；请沿用现有专用 Prefab 编辑方式。 ");
                Object[] objects = transforms.SelectMany(transform => new[] { (Object)transform.gameObject }
                    .Concat(transform.GetComponents<Component>().Cast<Object>())).ToArray();
                if (objects.Any(value => value == null)) throw new InvalidOperationException("Prefab 含 Missing Script，不能冻结完整写入范围。 ");
                var byId = new Dictionary<string, Object>(StringComparer.Ordinal);
                foreach (Object value in objects)
                {
                    string id = ObjectId(value);
                    if (string.IsNullOrEmpty(id) || !byId.TryAdd(id, value)) throw new InvalidOperationException("Prefab 对象身份缺失或重复，不能安全选择目标。 ");
                }
                return new Context { Path = path, AbsolutePath = absolute, Root = root, Objects = objects, ById = byId };
            }

            /// <summary>从精确对象标识取得当前 Prefab 中的真实业务 UIView。</summary>
            private static Context Resolve(BindingRequest input)
            {
                if (input == null) throw new ArgumentNullException(nameof(input));
                Context context = LoadContext(input.PrefabPath);
                SetView(context, input.ViewObjectId);
                return context;
            }

            /// <summary>核对业务 UIView 与实际 MonoScript 身份，不根据类名或目录推断脚本。</summary>
            private static void SetView(Context context, string objectId)
            {
                if (objectId == null || !context.ById.TryGetValue(objectId, out Object value) || !(value is UIView view) || view.GetType().IsAbstract)
                    throw new InvalidOperationException("ViewObjectId 必须精确指向该 Prefab 中已挂载的具体业务 UIView。 ");
                MonoScript script = MonoScript.FromMonoBehaviour(view);
                string scriptPath = script == null ? null : AssetDatabase.GetAssetPath(script);
                if (script == null || script.GetClass() != view.GetType() || string.IsNullOrEmpty(scriptPath) ||
                    !scriptPath.EndsWith(".cs", StringComparison.Ordinal) || !File.Exists(OwnedAbsolute(scriptPath)) ||
                    !File.Exists(Absolute(scriptPath) + ".meta") || string.IsNullOrEmpty(AssetDatabase.AssetPathToGUID(scriptPath)))
                    throw new InvalidOperationException("业务 UIView 必须拥有项目自有的真实匹配 MonoScript，不能修改框架或第三方源码。 ");
                context.View = view;
                context.Script = script;
                context.ScriptPath = scriptPath;
            }

            /// <summary>在任何赋值前检查整批字段、目标、精确类型和已有引用；不存在部分预演写入。</summary>
            private static int ValidateBindings(Context context, BindingRequest request)
            {
                if (request?.Bindings == null || request.Bindings.Length == 0) throw new InvalidOperationException("绑定输入为空。 ");
                ReflectionField[] fields = BusinessFields(context.View.GetType()).ToArray();
                int changes = 0;
                using (var serialized = new SerializedObject(context.View))
                {
                    serialized.Update();
                    foreach (Binding binding in request.Bindings)
                    {
                        Type requestedType = BindingType(binding.TypeName);
                        Object target = BindingTarget(context, binding, requestedType);
                        ReflectionField[] matches = fields.Where(field => field.Name == binding.FieldName).ToArray();
                        if (matches.Length != 1 || !IsObjectField(matches[0]) || matches[0].FieldType != requestedType)
                            throw new InvalidOperationException("字段不存在、被同名成员遮蔽或类型不匹配：" + binding.FieldName + "；生成字段后须完成真实编译并重新 Preview。 ");
                        SerializedProperty property = serialized.FindProperty(binding.FieldName);
                        if (property == null || property.propertyType != SerializedPropertyType.ObjectReference || !property.editable)
                            throw new InvalidOperationException("字段不是可编辑的直接序列化对象引用：" + binding.FieldName);
                        if (property.objectReferenceValue == null && property.objectReferenceEntityIdValue != default)
                            throw new InvalidOperationException("字段保留失效引用，必须先明确处理原引用：" + binding.FieldName);
                        if (property.objectReferenceValue != null && property.objectReferenceValue != target)
                            throw new InvalidOperationException("字段已有其他引用，工具不会覆盖：" + binding.FieldName);
                        if (property.objectReferenceValue != target) changes++;
                    }
                }
                return changes;
            }

            /// <summary>只接受已加载且完整名称一致的非泛型 Unity 对象类型，避免把输入文本拼入代码。</summary>
            private static Type BindingType(string typeName)
            {
                Type type = string.IsNullOrEmpty(typeName) ? null : Type.GetType(typeName, false);
                if (type == null || type.AssemblyQualifiedName != typeName || !typeof(Object).IsAssignableFrom(type) ||
                    type.ContainsGenericParameters || type.IsGenericType || !type.IsVisible ||
                    !Regex.IsMatch(type.FullName ?? string.Empty, @"^[A-Za-z_][A-Za-z0-9_]*(?:[.+][A-Za-z_][A-Za-z0-9_]*)*$"))
                    throw new InvalidOperationException("TypeName 必须是已加载且可见的 Unity 对象类型的精确 AssemblyQualifiedName。 ");
                return type;
            }

            /// <summary>目标必须是同一 Prefab 内确切的现存对象，兼容类型不能替代对象身份。</summary>
            private static Object BindingTarget(Context context, Binding binding, Type fieldType)
            {
                if (!context.ById.TryGetValue(binding.TargetObjectId, out Object target) || !fieldType.IsInstanceOfType(target))
                    throw new InvalidOperationException("目标不在该 Prefab 中、组件已缺失或类型不匹配：" + binding.FieldName);
                return target;
            }

            /// <summary>枚举业务继承链上的字段，遇到 UIView 停止，框架状态不进入批量绑定范围。</summary>
            private static IEnumerable<ReflectionField> BusinessFields(Type type)
            {
                for (Type current = type; current != null && current != typeof(UIView); current = current.BaseType)
                    foreach (ReflectionField field in current.GetFields(MemberFlags)) yield return field;
            }

            /// <summary>仅接受非静态、非只读、非隐藏的直接 Unity 对象序列化字段，不展开数组、结构或事件。</summary>
            private static bool IsObjectField(ReflectionField field) => !field.IsStatic && !field.IsInitOnly && !field.IsLiteral &&
                !field.IsNotSerialized && !field.IsDefined(typeof(HideInInspector), true) && typeof(Object).IsAssignableFrom(field.FieldType) &&
                (field.IsPublic || field.IsDefined(typeof(SerializeField), true));

            /// <summary>拒绝未就绪编辑器、已有脏态、PrefabStage、写保护和无法隔离的业务回调。</summary>
            private static string WriteBlockReason(Context context)
            {
                if (EditorApplication.isPlayingOrWillChangePlaymode || EditorApplication.isCompiling || EditorApplication.isUpdating || EditorUtility.scriptCompilationFailed)
                    return "请在编辑器空闲且编译通过后预演绑定。";
                if ((File.GetAttributes(context.AbsolutePath) & FileAttributes.ReadOnly) != 0 || !AssetDatabase.IsOpenForEdit(context.Path))
                    return "Prefab 当前不可写。";
                if (context.Objects.Any(EditorUtility.IsDirty)) return "Prefab 有未保存改动，请先完成原有编辑。";
                var stage = PrefabStageUtility.GetCurrentPrefabStage();
                if (stage != null && stage.assetPath == context.Path) return "Prefab 正在 PrefabStage 编辑，请先完成并关闭草稿。";
                string callbackReason = ComponentCallbackBlockReason(context);
                if (callbackReason != null) return callbackReason;
                foreach (UnityEditor.Editor editor in Resources.FindObjectsOfTypeAll<UnityEditor.Editor>())
                {
                    if (editor == null || editor.targets == null || !editor.targets.Any(context.Objects.Contains)) continue;
                    if (editor.GetType().Assembly != typeof(UnityEditor.Editor).Assembly) return "目标有打开的自定义 Inspector，无法确认其私有草稿。";
                    if (editor.serializedObject != null && editor.serializedObject.hasModifiedProperties) return "目标 Inspector 有待应用草稿。";
                }
                return null;
            }

            /// <summary>
            /// 在完整状态快照和保存前检查当前 Prefab 的全部脚本组件，而非只检查所选 UIView。
            /// 这里只读当前已加载对象的类型；不扫描其他资产，也不承诺隔离装载期生命周期或全局导入回调。
            /// </summary>
            private static string ComponentCallbackBlockReason(Context context)
            {
                var trustedTypes = new Dictionary<Type, bool>();
                foreach (MonoBehaviour component in context.Objects.OfType<MonoBehaviour>())
                {
                    Type type = component.GetType();
                    bool callbacks = typeof(ISerializationCallbackReceiver).IsAssignableFrom(type) ||
                        type.IsDefined(typeof(ExecuteAlways), true) || type.IsDefined(typeof(ExecuteInEditMode), true);
                    for (Type current = type; !callbacks && current != null && current != typeof(MonoBehaviour); current = current.BaseType)
                        callbacks = current.GetMethods(MemberFlags).Any(method => method.Name == "OnValidate");
                    if (!callbacks) continue;
                    if (!trustedTypes.TryGetValue(type, out bool trusted))
                        trustedTypes[type] = trusted = IsRegisteredUnityUiComponent(component);
                    if (!trusted)
                        return "组件「" + type.FullName + "」有编辑期或序列化回调，请沿用其专用编辑方式；本工具不会先序列化或保存整份 Prefab。";
                }
                return null;
            }

            /// <summary>
            /// 仅放行当前 Unity 已注册 uGUI/TMP 包中的原类型：程序集对象、MonoScript 类型、脚本所属包与物理来源须同时一致。
            /// 名称或程序集前缀不构成信任；项目自定义子类和本地改装包仍走原编辑路径，不视为任意第三方回调白名单。
            /// </summary>
            private static bool IsRegisteredUnityUiComponent(MonoBehaviour component)
            {
                try
                {
                    Type type = component.GetType();
                    Assembly assembly = type.Assembly;
                    bool ugui = assembly == typeof(UnityEngine.UI.Button).Assembly;
                    bool tmp = assembly == typeof(TMPro.TMP_Text).Assembly;
                    if (!ugui && !tmp || assembly.IsDynamic) return false;
                    PackageInfo package = PackageInfo.FindForAssembly(assembly);
                    if (package == null || package.name != "com.unity.ugui" && (!tmp || package.name != "com.unity.textmeshpro") ||
                        package.source != PackageSource.Registry && package.source != PackageSource.BuiltIn ||
                        package.assetPath != "Packages/" + package.name || string.IsNullOrEmpty(package.resolvedPath)) return false;
                    MonoScript script = MonoScript.FromMonoBehaviour(component);
                    string scriptPath = script == null ? null : AssetDatabase.GetAssetPath(script);
                    if (script == null || script.GetClass() != type || string.IsNullOrEmpty(scriptPath) ||
                        !scriptPath.StartsWith(package.assetPath + "/", StringComparison.Ordinal) ||
                        string.IsNullOrEmpty(AssetDatabase.AssetPathToGUID(scriptPath))) return false;
                    PackageInfo source = PackageInfo.FindForAssetPath(scriptPath);
                    if (source == null || source.packageId != package.packageId || source.name != package.name ||
                        source.version != package.version || source.source != package.source || string.IsNullOrEmpty(source.resolvedPath)) return false;
                    StringComparison comparison = Application.platform == RuntimePlatform.WindowsEditor
                        ? StringComparison.OrdinalIgnoreCase : StringComparison.Ordinal;
                    string root = Path.GetFullPath(package.resolvedPath).TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
                    string sourceRoot = Path.GetFullPath(source.resolvedPath).TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
                    if (!string.Equals(root, sourceRoot, comparison)) return false;
                    string physical = Path.GetFullPath(Path.Combine(root, scriptPath.Substring(package.assetPath.Length + 1)));
                    return physical.StartsWith(root + Path.DirectorySeparatorChar, comparison) && File.Exists(physical);
                }
                catch (Exception) { return false; }
            }

            /// <summary>把只读安全检查转换为执行前的明确阻断。</summary>
            private static void AssertWritable(Context context)
            {
                string reason = WriteBlockReason(context);
                if (reason != null) throw new InvalidOperationException(reason);
            }

            /// <summary>冻结完整 Prefab、全部对象状态与真实业务脚本及元数据。</summary>
            private static Snapshot Capture(Context context) => new Snapshot
            {
                PrefabHash = FileHash(context.AbsolutePath), PrefabMetaHash = FileHash(context.AbsolutePath + ".meta"),
                StateHash = StateHash(context), ScriptPath = context.ScriptPath,
                ScriptHash = FileHash(Absolute(context.ScriptPath)), ScriptMetaHash = FileHash(Absolute(context.ScriptPath) + ".meta"),
                Sources = CaptureBindingSources(context),
            };

            /// <summary>计划中的任意来源、对象或脚本漂移均要求重新预演，不自动重放旧请求。</summary>
            private static void AssertSnapshot(Context context, Snapshot before)
            {
                Snapshot current = Capture(context);
                if (before == null || current.PrefabHash != before.PrefabHash || current.PrefabMetaHash != before.PrefabMetaHash ||
                    current.StateHash != before.StateHash || current.ScriptPath != before.ScriptPath ||
                    current.ScriptHash != before.ScriptHash || current.ScriptMetaHash != before.ScriptMetaHash || !SourcesEqual(current.Sources, before.Sources))
                    throw new InvalidOperationException("Prefab、对象状态或业务脚本已变化，请重新 Preview。 ");
            }

            /// <summary>按稳定对象身份排序后冻结全部序列化状态，未列出的字段也参与漂移检查。</summary>
            private static string StateHash(Context context) => HashText(string.Join("\n", context.ById.OrderBy(pair => pair.Key, StringComparer.Ordinal)
                .Select(pair => pair.Key + "\n" + EditorJsonUtility.ToJson(pair.Value))));

            /// <summary>只通过 Unity Prefab 保存 API 写入该资产，不执行全工程 SaveAssets。</summary>
            private static void SavePrefab(GameObject root)
            {
                PrefabUtility.SavePrefabAsset(root, out bool saved);
                if (!saved) throw new InvalidOperationException("Unity 未成功保存 Prefab。 ");
            }

            /// <summary>读取本次保存后的完整文件摘要并复制输入，不保存业务字段原值。</summary>
            private static BindingReceipt CreateReceipt(Context context, BindingRequest input, int changes) => new BindingReceipt
            {
                Request = CopyRequest(input), ChangedFields = changes, PrefabHash = FileHash(context.AbsolutePath),
                PrefabMetaHash = FileHash(context.AbsolutePath + ".meta"), ScriptPath = context.ScriptPath,
                ScriptHash = FileHash(Absolute(context.ScriptPath)), ScriptMetaHash = FileHash(Absolute(context.ScriptPath) + ".meta"),
                Sources = CaptureBindingSources(context),
            };

            /// <summary>取得持久对象的完整标识；真正的空引用返回空字符串。</summary>
            private static string ObjectId(Object value) => value == null ? string.Empty : GlobalObjectId.GetGlobalObjectIdSlow(value).ToString();

            /// <summary>逐段保留对象名称；不把同名对象或带斜线名称合并成模糊路径。</summary>
            private static string[] HierarchyNames(Transform transform)
            {
                var names = new List<string>();
                for (Transform current = transform; current != null; current = current.parent) names.Add(current.name);
                names.Reverse();
                return names.ToArray();
            }

            /// <summary>只允许项目自有 Assets 区域，拒绝框架、插件、第三方、派生目录和路径跳转。</summary>
            private static string OwnedAbsolute(string path)
            {
                if (string.IsNullOrEmpty(path) || !path.StartsWith("Assets/", StringComparison.Ordinal) || path != path.Trim() ||
                    path.IndexOf('\\') >= 0 || path.IndexOf('\0') >= 0 || path.Split('/').Any(segment => string.IsNullOrEmpty(segment) ||
                        new[] { ".", "..", "Framework", "Plugins", "ThirdParty", "Third-Party", "Third Party", "Vendor", "Vendors", "Generated", "GeneratedCode", "_configs" }
                            .Contains(segment, StringComparer.OrdinalIgnoreCase)))
                    throw new InvalidOperationException("仅支持项目 Assets 内自有资产或源码，框架和第三方保持原样。 ");
                string absolute = Absolute(path);
                for (string current = absolute; current != null && current != ProjectRoot(); current = Path.GetDirectoryName(current))
                    if ((File.Exists(current) || Directory.Exists(current)) && (File.GetAttributes(current) & FileAttributes.ReparsePoint) != 0)
                        throw new InvalidOperationException("不通过符号链接修改资产或源码。 ");
                return absolute;
            }

            /// <summary>将已校验项目路径转换为当前工程的绝对路径。</summary>
            private static string Absolute(string path) => Path.GetFullPath(Path.Combine(ProjectRoot(), path));

            /// <summary>从 Unity 当前工程取得根目录，不依赖源码所在包的物理层级。</summary>
            private static string ProjectRoot() => Path.GetDirectoryName(Application.dataPath);

            /// <summary>读取完整文件字节摘要；缺失文件以 null 区分，不创建文件或目录。</summary>
            private static string FileHash(string path)
            {
                if (!File.Exists(path)) return null;
                using (var sha = SHA256.Create())
                using (FileStream stream = File.OpenRead(path)) return BytesHex(sha.ComputeHash(stream));
            }

            /// <summary>使用无 BOM UTF8 的精确内容摘要，生成和无变化判定采用同一编码。</summary>
            private static string HashText(string text)
            {
                using (var sha = SHA256.Create()) return BytesHex(sha.ComputeHash(new UTF8Encoding(false).GetBytes(text)));
            }

            /// <summary>把摘要字节转换为稳定小写十六进制文本。</summary>
            private static string BytesHex(byte[] bytes) => BitConverter.ToString(bytes).Replace("-", string.Empty).ToLowerInvariant();

            /// <summary>Unity 原生序列化身份字段不作为业务字段生成或绑定，避免覆盖引擎自己的同名属性。</summary>
            private static bool SystemField(string name) => new[] { "m_ObjectHideFlags", "m_CorrespondingSourceObject", "m_PrefabInstance", "m_PrefabAsset",
                "m_GameObject", "m_EditorHideFlags", "m_Script", "m_Name", "m_EditorClassIdentifier", "m_Enabled" }.Contains(name, StringComparer.Ordinal);

            /// <summary>仅接受明确的普通 C# 字段标识符，关键字和需转义的标识符不进入自动生成。</summary>
            private static bool ValidIdentifier(string value) => value != null && Regex.IsMatch(value, "^[A-Za-z_][A-Za-z0-9_]*$") &&
                !new[] { "abstract", "as", "base", "bool", "break", "byte", "case", "catch", "char", "checked", "class", "const", "continue", "decimal", "default", "delegate", "do", "double", "else", "enum", "event", "explicit", "extern", "false", "finally", "fixed", "float", "for", "foreach", "goto", "if", "implicit", "in", "int", "interface", "internal", "is", "lock", "long", "namespace", "new", "null", "object", "operator", "out", "override", "params", "private", "protected", "public", "readonly", "ref", "return", "sbyte", "sealed", "short", "sizeof", "stackalloc", "static", "string", "struct", "switch", "this", "throw", "true", "try", "typeof", "uint", "ulong", "unchecked", "unsafe", "ushort", "using", "virtual", "void", "volatile", "while", "partial", "record", "required", "file" }.Contains(value, StringComparer.Ordinal);
        }
    }
}
