/***************************************************************
 * (c) copyright 2026 - 2030, Solotopia
 * All Rights Reserved.
 * -------------------------------------------------------------
 * filename:  ThirdPayCountryResolver.cs
 * author:    yingzheng
 * created:   2026/9/17
 * descrip:   ThirdPay 国家码来源解析与配置刷新协调
 ***************************************************************/

using System;
using System.Threading;
using Cysharp.Threading.Tasks;
using NovaFramework.Runtime;

namespace NovaFramework.SDK.IAP.ThirdPay.Runtime
{
    /// <summary>
    /// 协调国家码来源、持久化以及国家变化后的支付配置刷新。
    /// </summary>
    internal sealed class ThirdPayCountryResolver : ThirdPayLogOwner
    {
        /// <summary>
        /// ThirdPay 强类型服务容器。
        /// </summary>
        private readonly ThirdPayServiceHub m_Hub;

        /// <summary>
        /// 创建国家码解析协调器。
        /// </summary>
        /// <param name="hub">ThirdPay 强类型服务容器。</param>
        public ThirdPayCountryResolver(ThirdPayServiceHub hub)
        {
            m_Hub = hub ?? throw new ArgumentNullException(nameof(hub));
        }

        /// <summary>
        /// 获取当前有效国家码，保持 Debug、锁定值及平台来源的既有优先级。
        /// </summary>
        /// <returns>规范化后的当前有效国家码；无有效来源时返回空字符串。</returns>
        public string Resolve()
        {
            return m_Hub.CountryState.Resolve();
        }

        /// <summary>
        /// 设置 Debug 国家码并重新拉取对应支付配置。
        /// </summary>
        /// <param name="countryCode">Debug 国家码；空值表示清除覆盖。</param>
        public void SetDebugCountryCode(string countryCode)
        {
            m_Hub.CountryState.SetDebugCountryCode(countryCode);
            m_Hub.PaymentConfigService.Invalidate();
            m_Hub.Store.TrySchedulePaymentConfigPrefetch("Debug 国家码变化支付配置预取");
        }

        /// <summary>
        /// 记录 Google Play Billing 返回的商店国家码。
        /// </summary>
        /// <param name="countryCode">Billing 返回的国家或地区代码。</param>
        public void SetBillingCountryCode(string countryCode)
        {
            string previousCountryCode = Resolve();
            if (!m_Hub.CountryState.SetBillingCountryCode(countryCode))
            {
                return;
            }

            LogDebug($"ThirdPay BillingCountryCode={m_Hub.CountryState.BillingCountryCode}");
            PersistCountryCodesIfChanged();
            RefreshPaymentConfigIfCountryChanged(previousCountryCode);
        }

        /// <summary>
        /// 记录登录后首次业务读取锁定的国家码。
        /// </summary>
        /// <param name="countryCode">需要在当前 Store 生命周期内锁定的国家码。</param>
        public void SetLockCountryCode(string countryCode)
        {
            m_Hub.CountryState.SetLockCountryCode(countryCode);
        }

        /// <summary>
        /// 记录 iOS StoreKit storefront 返回的国家码和区域标识。
        /// </summary>
        /// <param name="countryCode">StoreKit 返回的国家或地区代码。</param>
        /// <param name="identifier">Storefront 区域标识。</param>
        public void SetIosStorefrontCountryCode(string countryCode, string identifier)
        {
            string previousCountryCode = Resolve();
            if (!m_Hub.CountryState.SetIosStorefrontCountryCode(countryCode, identifier))
            {
                return;
            }

            LogDebug($"ThirdPay IosStorefrontCountryCode={m_Hub.CountryState.IosStorefrontCountryCode}, StorefrontIdentifier={m_Hub.CountryState.IosStorefrontIdentifier}");
            PersistCountryCodesIfChanged();
            RefreshPaymentConfigIfCountryChanged(previousCountryCode);
        }

        /// <summary>
        /// 记录广告模块返回或存档恢复的国家码。
        /// </summary>
        /// <param name="countryCode">广告模块返回的国家或地区代码。</param>
        public void SetAdCountryCode(string countryCode)
        {
            string previousCountryCode = Resolve();
            if (!m_Hub.CountryState.SetAdCountryCode(countryCode))
            {
                return;
            }

            LogDebug($"ThirdPay AdCountryCode={m_Hub.CountryState.AdCountryCode}");
            PersistCountryCodesIfChanged();
            RefreshPaymentConfigIfCountryChanged(previousCountryCode);
        }

