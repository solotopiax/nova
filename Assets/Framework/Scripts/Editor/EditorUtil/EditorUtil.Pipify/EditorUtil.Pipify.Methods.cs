/***************************************************************
 * (c) copyright 2026 - 2030, Solotopia
 * All Rights Reserved.
 * -------------------------------------------------------------
 * filename:  EditorUtil.Pipify.Methods.cs
 * author:    taoye
 * created:   2026/5/10
 * descrip:   Pipify 参数构建、覆盖、平台同步与运行前占位符解析
 ***************************************************************/

using System;
using System.Collections;
using System.Collections.Generic;
using System.Reflection;
using System.Runtime.CompilerServices;
using NovaFramework.Runtime;
using UnityEngine;

namespace NovaFramework.Editor
{
    public static partial class EditorUtil
    {
        public static partial class Pipify
        {
            /// <summary>
            /// 创建 Step 的默认参数实例；涉及平台的参数统一同步到 Unity 当前 Active BuildTarget。
            /// </summary>
            /// <param name="info">目标 Step 元信息。</param>
            /// <param name="configMaster">测试或迁移时显式提供的 ConfigMaster；为空时解析当前激活资产。</param>
            /// <returns>参数默认实例；无参 Step 返回 null。</returns>
            internal static object CreateDefaultParams(PipifyStepInfo info, ConfigMasterSO configMaster = null)
            {
                if (info?.ParamsType == null) return null;
                if (info.ParamsType == typeof(PipifySteps.ConfigExportParams))
                {
                    ConfigMasterSO master = configMaster ?? PipifySteps.Helpers.ResolveConfigMaster();
                    return PipifySteps.CreateConfigExportParams(master);
                }
                object parameters = Activator.CreateInstance(info.ParamsType);
                PipifySteps.SynchronizeActivePlatform(parameters);
                return parameters;
            }

            /// <summary>
            /// 为单次 Runner 调用解析参数；旧 Config 条目先固化并保存当前坐标，再应用仅本次生效的 CLI 覆盖。
            /// </summary>
            /// <param name="info">当前 Step 元信息。</param>
            /// <param name="itemIndex">当前条目索引。</param>
            /// <param name="item">当前条目。</param>
            /// <param name="settings">条目所属 PipifySettingsSO；无法定位时可为空。</param>
            /// <param name="overrides">本次执行的临时参数覆盖。</param>
            /// <param name="configMaster">测试时显式提供的 ConfigMaster；生产执行传空以解析当前激活资产。</param>
            /// <returns>已经应用本次覆盖的参数实例；无参 Step 返回 null。</returns>
            internal static object ResolveParamsForRun(
                PipifyStepInfo info,
                int itemIndex,
                BatchItem item,
                PipifySettingsSO settings,
                IReadOnlyDictionary<string, string> overrides,
                ConfigMasterSO configMaster = null)
            {
                return ResolveParamsForRun(
                    info,
                    itemIndex,
                    item,
                    settings,
                    overrides,
                    configMaster,
                    true);
            }

            /// <summary>
            /// 为执行或前瞻检查解析参数；只有即将调用 Step 的执行路径才展开字符串占位符。
            /// </summary>
            /// <param name="info">当前 Step 元信息。</param>
            /// <param name="itemIndex">当前条目索引。</param>
            /// <param name="item">当前条目。</param>
            /// <param name="settings">条目所属 PipifySettingsSO。</param>
            /// <param name="overrides">本次执行的临时参数覆盖。</param>
            /// <param name="configMaster">测试时显式提供的 ConfigMaster。</param>
            /// <param name="resolvePlaceholders">是否为本次真实调用展开字符串占位符。</param>
            /// <returns>完成指定运行期处理的参数实例。</returns>
            private static object ResolveParamsForRun(
                PipifyStepInfo info,
                int itemIndex,
                BatchItem item,
                PipifySettingsSO settings,
                IReadOnlyDictionary<string, string> overrides,
                ConfigMasterSO configMaster,
                bool resolvePlaceholders)
            {
                if (info?.ParamsType == null) return null;

                object paramsInstance;
                if (info.ParamsType == typeof(PipifySteps.ConfigExportParams))
                {
                    ConfigMasterSO master = configMaster ?? PipifySteps.Helpers.ResolveConfigMaster();
                    paramsInstance = PipifySteps.ResolveAndPersistConfigExportParams(
                        settings,
                        item,
                        master,
                        out _);
                }
                else
                {
                    paramsInstance = string.IsNullOrEmpty(item.ParamsJson)
                        ? CreateDefaultParams(info)
                        : Util.Json.Deserialize(item.ParamsJson, info.ParamsType);
                }

                ApplyOverridesForItem(info, itemIndex, paramsInstance, overrides);
                // 平台由 Unity Active BuildTarget 唯一决定；旧 JSON 或 CLI override 不能改变本次实际平台。
                PipifySteps.SynchronizeActivePlatform(paramsInstance);
                if (resolvePlaceholders) ResolveStringPlaceholdersForRun(paramsInstance, configMaster);
                return paramsInstance;
            }

