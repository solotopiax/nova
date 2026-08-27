/***************************************************************
 * (c) copyright 2026 - 2030, Solotopia
 * All Rights Reserved.
 * -------------------------------------------------------------
 * filename:  LocalizationManager.Visitors.cs
 * author:    taoye
 * created:   2026/4/10
 * descrip:   本地化管理器-访问器
 ***************************************************************/

using System;
using System.Collections.Generic;
using UnityEngine;

namespace NovaFramework.Runtime
{
    /// <summary>
    /// 本地化管理器。
    /// </summary>
    internal sealed partial class LocalizationManager : LocalizationManagerBase
    {
        /// <summary>
        /// 已支持的语言列表。
        /// </summary>
        private readonly List<Language> m_SupportedLanguages = new List<Language>();

        /// <summary>
        /// 当前语言的文本键值对数据（文本名称, 本地化内容）。
        /// </summary>
        private readonly Dictionary<string, string> m_LanguageTexts = new Dictionary<string, string>();

        /// <summary>
        /// 所有语言的字体配置数据（语言, 字体数据行列表）。
        /// </summary>
        private readonly Dictionary<Language, List<ILocalizationFontRow>> m_FontDatas = new Dictionary<Language, List<ILocalizationFontRow>>();

        /// <summary>
        /// Luban 文本数据持久缓存，LubanDataReceiver 写入，LubanTablesLoader 消费。
        /// </summary>
        private readonly LubanDataCache m_TextDataPersistCache = new LubanDataCache();

        /// <summary>
        /// 已构建的 Luban 文本表对象（表类型, ITable 实例）。
        /// </summary>
        private readonly Dictionary<Type, ITable> m_TextTableDatas = new Dictionary<Type, ITable>();

        /// <summary>
        /// 当前语言。
        /// </summary>
        private Language m_Language = Language.Unspecified;

        /// <summary>
        /// 管理器配置。
        /// </summary>
        private LocalizationManagerConfig m_Config;

        /// <summary>
        /// 文本数据单元设置列表。
        /// </summary>
        private List<LocalizationTextUnitSetting> m_TextUnitSettings;

        /// <summary>
        /// 字体数据单元设置列表。
        /// </summary>
        private List<LocalizationFontUnitSetting> m_FontUnitSettings;

        /// <summary>
        /// 资源管理器引用（Initialize 时从 FrameworkManagersGroup 获取）。
        /// </summary>
        private IAssetManager m_AssetManager;

        /// <summary>
        /// PlayerPrefs 持久化管理器引用（Initialize 时从 FrameworkManagersGroup 获取）。
        /// </summary>
        private IPlayerPrefsManager m_PlayerPrefsManager;

        /// <summary>
        /// 事件管理器引用（Initialize 时从 FrameworkManagersGroup 获取）。
        /// </summary>
        private IEventManager m_EventManager;

        /// <summary>
        /// 语言切换版本号（用于异步切换重入保护）。
        /// </summary>
        private int m_LanguageSwitchVersion;

        /// <summary>
        /// 获取当前语言。
        /// </summary>
        public override Language Language => m_Language;

        /// <summary>
        /// 获取当前语言名称。
        /// </summary>
        public override string LanguageName => m_Language.ToString();

        /// <summary>
        /// 获取当前语言在已支持语言列表中的索引。
        /// </summary>
        public override int LanguageIndex => m_SupportedLanguages.IndexOf(m_Language);

        /// <summary>
        /// 获取系统语言（映射后的 Language 枚举值）。
        /// </summary>
        public override Language SystemLanguage => LocalizationLanguageResolver.MapSystemLanguage(UnityEngine.Application.systemLanguage);

        /// <summary>
        /// 获取是否启用字体自动适配。
        /// </summary>
        public override bool AutoFontAdapt => m_Config?.AutoFontAdapt ?? false;
    }
}
