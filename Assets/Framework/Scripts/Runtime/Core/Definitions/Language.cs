/***************************************************************
 * (c) copyright 2026 - 2030, Solotopia
 * All Rights Reserved.
 * -------------------------------------------------------------
 * filename:  Language.cs
 * author:    taoye
 * created:   2026/3/27
 * descrip:   游戏语言类型及相关描述表
 ***************************************************************/

using System.Collections.Generic;

namespace NovaFramework.Runtime
{
    /// <summary>
    /// 游戏语言类型。
    /// </summary>
    public enum Language : byte
    {
        /// <summary>
        /// 未指定。
        /// </summary>
        Unspecified = 0,

        /// <summary>
        /// 南非荷兰语。
        /// </summary>
        Afrikaans,

        /// <summary>
        /// 阿尔巴尼亚语。
        /// </summary>
        Albanian,

        /// <summary>
        /// 阿拉伯语。
        /// </summary>
        Arabic,

        /// <summary>
        /// 巴斯克语。
        /// </summary>
        Basque,

        /// <summary>
        /// 白俄罗斯语。
        /// </summary>
        Belarusian,

        /// <summary>
        /// 保加利亚语。
        /// </summary>
        Bulgarian,

        /// <summary>
        /// 加泰罗尼亚语。
        /// </summary>
        Catalan,

        /// <summary>
        /// 简体中文。
        /// </summary>
        ChineseSimplified,

        /// <summary>
        /// 繁体中文。
        /// </summary>
        ChineseTraditional,

        /// <summary>
        /// 克罗地亚语。
        /// </summary>
        Croatian,

        /// <summary>
        /// 捷克语。
        /// </summary>
        Czech,

        /// <summary>
        /// 丹麦语。
        /// </summary>
        Danish,

        /// <summary>
        /// 荷兰语。
        /// </summary>
        Dutch,

        /// <summary>
        /// 英语。
        /// </summary>
        English,

        /// <summary>
        /// 爱沙尼亚语。
        /// </summary>
        Estonian,

        /// <summary>
        /// 法罗语。
        /// </summary>
        Faroese,

        /// <summary>
        /// 芬兰语。
        /// </summary>
        Finnish,

        /// <summary>
        /// 法语。
        /// </summary>
        French,

        /// <summary>
        /// 格鲁吉亚语。
        /// </summary>
        Georgian,

        /// <summary>
        /// 德语。
        /// </summary>
        German,

        /// <summary>
        /// 希腊语。
        /// </summary>
        Greek,

        /// <summary>
        /// 希伯来语。
        /// </summary>
        Hebrew,

        /// <summary>
        /// 匈牙利语。
        /// </summary>
        Hungarian,

        /// <summary>
        /// 冰岛语。
        /// </summary>
        Icelandic,

        /// <summary>
        /// 印尼语。
        /// </summary>
        Indonesian,

        /// <summary>
        /// 意大利语。
        /// </summary>
        Italian,

        /// <summary>
        /// 日语。
        /// </summary>
        Japanese,

        /// <summary>
        /// 韩语。
        /// </summary>
        Korean,

        /// <summary>
        /// 拉脱维亚语。
        /// </summary>
        Latvian,

        /// <summary>
        /// 立陶宛语。
        /// </summary>
        Lithuanian,

        /// <summary>
        /// 马其顿语。
        /// </summary>
        Macedonian,

        /// <summary>
        /// 马拉雅拉姆语。
        /// </summary>
        Malayalam,

        /// <summary>
        /// 挪威语。
        /// </summary>
        Norwegian,

        /// <summary>
        /// 波斯语。
        /// </summary>
        Persian,

        /// <summary>
        /// 波兰语。
        /// </summary>
        Polish,

        /// <summary>
        /// 巴西葡萄牙语。
        /// </summary>
        PortugueseBrazil,

        /// <summary>
        /// 葡萄牙语。
        /// </summary>
        PortuguesePortugal,