        /// <summary>
        /// 清理除 Debug 覆盖外的运行时国家码来源。
        /// </summary>
        public void ClearRuntimeSources()
        {
            m_Hub.CountryState.ClearRuntimeSources();
        }

        /// <summary>
        /// 清理全部国家码来源。
        /// </summary>
        public void ClearAll()
        {
            m_Hub.CountryState.ClearAll();
        }

        /// <summary>
        /// 请求 iOS StoreKit storefront 国家码；其他平台由桥接层直接忽略。
        /// </summary>
        public void ResolveIosStorefrontCountryCode()
        {
            try
            {
                ThirdPayIosStorefrontCountryBridge.Request(SetIosStorefrontCountryCode);
            }
            catch (Exception ex)
            {
                LogWarning($"读取原生商店国家码失败，将继续使用后续兜底国家码：{ex.Message}");
            }
        }

        /// <summary>
        /// 从 Nova AD 模块异步读取广告国家码。
        /// </summary>
        /// <param name="ct">取消令牌。</param>
        /// <returns>国家码读取完成的异步任务。</returns>
        public async UniTask ResolveAdCountryCodeAsync(CancellationToken ct)
        {
            try
            {
                SDKComponent sdk = await WaitForSdkInitializedAsync(ct);
                if (sdk == null)
                {
                    LogWarning("读取广告国家码失败：SDKComponent 或 SDKManager 不可用。");
                    return;
                }

                if (!sdk.TryGet<IAdPlugin>(out IAdPlugin adPlugin))
                {
                    LogWarning("读取广告国家码失败：SDK 初始化完成后未找到可用的 IAdPlugin。");
                    return;
                }

                // 直接等待共享数据槽首次发布，避免业务层 GetCountryCodeAsync 的超时窗口丢失迟到结果。
                object value = await adPlugin.FetchDataAsync(SDKDataKeys.AdCountryCode, ct);
                if (!(value is string countryCode))
                {
                    LogWarning($"读取广告国家码失败：数据类型无效，实际类型={value?.GetType().FullName ?? "null"}。");
                    return;
                }

                string normalizedCountryCode = ThirdPayCountryState.NormalizeCountryCode(countryCode);
                if (string.IsNullOrEmpty(normalizedCountryCode))
                {
                    LogWarning($"读取广告国家码失败：广告模块返回了无效国家码，值={countryCode}。");
                    return;
                }

                SetAdCountryCode(normalizedCountryCode);
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
        /// 退出当前 IAP 初始化调用栈后等待 SDK 管理器完成全部插件初始化，避免跨插件查询重入初始化任务。
        /// </summary>
        /// <param name="ct">取消令牌。</param>
        /// <returns>已完成统一初始化的 SDK 组件；组件或管理器不可用时返回 null。</returns>
        private static async UniTask<SDKComponent> WaitForSdkInitializedAsync(CancellationToken ct)
        {
            SDKComponent sdk = Nova.SDK;
            if (sdk == null || sdk.SDKManager == null)
            {
                return null;
            }

            await UniTask.Yield(cancellationToken: ct);
            await sdk.SDKManager.WaitForInitializedAsync(ct);
            return sdk;
        }

        /// <summary>
        /// 将平台解析出的国家码写入当前账号存档。
        /// </summary>
        private void PersistCountryCodesIfChanged()
        {
            m_Hub.PersistContext.PersistCountryCodesIfChanged(m_Hub.CountryState);
        }

        /// <summary>
        /// 有效国家变化时使配置快照失效，并在账号已就绪时重新预取。
        /// </summary>
        /// <param name="previousCountryCode">更新来源前解析出的有效国家码。</param>
        private void RefreshPaymentConfigIfCountryChanged(string previousCountryCode)
        {
            if (string.Equals(previousCountryCode, Resolve(), StringComparison.OrdinalIgnoreCase))
            {
                return;
            }

            m_Hub.PaymentConfigService?.Invalidate();
            m_Hub.Store.TrySchedulePaymentConfigPrefetch("国家码变化支付配置预取");
        }
    }
}
