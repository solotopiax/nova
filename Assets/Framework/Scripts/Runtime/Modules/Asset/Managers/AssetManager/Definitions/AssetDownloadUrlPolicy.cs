/***************************************************************
 * (c) copyright 2026 - 2030, Solotopia
 * All Rights Reserved.
 * -------------------------------------------------------------
 * filename:  AssetDownloadUrlPolicy.cs
 * author:    taoye
 * created:   2026/8/5
 * descrip:   Asset 远端候选 URL 轮换策略
 ***************************************************************/

using System;
using System.Collections.Generic;
using YooAsset;

namespace NovaFramework.Runtime
{
    /// <summary>
    /// YooAsset 远端候选 URL 轮换策略。
    /// 传输失败与上层内容校验失败统一按一次失败推进，成功后粘滞当前候选。
    /// </summary>
    internal sealed class AssetDownloadUrlPolicy : IDownloadUrlPolicy
    {
        private readonly bool m_EnableWhitelistMetadataDebugLog;
        private readonly List<string> m_ActiveMetadataUrls = new();
        private readonly HashSet<string> m_TransportFailedMetadataUrls = new(StringComparer.Ordinal);
        private int m_Cursor;
        private long m_FailureGeneration;

        public AssetDownloadUrlPolicy() : this(false)
        {
        }

        internal AssetDownloadUrlPolicy(bool enableWhitelistMetadataDebugLog)
        {
            m_EnableWhitelistMetadataDebugLog = enableWhitelistMetadataDebugLog;
        }

        /// <summary>
        /// 已处理失败次数；供元数据操作判断传输层是否已经推进候选。
        /// </summary>
        public long FailureGeneration => m_FailureGeneration;

        /// <summary>
        /// 按当前游标选择候选 URL。
        /// </summary>
        public string SelectUrl(IReadOnlyList<string> candidateUrls)
        {
            if (candidateUrls == null || candidateUrls.Count == 0)
            {
                throw new YooInternalException("Candidate URL list is null or empty.");
            }

            int index = (m_Cursor & int.MaxValue) % candidateUrls.Count;
            string selectedUrl = candidateUrls[index];
            if (m_EnableWhitelistMetadataDebugLog && TryGetMetadataFileName(selectedUrl, out _))
            {
                m_ActiveMetadataUrls.Add(selectedUrl);
            }
            return selectedUrl;
        }

        /// <summary>
        /// 开始一次由 Nova 编排的版本元数据操作。
        /// </summary>
        public void BeginMetadataRequest()
        {
            m_ActiveMetadataUrls.Clear();
            m_TransportFailedMetadataUrls.Clear();
        }

        /// <summary>
        /// 根据上层 YooAsset 操作结果收口本次选择过的元数据 URL。
        /// 不依赖修改 YooAsset Core：成功时所有已选择 URL 都已完成；失败时最后一个未触发传输失败回调的 URL 为内容/反序列化失败点。
        /// </summary>
        public void CompleteMetadataRequest(bool succeeded, string operationError)
        {
            if (!m_EnableWhitelistMetadataDebugLog)
            {
                BeginMetadataRequest();
                return;
            }

            int operationFailureIndex = -1;
            if (!succeeded && m_TransportFailedMetadataUrls.Count == 0)
            {
                operationFailureIndex = m_ActiveMetadataUrls.Count - 1;
            }

            for (int i = 0; i < m_ActiveMetadataUrls.Count; i++)
            {
                string url = m_ActiveMetadataUrls[i];
                if (m_TransportFailedMetadataUrls.Contains(url))
                {
                    continue;
                }

                if (i == operationFailureIndex)
                {
                    LogMetadataFailure(url, 0L, operationError ?? "Operation failed");
                }
                else
                {
                    OnRequestSucceeded(url);
                }
            }

            BeginMetadataRequest();
        }

        /// <summary>
        /// 请求成功后保持当前候选地址。
        /// </summary>
        public void OnRequestSucceeded(string url)
        {
            if (m_EnableWhitelistMetadataDebugLog && TryGetMetadataFileName(url, out string fileName))
            {
                Log.Debug(LogTag.Asset, "启动白名单版本元数据拉取成功：File={0}, URL={1}", fileName, url);
            }
        }

        /// <summary>
        /// YooAsset 传输层失败回调；每个失败请求都独立推进一次。
        /// </summary>
        public void OnRequestFailed(string url, long httpCode, string httpError)
        {
            if (m_EnableWhitelistMetadataDebugLog && TryGetMetadataFileName(url, out string fileName))
            {
                m_TransportFailedMetadataUrls.Add(url);
                LogMetadataFailure(fileName, url, httpCode, httpError);
            }
            Advance();
        }

        /// <summary>
        /// 上层操作失败收口。用于覆盖 HTTP 成功但版本、哈希或清单内容校验失败的场景。
        /// 若传输层已推进，本次调用不会重复推进。
        /// </summary>
        public void AdvanceAfterOperationFailure(long failureGenerationAtStart)
        {
            if (m_FailureGeneration == failureGenerationAtStart)
            {
                Advance();
            }
        }

        private void Advance()
        {
            unchecked
            {
                m_Cursor++;
                m_FailureGeneration++;
            }
        }

        private static void LogMetadataFailure(string url, long httpCode, string error)
        {
            if (TryGetMetadataFileName(url, out string fileName))
            {
                LogMetadataFailure(fileName, url, httpCode, error);
            }
        }

        private static void LogMetadataFailure(string fileName, string url, long httpCode, string error)
        {
            Log.Debug(LogTag.Asset, "启动白名单版本元数据拉取失败：File={0}, URL={1}, HttpCode={2}, Error={3}",
                fileName, url, httpCode, error);
        }

        private static bool TryGetMetadataFileName(string url, out string fileName)
        {
            fileName = null;
            if (string.IsNullOrWhiteSpace(url))
            {
                return false;
            }

            string path = url;
            if (Uri.TryCreate(url, UriKind.Absolute, out Uri uri))
            {
                path = uri.AbsolutePath;
            }
            else
            {
                int suffixIndex = path.IndexOfAny(new[] { '?', '#' });
                if (suffixIndex >= 0)
                {
                    path = path.Substring(0, suffixIndex);
                }
            }

            fileName = System.IO.Path.GetFileName(path);
            return !string.IsNullOrEmpty(fileName)
                   && (fileName.EndsWith(".version", StringComparison.OrdinalIgnoreCase)
                       || fileName.EndsWith(".hash", StringComparison.OrdinalIgnoreCase)
                       || fileName.EndsWith(".bytes", StringComparison.OrdinalIgnoreCase));
        }
    }
}
