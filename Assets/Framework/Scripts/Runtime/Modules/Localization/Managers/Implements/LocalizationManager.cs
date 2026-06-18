/***************************************************************
 * (c) copyright 2026 - 2030, Solotopia
 * All Rights Reserved.
 * -------------------------------------------------------------
 * filename:  LocalizationManager.cs
 * author:    taoye
 * created:   2026/4/10
 * descrip:   本地化管理器
 ***************************************************************/

using System;
using System.Collections.Generic;
using Cysharp.Threading.Tasks;
using UnityEngine;

namespace NovaFramework.Runtime
{
    /// <summary>
    /// 本地化管理器。
    /// </summary>
    internal sealed partial class LocalizationManager : LocalizationManagerBase
    {
        /// <summary>
        /// 构造函数（无参，由 TypeCreator 反射调用）。
        /// </summary>
        public LocalizationManager()
        {
        }

        /// <summary>
        /// 初始化本地化管理器。
        /// </summary>
        /// <param name="config">配置信息。</param>
        public override void Initialize(LocalizationManagerConfig config)
        {
            if (config == null)
            {
                throw new ArgumentNullException(nameof(config));
            }

            m_Config = config;
            m_TextUnitSettings = config.TextUnitSettings ?? new List<LocalizationTextUnitSetting>();
            m_FontUnitSettings = config.FontUnitSettings ?? new List<LocalizationFontUnitSetting>();
            m_AssetManager = FrameworkManagersGroup.GetManager<IAssetManager>();
            m_PlayerPrefsManager = FrameworkManagersGroup.GetManager<IPlayerPrefsManager>();
            m_EventManager = FrameworkManagersGroup.GetManager<IEventManager>();

            m_Language = Language.Unspecified;

            Log.Debug(LogTag.Localization, "本地化管理器初始化完成，回退语言='{0}'，字体自动适配='{1}'。", config.FallbackLanguage, config.AutoFontAdapt);
        }

        /// <summary>
        /// 管理器轮询（本地化模块无逐帧逻辑）。
        /// </summary>
        public override void Update()
        {
        }

        /// <summary>
        /// 关闭并清理管理器。
        /// </summary>
        public override void Shutdown()
        {
            m_SupportedLanguages.Clear();
            m_LanguageTexts.Clear();
            m_FontDatas.Clear();
            m_TextDataPersistCache?.Clear();
            m_TextTableDatas.Clear();
            m_Language = Language.Unspecified;
            m_AssetManager = null;
            m_PlayerPrefsManager = null;
            m_EventManager = null;
            m_Config = null;
            m_TextUnitSettings = null;
            m_FontUnitSettings = null;
        }

        /// <summary>
        /// 异步加载本地化基础数据（语言列表 + 字体配置）。
        /// </summary>
        /// <returns>是否加载成功。</returns>
        public override async UniTask<bool> LoadAsync()
        {
            await LoadSupportedLanguagesAsync();

            bool fontResult = await LoadFontDataAsync();
            if (!fontResult)
            {
                Log.Warning(LogTag.Localization, "字体数据加载失败，语言列表仍可使用。");
            }

            return true;
        }

        /// <summary>
        /// 同步加载本地化基础数据（语言列表 + 字体配置）。
        /// </summary>
        public override void LoadSync()
        {
            LoadSupportedLanguagesSync();
            LoadFontDataSync();
        }

        /// <summary>
        /// 根据优先级决定当前语言并同步加载对应文本数据。供 Procedure 调用完成语言初始化。
        /// </summary>
        /// <returns>解析并设置后的语言。</returns>
        public override Language InitCurrentLanguageSync()
        {
            Language resolved = ResolveLanguage();
            SetLanguageSync(resolved);
            return resolved;
        }

        /// <summary>
        /// 根据优先级决定当前语言并异步加载对应文本数据。供 Procedure 调用完成语言初始化。
        /// </summary>
        /// <returns>解析并设置后的语言。</returns>
        public override async UniTask<Language> InitCurrentLanguageAsync()
        {
            Language resolved = ResolveLanguage();
            await SetLanguageAsync(resolved);
            return resolved;
        }

        /// <summary>
        /// 检查指定语言是否在已支持列表中。
        /// </summary>
        /// <param name="language">待检查的语言。</param>
        /// <returns>是否已支持。</returns>
        public override bool HasSupportedLanguage(Language language)
        {
            return m_SupportedLanguages.Contains(language);
        }

        /// <summary>
        /// 获取所有已支持语言的只读列表。
        /// </summary>
        /// <returns>已支持语言只读列表。</returns>
        public override IReadOnlyList<Language> GetSupportedLanguages()
        {
            return m_SupportedLanguages;
        }

        /// <summary>
        /// 根据运行环境和配置策略决定当前语言。
        /// 编辑器：若 EditorLanguage 非 Unspecified 且在支持列表中则强制使用，否则走通用链。
        /// 终端：RuntimeLanguagePrefer 启用时走 持久化 > 系统 > 回退 通用链；禁用时强制使用回退语言。
        /// </summary>
        /// <returns>解析后的语言。</returns>
        public override Language ResolveLanguage()
        {
            if (Application.isEditor)
            {
                Language editorLang = m_Config?.EditorLanguage ?? Language.Unspecified;
                if (editorLang != Language.Unspecified && HasSupportedLanguage(editorLang))
                {
                    return editorLang;
                }

                return ResolveByPersistThenSystem();
            }

            if (m_Config?.RuntimeLanguagePrefer == false)
            {
                Language fallback = m_Config.FallbackLanguage;
                if (HasSupportedLanguage(fallback))
                {
                    return fallback;
                }

                if (m_SupportedLanguages.Count > 0)
                {
                    return m_SupportedLanguages[0];
                }

                return m_Config.FallbackLanguage;
            }

            return ResolveByPersistThenSystem();
        }

