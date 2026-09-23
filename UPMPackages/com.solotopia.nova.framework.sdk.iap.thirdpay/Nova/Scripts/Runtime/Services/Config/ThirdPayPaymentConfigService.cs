/***************************************************************
 * (c) copyright 2026 - 2030, Solotopia
 * All Rights Reserved.
 * -------------------------------------------------------------
 * filename:  ThirdPayPaymentConfigService.cs
 * author:    yingzheng
 * created:   2026/9/16
 * descrip:   第三方支付配置快照加载、缓存与并发合并服务
 ***************************************************************/

using System;
using System.Collections.Generic;
using System.Threading;
using Cysharp.Threading.Tasks;
using NovaFramework.Runtime;

namespace NovaFramework.SDK.IAP.ThirdPay.Runtime
{
    /// <summary>
    /// 标识一次第三方支付配置请求所属的账号、国家和路由上下文。
    /// </summary>
    internal readonly struct ThirdPayPaymentConfigContext : IEquatable<ThirdPayPaymentConfigContext>
    {
        /// <summary>
        /// 当前账号 UID。
        /// </summary>
        public readonly string UserId;

        /// <summary>
        /// 当前支付国家码。
        /// </summary>
        public readonly string CountryCode;

        /// <summary>
        /// 当前统一支付配置协议名。
        /// </summary>
        public readonly string CmdName;

        /// <summary>
        /// 创建规范化后的配置请求上下文。
        /// </summary>
        /// <param name="userId">当前账号 UID。</param>
        /// <param name="countryCode">当前支付国家码。</param>
        /// <param name="cmdName">统一支付配置协议名。</param>
        public ThirdPayPaymentConfigContext(string userId, string countryCode, string cmdName)
        {
            UserId = userId ?? string.Empty;
            CountryCode = countryCode ?? string.Empty;
            CmdName = cmdName ?? string.Empty;
        }

        /// <summary>
        /// 判断两个请求是否属于同一账号、国家和路由。
        /// </summary>
        /// <param name="other">待比较的配置请求上下文。</param>
        /// <returns>账号、国家和协议名均相同时返回 true。</returns>
        public bool Equals(ThirdPayPaymentConfigContext other)
        {
            return string.Equals(UserId, other.UserId, StringComparison.Ordinal)
                && string.Equals(CountryCode, other.CountryCode, StringComparison.OrdinalIgnoreCase)
                && string.Equals(CmdName, other.CmdName, StringComparison.Ordinal);
        }

        /// <summary>
        /// 判断指定对象是否为相同的配置请求上下文。
        /// </summary>
        /// <param name="obj">待比较的对象。</param>
        /// <returns>对象为相同配置请求上下文时返回 true。</returns>
        public override bool Equals(object obj)
        {
            return obj is ThirdPayPaymentConfigContext other && Equals(other);
        }

        /// <summary>
        /// 获取账号、国家和路由组合后的哈希值。
        /// </summary>
        /// <returns>当前配置请求上下文的哈希值。</returns>
        public override int GetHashCode()
        {
            unchecked
            {
                int hashCode = StringComparer.Ordinal.GetHashCode(UserId);
                hashCode = (hashCode * 397) ^ StringComparer.OrdinalIgnoreCase.GetHashCode(CountryCode);
                hashCode = (hashCode * 397) ^ StringComparer.Ordinal.GetHashCode(CmdName);
                return hashCode;
            }
        }
    }

    /// <summary>
    /// 已校验并可供支付流程读取的第三方支付配置快照。
    /// </summary>
    internal sealed class ThirdPayPaymentConfigSnapshot
    {
        /// <summary>
        /// 当前快照中的第三方商品列表。
        /// </summary>
        private readonly List<PbNetThirdProductInfo> m_Products;

        /// <summary>
        /// 按第三方商品 ID 建立的商品查询索引。
        /// </summary>
        private readonly Dictionary<string, PbNetThirdProductInfo> m_ProductLookup;

