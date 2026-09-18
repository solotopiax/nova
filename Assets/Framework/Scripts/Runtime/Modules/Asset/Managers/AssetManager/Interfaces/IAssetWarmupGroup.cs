/***************************************************************
 * (c) copyright 2026 - 2030, Solotopia
 * All Rights Reserved.
 * -------------------------------------------------------------
 * filename:  IAssetWarmupGroup.cs
 * author:    taoye
 * created:   2026/9/16
 * descrip:   资源 Bundle 预热组接口
 ***************************************************************/

using System;
using System.Threading;
using Cysharp.Threading.Tasks;

namespace NovaFramework.Runtime
{
    /// <summary>
    /// 一组异步 Bundle 预热任务及其 Handle 所有权容器。
    /// </summary>
    public interface IAssetWarmupGroup : IDisposable
    {
        /// <summary>
        /// 本组需要完成的资源项总数。
        /// </summary>
        int TotalCount { get; }

        /// <summary>
        /// 当前已经完成的资源项数量。
        /// </summary>
        int FinishedCount { get; }

        /// <summary>
        /// 当前进度，范围为 0~1。
        /// </summary>
        float Progress { get; }

        /// <summary>
        /// 当前组是否已经结束。
        /// </summary>
        bool IsDone { get; }

        /// <summary>
        /// 当前组是否成功完成。
        /// </summary>
        bool Succeeded { get; }

        /// <summary>
        /// 当前组是否已经释放。
        /// </summary>
        bool IsReleased { get; }

        /// <summary>
        /// 最近一次失败原因；成功或尚未失败时为空。
        /// </summary>
        string Error { get; }

        /// <summary>
        /// 预热范围描述，仅用于日志和 UI 展示。
        /// </summary>
        string Scope { get; }

        /// <summary>
        /// 进度变化事件，参数依次为已完成项数和总项数。
        /// </summary>
        event Action<int, int> OnProgress;

        /// <summary>
        /// 启动并等待本组全部 Bundle 完成加载。
        /// </summary>
        /// <param name="ct">取消令牌。</param>
        /// <returns>true 表示全部成功；false 表示至少一项失败。</returns>
        UniTask<bool> RunAsync(CancellationToken ct = default);

        /// <summary>
        /// 停止本组等待并释放全部 Handle。
        /// 已经开始的 YooAsset Bundle 请求没有独立取消接口，底层请求可能继续到自然结束。
        /// </summary>
        void Cancel();

        /// <summary>
        /// 幂等释放本组持有的全部 Handle；不会清除 Unity Web Cache。
        /// </summary>
        void Release();
    }
}
