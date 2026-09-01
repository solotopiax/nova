/***************************************************************
 * (c) copyright 2026 - 2030, Solotopia
 * All Rights Reserved.
 * -------------------------------------------------------------
 * filename:  ThirdPayStore.Methods.cs
 * author:    yingzheng
 * created:   2026/5/20
 * descrip:   ThirdPayStore 非公开业务方法
 ***************************************************************/

using System;
using System.Collections.Generic;
using System.Text;
using System.Threading;
using Cysharp.Threading.Tasks;
using NovaFramework.Runtime;
using NovaFramework.SDK.IAP.Runtime;

namespace NovaFramework.SDK.IAP.ThirdPay.Runtime
{
    public sealed partial class ThirdPayStore
    {
        private enum ThirdPayValidationScene
        {
            DirectPay,
            Recovered,
            ExternalBrowserReturn,
        }

        /// <summary>
        /// 注入测试或平台适配使用的 Google 外部结算客户端。
        /// </summary>
        /// <param name="client">Google 外部结算客户端；传入 null 时关闭政策流程。</param>
        internal void SetGoogleExternalBillingClient(IThirdPayGoogleExternalBillingClient client)
        {
            m_GooglePolicy?.Dispose();
            m_GooglePolicy = client == null ? null : new ThirdPayGooglePolicyService(client);
        }

        /// <summary>
        /// 校验第三方支付必需的 Store 配置项是否齐备；缺失时记录错误并判定 Store 未就绪。
        /// 仅校验初始化时刻即可确定的纯配置项，环境依赖（AES/支付页基址）由 EnsurePayEnvironment 兜底。
        /// </summary>
        /// <returns>拉取商品列表与验单协议名均已配置时返回 true。</returns>
        private bool ValidateConfig()
        {
            if (m_Config == null)
            {
                return false;
            }

            bool ready = true;
            if (string.IsNullOrEmpty(m_Config.GetProductListCmdName))
            {
                LogError("ThirdPayStoreConfig.GetProductListCmdName 未配置，无法拉取第三方商品列表。");
                ready = false;
            }

            if (string.IsNullOrEmpty(m_Config.VerifyIapCmdName))
            {
                LogError("ThirdPayStoreConfig.VerifyIapCmdName 未配置，无法验单。");
                ready = false;
            }

            if (string.IsNullOrEmpty(m_Config.PayChannelParamsCmdName))
            {
                LogWarning("ThirdPayStoreConfig.PayChannelParamsCmdName 未配置，将无法拉取渠道参数。");
            }

            if (string.IsNullOrEmpty(m_Config.QueryPendingOrderCmdName))
            {
                LogWarning("ThirdPayStoreConfig.QueryPendingOrderCmdName 未配置，将无法查询服务端未校验订单。");
            }

            return ready;
        }

        /// <summary>
        /// 解析并缓存支付 URL 构造所需的 AES 配置与支付页基址；已缓存时直接复用。
        /// 初始化时可能因依赖子系统尚未就绪而失败，此时保持未缓存，待首次支付时重试解析。
        /// </summary>
        /// <returns>AES 密钥、向量与支付页基址均已就绪时返回 true。</returns>
        private bool EnsurePayEnvironment()
        {
            if (IsValidAppAesSecret(m_AesKey) && IsValidAppAesSecret(m_AesIv) && !string.IsNullOrEmpty(m_PayUrlBase))
            {
                return true;
            }

            IConfigManager configManager = FrameworkManagersGroup.GetManager<IConfigManager>();
            if (configManager == null || !configManager.IsLoadOver)
            {
                LogError("ThirdPayStore 应用业务 AES 配置未就绪：请先完成 await Nova.Config.LoadAsync()；然后在 Nova/Open Config → 通用配置 → 应用配置中，为当前 Platform × Channel × DevelopMode 配置 AppAesKey / AppAesIV（UTF-8 各 16 字节），保存后重新导出 ConfigRuntimeSO。");
                return false;
            }

            string aesKey = configManager?.AppConfigs?.AppAesKey;
            string aesIv = configManager?.AppConfigs?.AppAesIV;
            INetworkCmdRow openUrlCmd = Nova.Network?.ResolveNetCmdRow(c_OpenUrlCmdName);
            string payUrlBase = Nova.Network?.ResolveNetCmdUrl(openUrlCmd);
            if (!IsValidAppAesSecret(aesKey) || !IsValidAppAesSecret(aesIv))
            {
                LogError("ThirdPayStore 应用业务 AES 配置无效：请在 Nova/Open Config → 通用配置 → 应用配置中，为当前 Platform × Channel × DevelopMode 配置 AppAesKey / AppAesIV（UTF-8 各 16 字节），保存后重新导出 ConfigRuntimeSO。");
                return false;
            }
            if (string.IsNullOrEmpty(payUrlBase))
            {
                return false;
            }

            m_AesKey = aesKey;
            m_AesIv = aesIv;
            m_PayUrlBase = payUrlBase;
            return true;
        }

        /// <summary>
        /// 校验应用协议 AES 单个凭据是否为可传入支付 URL 加密器的 UTF-8 16 字节字符串。
        /// </summary>
        /// <param name="value">待校验的 AppConfigs AES Key 或 IV。</param>
        /// <returns>非空且 UTF-8 编码长度为 16 字节时返回 true。</returns>
        private static bool IsValidAppAesSecret(string value)
        {
            return !string.IsNullOrEmpty(value) && Encoding.UTF8.GetByteCount(value) == 16;
        }

