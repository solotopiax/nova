/***************************************************************
 * (c) copyright 2026 - 2030, Solotopia
 * All Rights Reserved.
 * -------------------------------------------------------------
 * filename:  CommonConfig.cs
 * author:    taoye
 * created:   2026/4/29
 * descrip:   全局公共配置数据结构
 ***************************************************************/

using System;
using UnityEngine;

namespace NovaFramework.Runtime
{
    /// <summary>
    /// 全局公共配置；字段值由外层 ConfigMasterSO 按 DevelopMode 选取后填入。
    /// </summary>
    [Serializable]
    public sealed class CommonConfig
    {
        /// <summary>
        /// 应用标识符。
        /// </summary>
        [Tooltip("应用唯一标识符。公司 GM 后台登记的 App ID，用于识别当前应用。")]
        public string AppID;

        /// <summary>
        /// AES 加密密钥。
        /// </summary>
        [Tooltip("AES 加密密钥（Key）。公司 GM 后台登记的 AES 密钥，用于加密本地存档等敏感数据。")]
        public string AppAesKey;

        /// <summary>
        /// AES 初始化向量。
        /// </summary>
        [Tooltip("AES 初始化向量（IV）。公司 GM 后台登记的 AES 初始化向量，与 AppAesKey 配合使用。")]
        public string AppAesIV;
    }
}
