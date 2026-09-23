/***************************************************************
 * (c) copyright 2026 - 2030, Solotopia
 * All Rights Reserved.
 * -------------------------------------------------------------
 * filename:  ThirdPayStore.Methods.cs
 * author:    yingzheng
 * created:   2026/5/20
 * descrip:   ThirdPayStore 内部服务适配方法
 ***************************************************************/

using System;
using System.Collections.Generic;
using System.Threading;
using Cysharp.Threading.Tasks;
using NovaFramework.Runtime;
using NovaFramework.SDK.IAP.Runtime;

namespace NovaFramework.SDK.IAP.ThirdPay.Runtime
{
    /// <summary>
    /// 承载 ThirdPayStore 的内部服务适配方法。
    /// </summary>
    public sealed partial class ThirdPayStore
    {
        /// <summary>
        /// 标记用户已经主动进入支付流程，供支付协调器更新 Loading 展示语义。
        /// </summary>
        internal void MarkUserInteracted()
        {
            m_LoadingGuard.HasUserInteracted = true;
        }

        /// <summary>
        /// ThirdPay 的前置校验失败由 PayAsync 返回边界统一映射到细分失败原因后上报。
        /// </summary>
        /// <param name="result">支付前置校验生成的失败结果。</param>
        /// <returns>固定返回 false，禁止基类使用通用错误码直接上报。</returns>
        protected override bool ShouldTrackPayGuardFailure(IAPResult result)
        {
            return false;
        }

        /// <summary>
        /// 注入测试或平台适配使用的 Google 外部结算客户端。
        /// </summary>
        /// <param name="client">Google 外部结算客户端；传入 null 时关闭政策流程。</param>
        internal void SetGoogleExternalBillingClient(IThirdPayGoogleExternalBillingClient client)
        {
            m_Hub.GooglePolicy?.Dispose();
            m_Hub.GooglePolicy = client == null ? null : new ThirdPayGooglePolicyService(client);
        }

        /// <summary>
        /// 初始化 Google 外链政策服务入口；非 Android 真机直接跳过。
        /// </summary>
        /// <param name="ct">取消令牌。</param>
        /// <returns>初始化完成的异步任务。</returns>
        private UniTask InitializeGooglePolicyAsync(CancellationToken ct)
        {
#if UNITY_ANDROID && !UNITY_EDITOR
            return InitializeAndroidGooglePolicyAsync(ct);
#else
            return UniTask.CompletedTask;
#endif
        }

#if UNITY_ANDROID && !UNITY_EDITOR
        /// <summary>
        /// Android 真机初始化 Google 外链政策服务，并尽力读取 Google Play Billing 商店国家码。
        /// </summary>
        /// <param name="ct">取消令牌。</param>
        /// <returns>初始化完成的异步任务。</returns>
        private async UniTask InitializeAndroidGooglePolicyAsync(CancellationToken ct)
        {
            if (m_Hub.GooglePolicy == null)
            {
                double googleTimeout = m_Hub.Config?.GoogleApiTimeoutSeconds ?? 15d;
                m_Hub.GooglePolicy = new ThirdPayGooglePolicyService(new ThirdPayGoogleExternalBillingClient(googleTimeout));
            }

            if (m_Hub.GooglePolicy == null)
            {
                return;
            }

            try
            {
                string billingCountryCode = await m_Hub.GooglePolicy.GetBillingCountryCodeAsync(ct);
                SetBillingCountryCode(billingCountryCode);
            }
            catch (OperationCanceledException)
            {
                throw;
            }
            catch (Exception ex)
            {
                LogWarning($"读取 Google Play Billing 商店地区失败，将使用配置中的地区码：{ex.Message}");
            }
        }
#endif