        /// <summary>
        /// 罗马尼亚语。
        /// </summary>
        Romanian,

        /// <summary>
        /// 俄语。
        /// </summary>
        Russian,

        /// <summary>
        /// 塞尔维亚克罗地亚语。
        /// </summary>
        SerboCroatian,

        /// <summary>
        /// 塞尔维亚西里尔语。
        /// </summary>
        SerbianCyrillic,

        /// <summary>
        /// 塞尔维亚拉丁语。
        /// </summary>
        SerbianLatin,

        /// <summary>
        /// 斯洛伐克语。
        /// </summary>
        Slovak,

        /// <summary>
        /// 斯洛文尼亚语。
        /// </summary>
        Slovenian,

        /// <summary>
        /// 西班牙语。
        /// </summary>
        Spanish,

        /// <summary>
        /// 瑞典语。
        /// </summary>
        Swedish,

        /// <summary>
        /// 泰语。
        /// </summary>
        Thai,

        /// <summary>
        /// 土耳其语。
        /// </summary>
        Turkish,

        /// <summary>
        /// 乌克兰语。
        /// </summary>
        Ukrainian,

        /// <summary>
        /// 越南语。
        /// </summary>
        Vietnamese,

        /// <summary>
        /// 印地语。
        /// </summary>
        Hindi,

        /// <summary>
        /// 马来语。
        /// </summary>
        Malay,

        /// <summary>
        /// 菲律宾语。
        /// </summary>
        Filipino,
    }

    /// <summary>
    /// 语言信息，包含中文显示名称和 BCP 47 locale 标识码。
    /// </summary>
    public readonly struct LanguageInfo
    {
        /// <summary>
        /// 中文显示名称。
        /// </summary>
        public readonly string Desc;
        /// <summary>
        /// BCP 47 locale 标识码。
        /// </summary>
        public readonly string Flag;

        /// <summary>
        /// 初始化语言信息的新实例。
        /// </summary>
        /// <param name="desc">中文显示名称。</param>
        /// <param name="flag">BCP 47 locale 标识码。</param>
        public LanguageInfo(string desc, string flag)
        {
            Desc = desc;
            Flag = flag;
        }
    }

