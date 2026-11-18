/***************************************************************
 * (c) copyright 2026 - 2030, Solotopia
 * All Rights Reserved.
 * -------------------------------------------------------------
 * filename:  ThirdPayBrowserMessage.cs
 * author:    yingzheng
 * created:   2026/11/16
 * descrip:   不依赖浏览器插件的 URL 消息解析
 ***************************************************************/

using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;

namespace NovaFramework.SDK.IAP.ThirdPay.Runtime
{
    /// <summary>
    /// 网页消息的结构化值；只负责解码，不判断支付状态或发送者身份。
    /// </summary>
    internal readonly struct ThirdPayBrowserMessage
    {
        public string RawMessage { get; }
        public string Scheme { get; }
        public string Path { get; }
        public IReadOnlyDictionary<string, string> Args { get; }

        /// <summary>
        /// 保存已解析的消息，防止调用方修改参数集合。
        /// </summary>
        private ThirdPayBrowserMessage(string raw, string scheme, string path, Dictionary<string, string> args)
        {
            RawMessage = raw;
            Scheme = scheme;
            Path = path;
            Args = new ReadOnlyDictionary<string, string>(args);
        }

        /// <summary>
        /// 解析 scheme://回调名?参数；重复参数按逗号合并，query 中加号表示空格。
        /// 非法转义或缺少回调名时返回 false，不影响正在等待的支付会话。
        /// </summary>
        public static bool TryParse(string url, out ThirdPayBrowserMessage message)
        {
            message = default;
            if (string.IsNullOrEmpty(url)) return false;
            int separator = url.IndexOf("://", StringComparison.Ordinal);
            if (separator <= 0 || !Uri.CheckSchemeName(url.Substring(0, separator))) return false;
            int fragment = url.IndexOf('#', separator + 3);
            string content = fragment < 0 ? url.Substring(separator + 3) : url.Substring(separator + 3, fragment - separator - 3);
            int query = content.IndexOf('?');
            string rawPath = query < 0 ? content : content.Substring(0, query);
            if (rawPath.Length == 0 || !TryDecode(rawPath, false, out string path)) return false;
            var args = new Dictionary<string, string>(StringComparer.Ordinal);
            if (query >= 0)
            {
                foreach (string part in content.Substring(query + 1).Split('&'))
                {
                    if (part.Length == 0) continue;
                    int equals = part.IndexOf('=');
                    if (!TryDecode(equals < 0 ? part : part.Substring(0, equals), true, out string key)
                        || !TryDecode(equals < 0 ? string.Empty : part.Substring(equals + 1), true, out string value)) return false;
                    args[key] = args.TryGetValue(key, out string previous) ? previous + "," + value : value;
                }
            }
            message = new ThirdPayBrowserMessage(url, url.Substring(0, separator), path, args);
            return true;
        }

        /// <summary>
        /// 先拒绝不完整的百分号编码，再执行单次 URL 解码，避免容错解析误认订单。
        /// </summary>
        private static bool TryDecode(string value, bool query, out string decoded)
        {
            decoded = null;
            for (int i = 0; i < value.Length; i++)
            {
                if (value[i] != '%') continue;
                if (i + 2 >= value.Length || !Uri.IsHexDigit(value[i + 1]) || !Uri.IsHexDigit(value[i + 2])) return false;
                i += 2;
            }
            try
            {
                decoded = Uri.UnescapeDataString(query ? value.Replace('+', ' ') : value);
                return true;
            }
            catch (UriFormatException)
            {
                return false;
            }
        }
    }
}
