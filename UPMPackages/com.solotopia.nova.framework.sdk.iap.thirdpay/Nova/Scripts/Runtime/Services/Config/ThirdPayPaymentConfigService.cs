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
using System.Collections.ObjectModel;
using System.Threading;
using Cysharp.Threading.Tasks;
using NovaFramework.Runtime;
using NovaFramework.SDK.IAP.Runtime;
using Newtonsoft.Json;

namespace NovaFramework.SDK.IAP.ThirdPay.Runtime
{
    /// <summary>
    /// 服务端下发给客户端的第三方支付扩展配置。
    /// </summary>
    internal sealed class ThirdPayClientConfig
    {
        /// <summary>
        /// 获取服务端客户端配置包含的全部字段。
        /// </summary>
        public IReadOnlyDictionary<string, object> Values { get; }

        /// <summary>
        /// 获取服务端指定的 Google 信息页跳过策略；缺失或 null 时沿用本地开关。
        /// JSON 键名精确为 AndroidSkipPaymentInformationScreen，仅接受布尔值，不转换字符串或数字。
        /// </summary>
        public bool? AndroidSkipPaymentInformationScreen { get; }

        /// <summary>
        /// 获取服务端购买前补单查询开关；缺失或 null 时沿用本地。
        /// </summary>
        public bool? AndroidOrderRecoveryEnabled { get; }

        /// <summary>
        /// 获取服务端补单窗口分钟数，允许 0 至 30；缺失或 null 时沿用本地。
        /// </summary>
        public int? AndroidOrderRecoveryWindowMinutes { get; }

        /// <summary>
        /// 获取服务端浏览器返回验单延迟秒数，必须为有限正数；缺失或 null 时沿用本地。
        /// </summary>
        public double? AndroidExternalBrowserReturnValidateDelaySeconds { get; }

        /// <summary>
        /// 获取服务端 Google 网络操作超时秒数，必须为有限正数；缺失或 null 时沿用本地。
        /// </summary>
        public double? AndroidGoogleApiTimeoutSeconds { get; }

        /// <summary>
        /// 将服务端客户端 JSON 配置转换为强类型字段与只读动态字典。
        /// </summary>
        /// <param name="json">服务端下发的客户端配置 JSON。</param>
        /// <exception cref="JsonSerializationException">JSON 不是对象，或线上字段的类型及范围无效。</exception>
        public ThirdPayClientConfig(string json)
        {
            Dictionary<string, object> values = string.IsNullOrWhiteSpace(json) ? null : Util.Json.Deserialize<Dictionary<string, object>>(json);
            Values = new ReadOnlyDictionary<string, object>(values == null ? new Dictionary<string, object>(StringComparer.Ordinal) : new Dictionary<string, object>(values, StringComparer.Ordinal));
            AndroidSkipPaymentInformationScreen = ReadBoolean(nameof(AndroidSkipPaymentInformationScreen));
            AndroidOrderRecoveryEnabled = ReadBoolean(nameof(AndroidOrderRecoveryEnabled));
            AndroidOrderRecoveryWindowMinutes = ReadWindowMinutes();
            AndroidExternalBrowserReturnValidateDelaySeconds = ReadPositiveSeconds(nameof(AndroidExternalBrowserReturnValidateDelaySeconds));
            AndroidGoogleApiTimeoutSeconds = ReadPositiveSeconds(nameof(AndroidGoogleApiTimeoutSeconds));
        }

        /// <summary>
        /// 读取精确键名的可选布尔字段，不转换字符串和数字。
        /// </summary>
        /// <param name="key">JSON 字段名。</param>
        /// <returns>线上值；缺失或 null 返回 null；错误类型抛出配置解析异常。</returns>
        private bool? ReadBoolean(string key)
        {
            if (!Values.TryGetValue(key, out object value) || value == null)
                return null;
            if (value is bool result)
                return result;
            throw new JsonSerializationException($"ThirdPay client_config.{key} 必须为布尔值或 null。");
        }

        /// <summary>
        /// 读取零至三十分钟的补单窗口，仅接受 JSON 整数。
        /// </summary>
        /// <returns>线上窗口；缺失或 null 返回 null；错误类型或范围抛出配置解析异常。</returns>
        private int? ReadWindowMinutes()
        {
            if (!Values.TryGetValue(nameof(AndroidOrderRecoveryWindowMinutes), out object value) || value == null)
                return null;
            if (value is long minutes && minutes >= 0 && minutes <= 30)
                return (int)minutes;
            throw new JsonSerializationException("ThirdPay client_config.AndroidOrderRecoveryWindowMinutes 必须为 0 至 30 的整数或 null。");
        }

