/***************************************************************
 * (c) copyright 2026 - 2030, Solotopia
 * All Rights Reserved.
 * -------------------------------------------------------------
 * filename:  ThirdPayGooglePolicyService.cs
 * author:    yingzheng
 * created:   2026/8/3
 * descrip:   Android Google 外部结算政策流程
 ***************************************************************/

using System;
using System.Threading;
using Cysharp.Threading.Tasks;

namespace NovaFramework.SDK.IAP.ThirdPay.Runtime
{
    /// <summary>
    /// Google 外部结算 API 的统一响应状态。
    /// </summary>
    internal enum ThirdPayGoogleResponse
    {
        /// <summary>
        /// 请求成功。
        /// </summary>
        Ok = 0,

        /// <summary>
        /// 当前设备、账号或地区不支持该计划。
        /// </summary>
        Unavailable = 1,

        /// <summary>
        /// 用户取消 Google 信息页。
        /// </summary>
        UserCancelled = 2,

        /// <summary>
        /// 其他请求失败。
        /// </summary>
        Failed = 3,

        /// <summary>
        /// 请求在超时时间内未收到 Google 响应。
        /// </summary>
        Timeout = 4,
    }

    /// <summary>
    /// Google 外部交易上报 token 创建结果。
    /// </summary>
    internal readonly struct ThirdPayGoogleTokenResult
    {
        /// <summary>
        /// 初始化 token 创建结果。
        /// </summary>
        /// <param name="response">统一响应状态。</param>
        /// <param name="token">Google 外部交易上报 token。</param>
        public ThirdPayGoogleTokenResult(ThirdPayGoogleResponse response, string token)
        {
            Response = response;
            Token = token ?? string.Empty;
        }

        /// <summary>
        /// 获取统一响应状态。
        /// </summary>
        public ThirdPayGoogleResponse Response { get; }

        /// <summary>
        /// 获取 Google 外部交易上报 token。
        /// </summary>
        public string Token { get; }
    }

    /// <summary>
    /// Google 外部结算政策授权结果状态。
    /// </summary>
    internal enum ThirdPayGoogleAuthorizationStatus
    {
        /// <summary>
        /// 已完成政策授权。
        /// </summary>
        Authorized = 0,

        /// <summary>
        /// 当前环境不支持外部结算计划。
        /// </summary>
        ProgramUnavailable = 1,

        /// <summary>
        /// 无法连接 Google 结算服务。
        /// </summary>
        ConnectionFailed = 2,

        /// <summary>
        /// 无法创建外部交易上报 token。
        /// </summary>
        TokenCreationFailed = 3,

        /// <summary>
        /// Google 信息页打开失败。
        /// </summary>
        LaunchFailed = 4,

        /// <summary>
        /// 用户取消 Google 信息页。
        /// </summary>
        UserCancelled = 5,

        /// <summary>
        /// 已取得 token 但构造最终支付 URL 失败。
        /// </summary>
        UrlBuildFailed = 6,
    }

    /// <summary>
    /// Google 外部结算政策授权结果。
    /// </summary>
    internal readonly struct ThirdPayGoogleAuthorization
    {
        /// <summary>
        /// 初始化政策授权结果。
        /// </summary>
        /// <param name="status">授权状态。</param>
        /// <param name="googleToken">Google 外部交易上报 token。</param>
        /// <param name="paymentUrl">包含 token 的最终支付 URL。</param>
        public ThirdPayGoogleAuthorization(ThirdPayGoogleAuthorizationStatus status, string googleToken = null, string paymentUrl = null)
        {
            Status = status;
            GoogleToken = googleToken ?? string.Empty;
            PaymentUrl = paymentUrl ?? string.Empty;
        }

        /// <summary>
        /// 获取授权状态。
        /// </summary>
        public ThirdPayGoogleAuthorizationStatus Status { get; }

        /// <summary>
        /// 获取 Google 外部交易上报 token。
        /// </summary>
        public string GoogleToken { get; }

        /// <summary>
        /// 获取包含 token 的最终支付 URL。
        /// </summary>
        public string PaymentUrl { get; }
    }

