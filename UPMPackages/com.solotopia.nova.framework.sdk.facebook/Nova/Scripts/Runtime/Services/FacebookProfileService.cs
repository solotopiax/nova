using System.Collections.Generic;
using System.IO;
using System.Threading;
using Cysharp.Threading.Tasks;
using Facebook.Unity;
using NovaFramework.Runtime;
using UnityEngine;

namespace NovaFramework.SDK.Facebook
{
    /// <summary>
    /// Facebook 用户资料与头像服务。
    /// </summary>
    public sealed class FacebookProfileService
    {
        /// <summary>
        /// 所属 Facebook 插件。
        /// </summary>
        private readonly FacebookPlugin m_Plugin;

        /// <summary>
        /// Graph API 服务。
        /// </summary>
        private readonly FacebookGraphService m_GraphService;

        /// <summary>
        /// 头像尺寸。
        /// </summary>
        private readonly int m_AvatarSize;

        /// <summary>
        /// 创建资料服务。
        /// </summary>
        /// <param name="plugin">所属 Facebook 插件。</param>
        /// <param name="graphService">Graph API 服务。</param>
        /// <param name="avatarSize">头像尺寸。</param>
        internal FacebookProfileService(FacebookPlugin plugin, FacebookGraphService graphService, int avatarSize)
        {
            m_Plugin = plugin;
            m_GraphService = graphService;
            m_AvatarSize = avatarSize > 0 ? avatarSize : FacebookPluginConfig.DefaultAvatarSize;
        }

        /// <summary>
        /// 获取当前用户资料。
        /// </summary>
        /// <param name="ct">取消令牌。</param>
        /// <returns>当前用户资料。</returns>
        public async UniTask<FacebookProfile> GetCurrentProfileAsync(CancellationToken ct = default)
        {
            Log.Debug(LogTag.Facebook, "读取当前资料：fields=id,name,picture。");
            IGraphResult result = await m_GraphService.GetAsync(FacebookGraphPaths.CurrentProfileWithPicture).AttachExternalCancellation(ct);
            // 原有返回契约不变；Graph 错误在请求层记录，这里补充资料解析是否可用。
            if (!string.IsNullOrEmpty(result?.Error))
            {
                Log.Warning(LogTag.Facebook, "资料读取收到 Graph 错误，返回资料可能缺少昵称或头像。");
            }
            if (result == null || result.ResultDictionary == null)
            {
                Log.Warning(LogTag.Facebook, "资料读取未取得字段字典。");
                return null;
            }

            var profile = new FacebookProfile
            {
                FacebookId = ReadString(result.ResultDictionary, "id"),
                Name = ReadString(result.ResultDictionary, "name"),
                AvatarUrl = ReadPictureUrl(result.ResultDictionary)
            };

            if (!string.IsNullOrEmpty(profile.FacebookId))
            {
                profile.AvatarPath = await DownloadAvatarAsync(profile.FacebookId, ct);
            }

            Log.Debug(LogTag.Facebook, $"资料读取完成：含ID={!string.IsNullOrEmpty(profile.FacebookId)}，含昵称={!string.IsNullOrEmpty(profile.Name)}，含头像URL={!string.IsNullOrEmpty(profile.AvatarUrl)}，含头像文件={!string.IsNullOrEmpty(profile.AvatarPath)}。");
            return profile;
        }

        /// <summary>
        /// 下载用户头像。
        /// </summary>
        /// <param name="facebookId">Facebook 用户 ID。</param>
        /// <param name="ct">取消令牌。</param>
        /// <returns>头像本地路径。</returns>
        public async UniTask<string> DownloadAvatarAsync(string facebookId = null, CancellationToken ct = default)
        {
            Log.Debug(LogTag.Facebook, $"头像下载调用：目标={(string.IsNullOrEmpty(facebookId) ? "当前用户" : "指定用户")}，尺寸={m_AvatarSize}。");
            string id = ResolveFacebookId(facebookId);
            if (string.IsNullOrEmpty(id))
            {
                Log.Warning(LogTag.Facebook, "头像下载跳过：没有可用的 Facebook 用户ID。");
                return string.Empty;
            }

            string localPath = FacebookAvatarCache.BuildAvatarPath(id);
            if (File.Exists(localPath))
            {
                Log.Debug(LogTag.Facebook, "头像命中本地文件缓存，跳过网络下载。");
                return localPath;
            }

            IGraphResult result = await m_GraphService.GetAsync(FacebookGraphPaths.AvatarPicture(id, m_AvatarSize)).AttachExternalCancellation(ct);
            if (result == null || !string.IsNullOrEmpty(result.Error) || result.Texture == null)
            {
                Log.Warning(LogTag.Facebook, $"头像未取得图片：空结果={result == null}，接口错误={!string.IsNullOrEmpty(result?.Error)}，含纹理={result?.Texture != null}。");
                return string.Empty;
            }

            FacebookAvatarCache.EnsureAvatarDirectory();
            File.WriteAllBytes(localPath, result.Texture.EncodeToPNG());
            Log.Debug(LogTag.Facebook, "头像图片已写入本地缓存。");
            return localPath;
        }

        /// <summary>
        /// 获取头像纹理。
        /// </summary>
        /// <param name="facebookId">Facebook 用户 ID。</param>
        /// <param name="ct">取消令牌。</param>
        /// <returns>头像纹理。</returns>
        public async UniTask<Texture2D> GetAvatarTextureAsync(string facebookId = null, CancellationToken ct = default)
        {
            string path = await DownloadAvatarAsync(facebookId, ct);
            if (string.IsNullOrEmpty(path) || !File.Exists(path))
            {
                Log.Warning(LogTag.Facebook, "头像纹理加载跳过：本地文件不可用。");
                return null;
            }

            byte[] bytes = File.ReadAllBytes(path);
            var texture = new Texture2D(2, 2, TextureFormat.RGBA32, false);
            bool loaded = texture.LoadImage(bytes);
            if (loaded)
                Log.Debug(LogTag.Facebook, $"头像纹理解码完成：文件字节数={bytes.Length}。");
            else
                Log.Warning(LogTag.Facebook, $"头像纹理解码失败：文件字节数={bytes.Length}。");
            return loaded ? texture : null;
        }

        /// <summary>
        /// 获取头像本地路径。
        /// </summary>
        /// <param name="facebookId">Facebook 用户 ID。</param>
        /// <returns>头像本地路径。</returns>
        public UniTask<string> GetAvatarPathAsync(string facebookId = null)
        {
            string id = ResolveFacebookId(facebookId);
            return UniTask.FromResult(string.IsNullOrEmpty(id) ? string.Empty : FacebookAvatarCache.BuildAvatarPath(id));
        }

        /// <summary>
        /// 解析目标 Facebook 用户 ID。
        /// </summary>
        /// <param name="facebookId">指定用户 ID。</param>
        /// <returns>目标用户 ID。</returns>
        private string ResolveFacebookId(string facebookId)
        {
            return string.IsNullOrEmpty(facebookId) ? m_Plugin?.CurrentUserData?.UserId : facebookId;
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
