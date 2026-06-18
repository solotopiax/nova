/***************************************************************
 * (c) copyright 2026 - 2030, Solotopia
 * All Rights Reserved.
 * -------------------------------------------------------------
 * filename:  LocalizationManager.Methods.cs
 * author:    taoye
 * created:   2026/4/10
 * descrip:   本地化管理器-辅助方法
 ***************************************************************/

using System;
using System.Collections.Generic;
using Cysharp.Threading.Tasks;
using Newtonsoft.Json.Linq;
using UnityEngine;

namespace NovaFramework.Runtime
{
    /// <summary>
    /// 本地化管理器。
    /// </summary>
    internal sealed partial class LocalizationManager : LocalizationManagerBase
    {
        /// <summary>
        /// Luban Tables 类名称（与 Luban 导出配置中的 manager 参数一致）。
        /// </summary>
        private const string c_TextTablesClassName = "LocalizationTextTables";

        /// <summary>
        /// 通用语言解析链：持久化 > 系统 > 回退。
        /// 持久化和系统均不在支持列表时返回回退语言；回退语言也不在支持列表时返回列表首项；列表为空时直接返回回退语言。
        /// </summary>
        /// <returns>解析后的语言。</returns>
        private Language ResolveByPersistThenSystem()
        {
            Language saved = ReadLanguageFromPersist();
            if (saved != Language.Unspecified && HasSupportedLanguage(saved))
            {
                return saved;
            }

            Language system = MapUnitySystemLanguage(Application.systemLanguage);
            if (HasSupportedLanguage(system))
            {
                return system;
            }

            Language fallback = m_Config?.FallbackLanguage ?? Language.English;
            if (HasSupportedLanguage(fallback))
            {
                return fallback;
            }

            if (m_SupportedLanguages.Count > 0)
            {
                return m_SupportedLanguages[0];
            }

            return m_Config?.FallbackLanguage ?? Language.English;
        }

        /// <summary>
        /// 从持久化存储中读取保存的语言偏好。
        /// </summary>
        /// <returns>已保存的语言，不存在时返回 Unspecified。</returns>
        private Language ReadLanguageFromPersist()
        {
            if (m_PlayerPrefsManager == null || m_Config == null)
            {
                return Language.Unspecified;
            }

            string savedName = m_PlayerPrefsManager.GetString(m_Config.PersistClassifyName, m_Config.PersistItemKey, string.Empty);
            if (string.IsNullOrEmpty(savedName))
            {
                return Language.Unspecified;
            }

            if (Enum.TryParse<Language>(savedName, true, out Language result))
            {
                return result;
            }

            Log.Warning(LogTag.Localization, "持久化中保存的语言名称 '{0}' 无法解析为有效的 Language 枚举。", savedName);
            return Language.Unspecified;
        }

        /// <summary>
        /// 将语言偏好保存到持久化存储。
        /// </summary>
        /// <param name="language">要保存的语言。</param>
        private void SaveLanguageToPersist(Language language)
        {
            if (m_PlayerPrefsManager == null || m_Config == null)
            {
                return;
            }

            m_PlayerPrefsManager.SetString(m_Config.PersistClassifyName, m_Config.PersistItemKey, language.ToString());
            m_PlayerPrefsManager.Save(m_Config.PersistClassifyName);
        }

        /// <summary>
        /// 将 Unity 的 SystemLanguage 枚举映射为框架的 Language 枚举。
        /// </summary>
        /// <param name="systemLanguage">Unity 系统语言。</param>
        /// <returns>映射后的 Language 值，无法映射时返回 Unspecified。</returns>
        private Language MapUnitySystemLanguage(UnityEngine.SystemLanguage systemLanguage)
        {
            return s_SystemLanguageMap.TryGetValue(systemLanguage, out Language lang) ? lang : Language.Unspecified;
        }

