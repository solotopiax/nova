/***************************************************************
 * (c) copyright 2026 - 2030, Solotopia
 * All Rights Reserved.
 * -------------------------------------------------------------
 * filename:  AssetManager.LoadAll.cs
 * author:    taoye
 * created:   2026/5/14
 * descrip:   AssetManager 全资源批量加载（IAllAssetsHandle 通道）
 ***************************************************************/

using System.Threading;
using Cysharp.Threading.Tasks;
using YooAsset;

namespace NovaFramework.Runtime
{
    internal sealed partial class AssetManager : AssetManagerBase
    {
        /// <summary>
        /// 同步加载全资源批量句柄（泛型），返回中性 IAllAssetsHandle，由调用方自行 Release。
        /// 整批同生共死，禁止对单个资源单独 Release。
        /// </summary>
        /// <typeparam name="T">资源类型。</typeparam>
        /// <param name="location">Asset 地址。</param>
        /// <returns>全资源批量句柄，调用方负责 Release。</returns>
        public override IAllAssetsHandle<T> LoadAllSync<T>(string location)
        {
            ResourcePackage pkg = GetPackage(m_DefaultPackageName);
            AllAssetsHandle handle = pkg.LoadAllAssetsSync<T>(location);
            YooAssetAllAssetsHandleAdapter<T> adapter = null;
            try
            {
                RepairLoadedAssetShadersForEditor(handle.AllAssetObjects);
                adapter = ReferencePool.Get<YooAssetAllAssetsHandleAdapter<T>>();
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
        /// 异步加载全资源批量句柄（泛型），取消或异常时内部自动释放 handle，正常完成时由调用方自行 Release。
        /// 整批同生共死，禁止对单个资源单独 Release。
        /// </summary>
        /// <typeparam name="T">资源类型。</typeparam>
        /// <param name="location">Asset 地址。</param>
        /// <param name="ct">取消令牌。</param>
        /// <returns>全资源批量句柄，调用方负责 Release。</returns>
        public override async UniTask<IAllAssetsHandle<T>> LoadAllAsync<T>(string location, CancellationToken ct = default)
        {
            ResourcePackage pkg = GetPackage(m_DefaultPackageName);
            AllAssetsHandle handle = pkg.LoadAllAssetsAsync<T>(location);
            YooAssetAllAssetsHandleAdapter<T> adapter = null;
            try
            {
                await UniTask.WaitUntil(() => handle.IsDone, cancellationToken: ct);
                RepairLoadedAssetShadersForEditor(handle.AllAssetObjects);
                adapter = ReferencePool.Get<YooAssetAllAssetsHandleAdapter<T>>();
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
