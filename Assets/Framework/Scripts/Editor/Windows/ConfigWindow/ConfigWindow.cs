/***************************************************************
 * (c) copyright 2026 - 2030, Solotopia
 * All Rights Reserved.
 * -------------------------------------------------------------
 * filename:  ConfigWindow.cs
 * author:    taoye
 * created:   2026/4/27
 * descrip:   Nova 全局环境配置窗口
 ***************************************************************/

using UnityEditor;
using UnityEngine;
using static NovaFramework.Editor.EditorUtil.Environment.LubanChecker;

namespace NovaFramework.Editor
{
    /// <summary>
    /// Nova 全局环境配置窗口，集中展示与管理框架层级的各类环境检测和全局配置信息。
    /// </summary>
    internal sealed partial class ConfigWindow : EditorWindow
    {
        /// <summary>
        /// 菜单入口：打开环境配置窗口。
        /// </summary>
        [MenuItem(c_MenuPath)]
        public static void Open()
        {
            ConfigWindow window = GetWindow<ConfigWindow>(false, c_WindowTitle, true);
            window.minSize = new Vector2(c_WindowMinWidth, c_WindowMinHeight);
        }

        /// <summary>
        /// 通过 Luban 环境检测结果打开窗口并自动导航到 Luban 面板（Pipeline 调用入口）。
        /// </summary>
        /// <param name="result">Luban 环境检测结果。</param>
        public static void OpenLubanSection(EnvironmentCheckResult result)
        {
            ConfigWindow window = GetWindow<ConfigWindow>(false, c_WindowTitle, true);
            window.minSize = new Vector2(c_WindowMinWidth, c_WindowMinHeight);
            window.m_LubanCheckResult = result;
            window.m_SelectedItem = LeftTreeItem.LubanEnv;
        }

        /// <summary>
        /// EditorWindow 原生关窗"保存"回调（hasUnsavedChanges 为 true 时触发）：
        /// 将 WorkingCopy 通过 CopySerialized 写回真实资产后调基类完成流程。
        /// hasUnsavedChanges / saveChangesMessage 是 EditorWindow 可写普通属性，在 OnGUI 每帧同步写入。
        /// </summary>
        public override void SaveChanges()
        {
            CommitWorkingCopyToAsset();
            base.SaveChanges();
        }

        /// <summary>
        /// EditorWindow 原生关窗"不保存"回调：销毁 WorkingCopy、清除脏标记后调基类。
        /// OnDisable 紧随其后销毁窗口，无需重建副本（P4 清理：移除多余的 RebuildWorkingCopy）。
        /// </summary>
        public override void DiscardChanges()
        {
            DestroyWorkingCopy();
            m_IsDirty = false;
            base.DiscardChanges();
        }
    }
}
