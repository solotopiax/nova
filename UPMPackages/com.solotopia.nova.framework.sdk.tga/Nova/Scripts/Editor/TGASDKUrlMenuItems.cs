/***************************************************************
 * (c) copyright 2026 - 2030, Solotopia
 * All Rights Reserved.
 * -------------------------------------------------------------
 * filename:  TGASDKUrlMenuItems.cs
 * author:    taoye
 * created:   2026/8/10
 * descrip:   TGA SDK 外部入口菜单项
 ***************************************************************/

using UnityEditor;
using UnityEngine;

namespace NovaFramework.SDK.TGAPlugin.Editor
{
    /// <summary>
    /// TGA SDK 外部入口菜单项。
    /// </summary>
    internal static class TGASDKUrlMenuItems
    {
        private const int c_ConsoleMenuPriority = 1102;
        private const int c_ReadmeMenuPriority = 1103;

        /// <summary>
        /// 打开 TGA 后台。
        /// </summary>
        [MenuItem("Nova/Open SDK URL/TGA Console", false, c_ConsoleMenuPriority)]
        private static void OpenConsole()
        {
            Application.OpenURL("https://tga.lolipopmobi.com/#/tga/");
        }

        /// <summary>
        /// 打开 TGA 使用文档。
        /// </summary>
        [MenuItem("Nova/Open SDK URL/TGA Readme", false, c_ReadmeMenuPriority)]
        private static void OpenReadme()
        {
            Application.OpenURL("https://doc.thinkingdata.cn/ta-manual/latest/");
        }
    }
}
