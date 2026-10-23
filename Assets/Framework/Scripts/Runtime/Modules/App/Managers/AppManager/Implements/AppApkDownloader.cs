/***************************************************************
 * (c) copyright 2026 - 2030, Solotopia
 * All Rights Reserved.
 * -------------------------------------------------------------
 * filename:  AppApkDownloader.cs
 * author:    taoye
 * created:   2026/10/13
 * descrip:   APK 文件流下载、受控临时文件与原子提交
 ***************************************************************/

using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Threading;
using Cysharp.Threading.Tasks;
using UnityEngine;
using UnityEngine.Networking;
using IoPath = System.IO.Path;

namespace NovaFramework.Runtime
{
    /// <summary>
    /// App 模块内部的单操作文件下载器；只生成本地 APK 归档，不处理签名、版本身份或安装。
    /// </summary>
    internal sealed class AppApkDownloader
    {
        /// <summary>首版单个 APK 文件上限为 1 GiB，禁止把无界网络内容写满本地目录。</summary>
        internal const long MaximumBytes = 1024L * 1024 * 1024;
        /// <summary>连续 60 秒没有收到新字节时中止当前候选。</summary>
        private const float IdleTimeoutSeconds = 60f;
        /// <summary>单个候选含重定向的完整时间上限为 30 分钟。</summary>
        private const float TotalTimeoutSeconds = 1800f;
        /// <summary>最多跟随五次 HTTPS 重定向，不允许协议降级。</summary>
        private const int MaximumRedirects = 5;
        private readonly Func<string, string, CancellationToken, UniTask> m_Transfer;
        private CancellationTokenSource m_Active;

        /// <summary>生产入口固定使用受控 HTTPS 文件流下载，不从用户请求注入执行器。</summary>
        internal AppApkDownloader() : this(TransferAsync) { }

        /// <summary>内部测试替身只替换文件传输；路径、大小、归档、取消及原子提交仍走真实实现。</summary>
        /// <param name="transfer">向本次唯一临时路径写入文件的传输委托。</param>
        internal AppApkDownloader(Func<string, string, CancellationToken, UniTask> transfer)
        {
            m_Transfer = transfer ?? throw new ArgumentNullException(nameof(transfer));
        }

        /// <summary>冻结主备 HTTPS 地址并按完整规范地址去重；不接受凭据、片段或明文降级。</summary>
        /// <param name="primary">当前命中规则暴露的主下载地址，必须存在。</param>
        /// <param name="fallback">配置中的备用地址；空白表示未配置。</param>
        /// <returns>主地址在前、备用地址在后的独立数组。</returns>
        internal static string[] BuildCandidates(string primary, string fallback)
        {
            var result = new List<string> { HttpsUri(primary).AbsoluteUri };
            if (!string.IsNullOrWhiteSpace(fallback))
            {
                string alternate = HttpsUri(fallback).AbsoluteUri;
                if (!result.Contains(alternate, StringComparer.Ordinal)) result.Add(alternate);
            }
            return result.ToArray();
        }

        /// <summary>取消未提交操作；令牌在最终原子移动前再次核对，关闭不能提交迟到文件。</summary>
        internal void Cancel() => m_Active?.Cancel();

