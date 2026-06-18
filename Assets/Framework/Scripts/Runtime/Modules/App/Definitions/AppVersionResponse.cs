/***************************************************************
 * (c) copyright 2026 - 2030, Solotopia
 * All Rights Reserved.
 * -------------------------------------------------------------
 * filename:  AppVersionResponse.cs
 * author:    taoye
 * created:   2026/5/19
 * descrip:   CDN App 大版本检查 JSON 响应 DTO
 ***************************************************************/

namespace NovaFramework.Runtime
{
    /// <summary>
    /// CDN App 大版本检查 JSON 响应数据对象。
    /// 与服务端 JSON 字段一一对应，由 Util.Json.FromJson 反序列化。
    /// </summary>
    internal sealed class AppVersionResponse
    {
        /// <summary>
        /// 推荐更新规则的版本阈值。
        /// </summary>
        public string RecommendedDownloadVersion;

        /// <summary>
        /// 强制更新规则的版本阈值。
        /// </summary>
        public string ForcedDownloadVersion;
    }
}
