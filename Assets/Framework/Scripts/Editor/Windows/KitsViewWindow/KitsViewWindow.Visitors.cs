/***************************************************************
 * (c) copyright 2026 - 2030, Solotopia
 * All Rights Reserved.
 * -------------------------------------------------------------
 * filename:  KitsViewWindow.Visitors.cs
 * author:    taoye
 * created:   2026/4/22
 * descrip:   KitsView 窗口字段声明
 ***************************************************************/

using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

namespace NovaFramework.Editor
{
    internal sealed partial class KitsViewWindow : EditorWindow
    {
        /// <summary>
        /// Kit 包名前缀过滤条件。
        /// </summary>
        private const string c_KitPackagePrefix = "com.solotopia.nova.framework.kit";

        /// <summary>
        /// manifest.json 相对于项目根目录的路径。
        /// </summary>
        private const string c_ManifestPath = "Packages/manifest.json";

        /// <summary>
        /// packages-lock.json 相对于项目根目录的路径。
        /// </summary>
        private const string c_PackagesLockPath = "Packages/packages-lock.json";

        /// <summary>
        /// 菜单路径。
        /// </summary>
        private const string c_MenuPath = "Nova/Open KitsView";

        /// <summary>
        /// 窗口标题。
        /// </summary>
        private const string c_WindowTitle = "Nova · KitsView";

        /// <summary>
        /// 标签列宽度。
        /// </summary>
        private const float c_LabelWidth = 120f;

        /// <summary>
        /// 窗口单例。
        /// </summary>
        private static KitsViewWindow s_Instance;

        /// <summary>
        /// 所有 Kit 包条目。
        /// </summary>
        private List<KitEntry> m_KitEntries;

        /// <summary>
        /// 滚动位置。
        /// </summary>
        private Vector2 m_ScrollPos;

        /// <summary>
        /// 标题样式（Kit 条目名称）。
        /// </summary>
        private GUIStyle m_TitleStyle;

        /// <summary>
        /// 标签样式（字段名称如 Name、Version）。
        /// </summary>
        private GUIStyle m_LabelStyle;

        /// <summary>
        /// 值样式（字段对应的值）。
        /// </summary>
        private GUIStyle m_ValueStyle;

        /// <summary>
        /// 描述样式（长文本描述）。
        /// </summary>
        private GUIStyle m_DescStyle;

        /// <summary>
        /// Proto 文件名样式。
        /// </summary>
        private GUIStyle m_ProtoFileNameStyle;

        /// <summary>
        /// Proto 内容样式（等宽小字）。
        /// </summary>
        private GUIStyle m_ProtoContentStyle;

        /// <summary>
        /// 区域标题样式（Proto 区域标题）。
        /// </summary>
        private GUIStyle m_SectionTitleStyle;

        /// <summary>
        /// 依赖项样式。
        /// </summary>
        private GUIStyle m_DependencyStyle;
    }
}
