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
using System.Security.Cryptography;
using System.Text;
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

        /// <summary>
        /// 最终 ThirdPay 支付页是否由系统外部浏览器打开。
        /// </summary>
        public bool IsExternalBrowser;

        /// <summary>
        /// 支付页是否显示返回键。
        /// </summary>
        public bool ShowBackButton;
    }

    /// <summary>
    /// 按 Solar InAppAuto 契约构造明文 JSON，交由调用方加密后生成支付 URL。
    /// </summary>
    internal sealed class ThirdPayUrlBuilder : ThirdPayLogOwner
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
                { "is_external_browser", payload.IsExternalBrowser },
                { "show_back_button", payload.ShowBackButton },
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
            // 支付审计日志用于核对跳转前的明文、密文和最终 URL。
            LogDebug($"第三方支付跳转参数：加密前={plaintext}");
            LogDebug($"第三方支付跳转参数：加密后={encrypted}");
            LogDebug($"第三方支付跳转参数：URL={paymentUrl}");
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

    /// <summary>
    /// ThirdPay 支付 URL 参数动态 AES 加密工具。
    /// </summary>
    internal static class ThirdPayDynamicAesEncryptor
    {
        /// <summary>
        /// AES Key / IV 固定长度：16 字节。
        /// </summary>
        private const int c_SecretBytesLength = 16;

        /// <summary>
        /// 生成可作为 UTF-8 单字节字符传给 Util.Encrypt.AES 的随机范围。
        /// </summary>
        private const int c_RandomAsciiMin = 33;

        /// <summary>
        /// 生成可作为 UTF-8 单字节字符传给 Util.Encrypt.AES 的随机范围。
        /// </summary>
        private const int c_RandomAsciiMaxExclusive = 127;

        /// <summary>
        /// 将 ThirdPay 支付参数 AES 加密为 Base64 文本。
        /// </summary>
        /// <param name="content">明文字符串。</param>
        /// <returns>key + iv + cipher 的整体 Base64。</returns>
        public static string EncodeToBase64(string content)
        {
            byte[] keyArray = GetRandomSecretBytes();
            byte[] ivArray = GetRandomSecretBytes();
            string key = Encoding.ASCII.GetString(keyArray);
            string iv = Encoding.ASCII.GetString(ivArray);
            byte[] resultArray = Util.Encrypt.AES.EncryptBytes(Encoding.UTF8.GetBytes(content ?? string.Empty), key, iv);

            int prefixLength = c_SecretBytesLength * 2;
            byte[] output = new byte[prefixLength + resultArray.Length];
            int offset = 0;
            Buffer.BlockCopy(keyArray, 0, output, offset, keyArray.Length);
            offset += keyArray.Length;
            Buffer.BlockCopy(ivArray, 0, output, offset, ivArray.Length);
            offset += ivArray.Length;
            Buffer.BlockCopy(resultArray, 0, output, offset, resultArray.Length);
            return Convert.ToBase64String(output);
        }

        /// <summary>
        /// 获取随机密钥 16 字节。
        /// </summary>
        /// <returns>随机 Key 或 IV 字节数组。</returns>
        public static byte[] GetRandomSecretBytes()
        {
            byte[] bytes = new byte[c_SecretBytesLength];
            byte[] randomBytes = new byte[c_SecretBytesLength];
            using (RandomNumberGenerator rng = RandomNumberGenerator.Create())
            {
                rng.GetBytes(randomBytes);
            }

            for (int i = 0; i < bytes.Length; i++)
            {
                bytes[i] = (byte)(c_RandomAsciiMin + randomBytes[i] % (c_RandomAsciiMaxExclusive - c_RandomAsciiMin));
            }

            return bytes;
        }

        /// <summary>
        /// 获取随机密钥 16 字符。
        /// </summary>
        /// <returns>随机 Key 或 IV 字符串。</returns>
        public static string GetRandomSecretString()
        {
            return Encoding.ASCII.GetString(GetRandomSecretBytes());
        }
    }
}
