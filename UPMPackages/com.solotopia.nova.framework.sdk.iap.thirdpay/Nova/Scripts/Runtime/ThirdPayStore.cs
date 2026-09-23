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
using System.Collections.Generic;
using System.Threading;
using Cysharp.Threading.Tasks;
using NovaFramework.SDK.IAP.Runtime;

namespace NovaFramework.SDK.IAP.ThirdPay.Runtime
{
    /// <summary>
    /// 应用内第三方支付 Store。
    /// 仅支持 InAppAuto，由 Store 负责支付页、政策校验、验单、补单与订单持久化。
    /// </summary>
    [IAPStore(IAPStoreType.ThirdPay)]
    public sealed partial class ThirdPayStore : IAPStoreBase, IIAPThirdPayConfigCapable, IIAPThirdPayProductCapable, IIAPThirdPayCheckoutCapable, IIAPStorePauseListener, IIAPStoreFocusListener
    {
        /// <summary>
        /// 创建第三方支付 Store，并提前建立初始化前可用的共享状态容器。
        /// </summary>
        public ThirdPayStore()
        {
            m_Hub = new ThirdPayServiceHub(this);
        }

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
            // 初始化 Store 基类持有的商品表、运行上下文和公共运行状态。
            await base.InitializeAsync(table, config, ctx, ct);
            // 由 Hub 一次性绑定共享依赖并按顺序创建全部内部服务。
            m_Hub.Bind(ctx, config as ThirdPayStoreConfig, table);
            // 清空各平台上一次解析得到的国家码。
            ClearResolvedCountryCodes();
            // 清空支付配置快照及其请求状态。
            m_Hub.PaymentConfigService.Reset();
            // 恢复是否跳过 Google 第三方支付信息页的配置值。
            m_Hub.SkipPaymentInformationScreen = m_Hub.Config?.SkipPaymentInformationScreen ?? false;
            // 创建空订单仓储，等待设置用户 ID 后切换到对应账号数据。
            ResetRepository((ThirdPayPersistData)CreateEmptyPersistData());

            // 配置缺失或类型错误时记录初始化错误。
            if (m_Hub.Config == null)
            {
                LogError("ThirdPayStoreConfig 缺失或类型不正确。");
            }

            // 校验必需配置并缓存 Store 是否可用。
            m_Hub.ConfigReady = ValidateConfig();
            // 并行启动三种国家码查询，不等待结果，也不阻塞 Store 初始化。
            ResolveCountryCodesInBackground();
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
        public override async UniTask<IAPResult> PayAsync(IAPRequest request, CancellationToken ct)
        {
            ct.ThrowIfCancellationRequested();

#if UNITY_EDITOR
            if (Context?.EnableAlwaysPaySucceed == true)
            {
                var mock = new IAPResult(request.TableId, "MOCK_ORDER_THIRDPAY", false, true, request.CustomData, request.ReceiptParam, StoreType);
                Context.EventBridge?.RaisePaySuccess(mock);
                return mock;
            }
#endif

            Func<UniTask<IAPResult>> executeAsync = async () =>
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
            };
            IAPResult result = await PayGuardAsync(request, ct, executeAsync);
            TrackReturnedPayFailureInternal(result);
            return result;
        }

