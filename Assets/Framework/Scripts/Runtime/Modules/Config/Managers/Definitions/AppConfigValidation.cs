/***************************************************************
 * (c) copyright 2026 - 2030, Solotopia
 * All Rights Reserved.
 * -------------------------------------------------------------
 * filename:  AppConfigValidation.cs
 * author:    taoye
 * created:   2026/11/16
 * descrip:   应用协议参数的本地格式校验
 ***************************************************************/

using System;
using System.Globalization;
using System.Text;

namespace NovaFramework.Runtime
{
    /// <summary>
    /// 应用协议参数的本地格式判定，供设计态提示与真实请求消费者共用。
    /// 不判断后台登记或服务端匹配，也不影响本地运行时配置的加载。
    /// </summary>
    internal static class AppConfigValidation
    {
        /// <summary>空值和公开示例的 YOUR_ 标记均表示尚未配置，不能作为真实请求凭据。</summary>
        internal static bool IsUnconfigured(string value)
        {
            return string.IsNullOrWhiteSpace(value) ||
                   value.TrimStart().StartsWith("YOUR_", StringComparison.OrdinalIgnoreCase);
        }

        /// <summary>按协议的 int32 格式解析应用编号；不为缺值或错误值伪造编号。</summary>
        internal static bool TryParseAppId(string value, out int appId)
        {
            appId = default;
            return !IsUnconfigured(value) &&
                   int.TryParse(value, NumberStyles.Integer, CultureInfo.InvariantCulture, out appId);
        }

        /// <summary>协议 AES 参数必须已配置且按 UTF-8 编码恰好为 16 字节。</summary>
        internal static bool IsValidAesSecret(string value)
        {
            return !IsUnconfigured(value) && Encoding.UTF8.GetByteCount(value) == 16;
        }

        /// <summary>只判断本地请求前置；返回 false 时不得发送应用协议请求，游戏可继续使用本地配置。</summary>
        internal static bool IsReady(AppConfigs config)
        {
            return config != null && TryParseAppId(config.AppID, out _) &&
                   IsValidAesSecret(config.AppAesKey) && IsValidAesSecret(config.AppAesIV);
        }
    }
}
