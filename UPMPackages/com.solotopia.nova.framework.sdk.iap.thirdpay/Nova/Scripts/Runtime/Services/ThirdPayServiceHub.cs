/***************************************************************
 * (c) copyright 2026 - 2030, Solotopia
 * All Rights Reserved.
 * -------------------------------------------------------------
 * filename:  ThirdPayServiceHub.cs
 * author:    yingzheng
 * created:   2026/9/17
 * descrip:   ThirdPayStore 服务容器，统一持有共享状态、外部依赖与内部服务
 ***************************************************************/

using System;
using System.Threading;
using Cysharp.Threading.Tasks;
using NovaFramework.SDK.IAP.Runtime;

namespace NovaFramework.SDK.IAP.ThirdPay.Runtime
{
    /// <summary>
    /// ThirdPayStore 的强类型服务容器，集中负责依赖装配、后台任务取消和资源释放。
    /// </summary>
    internal sealed class ThirdPayServiceHub : ThirdPayLogOwner
    {
        /// <summary>
        /// Store 运行期后台任务取消源。
        /// </summary>
        private readonly CancellationTokenSource m_RuntimeTaskCts = new CancellationTokenSource();

        /// <summary>
        /// 标识运行期后台任务取消源是否已释放。
        /// </summary>
        private bool m_IsRuntimeTaskCtsDisposed;

        /// <summary>
        /// 标识 Hub 是否已经完成一次服务绑定。
        /// </summary>
        private bool m_IsBound;

        /// <summary>
        /// 获取所属第三方支付 Store。
        /// </summary>
        internal ThirdPayStore Store { get; }

        /// <summary>
        /// 获取 IAP Store 运行上下文。
        /// </summary>
        internal IIAPStoreContext Context { get; private set; }

        /// <summary>
        /// 获取第三方支付 Store 配置。
        /// </summary>
        internal ThirdPayStoreConfig Config { get; private set; }

        /// <summary>
        /// 获取支付商品表。
        /// </summary>
        internal IIAPProductTable Table { get; private set; }

        /// <summary>
        /// 获取第三方支付协议服务。
        /// </summary>
        internal ThirdIapNetService NetService { get; private set; }

        /// <summary>
        /// 获取统一支付配置服务。
        /// </summary>
        internal ThirdPayPaymentConfigService PaymentConfigService { get; private set; }

        /// <summary>
        /// 获取国家码解析协调器。
        /// </summary>
        internal ThirdPayCountryResolver CountryResolver { get; private set; }

        /// <summary>
        /// 获取订单验单服务。
        /// </summary>
        internal ThirdPayOrderValidationService OrderValidationService { get; private set; }

        /// <summary>
        /// 获取待验订单恢复服务。
        /// </summary>
        internal ThirdPayOrderRecoveryService OrderRecoveryService { get; private set; }

        /// <summary>
        /// 获取支付主流程协调器。
        /// </summary>
        internal ThirdPayCheckoutCoordinator CheckoutCoordinator { get; private set; }

        /// <summary>
        /// 获取应用内支付页服务。
        /// </summary>
        internal IThirdPayWebViewService WebViewService { get; private set; }

        /// <summary>
        /// 获取外部浏览器支付页服务。
        /// </summary>
        internal IThirdPayExternalBrowserService ExternalBrowserService { get; private set; }

        /// <summary>
        /// 获取或设置 Android Google 外链政策服务。
        /// </summary>
        internal ThirdPayGooglePolicyService GooglePolicy { get; set; }

        /// <summary>
        /// 获取国家码来源状态；该状态在 Store 初始化前即可安全访问。
        /// </summary>
        internal ThirdPayCountryState CountryState { get; } = new ThirdPayCountryState();

        /// <summary>
        /// 获取当前账号持久化上下文；该状态在 Store 初始化前即可安全访问。
        /// </summary>
        internal ThirdPayPersistContext PersistContext { get; } = new ThirdPayPersistContext();

        /// <summary>
        /// 获取外部浏览器支付会话状态；该状态在 Store 初始化前即可安全访问。
        /// </summary>
        internal ThirdPayExternalBrowserSessionState ExternalBrowserSessionState { get; } = new ThirdPayExternalBrowserSessionState();

        /// <summary>
        /// 获取或设置是否跳过 Google 第三方支付信息页。
        /// </summary>
        internal bool SkipPaymentInformationScreen { get; set; }

        /// <summary>
        /// 获取或设置必需 Store 配置是否齐备。
        /// </summary>
        internal bool ConfigReady { get; set; }

        /// <summary>
        /// 获取 Hub 是否已经完成服务绑定。
        /// </summary>
        internal bool IsBound => m_IsBound;

        /// <summary>
        /// 获取 Store 运行期后台任务取消令牌。
        /// </summary>
        internal CancellationToken RuntimeTaskToken => m_IsRuntimeTaskCtsDisposed ? new CancellationToken(true) : m_RuntimeTaskCts.Token;

