/***************************************************************
 * (c) copyright 2026 - 2030, Solotopia
 * All Rights Reserved.
 * -------------------------------------------------------------
 * filename:  FacebookPlugin.Visitors.cs
 * author:    yingzheng
 * created:   2026/4/20
 * descrip:   FacebookPlugin 字段、属性、常量
 ***************************************************************/

using System;

namespace NovaFramework.SDK.Facebook
{
    /// <summary>
    /// FacebookPlugin 字段、属性、常量。
    /// </summary>
    public sealed partial class FacebookPlugin
    {
        /// <summary>
        /// 提供方。
        /// </summary>
        private const string ProviderName = "Facebook";

        /// <summary>
        /// 配置类型。
        /// </summary>
        protected override Type ConfigType => typeof(FacebookPluginConfig);

        /// <summary>
        /// SDK 名称。
        /// </summary>
        public override string Name => ProviderName;

        /// <summary>
        /// 初始化优先级；Facebook 优先级为 40。
        /// </summary>
        public override int Priority => 40;

        /// <summary>
        /// 登录状态。
        /// </summary>
        public bool IsLoggedIn => !string.IsNullOrEmpty(m_CurrentUserData?.UserId) && !string.IsNullOrEmpty(m_CurrentUserData?.AccessToken);

        /// <summary>
        /// 当前用户。
        /// </summary>
        public FacebookUserData CurrentUserData => m_CurrentUserData;

        /// <summary>
        /// Profile 服务。
        /// </summary>
        public FacebookProfileService Profile => m_ProfileService;

        /// <summary>
        /// Friends 服务。
        /// </summary>
        public FacebookFriendsService Friends => m_FriendsService;

        /// <summary>
        /// Share 服务。
        /// </summary>
        public FacebookShareService Share => m_ShareService;

        /// <summary>
        /// 运行配置。
        /// </summary>
        private FacebookPluginConfig m_RuntimeConfig;

        /// <summary>
        /// 登录服务。
        /// </summary>
        private FacebookAuthService m_AuthService;

        /// <summary>
        /// Graph 服务。
        /// </summary>
        private FacebookGraphService m_GraphService;

        /// <summary>
        /// Profile 服务。
        /// </summary>
        private FacebookProfileService m_ProfileService;

        /// <summary>
        /// Friends 服务。
        /// </summary>
        private FacebookFriendsService m_FriendsService;

        /// <summary>
        /// Share 服务。
        /// </summary>
        private FacebookShareService m_ShareService;

        /// <summary>
        /// 当前用户。
        /// </summary>
        private FacebookUserData m_CurrentUserData;
    }
}
