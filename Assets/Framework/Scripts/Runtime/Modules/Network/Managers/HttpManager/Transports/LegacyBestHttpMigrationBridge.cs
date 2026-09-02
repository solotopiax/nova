#if NOVA_LEGACY_BESTHTTP_MIGRATION

using System;
using System.Net;
using System.Threading;
using Cysharp.Threading.Tasks;

namespace NovaFramework.Runtime
{
    /// <summary>
    /// 已下架 BestHTTP adapter 的一次性升级编译桥梁。
    /// 仅在旧 adapter 仍安装时编译；迁移器移除旧包后自动消失。
    /// </summary>
    public interface IHttpTransport
    {
        void Initialize(float requestTimeout, float connectTimeout);
        bool CanUseIpCandidate(Uri uri);
        UniTask<HttpResponse> GetAsync(string url, float requestTimeout, float connectTimeout, string headerInfos, string hostHeader);
        UniTask<HttpResponse> PostAsync(string url, byte[] bodyBytes, float requestTimeout, float connectTimeout, string headerInfos, string hostHeader);
        UniTask<HttpResponse> PostRawDataAsync(string url, byte[] contentBytes, float requestTimeout, float connectTimeout, string headerInfos, string hostHeader);
        UniTask<HttpResponse> PostFileAsync(string url, string bodyJsonData, byte[] fileBytes, string fileName, float requestTimeout, float connectTimeout, string headerInfos, string hostHeader);
        UniTask<HttpResponse> DownloadBinaryAsync(string url, int idleTimeout, Action<HttpResponse> progressCallback, CancellationToken cancellationToken, string hostHeader);
        UniTask<HttpResponse> DownloadTextAsync(string url, int idleTimeout, Action<HttpResponse> progressCallback, CancellationToken cancellationToken, string hostHeader);
        void Shutdown();
    }

    public interface IHttpIPAddressTransport
    {
        bool IsIPAddressRoutingAvailable { get; }
        string IPAddressRoutingUnavailableReason { get; }
        UniTask<HttpResponse> PostRawDataAsync(string url, IPAddress connectIPAddress, byte[] contentBytes, float requestTimeout, float connectTimeout, string headerInfos);
    }

    public interface IBusinessHttpTelemetryTransport
    {
        IBusinessHttpTelemetryScope BeginBusinessHttpTelemetry(string operationName);
        UniTask<HttpResponse> PostBusinessRawDataAsync(IBusinessHttpTelemetryScope telemetryScope, string url, IPAddress connectIPAddress, byte[] contentBytes, float requestTimeout, float connectTimeout, string headerInfos);
    }

    public interface IBusinessHttpTelemetryScope : IDisposable
    {
        void Complete();
    }

    public interface IHttpTransportFactory
    {
        int Priority { get; }
        IHttpTransport Create();
    }

    /// <summary>
    /// 接受旧 adapter 的启动注册但不保存、不启用它；Framework 始终使用 UWR。
    /// </summary>
    public static class HttpTransportRegistry
    {
        public static void Register(IHttpTransportFactory factory)
        {
        }
    }
}

#endif
