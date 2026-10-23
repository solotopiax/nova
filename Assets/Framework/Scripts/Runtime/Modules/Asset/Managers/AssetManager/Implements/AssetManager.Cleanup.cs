/***************************************************************
 * (c) copyright 2026 - 2030, Solotopia
 * All Rights Reserved.
 * -------------------------------------------------------------
 * filename:  AssetManager.Cleanup.cs
 * author:    taoye
 * created:   2026/5/14
 * descrip:   AssetManager 回收
 ***************************************************************/

using System.Threading;
using Cysharp.Threading.Tasks;
using YooAsset;

namespace NovaFramework.Runtime
{
    internal sealed partial class AssetManager : AssetManagerBase
    {
        /// <summary>
        /// 批量回收：扫描所有 RefCount=0 的 Provider 与 BundleLoader 并销毁。
        /// </summary>
        /// <param name="package">包名，null 走默认包。</param>
        /// <param name="ct">取消令牌。</param>
        public override async UniTask CleanupAsync(string package = null, CancellationToken ct = default)
        {
            string name = ResolvePackageName(package);
            ResourcePackage pkg = GetPackage(name);
            UnloadUnusedAssetsOperation op = pkg.UnloadUnusedAssetsAsync();
            await UniTask.WaitUntil(() => op.IsDone, cancellationToken: ct);
        }

        /// <summary>
        /// 清理磁盘缓存中未被当前清单引用的冗余 Bundle 文件，释放磁盘空间。
        /// 调用前须已完成 LoadManifestAsync；内置实现检查实际清单及底层操作失败，正常返回仍须按需核验磁盘结果。
        /// </summary>
        /// <param name="package">包名，null 或空串走默认包。</param>
        /// <param name="ct">取消令牌；开始前取消不调度，开始后仅停止等待，不中止或回滚底层清理。</param>
        /// <exception cref="System.InvalidOperationException">内置实现的目标包未注册、实际清单未就绪或底层清理操作报告失败。</exception>
        /// <exception cref="System.OperationCanceledException">调用前或等待过程中取消；已开始的底层清理可能继续。</exception>
        public override async UniTask ClearUnusedCacheAsync(string package = null, CancellationToken ct = default)
        {
            ct.ThrowIfCancellationRequested();
            string name = ResolvePackageName(package);
            ResourcePackage pkg = GetPackage(name);
            if (!pkg.PackageValid)
            {
                throw new System.InvalidOperationException($"Package '{name}' must have an active Manifest before clearing unused cache. Call LoadManifestAsync first.");
            }

            var options = new ClearCacheOptions(ClearCacheMethods.ClearUnusedBundleFiles);
            ClearCacheOperation op = pkg.ClearCacheAsync(options);
            // 取消只停止本次等待，不中止或回滚已调度的磁盘清理。
            await UniTask.WaitUntil(() => op.IsDone, cancellationToken: ct);
            if (op.Status != EOperationStatus.Succeeded)
            {
                throw new System.InvalidOperationException($"Package '{name}' failed to clear unused cache. Status: {op.Status}. Error: {op.Error}");
            }
        }
    }
}
