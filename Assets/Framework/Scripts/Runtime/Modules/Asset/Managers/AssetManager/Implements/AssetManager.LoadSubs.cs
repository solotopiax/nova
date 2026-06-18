/***************************************************************
 * (c) copyright 2026 - 2030, Solotopia
 * All Rights Reserved.
 * -------------------------------------------------------------
 * filename:  AssetManager.LoadSubs.cs
 * author:    taoye
 * created:   2026/5/14
 * descrip:   AssetManager 子资源批量加载（ISubAssetsHandle 通道）
 ***************************************************************/

using System.Threading;
using Cysharp.Threading.Tasks;
using YooAsset;

namespace NovaFramework.Runtime
{
    internal sealed partial class AssetManager : AssetManagerBase
    {
        /// <summary>
        /// 同步加载子资源批量句柄（泛型），返回中性 ISubAssetsHandle，由调用方自行 Release。
        /// 整批同生共死，禁止对单个子资源单独 Release。
        /// </summary>
        /// <typeparam name="T">子资源类型。</typeparam>
        /// <param name="location">主资源 Asset 地址。</param>
        /// <returns>子资源批量句柄，调用方负责 Release。</returns>
        public override ISubAssetsHandle<T> LoadSubsSync<T>(string location)
        {
            ResourcePackage pkg = GetPackage(m_DefaultPackageName);
            SubAssetsHandle handle = pkg.LoadSubAssetsSync<T>(location);
            YooAssetSubAssetsHandleAdapter<T> adapter = null;
            try
            {
                RepairLoadedAssetShadersForEditor(handle.SubAssetObjects);
                adapter = ReferencePool.Get<YooAssetSubAssetsHandleAdapter<T>>();
                adapter.Bind(handle);
                return adapter;
            }
            catch
            {
                handle.Release();
                if (adapter != null) ReferencePool.Put(adapter);
                throw;
            }
        }

        /// <summary>
        /// 异步加载子资源批量句柄（泛型），取消或异常时内部自动释放 handle，正常完成时由调用方自行 Release。
        /// 整批同生共死，禁止对单个子资源单独 Release。
        /// </summary>
        /// <typeparam name="T">子资源类型。</typeparam>
        /// <param name="location">主资源 Asset 地址。</param>
        /// <param name="ct">取消令牌。</param>
        /// <returns>子资源批量句柄，调用方负责 Release。</returns>
        public override async UniTask<ISubAssetsHandle<T>> LoadSubsAsync<T>(string location, CancellationToken ct = default)
        {
            ResourcePackage pkg = GetPackage(m_DefaultPackageName);
            SubAssetsHandle handle = pkg.LoadSubAssetsAsync<T>(location);
            YooAssetSubAssetsHandleAdapter<T> adapter = null;
            try
            {
                await UniTask.WaitUntil(() => handle.IsDone, cancellationToken: ct);
                RepairLoadedAssetShadersForEditor(handle.SubAssetObjects);
                adapter = ReferencePool.Get<YooAssetSubAssetsHandleAdapter<T>>();
                adapter.Bind(handle);
                return adapter;
            }
            catch
            {
                handle.Release();
                if (adapter != null) ReferencePool.Put(adapter);
                throw;
            }
        }
    }
}
