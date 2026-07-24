/***************************************************************
 * (c) copyright 2026 - 2030, Solotopia
 * All Rights Reserved.
 * -------------------------------------------------------------
 * filename:  AppConfigs.cs
 * author:    taoye
 * created:   2026/4/29
 * descrip:   应用运行时配置数据结构
 ***************************************************************/

using System;
using UnityEngine;

namespace NovaFramework.Runtime
{
    /// <summary>
    /// 应用运行时配置；由 Editor 配置中心按当前维度导出为单格快照。
    /// </summary>
    [Serializable]
    public sealed class AppConfigs
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
