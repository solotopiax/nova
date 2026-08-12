/***************************************************************
 * (c) copyright 2026 - 2030, Solotopia
 * All Rights Reserved.
 * -------------------------------------------------------------
 * filename:  FirebaseSDKUrlMenuItems.cs
 * author:    taoye
 * created:   2026/8/10
 * descrip:   Firebase SDK 外部入口菜单项
 ***************************************************************/

using UnityEditor;
using UnityEngine;

namespace NovaFramework.SDK.FirebasePlugin.Editor
{
    /// <summary>
    /// Firebase SDK 外部入口菜单项。
    /// </summary>
    internal static class FirebaseSDKUrlMenuItems
    {
        private const int c_ConsoleMenuPriority = 1078;
        private const int c_ReadmeMenuPriority = 1079;

        /// <summary>
        /// 打开 Firebase 后台。
        /// </summary>
        [MenuItem("Nova/Open SDK URL/Firebase Console", false, c_ConsoleMenuPriority)]
        private static void OpenConsole()
        {
            Application.OpenURL("https://console.firebase.google.com/");
        }

        /// <summary>
        /// 打开 Firebase Unity 接入文档。
        /// </summary>
        [MenuItem("Nova/Open SDK URL/Firebase Readme", false, c_ReadmeMenuPriority)]
        private static void OpenReadme()
        {
            Application.OpenURL("https://firebase.google.com/docs/unity/setup?authuser=0");
        }
    }
}
