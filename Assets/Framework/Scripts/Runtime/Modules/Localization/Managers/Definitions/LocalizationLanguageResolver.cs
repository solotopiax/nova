/***************************************************************
 * (c) copyright 2026 - 2030, Solotopia
 * All Rights Reserved.
 * -------------------------------------------------------------
 * filename:  LocalizationLanguageResolver.cs
 * author:    taoye
 * created:   2026/8/27
 * descrip:   正式本地化与启动期本地化共用的语言决策及启动镜像
 ***************************************************************/

using System;
using UnityEngine;

namespace NovaFramework.Runtime
{
    /// <summary>
    /// 不依赖资源系统的语言选择策略快照。
    /// </summary>
    internal readonly struct LocalizationLanguagePolicy
    {
        /// <summary>
        /// 创建语言选择策略快照。
        /// </summary>
        /// <param name="editorLanguage">编辑器强制语言。</param>
        /// <param name="runtimeLanguagePrefer">终端是否优先持久化与系统语言。</param>
        /// <param name="fallbackLanguage">回退语言。</param>
        internal LocalizationLanguagePolicy(
            Language editorLanguage,
            bool runtimeLanguagePrefer,
            Language fallbackLanguage)
        {
            EditorLanguage = editorLanguage;
            RuntimeLanguagePrefer = runtimeLanguagePrefer;
            FallbackLanguage = fallbackLanguage;
        }

        internal Language EditorLanguage { get; }
        internal bool RuntimeLanguagePrefer { get; }
        internal Language FallbackLanguage { get; }
    }

    /// <summary>
    /// 正式本地化与启动期本地化共用的纯语言决策器。
    /// 调用方负责提供各自阶段可用的语言集合，避免启动期反向依赖热更资源。
    /// </summary>
    internal static class LocalizationLanguageResolver
    {
        /// <summary>
        /// 按 Nova 统一优先级解析目标语言。
        /// </summary>
        /// <param name="policy">语言选择策略。</param>
        /// <param name="persistedLanguage">当前阶段可读取的持久化语言。</param>
        /// <param name="systemLanguage">Unity 系统语言。</param>
        /// <param name="isSupported">判断语言在当前阶段是否可用。</param>
        /// <param name="firstSupportedLanguage">当前阶段支持列表的第一项；无列表时传 Unspecified。</param>
        /// <param name="isEditor">当前是否运行在编辑器。</param>
        /// <returns>解析后的目标语言。</returns>
        internal static Language Resolve(
            LocalizationLanguagePolicy policy,
            Language persistedLanguage,
            SystemLanguage systemLanguage,
            Func<Language, bool> isSupported,
            Language firstSupportedLanguage,
            bool isEditor)
        {
            if (isEditor && IsSupported(policy.EditorLanguage, isSupported))
            {
                return policy.EditorLanguage;
            }

            if (!isEditor && !policy.RuntimeLanguagePrefer)
            {
                return ResolveFallback(policy.FallbackLanguage, firstSupportedLanguage, isSupported);
            }

            if (IsSupported(persistedLanguage, isSupported))
            {
                return persistedLanguage;
            }

            Language mappedSystemLanguage = MapSystemLanguage(systemLanguage);
            if (IsSupported(mappedSystemLanguage, isSupported))
            {
                return mappedSystemLanguage;
            }

            return ResolveFallback(policy.FallbackLanguage, firstSupportedLanguage, isSupported);
        }

        /// <summary>
        /// 将 Unity 系统语言映射为 Nova Language。
        /// </summary>
        /// <param name="systemLanguage">Unity 系统语言。</param>
        /// <returns>对应的 Nova 语言；无法映射时返回 Unspecified。</returns>
        internal static Language MapSystemLanguage(SystemLanguage systemLanguage)
        {
            switch (systemLanguage)
            {
                case SystemLanguage.Afrikaans: return Language.Afrikaans;
                case SystemLanguage.Arabic: return Language.Arabic;
                case SystemLanguage.Basque: return Language.Basque;
                case SystemLanguage.Belarusian: return Language.Belarusian;
                case SystemLanguage.Bulgarian: return Language.Bulgarian;
                case SystemLanguage.Catalan: return Language.Catalan;
                case SystemLanguage.Chinese: return Language.ChineseSimplified;
                case SystemLanguage.ChineseSimplified: return Language.ChineseSimplified;
                case SystemLanguage.ChineseTraditional: return Language.ChineseTraditional;
                case SystemLanguage.Czech: return Language.Czech;
                case SystemLanguage.Danish: return Language.Danish;
                case SystemLanguage.Dutch: return Language.Dutch;
                case SystemLanguage.English: return Language.English;
                case SystemLanguage.Estonian: return Language.Estonian;
                case SystemLanguage.Faroese: return Language.Faroese;
                case SystemLanguage.Finnish: return Language.Finnish;
                case SystemLanguage.French: return Language.French;
                case SystemLanguage.German: return Language.German;
                case SystemLanguage.Greek: return Language.Greek;
                case SystemLanguage.Hebrew: return Language.Hebrew;
                case SystemLanguage.Hungarian: return Language.Hungarian;
                case SystemLanguage.Icelandic: return Language.Icelandic;
                case SystemLanguage.Indonesian: return Language.Indonesian;
                case SystemLanguage.Italian: return Language.Italian;
                case SystemLanguage.Japanese: return Language.Japanese;
                case SystemLanguage.Korean: return Language.Korean;
                case SystemLanguage.Latvian: return Language.Latvian;
                case SystemLanguage.Lithuanian: return Language.Lithuanian;
                case SystemLanguage.Norwegian: return Language.Norwegian;
                case SystemLanguage.Polish: return Language.Polish;
                case SystemLanguage.Portuguese: return Language.PortuguesePortugal;
                case SystemLanguage.Romanian: return Language.Romanian;
                case SystemLanguage.Russian: return Language.Russian;
                case SystemLanguage.SerboCroatian: return Language.SerboCroatian;
                case SystemLanguage.Slovak: return Language.Slovak;
                case SystemLanguage.Slovenian: return Language.Slovenian;
                case SystemLanguage.Spanish: return Language.Spanish;
                case SystemLanguage.Swedish: return Language.Swedish;
                case SystemLanguage.Thai: return Language.Thai;
                case SystemLanguage.Turkish: return Language.Turkish;
                case SystemLanguage.Ukrainian: return Language.Ukrainian;
                case SystemLanguage.Vietnamese: return Language.Vietnamese;
                default: return Language.Unspecified;
            }
        }

