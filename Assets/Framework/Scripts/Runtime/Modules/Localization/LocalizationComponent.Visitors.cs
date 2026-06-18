/***************************************************************
 * (c) copyright 2026 - 2030, Solotopia
 * All Rights Reserved.
 * -------------------------------------------------------------
 * filename:  LocalizationComponent.Visitors.cs
 * author:    taoye
 * created:   2026/4/10
 * descrip:   本地化组件-访问器
 ***************************************************************/

using UnityEngine;

namespace NovaFramework.Runtime
{
    /// <summary>
    /// 本地化组件。
    /// </summary>
    public sealed partial class LocalizationComponent : FrameworkComponent
    {
        /// <summary>
        /// 当前本地化管理器类型名称。
        /// </summary>
        [Tooltip("本地化管理器实现类全名")]
        [SerializeField]
        private string m_CurLocalizationManagerTypeName = "NovaFramework.Runtime.LocalizationManager";
        public string CurLocalizationManagerTypeName => m_CurLocalizationManagerTypeName;

        /// <summary>
        /// 编辑器语言类型。
        /// 仅编辑器内生效；非 Unspecified 且在支持列表中时强制使用此语言。
        /// </summary>
        [SerializeField]
        private Language m_EditorLanguage = Language.Unspecified;

        /// <summary>
        /// 终端语言类型优先策略。
        /// 仅终端运行时生效；启用时按 持久化 > 系统 > 回退 解析；禁用时强制使用回退语言。
        /// </summary>
        [SerializeField]
        private bool m_RuntimeLanguagePrefer = true;

        /// <summary>
        /// 回退语言。
        /// </summary>
        [SerializeField]
        private Language m_FallbackLanguage = Language.English;

        /// <summary>
        /// 是否启用字体自动适配。
        /// </summary>
        [Tooltip("启用后根据当前语言自动适配字体")]
        [SerializeField]
        private bool m_AutoFontAdapt = false;

        /// <summary>
        /// 本地化设置（包含文本和字体两组数据源配置）。
        /// </summary>
        [SerializeField]
        private LocalizationSettings m_LocalizationSettings = new LocalizationSettings();
        public LocalizationSettings LocalizationSettings => m_LocalizationSettings;

        /// <summary>
        /// 持久化存储分类名称（固定值，不通过 Inspector 配置）。
        /// </summary>
        internal const string c_PersistClassifyName = "LocalizationCommon";

        /// <summary>
        /// 持久化存储条目键名（固定值，不通过 Inspector 配置）。
        /// </summary>
        internal const string c_PersistItemKey = "LocalizationLanguage";

        /// <summary>
        /// 语言列表资源地址。
        /// </summary>
        [Tooltip("语言列表 JSON 资源地址")]
        [SerializeField]
        private string m_SupportedLanguagesAssetLocation;

#if UNITY_EDITOR
        /// <summary>
        /// 文本数据模板文件路径（仅编辑器使用）。
        /// </summary>
        [Tooltip("文本数据模板文件路径（仅编辑器）")]
        [SerializeField]
        private string m_TextTemplatePath;

        /// <summary>
        /// 字体数据模板文件路径（仅编辑器使用）。
        /// </summary>
        [Tooltip("字体数据模板文件路径（仅编辑器）")]
        [SerializeField]
        private string m_FontTemplatePath;

        /// <summary>
        /// 语言列表 JSON 导出路径（仅编辑器使用）。
        /// </summary>
        [Tooltip("语言列表 JSON 导出路径（仅编辑器）")]
        [SerializeField]
        private string m_SupportedLanguagesJsonExportPath;
#endif

        /// <summary>
        /// 本地化管理器实例。
        /// </summary>
        private ILocalizationManager m_LocalizationManager;

        /// <summary>
        /// 获取当前语言。
        /// </summary>
        public Language Language => m_LocalizationManager?.Language ?? Language.Unspecified;

        /// <summary>
        /// 获取当前语言名称。
        /// </summary>
        public string LanguageName => m_LocalizationManager?.LanguageName ?? string.Empty;

        /// <summary>
        /// 获取当前语言在已支持语言列表中的索引。
        /// </summary>
        public int LanguageIndex => m_LocalizationManager?.LanguageIndex ?? -1;

        /// <summary>
        /// 获取系统语言（映射后的 Language 枚举值）。
        /// </summary>
        public Language SystemLanguage => m_LocalizationManager?.SystemLanguage ?? Language.Unspecified;

        /// <summary>
        /// 获取是否启用字体自动适配。
        /// </summary>
        public bool AutoFontAdapt => m_LocalizationManager?.AutoFontAdapt ?? false;
    }
}
