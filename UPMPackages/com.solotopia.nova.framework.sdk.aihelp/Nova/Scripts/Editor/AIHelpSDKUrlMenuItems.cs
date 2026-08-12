/***************************************************************
 * (c) copyright 2026 - 2030, Solotopia
 * All Rights Reserved.
 * -------------------------------------------------------------
 * filename:  AIHelpSDKUrlMenuItems.cs
 * author:    taoye
 * created:   2026/8/10
 * descrip:   AIHelp SDK 外部入口菜单项
 ***************************************************************/

using UnityEditor;
using UnityEngine;

namespace NovaFramework.SDK.AIHelp.Editor
{
    /// <summary>
    /// AIHelp SDK 外部入口菜单项。
    /// </summary>
    internal static class AIHelpSDKUrlMenuItems
    {
        private const int c_ConsoleMenuPriority = 1042;
        private const int c_ReadmeMenuPriority = 1043;

        /// <summary>
        /// 打开 AIHelp 后台。
        /// </summary>
        [MenuItem("Nova/Open SDK URL/AIHelp Console", false, c_ConsoleMenuPriority)]
        private static void OpenConsole()
        {
            Application.OpenURL("https://aihelp.net/dashboard/#/login");
        }

        /// <summary>
        /// 打开 AIHelp Unity 接入文档。
        /// </summary>
        [MenuItem("Nova/Open SDK URL/AIHelp Readme", false, c_ReadmeMenuPriority)]
        private static void OpenReadme()
        {
            Application.OpenURL("https://docs.aihelp.net/zh/unity/");
        }
    }
}