        /// <summary>
        /// 创建已初始化的第三方支付空存档。
        /// </summary>
        /// <returns>可直接读写的第三方支付存档。</returns>
        protected override IIAPStorePersistData CreateEmptyPersistData()
        {
            var data = new ThirdPayPersistData();
            data.EnsureInitialized();
            return data;
        }

        /// <summary>
        /// 记录 Google Play Billing 返回的商店国家码。
        /// </summary>
        /// <param name="countryCode">Billing 原始国家或地区代码。</param>
        private void SetBillingCountryCode(string countryCode)
        {
            m_BillingCountryCode = NormalizeCountryCode(countryCode);
            if (!string.IsNullOrEmpty(m_BillingCountryCode))
            {
                LogDebug($"ThirdPay BillingCountryCode={m_BillingCountryCode}");
            }
        }

        /// <summary>
        /// 记录商品快照使用的国家码，避免支付 URL 与已拉取商品国家漂移。
        /// </summary>
        /// <param name="countryCode">商品列表请求使用的国家或地区代码。</param>
        private void SetLockCountryCode(string countryCode)
        {
            m_LockCountryCode = NormalizeCountryCode(countryCode);
        }

        /// <summary>
        /// 记录 iOS StoreKit storefront 返回的商店国家码。
        /// </summary>
        /// <param name="countryCode">原生层返回的国家或地区代码。</param>
        /// <param name="identifier">原生层返回的商店区域标识。</param>
        private void SetNativeCountryCode(string countryCode, string identifier)
        {
            m_NativeCountryCode = NormalizeCountryCode(countryCode);
            m_NativeStorefrontIdentifier = string.IsNullOrWhiteSpace(identifier) ? string.Empty : identifier.Trim();
            if (!string.IsNullOrEmpty(m_NativeCountryCode))
            {
                LogDebug($"ThirdPay NativeCountryCode={m_NativeCountryCode}, StorefrontIdentifier={m_NativeStorefrontIdentifier}");
            }
        }

        /// <summary>
        /// 记录广告模块返回或缓存的国家码。
        /// </summary>
        /// <param name="countryCode">广告模块返回的国家或地区代码。</param>
        private void SetAdCountryCode(string countryCode)
        {
            m_AdCountryCode = NormalizeCountryCode(countryCode);
            if (!string.IsNullOrEmpty(m_AdCountryCode))
            {
                LogDebug($"ThirdPay AdCountryCode={m_AdCountryCode}");
            }
        }

        /// <summary>
        /// 初始化时触发平台原生商店国家码获取，目前仅 iOS 有有效实现。
        /// </summary>
        private void ResolveNativeCountryCode()
        {
            try
            {
                ThirdPayStorefrontRegionNativeBridge.Request(SetNativeCountryCode);
            }
            catch (Exception ex)
            {
                LogWarning($"读取原生商店国家码失败，将继续使用后续兜底国家码：{ex.Message}");
            }
        }

        /// <summary>
        /// 按 Nova AD 模块异步读取广告国家码缓存。
        /// </summary>
        /// <param name="ct">取消令牌。</param>
        private async UniTask ResolveAdCountryCodeAsync(CancellationToken ct)
        {
            try
            {
                if (Nova.SDK == null || !Nova.SDK.TryGet<IAdPlugin>(out IAdPlugin adPlugin))
                {
                    return;
                }

                SetAdCountryCode(await adPlugin.GetCountryCodeAsync(ct));
            }
            catch (OperationCanceledException)
            {
                throw;
            }
            catch (Exception ex)
            {
                LogWarning($"读取广告国家码失败，将继续使用后续兜底国家码：{ex.Message}");
            }
        }

        /// <summary>
        /// 拉取第三方支付商品列表，每次业务调用独立请求，并在网络失败时最多尝试三次。
        /// 仅允许请求上下文仍与当前 Store 一致的响应覆盖商品快照。
        /// </summary>
        /// <param name="ct">取消令牌。</param>
        /// <returns>成功取得有效响应时返回 true。</returns>
        private async UniTask<bool> FetchProductListInternalAsync(CancellationToken ct)
        {
            if (m_NetService == null || string.IsNullOrEmpty(m_GameUID)
                || string.IsNullOrEmpty(m_Config?.GetProductListCmdName))
            {
                return false;
            }

            string requestUid = m_GameUID;
            string requestCmdName = m_Config.GetProductListCmdName;
            string requestCountryCode = GetCountryCode();
            int requestVersion = ++m_ProductListRequestVersion;
            const int maxAttempts = 3;
            for (int attempt = 0; attempt < maxAttempts; attempt++)
            {
                ct.ThrowIfCancellationRequested();
                NetResponse<PbNetThirdProductListResp> response = await m_NetService.GetProductListAsync(requestCmdName, requestCountryCode);
                if (response.IsSuccess && response.Data != null)
                {
                    if (requestVersion != m_ProductListRequestVersion
                        || !string.Equals(requestUid, m_GameUID, StringComparison.Ordinal)
                        || !string.Equals(requestCmdName, m_Config?.GetProductListCmdName, StringComparison.Ordinal)
                        || !string.Equals(requestCountryCode, GetCountryCode(), StringComparison.OrdinalIgnoreCase))
                    {
                        return false;
                    }

                    m_ProductList = response.Data;
                    if (string.IsNullOrEmpty(m_DebugCountryCode))
                    {
                        SetLockCountryCode(requestCountryCode);
                    }

                    return true;
                }
            }

            return false;
        }

