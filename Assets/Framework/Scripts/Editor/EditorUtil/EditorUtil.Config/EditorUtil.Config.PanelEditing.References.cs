/***************************************************************
 * (c) copyright 2026 - 2030, Solotopia
 * All Rights Reserved.
 * -------------------------------------------------------------
 * filename:  EditorUtil.Config.PanelEditing.References.cs
 * author:    taoye
 * created:   2026/10/13
 * descrip:   已登记内嵌配置类型的受控补实例预演
 ***************************************************************/

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Threading;
using Newtonsoft.Json.Linq;
using NovaFramework.Runtime;
using UnityEditor;
using UnityEditor.Compilation;
using PackageInfo = UnityEditor.PackageManager.PackageInfo;
using UnityEngine;
using Coord = NovaFramework.Editor.EditorUtil.Config.DimensionProjector.Coord;
using Path = System.IO.Path;

namespace NovaFramework.Editor
{
    public static partial class EditorUtil
    {
        public static partial class Config
        {
            internal static partial class PanelEditing
            {
                internal const string IapConfigType = "NovaFramework.SDK.IAP.Runtime.IAPPluginConfig";
                internal const string IapStoreCollection = "m_StoreConfigList.m_Items";
                private const string IapStoreContract = "NovaFramework.SDK.IAP.Runtime.IIAPStoreConfig";

                /// <summary>固定领域登记不接受任意类型、程序集或源路径；新增领域须经源码审查后登记。</summary>
                private sealed class ReferenceRegistration
                {
                    internal readonly string FullName;
                    internal readonly string PackageId;
                    internal readonly string AssemblyName;

                    /// <summary>类型所属包和程序集由内置登记给出，不从请求拼接。</summary>
                    internal ReferenceRegistration(string fullName, string packageId, string assemblyName)
                    { FullName = fullName; PackageId = packageId; AssemblyName = assemblyName; }
                }

                private static readonly ReferenceRegistration[] s_IapReferenceTypes =
                {
                    new ReferenceRegistration("NovaFramework.SDK.IAP.Mobile.Runtime.MobileStoreConfig", "com.solotopia.nova.framework.sdk.iap.mobile", "NovaFramework.SDK.IAP.Mobile.Runtime"),
                    new ReferenceRegistration("NovaFramework.SDK.IAP.ThirdPay.Runtime.ThirdPayStoreConfig", "com.solotopia.nova.framework.sdk.iap.thirdpay", "NovaFramework.SDK.IAP.ThirdPay.Runtime"),
                    new ReferenceRegistration("NovaFramework.SDK.IAP.Voucher.Runtime.VoucherStoreConfig", "com.solotopia.nova.framework.sdk.iap.voucher", "NovaFramework.SDK.IAP.Voucher.Runtime"),
                };

                /// <summary>只记录包内源码身份和整文件摘要，不读取或持久化用户配置值。</summary>
                [Serializable]
                internal sealed class ReferenceSource
                {
                    public string packageId;
                    public string path;
                    public string sha256;
                }

                /// <summary>MVID 仅约束提交前同次程序集；Verify 使用稳定程序集身份与源码摘要跨重载核验。</summary>
                [Serializable]
                internal sealed class ReferenceTypeIdentity
                {
                    public string typeFullName;
                    public string assemblyIdentity;
                    public string moduleVersionId;
                    public string packageId;
                    public string packageVersion;
                    public ReferenceSource[] sources;
                }

                /// <summary>实际补项只公开坐标、列表地址及已登记类型，不公开初始化字段值。</summary>
                [Serializable]
                internal sealed class ReferenceAddition
                {
                    public string path;
                    public string typeFullName;
                    public string platform;
                    public string channel;
                    public string developMode;
                }

                /// <summary>引用补齐预演沿用完整 Master 副本与保存坐标，增量另列准确创建地址。</summary>
                internal sealed class ReferencePreview : IDisposable
                {
                    internal Preview PanelPreview;
                    internal ReferenceAddition[] Additions;

                    /// <summary>只销毁预演副本，不改变正式 Master。</summary>
                    public void Dispose() => PanelPreview?.Dispose();
                }

