/***************************************************************
 * (c) copyright 2026 - 2030, Solotopia
 * All Rights Reserved.
 * -------------------------------------------------------------
 * filename:  Util.Assembly.cs
 * author:    taoye
 * created:   2026/1/15
 * descrip:   程序集相关的实用函数
 ***************************************************************/
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;

namespace NovaFramework.Runtime
{
    public static partial class Util
    {
        public static class Assembly
        {
            private static volatile System.Reflection.Assembly[] s_Assemblies = null;
            private static readonly ConcurrentDictionary<string, Type> s_CachedTypes = new ConcurrentDictionary<string, Type>(StringComparer.Ordinal);
            private static readonly ConcurrentDictionary<string, string> s_CachedTypeNames = new ConcurrentDictionary<string, string>(StringComparer.Ordinal);

            static Assembly()
            {
                RefreshAssemblies();
            }

            /// <summary>
            /// 刷新程序集缓存（热更新加载新程序集后调用）。
            /// </summary>
            public static void RefreshAssemblies()
            {
                s_Assemblies = AppDomain.CurrentDomain.GetAssemblies();
            }

            /// <summary>
            /// 按程序集名在 AppDomain.CurrentDomain 中查找已加载的 Assembly。
            /// 未找到时返回 null。
            /// </summary>
            /// <param name="assemblyName">程序集名称（不含 .dll 扩展名）。</param>
            /// <returns>匹配的 Assembly；未找到时为 null。</returns>
            public static System.Reflection.Assembly GetAssembly(string assemblyName)
            {
                if (string.IsNullOrEmpty(assemblyName))
                {
                    throw new ArgumentException("assemblyName 无效。", nameof(assemblyName));
                }

                var assemblies = s_Assemblies;
                for (int i = 0; i < assemblies.Length; i++)
                {
                    if (assemblies[i].GetName().Name == assemblyName)
                    {
                        return assemblies[i];
                    }
                }

                return null;
            }

            /// <summary>
            /// 获取已加载的程序集。
            /// </summary>
            /// <returns>已加载的程序集。</returns>
            public static System.Reflection.Assembly[] GetAssemblies()
            {
                return s_Assemblies;
            }

            /// <summary>
            /// 获取已加载的程序集中的所有类型。
            /// </summary>
            /// <param name="results">已加载的程序集中的所有类型。</param>
            public static void GetTypes(List<Type> results)
            {
                if (results == null)
                {
                    throw new ArgumentNullException(nameof(results), "results 无效。");
                }

                results.Clear();
                var assemblies = s_Assemblies;
                if (assemblies == null)
                {
                    return;
                }
                foreach (System.Reflection.Assembly assembly in assemblies)
                {
                    results.AddRange(assembly.GetTypes());
                }
            }

            /// <summary>
            /// 获取已加载的程序集中的指定类型。
            /// </summary>
            /// <param name="typeName">要获取的类型名。</param>
            /// <param name="assemblyName">要查找的程序集名称。</param>
            /// <returns>已加载的程序集中的指定类型。</returns>
            public static Type GetType(string typeName, string assemblyName = null)
            {
                if (string.IsNullOrEmpty(typeName))
                {
                    throw new ArgumentException("typeName 无效。", nameof(typeName));
                }

                Type type = null;
                if (s_CachedTypes.TryGetValue(typeName, out type))
                {
                    return type;
                }

                type = Type.GetType(typeName);
                if (type != null)
                {
                    s_CachedTypes.TryAdd(typeName, type);
                    return type;
                }

                var assemblies = s_Assemblies;
                if (assemblies == null)
                {
                    return null;
                }
                foreach (System.Reflection.Assembly assembly in assemblies)
                {
                    if (!string.IsNullOrEmpty(assemblyName))
                    {
                        if (assembly.GetName().Name == assemblyName)
                        {
                            type = assembly.GetType(string.Concat(assemblyName, ".", typeName));
                            if (type != null)
                            {
                                s_CachedTypes.TryAdd(typeName, type);
                                return type;
                            }
                        }
                    }
                    else
                    {
                        type = Type.GetType(string.Concat(typeName, ", ", assembly.FullName));
                        if (type != null)
                        {
                            s_CachedTypes.TryAdd(typeName, type);
                            return type;
                        }
                    }
                }

                return null;
            }
            
