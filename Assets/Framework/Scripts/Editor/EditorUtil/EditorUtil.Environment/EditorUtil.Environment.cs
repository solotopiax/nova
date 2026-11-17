/***************************************************************
 * (c) copyright 2026 - 2030, Solotopia
 * All Rights Reserved.
 * -------------------------------------------------------------
 * filename:  EditorUtil.Environment.cs
 * author:    taoye
 * created:   2026/4/24
 * descrip:   编辑器环境检测及统一重新检测入口
 ***************************************************************/

using System;
using UnityEditor;
using UnityEngine;

namespace NovaFramework.Editor
{
    public static partial class EditorUtil
    {
        /// <summary>
        /// 编辑器环境检测工具及检测项的统一刷新入口。
        /// </summary>
        public static partial class Environment
        {
            /// <summary>
            /// 将无参数静态方法登记为环境重新检测入口；方法须更新自身的检测缓存，不执行安装或配置写入。
            /// 新检测项在其重新检测方法上添加此标记，即可参与 Config 窗口的统一刷新。
            /// </summary>
            [AttributeUsage(AttributeTargets.Method, AllowMultiple = false, Inherited = false)]
            public sealed class EnvironmentCheckAttribute : Attribute
            {
            }

            /// <summary>
            /// 重新执行全部已标记的环境检测，不维护固定检测项名单；单项异常只告警，继续检测其它环境。
            /// 调用方在编辑器就绪后调用，随后从各检测器读取更新后的缓存用于展示。
            /// </summary>
            internal static void RecheckAll()
            {
                foreach (var method in UnityEditor.TypeCache.GetMethodsWithAttribute<EnvironmentCheckAttribute>())
                {
                    string name = $"{method.DeclaringType?.FullName}.{method.Name}";
                    if (!method.IsStatic || method.ContainsGenericParameters || method.GetParameters().Length != 0)
                    {
                        Debug.LogWarning($"[Nova] 环境检测入口 {name} 必须是无参数、非泛型的静态方法。");
                        continue;
                    }
                    try
                    {
                        method.Invoke(null, null);
                    }
                    catch (Exception exception)
                    {
                        Debug.LogWarning($"[Nova] 环境检测 {name} 失败：{(exception.InnerException ?? exception).Message}");
                    }
                }
            }
        }
    }
}
