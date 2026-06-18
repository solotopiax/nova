/***************************************************************
 * (c) copyright 2026 - 2030, Solotopia
 * All Rights Reserved.
 * -------------------------------------------------------------
 * filename:  ILocalizationTextRow.cs
 * author:    taoye
 * created:   2026/4/26
 * descrip:   本地化文本数据行接口
 ***************************************************************/

namespace NovaFramework.Runtime
{
    /// <summary>
    /// 本地化文本数据行接口。
    /// Luban 生成的文本行类型须实现此接口，框架侧通过 ITable<ILocalizationTextRow> 协变直接访问 DataList。
    /// </summary>
    public interface ILocalizationTextRow
    {
        /// <summary>
        /// 获取本地化键名称。
        /// </summary>
        string Name { get; }

        /// <summary>
        /// 获取本地化文本值。
        /// </summary>
        string Value { get; }
    }
}