                /// <summary>首版只允许 IAP 的既有 Store 列表和三种已登记具体类型，空列表不能表达删除。</summary>
                internal static void ValidateReferenceRequest(Target target, string collectionPath, string[] typeFullNames)
                {
                    ValidateTarget(target);
                    if (target.panel != "SDK" || target.configType != IapConfigType || collectionPath != IapStoreCollection ||
                        typeFullNames == null || typeFullNames.Length == 0 || typeFullNames.Length > s_IapReferenceTypes.Length ||
                        typeFullNames.Any(name => !s_IapReferenceTypes.Any(item => item.FullName == name)) ||
                        typeFullNames.Distinct(StringComparer.Ordinal).Count() != typeFullNames.Length)
                        throw new InvalidOperationException("仅允许已登记 IAP Store 列表及明确、无重复的缺失类型。");
                }

                /// <summary>解析当前已安装领域类型；包、程序集、字段及完整 Runtime 源码共同形成只读冻结。</summary>
                internal static ReferenceTypeIdentity[] CaptureReferenceTypes(string[] requested, CancellationToken token)
                {
                    var parent = new ReferenceRegistration(IapConfigType, "com.solotopia.nova.framework.sdk.iap", "NovaFramework.SDK.IAP.Runtime");
                    ReferenceRegistration[] registrations = new[] { parent }.Concat(s_IapReferenceTypes.Where(item => requested.Contains(item.FullName, StringComparer.Ordinal) ||
                        AppDomain.CurrentDomain.GetAssemblies().Any(assembly => assembly.GetName().Name == item.AssemblyName && assembly.GetType(item.FullName, false, false) != null)))
                        .OrderBy(item => item.FullName, StringComparer.Ordinal).ToArray();
                    var identities = new List<ReferenceTypeIdentity>();
                    Type contract = ResolveRegisteredType(parent).Assembly.GetType(IapStoreContract, true, false);
                    foreach (ReferenceRegistration registration in registrations)
                    {
                        token.ThrowIfCancellationRequested();
                        Type type = ResolveRegisteredType(registration);
                        if (registration != parent && (!contract.IsAssignableFrom(type) || !type.IsSealed || !IsLeafStoreShape(type)))
                            throw new InvalidOperationException("已登记 Store 类型不再满足叶子配置契约。");
                        PackageInfo package = PackageInfo.FindForAssetPath("Packages/" + registration.PackageId + "/package.json");
                        if (package == null || package.name != registration.PackageId || string.IsNullOrEmpty(package.resolvedPath))
                            throw new InvalidOperationException("Store 所属包尚未解析，不能补实例。");
                        string prefix = "Packages/" + registration.PackageId + "/";
                        var assembly = CompilationPipeline.GetAssemblies(AssembliesType.Editor).SingleOrDefault(item => item.name == registration.AssemblyName);
                        if (assembly == null) throw new InvalidOperationException("已登记程序集没有当前编译来源。");
                        string definition = CompilationPipeline.GetAssemblyDefinitionFilePathFromAssemblyName(registration.AssemblyName);
                        string[] paths = assembly.sourceFiles.Select(path => NormalizePackageSource(path, package)).Concat(new[]
                            { NormalizePackageSource(definition, package), prefix + "package.json" }).Distinct(StringComparer.Ordinal).OrderBy(path => path, StringComparer.Ordinal).ToArray();
                        if (paths.Length < 3) throw new InvalidOperationException("已登记类型缺少完整来源。");
                        identities.Add(new ReferenceTypeIdentity
                        {
                            typeFullName = type.FullName, assemblyIdentity = type.Assembly.FullName,
                            moduleVersionId = type.Module.ModuleVersionId.ToString("D"), packageId = package.name, packageVersion = package.version,
                            sources = paths.Select(path => new ReferenceSource { packageId = package.name, path = path,
                                sha256 = GenerateActionCommon.ComputeFileHash(ResolvePackageSource(path, package), token) }).ToArray(),
                        });
                    }
                    return identities.ToArray();
                }

