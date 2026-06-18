/***************************************************************
 * (c) copyright 2026 - 2030, Solotopia
 * All Rights Reserved.
 * -------------------------------------------------------------
 * filename:  EditorUtil.Asset.Operator.cs
 * author:    taoye
 * created:   2026/5/10
 * descrip:   任意 ScriptableObject 资产查找、创建、按路径加载的通用入口（泛型版）
 ***************************************************************/

using UnityEditor;
using UnityEngine;

namespace NovaFramework.Editor
{
    public static partial class EditorUtil
    {
        /// <summary>
        /// 通用资产工具命名空间，集中放置与具体业务无关的 ScriptableObject 资产操作。
        /// </summary>
        public static partial class Asset
        {
            /// <summary>
            /// 任意 ScriptableObject 资产查找、创建、按路径加载的通用入口（泛型版）。
            /// <para>专用于 <c>.asset</c> 资产，路径一律使用 Assets 相对路径。</para>
            /// </summary>
            public static class Operator
            {
                /// <summary>
                /// 查找工程内第一个指定 ScriptableObject 类型的资产。
                /// <para>通过 AssetDatabase.FindAssets 按 <c>t:TypeName</c> 过滤，取结果列表中第一个资产加载并返回。</para>
                /// </summary>
                /// <typeparam name="T">目标 ScriptableObject 类型。</typeparam>
                /// <returns>
                /// 找到的 <typeparamref name="T"/> 实例；工程中不存在时返回 null。
                /// </returns>
                public static T Find<T>() where T : ScriptableObject
                {
                    string[] guids = AssetDatabase.FindAssets("t:" + typeof(T).Name);
                    if (guids == null || guids.Length == 0) return null;
                    string path = AssetDatabase.GUIDToAssetPath(guids[0]);
                    return AssetDatabase.LoadAssetAtPath<T>(path);
                }

                /// <summary>
                /// 在指定路径创建指定类型的空白 ScriptableObject 资产。
                /// <para>若目标目录不存在则自动递归创建；创建完成后调用 SaveAssets 与 Refresh 确保资产可用。</para>
                /// </summary>
                /// <typeparam name="T">目标 ScriptableObject 类型。</typeparam>
                /// <param name="assetPath">目标资产路径，格式为 Assets 相对路径（如 Assets/Config/Master.asset）。</param>
                /// <returns>
                /// 新建的 <typeparamref name="T"/> 实例。
                /// </returns>
                public static T CreateAt<T>(string assetPath) where T : ScriptableObject
                {
                    string dir = System.IO.Path.GetDirectoryName(assetPath);
                    if (!string.IsNullOrEmpty(dir) && !System.IO.Directory.Exists(dir))
                    {
                        System.IO.Directory.CreateDirectory(dir);
                    }
                    T so = ScriptableObject.CreateInstance<T>();
                    AssetDatabase.CreateAsset(so, assetPath);
                    AssetDatabase.SaveAssets();
                    AssetDatabase.Refresh();
                    return so;
                }

                /// <summary>
                /// 按路径加载指定类型的 ScriptableObject 资产。
                /// <para>直接调用 AssetDatabase.LoadAssetAtPath，路径不存在或类型不匹配时返回 null。</para>
                /// </summary>
                /// <typeparam name="T">目标 ScriptableObject 类型。</typeparam>
                /// <param name="assetPath">资产路径，格式为 Assets 相对路径。</param>
                /// <returns>
                /// 加载到的 <typeparamref name="T"/> 实例；路径无效或资产不存在时返回 null。
                /// </returns>
                public static T LoadAt<T>(string assetPath) where T : ScriptableObject
                {
                    return AssetDatabase.LoadAssetAtPath<T>(assetPath);
                }
            }
        }
    }
}
