/***************************************************************
 * (c) copyright 2026 - 2030, Solotopia
 * All Rights Reserved.
 * -------------------------------------------------------------
 * filename:  EditorUtil.Placeholder.cs
 * author:    Codex
 * created:   2026/7/24
 * descrip:   基于当前 ConfigMaster 的 Editor 占位符上下文适配器
 ***************************************************************/

using System;
using NovaFramework.Runtime;
using UnityEditor;
using UnityEngine;

namespace NovaFramework.Editor
{
    public static partial class EditorUtil
    {
        /// <summary>
        /// Editor 环境的占位符上下文适配器。
        /// </summary>
        public static class Placeholder
        {
            private const string c_NovaPrefabAssetPath = "Assets/Framework/Prefabs/Nova.prefab";
            private const string c_AssetDefaultPackageNameField = "m_DefaultPackageName";
            private const string c_AssetPackagesField = "m_Packages";

            /// <summary>
            /// 从指定 ConfigMaster 当前坐标构造上下文。
            /// </summary>
            public static PlaceholderContext FromConfigMaster(
                ConfigMasterSO master,
                string package,
                string version,
                DateTime time)
            {
                if (master == null) throw new ArgumentNullException(nameof(master));
                return FromConfigMaster(
                    master,
                    master.CurrentPlatform,
                    master.CurrentChannel,
                    package,
                    version,
                    time);
            }

            /// <summary>
            /// 从指定 ConfigMaster 与显式导出坐标构造上下文，不修改其当前选中状态。
            /// </summary>
            public static PlaceholderContext FromConfigMaster(
                ConfigMasterSO master,
                PlatformType platform,
                ChannelType channel,
                string package,
                string version,
                DateTime time)
            {
                if (master == null) throw new ArgumentNullException(nameof(master));
                return new PlaceholderContext(
                    platform,
                    channel,
                    package,
                    version,
                    time);
            }

            /// <summary>
            /// 使用指定 ConfigMaster 解析文本。
            /// </summary>
            public static string Resolve(
                string template,
                ConfigMasterSO master,
                string package,
                string version,
                DateTime time)
            {
                return Util.Placeholder.Resolve(
                    template,
                    FromConfigMaster(master, package, version, time));
            }

            /// <summary>
            /// 使用当前激活 ConfigMaster、默认资源包、Application.version 与当前时间解析文本。
            /// </summary>
            public static string ResolveFromActiveConfig(string template)
            {
                ConfigMasterSO master = Config.WorkspaceActive.Get();
                if (master == null)
                {
                    throw new InvalidOperationException("[Placeholder] 未找到当前激活的 ConfigMasterSO。");
                }

                return Resolve(
                    template,
                    master,
                    ResolveDefaultPackageName(),
                    Application.version,
                    DateTime.Now);
            }

            /// <summary>
            /// 从 canonical Nova.prefab 的 AssetComponent 读取默认资源包名；空值回退包列表首项。
            /// </summary>
            internal static string ResolveDefaultPackageName()
            {
                GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(c_NovaPrefabAssetPath);
                AssetComponent component = prefab != null
                    ? prefab.GetComponentInChildren<AssetComponent>(true)
                    : null;
                if (component == null) return string.Empty;

                using var serializedObject = new SerializedObject(component);
                string defaultName = serializedObject.FindProperty(c_AssetDefaultPackageNameField)?.stringValue;
                if (!string.IsNullOrEmpty(defaultName)) return defaultName;

                SerializedProperty packages = serializedObject.FindProperty(c_AssetPackagesField);
                return packages != null && packages.isArray && packages.arraySize > 0
                    ? packages.GetArrayElementAtIndex(0).stringValue ?? string.Empty
                    : string.Empty;
            }
        }
    }
}
