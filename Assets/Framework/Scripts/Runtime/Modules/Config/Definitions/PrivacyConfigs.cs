/***************************************************************
 * (c) copyright 2026 - 2030, Solotopia
 * All Rights Reserved.
 * -------------------------------------------------------------
 * filename:  PrivacyConfigs.cs
 * author:    taoye
 * created:   2026/8/13
 * descrip:   隐私运行时配置数据结构
 ***************************************************************/

using System;
using UnityEngine;

namespace NovaFramework.Runtime
{
    /// <summary>
    /// 隐私运行时配置；承载框架本地 AES 默认密钥及 iOS 构建期隐私声明。
    /// </summary>
    [Serializable]
    public sealed class PrivacyConfigs
    {
        /// <summary>
        /// 参考 Solar 的 iOS Required Reason API 声明模板；项目上线前需核对实际使用的 API 与理由码。
        /// </summary>
        internal const string DefaultPrivacyInfoConfig =
            @"{""NSPrivacyAccessedAPICategoryFileTimestamp"":[""C617.1"",""0A2A.1""]," +
            @"""NSPrivacyAccessedAPICategorySystemBootTime"":[""35F9.1""]," +
            @"""NSPrivacyAccessedAPICategoryDiskSpace"":[""E174.1""]," +
            @"""NSPrivacyAccessedAPICategoryActiveKeyboards"":[]," +
            @"""NSPrivacyAccessedAPICategoryUserDefaults"":[""CA92.1""]}";

        /// <summary>
        /// Util.Encrypt.AES 默认加密密钥，按 UTF-8 编码后必须为 16 字节。
        /// </summary>
        [Tooltip("Util.Encrypt.AES 默认 Key；按 UTF-8 编码后必须为 16 字节。")]
        public string AESKey;

        /// <summary>
        /// Util.Encrypt.AES 默认初始化向量，按 UTF-8 编码后必须为 16 字节。
        /// </summary>
        [Tooltip("Util.Encrypt.AES 默认 IV；按 UTF-8 编码后必须为 16 字节。")]
        public string AESIV;

        /// <summary>
        /// iOS Required Reason API 声明：JSON 对象的键为 API 类别，值为理由码字符串数组。
        /// 默认填入 Solar 参考模板，仅供 iOS 构建后处理使用；手动留空时不生成应用级声明。
        /// </summary>
        [TextArea(3, 8)]
        [Tooltip("仅 iOS 生效；填写 Required Reason API 类别与理由码的 JSON 映射，留空则不注入应用级声明。")]
        public string PrivacyInfoConfig = DefaultPrivacyInfoConfig;

        /// <summary>
        /// iOS 应用 Info.plist 的权限用途说明；JSON 对象的键为 UsageDescription，值为面向用户的实际用途。
        /// 仅供 iOS 构建后处理使用；空对象或旧资产中的空值不注入任何条目。
        /// </summary>
        [TextArea(3, 8)]
        [Tooltip("仅 iOS 生效；填写 Info.plist 的 UsageDescription 键与实际用途说明，留空则不注入。")]
        public string InfoPlistUsageDescriptions = "{}";
    }
}