        /// <summary>
        /// 按冻结候选各下载一次；仅网络/响应或归档不合格推进备用，文件提交失败不重试。
        /// 同一实例并发拒绝；临时件在成功、失败和取消后清除，旧正式文件仅在合格提交时替换。
        /// </summary>
        /// <param name="candidates">已冻结的一个主地址及可选备用 HTTPS 地址。</param>
        /// <param name="destination">调用方提供的正式绝对文件路径；所有祖先及文件不得为符号链接。</param>
        /// <param name="ct">取消当前下载及备用推进；取消抛 OperationCanceledException。</param>
        /// <returns>已原子提交的本地文件绝对路径。</returns>
        internal async UniTask<string> DownloadAsync(string[] candidates, string destination, CancellationToken ct)
        {
            ct.ThrowIfCancellationRequested();
            if (m_Active != null) throw new InvalidOperationException("已有 APK 文件下载正在执行。");
            if (candidates == null || candidates.Length == 0 || candidates.Length > 2)
                throw new InvalidOperationException("APK 下载候选必须为明确的主地址及可选备用地址。");
            string[] frozen = candidates.Select(candidate => HttpsUri(candidate).AbsoluteUri).Distinct(StringComparer.Ordinal).ToArray();
            string target = ValidateDestination(destination);
            var operation = CancellationTokenSource.CreateLinkedTokenSource(ct);
            m_Active = operation;
            try
            {
                foreach (string candidate in frozen)
                {
                    operation.Token.ThrowIfCancellationRequested();
                    string temporary = target + ".nova-" + Guid.NewGuid().ToString("N") + ".part";
                    try
                    {
                        using (new FileStream(temporary, FileMode.CreateNew, FileAccess.Write, FileShare.None)) { }
                        try
                        {
                            await m_Transfer(candidate, temporary, operation.Token);
                            operation.Token.ThrowIfCancellationRequested();
                            ValidateArchive(temporary);
                        }
                        catch (InvalidDataException)
                        {
                            operation.Token.ThrowIfCancellationRequested();
                            continue;
                        }
                        operation.Token.ThrowIfCancellationRequested();
                        ValidateDestination(target);
                        // 同目录 rename/replace 保证正式路径不会暴露下载中的半文件；失败不先删除旧文件。
                        if (File.Exists(target)) File.Replace(temporary, target, null);
                        else File.Move(temporary, target);
                        return target;
                    }
                    finally
                    {
                        if (File.Exists(temporary)) File.Delete(temporary);
                    }
                }
                throw new InvalidDataException("APK 主备下载均未取得合格文件；此前正式文件保持不变。");
            }
            finally
            {
                if (ReferenceEquals(m_Active, operation)) m_Active = null;
                operation.Dispose();
            }
        }

        /// <summary>仅接受绝对 HTTPS 地址；标准证书校验由 UnityWebRequest 保留，不提供绕过选项。</summary>
        /// <param name="url">当前配置地址或重定向解析后的完整地址。</param>
        /// <returns>合法 HTTPS Uri。</returns>
        private static Uri HttpsUri(string url)
        {
            if (!Uri.TryCreate(url, UriKind.Absolute, out Uri uri) || uri.Scheme != Uri.UriSchemeHttps ||
                string.IsNullOrEmpty(uri.Host) || !string.IsNullOrEmpty(uri.UserInfo) || !string.IsNullOrEmpty(uri.Fragment))
                throw new InvalidOperationException("APK 下载只允许无内嵌凭据和片段的绝对 HTTPS 地址。");
            return uri;
        }

        /// <summary>只向明确绝对文件路径写入，创建目录前后都拒绝祖先或目标文件符号链接。</summary>
        /// <param name="destination">正式文件绝对路径。</param>
        /// <returns>规范绝对路径。</returns>
        private static string ValidateDestination(string destination)
        {
            if (string.IsNullOrWhiteSpace(destination) || !IoPath.IsPathRooted(destination))
                throw new InvalidOperationException("APK 下载需要明确绝对文件路径。");
            string full = IoPath.GetFullPath(destination);
            string parent = IoPath.GetDirectoryName(full);
            if (string.IsNullOrEmpty(parent)) throw new InvalidOperationException("APK 下载目标必须为文件路径。");
            for (DirectoryInfo item = new DirectoryInfo(parent); item != null; item = item.Parent)
                if (item.Exists && (item.Attributes & FileAttributes.ReparsePoint) != 0)
                    throw new IOException("APK 下载目录不能包含符号链接。");
            Directory.CreateDirectory(parent);
            if (Directory.Exists(full) || (File.Exists(full) && (File.GetAttributes(full) & FileAttributes.ReparsePoint) != 0))
                throw new IOException("APK 正式路径不能是目录或符号链接。");
            return full;
        }

        /// <summary>核对非空、1 GiB 上限及唯一非空 AndroidManifest.xml；不提取归档，不把结构检查当签名或版本验证。</summary>
        /// <param name="file">本次已关闭写入的临时文件。</param>
        private static void ValidateArchive(string file)
        {
            long length = new FileInfo(file).Length;
            if (length <= 0 || length > MaximumBytes) throw new InvalidDataException("APK 文件为空或超过大小上限。");
            using var stream = new FileStream(file, FileMode.Open, FileAccess.Read, FileShare.Read);
            using var archive = new ZipArchive(stream, ZipArchiveMode.Read);
            if (archive.Entries.Count > 100000 || archive.Entries.Count(entry => entry.FullName == "AndroidManifest.xml") != 1 ||
                archive.GetEntry("AndroidManifest.xml").Length <= 0)
                throw new InvalidDataException("下载内容不是受支持的 APK 归档结构。");
        }