        /// <summary>
        /// 确保当前已持有可用的第三方商品列表；缺失时同步补拉一次。
        /// </summary>
        /// <param name="ct">取消令牌。</param>
        /// <returns>已持有或成功补拉到有效商品列表时返回 true。</returns>
        private async UniTask<bool> EnsureProductListAsync(CancellationToken ct)
        {
            if (m_ProductList?.ProductList != null && m_ProductList.ProductList.Count > 0)
            {
                return true;
            }

            return await FetchProductListInternalAsync(ct);
        }

        /// <summary>
        /// 在账号切换后预取第三方商品；失败只记录日志，不影响登录流程。
        /// </summary>
        /// <param name="ct">取消令牌。</param>
        /// <returns>预取结束的异步任务。</returns>
        private async UniTask PrefetchProductListAsync(CancellationToken ct)
        {
            try
            {
                bool succeeded = await FetchProductListInternalAsync(ct);
                if (!succeeded)
                {
                    LogWarning("登录后预取第三方支付商品列表失败。");
                }
            }
            catch (OperationCanceledException)
            {
                throw;
            }
            catch (Exception ex)
            {
                LogWarning($"登录后预取第三方支付商品列表异常：{ex.Message}");
            }
        }

        /// <summary>
        /// 按支付表行 ID 查找已拉取的第三方商品。
        /// </summary>
        /// <param name="tableId">支付商品表行 ID。</param>
        /// <returns>匹配的第三方商品；未命中时返回 null。</returns>
        private PbNetThirdProductInfo FindProductInfo(long tableId)
        {
            IAPProductEntry entry = Table?.FindByTableId(tableId);
            string thirdProductId = entry?.ThirdProductID;
            if (string.IsNullOrEmpty(thirdProductId) || m_ProductList?.ProductList == null)
            {
                return null;
            }

            foreach (PbNetThirdProductInfo product in m_ProductList.ProductList)
            {
                if (string.Equals(product.ProductId, thirdProductId, StringComparison.Ordinal))
                {
                    return product;
                }
            }

            return null;
        }

        /// <summary>
        /// 判断当前国家或地区是否需要使用外部浏览器支付。
        /// </summary>
        private bool ShouldUseExternalBrowserPayment()
        {
            string currentCountry = GetCountryCode();
            IReadOnlyList<string> browserCountries = m_Config?.ExternalBrowserCountryCodes;
            if (string.IsNullOrEmpty(currentCountry) || browserCountries == null || browserCountries.Count == 0)
            {
                return false;
            }

            for (int i = 0; i < browserCountries.Count; i++)
            {
                if (string.Equals(currentCountry, NormalizeCountryCode(browserCountries[i]), StringComparison.Ordinal))
                {
                    return true;
                }
            }

            return false;
        }

        /// <summary>
        /// 归一化 ISO 国家或地区代码，供支付路径匹配使用。
        /// </summary>
        private static string NormalizeCountryCode(string countryCode)
        {
            if (string.IsNullOrWhiteSpace(countryCode))
            {
                return string.Empty;
            }

            string normalized = countryCode.Trim().ToUpperInvariant();
            if (string.Equals(normalized, c_UnknownCountryCode, StringComparison.Ordinal))
            {
                return string.Empty;
            }

            return string.Equals(normalized, c_InvalidCountryCode, StringComparison.Ordinal) ? c_DefaultCountryCode : normalized;
        }

