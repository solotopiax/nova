/***************************************************************
 * (c) copyright 2026 - 2030, Solotopia
 * All Rights Reserved.
 * -------------------------------------------------------------
 * filename:  PlugPalsWindow.cs
 * author:    taoye
 * created:   2026/4/8
 * descrip:   PlugPals 私有 Verdaccio 仓库 UPM 包管理窗口
 ***************************************************************/

using UnityEditor;
using UnityEngine;

namespace NovaFramework.Editor
{
    /// <summary>
    /// PlugPals 编辑器窗口，用于管理私有 Verdaccio 仓库中的 UPM 包。
    /// 提供包列表浏览、搜索过滤、一键安装/卸载/升级功能。
    /// </summary>
    public sealed partial class PlugPalsWindow : EditorWindow
    {
        /// <summary>
        /// 打开 PlugPals 窗口（菜单入口）。
        /// </summary>
        [MenuItem(c_MenuPath)]
        public static void Open()
        {
            s_Instance = GetWindow<PlugPalsWindow>(false, c_WindowTitle, true);
            s_Instance.minSize = new Vector2(1000f, 400f);
            s_Instance.FetchPackages();
        }

        /// <summary>
        /// 打开 PlugPals 窗口并指定初始页签（已安装 / 全部）。
        /// 在 FetchPackages 之前写入 m_ShowInstalledOnly，FetchPackagesAsync 完成后 ApplyFilter 会自动落到对应页签。
        /// </summary>
        /// <param name="showInstalledOnly">true 进入"已安装"页签；false 进入"全部"页签。</param>
        public static void Open(bool showInstalledOnly)
        {
            s_Instance = GetWindow<PlugPalsWindow>(false, c_WindowTitle, true);
            s_Instance.minSize = new Vector2(1000f, 400f);
            s_Instance.m_ShowInstalledOnly = showInstalledOnly;
            s_Instance.FetchPackages();
        }

        /// <summary>
        /// 打开 PlugPals 窗口并直接进入内部云仓库页签。
        /// </summary>
        public static void OpenInternalRegistry()
        {
            s_Instance = GetWindow<PlugPalsWindow>(false, c_WindowTitle, true);
            s_Instance.minSize = new Vector2(1000f, 400f);
            s_Instance.m_SelectedCategory = EditorUtil.PlugPals.PackageCategory.All;
            s_Instance.m_ShowInstalledOnly = false;
            s_Instance.m_ShowInternalOnly = true;
            s_Instance.FetchPackages();
        }

        /// <summary>
        /// 窗口启用时加载 registry 配置并检查是否需要拉取包列表。
        /// </summary>
        private void OnEnable()
        {
            EditorUtil.PlugPals.RegistriesConfig registries = EditorUtil.PlugPals.LoadRegistries();
            m_ExternalUrl = registries.externalUrl;
            m_ExternalName = registries.externalName;
            m_InternalUrl = registries.internalUrl;
            m_InternalName = registries.internalName;

            if (m_ExternalPackages == null && m_InternalPackages == null)
            {
                FetchPackages();
            }
        }

        /// <summary>
        /// 绘制窗口界面。
        /// </summary>
        private void OnGUI()
        {
            EnsureStyles();
            DrawMainTitle();
            DrawToolbar();
            DrawCategoryTabs();
            EditorUtil.Draw.Space(2f);

            if (m_IsFetching)
            {
                EditorUtil.Draw.Layout.Vertical(() =>
                {
                    GUILayout.FlexibleSpace();
                    EditorUtil.Draw.Layout.Horizontal(() =>
                    {
                        GUILayout.FlexibleSpace();
                        EditorUtil.Draw.Label("正在加载包列表，请稍等...", false);
                        GUILayout.FlexibleSpace();
                    });
                    GUILayout.FlexibleSpace();
                });
                Repaint();
                return;
            }

            if (!string.IsNullOrEmpty(m_ErrorMessage) && (m_FilteredPackages == null || m_FilteredPackages.Count == 0))
            {
                EditorUtil.Draw.Space(10f);
                EditorUtil.Draw.HelpBox(MessageType.Error, new[] { m_ErrorMessage }, false);
                EditorUtil.Draw.Space(4f);
                EditorUtil.Draw.Button("重试", 80f, false, FetchPackages);
                return;
            }

            DrawRepoWarnings();

            if (m_FilteredPackages != null)
            {
                DrawHeader();
                m_ScrollPos = EditorGUILayout.BeginScrollView(m_ScrollPos);
                for (int i = 0; i < m_FilteredPackages.Count; i++)
                {
                    DrawPackageCard(m_FilteredPackages[i]);
                }
                EditorUtil.Draw.Space(10f);
                EditorGUILayout.EndScrollView();
            }
        }

        /// <summary>
        /// 窗口销毁时取消进行中的请求并清除单例引用。
        /// </summary>
        private void OnDestroy()
        {
            CancelFetchRequest();
            s_Instance = null;
        }
    }
}
