/***************************************************************
 * (c) copyright 2026 - 2030, Solotopia
 * All Rights Reserved.
 * -------------------------------------------------------------
 * filename:  FolderMenuItems.cs
 * author:    taoye
 * created:   2026/4/1
 * descrip:   打开文件夹与 IDE 工程相关菜单项集合
 ***************************************************************/

using NovaFramework.Runtime;
using UnityEditor;
using UnityEngine;
using YooAsset;

namespace NovaFramework.Editor
{
    /// <summary>
    /// 打开文件夹与 IDE 工程相关菜单项集合。
    /// </summary>
    public static class FolderMenuItems
    {
        /// <summary>
        /// 打开 IDE 工程菜单路径。
        /// </summary>
        private const string c_MenuOpenIdeProject = "Nova/Open IDE Project";

        /// <summary>
        /// 打开 Data Path 文件夹菜单路径。
        /// </summary>
        private const string c_MenuOpenFolderDataPath = "Nova/Open Folder/Data Path";

        /// <summary>
        /// 打开 Persistent Data Path (Unity) 文件夹菜单路径。
        /// </summary>
        private const string c_MenuOpenFolderPersistentDataPath = "Nova/Open Folder/Persistent Data Path (Unity)";

        /// <summary>
        /// 打开 Persistent Data Path (YooAsset) 文件夹菜单路径。
        /// </summary>
        private const string c_MenuOpenFolderPersistentDataPathYooAsset = "Nova/Open Folder/Persistent Data Path (YooAsset)";

        /// <summary>
        /// 打开 Bundle Generated Path 文件夹菜单路径。
        /// </summary>
        private const string c_MenuOpenFolderBundleGeneratedPath = "Nova/Open Folder/Bundle Generated Path";

        /// <summary>
        /// 打开 Streaming Assets Path 文件夹菜单路径。
        /// </summary>
        private const string c_MenuOpenFolderStreamingAssetsPath = "Nova/Open Folder/Streaming Assets Path";

        /// <summary>
        /// 打开 Caching Writing Path 文件夹菜单路径。
        /// </summary>
        private const string c_MenuOpenFolderCachingWritingPath = "Nova/Open Folder/Caching Writing Path";

        /// <summary>
        /// 打开 Temporary Cache Path 文件夹菜单路径。
        /// </summary>
        private const string c_MenuOpenFolderTemporaryCachePath = "Nova/Open Folder/Temporary Cache Path";

        /// <summary>
        /// 打开 IDE 工程菜单排序优先级。
        /// </summary>
        private const int c_PriorityOpenIdeProject = 1010;

        /// <summary>
        /// 打开 Data Path 文件夹菜单排序优先级。
        /// </summary>
        private const int c_PriorityOpenFolderDataPath = 1020;

        /// <summary>
        /// 打开 Persistent Data Path (Unity) 文件夹菜单排序优先级。
        /// </summary>
        private const int c_PriorityOpenFolderPersistentDataPath = 1021;

        /// <summary>
        /// 打开 Persistent Data Path (YooAsset) 文件夹菜单排序优先级。
        /// </summary>
        private const int c_PriorityOpenFolderPersistentDataPathYooAsset = 1022;

        /// <summary>
        /// 打开 Bundle Generated Path 文件夹菜单排序优先级。
        /// </summary>
        private const int c_PriorityOpenFolderBundleGeneratedPath = 1023;

        /// <summary>
        /// 打开 Streaming Assets Path 文件夹菜单排序优先级。
        /// </summary>
        private const int c_PriorityOpenFolderStreamingAssetsPath = 1024;

        /// <summary>
        /// 打开 Caching Writing Path 文件夹菜单排序优先级。
        /// </summary>
        private const int c_PriorityOpenFolderCachingWritingPath = 1025;

        /// <summary>
        /// 打开 Temporary Cache Path 文件夹菜单排序优先级。
        /// </summary>
        private const int c_PriorityOpenFolderTemporaryCachePath = 1026;

        /// <summary>
        /// Nova 框架根 Prefab 资源路径，用于读取 AssetComponent 上的默认包名。
        /// </summary>
        private const string c_NovaPrefabAssetPath = "Assets/Framework/Prefabs/Nova.prefab";

        /// <summary>
        /// AssetComponent 默认包名字段名，用于 SerializedObject 反射读取。
        /// </summary>
        private const string c_AssetComponentDefaultPackageNameField = "m_DefaultPackageName";

        /// <summary>
        /// AssetComponent 包名列表字段名，用于在默认包名为空时回退取首项。
        /// </summary>
        private const string c_AssetComponentPackagesField = "m_Packages";

        /// <summary>
        /// 使用 Unity 指定的外部脚本编辑器打开 IDE 工程。
        /// </summary>
        [MenuItem(c_MenuOpenIdeProject, false, c_PriorityOpenIdeProject)]
        public static void OpenIdeProject()
        {
            Unity.CodeEditor.CodeEditor.CurrentEditor.OpenProject();
        }

        /// <summary>
        /// 打开 Data Path 文件夹。
        /// </summary>
        [MenuItem(c_MenuOpenFolderDataPath, false, c_PriorityOpenFolderDataPath)]
        public static void OpenFolderDataPath()
        {
            EditorUtil.FileSystem.OpenFolder(Application.dataPath);
        }