        /// <summary>
        /// 执行第三方支付主链：补齐渠道参数、保存本地订单、完成政策流程、打开支付页并验单。
        /// </summary>
        /// <param name="request">第三方支付请求。</param>
        /// <param name="ct">取消令牌。</param>
        /// <returns>支付及服务端验单结果。</returns>
        private async UniTask<IAPResult> ExecutePayAsync(IAPThirdPayRequest request, CancellationToken ct)
        {
            // 用户主动发起支付，标记交互态使后续验单等待期按 UseCommonLoading 显示 Loading。
            m_LoadingGuard.HasUserInteracted = true;

            bool useExternalBrowser = false;
            if (m_WebViewService == null && m_ExternalBrowserService == null)
            {
                return Fail(request, IAPThirdPayErrorCode.StoreInitFailed, "第三方支付页服务尚未初始化。");
            }

            if (string.IsNullOrEmpty(m_GameUID))
            {
                return Fail(request, IAPThirdPayErrorCode.StoreInitFailed, "尚未设置用户 ID。");
            }

            ThirdPayOrderRecord order = null;
            string paymentUrl = string.Empty;
            AddWaitingRef();
            try
            {
                TrackBuyInternal(request);
                bool hasChannelParams = await EnsureChannelParamsAsync(ct);
                if (!hasChannelParams)
                {
                    // 渠道客户号创建失败（例如服务端 10707）不阻断第三方支付；
                    // URL 会省略 payment_customer_ids，继续进入应用内 WebView。
                    LogWarning("第三方支付渠道参数未就绪，将继续使用不含渠道客户号的支付 URL。");
                }

                if (!await EnsureProductListAsync(ct))
                {
                    const string productListReason = "第三方支付商品列表尚未就绪。";
                    TrackCreateOrderFailInternal(request, productListReason);
                    return Fail(request, IAPThirdPayErrorCode.StoreNotAvailable, productListReason);
                }

                useExternalBrowser = ShouldUseExternalBrowserPayment();
                if (!useExternalBrowser && m_WebViewService == null)
                {
                    return Fail(request, IAPThirdPayErrorCode.StoreInitFailed, "第三方应用内 WebView 支付服务尚未初始化。");
                }

                if (useExternalBrowser && m_ExternalBrowserService == null)
                {
                    return Fail(request, IAPThirdPayErrorCode.StoreInitFailed, "第三方外部浏览器支付服务尚未初始化。");
                }

                string clientOrderId = GenerateOrderId();
                order = new ThirdPayOrderRecord { ClientOrderId = clientOrderId, TableId = request.TableId, UserId = m_GameUID, CustomData = request.CustomData ?? string.Empty, ReceiptParam = request.ReceiptParam ?? string.Empty };

                m_OrderRepository.Upsert(order);
                TrackCreateOrderSuccessInternal(order);

                ThirdPayGoogleAuthorization authorization;
                try
                {
                    authorization = await AuthorizeAndBuildUrlAsync(order, useExternalBrowser, ct);
                }
                catch (OperationCanceledException)
                {
                    throw;
                }
                catch (Exception ex)
                {
                    m_OrderRepository.Remove(clientOrderId);
                    TrackCreateOrderFailInternal(request, ex.Message);
                    return Fail(request, IAPThirdPayErrorCode.StoreInitFailed, $"构造支付 URL 失败：{ex.Message}");
                }

                if (authorization.Status != ThirdPayGoogleAuthorizationStatus.Authorized || string.IsNullOrEmpty(authorization.PaymentUrl))
                {
                    (IAPThirdPayErrorCode code, string reason) = MapGoogleAuthorizationFailure(authorization.Status);
                    m_OrderRepository.Remove(clientOrderId);
                    TrackCreateOrderFailInternal(request, reason);
                    return Fail(request, code, reason);
                }

                paymentUrl = authorization.PaymentUrl;
                LogDebug($"第三方支付支付页已构建：OrderId={order.ClientOrderId}，TableId={order.TableId}，ExternalBrowser={useExternalBrowser}");
            }
            finally
            {
                SubWaitingRef();
            }

            if (useExternalBrowser)
            {
                return await OpenExternalBrowserPaymentAsync(request, order, paymentUrl, ct);
            }

            ThirdPayOpenResult openResult;
            try
            {
                openResult = await m_WebViewService.OpenAsync(paymentUrl, ct);
                LogDebug($"第三方支付 WebView 返回：OrderId={order.ClientOrderId}，Result={openResult}");
            }
            catch (OperationCanceledException)
            {
                throw;
            }
            catch (Exception ex)
            {
                LogWarning($"第三方支付 WebView 打开异常：OrderId={order.ClientOrderId}，Error={ex.Message}");
                TrackLocalPayFailInternal(request, IAPThirdPayErrorCode.WebViewClosed, ex.Message);
                return Fail(request, IAPThirdPayErrorCode.WebViewClosed, $"打开支付页异常：{ex.Message}");
            }

            if (openResult == ThirdPayOpenResult.Cancel)
            {
                TrackThirdPayCloseOrderInternal(order);
                LogDebug($"第三方支付 WebView 关闭返回，开始验单：OrderId={order.ClientOrderId}");
            }
            else if (openResult == ThirdPayOpenResult.Failed)
            {
                LogWarning($"第三方支付 WebView failed 返回：OrderId={order.ClientOrderId}");
                TrackLocalPayFailInternal(request, IAPThirdPayErrorCode.WebViewClosed, "支付页返回失败或打开异常。");
                return Fail(request, IAPThirdPayErrorCode.WebViewClosed, "支付页返回失败或打开异常，订单保留等待后续验单。");
            }
            else
            {
                TrackLocalPaySuccessInternal(order, false);
                LogDebug($"第三方支付 WebView 正常 message 返回，开始验单：OrderId={order.ClientOrderId}");
            }

            // 支付页已关闭，验单期间的网络等待才显示 Loading，避免遮挡支付页本身。
            AddWaitingRef();
            try
            {
                return await ValidateOrderAsync(order, ThirdPayValidationScene.DirectPay, ct);
            }
            finally
            {
                SubWaitingRef();
            }
        }

        /// <summary>
        /// 在支付前确保当前账号已取得渠道参数。
        /// 手动注入的非空参数优先；网络响应只允许写回发起请求时的账号存档。
        /// </summary>
        /// <param name="ct">取消令牌。</param>
        /// <returns>当前账号已存在或成功取得渠道参数时返回 true。</returns>
        private async UniTask<bool> EnsureChannelParamsAsync(CancellationToken ct)
        {
            if (!string.IsNullOrEmpty(m_PersistData?.ChannelParams))
            {
                return true;
            }

            if (m_ChannelParamsLoader == null || string.IsNullOrEmpty(m_Config?.PayChannelParamsCmdName))
            {
                return false;
            }

            string requestUid = m_GameUID;
            ThirdPayPersistData requestData = m_PersistData;
            NetResponse<PbNetThirdPayChannelParamsResp> response;
            try
            {
                response = await m_ChannelParamsLoader.LoadAsync(requestUid, m_Config.PayChannelParamsCmdName, ct);
            }
            catch (OperationCanceledException)
            {
                throw;
            }
            catch (Exception ex)
            {
                LogWarning($"查询第三方支付渠道参数失败：{ex.Message}");
                return false;
            }

            if (!response.IsSuccess || response.Data == null)
            {
                return false;
            }

            string channelParams = response.Data.PaymentCustomerIds;
            if (string.IsNullOrEmpty(channelParams))
            {
                return false;
            }

            if (!string.Equals(requestUid, m_GameUID, StringComparison.Ordinal) || !ReferenceEquals(requestData, m_PersistData))
            {
                return false;
            }

            if (!string.IsNullOrEmpty(requestData.ChannelParams))
            {
                return true;
            }

            requestData.ChannelParams = channelParams;
            SavePersistData(requestData);
            return true;
        }