    /// <summary>
    /// Google 外部结算客户端抽象，隔离 Unity Purchasing API 并支持定向测试。
    /// </summary>
    internal interface IThirdPayGoogleExternalBillingClient : IDisposable
    {
        /// <summary>
        /// 获取当前客户端连接是否可用。
        /// </summary>
        bool IsReady { get; }

        /// <summary>
        /// 建立 Google 外部结算服务连接。
        /// </summary>
        /// <param name="ct">取消令牌。</param>
        /// <returns>统一响应状态。</returns>
        UniTask<ThirdPayGoogleResponse> ConnectAsync(CancellationToken ct);

        /// <summary>
        /// 查询当前用户是否可使用外部结算计划。
        /// </summary>
        /// <param name="ct">取消令牌。</param>
        /// <returns>统一响应状态。</returns>
        UniTask<ThirdPayGoogleResponse> CheckAvailabilityAsync(CancellationToken ct);

        /// <summary>
        /// 读取 Google Play Billing 当前商店国家或地区代码。
        /// </summary>
        /// <param name="ct">取消令牌。</param>
        /// <returns>ISO 3166-1 alpha-2 代码；无法读取时返回空字符串。</returns>
        UniTask<string> GetBillingCountryCodeAsync(CancellationToken ct);

        /// <summary>
        /// 创建 Google 外部交易上报 token。
        /// </summary>
        /// <param name="ct">取消令牌。</param>
        /// <returns>token 创建结果。</returns>
        UniTask<ThirdPayGoogleTokenResult> CreateTokenAsync(CancellationToken ct);

        /// <summary>
        /// 打开 Google 外部链接信息页。
        /// </summary>
        /// <param name="url">最终第三方支付 URL。</param>
        /// <param name="ct">取消令牌。</param>
        /// <returns>统一响应状态。</returns>
        UniTask<ThirdPayGoogleResponse> LaunchInformationScreenAsync(string url, CancellationToken ct);
    }

    /// <summary>
    /// Google 外部结算政策流程：连接、资格检查、生成上报 token，再展示 Google 信息页。
    /// </summary>
    internal sealed class ThirdPayGooglePolicyService : IDisposable
    {
        /// <summary>
        /// Google 外部结算客户端。
        /// </summary>
        private readonly IThirdPayGoogleExternalBillingClient m_Client;

        /// <summary>
        /// 初始化 Google 外部结算政策服务。
        /// </summary>
        /// <param name="client">Google 外部结算客户端。</param>
        public ThirdPayGooglePolicyService(IThirdPayGoogleExternalBillingClient client)
        {
            m_Client = client ?? throw new ArgumentNullException(nameof(client));
        }

        /// <summary>
        /// 按需连接 Google Billing，并读取 Google Play Billing 商店国家或地区代码。
        /// </summary>
        /// <param name="ct">取消令牌。</param>
        /// <returns>商店国家或地区代码；无法读取时返回空字符串。</returns>
        public async UniTask<string> GetBillingCountryCodeAsync(CancellationToken ct)
        {
            ct.ThrowIfCancellationRequested();
            if (!m_Client.IsReady)
            {
                ThirdPayGoogleResponse connection = await m_Client.ConnectAsync(ct);
                if (connection != ThirdPayGoogleResponse.Ok)
                {
                    return string.Empty;
                }
            }

            string countryCode = await m_Client.GetBillingCountryCodeAsync(ct);
            return string.IsNullOrWhiteSpace(countryCode)
                ? string.Empty
                : countryCode.Trim().ToUpperInvariant();
        }