        /// <summary>将 HTTPS 响应直接写入临时文件；手动 HTTPS 重定向、空闲/总超时和长度校验均不创建正文 buffer。</summary>
        /// <param name="url">已冻结当前候选。</param>
        /// <param name="file">唯一受控临时文件。</param>
        /// <param name="ct">下载及最终验证取消令牌。</param>
        private static async UniTask TransferAsync(string url, string file, CancellationToken ct)
        {
            Uri current = HttpsUri(url);
            float started = Time.realtimeSinceStartup;
            for (int redirect = 0; redirect <= MaximumRedirects; redirect++)
            {
                ct.ThrowIfCancellationRequested();
                using var request = new UnityWebRequest(current.AbsoluteUri, UnityWebRequest.kHttpVerbGET);
                request.downloadHandler = new DownloadHandlerFile(file) { removeFileOnAbort = true };
                request.redirectLimit = 0;
                request.SetRequestHeader("Cache-Control", "no-cache");
                request.SetRequestHeader("Accept-Encoding", "identity");
                float lastProgress = Time.realtimeSinceStartup;
                ulong previous = 0;
                try
                {
                    UnityWebRequestAsyncOperation pending = request.SendWebRequest();
                    while (!pending.isDone)
                    {
                        ct.ThrowIfCancellationRequested();
                        if (request.downloadedBytes != previous)
                        { previous = request.downloadedBytes; lastProgress = Time.realtimeSinceStartup; }
                        if (request.downloadedBytes > (ulong)MaximumBytes || ReadContentLength(request) > MaximumBytes)
                            throw new InvalidDataException("APK 响应超过大小上限。");
                        float now = Time.realtimeSinceStartup;
                        if (now - lastProgress > IdleTimeoutSeconds || now - started > TotalTimeoutSeconds)
                            throw new InvalidDataException("APK 下载超时。");
                        await UniTask.Yield(PlayerLoopTiming.Update);
                    }
                    ct.ThrowIfCancellationRequested();
                    if (request.responseCode == 301 || request.responseCode == 302 || request.responseCode == 303 ||
                        request.responseCode == 307 || request.responseCode == 308)
                    {
                        if (redirect == MaximumRedirects || !Uri.TryCreate(current, request.GetResponseHeader("Location"), out Uri next))
                            throw new InvalidDataException("APK 重定向缺失或超过次数上限。");
                        try { current = HttpsUri(next.AbsoluteUri); }
                        catch (InvalidOperationException) { throw new InvalidDataException("APK 重定向不符合 HTTPS 规则。"); }
                        continue;
                    }
                    if (request.result == UnityWebRequest.Result.DataProcessingError)
                        throw new IOException("APK 文件写入处理失败；不对本地文件故障推进备用地址。");
                    if (request.result != UnityWebRequest.Result.Success || request.responseCode != 200)
                        throw new InvalidDataException("APK 下载未取得完整 HTTP 200 响应。");
                    string encoding = request.GetResponseHeader("Content-Encoding");
                    if (!string.IsNullOrEmpty(encoding) && !encoding.Equals("identity", StringComparison.OrdinalIgnoreCase))
                        throw new InvalidDataException("APK 下载不接受额外内容编码。");
                    long expected = ReadContentLength(request);
                    long observed = new FileInfo(file).Length;
                    if (observed <= 0 || observed > MaximumBytes || (expected >= 0 && expected != observed))
                        throw new InvalidDataException("APK 响应长度不完整或不符合大小限制。");
                    return;
                }
                catch
                {
                    request.Abort();
                    throw;
                }
            }
        }

        /// <summary>读取可选 Content-Length；未声明时只靠流大小与归档结构，非法声明不能视为未知长度。</summary>
        /// <param name="request">当前文件下载请求。</param>
        /// <returns>声明长度，缺失返回 -1。</returns>
        private static long ReadContentLength(UnityWebRequest request)
        {
            string value = request.GetResponseHeader("Content-Length");
            if (string.IsNullOrEmpty(value)) return -1;
            if (!long.TryParse(value, System.Globalization.NumberStyles.None, System.Globalization.CultureInfo.InvariantCulture, out long length))
                throw new InvalidDataException("APK 响应长度声明无效。");
            return length;
        }
    }
}