        /// <summary>
        /// 查询服务端支付成功但尚未校验的订单，与本地订单合并后执行一次批量验单。
        /// 服务端查询失败时仍继续处理本地订单。
        /// </summary>
        /// <param name="ct">取消令牌。</param>
        /// <returns>补单检查完成的异步任务。</returns>
        private async UniTask RecoverOrdersAsync(CancellationToken ct)
        {
            IReadOnlyCollection<ThirdPayOrderRecord> localOrders = m_OrderRepository?.GetAll();

            PbNetThirdQueryPendingOrderResp serverOrders = null;
            if (m_NetService != null && !string.IsNullOrEmpty(m_Config?.QueryPendingOrderCmdName))
            {
                try
                {
                    NetResponse<PbNetThirdQueryPendingOrderResp> response = await m_NetService.QueryPendingOrderAsync(m_Config.QueryPendingOrderCmdName);
                    if (response.IsSuccess && response.Data != null)
                    {
                        serverOrders = response.Data;
                    }
                }
                catch (OperationCanceledException)
                {
                    throw;
                }
                catch (Exception ex)
                {
                    LogWarning($"查询第三方支付未校验订单失败：{ex.Message}");
                }
            }

            List<ThirdPayOrderRecord> merged = MergeRecoverableOrders(localOrders, serverOrders);
            foreach (ThirdPayOrderRecord order in merged)
            {
                if (string.IsNullOrEmpty(order.UserId))
                {
                    order.UserId = m_GameUID;
                }
            }

            if (merged.Count > 0)
            {
                // 补单验单是否显示 Loading 由 GameStartShowCommonLoading 决定，启动阶段默认静默。
                AddWaitingRef();
                try
                {
                    await ValidateOrdersAsync(merged, ThirdPayValidationScene.Recovered, ct);
                }
                finally
                {
                    SubWaitingRef();
                }
            }
        }

        /// <summary>
        /// 合并本地订单与服务端未校验订单，并按客户端订单号去重。
        /// 本地记录包含完整支付上下文，因此发生重复时始终保留本地记录。
        /// </summary>
        /// <param name="localOrders">当前账号本地订单快照。</param>
        /// <param name="serverResponse">服务端未校验订单响应。</param>
        /// <returns>本地订单优先且顺序稳定的合并结果。</returns>
        private static List<ThirdPayOrderRecord> MergeRecoverableOrders(IReadOnlyCollection<ThirdPayOrderRecord> localOrders, PbNetThirdQueryPendingOrderResp serverResponse)
        {
            var result = new List<ThirdPayOrderRecord>();
            var clientOrderIds = new HashSet<string>(StringComparer.Ordinal);

            if (localOrders != null)
            {
                foreach (ThirdPayOrderRecord order in localOrders)
                {
                    if (order == null || string.IsNullOrEmpty(order.ClientOrderId) || !clientOrderIds.Add(order.ClientOrderId))
                    {
                        continue;
                    }

                    result.Add(order);
                }
            }

            if (serverResponse?.OrderList == null)
            {
                return result;
            }

            foreach (PbNetThirdQueryPendingOrderInfo serverOrder in serverResponse.OrderList)
            {
                if (serverOrder == null || string.IsNullOrEmpty(serverOrder.ClientOrderId) || !clientOrderIds.Add(serverOrder.ClientOrderId))
                {
                    continue;
                }

                result.Add(new ThirdPayOrderRecord { ClientOrderId = serverOrder.ClientOrderId, TableId = serverOrder.TableId });
            }

            return result;
        }

        /// <summary>
        /// 完成 Android Google 外链政策流程，并构造最终支付 URL。
        /// 非 Android 运行环境直接构造支付 URL 并合成已授权结果。
        /// </summary>
        /// <param name="order">本地订单上下文。</param>
        /// <param name="isExternalBrowser">是否使用外部浏览器支付页。</param>
        /// <param name="ct">取消令牌。</param>
        /// <returns>包含完整授权状态与支付 URL 的授权结果，供调用方按状态映射错误码。</returns>
        private async UniTask<ThirdPayGoogleAuthorization> AuthorizeAndBuildUrlAsync(ThirdPayOrderRecord order, bool isExternalBrowser, CancellationToken ct)
        {
#if UNITY_ANDROID && !UNITY_EDITOR
            if (m_GooglePolicy == null)
            {
                return new ThirdPayGoogleAuthorization(ThirdPayGoogleAuthorizationStatus.ProgramUnavailable);
            }

            return await m_GooglePolicy.AuthorizeAsync(token => BuildPaymentUrl(order, token, isExternalBrowser), m_SkipPaymentInformationScreen, ct);
#else
            await UniTask.CompletedTask;
            return new ThirdPayGoogleAuthorization(ThirdPayGoogleAuthorizationStatus.Authorized, string.Empty, BuildPaymentUrl(order, string.Empty, isExternalBrowser));
#endif
        }

