namespace NovaFramework.SDK.Facebook
{
    /// <summary>
    /// Facebook 分享结果。
    /// </summary>
    public sealed class FacebookShareResult
    {
        /// <summary>
        /// 是否分享成功。
        /// </summary>
        public bool Success;

        /// <summary>
        /// 是否被用户取消。
        /// </summary>
        public bool Cancelled;

        /// <summary>
        /// 错误信息。
        /// </summary>
        public string ErrorMessage;

        /// <summary>
        /// 原始返回内容。
        /// </summary>
        public string RawResult;
    }
}
