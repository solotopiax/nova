/***************************************************************
 * (c) copyright 2026 - 2030, Solotopia
 * All Rights Reserved.
 * -------------------------------------------------------------
 * filename:  PipifyWindow.cs
 * author:    taoye
 * created:   2026/5/10
 * descrip:   Pipify 流水线配置与执行窗口
 ***************************************************************/

using UnityEditor;
using UnityEngine;

namespace NovaFramework.Editor
{
    /// <summary>
    /// Pipify 流水线配置与执行窗口，用于管理 Batch 列表、配置步骤参数并触发一键构建流水线。
    /// </summary>
    internal sealed partial class PipifyWindow : EditorWindow
    {
        /// <summary>
        /// 菜单入口：打开 Pipify 窗口。
        /// </summary>
        [MenuItem("Nova/Open Pipify")]
        public static void Open()
        {
            PipifyWindow window = GetWindow<PipifyWindow>(false, c_WindowTitle, true);
            window.minSize = new Vector2(c_WindowMinWidth, c_WindowMinHeight);
        }
    }
}