                /// <summary>包来源与程序集形状必须仍等于原计划；跨重载只忽略 MVID，不忽略源文件或版本变化。</summary>
                internal static bool ReferenceTypesMatch(ReferenceTypeIdentity[] expected, string[] requested, bool requireMvid, CancellationToken token)
                {
                    ReferenceTypeIdentity[] current = CaptureReferenceTypes(requested, token);
                    if (expected == null || expected.Length != current.Length) return false;
                    for (int i = 0; i < current.Length; i++)
                    {
                        ReferenceTypeIdentity a = expected[i], b = current[i];
                        if (a == null || a.typeFullName != b.typeFullName || a.assemblyIdentity != b.assemblyIdentity ||
                            a.packageId != b.packageId || a.packageVersion != b.packageVersion || (requireMvid && a.moduleVersionId != b.moduleVersionId) ||
                            Util.Json.Serialize(a.sources) != Util.Json.Serialize(b.sources)) return false;
                    }
                    return true;
                }

                /// <summary>只在独立副本按真实掩码逐格追加独立实例；既有列表顺序、原值和范围外字段不得被归一覆盖。</summary>
                internal static ReferencePreview BuildReferencePreview(ConfigMasterSO master, Target target, string collectionPath, string[] requested)
                {
                    ValidateReferenceRequest(target, collectionPath, requested);
                    var result = new ReferencePreview { PanelPreview = new Preview { SaveCoordinate = Coordinate(target) } };
                    try
                    {
                        Preview preview = result.PanelPreview;
                        string before = Snapshot(master);
                        preview.BeforeHash = StateHash(master);
                        preview.WorkingCopy = UnityEngine.Object.Instantiate(master);
                        preview.WorkingCopy.hideFlags = HideFlags.DontSave;
                        ConfigMasterSO clone = preview.WorkingCopy;
                        if (!SchemaGuard.Validate(clone, out _)) throw new InvalidOperationException("ConfigMaster 结构版本不受支持。");
                        var additions = new List<ReferenceAddition>();
                        var allowed = new HashSet<string>(StringComparer.Ordinal);
                        using (var so = new SerializedObject(clone))
                        {
                            DesignTimeEditing.NormalizeWorkingCopy(clone, so, preview.SaveCoordinate);
                            if (Snapshot(clone) != before) throw new InvalidOperationException("需要先显式整理 Master；补引用不能改变其他存量结构。");
                            PanelDimensionMask mask = Mask(clone, EditablePanel.SDK, target.configType);
                            Coord[] members = Coordinates(clone).Where(member => SameGroup(mask, preview.SaveCoordinate, member)).ToArray();
                            if (members.Length == 0) throw new InvalidOperationException("目标逻辑组不存在。");
                            foreach (Coord member in members)
                            {
                                Target physical = CopyTarget(target, member);
                                SerializedProperty root = Locate(clone, so, physical);
                                if (root.managedReferenceValue?.GetType() != ResolveRegisteredType(new ReferenceRegistration(IapConfigType,
                                    "com.solotopia.nova.framework.sdk.iap", "NovaFramework.SDK.IAP.Runtime")))
                                    throw new InvalidOperationException("目标配置不是已安装的 IAPPluginConfig。");
                                SerializedProperty list = root.FindPropertyRelative(collectionPath);
                                if (list == null || !list.isArray || list.propertyType != SerializedPropertyType.Generic || list.arraySize > 1024)
                                    throw new InvalidOperationException("真实 Store 列表字段缺失或形状不受支持。");
                                object[] oldItems = ReferenceItems(list);
                                string[] oldSignatures = ReferenceSignatures(oldItems);
                                foreach (string name in requested)
                                {
                                    Type type = ResolveRegisteredType(s_IapReferenceTypes.Single(item => item.FullName == name));
                                    if (oldItems.Any(item => item.GetType() == type)) continue;
                                    if (list.arraySize >= 1024) throw new InvalidOperationException("Store 列表已达到受控上限。");
                                    int index = list.arraySize;
                                    list.arraySize++;
                                    SerializedProperty element = list.GetArrayElementAtIndex(index);
                                    if (element.propertyType != SerializedPropertyType.ManagedReference)
                                        throw new InvalidOperationException("Store 元素不是真实 SerializeReference。");
                                    // 每个物理格构造独立的已登记叶子，不复用引用也不广播整份含嵌套多态的 IAPConfig。
                                    element.managedReferenceValue = Activator.CreateInstance(type);
                                    additions.Add(new ReferenceAddition { path = element.propertyPath, typeFullName = name,
                                        platform = member.Platform.ToString(), channel = member.Channel.ToString(), developMode = member.Mode.ToString() });
                                }
                                allowed.Add(list.propertyPath);
                                so.ApplyModifiedPropertiesWithoutUndo();
                                so.Update();
                                SerializedProperty savedList = Locate(clone, so, physical).FindPropertyRelative(collectionPath);
                                AssertReferencePrefix(oldSignatures, ReferenceSignatures(ReferenceItems(savedList)));
                            }
                            string permitted = Snapshot(clone);
                            AssertFieldScope(JObject.Parse(before), JObject.Parse(permitted), allowed);
                            DesignTimeEditing.NormalizeWorkingCopy(clone, so, preview.SaveCoordinate);
                            if (Snapshot(clone) != permitted)
                                throw new InvalidOperationException("保存归一不能保留内嵌 Store 或引入请求外变化，已拒绝。");
                            AssertRetainedResourceKeys(master.ResourceEncryption, clone.ResourceEncryption);
                        }
                        preview.ExpectedHash = StateHash(clone);
                        preview.Differences = Differences(JObject.Parse(before), JObject.Parse(Snapshot(clone))).ToArray();
                        result.Additions = additions.ToArray();
                        return result;
                    }
                    catch { result.Dispose(); throw; }
                }

