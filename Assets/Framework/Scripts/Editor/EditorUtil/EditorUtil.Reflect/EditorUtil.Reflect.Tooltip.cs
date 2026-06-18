/***************************************************************
 * (c) copyright 2026 - 2030, Solotopia
 * All Rights Reserved.
 * -------------------------------------------------------------
 * filename:  EditorUtil.Reflect.Tooltip.cs
 * author:    taoye
 * created:   2026/5/25
 * descrip:   编辑器反射工具——字段 TooltipAttribute 读取与缓存
 ***************************************************************/

using System;
using System.Collections.Generic;
using System.Reflection;
using UnityEngine;

namespace NovaFramework.Editor
{
    public static partial class EditorUtil
    {
        /// <summary>
        /// 反射工具。
        /// 提供通过反射读取字段 Attribute 的工具方法，结果按"类型全名.字段名"缓存，
        /// 相同字段只反射一次。缓存在 Domain Reload 后自动清空（C# 静态字段重置语义）。
        /// </summary>
        public static class Reflect
        {
            /// <summary>
            /// 字段 Tooltip 缓存。
            /// key 为"类型全名.字段名"，value 为 TooltipAttribute.tooltip；
            /// 字段无 TooltipAttribute 时缓存 null，避免重复反射。
            /// </summary>
            private static readonly Dictionary<string, string> s_TooltipCache = new Dictionary<string, string>();

            /// <summary>
            /// 通过反射从目标对象的字段上读取 TooltipAttribute.tooltip。
            /// 结果按"类型全名.字段名"缓存，相同字段只反射一次。
            /// SerializeReference 托管引用的 SerializedProperty.tooltip 始终为空，
            /// 需通过本方法读取原始 C# 字段上的 [Tooltip]。
            /// </summary>
            /// <param name="target">持有该字段的托管对象实例。</param>
            /// <param name="fieldName">字段名（含 m_ 前缀等实际声明名称）。</param>
            /// <returns>Tooltip 文本；字段无 TooltipAttribute 或 target 为 null 时返回 null。</returns>
            public static string GetFieldTooltip(object target, string fieldName)
            {
                if (target == null || string.IsNullOrEmpty(fieldName))
                {
                    return null;
                }
                return GetFieldTooltip(target.GetType(), fieldName);
            }

            /// <summary>
            /// 通过反射从指定类型的字段上读取 TooltipAttribute.tooltip。
            /// 结果按"类型全名.字段名"缓存，相同字段只反射一次。
            /// </summary>
            /// <param name="targetType">声明该字段的类型。</param>
            /// <param name="fieldName">字段名（含 m_ 前缀等实际声明名称）。</param>
            /// <returns>Tooltip 文本；字段无 TooltipAttribute 或 targetType 为 null 时返回 null。</returns>
            public static string GetFieldTooltip(Type targetType, string fieldName)
            {
                if (targetType == null || string.IsNullOrEmpty(fieldName))
                {
                    return null;
                }

                string cacheKey = targetType.FullName + "." + fieldName;
                if (s_TooltipCache.TryGetValue(cacheKey, out string cached))
                {
                    return cached;
                }

                FieldInfo fi = targetType.GetField(fieldName, BindingFlags.NonPublic | BindingFlags.Public | BindingFlags.Instance);
                string tooltip = null;
                if (fi != null)
                {
                    TooltipAttribute attr = fi.GetCustomAttribute<TooltipAttribute>();
                    tooltip = attr?.tooltip;
                }

                s_TooltipCache[cacheKey] = tooltip;
                return tooltip;
            }
        }
    }
}
