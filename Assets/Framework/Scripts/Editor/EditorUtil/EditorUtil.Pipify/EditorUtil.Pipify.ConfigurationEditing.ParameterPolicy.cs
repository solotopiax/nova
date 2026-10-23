/***************************************************************
 * (c) copyright 2026 - 2030, Solotopia
 * All Rights Reserved.
 * -------------------------------------------------------------
 * filename:  EditorUtil.Pipify.ConfigurationEditing.ParameterPolicy.cs
 * author:    Codex
 * created:   2026/10/13
 * descrip:   Pipify 窗口与受控配置共用的参数规则
 ***************************************************************/

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text.RegularExpressions;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using NovaFramework.Runtime;
using UnityEditor;

namespace NovaFramework.Editor
{
    public static partial class EditorUtil
    {
        public static partial class Pipify
        {
            internal static partial class ConfigurationEditing
            {
                /// <summary>与窗口一致的基础字段能力；列表、对象和属性不作为字段写入入口。</summary>
                internal static bool SupportsField(FieldInfo field)
                {
                    Type type = field.FieldType;
                    var json = field.GetCustomAttribute<JsonPropertyAttribute>();
                    return !field.IsStatic && !field.IsInitOnly && !field.IsLiteral &&
                        !field.IsNotSerialized && field.GetCustomAttribute<JsonIgnoreAttribute>() == null &&
                        field.GetCustomAttribute<JsonConverterAttribute>() == null &&
                        (json == null || string.IsNullOrEmpty(json.PropertyName) || json.PropertyName == field.Name) &&
                        (type == typeof(string) || type == typeof(bool) || type == typeof(int) || type == typeof(float) || type.IsEnum);
                }

                /// <summary>只编辑默认字段序列化契约；自定义转换器或 opt-in 模型需要专属领域封装。</summary>
                private static bool SupportsParamsFields(Type type)
                {
                    var json = type.GetCustomAttribute<JsonObjectAttribute>();
                    return type.GetCustomAttribute<JsonConverterAttribute>() == null &&
                        type.GetCustomAttribute<System.Runtime.Serialization.DataContractAttribute>() == null &&
                        (json == null || json.MemberSerialization == MemberSerialization.OptOut);
                }

                /// <summary>读取真实公开字段特性的传递显隐规则，保留窗口原有防环语义。</summary>
                internal static bool IsFieldVisible(FieldInfo field, object instance, Type type)
                {
                    return IsVisible(field, type, name => type.GetField(name, BindingFlags.Public | BindingFlags.Instance)?.GetValue(instance), new HashSet<string>());
                }

                /// <summary>共用显隐算法；字段值来源可为窗口实例或不执行构造器的保存快照。</summary>
                private static bool IsVisible(FieldInfo field, Type type, Func<string, object> read, HashSet<string> visited)
                {
                    var attr = field.GetCustomAttribute<PipifyVisibleWhenAttribute>();
                    if (attr == null) return true;
                    FieldInfo dependency = type.GetField(attr.DependsOn, BindingFlags.Public | BindingFlags.Instance);
                    if (dependency == null || !visited.Add(dependency.Name)) return true;
                    if (!IsVisible(dependency, type, read, visited)) return false;
                    object value = read(dependency.Name);
                    if (value == null) return false;
                    try { return attr.AnyOf.Contains(Convert.ToInt32(value)); }
                    catch { return true; }
                }

