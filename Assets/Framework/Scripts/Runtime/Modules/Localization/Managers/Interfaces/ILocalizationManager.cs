/***************************************************************
 * (c) copyright 2026 - 2030, Solotopia
 * All Rights Reserved.
 * -------------------------------------------------------------
 * filename:  ILocalizationManager.cs
 * author:    taoye
 * created:   2026/4/10
 * descrip:   本地化管理器接口
 ***************************************************************/

using System.Collections.Generic;
using Cysharp.Threading.Tasks;

namespace NovaFramework.Runtime
{
    /// <summary>
    /// 本地化管理器接口。
    /// </summary>
    public interface ILocalizationManager
    {
        /// <summary>
        /// 获取当前语言。
        /// </summary>
        Language Language { get; }

        /// <summary>
        /// 获取当前语言名称。
        /// </summary>
        string LanguageName { get; }

        /// <summary>
        /// 获取当前语言在已支持语言列表中的索引。
        /// </summary>
        int LanguageIndex { get; }

        /// <summary>
        /// 获取系统语言（映射后的 Language 枚举值）。
        /// </summary>
        Language SystemLanguage { get; }

        /// <summary>
        /// 获取是否启用字体自动适配。
        /// </summary>
        bool AutoFontAdapt { get; }

        /// <summary>
        /// 初始化本地化管理器。
        /// </summary>
        /// <param name="config">配置信息。</param>
        void Initialize(LocalizationManagerConfig config);

        /// <summary>
        /// 异步加载本地化基础数据（语言列表 + 字体配置）。
        /// </summary>
        /// <returns>是否加载成功。</returns>
        UniTask<bool> LoadAsync();

        /// <summary>
        /// 同步加载本地化基础数据（语言列表 + 字体配置）。
        /// </summary>
        void LoadSync();

        /// <summary>
        /// 根据优先级决定当前语言并同步加载对应文本数据。供 Procedure 调用完成语言初始化。
        /// </summary>
        /// <returns>解析并设置后的语言。</returns>
        Language InitCurrentLanguageSync();

        /// <summary>
        /// 根据优先级决定当前语言并异步加载对应文本数据。供 Procedure 调用完成语言初始化。
        /// </summary>
        /// <returns>解析并设置后的语言。</returns>
        UniTask<Language> InitCurrentLanguageAsync();

        /// <summary>
        /// 检查指定语言是否在已支持列表中。
        /// </summary>
        /// <param name="language">待检查的语言。</param>
        /// <returns>是否已支持。</returns>
        bool HasSupportedLanguage(Language language);

        /// <summary>
        /// 获取所有已支持语言的只读列表。
        /// </summary>
        /// <returns>已支持语言只读列表。</returns>
        IReadOnlyList<Language> GetSupportedLanguages();

        /// <summary>
        /// 根据优先级决定当前语言：持久化 > 系统语言 > 回退语言。
        /// </summary>
        /// <returns>解析后的语言。</returns>
        Language ResolveLanguage();

        /// <summary>
        /// 同步切换语言。
        /// </summary>
        /// <param name="language">目标语言。</param>
        void SetLanguageSync(Language language);

        /// <summary>
        /// 异步切换语言。
        /// </summary>
        /// <param name="language">目标语言。</param>
        /// <returns>异步任务。</returns>
        UniTask SetLanguageAsync(Language language);

        /// <summary>
        /// 检查指定名称是否存在于当前语言数据中。
        /// </summary>
        /// <param name="name">文本名称。</param>
        /// <returns>是否存在。</returns>
        bool HasText(string name);

        /// <summary>
        /// 获取指定名称对应的本地化文本。
        /// </summary>
        /// <param name="name">文本名称。</param>
        /// <returns>本地化文本，不存在时返回名称本身。</returns>
        string GetText(string name);

        /// <summary>
        /// 检查是否包含指定类型的本地化文本表。
        /// </summary>
        /// <typeparam name="T">Luban 生成的 TbXxx 表类型。</typeparam>
        /// <returns>是否存在。</returns>
        bool HasTexts<T>() where T : class, ITable;

        /// <summary>
        /// 检查是否包含指定表名称的本地化文本表。
        /// </summary>
        /// <param name="tbName">表类型短名称（如 "TbCommon"）。</param>
        /// <returns>是否存在。</returns>
        bool HasTexts(string tbName);

        /// <summary>
        /// 获取指定类型的本地化文本表。
        /// </summary>
        /// <typeparam name="T">Luban 生成的 TbXxx 表类型。</typeparam>
        /// <returns>表实例，不存在时返回 null。</returns>
        T GetTexts<T>() where T : class, ITable;

        /// <summary>
        /// 获取指定表名称的本地化文本表。
        /// </summary>
        /// <param name="tbName">表类型短名称（如 "TbCommon"）。</param>
        /// <returns>表实例，不存在时返回 null。</returns>
        ITable GetTexts(string tbName);

        /// <summary>
        /// 检查指定语言是否存在字体数据。
        /// </summary>
        /// <param name="language">目标语言。</param>
        /// <returns>是否存在字体数据。</returns>
        bool HasFontDatas(Language language);

        /// <summary>
        /// 获取指定语言的字体数据列表。
        /// </summary>
        /// <param name="language">目标语言。</param>
        /// <returns>字体数据只读列表，语言不存在时返回 null。</returns>
        IReadOnlyList<ILocalizationFontRow> GetFontDatas(Language language);
    }
}
