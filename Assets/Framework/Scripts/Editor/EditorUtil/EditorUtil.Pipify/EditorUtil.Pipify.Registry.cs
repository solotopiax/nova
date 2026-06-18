/***************************************************************
 * (c) copyright 2026 - 2030, Solotopia
 * All Rights Reserved.
 * -------------------------------------------------------------
 * filename:  EditorUtil.Pipify.Registry.cs
 * author:    taoye
 * created:   2026/5/10
 * descrip:   Step 元信息注册表（TypeCache 扫描 [PipifyStep]）
 ***************************************************************/

using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using Cysharp.Threading.Tasks;
using NovaFramework.Runtime;
using UnityEditor;

namespace NovaFramework.Editor
{
    public static partial class EditorUtil
    {
        public static partial class Pipify
        {
            /// <summary>
            /// Step 元信息注册表（TypeCache 扫描 [PipifyStep]）。
            /// </summary>
            public static class Registry
            {
                /// <summary>
                /// 已注册的所有 Step，按 ID 索引。
                /// <StepId, PipifyStepInfo>
                /// </summary>
                private static readonly Dictionary<string, PipifyStepInfo> s_Map =
                    new Dictionary<string, PipifyStepInfo>(StringComparer.Ordinal);

                /// <summary>
                /// 静态构造：域重载时自动重新扫描。
                /// </summary>
                static Registry()
                {
                    Rebuild();
                }

                /// <summary>
                /// 重新扫描全工程的 [PipifyStep] 静态方法，刷新注册表。
                /// </summary>
                public static void Rebuild()
                {
                    s_Map.Clear();
                    UnityEditor.TypeCache.MethodCollection methods = UnityEditor.TypeCache.GetMethodsWithAttribute<PipifyStepAttribute>();
                    foreach (MethodInfo method in methods)
                    {
                        if (!ValidateSignature(method, out Type paramsType)) continue;
                        PipifyStepAttribute attr = method.GetCustomAttribute<PipifyStepAttribute>();
                        if (attr == null) continue;
                        if (s_Map.ContainsKey(attr.Id))
                        {
                            Log.Error(LogTag.Editor,
                                "{0} 重复 StepId：{1}（命中 {2}.{3}）",
                                c_LogPrefix, attr.Id, method.DeclaringType?.FullName, method.Name);
                            continue;
                        }
                        s_Map.Add(attr.Id,
                            new PipifyStepInfo(attr.Id, attr.DisplayName, attr.Category, paramsType, method));
                    }
                }

                /// <summary>
                /// 获取全部已注册 Step。
                /// </summary>
                /// <returns>所有注册的 StepInfo 集合。</returns>
                public static IReadOnlyCollection<PipifyStepInfo> GetAll() => s_Map.Values;

                /// <summary>
                /// 按 ID 查找 Step。
                /// </summary>
                /// <param name="id">Step ID。</param>
                /// <returns>命中则返回记录，否则 null。</returns>
                public static PipifyStepInfo FindById(string id)
                {
                    if (string.IsNullOrEmpty(id)) return null;
                    s_Map.TryGetValue(id, out PipifyStepInfo info);
                    return info;
                }

                /// <summary>
                /// 按 Category 分组，组内按 DisplayName 升序排列。
                /// </summary>
                /// <returns>按分类分组的 StepInfo 枚举。</returns>
                public static IEnumerable<IGrouping<string, PipifyStepInfo>> GroupByCategory()
                {
                    return s_Map.Values.OrderBy(v => v.Category).ThenBy(v => v.DisplayName).GroupBy(v => v.Category);
                }

                /// <summary>
                /// 校验 Step 方法签名是否合法。
                /// 合法签名：static + 返回 UniTask + 首参 PipifyContext
                /// （可附加第二参 [Serializable] class）。
                /// </summary>
                /// <param name="method">待校验方法。</param>
                /// <param name="paramsType">输出参数类型（无第二参则 null）。</param>
                /// <returns>是否合法。</returns>
                private static bool ValidateSignature(MethodInfo method, out Type paramsType)
                {
                    paramsType = null;
                    if (!method.IsStatic)
                    {
                        Log.Error(LogTag.Editor,
                            "{0} Step 必须是 static：{1}.{2}",
                            c_LogPrefix, method.DeclaringType?.FullName, method.Name);
                        return false;
                    }
                    if (method.ReturnType != typeof(UniTask))
                    {
                        Log.Error(LogTag.Editor,
                            "{0} Step 返回类型必须是 UniTask：{1}.{2}",
                            c_LogPrefix, method.DeclaringType?.FullName, method.Name);
                        return false;
                    }
                    ParameterInfo[] ps = method.GetParameters();
                    if (ps.Length == 0 || ps[0].ParameterType != typeof(PipifyContext))
                    {
                        Log.Error(LogTag.Editor,
                            "{0} Step 首参必须是 PipifyContext：{1}.{2}",
                            c_LogPrefix, method.DeclaringType?.FullName, method.Name);
                        return false;
                    }
                    if (ps.Length == 1) return true;
                    if (ps.Length == 2)
                    {
                        paramsType = ps[1].ParameterType;
                        if (!paramsType.IsClass || paramsType.GetCustomAttribute<SerializableAttribute>() == null)
                        {
                            Log.Error(LogTag.Editor,
                                "{0} Step 参数类必须是 [Serializable] class：{1}.{2}",
                                c_LogPrefix, method.DeclaringType?.FullName, method.Name);
                            return false;
                        }
                        return true;
                    }
                    Log.Error(LogTag.Editor,
                        "{0} Step 参数个数非法（应为 1 或 2）：{1}.{2}",
                        c_LogPrefix, method.DeclaringType?.FullName, method.Name);
                    return false;
                }
            }
        }
    }
}
