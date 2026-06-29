namespace NovaFramework.SDK.Facebook
{
    /// <summary>
    /// Facebook Graph API 默认路径。
    /// </summary>
    public static class FacebookGraphPaths
    {
        /// <summary>
        /// 当前用户资料与头像路径。
        /// </summary>
        public const string CurrentProfileWithPicture = "me?fields=id,name,picture";

        /// <summary>
        /// 好友列表与头像路径。
        /// </summary>
        public const string FriendsWithPicture = "me/friends?fields=id,name,picture";

        /// <summary>
        /// 构造头像图片请求路径。
        /// </summary>
        /// <param name="facebookId">Facebook 用户 ID。</param>
        /// <param name="size">头像尺寸。</param>
        /// <returns>头像请求路径。</returns>
        public static string AvatarPicture(string facebookId, int size)
        {
            string id = string.IsNullOrEmpty(facebookId) ? "me" : facebookId;
            int avatarSize = size > 0 ? size : FacebookPluginConfig.DefaultAvatarSize;
            return $"{id}/picture?width={avatarSize}&height={avatarSize}";
        }
    }
}
