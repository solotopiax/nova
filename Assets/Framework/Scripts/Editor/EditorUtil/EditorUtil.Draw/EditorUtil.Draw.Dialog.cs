/***************************************************************
 * (c) copyright 2026 - 2030, Solotopia
 * All Rights Reserved.
 * -------------------------------------------------------------
 * filename:  EditorUtil.Draw.Dialog.cs
 * author:    taoye
 * created:   2026/5/18
 * descrip:   编辑器绘制工具-对话框/系统交互
 ***************************************************************/

using UnityEditor;

namespace NovaFramework.Editor
{
    public static partial class EditorUtil
    {
        /// <summary>
        /// 绘制工具。
        /// </summary>
        public static partial class Draw
        {
            /// <summary>
            /// 弹出系统原生文件夹选择对话框，返回用户选中的目录绝对路径；用户取消则返回空字符串。
            /// </summary>
            /// <param name="title">对话框标题。</param>
            /// <param name="initialFolder">初始打开目录绝对路径。</param>
            /// <param name="defaultName">默认文件夹名称，通常传空字符串。</param>
            /// <returns>用户选中的目录绝对路径，取消时返回空字符串。</returns>
            public static string OpenFolderPanel(string title, string initialFolder, string defaultName = "")
            {
                return EditorUtility.OpenFolderPanel(title, initialFolder, defaultName);
            }

            /// <summary>
            /// 在系统文件管理器中定位并高亮显示指定路径的文件或目录。
            /// </summary>
            /// <param name="absolutePath">要在文件管理器中打开的绝对路径。</param>
            public static void RevealInFinder(string absolutePath)
            {
                EditorUtility.RevealInFinder(absolutePath);
            }
        }
    }
}
