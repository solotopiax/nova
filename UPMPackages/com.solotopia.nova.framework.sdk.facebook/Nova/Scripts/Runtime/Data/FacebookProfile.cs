namespace NovaFramework.SDK.Facebook
{
    /// <summary>
    /// Facebook 用户资料。
    /// </summary>
    public sealed class FacebookProfile
    {
        /// <summary>
        /// Facebook 用户 ID。
        /// </summary>
        public string FacebookId;

        /// <summary>
        /// 昵称。
        /// </summary>
        public string Name;

        /// <summary>
        /// 头像远程地址。
        /// </summary>
        public string AvatarUrl;

        /// <summary>
        /// 头像本地路径。
        /// </summary>
        public string AvatarPath;
    }
}