        /// <summary>
        /// 将 Google 外链授权失败状态映射为第三方支付错误码与失败原因。
        /// </summary>
        /// <param name="status">Google 外链授权状态。</param>
        /// <returns>对应的第三方支付错误码与失败原因。</returns>
        private static (IAPThirdPayErrorCode Code, string Reason) MapGoogleAuthorizationFailure(ThirdPayGoogleAuthorizationStatus status)
        {
            switch (status)
            {
                case ThirdPayGoogleAuthorizationStatus.ProgramUnavailable:
                    return (IAPThirdPayErrorCode.StoreNotAvailable, "当前设备或地区不支持 Google 外链结算计划。");
                case ThirdPayGoogleAuthorizationStatus.ConnectionFailed:
                    return (IAPThirdPayErrorCode.BillingNotReady, "无法连接 Google 外链结算服务。");
                case ThirdPayGoogleAuthorizationStatus.TokenCreationFailed:
                    return (IAPThirdPayErrorCode.BillingNotReady, "创建 Google 外链上报 token 失败。");
                case ThirdPayGoogleAuthorizationStatus.UrlBuildFailed:
                    return (IAPThirdPayErrorCode.StoreInitFailed, "构造第三方支付 URL 失败。");
                case ThirdPayGoogleAuthorizationStatus.LaunchFailed:
                    return (IAPThirdPayErrorCode.BillingNotReady, "打开 Google 外链信息页失败。");
                case ThirdPayGoogleAuthorizationStatus.UserCancelled:
                    return (IAPThirdPayErrorCode.UserCancelled, "用户在 Google 外链信息页取消支付。");
                default:
                    return (IAPThirdPayErrorCode.BillingNotReady, "Google 外链政策校验未通过。");
            }
        }

        /// <summary>
        /// 使用应用 AES 配置加密固定字段并构造第三方支付 URL。
        /// </summary>
        /// <param name="order">本地订单上下文。</param>
        /// <param name="googleToken">Google 外部结算上报 token。</param>
        /// <param name="isExternalBrowser">是否使用外部浏览器支付页。</param>
        /// <returns>加密后的第三方支付 URL。</returns>
        private string BuildPaymentUrl(ThirdPayOrderRecord order, string googleToken, bool isExternalBrowser)
        {
            IAPProductEntry productEntry = Table?.FindByTableId(order.TableId);
            if (productEntry == null)
            {
                throw new InvalidOperationException($"支付商品表中不存在 TableId={order.TableId} 的条目。");
            }

            PbNetThirdProductInfo productInfo = FindProductInfo(order.TableId);
            if (productInfo == null)
            {
                throw new InvalidOperationException($"第三方商品列表中不存在 TableId={order.TableId} 对应的商品。");
            }

            if (productInfo.Id <= 0 || string.IsNullOrEmpty(productInfo.LocalCurrency) || string.IsNullOrEmpty(productInfo.LocalPrice) || string.IsNullOrEmpty(productEntry.Name))
            {
                throw new InvalidOperationException($"TableId={order.TableId} 的第三方商品 ID、货币、价格或名称不完整。");
            }

            if (!EnsurePayEnvironment())
            {
                throw new InvalidOperationException("第三方支付环境未就绪：AES 配置或支付页基址无法解析。");
            }

            var builder = new ThirdPayUrlBuilder(value => Util.Encrypt.AES.EncryptString(value, m_AesKey, m_AesIv));
            PbNetReqHeader header = NetBuilder.BuildHeader();
            if (header.Appid <= 0)
            {
                throw new InvalidOperationException("公共请求头 AppId 未配置或不是有效正整数。");
            }

            return builder.Build(m_PayUrlBase, header.Language, new ThirdPayUrlPayload
            {
                ProductId = productInfo.Id,
                UserId = order.UserId,
                TableId = order.TableId,
                Currency = productInfo.LocalCurrency,
                Price = productInfo.LocalPrice,
                ProductName = productEntry.Name,
                CountryCode = GetCountryCode(),
                ClientOrderId = order.ClientOrderId,
                Platform = header.Platform.ToString(),
                AppId = header.Appid,
                ChannelParams = m_PersistData?.ChannelParams,
                GoogleToken = googleToken,
                ReceiptParam = order.ReceiptParam,
                IsExternalBrowser = isExternalBrowser,
            });
        }

        /// <summary>
        /// 验证单笔第三方支付订单。
        /// </summary>
        /// <param name="order">待验证订单。</param>
        /// <param name="scene">验单触发场景。</param>
        /// <param name="ct">取消令牌。</param>
        /// <returns>单笔订单验单结果。</returns>
        private async UniTask<IAPResult> ValidateOrderAsync(ThirdPayOrderRecord order, ThirdPayValidationScene scene, CancellationToken ct)
        {
            List<IAPResult> results = await ValidateOrdersAsync(new List<ThirdPayOrderRecord> { order }, scene, ct);
            return results.Count > 0 ? results[0] : new IAPResult(order.TableId, (int)IAPThirdPayErrorCode.ServerValidationFailed, IAPErrorSource.ThirdPay, "验单未返回结果。", order.CustomData, order.ReceiptParam);
        }

