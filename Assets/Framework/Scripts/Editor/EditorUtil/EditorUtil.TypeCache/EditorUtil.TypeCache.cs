/***************************************************************
 * (c) copyright 2026 - 2030, Solotopia
 * All Rights Reserved.
 * -------------------------------------------------------------
 * filename:  EditorUtil.TypeCache.cs
 * author:    taoye
 * created:   2026/3/30
 * descrip:   编辑器类型缓存
 ***************************************************************/

using System;
using System.Collections.Generic;
using NovaFramework.Runtime;
using UnityEditor;

namespace NovaFramework.Editor
{
    public static partial class EditorUtil
    {
        /// <summary>
        /// 编辑器类型缓存 - 全局缓存所有编辑器需要的类型名称列表。
        /// 首次访问时反射收集，编译后自动刷新。
        /// <para>线程安全：仅限主线程访问。所有方法均在 EditorApplication.update / OnInspectorGUI 等主线程回调中调用，
        /// 无需加锁。禁止从后台线程访问。</para>
        /// </summary>
        public static class TypeCache
        {
            /// <summary>
            /// 缓存：Type.FullName -> string[]（排序后的实现类全名数组）。
            /// </summary>
            private static readonly Dictionary<string, string[]> s_CachedTypeNames = new Dictionary<string, string[]>();

            /// <summary>
            /// 缓存是否有效。
            /// </summary>
            private static bool s_IsCacheValid = false;

            /// <summary>
            /// 获取指定基类/接口的所有实现类名称（带缓存）。
            /// </summary>
            /// <param name="typeBase">基类或接口类型。</param>
            /// <returns>所有实现类的全名数组。</returns>
            public static string[] GetTypeNames(Type typeBase)
            {
                if (!s_IsCacheValid)
                {
                    ResetCache();
                }

                string key = typeBase.FullName;
                if (!s_CachedTypeNames.TryGetValue(key, out string[] result))
                {
                    result = Util.Assembly.GetTypeNames(typeBase);
                    s_CachedTypeNames[key] = result;
                }

                return result;
            }

            /// <summary>
            /// 使缓存失效（编译完成后调用）。
            /// </summary>
            public static void InvalidateCache()
            {
                s_IsCacheValid = false;
                s_CachedTypeNames.Clear();
            }

            /// <summary>
            /// 重置缓存，清空旧数据并标记为有效。后续通过 GetTypeNames 按需延迟加载。
            /// </summary>
            private static void ResetCache()
            {
                s_CachedTypeNames.Clear();
                s_IsCacheValid = true;
            }

            /// <summary>
            /// 编辑器加载时注册编译完成回调。
            /// </summary>
            [InitializeOnLoadMethod]
            private static void OnEditorLoad()
            {
                s_IsCacheValid = false;
                UnityEditor.Compilation.CompilationPipeline.compilationFinished += OnCompilationFinished;
            }

            /// <summary>
            /// 编译完成回调，使缓存失效。
            /// </summary>
            /// <param name="obj">编译上下文（未使用）。</param>
            private static void OnCompilationFinished(object obj)
            {
                InvalidateCache();
            }
        }
    }
}
