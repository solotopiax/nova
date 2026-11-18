using System.Threading;
using Cysharp.Threading.Tasks;
using Facebook.Unity;
using NovaFramework.Runtime;

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
            Log.Debug(LogTag.Facebook, "分享调用开始。");
            if (request == null || request.ContentUrl == null)
            {
                Log.Warning(LogTag.Facebook, "分享未执行：链接为空。");
                return UniTask.FromResult(new FacebookShareResult
                {
                    Success = false,
                    ErrorMessage = "Facebook 分享链接为空。"
                });
            }

            try
            {
                ct.ThrowIfCancellationRequested();
                var tcs = new UniTaskCompletionSource<FacebookShareResult>();
                FB.ShareLink(
                    request.ContentUrl,
                    request.Title,
                    request.Description,
                    request.PhotoUrl,
                    result =>
                    {
                        FacebookShareResult share = BuildShareResult(result);
                        Log.Debug(LogTag.Facebook, $"分享回调：成功={share.Success}，取消={share.Cancelled}；不代表接收者已收到。");
                        if (!share.Success)
                            Log.Warning(LogTag.Facebook, $"分享未完成：error={(share.ErrorMessage?.Length > 240 ? share.ErrorMessage.Substring(0, 237) + "..." : share.ErrorMessage)}。");
                        tcs.TrySetResult(share);
                    });
                return tcs.Task.AttachExternalCancellation(ct);
            }
            catch (System.OperationCanceledException)
            {
                Log.Debug(LogTag.Facebook, "分享调用已取消。");
                throw;
            }
            catch (System.Exception ex)
            {
                Log.Warning(LogTag.Facebook, $"分享异常：error={(ex.Message.Length > 240 ? ex.Message.Substring(0, 237) + "..." : ex.Message)}。");
                throw;
            }
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
