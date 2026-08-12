/***************************************************************
 * (c) copyright 2026 - 2030, Solotopia
 * All Rights Reserved.
 * -------------------------------------------------------------
 * filename:  AppsFlyerSDKUrlMenuItems.cs
 * author:    taoye
 * created:   2026/8/10
 * descrip:   AppsFlyer SDK 外部入口菜单项
 ***************************************************************/

using UnityEditor;
using UnityEngine;

namespace NovaFramework.SDK.AppsFlyerPlugin.Editor
{
    /// <summary>
    /// AppsFlyer SDK 外部入口菜单项。
    /// </summary>
    internal static class AppsFlyerSDKUrlMenuItems
    {
        private const int c_ConsoleMenuPriority = 1054;
        private const int c_ReadmeMenuPriority = 1055;

        /// <summary>
        /// 打开 AppsFlyer 后台。
        /// </summary>
        [MenuItem("Nova/Open SDK URL/AppsFlyer Console", false, c_ConsoleMenuPriority)]
        private static void OpenConsole()
        {
            Application.OpenURL("https://hq1.appsflyer.com/apps/myapps");
        }

        /// <summary>
        /// 打开 AppsFlyer Unity 接入文档。
        /// </summary>
        [MenuItem("Nova/Open SDK URL/AppsFlyer Readme", false, c_ReadmeMenuPriority)]
        private static void OpenReadme()
        {
            Application.OpenURL("https://dev.appsflyer.com/hc/docs/unity-plugin");
        }
    }
}
