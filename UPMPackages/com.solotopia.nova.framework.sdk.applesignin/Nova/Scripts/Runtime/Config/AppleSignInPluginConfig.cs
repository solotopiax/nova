/***************************************************************
 * (c) copyright 2026 - 2030, Solotopia
 * All Rights Reserved.
 * -------------------------------------------------------------
 * filename:  AppleSignInPluginConfig.cs
 * author:    Codex
 * created:   2026/6/25
 * descrip:   Apple 登录插件配置
 ***************************************************************/

using System;
using NovaFramework.Runtime;
using UnityEngine;

namespace NovaFramework.SDK.AppleSignIn
{
    /// <summary>
    /// Apple 登录插件运行时配置。
    /// </summary>
    [Serializable]
    public sealed class AppleSignInPluginConfig : ISDKPluginConfig
    {
        /// <summary>
        /// 是否请求姓名。
        /// </summary>
        [SerializeField, Tooltip("登录时是否请求 Apple 姓名；Apple 可能只在首次授权时返回。")]
        private bool m_RequestFullName = true;

        /// <summary>
        /// 获取是否请求姓名。
        /// </summary>
        public bool RequestFullName => m_RequestFullName;

        /// <summary>
        /// 获取配置显示名称。
        /// </summary>
        public string DisplayName => "Apple";

        /// <summary>
        /// 创建默认配置。
        /// </summary>
        public AppleSignInPluginConfig() { }

        /// <summary>
        /// 创建指定值的配置。
        /// </summary>
        /// <param name="requestFullName">是否请求姓名。</param>
        public AppleSignInPluginConfig(bool requestFullName = true)
        {
            m_RequestFullName = requestFullName;
        }
    }
}
