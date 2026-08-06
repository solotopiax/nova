/***************************************************************
 * (c) copyright 2026 - 2030, Solotopia
 * All Rights Reserved.
 * -------------------------------------------------------------
 * filename:  AssetCacheMenuItems.cs
 * author:    taoye
 * created:   2026/8/6
 * descrip:   资源缓存相关菜单项集合
 ***************************************************************/

using UnityEditor;

namespace NovaFramework.Editor
{
    /// <summary>
    /// 资源缓存相关菜单项集合。
    /// </summary>
    public static class AssetCacheMenuItems
    {
        /// <summary>
        /// 清空本地热更资源缓存菜单路径。
        /// </summary>
        private const string c_MenuClearLocalHotfixResourceCache = "Nova/Clean Hotfix Caches";

        /// <summary>
        /// 清理菜单排序优先级；与 Open Folder 的 1021 同组，并与 Enable Logs 的 1042 分组显示。
        /// </summary>
        private const int c_PriorityClearLocalHotfixResourceCache = 1031;

        /// <summary>
        /// 清空 YooAsset Editor 沙盒及框架自主保存的 version 文件。
        /// </summary>
        [MenuItem(c_MenuClearLocalHotfixResourceCache, false, c_PriorityClearLocalHotfixResourceCache)]
        public static void ClearLocalHotfixResourceCache()
        {
            EditorUtil.Asset.Cache.ClearAllHotfixResources();
        }

        /// <summary>
        /// 仅在非 Play Mode 状态启用清理菜单。
        /// </summary>
        [MenuItem(c_MenuClearLocalHotfixResourceCache, true)]
        private static bool ValidateClearLocalHotfixResourceCache()
        {
            return !EditorApplication.isPlayingOrWillChangePlaymode;
        }
    }
}
