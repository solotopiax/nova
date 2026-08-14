/***************************************************************
 * (c) copyright 2026 - 2030, Solotopia
 * All Rights Reserved.
 * -------------------------------------------------------------
 * filename:  DoHClient.cs
 * author:    taoye
 * created:   2026/3/9
 * descrip:   DoH查询器
 ***************************************************************/

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Security.Cryptography;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Cysharp.Threading.Tasks;
using Newtonsoft.Json.Linq;

namespace NovaFramework.Runtime
{
    /// <summary>
    /// 针对单个主机名的 DoH 查询器，内置结果缓存与多端点轮询。
    /// </summary>
    public class DoHClient : IDisposable
    {
        /// <summary>
        /// DNS-over-HTTPS JSON 内容类型。
        /// </summary>
        private const string c_JsonContentType = "application/dns-json";

        /// <summary>
        /// DNS 错误码说明表。
        /// </summary>
        private static readonly Dictionary<int, string> s_DNSCodes = new Dictionary<int, string>
        {
            { 1, "Format Error" },
            { 2, "Server Failure" },
            { 3, "Non-Existent Domain" },
            { 4, "Not Implemented" },
            { 5, "Query Refused" },
            { 6, "Name Exists when it should not" },
            { 7, "RR Set Exists when it should not" },
            { 8, "RR Set that should exist does not" },
            { 9, "Server Not Authoritative for zone" },
            { 10, "Name not contained in zone" },
            { 16, "Bad OPT Version / TSIG Signature Failure" },
            { 17, "Key not recognized" },
            { 18, "Signature out of time window" },
            { 19, "Bad TKEY Mode" },
            { 20, "Duplicate key name" },
            { 21, "Algorithm not supported" },
            { 22, "Bad Truncation" },
            { 23, "Bad/missing Server Cookie" }
        };

        /// <summary>
        /// 查询端点列表（按顺序尝试，首个成功即返回）。
        /// </summary>
        private static readonly string[] s_EndpointsList =
        {
            DNSAddress.Cloudflare.IPv4.c_Primary,
            DNSAddress.Cloudflare.IPv4.c_Secondary,
            DNSAddress.Cloudflare.c_URL
        };

        /// <summary>
        /// 填充随机数生成器（使用加密安全种子）。
        /// </summary>
        private readonly Random m_Random;

        /// <summary>
        /// 释放查询器时用于中止仍在等待的网络请求，避免无限等待阻塞关闭流程。
        /// </summary>
        private readonly CancellationTokenSource m_DisposeCancellationTokenSource;

        /// <summary>
        /// 查询器是否已经释放。
        /// </summary>
        private bool m_Disposed;

        /// <summary>
        /// 按记录类型隔离的 DNS 结果缓存，避免不同查询类型互相覆盖。
        /// </summary>
        private readonly Dictionary<ResourceRecordType, DNSCacheEntry> m_AnswersCaches;

        /// <summary>
        /// 本查询器对应的主机名。
        /// </summary>
        private readonly string m_HostName;

        /// <summary>
        /// 按记录类型隔离的进行中查询，同一主机的相同类型只发起一次请求链。
        /// </summary>
        private readonly Dictionary<ResourceRecordType, UniTask<DNSAnswer[]>> m_WaitingTasks;

        /// <summary>
        /// 构造 DoHClient 实例。
        /// </summary>
        /// <param name="hostName">待查询的主机名。</param>
        public DoHClient(string hostName)
        {
            m_HostName = hostName;
            m_Random   = GenerateCryptoSeededRandom();
            m_DisposeCancellationTokenSource = new CancellationTokenSource();
            m_AnswersCaches = new Dictionary<ResourceRecordType, DNSCacheEntry>();
            m_WaitingTasks = new Dictionary<ResourceRecordType, UniTask<DNSAnswer[]>>();
        }

        /// <summary>
        /// 清除本地 DNS 结果缓存，强制下次重新查询。
        /// </summary>
        public void ClearCache()
        {
            m_AnswersCaches.Clear();
        }

        /// <summary>
        /// 使用兼容入口查询 A 记录；整次查询共享由 timeout 创建的截止时间。
        /// </summary>
        /// <param name="timeout">查询超时时间（毫秒）；小于等于 0 时无限等待。</param>
        /// <returns>DNS 应答数组，所有端点均失败时返回 null。</returns>
        public UniTask<DNSAnswer[]> QueryAsync(int timeout)
        {
            return QueryAsync(ResourceRecordType.A, CreateQueryDeadlineUtc(timeout));
        }

