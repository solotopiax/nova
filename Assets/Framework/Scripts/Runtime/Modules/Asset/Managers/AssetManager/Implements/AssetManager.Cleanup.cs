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
        /// 调用前须已完成 LoadManifestAsync（需要清单对比才能判定"未使用"）。
        /// </summary>
        /// <param name="package">包名，null 走默认包。</param>
        /// <param name="ct">取消令牌。</param>
        public override async UniTask ClearUnusedCacheAsync(string package = null, CancellationToken ct = default)
        {
            string name = ResolvePackageName(package);
            ResourcePackage pkg = GetPackage(name);
            var options = new ClearCacheOptions(ClearCacheMethods.ClearUnusedBundleFiles);
            ClearCacheOperation op = pkg.ClearCacheAsync(options);
            await UniTask.WaitUntil(() => op.IsDone, cancellationToken: ct);
        }
    }
}