    /// <summary>
    /// 游戏语言类型元数据，提供各语言的中文显示名称和 locale 标识码查询。
    /// </summary>
    public static class LanguageMetadata
    {
        /// <summary>
        /// 语言信息字典，<Language 枚举值, 语言信息>。
        /// </summary>
        private static readonly Dictionary<Language, LanguageInfo> s_LanguageInfos = new Dictionary<Language, LanguageInfo>
        {
            { Language.Unspecified, new LanguageInfo("未指定", "unknown") },
            { Language.Afrikaans, new LanguageInfo("南非荷兰语", "af-ZA") },
            { Language.Albanian, new LanguageInfo("阿尔巴尼亚语", "sq") },
            { Language.Arabic, new LanguageInfo("阿拉伯语", "ar") },
            { Language.Basque, new LanguageInfo("巴斯克语", "eu") },
            { Language.Belarusian, new LanguageInfo("白俄罗斯语", "be") },
            { Language.Bulgarian, new LanguageInfo("保加利亚语", "bg") },
            { Language.Catalan, new LanguageInfo("加泰罗尼亚语", "ca") },
            { Language.ChineseSimplified, new LanguageInfo("简体中文", "zh-CN") },
            { Language.ChineseTraditional, new LanguageInfo("繁体中文", "zh-TW") },
            { Language.Croatian, new LanguageInfo("克罗地亚语", "hr") },
            { Language.Czech, new LanguageInfo("捷克语", "cs") },
            { Language.Danish, new LanguageInfo("丹麦语", "da") },
            { Language.Dutch, new LanguageInfo("荷兰语", "nl") },
            { Language.English, new LanguageInfo("英语", "en-US") },
            { Language.Estonian, new LanguageInfo("爱沙尼亚语", "et") },
            { Language.Faroese, new LanguageInfo("法罗语", "fo") },
            { Language.Finnish, new LanguageInfo("芬兰语", "fi") },
            { Language.French, new LanguageInfo("法语", "fr") },
            { Language.Georgian, new LanguageInfo("格鲁吉亚语", "ka") },
            { Language.German, new LanguageInfo("德语", "de") },
            { Language.Greek, new LanguageInfo("希腊语", "el") },
            { Language.Hebrew, new LanguageInfo("希伯来语", "he") },
            { Language.Hungarian, new LanguageInfo("匈牙利语", "hu") },
            { Language.Icelandic, new LanguageInfo("冰岛语", "is") },
            { Language.Indonesian, new LanguageInfo("印尼语", "id") },
            { Language.Italian, new LanguageInfo("意大利语", "it") },
            { Language.Japanese, new LanguageInfo("日语", "ja") },
            { Language.Korean, new LanguageInfo("韩语", "ko") },
            { Language.Latvian, new LanguageInfo("拉脱维亚语", "lv") },
            { Language.Lithuanian, new LanguageInfo("立陶宛语", "lt") },
            { Language.Macedonian, new LanguageInfo("马其顿语", "mk") },
            { Language.Malayalam, new LanguageInfo("马拉雅拉姆语", "ml") },
            { Language.Norwegian, new LanguageInfo("挪威语", "nb") },
            { Language.Persian, new LanguageInfo("波斯语", "fa") },
            { Language.Polish, new LanguageInfo("波兰语", "pl") },
            { Language.PortugueseBrazil, new LanguageInfo("巴西葡萄牙语", "pt-BR") },
            { Language.PortuguesePortugal, new LanguageInfo("葡萄牙语", "pt") },
            { Language.Romanian, new LanguageInfo("罗马尼亚语", "ro") },
            { Language.Russian, new LanguageInfo("俄语", "ru") },
            { Language.SerboCroatian, new LanguageInfo("塞尔维亚克罗地亚语", "sh") },
            { Language.SerbianCyrillic, new LanguageInfo("塞尔维亚西里尔语", "sr-SP") },
            { Language.SerbianLatin, new LanguageInfo("塞尔维亚拉丁语", "sr-CS") },
            { Language.Slovak, new LanguageInfo("斯洛伐克语", "sk") },
            { Language.Slovenian, new LanguageInfo("斯洛文尼亚语", "sl") },
            { Language.Spanish, new LanguageInfo("西班牙语", "es") },
            { Language.Swedish, new LanguageInfo("瑞典语", "sv") },
            { Language.Thai, new LanguageInfo("泰语", "th") },
            { Language.Turkish, new LanguageInfo("土耳其语", "tr") },
            { Language.Ukrainian, new LanguageInfo("乌克兰语", "uk") },
            { Language.Vietnamese, new LanguageInfo("越南语", "vi") },
            { Language.Hindi, new LanguageInfo("印地语", "hi") },
            { Language.Malay, new LanguageInfo("马来语", "ms") },
            { Language.Filipino, new LanguageInfo("菲律宾语", "tl") },
        };

        /// <summary>
        /// 获取指定语言的中文显示名称。
        /// </summary>
        /// <param name="language">语言类型。</param>
        /// <returns>中文显示名称，语言不存在时返回空字符串。</returns>
        public static string GetDesc(Language language)
        {
            return s_LanguageInfos.TryGetValue(language, out LanguageInfo info) ? info.Desc : string.Empty;
        }

        /// <summary>
        /// 获取指定语言的 BCP 47 locale 标识码。
        /// </summary>
        /// <param name="language">语言类型。</param>
        /// <returns>locale 标识码，语言不存在时返回空字符串。</returns>
        public static string GetFlag(Language language)
        {
            return s_LanguageInfos.TryGetValue(language, out LanguageInfo info) ? info.Flag : string.Empty;
        }
    }
}