        /// <summary>
        /// 批量验证第三方支付订单，并按当前支付或补单场景应用重试策略。
        /// </summary>
        /// <param name="orders">待验证订单列表。</param>
        /// <param name="scene">验单触发场景。</param>
        /// <param name="ct">取消令牌。</param>
        /// <returns>与输入订单顺序一致的验单结果。</returns>
        private async UniTask<List<IAPResult>> ValidateOrdersAsync(List<ThirdPayOrderRecord> orders, ThirdPayValidationScene scene, CancellationToken ct)
        {
            if (orders == null || orders.Count == 0)
            {
                return new List<IAPResult>();
            }

            bool isRecovered = scene == ThirdPayValidationScene.Recovered;
            bool publishNonTerminalFailures = scene == ThirdPayValidationScene.DirectPay;

            if (m_NetService == null || string.IsNullOrEmpty(m_Config?.VerifyIapCmdName))
            {
                TrackValidationFailureBatchInternal(orders, isRecovered, 0, false, 0, "验单服务未配置。", true);
                return BuildValidationFailures(orders, "验单服务未配置。", publishNonTerminalFailures);
            }

            var clientOrderIds = new List<string>(orders.Count);
            foreach (ThirdPayOrderRecord order in orders)
            {
                clientOrderIds.Add(order.ClientOrderId);
            }

            int maxAttempts = isRecovered ? 1 : Math.Max(1, Context?.RetryValidateMaxNum ?? 1);
            for (int attempt = 0; attempt < maxAttempts; attempt++)
            {
                ct.ThrowIfCancellationRequested();
                NetResponse<PbNetThirdVerifyIapResp> response = await m_NetService.VerifyIapAsync(m_Config.VerifyIapCmdName, clientOrderIds);
                if (response.IsSuccess && response.Data != null)
                {
                    return ApplyValidationResponse(orders, response.Data, scene, attempt + 1);
                }

                bool isFinalAttempt = attempt + 1 >= maxAttempts;
                TrackValidationFailureBatchInternal(orders, isRecovered, attempt + 1, true, response.ErrorCode, response.ErrorMessage, isFinalAttempt);
                if (scene == ThirdPayValidationScene.DirectPay && attempt == 0)
                {
                    TrackFirstValidationFailureBatchInternal(orders, attempt + 1, true);
                }

                if (!isFinalAttempt)
                {
                    int intervalIndex = Math.Min(attempt, s_ValidateRetryIntervals.Length - 1);
                    await UniTask.Delay(TimeSpan.FromSeconds(s_ValidateRetryIntervals[intervalIndex]), cancellationToken: ct);
                }
            }

            return BuildValidationFailures(orders, "验单网络请求失败，订单已保留。", publishNonTerminalFailures);
        }

        /// <summary>
        /// 将服务端验单响应转换为业务结果，并同步更新本地订单存档。
        /// </summary>
        /// <param name="orders">本次验单的本地订单列表。</param>
        /// <param name="response">服务端验单响应。</param>
        /// <param name="scene">验单触发场景。</param>
        /// <param name="validateCount">本次成功响应对应的验单次数。</param>
        /// <returns>与输入订单顺序一致的业务结果。</returns>
        private List<IAPResult> ApplyValidationResponse(List<ThirdPayOrderRecord> orders, PbNetThirdVerifyIapResp response, ThirdPayValidationScene scene, int validateCount)
        {
            var results = new List<IAPResult>(orders.Count);
            bool anyRemoved = false;
            bool isRecovered = scene == ThirdPayValidationScene.Recovered;
            bool publishNonTerminalFailures = scene == ThirdPayValidationScene.DirectPay;
            bool publishTerminalFailures = scene != ThirdPayValidationScene.Recovered;
            foreach (ThirdPayOrderRecord order in orders)
            {
                PbNetThirdVerifyOrderResult matched = FindResponse(response, order.ClientOrderId);
                if (matched == null)
                {
                    const string missingReason = "验单响应未包含该订单，订单已保留。";
                    TrackValidateFailFinishInternal(order, isRecovered, validateCount, false, 0, missingReason);
                    var missing = new IAPResult(order.TableId, (int)IAPThirdPayErrorCode.ServerValidationFailed, IAPErrorSource.ThirdPay, missingReason, order.CustomData, order.ReceiptParam);
                    if (publishNonTerminalFailures)
                    {
                        Context?.EventBridge?.RaisePayFailed(missing);
                    }

                    results.Add(missing);
                    continue;
                }

                ThirdPayOrderResolution resolution = ThirdPayOrderResolution.FromStatus(matched.Status);
                long tableId = matched.TableId != 0 ? matched.TableId : order.TableId;
                string receiptParam = string.IsNullOrEmpty(matched.ReceiptParam) ? order.ReceiptParam : matched.ReceiptParam;
                if (resolution.RemoveOrder)
                {
                    anyRemoved |= m_OrderRepository.Remove(order.ClientOrderId, false);
                }

                if (resolution.Disposition == ThirdPayOrderDisposition.Deliverable)
                {
                    string orderId = string.IsNullOrEmpty(matched.ServerOrderId) ? order.ClientOrderId : matched.ServerOrderId;
                    TrackValidateSuccessInternal(order, tableId, orderId, isRecovered, validateCount);
                    var success = new IAPResult(tableId, orderId, isRecovered, true, order.CustomData, receiptParam);
                    Context?.EventBridge?.RaisePaySuccess(success);
                    results.Add(success);
                    continue;
                }

                if (resolution.Disposition == ThirdPayOrderDisposition.AlreadyDelivered)
                {
                    string orderId = string.IsNullOrEmpty(matched.ServerOrderId) ? order.ClientOrderId : matched.ServerOrderId;
                    TrackValidateSuccessInternal(order, tableId, orderId, isRecovered, validateCount);
                    results.Add(new IAPResult(tableId, orderId, isRecovered, false, order.CustomData, receiptParam));
                    continue;
                }

                bool isFailed = resolution.Disposition == ThirdPayOrderDisposition.Failed
                    || resolution.Disposition == ThirdPayOrderDisposition.NotFound;
                string reason = resolution.Disposition == ThirdPayOrderDisposition.NotFound
                    ? "第三方订单不存在，已移除本地订单。"
                    : isFailed
                        ? $"第三方订单支付失败，状态={matched.Status}。"
                        : $"第三方订单仍在处理中，状态={matched.Status}，订单已保留。";
                var failure = new IAPResult(tableId, (int)(isFailed ? IAPThirdPayErrorCode.ServerValidationFailed : IAPThirdPayErrorCode.OrderPending), IAPErrorSource.ThirdPay, reason, order.CustomData, receiptParam);
                if (isFailed)
                {
                    TrackValidateFailFinishInternal(order, isRecovered, validateCount, false, 0, reason);
                    if (publishTerminalFailures)
                    {
                        Context?.EventBridge?.RaisePayFailed(failure);
                    }
                }
                else
                {
                    TrackValidateFailInternal(order, isRecovered, validateCount, false, 0, reason);
                    if (publishNonTerminalFailures)
                    {
                        Context?.EventBridge?.RaisePayFailed(failure);
                    }
                }

                results.Add(failure);
            }

            if (anyRemoved)
            {
                m_OrderRepository.Save();
            }

            return results;
        }