        /// <summary>
        /// 通过 LubanTablesLoader 从缓存构建 Luban Tb 对象。
        /// </summary>
        private void BuildTextTablesFromCache()
        {
            m_TextTableDatas.Clear();

            if (m_TextDataPersistCache.DataMap.Count == 0)
            {
                return;
            }

            Func<string, JArray> loader = key =>
            {
                if (m_TextDataPersistCache.DataMap.TryGetValue(key, out object value) && value is JArray jArray)
                {
                    return jArray;
                }
                Log.Warning(LogTag.Localization, "文本数据缓存中未找到数据：{0}", key);
                return new JArray();
            };

            IConfigManager configManager = FrameworkManagersGroup.GetManager<IConfigManager>();
            if (configManager == null)
            {
                Log.Error(LogTag.Localization, "IConfigManager 未注册，无法加载 Localization 文本数据，请确认场景中存在 ConfigComponent。");
                return;
            }
            string namespace_ = configManager.Namespace;
            Dictionary<Type, ITable> tables = LubanTablesLoader.Load(c_TextTablesClassName, namespace_, loader);
            if (tables == null || tables.Count == 0)
            {
                Log.Warning(LogTag.Localization, "LubanTablesLoader 未能构建任何文本表对象。");
                return;
            }

            foreach (var kvp in tables)
            {
                m_TextTableDatas[kvp.Key] = kvp.Value;
            }

            Log.Debug(LogTag.Localization, "Localization 成功加载了 {0} 个数据文件，共计 {1} 个表格数据。", m_TextUnitSettings.Count, m_TextTableDatas.Count);
        }

        /// <summary>
        /// 将 Luban Tb 对象扁平化到 m_LanguageTexts 字典，保持 GetText/HasText 向后兼容。
        /// 遍历所有 ITable 中的 DataList，提取 Name 和 Value 字段。
        /// </summary>
        private void FlattenToLanguageTexts()
        {
            m_LanguageTexts.Clear();

            foreach (var kvp in m_TextTableDatas)
            {
                ITable table = kvp.Value;
                FlattenTableToLanguageTexts(table);
            }

            if (m_LanguageTexts.Count > 0)
            {
                Log.Debug(LogTag.Localization, "已扁平化 {0} 条本地化文本到快速查找字典。", m_LanguageTexts.Count);
            }
        }

        /// <summary>
        /// 将单个 ITable 的数据扁平化到 m_LanguageTexts。
        /// 通过 ITable<ILocalizationTextRow> 协变转型直接访问 DataList，零反射。
        /// </summary>
        /// <param name="table">Luban 表实例。</param>
        private void FlattenTableToLanguageTexts(ITable table)
        {
            if (table == null)
            {
                return;
            }

            if (!(table is ITable<ILocalizationTextRow> typedTable))
            {
                Log.Warning(LogTag.Localization, "表类型 '{0}' 未实现 ITable<ILocalizationTextRow>，已跳过扁平化。请确认 Luban bean 已实现 ILocalizationTextRow 接口。", table.GetType().Name);
                return;
            }

            IReadOnlyList<ILocalizationTextRow> dataList = typedTable.DataList;
            for (int i = 0; i < dataList.Count; i++)
            {
                ILocalizationTextRow row = dataList[i];
                if (row != null && !string.IsNullOrEmpty(row.Name))
                {
                    m_LanguageTexts[row.Name] = row.Value ?? string.Empty;
                }
            }
        }

        /// <summary>
        /// 校验字体单元设置列表是否已配置，未配置时输出跳过日志。
        /// 返回 true 表示列表非空可继续加载，返回 false 表示跳过加载。
        /// </summary>
        /// <returns>是否可继续加载。</returns>
        private bool PrepareLoadFontData()
        {
            if (m_FontUnitSettings == null || m_FontUnitSettings.Count == 0)
            {
                Log.Debug(LogTag.Localization, "字体数据单元设置为空，跳过字体加载。");
                return false;
            }

            return true;
        }

        /// <summary>
        /// 异步加载字体数据。
        /// </summary>
        /// <returns>是否加载成功。</returns>
        private async UniTask<bool> LoadFontDataAsync()
        {
            if (!PrepareLoadFontData())
            {
                return true;
            }

            DataReceiver.LoadAssetAsyncFunc loadFunc = LoadAssetAsync;
            DataReceiver.ReleaseAssetAction releaseFunc = ReleaseAsset;

            LubanDataCache fontDataCache = new LubanDataCache();

            List<UniTask<bool>> tasks = new List<UniTask<bool>>(m_FontUnitSettings.Count);
            for (int i = 0; i < m_FontUnitSettings.Count; i++)
            {
                LocalizationFontUnitSetting unit = m_FontUnitSettings[i];
                if (string.IsNullOrEmpty(unit.AssetLocation))
                {
                    continue;
                }

                tasks.Add(new LubanDataReceiver(fontDataCache, unit, loadFunc, releaseFunc).ReadDataAssetAsync(unit.AssetLocation));
            }

            if (tasks.Count > 0)
            {
                bool[] results = await UniTask.WhenAll(tasks);
                for (int i = 0; i < results.Length; i++)
                {
                    if (!results[i])
                    {
                        return false;
                    }
                }
            }

            if (m_AssetManager == null)
            {
                return false;
            }

            BuildFontTablesFromCache(fontDataCache);
            return true;
        }

