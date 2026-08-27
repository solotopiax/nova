/***************************************************************
 * (c) copyright 2026 - 2030, Solotopia
 * All Rights Reserved.
 * -------------------------------------------------------------
 * filename:  EditorUtil.Pipify.Methods.cs
 * author:    taoye
 * created:   2026/5/10
 * descrip:   Pipify 私有工具方法（ApplyOverridesForItem / ConvertOverrideValue）
 ***************************************************************/

using System;
using System.Collections.Generic;
using System.Reflection;
using NovaFramework.Runtime;

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
                return paramsInstance;
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
