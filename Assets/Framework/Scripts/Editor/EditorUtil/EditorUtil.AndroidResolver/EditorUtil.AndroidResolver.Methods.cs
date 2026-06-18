/***************************************************************
 * (c) copyright 2026 - 2030, Solotopia
 * All Rights Reserved.
 * -------------------------------------------------------------
 * filename:  EditorUtil.AndroidResolver.Methods.cs
 * author:    taoye
 * created:   2026/5/11
 * descrip:   Android 依赖解析工具 —— 私有方法
 ***************************************************************/

using System;
using System.Reflection;
using NovaFramework.Runtime;

namespace NovaFramework.Editor
{
    public static partial class EditorUtil
    {
        public static partial class AndroidResolver
        {
            /// <summary>
            /// 通过反射解析 EDM4U PlayServicesResolver 类型并调用 ResolveSync(bool)。
            /// 解析逻辑：
            ///   1. 遍历 AppDomain 全部已加载 Assembly，优先匹配 FullName 含 "Google.JarResolver" 或 "Google.VersionHandlerImpl" 的程序集，降级时全程序集扫描。
            ///   2. 在候选程序集中 GetType("GooglePlayServices.PlayServicesResolver")。
            ///   3. 找到类型后反射调用 ResolveSync(bool forceResolution)，参数传 true。
            /// 失败语义：找不到类型/方法或反射调用抛异常时，本方法直接抛 InvalidOperationException，
            /// 由 Pipify Runner 捕获并标记当前 Step 失败，确保不会"静默跳过"导致后续 Step 在
            /// GeneratedLocalRepo 目录缺失时崩溃。
            /// </summary>
            private static void ExecuteResolveSync()
            {
                const string typeName = "GooglePlayServices.PlayServicesResolver";
                const string methodName = "ResolveSync";

                Type resolverType = FindResolverType(typeName);
                if (resolverType == null)
                {
                    throw new InvalidOperationException($"[AndroidResolver] 未找到 EDM4U 类型 {typeName}，请确认工程中已安装 External Dependency Manager for Unity。");
                }

                MethodInfo method = resolverType.GetMethod(methodName, BindingFlags.Public | BindingFlags.Static, null, new[] { typeof(bool) }, null);
                if (method == null)
                {
                    throw new InvalidOperationException($"[AndroidResolver] 在类型 {typeName} 上未找到方法 {methodName}(bool)，请确认 EDM4U 版本（已知签名为 ResolveSync(bool forceResolution)）。");
                }

                try
                {
                    Log.Debug(LogTag.Editor, "[AndroidResolver] 开始执行 Android 依赖解析（forceResolution=true）...");
                    method.Invoke(null, new object[] { true });
                    Log.Debug(LogTag.Editor, "[AndroidResolver] Android 依赖解析完成。");
                }
                catch (TargetInvocationException tie)
                {
                    throw new InvalidOperationException("[AndroidResolver] ResolveSync 执行异常。", tie.InnerException ?? tie);
                }
            }

            /// <summary>
            /// 在 AppDomain 已加载程序集中查找 GooglePlayServices.PlayServicesResolver 类型。
            /// 优先匹配 Assembly.FullName 含 "Google.JarResolver" 的程序集；
            /// 降级时扫描全部程序集（兼容 Google.VersionHandlerImpl 等重新封装的分发形式）。
            /// </summary>
            /// <param name="typeName">要查找的完整类型名称。</param>
            /// <returns>找到的 Type，未找到返回 null。</returns>
            private static Type FindResolverType(string typeName)
            {
                // 第一轮：优先在 Google.JarResolver / Google.VersionHandlerImpl 程序集中查找
                foreach (Assembly asm in AppDomain.CurrentDomain.GetAssemblies())
                {
                    string fullName = asm.FullName ?? string.Empty;
                    if (!fullName.Contains("Google.JarResolver") && !fullName.Contains("Google.VersionHandlerImpl")) continue;

                    Type t = asm.GetType(typeName, throwOnError: false);
                    if (t != null) return t;
                }

                // 第二轮：全程序集扫描（兜底，EDM4U 分发形式可能变化）
                foreach (Assembly asm in AppDomain.CurrentDomain.GetAssemblies())
                {
                    Type t = asm.GetType(typeName, throwOnError: false);
                    if (t != null) return t;
                }

                return null;
            }
        }
    }
}