        /// <summary>
        /// 同步加载字体数据。
        /// </summary>
        private void LoadFontDataSync()
        {
            if (!PrepareLoadFontData())
            {
                return;
            }

            DataReceiver.LoadAssetSyncFunc syncLoadFunc = (assetLocation) =>
            {
                IAssetHandle<UnityEngine.TextAsset> handle = m_AssetManager.LoadSync<UnityEngine.TextAsset>(assetLocation);
                UnityEngine.TextAsset asset = handle.Asset;
                handle.Release();
                return asset;
            };
            DataReceiver.ReleaseAssetAction releaseFunc = _ => { };

            LubanDataCache fontDataCache = new LubanDataCache();

            for (int i = 0; i < m_FontUnitSettings.Count; i++)
            {
                LocalizationFontUnitSetting unit = m_FontUnitSettings[i];
                if (string.IsNullOrEmpty(unit.AssetLocation))
                {
                    continue;
                }

                new LubanDataReceiver(fontDataCache, unit, syncLoadFunc, releaseFunc).ReadDataAssetSync(unit.AssetLocation);
            }

            BuildFontTablesFromCache(fontDataCache);
        }

        /// <summary>
        /// 异步加载语言列表 JSON（从 AB 资源），解析为 Language 枚举填充 m_SupportedLanguages。
        /// </summary>
        /// <returns>是否加载成功。</returns>
        private async UniTask<bool> LoadSupportedLanguagesAsync()
        {
            if (!PrepareLoadSupportedLanguages())
            {
                return true;
            }

            string json = await LoadTextAssetAsync(m_Config.SupportedLanguagesAssetLocation);
            if (m_Config == null || m_AssetManager == null)
            {
                return false;
            }

            ParseSupportedLanguagesJson(json);
            return true;
        }

        /// <summary>
        /// 同步加载语言列表 JSON（从 AB 资源），解析为 Language 枚举填充 m_SupportedLanguages。
        /// </summary>
        private void LoadSupportedLanguagesSync()
        {
            if (!PrepareLoadSupportedLanguages())
            {
                return;
            }

            string json = LoadTextAssetSync(m_Config.SupportedLanguagesAssetLocation);
            ParseSupportedLanguagesJson(json);
        }

        /// <summary>
        /// 清空已支持语言列表并校验语言列表资源路径是否已配置。
        /// 返回 true 表示校验通过可继续加载，返回 false 表示跳过加载（路径未配置）。
        /// </summary>
        /// <returns>是否可继续加载。</returns>
        private bool PrepareLoadSupportedLanguages()
        {
            m_SupportedLanguages.Clear();

            if (m_Config == null || string.IsNullOrEmpty(m_Config.SupportedLanguagesAssetLocation))
            {
                Log.Warning(LogTag.Localization, "语言列表资源路径未配置，跳过加载。");
                return false;
            }

            return true;
        }

        /// <summary>
        /// 解析语言列表 JSON 字符串，将有效语言名称转换为 Language 枚举并填充到 m_SupportedLanguages。
        /// </summary>
        /// <param name="json">JSON 数组字符串（如 ["ChineseSimplified","English"]）。</param>
        private void ParseSupportedLanguagesJson(string json)
        {
            if (string.IsNullOrEmpty(json))
            {
                Log.Warning(LogTag.Localization, "语言列表 JSON 为空，已支持语言列表将保持为空。");
                return;
            }

            List<string> languageNames = Newtonsoft.Json.JsonConvert.DeserializeObject<List<string>>(json);
            if (languageNames == null || languageNames.Count == 0)
            {
                return;
            }

            for (int i = 0; i < languageNames.Count; i++)
            {
                if (Enum.TryParse<Language>(languageNames[i], true, out Language lang) && lang != Language.Unspecified)
                {
                    m_SupportedLanguages.Add(lang);
                }
            }

            Log.Debug(LogTag.Localization, "已加载 {0} 种支持语言。", m_SupportedLanguages.Count);
        }

