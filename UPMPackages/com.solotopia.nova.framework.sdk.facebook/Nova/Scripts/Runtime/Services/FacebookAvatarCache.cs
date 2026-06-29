using System.IO;
using UnityEngine;

namespace NovaFramework.SDK.Facebook
{
    /// <summary>
    /// Facebook 头像缓存路径工具。
    /// </summary>
    public static class FacebookAvatarCache
    {
        /// <summary>
        /// 头像缓存相对目录。
        /// </summary>
        public const string RelativeDirectory = "Nova/Facebook/Avatars";

        /// <summary>
        /// 构造指定根目录下的头像路径。
        /// </summary>
        /// <param name="persistentDataRoot">持久化根目录。</param>
        /// <param name="facebookId">Facebook 用户 ID。</param>
        /// <returns>头像本地路径。</returns>
        public static string BuildAvatarPath(string persistentDataRoot, string facebookId)
        {
            if (string.IsNullOrEmpty(persistentDataRoot) || string.IsNullOrEmpty(facebookId))
            {
                return string.Empty;
            }

            string path = Path.Combine(persistentDataRoot, "Nova", "Facebook", "Avatars", facebookId + ".png");
            return path.Replace('\\', '/');
        }

        /// <summary>
        /// 构造当前应用下的头像路径。
        /// </summary>
        /// <param name="facebookId">Facebook 用户 ID。</param>
        /// <returns>头像本地路径。</returns>
        public static string BuildAvatarPath(string facebookId)
        {
            return BuildAvatarPath(Application.persistentDataPath, facebookId);
        }

        /// <summary>
        /// 确保头像缓存目录存在。
        /// </summary>
        public static void EnsureAvatarDirectory()
        {
            string directory = Path.Combine(Application.persistentDataPath, "Nova", "Facebook", "Avatars");
            if (!Directory.Exists(directory))
            {
                Directory.CreateDirectory(directory);
            }
        }
    }
}