            /// <summary>
            /// 在 Step 调用前统一解析本次参数快照中的标准文本占位符，不回写 ParamsJson 或配置资产。
            /// </summary>
            /// <param name="paramsInstance">已经完成反序列化、CLI 覆盖和平台同步的参数实例。</param>
            /// <param name="configMaster">测试或显式调用时指定的 ConfigMaster；为空时使用当前激活资产。</param>
            private static void ResolveStringPlaceholdersForRun(object paramsInstance, ConfigMasterSO configMaster)
            {
                if (paramsInstance == null || !ContainsStandardPlaceholder(
                        paramsInstance,
                        new HashSet<object>(ReferenceObjectComparer.Instance)))
                {
                    return;
                }

                ConfigMasterSO master = configMaster ?? PipifySteps.Helpers.ResolveConfigMaster();
                PlaceholderContext context = Placeholder.FromConfigMaster(
                    master,
                    Placeholder.ResolveDefaultPackageName(),
                    Application.version,
                    DateTime.Now);
                ResolveStringPlaceholders(
                    paramsInstance,
                    context,
                    new HashSet<object>(ReferenceObjectComparer.Instance));
            }

            /// <summary>
            /// 递归判断参数对象的公开实例字段和列表元素中是否包含标准占位符。
            /// </summary>
            /// <param name="value">待检查值。</param>
            /// <param name="visited">已访问的引用对象，防止异常参数模型形成循环。</param>
            /// <returns>任一字符串包含标准占位符时返回 true。</returns>
            private static bool ContainsStandardPlaceholder(object value, HashSet<object> visited)
            {
                if (value == null) return false;
                if (value is string text) return ContainsStandardPlaceholder(text);

                Type type = value.GetType();
                if (IsPlaceholderTraversalTerminal(type)) return false;
                if (!type.IsValueType && !visited.Add(value)) return false;

                if (value is IList list)
                {
                    for (int i = 0; i < list.Count; i++)
                    {
                        if (ContainsStandardPlaceholder(list[i], visited)) return true;
                    }
                    return false;
                }

                foreach (FieldInfo field in type.GetFields(BindingFlags.Public | BindingFlags.Instance))
                {
                    if (ContainsStandardPlaceholder(field.GetValue(value), visited)) return true;
                }
                return false;
            }

            /// <summary>
            /// 递归替换参数对象公开实例字段和列表元素中的标准占位符。
            /// </summary>
            /// <param name="value">待处理值。</param>
            /// <param name="context">本 Step 统一使用的占位符值快照。</param>
            /// <param name="visited">已访问的引用对象，防止异常参数模型形成循环。</param>
            /// <returns>替换后的值；值类型由调用方写回所属字段或列表。</returns>
            private static object ResolveStringPlaceholders(
                object value,
                PlaceholderContext context,
                HashSet<object> visited)
            {
                if (value == null) return null;
                if (value is string text) return Util.Placeholder.Resolve(text, context);

                Type type = value.GetType();
                if (IsPlaceholderTraversalTerminal(type)) return value;
                if (!type.IsValueType && !visited.Add(value)) return value;

                if (value is IList list)
                {
                    for (int i = 0; i < list.Count; i++)
                    {
                        list[i] = ResolveStringPlaceholders(list[i], context, visited);
                    }
                    return value;
                }

                foreach (FieldInfo field in type.GetFields(BindingFlags.Public | BindingFlags.Instance))
                {
                    object current = field.GetValue(value);
                    object resolved = ResolveStringPlaceholders(current, context, visited);
                    if (!field.IsInitOnly && !Equals(current, resolved)) field.SetValue(value, resolved);
                }
                return value;
            }

            /// <summary>
            /// 判断类型是否不应继续遍历其内部字段。
            /// </summary>
            private static bool IsPlaceholderTraversalTerminal(Type type)
            {
                return type.IsPrimitive || type.IsEnum || type == typeof(decimal) || type == typeof(DateTime) ||
                       type == typeof(Guid) || typeof(UnityEngine.Object).IsAssignableFrom(type);
            }