        /// <summary>
        /// Luban Font Tables 类名称。
        /// </summary>
        private const string c_FontTablesClassName = "LocalizationFontTables";

        /// <summary>
        /// 反射构造 LocalizationFontTables 实例，通过 ITable<ILocalizationFontRow> 协变直接访问 DataList 填充 m_FontDatas。
        /// </summary>
        /// <param name="fontDataCache">Phase 1 写入的字体数据加载缓存，消费后由本方法清空。</param>
        private void BuildFontTablesFromCache(LubanDataCache fontDataCache)
        {
            if (fontDataCache.DataMap.Count == 0)
            {
                return;
            }

            Func<string, JArray> loader = key =>
            {
                if (fontDataCache.DataMap.TryGetValue(key, out object value) && value is JArray jArray)
                {
                    return jArray;
                }
                Log.Warning(LogTag.Localization, "字体数据缓存中未找到数据：{0}", key);
                return new JArray();
            };

            IConfigManager configManager = FrameworkManagersGroup.GetManager<IConfigManager>();
            if (configManager == null)
            {
                Log.Error(LogTag.Localization, "IConfigManager 未注册，无法加载 Localization 字体数据，请确认场景中存在 ConfigComponent。");
                return;
            }
            string namespace_ = configManager.Namespace;
            Dictionary<Type, ITable> tables = LubanTablesLoader.Load(c_FontTablesClassName, namespace_, loader);
            if (tables == null || tables.Count == 0)
            {
                Log.Warning(LogTag.Localization, "LubanTablesLoader 未能构建任何字体表对象。");
                return;
            }

            int totalCount = 0;
            foreach (var kv in tables)
            {
                if (!(kv.Value is ITable<ILocalizationFontRow> typedTable))
                {
                    Log.Warning(LogTag.Localization, "字体表类型 '{0}' 未实现 ITable<ILocalizationFontRow>，已跳过。请确认 Luban bean 已实现 ILocalizationFontRow 接口。", kv.Value.GetType().Name);
                    continue;
                }

                IReadOnlyList<ILocalizationFontRow> dataList = typedTable.DataList;
                for (int i = 0; i < dataList.Count; i++)
                {
                    ILocalizationFontRow row = dataList[i];
                    if (row == null)
                    {
                        continue;
                    }

                    if (!Enum.TryParse<Language>(row.Language, true, out Language lang) || lang == Language.Unspecified)
                    {
                        Log.Warning(LogTag.Localization, "字体数据中的语言名称 '{0}' 无法解析为有效的 Language 枚举，已跳过。", row.Language);
                        continue;
                    }

                    if (!m_FontDatas.TryGetValue(lang, out List<ILocalizationFontRow> list))
                    {
                        list = new List<ILocalizationFontRow>();
                        m_FontDatas[lang] = list;
                    }

                    list.Add(row);
                    totalCount++;
                }
            }

            Log.Debug(LogTag.Localization, "Localization Font 成功加载了 {0} 个数据文件，共计 {1} 条字体数据。", m_FontUnitSettings?.Count ?? 0, totalCount);
        }

        /// <summary>
        /// 通过 AssetManager 同步加载 TextAsset 并返回其文本内容。
        /// 读取文本后立即释放资源，避免 TextAsset 驻留内存。
        /// </summary>
        /// <param name="assetLocation">资源地址。</param>
        /// <returns>TextAsset 的文本内容，加载失败时返回 null。</returns>
        private string LoadTextAssetSync(string assetLocation)
        {
            if (m_AssetManager == null)
            {
                Log.Error(LogTag.Localization, "AssetManager 未初始化，无法同步加载资源。");
                return null;
            }

            IAssetHandle<TextAsset> handle = m_AssetManager.LoadSync<TextAsset>(assetLocation);
            TextAsset textAsset = handle.Asset;
            handle.Release();
            if (textAsset == null)
            {
                Log.Error(LogTag.Localization, "同步加载 TextAsset 失败，AssetLocation='{0}'。", assetLocation);
                return null;
            }

            return textAsset.text;
        }

