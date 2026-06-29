using System;
using NovaFramework.Runtime;
using UnityEngine;

namespace NovaFramework.SDK.Facebook
{
    /// <summary>
    /// Facebook 插件运行时配置。
    /// </summary>
    [Serializable]
    public sealed class FacebookPluginConfig : ISDKPluginConfig
    {
        /// <summary>
        /// 默认头像尺寸。
        /// </summary>
        public const int DefaultAvatarSize = 100;

        /// <summary>
        /// Facebook App ID。
        /// </summary>
        [SerializeField, Tooltip("Facebook App ID。")]
        private string m_FacebookAppId;

        /// <summary>
        /// Facebook Client Token。
        /// </summary>
        [SerializeField, Tooltip("Facebook Client Token。")]
        private string m_FacebookClientToken;

        /// <summary>
        /// 登录成功后是否自动下载当前用户头像。
        /// </summary>
        [SerializeField, Tooltip("登录成功后是否自动下载当前用户 Facebook 头像。")]
        private bool m_AutoDownloadAvatarOnLogin = true;

        /// <summary>
        /// 头像下载尺寸。
        /// </summary>
        [SerializeField, Tooltip("头像下载尺寸，默认 100，最终请求为 width x height。")]
        private int m_AvatarSize = DefaultAvatarSize;

        /// <summary>
        /// 获取 Facebook App ID。
        /// </summary>
        public string FacebookAppId => m_FacebookAppId;

        /// <summary>
        /// 获取 Facebook Client Token。
        /// </summary>
        public string FacebookClientToken => m_FacebookClientToken;

        /// <summary>
        /// 获取是否自动下载头像。
        /// </summary>
        public bool AutoDownloadAvatarOnLogin => m_AutoDownloadAvatarOnLogin;

        /// <summary>
        /// 获取头像尺寸。
        /// </summary>
        public int AvatarSize => m_AvatarSize > 0 ? m_AvatarSize : DefaultAvatarSize;

        /// <summary>
        /// 获取配置显示名。
        /// </summary>
        public string DisplayName => "Facebook";

        /// <summary>
        /// 创建默认配置。
        /// </summary>
        public FacebookPluginConfig() { }

        /// <summary>
        /// 创建指定值的配置。
        /// </summary>
        /// <param name="facebookAppId">Facebook App ID。</param>
        /// <param name="facebookClientToken">Facebook Client Token。</param>
        /// <param name="autoDownloadAvatarOnLogin">是否自动下载头像。</param>
        /// <param name="avatarSize">头像尺寸。</param>
        public FacebookPluginConfig(string facebookAppId, string facebookClientToken, bool autoDownloadAvatarOnLogin = true, int avatarSize = DefaultAvatarSize)
        {
            m_FacebookAppId = facebookAppId;
            m_FacebookClientToken = facebookClientToken;
            m_AutoDownloadAvatarOnLogin = autoDownloadAvatarOnLogin;
            m_AvatarSize = avatarSize;
        }
    }
}
