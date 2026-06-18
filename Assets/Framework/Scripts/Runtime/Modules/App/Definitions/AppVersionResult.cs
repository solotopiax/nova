/***************************************************************
 * (c) copyright 2026 - 2030, Solotopia
 * All Rights Reserved.
 * -------------------------------------------------------------
 * filename:  AppVersionResult.cs
 * author:    taoye
 * created:   2026/5/19
 * descrip:   App 大版本检查结果枚举
 ***************************************************************/

namespace NovaFramework.Runtime
{
    /// <summary>
    /// App 大版本检查结果。
    /// </summary>
    public enum AppVersionResult
    {
        /// <summary>
        /// 无新版本，可直接进入后续流程。
        /// </summary>
        NoDownload,

        /// <summary>
        /// 有更新，但客户端当前可跳过，不需要强制中断。
        /// </summary>
        RecommendedDownload,

        /// <summary>
        /// 服务端强制更新，必须跳转下载页，不可跳过。
        /// </summary>
        ForcedDownload,
    }
}
