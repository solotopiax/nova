/***************************************************************
 * (c) copyright 2026 - 2030, Solotopia
 * All Rights Reserved.
 * -------------------------------------------------------------
 * filename:  ThirdPayCountryState.cs
 * author:    yingzheng
 * created:   2026/9/16
 * descrip:   ThirdPay 国家码运行时状态
 ***************************************************************/

using System;

namespace NovaFramework.SDK.IAP.ThirdPay.Runtime
{
    /// <summary>
    /// 维护 ThirdPay 国家码来源、优先级和存档同步，不直接参与支付流程编排。
    /// </summary>
    internal sealed class ThirdPayCountryState
    {
        /// <summary>
        /// 广告或平台侧可能返回的无效国家码，ThirdPay 视为无国家码。
        /// </summary>
        private const string c_InvalidCountryCode = "IV";

        /// <summary>
        /// iOS StoreKit storefront 在无法识别商店国家码时返回的占位值。
        /// </summary>
        private const string c_UnknownCountryCode = "UNKNOWN";

        /// <summary>
        /// Debug 覆盖用国家或地区代码，优先级高于所有运行时自动解析来源。
        /// </summary>
        public string DebugCountryCode { get; private set; } = string.Empty;

        /// <summary>
        /// 首次自动解析后锁定的国家码，避免同一运行期商品国家反复漂移。
        /// </summary>
        public string LockCountryCode { get; private set; } = string.Empty;

        /// <summary>
        /// Google Play Billing 返回的商店国家码。
        /// </summary>
        public string BillingCountryCode { get; private set; } = string.Empty;

        /// <summary>
        /// iOS StoreKit storefront 返回的商店国家码。
        /// </summary>
        public string IosStorefrontCountryCode { get; private set; } = string.Empty;

        /// <summary>
        /// iOS StoreKit storefront 返回的商店区域标识。
        /// </summary>
        public string IosStorefrontIdentifier { get; private set; } = string.Empty;

        /// <summary>
        /// 广告模块返回或 ThirdPay 存档恢复的国家码。
        /// </summary>
        public string AdCountryCode { get; private set; } = string.Empty;

        /// <summary>
        /// 当前是否存在 Debug 覆盖国家码。
        /// </summary>
        public bool HasDebugCountryCode => !string.IsNullOrEmpty(DebugCountryCode);

        /// <summary>
        /// 设置 Debug 覆盖国家码，空值、IV 和 UNKNOWN 会归一为空。
        /// </summary>
        /// <param name="countryCode">原始国家或地区代码。</param>
        public void SetDebugCountryCode(string countryCode)
        {
            DebugCountryCode = NormalizeCountryCode(countryCode);
        }

        /// <summary>
        /// 设置商品快照锁定国家码。
        /// </summary>
        /// <param name="countryCode">商品列表请求使用的国家或地区代码。</param>
        public void SetLockCountryCode(string countryCode)
        {
            LockCountryCode = NormalizeCountryCode(countryCode);
        }

        /// <summary>
        /// 设置 Google Play Billing 返回的商店国家码。
        /// </summary>
        /// <param name="countryCode">Billing 原始国家或地区代码。</param>
        /// <returns>写入了有效国家码时返回 true。</returns>
        public bool SetBillingCountryCode(string countryCode)
        {
            string normalizedCountryCode = NormalizeCountryCode(countryCode);
            if (string.IsNullOrEmpty(normalizedCountryCode))
            {
                return false;
            }

            BillingCountryCode = normalizedCountryCode;
            return true;
        }

        /// <summary>
        /// 设置 iOS StoreKit storefront 返回的商店国家码和区域标识。
        /// </summary>
        /// <param name="countryCode">iOS storefront 原始国家或地区代码。</param>
        /// <param name="identifier">iOS storefront 区域标识。</param>
        /// <returns>写入了有效国家码时返回 true。</returns>
        public bool SetIosStorefrontCountryCode(string countryCode, string identifier)
        {
            IosStorefrontIdentifier = string.IsNullOrWhiteSpace(identifier) ? string.Empty : identifier.Trim();
            string normalizedCountryCode = NormalizeCountryCode(countryCode);
            if (string.IsNullOrEmpty(normalizedCountryCode))
            {
                return false;
            }

            IosStorefrontCountryCode = normalizedCountryCode;
            return true;
        }

        /// <summary>
        /// 设置广告模块返回的国家码。
        /// </summary>
        /// <param name="countryCode">广告模块原始国家或地区代码。</param>
        /// <returns>写入了有效国家码时返回 true。</returns>
        public bool SetAdCountryCode(string countryCode)
        {
            string normalizedCountryCode = NormalizeCountryCode(countryCode);
            if (string.IsNullOrEmpty(normalizedCountryCode))
            {
                return false;
            }

            AdCountryCode = normalizedCountryCode;
            return true;
        }