        /// <summary>
        /// 同步切换语言。
        /// </summary>
        /// <param name="language">目标语言。</param>
        public override void SetLanguageSync(Language language)
        {
            if (!ValidateLanguageSwitch(language))
            {
                return;
            }

            bool loaded = TryLoadLanguageTextsSync(language);
            if (!loaded)
            {
                Log.Error(LogTag.Localization, "同步切换语言失败，语言 '{0}' 的文本数据加载后为空，当前语言 '{1}' 保持不变。", language, m_Language);
                return;
            }

            ApplyLanguageSwitchResult(language);
        }

        /// <summary>
        /// 异步切换语言。
        /// </summary>
        /// <param name="language">目标语言。</param>
        /// <returns>异步任务。</returns>
        public override async UniTask SetLanguageAsync(Language language)
        {
            if (!ValidateLanguageSwitch(language))
            {
                return;
            }

            m_LanguageSwitchVersion++;
            int currentVersion = m_LanguageSwitchVersion;

            bool loaded = await TryLoadLanguageTextsAsync(language);

            if (m_LanguageSwitchVersion != currentVersion)
            {
                Log.Debug(LogTag.Localization, "异步切换语言 '{0}' 已被更新的切换请求覆盖，当前请求终止。", language);
                return;
            }

            if (!loaded)
            {
                Log.Error(LogTag.Localization, "异步切换语言失败，语言 '{0}' 的文本数据加载后为空，当前语言 '{1}' 保持不变。", language, m_Language);
                return;
            }

            ApplyLanguageSwitchResult(language);
        }

        /// <summary>
        /// 检查指定名称是否存在于当前语言数据中。
        /// </summary>
        /// <param name="name">文本名称。</param>
        /// <returns>是否存在。</returns>
        public override bool HasText(string name)
        {
            if (string.IsNullOrEmpty(name))
            {
                return false;
            }

            return m_LanguageTexts.ContainsKey(name);
        }

        /// <summary>
        /// 获取指定名称对应的本地化文本。
        /// </summary>
        /// <param name="name">文本名称。</param>
        /// <returns>本地化文本，不存在时返回名称本身。</returns>
        public override string GetText(string name)
        {
            if (string.IsNullOrEmpty(name))
            {
                return string.Empty;
            }

            if (m_LanguageTexts.TryGetValue(name, out string value))
            {
                return value;
            }

            Log.Warning(LogTag.Localization, "本地化文本 '{0}' 不存在于当前语言 '{1}' 的数据中，返回名称本身。", name, m_Language);
            return name;
        }

        /// <summary>
        /// 检查是否包含指定类型的本地化文本表。
        /// </summary>
        /// <typeparam name="T">Luban 生成的 TbXxx 表类型。</typeparam>
        /// <returns>是否存在。</returns>
        public override bool HasTexts<T>()
        {
            return m_TextTableDatas.ContainsKey(typeof(T));
        }

        /// <summary>
        /// 检查是否包含指定表名称的本地化文本表。
        /// </summary>
        /// <param name="tbName">表类型短名称（如 "TbCommon"）。</param>
        /// <returns>是否存在。</returns>
        public override bool HasTexts(string tbName)
        {
            if (string.IsNullOrEmpty(tbName))
            {
                return false;
            }

            foreach (var kvp in m_TextTableDatas)
            {
                if (kvp.Key.Name == tbName)
                {
                    return true;
                }
            }

            return false;
        }

        /// <summary>
        /// 获取指定类型的本地化文本表。
        /// </summary>
        /// <typeparam name="T">Luban 生成的 TbXxx 表类型。</typeparam>
        /// <returns>表实例，不存在时返回 null。</returns>
        public override T GetTexts<T>()
        {
            return m_TextTableDatas.TryGetValue(typeof(T), out ITable table) ? table as T : null;
        }

        /// <summary>
        /// 获取指定表名称的本地化文本表。
        /// </summary>
        /// <param name="tbName">表类型短名称（如 "TbCommon"）。</param>
        /// <returns>表实例，不存在时返回 null。</returns>
        public override ITable GetTexts(string tbName)
        {
            if (string.IsNullOrEmpty(tbName))
            {
                return null;
            }

            foreach (var kvp in m_TextTableDatas)
            {
                if (kvp.Key.Name == tbName)
                {
                    return kvp.Value;
                }
            }

            return null;
        }

        /// <summary>
        /// 检查指定语言是否存在字体数据。
        /// </summary>
        /// <param name="language">目标语言。</param>
        /// <returns>是否存在字体数据。</returns>
        public override bool HasFontDatas(Language language)
        {
            return m_FontDatas.TryGetValue(language, out var list) && list != null && list.Count > 0;
        }

        /// <summary>
        /// 获取指定语言的字体数据列表。
        /// </summary>
        /// <param name="language">目标语言。</param>
        /// <returns>字体数据只读列表，语言不存在时返回 null。</returns>
        public override IReadOnlyList<ILocalizationFontRow> GetFontDatas(Language language)
        {
            return m_FontDatas.TryGetValue(language, out List<ILocalizationFontRow> list) ? list : null;
        }
    }
}
