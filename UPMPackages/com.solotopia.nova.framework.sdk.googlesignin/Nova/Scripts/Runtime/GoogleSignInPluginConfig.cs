/***************************************************************
 * (c) copyright 2026 - 2030, Solotopia
 * All Rights Reserved.
 * -------------------------------------------------------------
 * filename:  GoogleSignInPluginConfig.cs
 * author:    yingzheng
 * created:   2026/6/25
 * descrip:   谷歌登录配置
 ***************************************************************/

using System;
using NovaFramework.Runtime;
using UnityEngine;

namespace NovaFramework.SDK.GoogleSignIn
{
    [Serializable]
    public sealed class GoogleSignInPluginConfig : ISDKPluginConfig
    {
        [SerializeField, Tooltip("谷歌网页客户端编号，用于身份令牌。")]
        private string m_ClientId;

        [SerializeField, Tooltip("登录时请求邮箱。")]
        private bool m_RequestEmail = true;

        [SerializeField, Tooltip("优先使用已授权账号。")]
        private bool m_FilterByAuthorizedAccounts = true;

        [SerializeField, Tooltip("允许自动选择账号。")]
        private bool m_AutoSelectEnabled = true;

        [SerializeField, Tooltip("初始化时恢复上次登录。")]
        private bool m_AutoRestoreOnInitialize;

        public string ClientId => m_ClientId;

        public bool RequestEmail => m_RequestEmail;

        public bool FilterByAuthorizedAccounts => m_FilterByAuthorizedAccounts;

        public bool AutoSelectEnabled => m_AutoSelectEnabled;

        public bool AutoRestoreOnInitialize => m_AutoRestoreOnInitialize;

        public string DisplayName => "Google";

        public GoogleSignInPluginConfig() { }

        public GoogleSignInPluginConfig(
            string clientId,
            bool requestEmail = true,
            bool filterByAuthorizedAccounts = true,
            bool autoSelectEnabled = true,
            bool autoRestoreOnInitialize = false)
        {
            m_ClientId = clientId;
            m_RequestEmail = requestEmail;
            m_FilterByAuthorizedAccounts = filterByAuthorizedAccounts;
            m_AutoSelectEnabled = autoSelectEnabled;
            m_AutoRestoreOnInitialize = autoRestoreOnInitialize;
        }
    }
}