        /// <summary>
        /// 按记录类型查询 DNS；缓存、进行中任务和端点轮询均按记录类型隔离。
        /// </summary>
        /// <param name="recordType">要查询的 DNS 记录类型。</param>
        /// <param name="deadlineUtc">原始域名完整解析链的 UTC 截止时间；null 表示无限等待。</param>
        /// <returns>DNS 应答数组，截止时间耗尽或所有端点均失败时返回 null。</returns>
        internal async UniTask<DNSAnswer[]> QueryAsync(ResourceRecordType recordType, DateTime? deadlineUtc)
        {
            if (m_Disposed)
            {
                return null;
            }

            if (m_AnswersCaches.TryGetValue(recordType, out DNSCacheEntry answersCache))
            {
                if (answersCache.ExpireTime <= DateTime.Now)
                {
                    m_AnswersCaches.Remove(recordType);
                }
                else
                {
                    return answersCache.Answers;
                }
            }

            if (m_WaitingTasks.TryGetValue(recordType, out UniTask<DNSAnswer[]> waitingTask) &&
                waitingTask.Status == UniTaskStatus.Pending)
            {
                return await AwaitSharedQueryAsync(waitingTask, recordType, deadlineUtc);
            }

            UniTaskCompletionSource<DNSAnswer[]> tcs = new UniTaskCompletionSource<DNSAnswer[]>();
            m_WaitingTasks[recordType] = tcs.Task;
            DNSAnswer[] finalAnswers = null;

            try
            {
                foreach (string endpoint in s_EndpointsList)
                {
                    if (m_Disposed)
                    {
                        break;
                    }

                    if (GetRemainingTimeoutMilliseconds(deadlineUtc) == 0)
                    {
                        PrintWarning(endpoint, $"{recordType} 查询已达到当前域名完整解析链的截止时间。");
                        break;
                    }

                    DNSAnswer[] answers = await DoQuery(endpoint, recordType, deadlineUtc);
                    if (answers == null)
                    {
                        continue;
                    }

                    if (answers.Length > 0)
                    {
                        DNSCacheEntry cacheEntry = new DNSCacheEntry(answers);
                        m_AnswersCaches[recordType] = cacheEntry;
                    }

                    // NOERROR 的空 Answer 是合法结果，不能误判为当前 DoH 端点失败。
                    finalAnswers = answers;
                    break;
                }
            }
            catch (Exception e)
            {
                PrintWarning("DoH", $"查询过程异常：{e.Message}");
            }
            finally
            {
                m_WaitingTasks.Remove(recordType);
                tcs.TrySetResult(finalAnswers);
            }

            return finalAnswers;
        }

        /// <summary>
        /// 在当前调用方的解析链截止时间内等待已有的同类型查询，不延长当前链路的等待时间。
        /// </summary>
        /// <param name="waitingTask">同一主机、同一记录类型的进行中查询。</param>
        /// <param name="recordType">正在等待的记录类型。</param>
        /// <param name="deadlineUtc">当前原始域名解析链的 UTC 截止时间；null 表示无限等待。</param>
        /// <returns>共享查询结果；当前链路先到期时返回 null，且不终止共享查询。</returns>
        private async UniTask<DNSAnswer[]> AwaitSharedQueryAsync(
            UniTask<DNSAnswer[]> waitingTask,
            ResourceRecordType recordType,
            DateTime? deadlineUtc)
        {
            int remainingTimeout = GetRemainingTimeoutMilliseconds(deadlineUtc);
            if (remainingTimeout == 0)
            {
                return null;
            }

            try
            {
                return remainingTimeout == System.Threading.Timeout.Infinite
                    ? await waitingTask
                    : await waitingTask.Timeout(TimeSpan.FromMilliseconds(remainingTimeout), DelayType.Realtime);
            }
            catch (TimeoutException)
            {
                PrintWarning("DoH", $"等待共享的 {recordType} 查询时，当前域名完整解析链已超时。");
                return null;
            }
        }

        /// <summary>
        /// 释放查询器，并中止仍在等待的网络请求。
        /// </summary>
        public void Dispose()
        {
            if (m_Disposed)
            {
                return;
            }

            m_Disposed = true;
            m_DisposeCancellationTokenSource.Cancel();
            m_DisposeCancellationTokenSource.Dispose();
        }