        /// <summary>
        /// 按 Debug > Lock > Billing > iOS Storefront > AD 的优先级解析当前有效国家码。
        /// </summary>
        /// <returns>当前有效国家码；所有来源缺失时返回空字符串。</returns>
        public string Resolve()
        {
            if (!string.IsNullOrEmpty(DebugCountryCode))
            {
                return DebugCountryCode;
            }

            if (!string.IsNullOrEmpty(LockCountryCode))
            {
                return LockCountryCode;
            }

            if (!string.IsNullOrEmpty(BillingCountryCode))
            {
                return BillingCountryCode;
            }

            if (!string.IsNullOrEmpty(IosStorefrontCountryCode))
            {
                return IosStorefrontCountryCode;
            }

            return !string.IsNullOrEmpty(AdCountryCode) ? AdCountryCode : string.Empty;
        }

        /// <summary>
        /// 清理除 Debug 覆盖外的运行时国家码来源。
        /// </summary>
        public void ClearRuntimeSources()
        {
            LockCountryCode = string.Empty;
            BillingCountryCode = string.Empty;
            IosStorefrontCountryCode = string.Empty;
            IosStorefrontIdentifier = string.Empty;
            AdCountryCode = string.Empty;
        }

        /// <summary>
        /// 清理所有国家码来源。
        /// </summary>
        public void ClearAll()
        {
            DebugCountryCode = string.Empty;
            ClearRuntimeSources();
        }

        /// <summary>
        /// 从当前账号存档恢复 Billing / iOS Storefront / AD 国家码兜底值。
        /// </summary>
        /// <param name="data">当前账号 ThirdPay 存档。</param>
        public void RestorePersistedCountryCodes(ThirdPayPersistData data)
        {
            if (data == null)
            {
                return;
            }

            string billingCountryCode = NormalizeCountryCode(data.BillingCountryCode);
            if (!string.IsNullOrEmpty(billingCountryCode) && string.IsNullOrEmpty(BillingCountryCode))
            {
                BillingCountryCode = billingCountryCode;
            }

            string iosStorefrontCountryCode = NormalizeCountryCode(data.IosStorefrontCountryCode);
            if (!string.IsNullOrEmpty(iosStorefrontCountryCode) && string.IsNullOrEmpty(IosStorefrontCountryCode))
            {
                IosStorefrontCountryCode = iosStorefrontCountryCode;
            }

            string adCountryCode = NormalizeCountryCode(data.AdCountryCode);
            if (!string.IsNullOrEmpty(adCountryCode) && string.IsNullOrEmpty(AdCountryCode))
            {
                AdCountryCode = adCountryCode;
            }
        }

        /// <summary>
        /// 将当前有效国家码写入 ThirdPay 自有存档。
        /// </summary>
        /// <param name="data">当前账号 ThirdPay 存档。</param>
        /// <returns>存档字段发生变化时返回 true。</returns>
        public bool PersistCountryCodesIfChanged(ThirdPayPersistData data)
        {
            if (data == null)
            {
                return false;
            }

            bool changed = false;
            changed |= TryUpdatePersistedCountryCode(ref data.BillingCountryCode, BillingCountryCode);
            changed |= TryUpdatePersistedCountryCode(ref data.IosStorefrontCountryCode, IosStorefrontCountryCode);
            changed |= TryUpdatePersistedCountryCode(ref data.AdCountryCode, AdCountryCode);
            return changed;
        }

        /// <summary>
        /// 归一化 ISO 国家或地区代码，供支付路径匹配使用。
        /// </summary>
        /// <param name="countryCode">原始国家或地区代码。</param>
        /// <returns>归一化后的国家码；无效值返回空字符串。</returns>
        public static string NormalizeCountryCode(string countryCode)
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

            return string.Equals(normalized, c_InvalidCountryCode, StringComparison.Ordinal) ? string.Empty : normalized;
        }

        /// <summary>
        /// 仅在国家码有效且与存档不同的时候更新存档字段。
        /// </summary>
        /// <param name="persistedCountryCode">待更新的存档字段引用。</param>
        /// <param name="countryCode">当前运行时国家码。</param>
        /// <returns>存档字段发生变化时返回 true。</returns>
        private static bool TryUpdatePersistedCountryCode(ref string persistedCountryCode, string countryCode)
        {
            string normalizedCountryCode = NormalizeCountryCode(countryCode);
            if (string.IsNullOrEmpty(normalizedCountryCode)
                || string.Equals(persistedCountryCode, normalizedCountryCode, StringComparison.Ordinal))
            {
                return false;
            }

            persistedCountryCode = normalizedCountryCode;
            return true;
        }
    }
}