        /// <summary>
        /// 通过 AssetManager 异步加载 TextAsset 并返回其文本内容。
        /// 读取文本后立即释放资源，避免 TextAsset 驻留内存。
        /// await 前捕获 assetManager 引用，以便 Shutdown 竞态时仍能释放已加载资源。
        /// </summary>
        /// <param name="assetLocation">资源地址。</param>
        /// <returns>TextAsset 的文本内容，加载失败时返回 null。</returns>
        private async UniTask<string> LoadTextAssetAsync(string assetLocation)
        {
            IAssetManager assetManager = m_AssetManager;
            if (assetManager == null)
            {
                Log.Error(LogTag.Localization, "AssetManager 未初始化，无法异步加载资源。");
                return null;
            }

            IAssetHandle<TextAsset> handle = await assetManager.LoadAsync<TextAsset>(assetLocation);
            TextAsset textAsset;
            try
            {
                textAsset = handle.Asset;
            }
            finally
            {
                handle.Release();
            }

            if (m_Config == null || m_AssetManager == null)
            {
                return null;
            }

            if (textAsset == null)
            {
                Log.Error(LogTag.Localization, "异步加载 TextAsset 失败，AssetLocation='{0}'。", assetLocation);
                return null;
            }

            return textAsset.text;
        }

        /// <summary>
        /// 尝试同步加载指定语言的文本数据：清空缓存后重新加载并重建 Tb 对象和扁平字典。
        /// </summary>
        /// <param name="language">目标语言。</param>
        /// <returns>是否成功加载到有效文本数据。</returns>
        private bool TryLoadLanguageTextsSync(Language language)
        {
            string languageName = language.ToString();

            LubanDataCache tempCache = new LubanDataCache();
            DataReceiver.LoadAssetSyncFunc syncLoadFunc = (assetLocation) =>
            {
                IAssetHandle<UnityEngine.TextAsset> handle = m_AssetManager.LoadSync<UnityEngine.TextAsset>(assetLocation);
                UnityEngine.TextAsset asset = handle.Asset;
                handle.Release();
                return asset;
            };

            for (int i = 0; i < m_TextUnitSettings.Count; i++)
            {
                LocalizationTextUnitSetting unit = m_TextUnitSettings[i];
                if (string.IsNullOrEmpty(unit.AssetLocation))
                {
                    continue;
                }

                string assetLocation = Txt.Format(unit.AssetLocation, languageName);
                string content = LoadTextAssetSync(assetLocation);
                if (string.IsNullOrEmpty(content))
                {
                    continue;
                }

                LubanDataReceiver receiver = new LubanDataReceiver(tempCache, unit, syncLoadFunc, ReleaseAsset);
                receiver.OnParseDataAsset(content);
            }

            return CommitLanguageTextCache(tempCache);
        }

        /// <summary>
        /// 尝试异步加载指定语言的文本数据：清空缓存后重新加载并重建 Tb 对象和扁平字典。
        /// </summary>
        /// <param name="language">目标语言。</param>
        /// <returns>是否成功加载到有效文本数据。</returns>
        private async UniTask<bool> TryLoadLanguageTextsAsync(Language language)
        {
            string languageName = language.ToString();

            LubanDataCache tempCache = new LubanDataCache();

            List<UniTask<bool>> tasks = new List<UniTask<bool>>(m_TextUnitSettings.Count);
            for (int i = 0; i < m_TextUnitSettings.Count; i++)
            {
                LocalizationTextUnitSetting unit = m_TextUnitSettings[i];
                if (string.IsNullOrEmpty(unit.AssetLocation))
                {
                    continue;
                }

                string assetLocation = Txt.Format(unit.AssetLocation, languageName);
                LubanDataReceiver receiver = new LubanDataReceiver(tempCache, unit, LoadAssetAsync, ReleaseAsset);
                tasks.Add(receiver.ReadDataAssetAsync(assetLocation));
            }

            if (tasks.Count == 0)
            {
                return false;
            }

            bool[] results = await UniTask.WhenAll(tasks);
            if (m_Config == null || m_AssetManager == null)
            {
                return false;
            }

            for (int i = 0; i < results.Length; i++)
            {
                if (!results[i])
                {
                    Log.Warning(LogTag.Localization, "异步加载语言 '{0}' 文本数据时第 {1} 个单元失败。", languageName, i);
                }
            }

            return CommitLanguageTextCache(tempCache);
        }