        /// <summary>
        /// 校验第三方支付必需的 Store 配置项是否齐备。
        /// </summary>
        /// <returns>统一配置与验单协议均已配置时返回 true。</returns>
        private bool ValidateConfig()
        {
            if (m_Hub.Config == null)
            {
                return false;
            }

            bool ready = true;
            if (string.IsNullOrEmpty(m_Hub.Config.PaymentConfigCmdName))
            {
                LogError("ThirdPayStoreConfig.PaymentConfigCmdName 未配置，无法拉取第三方支付配置。");
                ready = false;
            }

            if (string.IsNullOrEmpty(m_Hub.Config.VerifyIapCmdName))
            {
                LogError("ThirdPayStoreConfig.VerifyIapCmdName 未配置，无法验单。");
                ready = false;
            }

            if (string.IsNullOrEmpty(m_Hub.Config.QueryPendingOrderCmdName))
            {
                LogWarning("ThirdPayStoreConfig.QueryPendingOrderCmdName 未配置，将无法查询服务端未校验订单。");
            }

            return ready;
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
        /// 将 Billing 国家码转交国家解析服务；未初始化时仅更新状态。
        /// </summary>
        /// <param name="countryCode">Billing 返回的国家或地区代码。</param>
        private void SetBillingCountryCode(string countryCode)
        {
            if (m_Hub.CountryResolver != null)
            {
                m_Hub.CountryResolver.SetBillingCountryCode(countryCode);
                return;
            }

            m_Hub.CountryState.SetBillingCountryCode(countryCode);
        }

        /// <summary>
        /// 将登录后首次业务读取锁定的国家码转交国家解析服务。
        /// </summary>
        /// <param name="countryCode">需要在当前 Store 生命周期内锁定的国家码。</param>
        private void SetLockCountryCode(string countryCode)
        {
            if (m_Hub.CountryResolver != null)
            {
                m_Hub.CountryResolver.SetLockCountryCode(countryCode);
                return;
            }

            m_Hub.CountryState.SetLockCountryCode(countryCode);
        }

        /// <summary>
        /// 将 iOS storefront 国家码转交国家解析服务；未初始化时仅更新状态。
        /// </summary>
        /// <param name="countryCode">StoreKit 返回的国家或地区代码。</param>
        /// <param name="identifier">Storefront 区域标识。</param>
        private void SetIosStorefrontCountryCode(string countryCode, string identifier)
        {
            if (m_Hub.CountryResolver != null)
            {
                m_Hub.CountryResolver.SetIosStorefrontCountryCode(countryCode, identifier);
                return;
            }

            m_Hub.CountryState.SetIosStorefrontCountryCode(countryCode, identifier);
        }

        /// <summary>
        /// 将广告国家码转交国家解析服务；未初始化时仅更新状态。
        /// </summary>
        /// <param name="countryCode">广告模块返回的国家或地区代码。</param>
        private void SetAdCountryCode(string countryCode)
        {
            if (m_Hub.CountryResolver != null)
            {
                m_Hub.CountryResolver.SetAdCountryCode(countryCode);
                return;
            }

            m_Hub.CountryState.SetAdCountryCode(countryCode);
        }

        /// <summary>
        /// 清理当前账号运行时解析出的国家码。
        /// </summary>
        private void ClearResolvedCountryCodes()
        {
            if (m_Hub.CountryResolver != null)
            {
                m_Hub.CountryResolver.ClearRuntimeSources();
                return;
            }

            m_Hub.CountryState.ClearRuntimeSources();
        }

        /// <summary>
        /// 请求 iOS StoreKit storefront 国家码。
        /// </summary>
        private void ResolveIosStorefrontCountryCode()
        {
            m_Hub.CountryResolver?.ResolveIosStorefrontCountryCode();
        }

        /// <summary>
        /// 请求广告模块国家码。
        /// </summary>
        /// <param name="ct">取消令牌。</param>
        /// <returns>国家码读取完成的异步任务。</returns>
        private UniTask ResolveAdCountryCodeAsync(CancellationToken ct)
        {
            return m_Hub.CountryResolver?.ResolveAdCountryCodeAsync(ct) ?? UniTask.CompletedTask;
        }

        /// <summary>
        /// 在 Store 初始化阶段并行启动各国家码来源；结果在 Store 生命周期内持续复用。
        /// </summary>
        private void ResolveCountryCodesInBackground()
        {
            ResolveIosStorefrontCountryCode();
            m_Hub.RunBackgroundTask(InitializeGooglePolicyAsync, "Google Billing 国家码解析");
            m_Hub.RunBackgroundTask(ResolveAdCountryCodeAsync, "广告国家码解析");
        }

        /// <summary>
        /// 在配置、账号、国家码和协议服务均就绪时统一调度支付配置预取。
        /// </summary>
        /// <param name="taskName">用于后台任务和诊断日志的触发原因。</param>
        /// <returns>满足全部门禁并成功提交后台任务时返回 true。</returns>
        internal bool TrySchedulePaymentConfigPrefetch(string taskName)
        {
            if (!m_Hub.ConfigReady)
            {
                LogDebug($"未调度第三方支付配置预取：Store 配置尚未就绪，触发原因={taskName}。");
                return false;
            }

            if (string.IsNullOrEmpty(m_GameUID))
            {
                LogDebug($"未调度第三方支付配置预取：等待登录 UID，触发原因={taskName}。");
                return false;
            }

            if (m_Hub.PaymentConfigService == null)
            {
                LogWarning($"未调度第三方支付配置预取：配置服务尚未初始化，触发原因={taskName}。");
                return false;
            }

            if (string.IsNullOrEmpty(m_Hub.Config?.PaymentConfigCmdName))
            {
                LogWarning($"未调度第三方支付配置预取：PaymentConfigCmdName 为空，触发原因={taskName}。");
                return false;
            }

            string countryCode = GetCountryCode();
            if (string.IsNullOrEmpty(countryCode))
            {
                LogWarning($"未调度第三方支付配置预取：登录后仍未取得有效国家码，将等待平台国家码回调，触发原因={taskName}。");
                return false;
            }

            LogDebug($"调度第三方支付配置预取：触发原因={taskName}，Country={countryCode}。");
            m_Hub.RunBackgroundTask(PrefetchPaymentConfigAsync, taskName);
            return true;
        }

        /// <summary>
        /// 确保当前账号、国家和路由已持有有效支付配置快照。
        /// </summary>
        /// <param name="ct">取消令牌。</param>
        /// <returns>当前上下文已取得有效配置快照时返回 true。</returns>
        internal async UniTask<bool> EnsurePaymentConfigAsync(CancellationToken ct)
        {
            if (!m_Hub.ConfigReady)
            {
                LogWarning("获取第三方支付配置失败：Store 配置尚未就绪。");
                return false;
            }

            if (string.IsNullOrEmpty(m_GameUID))
            {
                LogWarning("获取第三方支付配置失败：用户尚未登录。");
                return false;
            }

            ThirdPayPaymentConfigService paymentConfigService = m_Hub.PaymentConfigService;
            if (paymentConfigService == null)
            {
                LogWarning("获取第三方支付配置失败：配置服务尚未初始化。");
                return false;
            }

            string cmdName = m_Hub.Config?.PaymentConfigCmdName;
            if (string.IsNullOrEmpty(cmdName))
            {
                LogWarning("获取第三方支付配置失败：PaymentConfigCmdName 为空。");
                return false;
            }

            string countryCode = GetCountryCode();
            if (string.IsNullOrEmpty(countryCode))
            {
                LogWarning("获取第三方支付配置失败：登录后仍未取得有效国家码，协议未发送。");
                return false;
            }

            var context = new ThirdPayPaymentConfigContext(m_GameUID, countryCode, cmdName);
            bool succeeded = await paymentConfigService.EnsureAsync(context, ct);
            ThirdPayPaymentConfigSnapshot snapshot = paymentConfigService.Snapshot;
            if (!succeeded || snapshot == null)
            {
                return false;
            }

            return true;
        }

        /// <summary>
        /// 在账号或国家变化后预取统一支付配置；失败只记录日志。
        /// </summary>
        /// <param name="ct">取消令牌。</param>
        /// <returns>配置预取完成的异步任务。</returns>
        internal async UniTask PrefetchPaymentConfigAsync(CancellationToken ct)
        {
            try
            {
                bool succeeded = await EnsurePaymentConfigAsync(ct);
                if (!succeeded)
                {
                    string lastFailureReason = m_Hub.PaymentConfigService?.LastFailureReason;
                    string failureReason = string.IsNullOrEmpty(lastFailureReason) ? "未取得有效配置快照。" : lastFailureReason;
                    LogWarning($"预取第三方支付配置失败：{failureReason} Country={GetCountryCode()}，Cmd={m_Hub.Config?.PaymentConfigCmdName ?? string.Empty}");
                }
            }
            catch (OperationCanceledException)
            {
                throw;
            }
            catch (Exception ex)
            {
                LogWarning($"预取第三方支付配置异常：{ex.Message}");
            }
        }

        /// <summary>
        /// 按支付表行 ID 查找已拉取的第三方商品。
        /// </summary>
        /// <param name="tableId">支付表行 ID。</param>
        /// <returns>匹配的第三方商品；未命中时返回 null。</returns>
        private PbNetThirdProductInfo FindProductInfo(long tableId)
        {
            IAPProductEntry entry = Table?.FindByTableId(tableId);
            return m_Hub.PaymentConfigService?.FindProduct(entry?.ThirdProductID);
        }

        /// <summary>
        /// 归一化 ISO 国家或地区代码，供现有内部调用适配使用。
        /// </summary>
        /// <param name="countryCode">原始国家或地区代码。</param>
        /// <returns>规范化后的代码；无效输入返回空字符串。</returns>
        private static string NormalizeCountryCode(string countryCode)
        {
            return ThirdPayCountryState.NormalizeCountryCode(countryCode);
        }

        /// <summary>
        /// 将支付请求委托给内部支付主流程协调器。
        /// </summary>
        /// <param name="request">第三方支付请求。</param>
        /// <param name="ct">取消令牌。</param>
        /// <returns>本次支付或验单的最终结果。</returns>
        private UniTask<IAPResult> ExecutePayAsync(IAPThirdPayRequest request, CancellationToken ct)
        {
            return m_Hub.CheckoutCoordinator != null ? m_Hub.CheckoutCoordinator.ExecuteAsync(request, ct) : UniTask.FromResult(Fail(request, IAPThirdPayErrorCode.StoreInitFailed, "第三方支付服务尚未初始化。"));
        }

        /// <summary>
        /// 验证单笔订单并同步 Store 最近一次结果。
        /// </summary>
        /// <param name="order">待验单的本地订单。</param>
        /// <param name="scene">本次验单触发场景。</param>
        /// <param name="ct">取消令牌。</param>
        /// <returns>单笔订单验单结果。</returns>
        internal async UniTask<IAPResult> ValidateOrderAsync(ThirdPayOrderRecord order, ThirdPayValidationScene scene, CancellationToken ct)
        {
            if (m_Hub.OrderValidationService == null)
            {
                var failure = new IAPResult(order.TableId, (int)IAPThirdPayErrorCode.ServerValidationFailed, IAPErrorSource.ThirdPay, "验单服务未初始化。", order.CustomData, order.ClientOrderId, scene == ThirdPayValidationScene.Recovered, order.ReceiptParam);
                return AttachReturnedValidationFailureContext(failure, order, new ThirdPayValidationTrackContext(scene, 0, ThirdPayClientOrderStatus.ValidationServiceUnavailable, string.Empty, ThirdPayPaymentFailureReason.ValidationServiceUnavailable));
            }

            List<IAPResult> results = await m_Hub.OrderValidationService.ValidateAsync(new List<ThirdPayOrderRecord> { order }, scene, ct);
            if (results.Count > 0)
            {
                return MarkStoreResult(results[0]);
            }

            var emptyResult = new IAPResult(order.TableId, (int)IAPThirdPayErrorCode.ServerValidationFailed, IAPErrorSource.ThirdPay, "验单未返回结果。", order.CustomData, order.ClientOrderId, scene == ThirdPayValidationScene.Recovered, order.ReceiptParam);
            return AttachReturnedValidationFailureContext(emptyResult, order, new ThirdPayValidationTrackContext(scene, 0, ThirdPayClientOrderStatus.ResponseOrderMissing, string.Empty, ThirdPayPaymentFailureReason.ValidationResponseOrderMissing));
        }

        /// <summary>
        /// 批量验单适配入口，实际规则由订单验单服务维护。
        /// </summary>
        /// <param name="orders">待验单的本地订单列表。</param>
        /// <param name="scene">本次验单触发场景。</param>
        /// <param name="ct">取消令牌。</param>
        /// <returns>与输入订单对应的验单结果列表。</returns>
        private UniTask<List<IAPResult>> ValidateOrdersAsync(List<ThirdPayOrderRecord> orders, ThirdPayValidationScene scene, CancellationToken ct)
        {
            return m_Hub.OrderValidationService != null ? m_Hub.OrderValidationService.ValidateAsync(orders, scene, ct) : UniTask.FromResult(new List<IAPResult>());
        }

        /// <summary>
        /// 将补单检查委托给订单恢复服务。
        /// </summary>
        /// <param name="ct">取消令牌。</param>
        /// <returns>补单查询与验单完成的异步任务。</returns>
        private UniTask RecoverOrdersAsync(CancellationToken ct)
        {
            return m_Hub.OrderRecoveryService?.RecoverAsync(ct) ?? UniTask.CompletedTask;
        }

        /// <summary>
        /// 保留原有内部测试入口，将订单合并规则委托给恢复服务。
        /// </summary>
        /// <param name="localOrders">当前账号的本地订单集合。</param>
        /// <param name="serverResponse">服务端待验订单响应。</param>
        /// <returns>按客户端订单号去重后的可恢复订单列表。</returns>
        private static List<ThirdPayOrderRecord> MergeRecoverableOrders(IReadOnlyCollection<ThirdPayOrderRecord> localOrders, PbNetThirdQueryPendingOrderResp serverResponse)
        {
            return ThirdPayOrderRecoveryService.MergeRecoverableOrders(localOrders, serverResponse);
        }

        /// <summary>
        /// 将外部浏览器回退 URL 构造委托给支付协调器。
        /// </summary>
        /// <param name="order">当前本地订单。</param>
        /// <param name="paymentConfig">本次支付固定使用的配置快照。</param>
        /// <param name="googleToken">Google 外链上报令牌；无令牌时为空。</param>
        /// <param name="isExternalBrowser">是否使用外部浏览器打开。</param>
        /// <param name="showBackButton">是否要求支付页显示返回按钮。</param>
        /// <param name="isCustomTab">是否由 Auth Tab 或 Custom Tabs 承载。</param>
        /// <returns>包含加密支付参数的完整支付 URL。</returns>
        private string BuildPaymentUrl(ThirdPayOrderRecord order, ThirdPayPaymentConfigSnapshot paymentConfig, string googleToken, bool isExternalBrowser, bool showBackButton = false, bool isCustomTab = false)
        {
            if (m_Hub.CheckoutCoordinator == null)
            {
                throw new InvalidOperationException("第三方支付主流程协调器尚未初始化。");
            }

            return m_Hub.CheckoutCoordinator.BuildPaymentUrl(order, paymentConfig, googleToken, isExternalBrowser, showBackButton, isCustomTab);
        }

        /// <summary>
        /// 支付页未成功建立会话时移除本地订单，并上报实际删除原因。
        /// </summary>
        /// <param name="order">需要清理的本地订单。</param>
        /// <param name="deleteReason">本次清理对应的稳定删除原因。</param>
        private void RemoveLocalOrderIfPaymentPageDidNotOpen(ThirdPayOrderRecord order, ThirdPayOrderDeleteReason deleteReason = ThirdPayOrderDeleteReason.PaymentPageNeverOpened)
        {
            RemoveLocalOrderAndTrack(order, false, 0, new ThirdPayValidationTrackContext(default, 0, ThirdPayClientOrderStatus.Unknown, string.Empty, ThirdPayPaymentFailureReason.PaymentPageOpenFailed), deleteReason, true);
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
            if (m_Hub.CheckoutCoordinator != null)
            {
                return m_Hub.CheckoutCoordinator.CreateFailure(request, code, reason);
            }

            var result = new IAPResult(request.TableId, (int)code, IAPErrorSource.ThirdPay, reason, request.CustomData, request.ReceiptParam);
            Context?.EventBridge?.RaisePayFailed(result);
            return result;
        }

        /// <summary>
        /// 切换当前账号存档并重建订单仓储。
        /// </summary>
        /// <param name="data">当前账号存档；为空时创建空存档。</param>
        private void ResetRepository(ThirdPayPersistData data)
        {
            m_Hub.PersistContext.Reset(data ?? (ThirdPayPersistData)CreateEmptyPersistData(), SavePersistData);
            m_Hub.PersistContext.RestoreCountryCodes(m_Hub.CountryState);
        }
    }
}
