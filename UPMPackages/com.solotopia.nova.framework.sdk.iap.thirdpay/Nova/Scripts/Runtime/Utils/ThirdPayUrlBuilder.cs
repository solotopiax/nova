/***************************************************************
 * (c) copyright 2026 - 2030, Solotopia
 * All Rights Reserved.
 * -------------------------------------------------------------
 * filename:  ThirdPayUrlBuilder.cs
 * author:    yingzheng
 * created:   2026/8/3
 * descrip:   第三方支付 URL 构造工具
 ***************************************************************/

using System;
using System.Globalization;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using NovaFramework.Runtime;

namespace NovaFramework.SDK.IAP.ThirdPay.Runtime
{
    /// <summary>
    /// 第三方支付页固定业务参数。
    /// </summary>
    internal sealed class ThirdPayUrlPayload
    {
        /// <summary>
        /// 第三方商品列表中的商品自增 ID。
        /// </summary>
        public int ProductId;

        /// <summary>
        /// 当前用户 UID。
        /// </summary>
        public string UserId = string.Empty;

        /// <summary>
        /// 支付商品表行 ID。
        /// </summary>
        public long TableId;

        /// <summary>
        /// 第三方商品本地货币码。
        /// </summary>
        public string Currency = string.Empty;

        /// <summary>
        /// 第三方商品本地价格。
        /// </summary>
        public string Price = string.Empty;

        /// <summary>
        /// 商品展示名称。
        /// </summary>
        public string ProductName = string.Empty;

        /// <summary>
        /// 国家或地区代码。
        /// </summary>
        public string CountryCode = string.Empty;

        /// <summary>
        /// 客户端订单号。
        /// </summary>
        public string ClientOrderId = string.Empty;

        /// <summary>
        /// 当前构建平台名称。
        /// </summary>
        public string Platform = string.Empty;

        /// <summary>
        /// 公共请求头应用 ID。
        /// </summary>
        public int AppId;

        /// <summary>
        /// CID 等渠道参数。
        /// </summary>
        public string ChannelParams = string.Empty;

        /// <summary>
        /// Google 外部结算上报 token。
        /// </summary>
        public string GoogleToken = string.Empty;

        /// <summary>
        /// 随第三方支付票据往返的业务透传参数。
        /// </summary>
        public string ReceiptParam = string.Empty;
    }

    /// <summary>
    /// 按 Solar InAppAuto 契约构造明文 JSON，交由调用方加密后生成支付 URL。
    /// </summary>
    internal sealed class ThirdPayUrlBuilder
    {
        /// <summary>
        /// 业务参数加密函数。
        /// </summary>
        private readonly Func<string, string> m_Encrypt;

        /// <summary>
        /// 初始化第三方支付 URL 构造器。
        /// </summary>
        /// <param name="encrypt">业务参数加密函数。</param>
        public ThirdPayUrlBuilder(Func<string, string> encrypt)
        {
            m_Encrypt = encrypt ?? throw new ArgumentNullException(nameof(encrypt));
        }

        /// <summary>
        /// 构造并加密第三方支付 URL。
        /// </summary>
        /// <param name="baseUrl">第三方支付页 URL 基址。</param>
        /// <param name="language">当前语言 locale 标识。</param>
        /// <param name="payload">固定业务参数。</param>
        /// <returns>包含 lang、params 和 app_id 的完整 URL。</returns>
        public string Build(string baseUrl, string language, ThirdPayUrlPayload payload)
        {
            if (string.IsNullOrEmpty(baseUrl))
            {
                throw new ArgumentException("支付 URL 不能为空。", nameof(baseUrl));
            }

            if (payload == null)
            {
                throw new ArgumentNullException(nameof(payload));
            }

            string customParam = new JObject { { "receipt_param", payload.ReceiptParam ?? string.Empty } }.ToString(Formatting.None);
            var parameters = new JObject
            {
                { "id", payload.ProductId },
                { "uid", payload.UserId ?? string.Empty },
                { "table_id", payload.TableId },
                { "currency", payload.Currency ?? string.Empty },
                { "price", payload.Price ?? string.Empty },
                { "product_name", payload.ProductName ?? string.Empty },
                { "country", payload.CountryCode ?? string.Empty },
                { "order_id", payload.ClientOrderId ?? string.Empty },
                { "platform", payload.Platform ?? string.Empty },
                { "is_external_browser", false },
                { "custom_param", customParam },
            };
            if (!string.IsNullOrEmpty(payload.ChannelParams))
            {
                parameters.Add("payment_customer_ids", payload.ChannelParams);
            }

            parameters.Add("google_transaction_token", payload.GoogleToken ?? string.Empty);
            string plaintext = parameters.ToString(Formatting.None);
            string encrypted = m_Encrypt(plaintext);
            if (string.IsNullOrEmpty(encrypted))
            {
                throw new InvalidOperationException("支付参数加密结果为空。");
            }

            string paymentUrl = $"{baseUrl.TrimEnd('/')}/?lang={Escape(language)}&params={Escape(encrypted)}&app_id={payload.AppId.ToString(CultureInfo.InvariantCulture)}";
            // 支付调试日志用于核对跳转前的明文、密文和最终 URL。
            Log.Debug(LogTag.IAPThirdPay, $"第三方支付跳转参数：加密前={plaintext}；加密后={encrypted}；URL={paymentUrl}");
            return paymentUrl;
        }

        /// <summary>
        /// 对 URL 参数值执行转义，空值按空字符串处理。
        /// </summary>
        /// <param name="value">待转义字符串。</param>
        /// <returns>转义后的 URL 参数值。</returns>
        private static string Escape(string value)
        {
            return Uri.EscapeDataString(value ?? string.Empty);
        }
    }
}
