/***************************************************************
 * (c) copyright 2026 - 2030, Solotopia
 * All Rights Reserved.
 * -------------------------------------------------------------
 * filename:  LauncherLocalization.cs
 * author:    taoye
 * created:   2026/5/26
 * descrip:   启动期轻量本地化解析器
 *            只走 Resources 通道，与 LocalizationManager 完全解耦。
 *            在 AB / 资源系统就绪前的 Splash → CheckVersion → Hotfix →
 *            AppDownload → LoadDll 全链路上均可安全使用。
 ***************************************************************/

using System;
using System.Collections.Generic;
using Newtonsoft.Json.Linq;
using UnityEngine;

namespace NovaFramework.Runtime
{
    /// <summary>
    /// 启动期轻量本地化解析器。
    /// 只走 Resources.Load 通道读取 BuiltIn/Jsons/LocalizationTexts_{language}.json，
    /// 与 LocalizationManager / IAssetManager / EventManager 完全解耦，
    /// 可在 AB 资源系统就绪前的任意启动阶段安全调用。
    /// Initialize() 幂等；GetText() miss 时返回 key 本身。
    /// </summary>
    public static class LauncherLocalization
    {
        /// <summary>
        /// 默认的本地化文本 JSON Resources 路径模板（不含扩展名），{0} 填充 Language.ToString()。
        /// 当 Initialize 未提供模板或传入空串时使用。
        /// </summary>
        private const string c_DefaultJsonPathTemplate = "BuiltIn/Jsons/LocalizationTexts_{0}";

        /// <summary>
        /// JSON 根节点名称，与 LocalizationTexts_*.json 的结构保持一致。
        /// </summary>
        private const string c_JsonRootKey = "Launcher_Localization";

        /// <summary>
        /// 当前使用的 JSON 路径模板，由 Initialize 传入，{0} 填充 Language.ToString()。
        /// </summary>
        private static string s_JsonPathTemplate = c_DefaultJsonPathTemplate;

        /// <summary>
        /// JSON 条目的 Name 字段名。
        /// </summary>
        private const string c_JsonNameField = "Name";

        /// <summary>
        /// JSON 条目的 Value 字段名。
        /// </summary>
        private const string c_JsonValueField = "Value";

        /// <summary>
        /// 当前解析后的本地化文本缓存，键为 Name，值为 Value。
        /// </summary>
        private static Dictionary<string, string> s_TextMap;

        /// <summary>
        /// 当前已解析并使用的语言。
        /// </summary>
        private static Language s_Language = Language.Unspecified;

        /// <summary>
        /// 是否已完成初始化，用于幂等保护。
        /// </summary>
        private static bool s_IsInitialized;

        /// <summary>
        /// 当前已解析并使用的语言（只读，供调试/日志使用）。
        /// </summary>
        public static Language Language => s_Language;

#if UNITY_EDITOR
        /// <summary>
        /// 编辑器域重载时重置静态状态，与 LauncherUIController.ResetStatics 保持一致。
        /// </summary>
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStatics()
        {
            s_TextMap = null;
            s_Language = Language.Unspecified;
            s_IsInitialized = false;
            s_JsonPathTemplate = c_DefaultJsonPathTemplate;
        }
#endif

        /// <summary>
        /// 初始化启动期本地化解析器。
        /// 解析当前语言 → 加载对应 JSON → 缓存到内部字典。
        /// 加载失败时自动回退到 English；English 也失败则留空字典（GetText 走 key 回退）。
        /// 幂等：重复调用不重复加载。
        /// </summary>
        /// <param name="jsonPathTemplate">JSON 路径模板（相对于 Resources/，{0} 占位 Language.ToString()）。为空或缺省时使用内置默认值。</param>
        public static void Initialize(string jsonPathTemplate = null)
        {
            if (s_IsInitialized)
            {
                return;
            }

            s_JsonPathTemplate = string.IsNullOrEmpty(jsonPathTemplate) ? c_DefaultJsonPathTemplate : jsonPathTemplate;
            s_IsInitialized = true;
            s_Language = ResolveStartupLanguage();
            s_TextMap = LoadJson(s_Language);

            if (s_TextMap == null && s_Language != Language.English)
            {
                Log.Warning(LogTag.Procedure, Txt.Format("LauncherLocalization: 语言 {0} 的 JSON 加载失败，回退到 English。", s_Language));
                s_Language = Language.English;
                s_TextMap = LoadJson(Language.English);
            }

            if (s_TextMap == null)
            {
                Log.Warning(LogTag.Procedure, "LauncherLocalization: English JSON 也加载失败，使用空字典，GetText 将返回 key 本身。");
                s_TextMap = new Dictionary<string, string>();
            }
        }

