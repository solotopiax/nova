using System.Threading;
using Cysharp.Threading.Tasks;
using Facebook.Unity;

namespace NovaFramework.SDK.Facebook
{
    /// <summary>
    /// Facebook 分享服务。
    /// </summary>
    public sealed class FacebookShareService
    {
        /// <summary>
        /// 分享链接。
        /// </summary>
        /// <param name="request">分享请求。</param>
        /// <param name="ct">取消令牌。</param>
        /// <returns>分享结果。</returns>
        public UniTask<FacebookShareResult> ShareLinkAsync(FacebookShareRequest request, CancellationToken ct = default)
        {
            ct.ThrowIfCancellationRequested();
            if (request == null || request.ContentUrl == null)
            {
                return UniTask.FromResult(new FacebookShareResult
                {
                    Success = false,
                    ErrorMessage = "Facebook 分享链接为空。"
                });
            }

            var tcs = new UniTaskCompletionSource<FacebookShareResult>();
            FB.ShareLink(
                request.ContentUrl,
                request.Title,
                request.Description,
                request.PhotoUrl,
                result => tcs.TrySetResult(BuildShareResult(result)));

            return tcs.Task.AttachExternalCancellation(ct);
        }

        /// <summary>
        /// 构造分享结果。
        /// </summary>
        /// <param name="result">Facebook 分享结果。</param>
        /// <returns>框架分享结果。</returns>
        private static FacebookShareResult BuildShareResult(IShareResult result)
        {
            if (result == null)
            {
                return new FacebookShareResult { Success = false, ErrorMessage = "Facebook 分享结果为空。" };
            }

            return new FacebookShareResult
            {
                Success = string.IsNullOrEmpty(result.Error) && !result.Cancelled,
                Cancelled = result.Cancelled,
                ErrorMessage = result.Error,
                RawResult = result.RawResult
            };
        }
    }
}