        /// <summary>
        /// 获取服务端是否允许当前上下文发起第三方支付。
        /// </summary>
        public bool Enabled { get; }

        /// <summary>
        /// 获取服务端返回的支付禁用原因编码。
        /// </summary>
        public int DisabledReason { get; }

        /// <summary>
        /// 获取当前快照中的只读商品列表。
        /// </summary>
        public IReadOnlyList<PbNetThirdProductInfo> Products => m_Products;

        /// <summary>
        /// 获取支付渠道客户号参数。
        /// </summary>
        public string PaymentCustomerIds { get; }

        /// <summary>
        /// 获取支付页 HTTPS 地址。
        /// </summary>
        public string PaymentPageUrl { get; }

        /// <summary>
        /// 获取本次配置请求使用的国家码。
        /// </summary>
        public string CountryCode { get; }

        /// <summary>
        /// 将协议响应转换为不可变的运行时快照，并建立商品索引。
        /// </summary>
        /// <param name="response">已通过基础校验的统一支付配置响应。</param>
        /// <param name="fallbackCountryCode">请求统一配置时使用的国家码。</param>
        public ThirdPayPaymentConfigSnapshot(PbNetThirdPaymentConfigResp response, string fallbackCountryCode)
        {
            Enabled = response.Availability.Enabled;
            DisabledReason = response.Availability.DisabledReason;
            PaymentCustomerIds = response.ChannelConfig?.PaymentCustomerIds ?? string.Empty;
            PaymentPageUrl = response.PaymentPageConfig?.PaymentPageUrl ?? string.Empty;
            CountryCode = fallbackCountryCode ?? string.Empty;

            m_Products = response.ProductConfig == null ? new List<PbNetThirdProductInfo>() : new List<PbNetThirdProductInfo>(response.ProductConfig.ProductList);
            m_ProductLookup = new Dictionary<string, PbNetThirdProductInfo>(StringComparer.Ordinal);
            foreach (PbNetThirdProductInfo product in m_Products)
            {
                if (product != null && !string.IsNullOrEmpty(product.ProductId))
                {
                    m_ProductLookup[product.ProductId] = product;
                }
            }
        }

        /// <summary>
        /// 按第三方商品 ID 查询当前快照中的商品。
        /// </summary>
        /// <param name="productId">第三方商品 ID。</param>
        /// <returns>匹配的商品；输入为空或未命中时返回 null。</returns>
        public PbNetThirdProductInfo FindProduct(string productId)
        {
            return !string.IsNullOrEmpty(productId) && m_ProductLookup.TryGetValue(productId, out PbNetThirdProductInfo product) ? product : null;
        }
    }

    /// <summary>
    /// 集中管理第三方支付启动配置的请求、校验、缓存、并发合并和失效响应隔离。
    /// </summary>
    internal sealed class ThirdPayPaymentConfigService
    {
        /// <summary>
        /// 单次配置拉取允许的最大请求次数。
        /// </summary>
        private const int c_MaxAttempts = 3;

        /// <summary>
        /// 发起统一支付配置网络请求的回调。
        /// </summary>
        private readonly Func<string, string, UniTask<NetResponse<PbNetThirdPaymentConfigResp>>> m_RequestAsync;

        /// <summary>
        /// 最近一次成功且仍有效的支付配置快照。
        /// </summary>
        private ThirdPayPaymentConfigSnapshot m_Snapshot;

        /// <summary>
        /// 当前配置快照对应的请求上下文。
        /// </summary>
        private ThirdPayPaymentConfigContext m_SnapshotContext;

        /// <summary>
        /// 当前在途配置请求对应的请求上下文。
        /// </summary>
        private ThirdPayPaymentConfigContext m_InFlightContext;

        /// <summary>
        /// 相同上下文调用方共享的在途请求完成源。
        /// </summary>
        private UniTaskCompletionSource<bool> m_InFlightCompletion;