        /// <summary>
        /// 按客户端订单号查找对应的服务端验单条目。
        /// </summary>
        /// <param name="response">服务端验单响应。</param>
        /// <param name="clientOrderId">客户端订单号。</param>
        /// <returns>匹配的验单条目；未命中时返回 null。</returns>
        private static PbNetThirdVerifyOrderResult FindResponse(PbNetThirdVerifyIapResp response, string clientOrderId)
        {
            if (response?.OrderList == null)
            {
                return null;
            }

            foreach (PbNetThirdVerifyOrderResult item in response.OrderList)
            {
                if (string.Equals(item.ClientOrderId, clientOrderId, StringComparison.Ordinal))
                {
                    return item;
                }
            }

            return null;
        }

        /// <summary>
        /// 为整批订单构造相同原因的验单失败结果。
        /// </summary>
        /// <param name="orders">待构造结果的订单列表。</param>
        /// <param name="reason">失败原因。</param>
        /// <param name="publishFailures">是否向业务事件桥发布失败事件。</param>
        /// <returns>与输入订单顺序一致的失败结果。</returns>
        private List<IAPResult> BuildValidationFailures(List<ThirdPayOrderRecord> orders, string reason, bool publishFailures)
        {
            var results = new List<IAPResult>(orders.Count);
            foreach (ThirdPayOrderRecord order in orders)
            {
                var failure = new IAPResult(order.TableId, (int)IAPThirdPayErrorCode.ServerValidationFailed, IAPErrorSource.ThirdPay, reason, order.CustomData, order.ReceiptParam);
                if (publishFailures)
                {
                    Context?.EventBridge?.RaisePayFailed(failure);
                }

                results.Add(failure);
            }

            return results;
        }

        /// <summary>
        /// 构造并发布单次第三方支付失败结果。
        /// </summary>
        /// <param name="request">第三方支付请求。</param>
        /// <param name="code">第三方支付错误码。</param>
        /// <param name="reason">失败原因。</param>
        /// <returns>已发布的失败结果。</returns>
        private IAPResult Fail(IAPThirdPayRequest request, IAPThirdPayErrorCode code, string reason)
        {
            var result = new IAPResult(request.TableId, (int)code, IAPErrorSource.ThirdPay, reason, request.CustomData, request.ReceiptParam);
            Context?.EventBridge?.RaisePayFailed(result);
            return result;
        }

        /// <summary>
        /// 切换当前存档并重建订单仓储。
        /// </summary>
        /// <param name="data">当前账号存档；为空时创建空存档。</param>
        private void ResetRepository(ThirdPayPersistData data)
        {
            m_PersistData = data ?? (ThirdPayPersistData)CreateEmptyPersistData();
            m_OrderRepository = new ThirdPayOrderRepository(m_PersistData, SavePersistData);
        }

        /// <summary>
        /// 生成当前客户端唯一的第三方支付订单号。
        /// </summary>
        /// <returns>UTC 毫秒时间戳与 GUID 片段组成的订单号。</returns>
        private static string GenerateOrderId()
        {
            return DateTime.UtcNow.ToString("yyyyMMddHHmmssfff") + Guid.NewGuid().ToString("N").Substring(0, 8);
        }
    }
}