        /// <summary>
        /// 将临时文本缓存替换到主缓存，重建 Luban Tb 对象和扁平字典。
        /// </summary>
        /// <param name="tempCache">临时数据加载缓存。</param>
        /// <returns>是否存在有效文本数据。</returns>
        private bool CommitLanguageTextCache(LubanDataCache tempCache)
        {
            if (tempCache.DataMap.Count == 0)
            {
                return false;
            }

            m_TextDataPersistCache.Clear();
            foreach (var kvp in tempCache.DataMap)
            {
                m_TextDataPersistCache.DataMap[kvp.Key] = kvp.Value;
            }
            foreach (var kvp in tempCache.SourceTracker)
            {
                m_TextDataPersistCache.SourceTracker[kvp.Key] = kvp.Value;
            }

            BuildTextTablesFromCache();
            FlattenToLanguageTexts();

            return m_LanguageTexts.Count > 0;
        }

        /// <summary>
        /// 校验语言切换请求的前置条件：语言不为 Unspecified、在已支持列表中、与当前语言不同。
        /// 返回 true 表示校验通过可继续切换，返回 false 表示本次切换已终止。
        /// </summary>
        /// <param name="language">目标语言。</param>
        /// <returns>是否可继续切换。</returns>
        private bool ValidateLanguageSwitch(Language language)
        {
            if (language == Language.Unspecified)
            {
                Log.Warning(LogTag.Localization, "无法切换到未指定的语言。");
                return false;
            }

            if (!HasSupportedLanguage(language))
            {
                Log.Warning(LogTag.Localization, "语言 '{0}' 不在已支持列表中，切换已取消。", language);
                return false;
            }

            if (m_Language == language)
            {
                Log.Debug(LogTag.Localization, "当前语言已是 '{0}'，无需切换。", language);
                return false;
            }

            return true;
        }

        /// <summary>
        /// 将语言切换结果应用到管理器状态：更新当前语言、缓存索引、持久化、广播事件。
        /// m_LanguageTexts 已在 CommitLanguageTextCache -> FlattenToLanguageTexts 中完成更新。
        /// </summary>
        /// <param name="language">目标语言。</param>
        private void ApplyLanguageSwitchResult(Language language)
        {
            Language oldLanguage = m_Language;
            m_Language = language;
            SaveLanguageToPersist(language);

            FireRefreshEvent(oldLanguage, language);
            Log.Debug(LogTag.Localization, "语言已切换：'{0}' -> '{1}'。", oldLanguage, language);
        }

        /// <summary>
        /// 广播语言切换刷新事件。
        /// </summary>
        /// <param name="oldLanguage">切换前的语言。</param>
        /// <param name="newLanguage">切换后的语言。</param>
        private void FireRefreshEvent(Language oldLanguage, Language newLanguage)
        {
            if (m_EventManager == null)
            {
                Log.Warning(LogTag.Localization, "EventManager 未初始化，无法广播语言切换事件。");
                return;
            }

            LocalizationRefreshEventData eventData = LocalizationRefreshEventData.Create(oldLanguage, newLanguage);
            m_EventManager.FireNow(this, eventData);
        }

        /// <summary>
        /// 异步加载资源的委托（供 LubanDataReceiver 使用）。
        /// await 前捕获 assetManager 引用，以便 Shutdown 竞态时仍能释放已加载资源。
        /// </summary>
        /// <param name="assetLocation">资源地址。</param>
        /// <returns>加载到的资源对象。</returns>
        private async UniTask<UnityEngine.Object> LoadAssetAsync(string assetLocation)
        {
            IAssetManager assetManager = m_AssetManager;
            if (assetManager == null)
            {
                return null;
            }

            IAssetHandle<TextAsset> handle = await assetManager.LoadAsync<TextAsset>(assetLocation);
            TextAsset textAsset = handle.Asset;
            handle.Release();
            if (m_AssetManager == null)
            {
                return null;
            }

            return textAsset;
        }

        /// <summary>
        /// 释放资源的委托（供 LubanDataReceiver 使用）。
        /// </summary>
        /// <param name="asset">要释放的资源对象。</param>
        private void ReleaseAsset(object asset)
        {
            // 资源句柄已在加载委托内部立即 Release，此方法保留供历史回调，不再需要操作。
        }
    }
}
