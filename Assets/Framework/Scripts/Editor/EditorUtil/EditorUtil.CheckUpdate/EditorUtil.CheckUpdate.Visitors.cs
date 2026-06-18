/***************************************************************
 * (c) copyright 2026 - 2030, Solotopia
 * All Rights Reserved.
 * -------------------------------------------------------------
 * filename:  EditorUtil.CheckUpdate.Visitors.cs
 * author:    taoye
 * created:   2026/4/28
 * descrip:   CheckUpdate 工具 —— 字段与常量
 ***************************************************************/

using UnityEditor;

namespace NovaFramework.Editor
{
    public static partial class EditorUtil
    {
        public static partial class CheckUpdate
        {
            /// <summary>
            /// 跳过配置文件相对工程根目录的路径。
            /// </summary>
            private const string c_ConfigPath = "Library/Nova/CheckUpdate.json";

            /// <summary>
            /// 菜单路径。
            /// </summary>
            private const string c_MenuPath = "Nova/Open CheckUpdate";

            /// <summary>
            /// 打开 CheckUpdateWindow 菜单入口。
            /// </summary>
            [MenuItem(c_MenuPath)]
            private static void OpenWindow() => CheckUpdateWindow.Open();
        }
    }
}
