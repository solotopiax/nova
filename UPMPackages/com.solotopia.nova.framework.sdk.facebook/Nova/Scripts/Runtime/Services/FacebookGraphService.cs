using System;
using Cysharp.Threading.Tasks;
using Facebook.Unity;

namespace NovaFramework.SDK.Facebook
{
    /// <summary>
    /// Facebook Graph API UniTask 封装。
    /// </summary>
    public sealed class FacebookGraphService
    {
        /// <summary>
        /// 发起 GET 请求。
        /// </summary>
        /// <param name="path">Graph API 路径。</param>
        /// <returns>Graph API 结果。</returns>
        public UniTask<IGraphResult> GetAsync(string path)
        {
            if (string.IsNullOrEmpty(path))
            {
                throw new ArgumentException("Graph path is empty.", nameof(path));
            }

            var tcs = new UniTaskCompletionSource<IGraphResult>();
            FB.API(path, HttpMethod.GET, result => tcs.TrySetResult(result));
            return tcs.Task;
        }
    }
}
