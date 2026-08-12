/***************************************************************
 * (c) copyright 2026 - 2030, Solotopia
 * All Rights Reserved.
 * -------------------------------------------------------------
 * filename:  FacebookSDKUrlMenuItems.cs
 * author:    taoye
 * created:   2026/8/10
 * descrip:   Facebook SDK 外部入口菜单项
 ***************************************************************/

using UnityEditor;
using UnityEngine;

namespace NovaFramework.SDK.Facebook.Editor
{
    /// <summary>
    /// Facebook SDK 外部入口菜单项。
    /// </summary>
    internal static class FacebookSDKUrlMenuItems
    {
        private const int c_ConsoleMenuPriority = 1066;
        private const int c_ReadmeMenuPriority = 1067;

        /// <summary>
        /// 打开 Facebook 开发者后台。
        /// </summary>
        [MenuItem("Nova/Open SDK URL/Facebook Console", false, c_ConsoleMenuPriority)]
        private static void OpenConsole()
        {
            Application.OpenURL("https://developers.facebook.com/apps/");
        }

        /// <summary>
        /// 打开 Facebook Unity 接入文档。
        /// </summary>
        [MenuItem("Nova/Open SDK URL/Facebook Readme", false, c_ReadmeMenuPriority)]
        private static void OpenReadme()
        {
            Application.OpenURL("https://developers.facebook.com/docs/unity");
        }
    }
}