                /// <summary>读取既有元素时拒绝 Missing/null、重复具体类型或重复 StoreType；不能隐式清理历史配置。</summary>
                private static object[] ReferenceItems(SerializedProperty list)
                {
                    var items = new List<object>();
                    var stores = new HashSet<string>(StringComparer.Ordinal);
                    for (int i = 0; i < list.arraySize; i++)
                    {
                        SerializedProperty item = list.GetArrayElementAtIndex(i);
                        object value = item.propertyType == SerializedPropertyType.ManagedReference ? item.managedReferenceValue : null;
                        ReferenceRegistration registration = value == null ? null : s_IapReferenceTypes.SingleOrDefault(entry => entry.FullName == value.GetType().FullName);
                        if (registration == null || value.GetType() != ResolveRegisteredType(registration) ||
                            items.Any(existing => existing.GetType() == value.GetType()))
                            throw new InvalidOperationException("既有 Store 存在 Missing、未登记或重复类型，不能安全补齐。");
                        string store = value.GetType().GetProperty("StoreType").GetValue(value)?.ToString();
                        if (string.IsNullOrEmpty(store) || store == "None" || !stores.Add(store))
                            throw new InvalidOperationException("既有 Store 路由键重复或无效。");
                        items.Add(value);
                    }
                    return items.ToArray();
                }

                /// <summary>追加只允许尾部增长，逐项核对原类型和全部字段，不以列表长度代替存量保留。</summary>
                private static void AssertReferencePrefix(string[] before, string[] after)
                {
                    if (after.Length < before.Length) throw new InvalidOperationException("Store 补齐不能删除旧项。");
                    for (int i = 0; i < before.Length; i++)
                        if (before[i] != after[i])
                            throw new InvalidOperationException("Store 补齐改变了旧项或列表顺序。");
                }

                /// <summary>旧字段签名立即冻结为字符串，不能因 Unity 重用同一实例掩盖其后原值变化。</summary>
                private static string[] ReferenceSignatures(object[] items) => items.Select(item => item.GetType().AssemblyQualifiedName + "|" + JsonUtility.ToJson(item)).ToArray();