        /// <summary>
        /// 配置请求版本号，用于阻止失效响应覆盖当前快照。
        /// </summary>
        private int m_RequestVersion;

        /// <summary>
        /// 最近一次配置拉取失败的具体原因。
        /// </summary>
        private string m_LastFailureReason = string.Empty;

        /// <summary>
        /// 获取最近一次成功且仍有效的支付配置快照。
        /// </summary>
        public ThirdPayPaymentConfigSnapshot Snapshot => m_Snapshot;

        /// <summary>
        /// 获取最近一次配置拉取失败的具体原因；尚无失败信息时为空字符串。
        /// </summary>
        public string LastFailureReason => m_LastFailureReason;

        /// <summary>
        /// 创建第三方支付配置服务。
        /// </summary>
        /// <param name="requestAsync">发起统一支付配置网络请求的回调。</param>
        public ThirdPayPaymentConfigService(Func<string, string, UniTask<NetResponse<PbNetThirdPaymentConfigResp>>> requestAsync)
        {
            m_RequestAsync = requestAsync ?? throw new ArgumentNullException(nameof(requestAsync));
        }

        /// <summary>
        /// 确保指定上下文已有有效配置；相同上下文的并发调用共享同一个网络请求。
        /// </summary>
        /// <param name="context">本次请求的账号、国家及协议上下文。</param>
        /// <param name="ct">调用方取消等待使用的取消令牌。</param>
        /// <returns>取得有效配置快照时返回 true。</returns>
        public UniTask<bool> EnsureAsync(ThirdPayPaymentConfigContext context, CancellationToken ct)
        {
            ct.ThrowIfCancellationRequested();
            if (Snapshot != null && m_SnapshotContext.Equals(context))
            {
                m_LastFailureReason = string.Empty;
                return UniTask.FromResult(true);
            }

            if (m_InFlightCompletion != null && m_InFlightContext.Equals(context))
            {
                return m_InFlightCompletion.Task.AttachExternalCancellation(ct);
            }

            InvalidateInFlight();
            m_LastFailureReason = string.Empty;
            int requestVersion = ++m_RequestVersion;
            var completion = new UniTaskCompletionSource<bool>();
            m_InFlightContext = context;
            m_InFlightCompletion = completion;
            RequestAsync(context, requestVersion, completion).Forget();
            return completion.Task.AttachExternalCancellation(ct);
        }

        /// <summary>
        /// 使当前快照和在途请求失效，后续调用将重新拉取配置。
        /// </summary>
        public void Invalidate()
        {
            m_RequestVersion++;
            m_Snapshot = null;
            m_SnapshotContext = default;
            m_LastFailureReason = string.Empty;
            InvalidateInFlight();
        }

        /// <summary>
        /// 清理配置服务持有的全部运行时状态。
        /// </summary>
        public void Reset()
        {
            Invalidate();
        }

        /// <summary>
        /// 按第三方商品 ID 查询当前有效快照中的商品。
        /// </summary>
        /// <param name="productId">第三方商品 ID。</param>
        /// <returns>匹配的商品；快照不存在或未命中时返回 null。</returns>
        public PbNetThirdProductInfo FindProduct(string productId)
        {
            return Snapshot?.FindProduct(productId);
        }