        /// <summary>
        /// 获取指定 key 的本地化文本。
        /// 命中字典返回对应 Value；miss 或 key 为空返回 key 本身（空 key 返回 string.Empty）。
        /// </summary>
        /// <param name="key">本地化 Key，对应 JSON 中的 Name 字段。</param>
        /// <returns>本地化文本；未命中时返回 key 本身。</returns>
        public static string GetText(string key)
        {
            if (string.IsNullOrEmpty(key))
            {
                return string.Empty;
            }

            if (s_TextMap != null && s_TextMap.TryGetValue(key, out string value))
            {
                return value;
            }

            return key;
        }

        /// <summary>
        /// 解析启动期应使用的语言。
        /// 优先级：持久化（IPlayerPrefsManager）> Application.systemLanguage 映射 > English 回退。
        /// </summary>
        /// <returns>解析出的语言枚举值。</returns>
        private static Language ResolveStartupLanguage()
        {
            try
            {
                IPlayerPrefsManager playerPrefsManager = FrameworkManagersGroup.GetManager<IPlayerPrefsManager>();
                if (playerPrefsManager != null)
                {
                    string savedName = playerPrefsManager.GetString(LocalizationComponent.c_PersistClassifyName, LocalizationComponent.c_PersistItemKey, "");
                    if (!string.IsNullOrEmpty(savedName) && Enum.TryParse<Language>(savedName, true, out Language saved))
                    {
                        return saved;
                    }
                }
            }
            catch (Exception ex)
            {
                Log.Warning(LogTag.Procedure, Txt.Format("LauncherLocalization: 读取持久化语言失败（启动极早期正常），跳过持久化步骤。异常: {0}", ex.Message));
            }

            Language mapped = MapSystemLanguage(Application.systemLanguage);
            if (mapped != Language.Unspecified)
            {
                return mapped;
            }

            return Language.English;
        }

        /// <summary>
        /// 将 UnityEngine.SystemLanguage 映射到框架 Language 枚举。
        /// 覆盖常见语言；不在覆盖范围内时返回 Language.Unspecified，上层走 English 回退。
        /// </summary>
        /// <param name="systemLanguage">Unity 系统语言枚举。</param>
        /// <returns>对应的 Language 枚举值；不支持时返回 Language.Unspecified。</returns>
        private static Language MapSystemLanguage(SystemLanguage systemLanguage)
        {
            switch (systemLanguage)
            {
                case SystemLanguage.ChineseSimplified: return Language.ChineseSimplified;
                case SystemLanguage.ChineseTraditional: return Language.ChineseTraditional;
                case SystemLanguage.Chinese: return Language.ChineseSimplified;
                case SystemLanguage.English: return Language.English;
                case SystemLanguage.Japanese: return Language.Japanese;
                case SystemLanguage.Korean: return Language.Korean;
                case SystemLanguage.French: return Language.French;
                case SystemLanguage.German: return Language.German;
                case SystemLanguage.Spanish: return Language.Spanish;
                case SystemLanguage.Italian: return Language.Italian;
                case SystemLanguage.Russian: return Language.Russian;
                case SystemLanguage.Portuguese: return Language.PortuguesePortugal;
                case SystemLanguage.Indonesian: return Language.Indonesian;
                case SystemLanguage.Thai: return Language.Thai;
                case SystemLanguage.Vietnamese: return Language.Vietnamese;
                case SystemLanguage.Arabic: return Language.Arabic;
                case SystemLanguage.Turkish: return Language.Turkish;
                default: return Language.Unspecified;
            }
        }

        /// <summary>
        /// 从 Resources 通道加载指定语言的本地化 JSON 并解析为字典。
        /// 解析失败时返回 null，由调用方决定是否回退。
        /// </summary>
        /// <param name="lang">要加载的语言。</param>
        /// <returns>解析成功的字典；加载或解析失败返回 null。</returns>
        private static Dictionary<string, string> LoadJson(Language lang)
        {
            string path = Txt.Format(s_JsonPathTemplate, lang.ToString());
            TextAsset textAsset = Resources.Load<TextAsset>(path);
            if (textAsset == null)
            {
                Log.Warning(LogTag.Procedure, Txt.Format("LauncherLocalization: Resources.Load 失败，路径: {0}", path));
                return null;
            }

            try
            {
                JObject root = JObject.Parse(textAsset.text);
                JArray array = root[c_JsonRootKey] as JArray;
                if (array == null)
                {
                    Log.Warning(LogTag.Procedure, Txt.Format("LauncherLocalization: JSON 中未找到节点 {0}，路径: {1}", c_JsonRootKey, path));
                    return null;
                }

                Dictionary<string, string> map = new Dictionary<string, string>(array.Count);
                for (int i = 0; i < array.Count; i++)
                {
                    JToken element = array[i];
                    string name = element[c_JsonNameField]?.ToString();
                    string value = element[c_JsonValueField]?.ToString() ?? string.Empty;
                    if (!string.IsNullOrEmpty(name))
                    {
                        map[name] = value;
                    }
                }

                return map;
            }
            catch (Exception ex)
            {
                Log.Warning(LogTag.Procedure, Txt.Format("LauncherLocalization: JSON 解析异常，路径: {0}，异常: {1}", path, ex.Message));
                return null;
            }
        }
    }
}
