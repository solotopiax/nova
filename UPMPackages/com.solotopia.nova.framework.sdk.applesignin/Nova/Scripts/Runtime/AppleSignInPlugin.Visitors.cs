/***************************************************************
 * (c) copyright 2026 - 2030, Solotopia
 * All Rights Reserved.
 * -------------------------------------------------------------
 * filename:  AppleSignInPlugin.Visitors.cs
 * author:    yingzheng
 * created:   2026/6/25
 * descrip:   AppleSignInPlugin 字段、属性、常量
 ***************************************************************/

using System;

namespace NovaFramework.SDK.AppleSignIn
{
    /// <summary>
    /// AppleSignInPlugin 字段、属性、常量。
    /// </summary>
    public sealed partial class AppleSignInPlugin
    {
        /// <summary>
        /// 提供方。
        /// </summary>
        private const string c_ProviderName = "Apple";

        /// <summary>
        /// 配置类型。
        /// </summary>
        protected override Type ConfigType => typeof(AppleSignInPluginConfig);

        /// <summary>
        /// SDK 名称。
        /// </summary>
        public override string Name => c_ProviderName;

        /// <summary>
        /// 初始化优先级。
        /// </summary>
        public override int Priority => 30;

        /// <summary>
        /// 登录状态。
        /// </summary>
        public bool IsLoggedIn => !string.IsNullOrEmpty(m_CurrentUserData?.UserId);

        /// <summary>
        /// 当前用户。
        /// </summary>
        public AppleSignInUserData CurrentUserData => m_CurrentUserData;

        /// <summary>
        /// 运行配置。
        /// </summary>
        private AppleSignInPluginConfig m_RuntimeConfig;

        /// <summary>
        /// 登录服务。
        /// </summary>
        private AppleSignInAuthService m_AuthService;

        /// <summary>
        /// 当前用户。
        /// </summary>
        private AppleSignInUserData m_CurrentUserData;
    }
}

