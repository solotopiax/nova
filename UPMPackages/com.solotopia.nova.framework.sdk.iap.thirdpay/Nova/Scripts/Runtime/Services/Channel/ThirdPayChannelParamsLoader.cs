/***************************************************************
 * (c) copyright 2026 - 2030, Solotopia
 * All Rights Reserved.
 * -------------------------------------------------------------
 * filename:  ThirdPayChannelParamsLoader.cs
 * author:    yingzheng
 * created:   2026/8/3
 * descrip:   第三方支付渠道参数按账号并发去重加载器
 ***************************************************************/

using System;
using System.Threading;
using Cysharp.Threading.Tasks;
using NovaFramework.Runtime;

namespace NovaFramework.SDK.IAP.ThirdPay.Runtime
{
    /// <summary>
    /// 合并同一 GameUID 的在途渠道参数请求，让登录预取与立即支付等待同一结果。
    /// </summary>
    internal sealed class ThirdPayChannelParamsLoader
    {
        /// <summary>
        /// 实际发送渠道参数协议的委托。
        /// </summary>
        private readonly Func<string, UniTask<NetResponse<PbNetThirdPayChannelParamsResp>>> m_RequestAsync;

        /// <summary>
        /// 当前在途请求对应的 GameUID。
        /// </summary>
        private string m_InFlightGameUid = string.Empty;

        /// <summary>
        /// 当前在途请求的共享完成源，允许登录预取与支付流程并发等待。
        /// </summary>
        private UniTaskCompletionSource<NetResponse<PbNetThirdPayChannelParamsResp>> m_InFlightCompletion;

        /// <summary>
        /// 创建渠道参数加载器。
        /// </summary>
        /// <param name="requestAsync">实际发送渠道参数协议的委托。</param>
        internal ThirdPayChannelParamsLoader(Func<string, UniTask<NetResponse<PbNetThirdPayChannelParamsResp>>> requestAsync)
        {
            m_RequestAsync = requestAsync ?? throw new ArgumentNullException(nameof(requestAsync));
        }

        /// <summary>
        /// 加载指定 GameUID 的渠道参数；同一 GameUID 已有在途请求时复用其完成结果。
        /// </summary>
        /// <param name="gameUid">当前登录账号 UID。</param>
        /// <param name="cmdName">渠道参数协议的 NetCmd 名称。</param>
        /// <param name="ct">仅取消当前等待者，不取消登录阶段已经发出的共享请求。</param>
        /// <returns>渠道参数协议响应。</returns>
        internal UniTask<NetResponse<PbNetThirdPayChannelParamsResp>> LoadAsync(string gameUid, string cmdName, CancellationToken ct)
        {
            ct.ThrowIfCancellationRequested();
            if (m_InFlightCompletion != null && string.Equals(m_InFlightGameUid, gameUid, StringComparison.Ordinal))
            {
                return m_InFlightCompletion.Task.AttachExternalCancellation(ct);
            }

            var completion = new UniTaskCompletionSource<NetResponse<PbNetThirdPayChannelParamsResp>>();
            m_InFlightGameUid = gameUid;
            m_InFlightCompletion = completion;
            RequestAsync(cmdName, completion).Forget();
            return completion.Task.AttachExternalCancellation(ct);
        }

        /// <summary>
        /// 执行一次真实协议请求，并安全完成对应账号的共享完成源。
        /// </summary>
        /// <param name="cmdName">渠道参数协议的 NetCmd 名称。</param>
        /// <param name="completion">本次请求独占的共享完成源。</param>
        /// <returns>请求结束的异步任务。</returns>
        private async UniTask RequestAsync(string cmdName, UniTaskCompletionSource<NetResponse<PbNetThirdPayChannelParamsResp>> completion)
        {
            try
            {
                NetResponse<PbNetThirdPayChannelParamsResp> response = await m_RequestAsync(cmdName);
                completion.TrySetResult(response);
            }
            catch (Exception ex)
            {
                completion.TrySetException(ex);
            }
            finally
            {
                if (ReferenceEquals(m_InFlightCompletion, completion))
                {
                    m_InFlightGameUid = string.Empty;
                    m_InFlightCompletion = null;
                }
            }
        }
    }
}
