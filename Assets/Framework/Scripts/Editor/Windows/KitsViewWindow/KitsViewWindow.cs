/***************************************************************
 * (c) copyright 2026 - 2030, Solotopia
 * All Rights Reserved.
 * -------------------------------------------------------------
 * filename:  KitsViewWindow.cs
 * author:    taoye
 * created:   2026/4/22
 * descrip:   KitsView 窗口，展示从 manifest.json 中已安装的 Kit 插件包信息及 Proto 协议
 ***************************************************************/

using UnityEditor;
using UnityEngine;

namespace NovaFramework.Editor
{
    /// <summary>
    /// KitsView 编辑器窗口，从 manifest.json 读取已安装的 Kit 包（com.solotopia.nova.framework.kit 前缀），
    /// 展示版本、描述、依赖和协议文件，同时支持开发者模式（file: 本地路径）和消费者模式（PackageCache）。
    /// </summary>
    internal sealed partial class KitsViewWindow : EditorWindow
    {
        /// <summary>
        /// 打开 KitsView 窗口（菜单入口）。
        /// </summary>
        [MenuItem(c_MenuPath)]
        public static void Open()
        {
            s_Instance = GetWindow<KitsViewWindow>(false, c_WindowTitle, true);
            s_Instance.minSize = new Vector2(600f, 400f);
        }

        /// <summary>
        /// 窗口启用时加载 Kit 列表。
        /// </summary>
        private void OnEnable()
        {
            CollectKitEntries();
        }

        /// <summary>
        /// 绘制窗口界面。
        /// </summary>
        private void OnGUI()
        {
            EnsureStyles();

            EditorUtil.Draw.Layout.Horizontal(() =>
            {
                EditorUtil.Draw.Space(8f);
                EditorUtil.Draw.Label("已安装 Kits 套件", EditorStyles.whiteLargeLabel, false, GUILayout.Height(24f));
                GUILayout.FlexibleSpace();
                EditorUtil.Draw.Button("刷新", 60f, false, CollectKitEntries);
                EditorUtil.Draw.Space(8f);
            });

            if (m_KitEntries == null || m_KitEntries.Count == 0)
            {
                EditorUtil.Draw.Space(20f);
                EditorUtil.Draw.HelpBox(MessageType.Info, new[] { "未找到任何 Kit 包（前缀: " + c_KitPackagePrefix + "）" }, false);
                return;
            }

            m_ScrollPos = EditorGUILayout.BeginScrollView(m_ScrollPos);
            for (int i = 0; i < m_KitEntries.Count; i++)
            {
                DrawKitEntry(m_KitEntries[i]);
            }
            EditorUtil.Draw.Space(10f);
            EditorGUILayout.EndScrollView();
        }

        /// <summary>
        /// 窗口销毁时清除单例引用。
        /// </summary>
        private void OnDestroy()
        {
            s_Instance = null;
        }
    }
}
