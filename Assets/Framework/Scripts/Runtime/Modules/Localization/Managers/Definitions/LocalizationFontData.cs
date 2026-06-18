/***************************************************************
 * (c) copyright 2026 - 2030, Solotopia
 * All Rights Reserved.
 * -------------------------------------------------------------
 * filename:  LocalizationFontData.cs
 * author:    taoye
 * created:   2026/4/10
 * descrip:   本地化字体配置数据
 ***************************************************************/

using System;

namespace NovaFramework.Runtime
{
    /// <summary>
    /// 本地化字体配置数据。
    /// </summary>
    [Serializable]
    public sealed class LocalizationFontData
    {
        /// <summary>
        /// 语言名称（对应 Language 枚举名称，仅 JSON 反序列化时使用）。
        /// </summary>
        public string Language { get; set; }

        /// <summary>
        /// 字体标记（如 "Main"、"Special"），用于区分同语言下的多套字体。
        /// </summary>
        public string Mark { get; set; }

        /// <summary>
        /// 字体资源地址。
        /// </summary>
        public string AssetLocation { get; set; }

        /// <summary>
        /// 自定义材质名称（可选，为空时使用默认材质）。
        /// </summary>
        public string MaterialName { get; set; }

        /// <summary>
        /// 字体大小缩放比例（基准值为 1.0）。
        /// </summary>
        public float FontSizeScaleRatio { get; set; } = 1.0f;
    }
}
