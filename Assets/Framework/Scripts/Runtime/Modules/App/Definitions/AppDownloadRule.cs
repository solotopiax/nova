/***************************************************************
 * (c) copyright 2026 - 2030, Solotopia
 * All Rights Reserved.
 * -------------------------------------------------------------
 * filename:  AppDownloadRule.cs
 * author:    taoye
 * created:   2026/3/6
 * descrip:   大版本更新规则类型
 ***************************************************************/

namespace NovaFramework.Runtime
{
    /// <summary>
    /// 大版本更新规则类型，表示本次检测命中的规则（互斥单选）。
    /// </summary>
    public enum AppDownloadRule : byte
    {
        /// <summary>
        /// 未命中任何规则，无需大版本更新。
        /// </summary>
        None = 0,

        /// <summary>
        /// 命中推荐更新规则。
        /// </summary>
        Recommended,

        /// <summary>
        /// 命中强制更新规则。
        /// </summary>
        Forced,
    }
}