                /// <summary>只解析精确已登记名称和程序集，不使用用户提供程序集加载或通用反射类型执行。</summary>
                private static Type ResolveRegisteredType(ReferenceRegistration registration)
                {
                    Type[] matches = AppDomain.CurrentDomain.GetAssemblies().Where(assembly => assembly.GetName().Name == registration.AssemblyName)
                        .Select(assembly => assembly.GetType(registration.FullName, false, false)).Where(type => type != null).ToArray();
                    if (matches.Length != 1 || matches[0].IsAbstract || matches[0].IsInterface || !matches[0].IsSerializable ||
                        matches[0].GetConstructor(Type.EmptyTypes) == null || typeof(UnityEngine.Object).IsAssignableFrom(matches[0]))
                        throw new InvalidOperationException("已安装类型身份缺失、歧义或不满足配置构造契约。");
                    return matches[0];
                }

                /// <summary>首版 Store 只允许真实已登记的标量/枚举及其一维数组或 List，不接受藏在包装内的新多态或回调结构。</summary>
                private static bool IsLeafStoreShape(Type type)
                {
                    if (typeof(ISerializationCallbackReceiver).IsAssignableFrom(type)) return false;
                    foreach (FieldInfo field in type.GetFields(BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public))
                    {
                        if (field.IsStatic || field.IsNotSerialized || (!field.IsPublic && !field.IsDefined(typeof(SerializeField), false) && !field.IsDefined(typeof(SerializeReference), false))) continue;
                        if (field.IsDefined(typeof(SerializeReference), false)) return false;
                        Type value = field.FieldType;
                        if (value.IsArray)
                        {
                            if (value.GetArrayRank() != 1) return false;
                            value = value.GetElementType();
                        }
                        else if (value.IsGenericType && value.GetGenericTypeDefinition() == typeof(List<>)) value = value.GetGenericArguments()[0];
                        if (!(value.IsPrimitive || value.IsEnum || value == typeof(string))) return false;
                    }
                    return true;
                }

                /// <summary>将 Unity 编译来源映射为固定包路径，绝对来源只允许当前解析包目录内文件。</summary>
                private static string NormalizePackageSource(string path, PackageInfo package)
                {
                    if (string.IsNullOrEmpty(path)) throw new InvalidOperationException("源码路径缺失。");
                    string prefix = "Packages/" + package.name + "/";
                    string normalized = path.Replace('\\', '/');
                    if (normalized.StartsWith(prefix, StringComparison.Ordinal))
                    { ResolvePackageSource(normalized, package); return normalized; }
                    string root = Path.GetFullPath(package.resolvedPath).TrimEnd(Path.DirectorySeparatorChar) + Path.DirectorySeparatorChar;
                    string absolute = Path.GetFullPath(Path.IsPathRooted(path) ? path : Path.Combine(GenerateActionCommon.ProjectRoot, path));
                    if (!absolute.StartsWith(root, StringComparison.Ordinal)) throw new InvalidOperationException("类型源码不属于其已解析包。");
                    string result = prefix + absolute.Substring(root.Length).Replace('\\', '/');
                    ResolvePackageSource(result, package);
                    return result;
                }

                /// <summary>只读固定包内真实文件；禁止目录越界和路径段符号链接，摘要不追踪外部位置。</summary>
                private static string ResolvePackageSource(string path, PackageInfo package)
                {
                    string prefix = "Packages/" + package.name + "/";
                    if (!path.StartsWith(prefix, StringComparison.Ordinal)) throw new InvalidOperationException("包源路径不一致。");
                    string relative = path.Substring(prefix.Length);
                    if (relative.Split('/').Any(part => part.Length == 0 || part == "." || part == "..")) throw new InvalidOperationException("包源路径不规范。");
                    string root = Path.GetFullPath(package.resolvedPath).TrimEnd(Path.DirectorySeparatorChar);
                    for (DirectoryInfo directory = new DirectoryInfo(root); directory != null; directory = directory.Parent)
                        if ((directory.Attributes & FileAttributes.ReparsePoint) != 0) throw new InvalidOperationException("包根或祖先不能包含符号链接。");
                    string cursor = root;
                    foreach (string part in relative.Split('/'))
                    {
                        cursor = Path.Combine(cursor, part);
                        if ((File.GetAttributes(cursor) & FileAttributes.ReparsePoint) != 0) throw new InvalidOperationException("包源路径不能包含符号链接。");
                    }
                    if (!File.Exists(cursor)) throw new InvalidOperationException("包源文件不存在。");
                    return cursor;
                }
            }
        }
    }
}
