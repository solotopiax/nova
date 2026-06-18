/***************************************************************
 * (c) copyright 2026 - 2030, Solotopia
 * All Rights Reserved.
 * -------------------------------------------------------------
 * filename:  LocalizationComponentInspector.Visitors.cs
 * author:    taoye
 * created:   2026/4/10
 * descrip:   本地化组件编辑器面板定制 —— 属性与字段
 ***************************************************************/

using System.Collections.Generic;
using UnityEditor;

namespace NovaFramework.Editor
{
    internal sealed partial class LocalizationComponentInspector : BaseComponentInspector
    {
        /// <summary>
        /// 一级条目标签宽度（项目级统一基准，与 AssetComponentInspector 一致）。
        /// </summary>
        private const float c_LabelWidth = 180f;

        /// <summary>
        /// 语言列表区域标签宽度。
        /// </summary>
        private const float c_LangListLabelWidth = 140f;

        /// <summary>
        /// 文本文件树折叠状态：键为文件夹完整路径，值为该层 Foldout 是否展开。
        /// </summary>
        private readonly Dictionary<string, bool> m_FolderFoldoutState = new Dictionary<string, bool>();

        /// <summary>
        /// 当前本地化管理器类型名称属性。
        /// </summary>
        private SerializedProperty m_CurLocalizationManagerTypeName;

        /// <summary>
        /// 回退语言属性。
        /// </summary>
        private SerializedProperty m_FallbackLanguage;

        /// <summary>
        /// 编辑器语言类型属性。
        /// 仅编辑器内生效，用于本地化测试。
        /// </summary>
        private SerializedProperty m_EditorLanguage;

        /// <summary>
        /// 终端语言类型优先策略属性。
        /// 仅终端运行时生效。
        /// </summary>
        private SerializedProperty m_RuntimeLanguagePrefer;

        /// <summary>
        /// 字体自动适配开关属性。
        /// </summary>
        private SerializedProperty m_AutoFontAdapt;

        /// <summary>
        /// 本地化设置属性。
        /// </summary>
        private SerializedProperty m_LocalizationSettings;

        /// <summary>
        /// 文本数据源目录路径属性。
        /// </summary>
        private SerializedProperty m_TextSourceDirPath;

        /// <summary>
        /// 字体数据源目录路径属性。
        /// </summary>
        private SerializedProperty m_FontSourceDirPath;

        /// <summary>
        /// 文本数据单元设置列表属性。
        /// </summary>
        private SerializedProperty m_TextUnitsSettings;

        /// <summary>
        /// 字体数据单元设置列表属性。
        /// </summary>
        private SerializedProperty m_FontUnitsSettings;

        /// <summary>
        /// 字体数据模板文件路径属性。
        /// </summary>
        private SerializedProperty m_FontTemplatePath;

        /// <summary>
        /// 语言列表 JSON 导出路径属性。
        /// </summary>
        private SerializedProperty m_SupportedLanguagesJsonExportPath;

        /// <summary>
        /// 语言列表资源地址属性。
        /// </summary>
        private SerializedProperty m_SupportedLanguagesAssetLocation;

        /// <summary>
        /// 程序集中所有实现 ILocalizationManager 的类型名称列表，用于下拉选择。
        /// </summary>
        private List<string> m_LocalizationManagerTypeNames;

        /// <summary>
        /// 字体文件树折叠状态：键为文件夹完整路径，值为该层 Foldout 是否展开。
        /// 字体文件树使用此字典，与文本文件树的 m_FolderFoldoutState 区分以避免混淆。
        /// </summary>
        private readonly Dictionary<string, bool> m_FontFolderFoldoutState = new Dictionary<string, bool>();
    }
}