        /// <summary>
        /// 打开 Persistent Data Path (Unity) 文件夹。
        /// </summary>
        [MenuItem(c_MenuOpenFolderPersistentDataPath, false, c_PriorityOpenFolderPersistentDataPath)]
        public static void OpenFolderPersistentDataPath()
        {
            EditorUtil.FileSystem.OpenFolder(Application.persistentDataPath);
        }

        /// <summary>
        /// 打开 Persistent Data Path (YooAsset) 文件夹：{项目根}/{YooFolderName}/{PackageName}；
        /// YooFolderName 为空时回退至项目根。
        /// </summary>
        [MenuItem(c_MenuOpenFolderPersistentDataPathYooAsset, false, c_PriorityOpenFolderPersistentDataPathYooAsset)]
        public static void OpenFolderPersistentDataPathYooAsset()
        {
            string projectRoot = GetProjectRootPath();
            string folderName = YooAssetConfiguration.GetYooFolderName();
            if (string.IsNullOrEmpty(folderName))
            {
                EditorUtil.FileSystem.OpenFolder(projectRoot);
                return;
            }

            string folderPath = Util.SysIO.Path.Combine(projectRoot, folderName);
            string packageName = GetDefaultPackageName();
            string packagePath = string.IsNullOrEmpty(packageName)
                ? null
                : Util.SysIO.Path.Combine(folderPath, packageName);

            string target = packagePath != null && Util.SysIO.Directory.Exists(packagePath)
                ? packagePath
                : Util.SysIO.Directory.Exists(folderPath)
                    ? folderPath
                    : projectRoot;
            EditorUtil.FileSystem.OpenFolder(target);
        }

        /// <summary>
        /// 打开 Bundle Generated Path 文件夹：{项目根}/Bundles/{Platform}/{PackageName}；
        /// 任一层不存在则回退至上一层存在的目录，最终回退至项目根。
        /// </summary>
        [MenuItem(c_MenuOpenFolderBundleGeneratedPath, false, c_PriorityOpenFolderBundleGeneratedPath)]
        public static void OpenFolderBundleGeneratedPath()
        {
            string projectRoot = GetProjectRootPath();
            string bundlesRoot = Util.SysIO.Path.Combine(projectRoot, "Bundles");
            string platform = EditorUserBuildSettings.activeBuildTarget.ToString();
            string platformPath = Util.SysIO.Path.Combine(bundlesRoot, platform);
            string packageName = GetDefaultPackageName();
            string packagePath = string.IsNullOrEmpty(packageName)
                ? null
                : Util.SysIO.Path.Combine(platformPath, packageName);

            string target = packagePath != null && Util.SysIO.Directory.Exists(packagePath)
                ? packagePath
                : Util.SysIO.Directory.Exists(platformPath)
                    ? platformPath
                    : Util.SysIO.Directory.Exists(bundlesRoot)
                        ? bundlesRoot
                        : projectRoot;
            EditorUtil.FileSystem.OpenFolder(target);
        }

        /// <summary>
        /// 打开 Streaming Assets Path 文件夹。
        /// </summary>
        [MenuItem(c_MenuOpenFolderStreamingAssetsPath, false, c_PriorityOpenFolderStreamingAssetsPath)]
        public static void OpenFolderStreamingAssetsPath()
        {
            EditorUtil.FileSystem.OpenFolder(Application.streamingAssetsPath);
        }

        /// <summary>
        /// 打开 Caching Writing Path 文件夹。
        /// </summary>
        [MenuItem(c_MenuOpenFolderCachingWritingPath, false, c_PriorityOpenFolderCachingWritingPath)]
        public static void OpenFolderCachingWritingPath()
        {
            EditorUtil.FileSystem.OpenFolder(Caching.currentCacheForWriting.path);
        }

        /// <summary>
        /// 打开 Temporary Cache Path 文件夹。
        /// </summary>
        [MenuItem(c_MenuOpenFolderTemporaryCachePath, false, c_PriorityOpenFolderTemporaryCachePath)]
        public static void OpenFolderTemporaryCachePath()
        {
            EditorUtil.FileSystem.OpenFolder(Application.temporaryCachePath);
        }

        /// <summary>
        /// 获取项目根目录（去掉末尾 /Assets）。
        /// </summary>
        private static string GetProjectRootPath()
        {
            return System.IO.Directory.GetParent(Application.dataPath).FullName.Replace("\\", "/");
        }

        /// <summary>
        /// 从 Nova.prefab 上的 AssetComponent 读取默认包名；为空时回退到 m_Packages[0]，
        /// 仍读不到时返回空字符串。
        /// </summary>
        private static string GetDefaultPackageName()
        {
            GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(c_NovaPrefabAssetPath);
            if (prefab == null)
            {
                return string.Empty;
            }

            AssetComponent component = prefab.GetComponentInChildren<AssetComponent>(true);
            if (component == null)
            {
                return string.Empty;
            }

            using SerializedObject so = new SerializedObject(component);
            string defaultName = so.FindProperty(c_AssetComponentDefaultPackageNameField)?.stringValue;
            if (!string.IsNullOrEmpty(defaultName))
            {
                return defaultName;
            }

            SerializedProperty packages = so.FindProperty(c_AssetComponentPackagesField);
            if (packages != null && packages.isArray && packages.arraySize > 0)
            {
                return packages.GetArrayElementAtIndex(0).stringValue ?? string.Empty;
            }

            return string.Empty;
        }
    }
}
