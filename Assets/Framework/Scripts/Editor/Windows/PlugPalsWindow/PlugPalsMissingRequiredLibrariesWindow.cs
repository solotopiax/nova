/***************************************************************
 * (c) copyright 2026 - 2030, Solotopia
 * All Rights Reserved.
 * -------------------------------------------------------------
 * filename:  PlugPalsMissingRequiredLibrariesWindow.cs
 * author:    taoye
 * created:   2026/6/15
 * descrip:   PlugPals 缺失必须三方库提示窗口
 ***************************************************************/

using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

namespace NovaFramework.Editor
{
    /// <summary>
    /// 展示 Nova 包依赖但当前工程尚未安装的必须三方库。
    /// </summary>
    public sealed class PlugPalsMissingRequiredLibrariesWindow : EditorWindow
    {
        private const string c_WindowTitle = "Nova · 缺失必须三方库";
        private static readonly List<EditorUtil.PlugPals.MissingRequiredLibraryInfo> s_Items = new List<EditorUtil.PlugPals.MissingRequiredLibraryInfo>();

        private Vector2 m_ScrollPos;

        public static void Open(IReadOnlyList<EditorUtil.PlugPals.MissingRequiredLibraryInfo> items)
        {
            s_Items.Clear();
            if (items != null)
            {
                for (int i = 0; i < items.Count; i++)
                {
                    s_Items.Add(items[i]);
                }
            }

            PlugPalsMissingRequiredLibrariesWindow window = GetWindow<PlugPalsMissingRequiredLibrariesWindow>(true, c_WindowTitle, true);
            window.minSize = new Vector2(720f, 360f);
            window.Show();
        }

        private void OnGUI()
        {
            EditorUtil.Draw.Space(8f);
            EditorUtil.Draw.Label("安装的 Nova package 依赖以下必须三方库，但当前工程尚未安装。", EditorStyles.boldLabel, false);
            EditorUtil.Draw.Space(4f);
            EditorUtil.Draw.HelpBox(MessageType.Warning, new[]
            {
                "当前 package 依赖商业性质第三方库。",
                "如果你是 Solotopia 成员，请到 PlugPalsWindow 中“内部云仓库”自行安装已购买的插件。",
                "如果你是非 Solotopia 成员，请参考购买地址自行获取并安装对应商业库后再导入。"
            }, false);
            EditorUtil.Draw.Space(6f);

            m_ScrollPos = EditorGUILayout.BeginScrollView(m_ScrollPos);
            for (int i = 0; i < s_Items.Count; i++)
            {
                DrawItem(s_Items[i]);
            }
            EditorGUILayout.EndScrollView();

            EditorUtil.Draw.Space(8f);
            EditorUtil.Draw.Layout.Horizontal(() =>
            {
                GUILayout.FlexibleSpace();
                EditorUtil.Draw.Button("打开内部云仓库", 140f, false, PlugPalsWindow.OpenInternalRegistry);
                EditorUtil.Draw.Button("关闭", 80f, false, Close);
            });
        }

        private static void DrawItem(EditorUtil.PlugPals.MissingRequiredLibraryInfo item)
        {
            if (item == null)
            {
                return;
            }

            EditorUtil.Draw.Layout.Vertical("box", () =>
            {
                EditorUtil.Draw.Label(item.DisplayName + "  (" + item.PackageName + ")", EditorStyles.boldLabel, false);
                EditorUtil.Draw.Label("依赖版本：" + (string.IsNullOrEmpty(item.RequiredVersion) ? "未声明" : item.RequiredVersion), false);
                EditorUtil.Draw.Label("依赖来源：" + (string.IsNullOrEmpty(item.DependentPackageDisplayName) ? item.DependentPackageName : item.DependentPackageDisplayName), false);
                if (!string.IsNullOrEmpty(item.PurchaseUrl))
                {
                    EditorUtil.Draw.Button("打开购买 / 下载地址", 150f, false, () => Application.OpenURL(item.PurchaseUrl));
                }
            });
        }
    }
}