            /// <summary>
            /// 获取指定基类的所有子类的名称。
            /// </summary>
            /// <param name="typeBase">基类类型。</param>
            /// <param name="assemblyName">要查找的程序集名称。</param>
            /// <returns>指定基类的所有子类名称。</returns>
            public static string[] GetTypeNames(Type typeBase, string assemblyName = null)
            {
                if (typeBase == null) throw new ArgumentNullException(nameof(typeBase));

                List<string> typeNames = new List<string>();
                var assemblies = s_Assemblies;
                if (assemblies == null)
                {
                    return Array.Empty<string>();
                }
                foreach (var assembly in assemblies)
                {
                    if (!string.IsNullOrEmpty(assemblyName) && assembly.GetName().Name != assemblyName)
                    {
                        continue;
                    }

                    try
                    {
                        foreach (var type in assembly.GetTypes())
                        {
                            if (type.IsClass && !type.IsAbstract && typeBase.IsAssignableFrom(type))
                            {
                                typeNames.Add(type.FullName);
                            }
                        }
                    }
                    catch (System.Reflection.ReflectionTypeLoadException ex)
                    {
                        Type[] loadedTypes = ex.Types;
                        for (int i = 0; i < loadedTypes.Length; i++)
                        {
                            if (loadedTypes[i] != null && loadedTypes[i].IsClass && !loadedTypes[i].IsAbstract && typeBase.IsAssignableFrom(loadedTypes[i]))
                            {
                                typeNames.Add(loadedTypes[i].FullName);
                            }
                        }
                    }
                }

                typeNames.Sort(StringComparer.Ordinal);
                return typeNames.ToArray();
            }
            
            /// <summary>
            /// 按基类扫描指定 Assembly 中的非抽象子类型名列表。
            /// 用于将扫描范围限定在单个程序集（如 Framework 程序集），
            /// 避免 HybridCLR 业务 DLL 未加载时的漏扫或业务 Procedure 的意外注册。
            /// </summary>
            /// <param name="baseType">基类类型。</param>
            /// <param name="targetAssembly">目标程序集。</param>
            /// <returns>目标程序集中所有非抽象子类的全名列表（按序排列）。</returns>
            public static string[] GetTypeNames(Type baseType, System.Reflection.Assembly targetAssembly)
            {
                if (baseType == null) throw new ArgumentNullException(nameof(baseType));
                if (targetAssembly == null) throw new ArgumentNullException(nameof(targetAssembly));

                List<string> typeNames = new List<string>();
                try
                {
                    foreach (var type in targetAssembly.GetTypes())
                    {
                        if (type.IsClass && !type.IsAbstract && baseType.IsAssignableFrom(type))
                        {
                            typeNames.Add(type.FullName);
                        }
                    }
                }
                catch (System.Reflection.ReflectionTypeLoadException ex)
                {
                    Type[] loadedTypes = ex.Types;
                    for (int i = 0; i < loadedTypes.Length; i++)
                    {
                        if (loadedTypes[i] != null && loadedTypes[i].IsClass && !loadedTypes[i].IsAbstract && baseType.IsAssignableFrom(loadedTypes[i]))
                        {
                            typeNames.Add(loadedTypes[i].FullName);
                        }
                    }
                }

                typeNames.Sort(StringComparer.Ordinal);
                return typeNames.ToArray();
            }

            /// <summary>
            /// 获取指定基类的所有子类的名称，并写入传入的 List。
            /// </summary>
            /// <param name="typeBase">基类类型。</param>
            /// <param name="results">输出子类名称列表。</param>
            /// <param name="assemblyName">要查找的程序集名称。</param>
            public static void GetTypeNames(Type typeBase, List<string> results, string assemblyName = null)
            {
                if (typeBase == null) throw new ArgumentNullException(nameof(typeBase));
                if (results == null) throw new ArgumentNullException(nameof(results));

                results.Clear();
                var assemblies = s_Assemblies;
                if (assemblies == null)
                {
                    return;
                }
                foreach (var assembly in assemblies)
                {
                    if (!string.IsNullOrEmpty(assemblyName) && assembly.GetName().Name != assemblyName)
                    {
                        continue;
                    }

                    try
                    {
                        foreach (var type in assembly.GetTypes())
                        {
                            if (type.IsClass && !type.IsAbstract && typeBase.IsAssignableFrom(type))
                            {
                                results.Add(type.FullName);
                            }
                        }
                    }
                    catch (System.Reflection.ReflectionTypeLoadException ex)
                    {
                        Type[] loadedTypes = ex.Types;
                        for (int i = 0; i < loadedTypes.Length; i++)
                        {
                            if (loadedTypes[i] != null && loadedTypes[i].IsClass && !loadedTypes[i].IsAbstract && typeBase.IsAssignableFrom(loadedTypes[i]))
                            {
                                results.Add(loadedTypes[i].FullName);
                            }
                        }
                    }
                }

                results.Sort(StringComparer.Ordinal);
            }
            
        }   
    }
}


