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

namespace NovaFramework.Editor
{
    public static partial class EditorUtil
    {
        public static partial class Pipify
        {
            /// <summary>
            /// 将 overrides 字典中匹配当前 (stepId, itemIndex) 的键值写回 paramsInstance。
            /// 支持 key 形如 "stepId.字段名"（适配所有索引）或 "stepId[索引].字段名"（仅适配该索引）。
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
