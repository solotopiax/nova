using System;
using System.Threading;
using Cysharp.Threading.Tasks;
using Facebook.Unity;
using NovaFramework.Runtime;

namespace NovaFramework.SDK.Facebook
{
    /// <summary>
    /// Facebook Graph API UniTask 封装。
    /// </summary>
    public sealed class FacebookGraphService
    {
        // 本地编号只关联本次请求与其回调，不改变原厂外部操作的归属。
        private static int s_RequestId;

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

            int requestId = Interlocked.Increment(ref s_RequestId);
            string route = path.Length > 240 ? path.Substring(0, 237) + "..." : path;
            Log.Debug(LogTag.Facebook, $"Graph GET 开始：request={requestId}，route={route}。");
            try
            {
                var tcs = new UniTaskCompletionSource<IGraphResult>();
                FB.API(path, HttpMethod.GET, result =>
                {
                    if (result == null || !string.IsNullOrEmpty(result.Error))
                    {
                        string error = result?.Error ?? "回调结果为空";
                        Log.Warning(LogTag.Facebook, $"Graph GET 失败：request={requestId}，route={route}，error={(error.Length > 240 ? error.Substring(0, 237) + "..." : error)}。");
                    }
                    else
                    {
                        Log.Debug(LogTag.Facebook, $"Graph GET 完成：request={requestId}，route={route}，字段数={result.ResultDictionary?.Count ?? 0}，含纹理={result.Texture != null}。");
                    }
                    tcs.TrySetResult(result);
                });
                return tcs.Task;
            }
            catch (Exception ex)
            {
                Log.Warning(LogTag.Facebook, $"Graph GET 异常：request={requestId}，route={route}，error={(ex.Message.Length > 240 ? ex.Message.Substring(0, 237) + "..." : ex.Message)}。");
                throw;
            }
        }
    }
}