        /// <summary>
        /// 读取有限正秒数，拒绝字符串、非数值及无法安全用于计时器的范围。
        /// </summary>
        /// <param name="key">JSON 字段名。</param>
        /// <returns>线上秒数；缺失或 null 返回 null；非法值抛出配置解析异常。</returns>
        private double? ReadPositiveSeconds(string key)
        {
            if (!Values.TryGetValue(key, out object value) || value == null)
                return null;
            if (value is long || value is double || value is decimal)
            {
                double seconds = Convert.ToDouble(value);
                if (!double.IsNaN(seconds) && !double.IsInfinity(seconds) && seconds > 0 && seconds <= int.MaxValue / 1000d)
                    return seconds;
            }
            throw new JsonSerializationException($"ThirdPay client_config.{key} 必须为有效正秒数或 null（最大 {int.MaxValue / 1000d} 秒）。");
        }

        /// <summary>
        /// 按字段名读取服务端下发的客户端配置值。
        /// </summary>
        /// <param name="key">客户端配置字段名。</param>
        /// <param name="value">读取成功时返回对应字段值。</param>
        /// <returns>存在指定字段时返回 true。</returns>
        public bool TryGetValue(string key, out object value)
        {
            return Values.TryGetValue(key, out value);
        }

        /// <summary>
        /// 将全部客户端配置字段序列化为紧凑 JSON。
        /// </summary>
        /// <returns>包含全部客户端配置字段的 JSON。</returns>
        public override string ToString()
        {
            return Util.Json.Serialize(Values, Formatting.None);
        }
    }

    /// <summary>
    /// 单次支付固定使用的有效客户端配置，线上明确值优先，本地值作为缺省。
    /// </summary>
    internal readonly struct ThirdPayEffectiveClientConfig
    {
        /// <summary>
        /// 获取本次信息页跳过策略。
        /// </summary>
        public bool AndroidSkipPaymentInformationScreen { get; }

        /// <summary>
        /// 获取本次购买前补单查询开关。
        /// </summary>
        public bool AndroidOrderRecoveryEnabled { get; }

        /// <summary>
        /// 获取本次补单查询窗口分钟数。
        /// </summary>
        public int AndroidOrderRecoveryWindowMinutes { get; }

        /// <summary>
        /// 获取本次浏览器返回验单延迟秒数。
        /// </summary>
        public double AndroidExternalBrowserReturnValidateDelaySeconds { get; }

        /// <summary>
        /// 获取本次Google 网络操作超时秒数。
        /// </summary>
        public double AndroidGoogleApiTimeoutSeconds { get; }

        /// <summary>
        /// 合并线上可选值与本地当前值，不回写任何配置来源。
        /// </summary>
        /// <param name="online">当前账号与国家的线上配置；不存在时全部使用本地。</param>
        /// <param name="local">本地 Store 配置；不存在时使用框架默认值。</param>
        /// <param name="skipInformationScreen">本地运行期信息页开关。</param>
        /// <param name="recoveryEnabled">本地运行期补单查询开关。</param>
        /// <param name="recoveryWindowMinutes">本地运行期补单查询窗口。</param>
        internal ThirdPayEffectiveClientConfig(ThirdPayClientConfig online, ThirdPayStoreConfig local, bool skipInformationScreen, bool recoveryEnabled, int recoveryWindowMinutes)
        {
            AndroidSkipPaymentInformationScreen = online?.AndroidSkipPaymentInformationScreen ?? skipInformationScreen;
            AndroidOrderRecoveryEnabled = online?.AndroidOrderRecoveryEnabled ?? recoveryEnabled;
            AndroidOrderRecoveryWindowMinutes = online?.AndroidOrderRecoveryWindowMinutes ?? Math.Max(0, Math.Min(30, recoveryWindowMinutes));
            AndroidExternalBrowserReturnValidateDelaySeconds = online?.AndroidExternalBrowserReturnValidateDelaySeconds ?? local?.AndroidExternalBrowserReturnValidateDelaySeconds ?? 2.5d;
            AndroidGoogleApiTimeoutSeconds = online?.AndroidGoogleApiTimeoutSeconds ?? local?.AndroidGoogleApiTimeoutSeconds ?? 15d;
        }
    }

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
        /// 获取当前快照的商品列表视图；元素仍是可变的协议对象引用。
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
        /// 获取服务端客户端 JSON 配置转换后的强类型数据。
        /// </summary>
        public ThirdPayClientConfig ClientConfig { get; }

        /// <summary>
        /// 复制已校验响应的标量和商品列表容器，并建立商品索引；不深复制协议商品对象。
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
            ClientConfig = new ThirdPayClientConfig(response.ClientConfig);

            m_Products = response.ProductConfig == null ? new List<PbNetThirdProductInfo>() : new List<PbNetThirdProductInfo>(response.ProductConfig.ProductList);
            m_ProductLookup = new Dictionary<string, PbNetThirdProductInfo>(StringComparer.Ordinal);
            foreach (PbNetThirdProductInfo product in m_Products)
            {
                if (product != null && !string.IsNullOrEmpty(product.ProductId))
                {
                    m_ProductLookup[product.ProductId] = product;
                }
            }

            IAPLog.Debug(LogTag.IAPThirdPay, $"ThirdPayClientConfig={ClientConfig}");
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
        /// 获取最近一次成功保存的配置快照；读取本身不核对当前上下文或触发刷新。
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
        /// 按第三方商品 ID 查询已保存快照中的商品，不检查调用方当前配置上下文。
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
