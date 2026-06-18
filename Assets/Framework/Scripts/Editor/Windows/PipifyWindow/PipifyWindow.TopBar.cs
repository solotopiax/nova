/***************************************************************
 * (c) copyright 2026 - 2030, Solotopia
 * All Rights Reserved.
 * -------------------------------------------------------------
 * filename:  PipifyWindow.TopBar.cs
 * author:    taoye
 * created:   2026/5/10
 * descrip:   Pipify 窗口顶部工具栏（存档文件选择 + 创建 + 选择 + 保存 + 打开文件夹）
 ***************************************************************/

using UnityEditor;
using UnityEngine;

namespace NovaFramework.Editor
{
    internal sealed partial class PipifyWindow : EditorWindow
    {
        /// <summary>
        /// 绘制顶部工具栏：存档文件 ObjectField + 创建 + 选择 + 保存 + 打开文件夹一排控件。
        /// ObjectField 值变化时调用 RebindSettings；保存按钮仅在有脏数据时可用。
        /// </summary>
        private void DrawTopBar()
        {
            EditorUtil.Draw.Layout.Vertical("helpBox", () =>
            {
                EditorUtil.Draw.Layout.Horizontal(() =>
                {
                    EditorUtil.Draw.Label("编辑器存档文件：", false, GUILayout.Width(c_TopBarLabelWidth));
                    PipifySettingsSO newSettings = EditorUtil.Draw.ObjectField<PipifySettingsSO>(string.Empty, m_Settings, false, false);
                    if (newSettings != m_Settings) RebindSettings(newSettings);
                    EditorUtil.Draw.Button("创建", false, () => OnClickCreate(), GUILayout.Width(c_TopBarButtonWidth));
                    EditorUtil.Draw.Button("选择", false, () => OnClickPick(), GUILayout.Width(c_TopBarButtonWidth));
                    EditorUtil.Draw.DisabledGroup(!m_IsDirty, () =>
                    {
                        EditorUtil.Draw.SuccessButton("保存", false, () => OnClickSave(), GUILayout.Width(c_TopBarButtonWidth));
                    });
                    EditorUtil.Draw.Button("打开文件夹", false, () => OnClickRevealInFinder(), GUILayout.Width(c_TopBarWideButtonWidth));
                });
            });
        }

        /// <summary>
        /// "选择"按钮回调：弹 OpenFilePanel 让用户选择已存在的 PipifySettings.asset 并绑定。
        /// 所选文件不在 Unity 项目目录或非 PipifySettingsSO 类型时弹错提示。
        /// </summary>
        private void OnClickPick()
        {
            string abs = EditorUtility.OpenFilePanel("选择 PipifySettings", "Assets", "asset");
            if (string.IsNullOrEmpty(abs)) return;
            string rel = FileUtil.GetProjectRelativePath(abs);
            if (string.IsNullOrEmpty(rel))
            {
                EditorUtility.DisplayDialog("路径错误", "所选文件不在当前 Unity 项目目录中，请重新选择。", "知道了");
                return;
            }
            PipifySettingsSO loaded = EditorUtil.Asset.Operator.LoadAt<PipifySettingsSO>(rel);
            if (loaded == null)
            {
                EditorUtility.DisplayDialog("类型错误", "请选择 PipifySettingsSO 类型资产。", "知道了");
                return;
            }
            RebindSettings(loaded);
        }
    }
}
