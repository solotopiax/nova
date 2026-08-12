/***************************************************************
 * (c) copyright 2026 - 2030, Solotopia
 * All Rights Reserved.
 * -------------------------------------------------------------
 * filename:  ThirdPayStore.cs
 * author:    yingzheng
 * created:   2026/5/20
 * descrip:   应用内第三方支付 Store 公开入口
 ***************************************************************/

using System;
using System.Threading;
using Cysharp.Threading.Tasks;
using NovaFramework.SDK.IAP.Runtime;

namespace NovaFramework.SDK.IAP.ThirdPay.Runtime
{
    /// <summary>
    /// 应用内第三方支付 Store。
    /// 仅支持 InAppAuto，由 Store 负责支付页、政策校验、验单、补单与订单持久化。
    /// </summary>
    [IAPStore]
    public sealed partial class ThirdPayStore : IAPStoreBase, IIAPThirdPayCapable
    {
        /// <summary>
        /// 获取当前 Store 类型。
        /// </summary>
        public override IAPStoreType StoreType => IAPStoreType.ThirdPay;

        /// <summary>
        /// 初始化第三方支付 Store 及其内部服务。
        /// </summary>
        /// <param name="table">支付商品表。</param>
        /// <param name="config">第三方支付 Store 配置。</param>
        /// <param name="ctx">支付 Store 运行上下文。</param>
        /// <param name="ct">取消令牌。</param>
        /// <returns>初始化完成的异步任务。</returns>
        public override async UniTask InitializeAsync(IIAPProductTable table, IIAPStoreConfig config, IIAPStoreContext ctx, CancellationToken ct)
        {
            await base.InitializeAsync(table, config, ctx, ct);
            m_Config = config as ThirdPayStoreConfig;
            m_NetService = new ThirdIapNetService();
            m_ChannelParamsLoader = new ThirdPayChannelParamsLoader(m_NetService.GetPayChannelParamsAsync);
            m_WebViewService?.Dispose();
            m_WebViewService = new ThirdPayWebViewService();
            m_CountryCode = m_Config?.CountryCode ?? string.Empty;
            ResetRepository((ThirdPayPersistData)CreateEmptyPersistData());

#if UNITY_ANDROID && !UNITY_EDITOR
            if (m_GooglePolicy == null)
            {
                double googleTimeout = m_Config?.GoogleApiTimeoutSeconds ?? 15d;
                m_GooglePolicy = new ThirdPayGooglePolicyService(new ThirdPayGoogleExternalBillingClient(googleTimeout));
            }
#endif

            if (m_Config == null)
            {
                LogError("ThirdPayStoreConfig 缺失或类型不正确。");
            }

            m_ConfigReady = ValidateConfig();
            if (!EnsurePayEnvironment())
            {
                LogWarning("第三方支付环境（AES 配置或支付页基址）在初始化时尚未就绪，将在首次支付时重试解析。");
            }
        }

        /// <summary>
        /// 判断当前 Store 是否支持指定支付请求。
        /// </summary>
        /// <param name="request">待判断的支付请求。</param>
        /// <returns>请求为第三方支付请求时返回 true。</returns>
        public override bool CanHandle(IAPRequest request)
        {
            return request is IAPThirdPayRequest;
        }

        /// <summary>
        /// 发起一次应用内第三方支付。
        /// </summary>
        /// <param name="request">第三方支付请求。</param>
        /// <param name="ct">取消令牌。</param>
        /// <returns>支付及服务端验单结果。</returns>
        public override UniTask<IAPResult> PayAsync(IAPRequest request, CancellationToken ct)
        {
            ct.ThrowIfCancellationRequested();

#if UNITY_EDITOR
            if (Context?.EnableAlwaysPaySucceed == true)
            {
                var mock = new IAPResult(request.TableId, "MOCK_ORDER_THIRDPAY", false, true, request.CustomData, request.ReceiptParam);
                Context.EventBridge?.RaisePaySuccess(mock);
                return UniTask.FromResult(mock);
            }
#endif

            return PayGuardAsync(request, ct, async () =>
            {
                m_InPayTableId = request.TableId;
                try
                {
                    return await ExecutePayAsync((IAPThirdPayRequest)request, ct);
                }
                finally
                {
                    m_InPayTableId = 0;
                }
            });
        }

