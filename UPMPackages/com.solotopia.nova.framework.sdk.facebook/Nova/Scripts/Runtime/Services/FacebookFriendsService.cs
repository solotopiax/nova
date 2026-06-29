using System.Collections.Generic;
using System.Threading;
using Cysharp.Threading.Tasks;
using Facebook.Unity;

namespace NovaFramework.SDK.Facebook
{
    /// <summary>
    /// Facebook 好友服务。
    /// </summary>
    public sealed class FacebookFriendsService
    {
        /// <summary>
        /// 用户资料服务。
        /// </summary>
        private readonly FacebookProfileService m_ProfileService;

        /// <summary>
        /// Graph API 服务。
        /// </summary>
        private readonly FacebookGraphService m_GraphService;

        /// <summary>
        /// 创建好友服务。
        /// </summary>
        /// <param name="profileService">用户资料服务。</param>
        /// <param name="graphService">Graph API 服务。</param>
        internal FacebookFriendsService(FacebookProfileService profileService, FacebookGraphService graphService)
        {
            m_ProfileService = profileService;
            m_GraphService = graphService;
        }

        /// <summary>
        /// 获取好友列表。
        /// </summary>
        /// <param name="ct">取消令牌。</param>
        /// <returns>好友列表。</returns>
        public async UniTask<IReadOnlyList<FacebookFriend>> GetFriendsAsync(CancellationToken ct = default)
        {
            IGraphResult result = await m_GraphService.GetAsync(FacebookGraphPaths.FriendsWithPicture).AttachExternalCancellation(ct);
            return ParseFriends(result);
        }

        /// <summary>
        /// 获取好友列表并下载头像。
        /// </summary>
        /// <param name="ct">取消令牌。</param>
        /// <returns>带本地头像路径的好友列表。</returns>
        public async UniTask<IReadOnlyList<FacebookFriend>> GetFriendsWithAvatarsAsync(CancellationToken ct = default)
        {
            IReadOnlyList<FacebookFriend> friends = await GetFriendsAsync(ct);
            for (int i = 0; i < friends.Count; i++)
            {
                FacebookFriend friend = friends[i];
                friend.AvatarPath = await m_ProfileService.DownloadAvatarAsync(friend.FacebookId, ct);
            }

            return friends;
        }

        /// <summary>
        /// 解析好友列表。
        /// </summary>
        /// <param name="result">Graph API 结果。</param>
        /// <returns>好友列表。</returns>
        private static IReadOnlyList<FacebookFriend> ParseFriends(IGraphResult result)
        {
            var friends = new List<FacebookFriend>();
            if (result == null || result.ResultDictionary == null || !result.ResultDictionary.TryGetValue("data", out object dataObj))
            {
                return friends;
            }

            if (dataObj is not IEnumerable<object> data)
            {
                return friends;
            }

            foreach (object item in data)
            {
                if (item is not IDictionary<string, object> row)
                {
                    continue;
                }

                var friend = new FacebookFriend
                {
                    FacebookId = ReadString(row, "id"),
                    Name = ReadString(row, "name"),
                    AvatarUrl = ReadPictureUrl(row)
                };

                if (!string.IsNullOrEmpty(friend.FacebookId))
                {
                    friends.Add(friend);
                }
            }

            return friends;
        }

        /// <summary>
        /// 读取字符串字段。
        /// </summary>
        /// <param name="row">数据行。</param>
        /// <param name="key">字段名。</param>
        /// <returns>字段值。</returns>
        private static string ReadString(IDictionary<string, object> row, string key)
        {
            return row.TryGetValue(key, out object value) ? value as string : null;
        }

        /// <summary>
        /// 读取头像地址。
        /// </summary>
        /// <param name="row">数据行。</param>
        /// <returns>头像地址。</returns>
        private static string ReadPictureUrl(IDictionary<string, object> row)
        {
            if (!row.TryGetValue("picture", out object pictureObj) || pictureObj is not IDictionary<string, object> picture)
            {
                return null;
            }

            if (!picture.TryGetValue("data", out object dataObj) || dataObj is not IDictionary<string, object> data)
            {
                return null;
            }

            return ReadString(data, "url");
        }
    }
}