                /// <summary>字段特性、Google Play 审核状态与 CDN 自动关联派生字段均不可写。</summary>
                internal static bool IsReadOnly(FieldInfo field, object instance)
                {
                    if (field.IsInitOnly || field.IsLiteral || field.GetCustomAttribute<PipifyReadOnlyAttribute>() != null) return true;
                    if (instance is PipifySteps.GooglePlayUploadParams upload &&
                        field.Name == nameof(PipifySteps.GooglePlayUploadParams.ChangesNotSentForReview) &&
                        !PipifySteps.IsGooglePlayManualReviewAvailable(upload)) return true;
                    if (instance is PipifySteps.CdnDeployParams cdn && cdn.AutoLinkLatestVersion &&
                        field.Name == nameof(PipifySteps.CdnDeployParams.LocalDirectory)) return true;
                    return instance is PipifySteps.CdnWhitelistDeployParams whitelist && whitelist.AutoLinkLatestVersion &&
                        (field.Name == nameof(PipifySteps.CdnWhitelistDeployParams.ManifestBytesLocalFilePath) ||
                         field.Name == nameof(PipifySteps.CdnWhitelistDeployParams.ManifestHashLocalFilePath) ||
                         field.Name == nameof(PipifySteps.CdnWhitelistDeployParams.PackageVersionLocalFilePath));
                }

                /// <summary>Config 渠道与 WebGL 首包模式的候选过滤由窗口和 Action 共用。</summary>
                internal static List<Enum> GetEnumValues(FieldInfo field, object instance)
                {
                    bool config = instance is PipifySteps.ConfigExportParams && field.Name == nameof(PipifySteps.ConfigExportParams.Channel);
                    bool web = field.Name == nameof(AssetBundleBuildArgs.BundledCopyOption) &&
                        (instance is AssetBundleBuildArgs asset && asset.Target == BuildTarget.WebGL ||
                         instance is RawFileBuildArgs raw && raw.Target == BuildTarget.WebGL);
                    return Enum.GetValues(field.FieldType).Cast<Enum>().Where(value =>
                        (!config || !value.Equals(ChannelType.None)) &&
                        (!web || BundleBuilder.IsWebGLBundledCopyOptionSupported((YooAsset.Editor.EBundledCopyOption)value))).ToList();
                }

                /// <summary>仅查询接口实现元数据，不实例化选项类型；与资源构建窗口使用同一候选。</summary>
                internal static string[] GetDropdownOptions(Type contract)
                {
                    bool build = contract == typeof(YooAsset.IBundleEncryptor) || contract == typeof(YooAsset.IManifestEncryptor) || contract == typeof(YooAsset.IManifestDecryptor);
                    return build ? YooAsset.Editor.EditorAssemblyUtility.GetBuildServiceTypes(contract).Select(type => type.FullName).ToArray() : EditorUtil.TypeCache.GetTypeNames(contract);
                }

                /// <summary>自动化只执行源码固定的包名提供者；请求不可指定提供者或方法。</summary>
                internal static string[] ResolveDynamicDropdown(PipifyDynamicDropdownAttribute attr)
                {
                    if (attr?.ProviderType != typeof(AssetBundleBuildArgs) || attr.MethodName != nameof(AssetBundleBuildArgs.GetPackageNameOptions)) return null;
                    return AssetBundleBuildArgs.GetPackageNameOptions();
                }

                /// <summary>动态版本只作占位提示，空值不会被写成执行时刻的版本号。</summary>
                internal static string ResolveDynamicDefault(PipifyDynamicDefaultAttribute attr)
                {
                    return attr?.ProviderType == typeof(BundleBuilder) && attr.MethodName == nameof(BundleBuilder.GetDefaultPackageVersion)
                        ? BundleBuilder.GetDefaultPackageVersion() : string.Empty;
                }

                /// <summary>窗口仍允许既有 Package 特性提供者；Action 对未知提供者只返回未支持状态。</summary>
                internal static string[] ResolveWindowDynamicDropdown(PipifyDynamicDropdownAttribute attr)
                {
                    if (attr?.ProviderType == typeof(AssetBundleBuildArgs)) return ResolveDynamicDropdown(attr);
                    try { return attr?.ProviderType.GetMethod(attr.MethodName, BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Static, null, Type.EmptyTypes, null)?.Invoke(null, null) as string[]; }
                    catch { return null; }
                }

