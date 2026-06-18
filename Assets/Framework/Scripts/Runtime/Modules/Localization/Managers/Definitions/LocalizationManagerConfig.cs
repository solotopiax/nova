/***************************************************************
 * (c) copyright 2026 - 2030, Solotopia
 * All Rights Reserved.
 * -------------------------------------------------------------
 * filename:  LocalizationManagerConfig.cs
 * author:    taoye
 * created:   2026/4/10
 * descrip:   本地化管理器配置
 ***************************************************************/

using System.Collections.Generic;

namespace NovaFramework.Runtime
{
    /// <summary>
    /// 本地化管理器配置。
    /// </summary>
    public class LocalizationManagerConfig
    {
        /// <summary>
        /// 编辑器语言类型；非 Unspecified 且在支持列表时强制使用（仅编辑器生效）。
        /// </summary>
        public Language EditorLanguage { get; set; } = Language.Unspecified;

        /// <summary>
        /// 终端语言类型优先策略；启用按 持久化>系统>回退 解析，禁用强制回退（仅终端生效）。
        /// </summary>
        public bool RuntimeLanguagePrefer { get; set; } = true;

        /// <summary>
        /// 回退语言（当持久化和系统语言都不在支持列表中时使用）。
        /// </summary>
        public Language FallbackLanguage { get; set; } = Language.English;

        /// <summary>
        /// 是否启用字体自动适配。
        /// </summary>
        public bool AutoFontAdapt { get; set; } = false;

        /// <summary>
        /// 文本数据单元设置列表。
        /// </summary>
        public List<LocalizationTextUnitSetting> TextUnitSettings { get; set; }

        /// <summary>
        /// 字体数据单元设置列表。
        /// </summary>
        public List<LocalizationFontUnitSetting> FontUnitSettings { get; set; }

        /// <summary>
        /// 语言列表资源地址。
        /// </summary>
        public string SupportedLanguagesAssetLocation { get; set; }

        /// <summary>
        /// 持久化存储分类名称。
        /// </summary>
        public string PersistClassifyName { get; set; }

        /// <summary>
        /// 持久化存储条目键名。
        /// </summary>
        public string PersistItemKey { get; set; }
    }
}