        /// <summary>
        /// 完成外部结算连接、资格检查、token 创建和 Google 信息页展示。
        /// </summary>
        /// <param name="buildPaymentUrl">使用 Google token 构造最终支付 URL 的回调。</param>
        /// <param name="skipInformationScreen">是否跳过 Google 信息页并直接进入 ThirdPay。</param>
        /// <param name="ct">取消令牌。</param>
        /// <returns>政策授权结果。</returns>
        public async UniTask<ThirdPayGoogleAuthorization> AuthorizeAsync(
            Func<string, string> buildPaymentUrl, bool skipInformationScreen, CancellationToken ct)
        {
            if (buildPaymentUrl == null)
            {
                throw new ArgumentNullException(nameof(buildPaymentUrl));
            }

            ct.ThrowIfCancellationRequested();
            if (!m_Client.IsReady)
            {
                ThirdPayGoogleResponse connection = await m_Client.ConnectAsync(ct);
                if (connection == ThirdPayGoogleResponse.Unavailable)
                {
                    return BuildFallbackAuthorization(buildPaymentUrl);
                }

                if (connection != ThirdPayGoogleResponse.Ok)
                {
                    return new ThirdPayGoogleAuthorization(ThirdPayGoogleAuthorizationStatus.ConnectionFailed);
                }
            }

            ThirdPayGoogleResponse availability = await m_Client.CheckAvailabilityAsync(ct);
            if (availability == ThirdPayGoogleResponse.Unavailable)
            {
                return BuildFallbackAuthorization(buildPaymentUrl);
            }

            if (availability != ThirdPayGoogleResponse.Ok)
            {
                return new ThirdPayGoogleAuthorization(ThirdPayGoogleAuthorizationStatus.ConnectionFailed);
            }

            ThirdPayGoogleTokenResult tokenResult = await m_Client.CreateTokenAsync(ct);
            if (tokenResult.Response != ThirdPayGoogleResponse.Ok || string.IsNullOrEmpty(tokenResult.Token))
            {
                return new ThirdPayGoogleAuthorization(ThirdPayGoogleAuthorizationStatus.TokenCreationFailed);
            }

            string paymentUrl = buildPaymentUrl(tokenResult.Token);
            if (string.IsNullOrEmpty(paymentUrl))
            {
                return new ThirdPayGoogleAuthorization(ThirdPayGoogleAuthorizationStatus.UrlBuildFailed, tokenResult.Token);
            }

            if (skipInformationScreen)
            {
                return new ThirdPayGoogleAuthorization(ThirdPayGoogleAuthorizationStatus.Authorized, tokenResult.Token, paymentUrl);
            }

            ThirdPayGoogleResponse launch = await m_Client.LaunchInformationScreenAsync(paymentUrl, ct);
            if (launch == ThirdPayGoogleResponse.UserCancelled)
            {
                return new ThirdPayGoogleAuthorization(ThirdPayGoogleAuthorizationStatus.UserCancelled, tokenResult.Token, paymentUrl);
            }

            if (launch != ThirdPayGoogleResponse.Ok)
            {
                return new ThirdPayGoogleAuthorization(ThirdPayGoogleAuthorizationStatus.LaunchFailed, tokenResult.Token, paymentUrl);
            }

            return new ThirdPayGoogleAuthorization(ThirdPayGoogleAuthorizationStatus.Authorized, tokenResult.Token, paymentUrl);
        }

        /// <summary>
        /// Google 外链计划不可用时，跳过 Google 信息页并直接进入 ThirdPay WebView。
        /// </summary>
        /// <param name="buildPaymentUrl">使用空 Google token 构造支付 URL 的回调。</param>
        /// <returns>可继续打开 WebView 的授权结果。</returns>
        private static ThirdPayGoogleAuthorization BuildFallbackAuthorization(Func<string, string> buildPaymentUrl)
        {
            // 外链计划不可用只表示当前设备、账号或地区不满足政策条件；
            // 第三方支付仍可通过应用内 WebView 继续，不应把该条件当成支付失败。
            string fallbackPaymentUrl = buildPaymentUrl(string.Empty);
            if (string.IsNullOrEmpty(fallbackPaymentUrl))
            {
                return new ThirdPayGoogleAuthorization(ThirdPayGoogleAuthorizationStatus.UrlBuildFailed);
            }

            return new ThirdPayGoogleAuthorization(ThirdPayGoogleAuthorizationStatus.Authorized, string.Empty, fallbackPaymentUrl);
        }

        /// <summary>
        /// 释放底层 Google 外部结算客户端。
        /// </summary>
        public void Dispose()
        {
            m_Client.Dispose();
        }
    }
}