        /// <summary>
        /// 判断候选语言是否有效且在当前阶段可用。
        /// </summary>
        /// <param name="language">候选语言。</param>
        /// <param name="isSupported">当前阶段的可用性判断。</param>
        /// <returns>可用返回 true。</returns>
        private static bool IsSupported(Language language, Func<Language, bool> isSupported)
        {
            return language != Language.Unspecified && isSupported != null && isSupported(language);
        }

        /// <summary>
        /// 按配置回退语言、支持列表第一项、配置回退语言原值的顺序完成兜底。
        /// </summary>
        /// <param name="fallbackLanguage">配置的回退语言。</param>
        /// <param name="firstSupportedLanguage">支持列表第一项。</param>
        /// <param name="isSupported">当前阶段的可用性判断。</param>
        /// <returns>最终回退语言。</returns>
        private static Language ResolveFallback(
            Language fallbackLanguage,
            Language firstSupportedLanguage,
            Func<Language, bool> isSupported)
        {
            if (IsSupported(fallbackLanguage, isSupported))
            {
                return fallbackLanguage;
            }

            if (IsSupported(firstSupportedLanguage, isSupported))
            {
                return firstSupportedLanguage;
            }

            return fallbackLanguage != Language.Unspecified ? fallbackLanguage : Language.English;
        }
    }

    /// <summary>
    /// 启动期语言上下文。保存 Inspector 策略，并通过独立明文镜像跨冷启动读取语言偏好。
    /// 语言枚举不属于敏感信息；正式持久化仍维持原有 AES 存储。
    /// </summary>
    internal static class LocalizationBootstrapLanguage
    {
        internal const string PreferenceKey = "Nova.Localization.BootstrapLanguage.v1";

        private static LocalizationLanguagePolicy s_Policy = CreateDefaultPolicy();

        /// <summary>
        /// 子系统初始化时重置静态策略，兼容关闭 Domain Reload 的编辑器运行方式。
        /// </summary>
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStatics()
        {
            s_Policy = CreateDefaultPolicy();
        }

        /// <summary>
        /// 在 LocalizationComponent.Awake 阶段注册序列化语言策略。
        /// </summary>
        /// <param name="policy">当前场景中的语言策略。</param>
        internal static void Configure(LocalizationLanguagePolicy policy)
        {
            s_Policy = policy;
        }

        /// <summary>
        /// 使用启动镜像和统一策略解析启动期语言。
        /// </summary>
        /// <param name="isSupported">判断对应精简启动 JSON 是否存在。</param>
        /// <param name="firstSupportedLanguage">启动资源的首选兜底语言。</param>
        /// <returns>启动期应使用的语言。</returns>
        internal static Language ResolveLanguage(
            Func<Language, bool> isSupported,
            Language firstSupportedLanguage)
        {
            return LocalizationLanguageResolver.Resolve(
                s_Policy,
                ReadLanguage(),
                Application.systemLanguage,
                isSupported,
                firstSupportedLanguage,
                Application.isEditor);
        }

        /// <summary>
        /// 在正式语言切换成功后写入启动期明文镜像并立即落盘。
        /// 写入失败不会中断正式本地化切换。
        /// </summary>
        /// <param name="language">已经生效的正式语言。</param>
        internal static void SaveLanguage(Language language)
        {
            if (language == Language.Unspecified)
            {
                return;
            }

            try
            {
                PlatformPlayerPrefs.SetString(PreferenceKey, language.ToString());
                PlatformPlayerPrefs.Save();
            }
            catch (Exception ex)
            {
                Log.Warning(LogTag.Localization, "启动期语言镜像保存失败，正式语言 '{0}' 已生效。异常: {1}", language, ex.Message);
            }
        }

        /// <summary>
        /// 从不加密的平台存储读取启动期语言镜像。
        /// </summary>
        /// <returns>有效镜像语言；不存在、损坏或读取失败时返回 Unspecified。</returns>
        private static Language ReadLanguage()
        {
            try
            {
                string savedName = PlatformPlayerPrefs.GetString(PreferenceKey, string.Empty);
                if (!string.IsNullOrEmpty(savedName) &&
                    Enum.TryParse(savedName, true, out Language savedLanguage) &&
                    savedLanguage != Language.Unspecified &&
                    Enum.IsDefined(typeof(Language), savedLanguage))
                {
                    return savedLanguage;
                }
            }
            catch (Exception ex)
            {
                Log.Warning(LogTag.Localization, "启动期语言镜像读取失败，将按系统语言与回退策略继续解析。异常: {0}", ex.Message);
            }

            return Language.Unspecified;
        }

        /// <summary>
        /// 创建未注册组件配置时使用的安全默认策略。
        /// </summary>
        /// <returns>默认语言策略。</returns>
        private static LocalizationLanguagePolicy CreateDefaultPolicy()
        {
            return new LocalizationLanguagePolicy(Language.Unspecified, true, Language.English);
        }
    }
}