        /// <summary>
        /// 向指定端点发送实际的 DoH 查询请求并解析 JSON 响应。
        /// </summary>
        /// <param name="endpoint">DoH 端点 URL。</param>
        /// <param name="recordType">要查询的 DNS 记录类型。</param>
        /// <param name="deadlineUtc">原始域名完整解析链的 UTC 截止时间；null 表示无限等待。</param>
        /// <returns>解析出的 DNS 应答数组，失败时返回 null。</returns>
        private async UniTask<DNSAnswer[]> DoQuery(
            string endpoint,
            ResourceRecordType recordType,
            DateTime? deadlineUtc)
        {
            HttpWebRequest request = null;
            try
            {
                int remainingTimeout = GetRemainingTimeoutMilliseconds(deadlineUtc);
                if (remainingTimeout == 0)
                {
                    return null;
                }

                request = CreateRequest(endpoint, remainingTimeout, recordType);
                using CancellationTokenRegistration cancellationRegistration =
                    m_DisposeCancellationTokenSource.Token.Register(request.Abort);
                UniTask<WebResponse> responseTask = request.GetResponseAsync().AsUniTask();
                WebResponse webResponse = remainingTimeout == System.Threading.Timeout.Infinite
                    ? await responseTask
                    : await responseTask.Timeout(TimeSpan.FromMilliseconds(remainingTimeout), DelayType.Realtime);
                using HttpWebResponse response = (HttpWebResponse)webResponse;
                if (response.StatusCode != HttpStatusCode.OK)
                {
                    PrintWarning(endpoint, $"状态码错误：{(int)response.StatusCode} {response.StatusDescription}。");
                    return null;
                }

                using Stream rs = response.GetResponseStream();
                if (rs != null)
                {
                    remainingTimeout = GetRemainingTimeoutMilliseconds(deadlineUtc);
                    if (remainingTimeout == 0)
                    {
                        request.Abort();
                        return null;
                    }

                    using StreamReader reader = new StreamReader(rs, Encoding.UTF8);
                    UniTask<string> readTask = reader.ReadToEndAsync().AsUniTask();
                    string content = remainingTimeout == System.Threading.Timeout.Infinite
                        ? await readTask
                        : await readTask.Timeout(TimeSpan.FromMilliseconds(remainingTimeout), DelayType.Realtime);
                    return HandleJSONResponse(endpoint, content);
                }
            }
            catch (TimeoutException)
            {
                request?.Abort();
                PrintWarning(endpoint, "查询超时，已中止当前请求。");
            }
            catch (Exception e)
            {
                if (!m_Disposed)
                {
                    PrintWarning(endpoint, e.Message);
                }
            }

            return null;
        }

        /// <summary>
        /// 构建 DoH 查询的 HttpWebRequest 对象。
        /// </summary>
        /// <param name="url">端点基础 URL。</param>
        /// <param name="timeout">当前候选地址可用的剩余超时时间（毫秒）；Timeout.Infinite 表示无限等待。</param>
        /// <param name="recordType">要查询的 DNS 记录类型。</param>
        /// <returns>配置好的 HttpWebRequest 实例。</returns>
        private HttpWebRequest CreateRequest(string url, int timeout, ResourceRecordType recordType)
        {
            ServicePointManager.SecurityProtocol |= SecurityProtocolType.Tls12;
            string uri = GenerateQueryUrl(url, recordType);
            HttpWebRequest request = WebRequest.CreateHttp(uri);
            request.Method = "GET";
            request.Accept = c_JsonContentType;
            if (timeout == System.Threading.Timeout.Infinite)
            {
                request.Timeout = System.Threading.Timeout.Infinite;
                request.ReadWriteTimeout = System.Threading.Timeout.Infinite;
            }
            else if (timeout > 0)
            {
                request.Timeout = timeout;
            }

            return request;
        }

        /// <summary>
        /// 解析 DoH 响应 JSON，提取 DNS 应答数组。
        /// </summary>
        /// <param name="url">来源端点 URL（仅用于错误日志）。</param>
        /// <param name="content">响应体 JSON 字符串。</param>
        /// <returns>解析出的 DNS 应答数组，解析失败时返回 null。</returns>
        private DNSAnswer[] HandleJSONResponse(string url, string content)
        {
            try
            {
                JObject json = JObject.Parse(content);
                int status = Convert.ToInt32(json["Status"].ToString());
                if (status != 0)
                {
                    string comment = json.ContainsKey("Comment") ? json["Comment"].ToString() : string.Empty;
                    PrintWarning(url, $"DNS RCode 错误，code：{status}，comment：{comment}。");
                    return null;
                }

                JArray answers = (JArray)json["Answer"];
                DNSAnswer[] dnsAnswers = new DNSAnswer[answers?.Count ?? 0];
                if (answers != null)
                {
                    int index = 0;
                    foreach (JObject data in answers)
                    {
                        dnsAnswers[index++] = DNSAnswer.FromJSON(data);
                    }
                }

                return dnsAnswers;
            }
            catch (Exception e)
            {
                PrintWarning(url, $"JSON 解析失败：{e.Message}，内容：{content}。");
                return null;
            }
        }

