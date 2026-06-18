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
        /// Unity SystemLanguage 到框架 Language 的映射表。
        /// 注意：对于 SystemLanguage 中不存在的语言（如 Hindi、Malay、Filipino、PortugueseBrazil），
        /// 待 SDK 模块 NativeHelper 就绪后可通过原生接口获取更精确的语言编码进行补充映射。
        /// </summary>
        private static readonly Dictionary<UnityEngine.SystemLanguage, Language> s_SystemLanguageMap = new Dictionary<UnityEngine.SystemLanguage, Language>
        {
            { UnityEngine.SystemLanguage.Afrikaans, Language.Afrikaans },
            { UnityEngine.SystemLanguage.Arabic, Language.Arabic },
            { UnityEngine.SystemLanguage.Basque, Language.Basque },
            { UnityEngine.SystemLanguage.Belarusian, Language.Belarusian },
            { UnityEngine.SystemLanguage.Bulgarian, Language.Bulgarian },
            { UnityEngine.SystemLanguage.Catalan, Language.Catalan },
            { UnityEngine.SystemLanguage.Chinese, Language.ChineseSimplified },
            { UnityEngine.SystemLanguage.ChineseSimplified, Language.ChineseSimplified },
            { UnityEngine.SystemLanguage.ChineseTraditional, Language.ChineseTraditional },
            { UnityEngine.SystemLanguage.Czech, Language.Czech },
            { UnityEngine.SystemLanguage.Danish, Language.Danish },
            { UnityEngine.SystemLanguage.Dutch, Language.Dutch },
            { UnityEngine.SystemLanguage.English, Language.English },
            { UnityEngine.SystemLanguage.Estonian, Language.Estonian },
            { UnityEngine.SystemLanguage.Faroese, Language.Faroese },
            { UnityEngine.SystemLanguage.Finnish, Language.Finnish },
            { UnityEngine.SystemLanguage.French, Language.French },
            { UnityEngine.SystemLanguage.German, Language.German },
            { UnityEngine.SystemLanguage.Greek, Language.Greek },
            { UnityEngine.SystemLanguage.Hebrew, Language.Hebrew },
            { UnityEngine.SystemLanguage.Hungarian, Language.Hungarian },
            { UnityEngine.SystemLanguage.Icelandic, Language.Icelandic },
            { UnityEngine.SystemLanguage.Indonesian, Language.Indonesian },
            { UnityEngine.SystemLanguage.Italian, Language.Italian },
            { UnityEngine.SystemLanguage.Japanese, Language.Japanese },
            { UnityEngine.SystemLanguage.Korean, Language.Korean },
            { UnityEngine.SystemLanguage.Latvian, Language.Latvian },
            { UnityEngine.SystemLanguage.Lithuanian, Language.Lithuanian },
            { UnityEngine.SystemLanguage.Norwegian, Language.Norwegian },
            { UnityEngine.SystemLanguage.Polish, Language.Polish },
            { UnityEngine.SystemLanguage.Portuguese, Language.PortuguesePortugal },
            { UnityEngine.SystemLanguage.Romanian, Language.Romanian },
            { UnityEngine.SystemLanguage.Russian, Language.Russian },
            { UnityEngine.SystemLanguage.SerboCroatian, Language.SerboCroatian },
            { UnityEngine.SystemLanguage.Slovak, Language.Slovak },
            { UnityEngine.SystemLanguage.Slovenian, Language.Slovenian },
            { UnityEngine.SystemLanguage.Spanish, Language.Spanish },
            { UnityEngine.SystemLanguage.Swedish, Language.Swedish },
            { UnityEngine.SystemLanguage.Thai, Language.Thai },
            { UnityEngine.SystemLanguage.Turkish, Language.Turkish },
            { UnityEngine.SystemLanguage.Ukrainian, Language.Ukrainian },
            { UnityEngine.SystemLanguage.Vietnamese, Language.Vietnamese },
        };

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
        public override Language SystemLanguage => MapUnitySystemLanguage(UnityEngine.Application.systemLanguage);

        /// <summary>
        /// 获取是否启用字体自动适配。
        /// </summary>
        public override bool AutoFontAdapt => m_Config?.AutoFontAdapt ?? false;
    }
}
