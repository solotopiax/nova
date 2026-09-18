/***************************************************************
 * (c) copyright 2026 - 2030, Solotopia
 * All Rights Reserved.
 * -------------------------------------------------------------
 * filename:  IAssetStartupWarmupController.cs
 * author:    taoye
 * created:   2026/9/16
 * descrip:   WebGL 启动预热生命周期扩展接口
 ***************************************************************/

using System.Threading;
using Cysharp.Threading.Tasks;

namespace NovaFramework.Runtime
{
    /// <summary>
    /// 资源管理器可选实现的 WebGL 启动预热控制面。
    /// 框架 Procedure 通过该接口编排启动预热，不接触底层 Bundle 实现。
    /// </summary>
    internal interface IAssetStartupWarmupController
    {
        /// <summary>
        /// 当前 WebGL Player 是否需要进入启动 Warmup 流程。
        /// </summary>
        bool RequiresLaunchWarmup { get; }

        /// <summary>
        /// 根据当前策略为默认包创建一次新的启动 WarmupGroup。
        /// </summary>
        /// <returns>尚未启动的预热组。</returns>
        IAssetWarmupGroup CreateLaunchWarmupGroup();

        /// <summary>
        /// 结束一轮启动预热：AllOnLaunch 成功时接管长期持有；TagsOnLaunch 成功时释放并登记延后清理；失败时立即释放清理。
        /// </summary>
        /// <param name="group">本轮预热组。</param>
        /// <param name="succeeded">本轮是否成功。</param>
        /// <param name="ct">取消令牌。</param>
        UniTask CompleteLaunchWarmupAsync(IAssetWarmupGroup group, bool succeeded, CancellationToken ct);

        /// <summary>
        /// 在启动 DLL 已消费完成后，清理 TagsOnLaunch 已释放但尚未卸载的 Bundle 引用。
        /// AllOnLaunch 与没有待清理预热组时为空操作。
        /// </summary>
        /// <param name="ct">取消令牌。</param>
        UniTask CleanupLaunchWarmupAfterDllAsync(CancellationToken ct);
    }
}
