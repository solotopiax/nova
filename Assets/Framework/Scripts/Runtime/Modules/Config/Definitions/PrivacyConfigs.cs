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
    /// 隐私运行时配置；仅承载框架本地 AES 默认密钥，与 AppConfigs 中的应用业务密钥相互独立。
    /// </summary>
    [Serializable]
    public sealed class PrivacyConfigs
    {
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
    }
}
