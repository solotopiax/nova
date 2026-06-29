using System;

namespace NovaFramework.SDK.Facebook
{
    /// <summary>
    /// Facebook 链接分享请求。
    /// </summary>
    public sealed class FacebookShareRequest
    {
        /// <summary>
        /// 分享链接。
        /// </summary>
        public Uri ContentUrl;

        /// <summary>
        /// 分享标题。
        /// </summary>
        public string Title;

        /// <summary>
        /// 分享描述。
        /// </summary>
        public string Description;

        /// <summary>
        /// 分享图片地址。
        /// </summary>
        public Uri PhotoUrl;
    }
}
