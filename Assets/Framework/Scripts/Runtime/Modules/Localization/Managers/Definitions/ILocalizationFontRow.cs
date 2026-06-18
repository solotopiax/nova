/***************************************************************
 * (c) copyright 2026 - 2030, Solotopia
 * All Rights Reserved.
 * -------------------------------------------------------------
 * filename:  ILocalizationFontRow.cs
 * author:    taoye
 * created:   2026/4/26
 * descrip:   本地化字体数据行接口
 ***************************************************************/

namespace NovaFramework.Runtime
{
    /// <summary>
    /// 本地化字体数据行接口。
    /// Luban 生成的字体 bean 类须实现此接口，框架侧通过接口直接访问字段，彻底消除反射。
    /// </summary>
    public interface ILocalizationFontRow
    {
        /// <summary>
        /// 语言名称（对应 Language 枚举名称）。
        /// </summary>
        string Language { get; }

        /// <summary>
        /// 字体标记（如 "Main"、"Special"），用于区分同语言下的多套字体。
        /// </summary>
        string Mark { get; }

        /// <summary>
        /// 字体资源地址。
        /// </summary>
        string AssetLocation { get; }

        /// <summary>
        /// 自定义材质名称（可选，为空时使用默认材质）。
        /// </summary>
        string MaterialName { get; }

        /// <summary>
        /// 字体大小缩放比例（基准值为 1.0）。
        /// </summary>
        float FontSizeScaleRatio { get; }
    }
}