                /// <summary>保持旧窗口自定义动态占位能力，自动化不开放此反射入口。</summary>
                internal static string ResolveWindowDynamicDefault(PipifyDynamicDefaultAttribute attr)
                {
                    if (attr?.ProviderType == typeof(BundleBuilder)) return ResolveDynamicDefault(attr);
                    try { return attr?.ProviderType.GetMethod(attr.MethodName, BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Static, null, Type.EmptyTypes, null)?.Invoke(null, null) as string ?? string.Empty; }
                    catch { return string.Empty; }
                }

                /// <summary>严格读取唯一基础 JSON token；拒绝注释、额外内容、对象、数组和类型元数据。</summary>
                internal static JToken ReadValue(string json)
                {
                    if (json == null || json.Length > 16384) throw new InvalidOperationException("参数值 JSON 缺失或过长。");
                    string trimmed = json.Trim();
                    const string stringPattern = "^\"(?:[^\"\\\\\\x00-\\x1F]|\\\\(?:[\"\\\\/bfnrt]|u[0-9a-fA-F]{4}))*\"$";
                    const string numberPattern = "^-?(?:0|[1-9][0-9]*)(?:\\.[0-9]+)?(?:[eE][+-]?[0-9]+)?$";
                    if (trimmed != "true" && trimmed != "false" && !Regex.IsMatch(trimmed, stringPattern, RegexOptions.CultureInvariant) && !Regex.IsMatch(trimmed, numberPattern, RegexOptions.CultureInvariant))
                        throw new InvalidOperationException("参数值必须使用标准 JSON token 语法。");
                    using var reader = new JsonTextReader(new StringReader(trimmed)) { DateParseHandling = DateParseHandling.None, FloatParseHandling = FloatParseHandling.Decimal, MaxDepth = 8 };
                    if (!reader.Read() || (reader.TokenType != JsonToken.String && reader.TokenType != JsonToken.Boolean && reader.TokenType != JsonToken.Integer && reader.TokenType != JsonToken.Float))
                        throw new InvalidOperationException("参数值必须为字符串、布尔或数字 JSON token。");
                    JToken value = new JValue(reader.Value);
                    if (reader.Read()) throw new InvalidOperationException("参数值 JSON 含额外内容。");
                    return value;
                }

                /// <summary>按字段真实类型转换，拒绝字符串数字、整数布尔和未定义枚举。</summary>
                private static object ConvertValue(FieldInfo field, JToken value)
                {
                    Type type = field.FieldType;
                    if (type == typeof(string) && value.Type == JTokenType.String) return value.Value<string>();
                    if (type == typeof(bool) && value.Type == JTokenType.Boolean) return value.Value<bool>();
                    if (type == typeof(int) && value.Type == JTokenType.Integer) return checked(value.Value<int>());
                    if (type == typeof(float) && (value.Type == JTokenType.Float || value.Type == JTokenType.Integer))
                    {
                        float result = value.Value<float>();
                        if (!float.IsNaN(result) && !float.IsInfinity(result)) return result;
                    }
                    if (type.IsEnum && value.Type == JTokenType.String)
                    {
                        string name = value.Value<string>();
                        if (Enum.GetNames(type).Contains(name, StringComparer.Ordinal)) return Enum.Parse(type, name, false);
                    }
                    throw new InvalidOperationException("参数 JSON token 与真实字段类型不匹配。");
                }

                /// <summary>判断首次进入 Android DevelopmentBuild AAB 上传签名风险，与窗口共用。</summary>
                internal static bool ShouldConfirmAndroidRisk(PipifySteps.PackageParams before, PipifySteps.PackageParams after, bool exportProject)
                {
                    return !HasAndroidRisk(before, exportProject) && HasAndroidRisk(after, exportProject);
                }

                /// <summary>只判断风险组合，不修改签名或执行构建。</summary>
                private static bool HasAndroidRisk(PipifySteps.PackageParams parameters, bool exportProject)
                {
                    return parameters != null && parameters.Target == BuildTarget.Android && parameters.DevelopmentBuild && parameters.BuildAppBundle && !exportProject;
                }
            }
        }
    }
}