        /// <summary>
        /// 同步当前用户；账号变化时加载独立存档，并复用 Store 初始化阶段取得的国家码预取支付配置。
        /// </summary>
        /// <param name="uid">当前用户 UID。</param>
        public override void SetUserId(string uid)
        {
            string previous = m_GameUID;
            base.SetUserId(uid);
            if (!string.Equals(previous, m_GameUID, StringComparison.Ordinal))
            {
                CloseAndClearExternalBrowserPaySession(ThirdPayCloseReason.SessionReplaced, ThirdPayPaymentFailureReason.SessionReplaced);
                // 平台国家码和首次业务读取形成的锁定值均在 Store 生命周期内复用，账号切换只切换用户态数据。
                ResetRepository(LoadPersistData<ThirdPayPersistData>());
                // 未启用的 Store 不创建运行服务，账号同步只更新初始化前共享状态。
                if (!m_Hub.IsBound)
                {
                    return;
                }

                m_Hub.PaymentConfigService?.Invalidate();
                TrySchedulePaymentConfigPrefetch("登录后支付配置预取");
            }
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
        /// 设置 Debug 覆盖用支付国家或地区代码；生产环境通常留空，由 Store 自动解析。
        /// </summary>
        /// <param name="countryCode">ISO 3166-1 alpha-2 国家或地区代码；空值表示取消 Debug 覆盖。</param>
        public void SetDebugCountryCode(string countryCode)
        {
            if (m_Hub.CountryResolver != null)
            {
                m_Hub.CountryResolver.SetDebugCountryCode(countryCode);
                return;
            }

            m_Hub.CountryState.SetDebugCountryCode(countryCode);
        }

        /// <summary>
        /// 获取 ThirdPay 当前使用的国家码；登录后首次读取会锁定自动解析结果，避免后续异步来源改变业务国家。
        /// </summary>
        /// <returns>规范化后的 ISO 3166-1 alpha-2 国家或地区代码。</returns>
        public string GetCountryCode()
        {
            string countryCode = m_Hub.CountryResolver?.Resolve() ?? m_Hub.CountryState.Resolve();
            if (string.IsNullOrEmpty(countryCode) || string.IsNullOrEmpty(m_GameUID) || m_Hub.CountryState.HasDebugCountryCode || !string.IsNullOrEmpty(m_Hub.CountryState.LockCountryCode))
            {
                return countryCode;
            }

            SetLockCountryCode(countryCode);
            return m_Hub.CountryResolver?.Resolve() ?? m_Hub.CountryState.Resolve();
        }

        /// <summary>
        /// 设置是否跳过 Google 第三方支付信息页；默认值来自 ThirdPayStoreConfig。
        /// </summary>
        /// <param name="skip">是否跳过信息页。</param>
        public void SetSkipPaymentInformationScreen(bool skip)
        {
            m_Hub.SkipPaymentInformationScreen = skip;
        }

        /// <summary>
        /// 获取当前是否跳过 Google 第三方支付信息页。
        /// </summary>
        public bool IsPaymentInformationScreenSkipped => m_Hub.SkipPaymentInformationScreen;

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
        /// 获取最近一次成功拉取的全部第三方支付商品。
        /// </summary>
        /// <returns>商品列表；尚未拉取成功或列表为空时返回空列表。</returns>
        public IReadOnlyList<PbNetThirdProductInfo> GetProductList()
        {
            return m_Hub.PaymentConfigService?.Snapshot?.Products ?? Array.Empty<PbNetThirdProductInfo>();
        }

        /// <summary>
        /// 判断最近一次成功拉取的第三方支付商品列表是否有商品。
        /// </summary>
        /// <returns>有商品时返回 true。</returns>
        public bool HasProducts()
        {
            return GetProductList().Count > 0;
        }

        /// <summary>
        /// 获取统一支付配置是否已经成功加载且仍在有效期内。
        /// </summary>
        public bool IsPaymentConfigReady => m_Hub.PaymentConfigService?.Snapshot != null;

        /// <summary>
        /// 获取服务端是否允许当前用户在当前国家发起新的第三方支付。
        /// </summary>
        public bool IsPaymentAvailable => m_Hub.PaymentConfigService?.Snapshot?.Enabled == true;

        /// <summary>
        /// 获取服务端返回的第三方支付关闭原因码；配置未就绪时返回 0。
        /// </summary>
        public int PaymentDisabledReason => m_Hub.PaymentConfigService?.Snapshot?.DisabledReason ?? 0;

        /// <summary>
        /// 设置第三方支付 WebView 导航栏左上角的显示名称。
        /// </summary>
        /// <param name="titleText">标题文本；空值时恢复应用名称。</param>
        public void SetThirdPayWebViewTitleText(string titleText)
        {
            m_Hub.WebViewService?.SetTitleText(titleText);
        }

        /// <summary>
        /// 设置第三方支付 WebView 导航栏右上角的关闭文本。
        /// </summary>
        /// <param name="closeText">关闭文本；空值时恢复为 close。</param>
        public void SetThirdPayWebViewCloseText(string closeText)
        {
            m_Hub.WebViewService?.SetCloseText(closeText);
        }

        /// <summary>
        /// 释放 Google 政策服务和当前账号运行状态。
        /// </summary>
        /// <param name="ct">取消令牌。</param>
        /// <returns>释放完成的异步任务。</returns>
        public override async UniTask DisposeAsync(CancellationToken ct)
        {
            m_Hub.Dispose();
            await base.DisposeAsync(ct);
        }
    }
}
