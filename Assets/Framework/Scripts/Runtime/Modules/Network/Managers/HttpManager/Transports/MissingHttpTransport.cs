using System;
using System.Threading;
using Cysharp.Threading.Tasks;

namespace NovaFramework.Runtime
{
    internal sealed class MissingHttpTransport : IHttpTransport
    {
        private const string c_ErrorMessage = "Nova HTTP backend unavailable. Install com.solotopia.nova.framework.besthttp and BestHTTP/TLS. Solotopia members can install commercial libraries from the PlugPals internal cloud registry.";

        internal static readonly MissingHttpTransport Instance = new MissingHttpTransport();

        private MissingHttpTransport()
        {
        }

        public void Initialize(float requestTimeout, float connectTimeout)
        {
        }

        public UniTask<HttpResponse> GetAsync(string url, float requestTimeout, float connectTimeout, string headerInfos, string hostHeader)
        {
            return CreateUnavailableResponseAsync();
        }

        public UniTask<HttpResponse> PostAsync(string url, byte[] bodyBytes, float requestTimeout, float connectTimeout, string headerInfos, string hostHeader)
        {
            return CreateUnavailableResponseAsync();
        }

        public UniTask<HttpResponse> PostRawDataAsync(string url, byte[] contentBytes, float requestTimeout, float connectTimeout, string headerInfos, string hostHeader)
        {
            return CreateUnavailableResponseAsync();
        }

        public UniTask<HttpResponse> PostFileAsync(string url, string bodyJsonData, byte[] fileBytes, string fileName, float requestTimeout, float connectTimeout, string headerInfos, string hostHeader)
        {
            return CreateUnavailableResponseAsync();
        }

        public UniTask<HttpResponse> DownloadBinaryAsync(string url, int idleTimeout, Action<HttpResponse> progressCallback, CancellationToken cancellationToken, string hostHeader)
        {
            return CreateUnavailableResponseAsync();
        }

        public UniTask<HttpResponse> DownloadTextAsync(string url, int idleTimeout, Action<HttpResponse> progressCallback, CancellationToken cancellationToken, string hostHeader)
        {
            return CreateUnavailableResponseAsync();
        }

        public void Shutdown()
        {
        }

        private static UniTask<HttpResponse> CreateUnavailableResponseAsync()
        {
            return UniTask.FromResult(HttpResponse.Create(0, null, null, null, c_ErrorMessage, false, 0, -1L));
        }
    }
}