        /// <summary>
        /// 生成完整的 DoH 查询 URL（含 name/type/ct/cd 参数与随机填充）。
        /// </summary>
        /// <param name="url">端点基础 URL。</param>
        /// <param name="recordType">要查询的 DNS 记录类型。</param>
        /// <returns>完整的查询 URL 字符串。</returns>
        private string GenerateQueryUrl(string url, ResourceRecordType recordType)
        {
            Dictionary<string, string> fields = new Dictionary<string, string>
            {
                { "name", m_HostName },
                { "type", recordType.ToString() },
                { "ct", c_JsonContentType },
                { "cd", "false" }
            };

            const int padToLength = 250;
            string uri = $"{url}?{string.Join("&", fields.Select(f => f.Key + "=" + f.Value))}";
            if (uri.Length - 16 < padToLength)
            {
                uri += $"&random_padding={GeneratePadding(padToLength - uri.Length - 16)}";
            }

            return uri;
        }

        /// <summary>
        /// 生成指定长度的随机字符串填充（URL 安全字符集）。
        /// </summary>
        /// <param name="paddingLength">填充长度，若小于等于 0 则返回空字符串。</param>
        /// <returns>随机填充字符串。</returns>
        private string GeneratePadding(int paddingLength)
        {
            if (paddingLength <= 0)
            {
                return string.Empty;
            }

            const string chars = "abcdefghijklmnopqrstuvwxyzABCDEFGHIJKLMNOPQRSTUVWXYZ0123456789-._~";
            StringBuilder sb = new StringBuilder(paddingLength);
            for (int i = 0; i < paddingLength; i++)
            {
                sb.Append(chars[m_Random.Next(chars.Length)]);
            }

            return sb.ToString();
        }

        /// <summary>
        /// 记录可恢复的 DoH 查询告警。
        /// </summary>
        /// <param name="dnsUrl">产生告警的 DoH 端点 URL。</param>
        /// <param name="message">告警描述信息。</param>
        private void PrintWarning(string dnsUrl, string message)
        {
            Log.Warning(LogTag.DoH, "DoH 查询失败，dns：{0}，host：{1}，message：{2}。", dnsUrl, m_HostName, message);
        }

        /// <summary>
        /// 计算共享截止时间的剩余毫秒数。
        /// </summary>
        /// <param name="deadlineUtc">UTC 截止时间；null 表示无限等待。</param>
        /// <returns>剩余毫秒数；无限等待返回 Timeout.Infinite，截止时间已到时返回 0。</returns>
        private static int GetRemainingTimeoutMilliseconds(DateTime? deadlineUtc)
        {
            if (!deadlineUtc.HasValue)
            {
                return System.Threading.Timeout.Infinite;
            }

            double remainingMilliseconds = (deadlineUtc.Value - DateTime.UtcNow).TotalMilliseconds;
            if (remainingMilliseconds <= 0)
            {
                return 0;
            }

            return (int)Math.Min(int.MaxValue, Math.Ceiling(remainingMilliseconds));
        }

        /// <summary>
        /// 根据兼容入口的超时毫秒数创建一次绝对截止时间。
        /// </summary>
        /// <param name="timeout">超时时间（毫秒）；小于等于 0 表示无限等待。</param>
        /// <returns>UTC 截止时间；无限等待时返回 null。</returns>
        private static DateTime? CreateQueryDeadlineUtc(int timeout)
        {
            return timeout > 0 ? DateTime.UtcNow.AddMilliseconds(timeout) : (DateTime?)null;
        }

        /// <summary>
        /// 使用加密安全随机数生成器生成带随机种子的 Random 实例。
        /// </summary>
        /// <returns>已播种的 Random 实例。</returns>
        private static Random GenerateCryptoSeededRandom()
        {
            byte[] seed = new byte[4];
            using RandomNumberGenerator rng = RandomNumberGenerator.Create();
            rng.GetBytes(seed);
            return new Random(BitConverter.ToInt32(seed, 0));
        }
    }
}