            /// <summary>
            /// 判断文本是否包含 Pipify 支持的任一标准占位符；未知占位符不会触发配置解析。
            /// </summary>
            private static bool ContainsStandardPlaceholder(string value)
            {
                if (string.IsNullOrEmpty(value)) return false;
                return value.IndexOf("{Platform}", StringComparison.Ordinal) >= 0 ||
                       value.IndexOf("{Channel}", StringComparison.Ordinal) >= 0 ||
                       value.IndexOf("{Package}", StringComparison.Ordinal) >= 0 ||
                       value.IndexOf("{Version}", StringComparison.Ordinal) >= 0 ||
                       value.IndexOf("{Time}", StringComparison.Ordinal) >= 0;
            }

            /// <summary>
            /// 为递归参数遍历提供引用相等比较，避免自定义 Equals 将不同参数节点误判为同一对象。
            /// </summary>
            private sealed class ReferenceObjectComparer : IEqualityComparer<object>
            {
                internal static readonly ReferenceObjectComparer Instance = new ReferenceObjectComparer();

                /// <summary>
                /// 判断两个对象是否为同一引用。
                /// </summary>
                public new bool Equals(object x, object y)
                {
                    return ReferenceEquals(x, y);
                }

                /// <summary>
                /// 返回不受对象自定义相等语义影响的引用哈希值。
                /// </summary>
                public int GetHashCode(object obj)
                {
                    return RuntimeHelpers.GetHashCode(obj);
                }
            }

            /// <summary>
            /// 将 overrides 字典中匹配当前 (stepId, itemIndex) 的可编辑字段写回 paramsInstance。
            /// 标记 PipifyReadOnly 的字段直接忽略；支持 key 形如 "stepId.字段名"（适配所有索引）或 "stepId[索引].字段名"（仅适配该索引）。
            /// </summary>
            /// <param name="info">Step 元信息。</param>
            /// <param name="itemIndex">当前 Item 在 Batch 中的索引。</param>
            /// <param name="paramsInstance">参数实例（null 代表无参 Step，直接返回）。</param>
            /// <param name="overrides">键值对字典；可为 null。</param>
            private static void ApplyOverridesForItem(PipifyStepInfo info, int itemIndex, object paramsInstance, IReadOnlyDictionary<string, string> overrides)
            {
                if (paramsInstance == null || overrides == null || overrides.Count == 0) return;
                string prefixPlain = info.Id + ".";
                string prefixIndexed = string.Format("{0}[{1}].", info.Id, itemIndex);
                foreach (KeyValuePair<string, string> kv in overrides)
                {
                    string fieldName;
                    if (kv.Key.StartsWith(prefixIndexed, StringComparison.Ordinal))
                    {
                        fieldName = kv.Key.Substring(prefixIndexed.Length);
                    }
                    else if (kv.Key.StartsWith(prefixPlain, StringComparison.Ordinal) && !kv.Key.Contains("["))
                    {
                        fieldName = kv.Key.Substring(prefixPlain.Length);
                    }
                    else
                    {
                        continue;
                    }
                    FieldInfo field = info.ParamsType.GetField(fieldName, BindingFlags.Public | BindingFlags.Instance);
                    if (field == null)
                    {
                        throw new InvalidOperationException(string.Format("{0} 覆盖失败：{1} 不含字段 {2}", c_LogPrefix, info.ParamsType.Name, fieldName));
                    }
                    // 运行环境提供的只读字段不接受 CLI 覆盖；跳过转换，旧脚本中的过期值也不会触发枚举解析错误。
                    if (field.GetCustomAttribute<PipifyReadOnlyAttribute>() != null) continue;
                    object converted = ConvertOverrideValue(kv.Value, field.FieldType);
                    field.SetValue(paramsInstance, converted);
                }
            }

            /// <summary>
            /// 将字符串值转换为目标字段类型（string / 数字 / bool / enum）。
            /// </summary>
            /// <param name="raw">原始字符串值。</param>
            /// <param name="targetType">目标字段类型。</param>
            /// <returns>转换后的对象。</returns>
            private static object ConvertOverrideValue(string raw, Type targetType)
            {
                if (targetType == typeof(string)) return raw;
                if (targetType.IsEnum) return Enum.Parse(targetType, raw, ignoreCase: false);
                return Convert.ChangeType(raw, targetType);
            }
        }
    }
}
