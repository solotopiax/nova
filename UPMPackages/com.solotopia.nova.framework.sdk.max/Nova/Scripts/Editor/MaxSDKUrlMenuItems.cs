/***************************************************************
 * (c) copyright 2026 - 2030, Solotopia
 * All Rights Reserved.
 * -------------------------------------------------------------
 * filename:  MaxSDKUrlMenuItems.cs
 * author:    taoye
 * created:   2026/8/10
 * descrip:   MAX SDK 外部入口菜单项
 ***************************************************************/

using UnityEditor;
using UnityEngine;

namespace NovaFramework.SDK.MaxAdPlugin.Editor
{
    /// <summary>
    /// MAX SDK 外部入口菜单项。
    /// </summary>
    internal static class MaxSDKUrlMenuItems
    {
        private const int c_ConsoleMenuPriority = 1090;
        private const int c_ReadmeMenuPriority = 1091;

        /// <summary>
        /// 打开 MAX 后台。
        /// </summary>
        [MenuItem("Nova/Open SDK URL/MAX Console", false, c_ConsoleMenuPriority)]
        private static void OpenConsole()
        {
            Application.OpenURL("https://dash.applovin.com/login");
        }

        /// <summary>
        /// 打开 MAX Unity 接入文档。
        /// </summary>
        [MenuItem("Nova/Open SDK URL/MAX Readme", false, c_ReadmeMenuPriority)]
        private static void OpenReadme()
        {
            Application.OpenURL("https://dash.applovin.com/documentation/mediation/unity/getting-started/integration");
        }
    }
}