        /// <summary>
        /// 执行统一配置请求，并仅在请求上下文仍有效时原子应用响应。
        /// </summary>
        /// <param name="context">本次网络请求对应的配置上下文。</param>
        /// <param name="requestVersion">发起请求时捕获的配置版本号。</param>
        /// <param name="completion">向同上下文等待方发布结果的完成源。</param>
        /// <returns>网络请求和快照写回完成的异步任务。</returns>
        private async UniTask RequestAsync(ThirdPayPaymentConfigContext context, int requestVersion, UniTaskCompletionSource<bool> completion)
        {
            try
            {
                string failureReason = string.Empty;
                for (int attempt = 0; attempt < c_MaxAttempts; attempt++)
                {
                    NetResponse<PbNetThirdPaymentConfigResp> response = await m_RequestAsync(context.CmdName, context.CountryCode);
                    if (!TryCreateSnapshot(response, context.CountryCode, out ThirdPayPaymentConfigSnapshot snapshot, out failureReason))
                    {
                        continue;
                    }

                    if (requestVersion != m_RequestVersion || !ReferenceEquals(m_InFlightCompletion, completion)
                        || !m_InFlightContext.Equals(context))
                    {
                        completion.TrySetResult(false);
                        return;
                    }

                    m_Snapshot = snapshot;
                    m_SnapshotContext = context;
                    m_LastFailureReason = string.Empty;
                    completion.TrySetResult(true);
                    return;
                }

                if (requestVersion == m_RequestVersion && ReferenceEquals(m_InFlightCompletion, completion))
                {
                    m_LastFailureReason = string.IsNullOrEmpty(failureReason) ? "配置请求重试耗尽，未取得有效响应。" : failureReason;
                }

                completion.TrySetResult(false);
            }
            catch (Exception ex)
            {
                completion.TrySetException(ex);
            }
            finally
            {
                if (ReferenceEquals(m_InFlightCompletion, completion))
                {
                    m_InFlightCompletion = null;
                    m_InFlightContext = default;
                }
            }
        }

        /// <summary>
        /// 校验协议响应并构造配置快照；启用状态下商品配置和 HTTPS 支付页地址必须存在。
        /// </summary>
        /// <param name="response">统一支付配置网络响应。</param>
        /// <param name="fallbackCountryCode">请求配置时使用的国家码。</param>
        /// <param name="snapshot">校验成功后构造的配置快照。</param>
        /// <param name="failureReason">校验失败时返回可用于诊断日志的具体原因。</param>
        /// <returns>响应有效并成功构造快照时返回 true。</returns>
        private static bool TryCreateSnapshot(NetResponse<PbNetThirdPaymentConfigResp> response, string fallbackCountryCode, out ThirdPayPaymentConfigSnapshot snapshot, out string failureReason)
        {
            snapshot = null;
            failureReason = string.Empty;
            if (response == null)
            {
                failureReason = "网络响应为空。";
                return false;
            }

            if (!response.IsSuccess)
            {
                failureReason = $"网络请求失败，ErrorCode={response.ErrorCode}，ErrorMessage={response.ErrorMessage ?? string.Empty}。";
                return false;
            }

            PbNetThirdPaymentConfigResp data = response.Data;
            if (data == null)
            {
                failureReason = "协议响应数据为空。";
                return false;
            }

            if (data.Availability == null)
            {
                failureReason = "协议响应缺少 availability。";
                return false;
            }

            if (data.Availability.Enabled)
            {
                string paymentPageUrl = data.PaymentPageConfig?.PaymentPageUrl;
                if (data.ProductConfig == null)
                {
                    failureReason = "第三方支付已开启，但协议响应缺少 product_config。";
                    return false;
                }

                if (!Uri.TryCreate(paymentPageUrl, UriKind.Absolute, out Uri uri) || !string.Equals(uri.Scheme, Uri.UriSchemeHttps, StringComparison.OrdinalIgnoreCase))
                {
                    failureReason = "第三方支付已开启，但 payment_page_url 不是有效的 HTTPS 地址。";
                    return false;
                }
            }

            snapshot = new ThirdPayPaymentConfigSnapshot(data, fallbackCountryCode);
            return true;
        }

        /// <summary>
        /// 结束当前共享等待并使尚未返回的网络请求失去写回资格。
        /// </summary>
        private void InvalidateInFlight()
        {
            UniTaskCompletionSource<bool> completion = m_InFlightCompletion;
            m_InFlightCompletion = null;
            m_InFlightContext = default;
            completion?.TrySetResult(false);
        }
    }
}