        /// <summary>
        /// 同步当前用户；账号变化时加载独立存档并预取商品，每次调用后都立即确保渠道参数已获取。
        /// </summary>
        /// <param name="uid">当前用户 UID。</param>
        public override void SetUserId(string uid)
        {
            string previous = m_GameUID;
            base.SetUserId(uid);
            if (!string.Equals(previous, m_GameUID, StringComparison.Ordinal))
            {
                ResetRepository(LoadPersistData<ThirdPayPersistData>());
                PrefetchProductListAsync(CancellationToken.None).Forget();
            }

            EnsureChannelParamsAsync(CancellationToken.None).Forget();
        }

        /// <summary>
        /// 合并本地订单与服务端待补发订单，并执行一次批量验单。
        /// </summary>
        /// <param name="ct">取消令牌。</param>
        /// <returns>补单检查完成的异步任务。</returns>
        public override async UniTask CheckLocalOrdersAsync(CancellationToken ct)
        {
            if (!IsEnabled || !IsInitialized || string.IsNullOrEmpty(m_GameUID))
            {
                return;
            }

            await RecoverOrdersAsync(ct);
        }

        /// <summary>
        /// 覆盖当前支付国家或地区代码。
        /// </summary>
        /// <param name="countryCode">ISO 3166-1 alpha-2 国家或地区代码。</param>
        public void SetCountryCode(string countryCode)
        {
            string normalized = countryCode ?? string.Empty;
            if (string.Equals(m_CountryCode, normalized, StringComparison.Ordinal))
            {
                return;
            }

            m_CountryCode = normalized;
            m_ProductList = null;
            if (!string.IsNullOrEmpty(m_GameUID))
            {
                PrefetchProductListAsync(CancellationToken.None).Forget();
            }
        }

        /// <summary>
        /// 手动设置当前账号的第三方支付渠道参数。
        /// 非空值会阻止支付前再次向服务端拉取。
        /// </summary>
        /// <param name="channelParams">支付页需要透传的 CID 等渠道参数。</param>
        public void SetChannelParams(string channelParams)
        {
            if (m_PersistData == null)
            {
                return;
            }

            m_PersistData.ChannelParams = channelParams ?? string.Empty;
            SavePersistData(m_PersistData);
        }

        /// <summary>
        /// 拉取当前国家或地区可用的第三方支付商品。
        /// </summary>
        /// <param name="ct">取消令牌。</param>
        /// <returns>成功取得有效商品列表时返回 true。</returns>
        public UniTask<bool> FetchProductListAsync(CancellationToken ct)
        {
            return FetchProductListInternalAsync(ct);
        }

        /// <summary>
        /// 按支付表行 ID 获取已拉取的第三方商品信息。
        /// </summary>
        /// <param name="tableId">支付商品表行 ID。</param>
        /// <returns>匹配的第三方商品；未拉取或未配置时返回 null。</returns>
        public PbNetThirdProductInfo GetProductInfo(long tableId)
        {
            return FindProductInfo(tableId);
        }

        /// <summary>
        /// 释放 Google 政策服务和当前账号运行状态。
        /// </summary>
        /// <param name="ct">取消令牌。</param>
        /// <returns>释放完成的异步任务。</returns>
        public override async UniTask DisposeAsync(CancellationToken ct)
        {
            m_GooglePolicy?.Dispose();
            m_GooglePolicy = null;
            m_WebViewService?.Dispose();
            m_WebViewService = null;
            m_OrderRepository = null;
            m_PersistData = null;
            m_ProductList = null;
            m_ChannelParamsLoader = null;
            m_NetService = null;
            m_Config = null;
            m_ConfigReady = false;
            m_AesKey = null;
            m_AesIv = null;
            m_PayUrlBase = null;
            await base.DisposeAsync(ct);
        }
    }
}
