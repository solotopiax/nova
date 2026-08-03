/***************************************************************
 * (c) copyright 2026 - 2030, Solotopia
 * All Rights Reserved.
 * -------------------------------------------------------------
 * filename:  LubanDataFormat.cs
 * author:    taoye
 * created:   2026/7/29
 * descrip:   Luban 数据导出格式
 ***************************************************************/

namespace NovaFramework.Runtime
{
    /// <summary>
    /// Nova Unit 导出链支持的 Luban 数据格式。
    /// </summary>
    public enum LubanDataFormat
    {
        /// <summary>
        /// Newtonsoft JSON 代码与 JSON 数据。
        /// </summary>
        Json = 0,

        /// <summary>
        /// Luban Binary 代码与 Binary 数据。
        /// </summary>
        Binary = 1,
    }
}
