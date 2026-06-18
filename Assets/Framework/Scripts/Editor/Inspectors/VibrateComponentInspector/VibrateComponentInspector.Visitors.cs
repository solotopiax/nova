/***************************************************************
 * (c) copyright 2026 - 2030, Solotopia
 * All Rights Reserved.
 * -------------------------------------------------------------
 * filename:  VibrateComponentInspector.Visitors.cs
 * author:    taoye
 * created:   2026/4/9
 * descrip:   振动组件编辑器面板定制 —— 属性与字段
 ***************************************************************/

using System.Collections.Generic;
using UnityEditor;

namespace NovaFramework.Editor
{
    internal sealed partial class VibrateComponentInspector : BaseComponentInspector
    {
        /// <summary>
        /// Emphasis 模板文件名。
        /// </summary>
        private const string c_EmphasisTemplateFileName = "VibrateEmphasisTemplate.xlsx";

        /// <summary>
        /// Custom 模板文件名。
        /// </summary>
        private const string c_CustomTemplateFileName = "VibrateCustomTemplate.xlsx";

        /// <summary>
        /// 当前管理器类型名称。
        /// </summary>
        private SerializedProperty m_CurManagerTypeName;

        /// <summary>
        /// 振动设置。
        /// </summary>
        private SerializedProperty m_Settings;

        /// <summary>
        /// Emphasis 数据源目录路径。
        /// </summary>
        private SerializedProperty m_EmphasisSourceDirPath;

        /// <summary>
        /// Custom 数据源目录路径。
        /// </summary>
        private SerializedProperty m_CustomSourceDirPath;

        /// <summary>
        /// Emphasis 振动单元设置列表。
        /// </summary>
        private SerializedProperty m_EmphasisUnitsSettings;

        /// <summary>
        /// Custom 振动单元设置列表。
        /// </summary>
        private SerializedProperty m_CustomUnitsSettings;

        /// <summary>
        /// 管理器所有类型名称。
        /// </summary>
        private List<string> m_ManagerTypeNames;

        /// <summary>
        /// Emphasis 文件树折叠状态：键为文件夹完整路径，值为是否展开。
        /// </summary>
        private readonly Dictionary<string, bool> m_EmphasisFolderFoldoutState = new Dictionary<string, bool>();

        /// <summary>
        /// Custom 文件树折叠状态：键为文件夹完整路径，值为是否展开。
        /// </summary>
        private readonly Dictionary<string, bool> m_CustomFolderFoldoutState = new Dictionary<string, bool>();

    }
}