        /// <summary>
        /// 创建服务容器并立即建立初始化前可用的共享状态。
        /// </summary>
        /// <param name="store">所属第三方支付 Store。</param>
        internal ThirdPayServiceHub(ThirdPayStore store)
        {
            Store = store ?? throw new ArgumentNullException(nameof(store));
        }

        /// <summary>
        /// 一次性绑定运行依赖并按依赖顺序创建全部内部服务。
        /// </summary>
        /// <param name="context">IAP Store 运行上下文。</param>
        /// <param name="config">第三方支付 Store 配置。</param>
        /// <param name="table">支付商品表。</param>
        internal void Bind(IIAPStoreContext context, ThirdPayStoreConfig config, IIAPProductTable table)
        {
            if (m_IsBound)
            {
                throw new InvalidOperationException("ThirdPayServiceHub 只能绑定一次。");
            }

            // 先固定 Store 运行期共享依赖，再创建只读取 Hub 的内部服务。
            m_IsBound = true;
            Context = context;
            Config = config;
            Table = table;
            NetService = new ThirdIapNetService();
            PaymentConfigService = new ThirdPayPaymentConfigService(NetService.GetPaymentConfigAsync);
            WebViewService = new ThirdPayWebViewService();
            ExternalBrowserService = new ThirdPayExternalBrowserService();
            CountryResolver = new ThirdPayCountryResolver(this);
            OrderValidationService = new ThirdPayOrderValidationService(this);
            OrderRecoveryService = new ThirdPayOrderRecoveryService(this);
            CheckoutCoordinator = new ThirdPayCheckoutCoordinator(this);
        }

        /// <summary>
        /// 通过统一入口启动后台任务，并自动接入 Store 运行期取消令牌。
        /// </summary>
        /// <param name="taskFactory">接收运行期取消令牌并返回后台任务的工厂。</param>
        /// <param name="taskName">后台任务名称。</param>
        internal void RunBackgroundTask(Func<CancellationToken, UniTask> taskFactory, string taskName)
        {
            if (taskFactory == null)
            {
                LogWarning($"后台任务启动失败，任务名={taskName}，原因=任务工厂为空。");
                return;
            }

            if (m_IsRuntimeTaskCtsDisposed || m_RuntimeTaskCts.IsCancellationRequested)
            {
                LogDebug($"后台任务已跳过，第三方支付 Store 正在释放或已释放，任务名={taskName}。");
                return;
            }

            RunBackgroundTaskAsync(taskFactory, taskName, RuntimeTaskToken).Forget();
        }

        /// <summary>
        /// 执行后台任务并统一处理取消和异常。
        /// </summary>
        /// <param name="taskFactory">后台任务工厂。</param>
        /// <param name="taskName">后台任务名称。</param>
        /// <param name="ct">Store 运行期取消令牌。</param>
        private async UniTaskVoid RunBackgroundTaskAsync(Func<CancellationToken, UniTask> taskFactory, string taskName, CancellationToken ct)
        {
            try
            {
                await taskFactory(ct);
            }
            catch (OperationCanceledException)
            {
                LogDebug($"后台任务已取消，任务名={taskName}。");
            }
            catch (Exception ex)
            {
                LogWarning($"后台任务执行异常，任务名={taskName}，详情={ex.Message}");
            }
        }

        /// <summary>
        /// 取消全部经 Hub 启动的 Store 运行期后台任务。
        /// </summary>
        internal void CancelRuntimeTasks()
        {
            if (m_IsRuntimeTaskCtsDisposed || m_RuntimeTaskCts.IsCancellationRequested)
            {
                return;
            }

            m_RuntimeTaskCts.Cancel();
        }

        /// <summary>
        /// 按依赖逆序释放服务、共享状态与后台任务取消源；该方法可重复调用。
        /// </summary>
        internal void Dispose()
        {
            if (m_IsRuntimeTaskCtsDisposed)
            {
                return;
            }

            // 先阻止后台任务继续访问即将释放的服务，再清理活跃支付会话。
            CancelRuntimeTasks();
            Store.CloseAndClearExternalBrowserPaySession(ThirdPayCloseReason.StoreDisposed, ThirdPayPaymentFailureReason.StoreDisposed);
            GooglePolicy?.Dispose();
            GooglePolicy = null;
            WebViewService?.Dispose();
            WebViewService = null;
            ExternalBrowserService?.Dispose();
            ExternalBrowserService = null;
            CheckoutCoordinator = null;
            OrderRecoveryService = null;
            OrderValidationService = null;
            CountryResolver = null;
            PersistContext.Clear();
            PaymentConfigService?.Reset();
            PaymentConfigService = null;
            CountryState.ClearAll();
            NetService = null;
            SkipPaymentInformationScreen = false;
            ConfigReady = false;
            Config = null;
            Context = null;
            Table = null;
            m_RuntimeTaskCts.Dispose();
            m_IsRuntimeTaskCtsDisposed = true;
        }
    }
}
