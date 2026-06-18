/***************************************************************
 * (c) copyright 2026 - 2030, Solotopia
 * All Rights Reserved.
 * -------------------------------------------------------------
 * filename:  AssetManager.Load.cs
 * author:    taoye
 * created:   2026/5/14
 * descrip:   AssetManager 主资源加载（IAssetHandle 通道）
 ***************************************************************/

using System.Threading;
using Cysharp.Threading.Tasks;
using YooAsset;

namespace NovaFramework.Runtime
{
    internal sealed partial class AssetManager : AssetManagerBase
    {
        /// <summary>
        /// 同步加载主资源句柄（泛型），返回中性 IAssetHandle，由调用方自行 Release。
        /// </summary>
        /// <typeparam name="T">资源类型。</typeparam>
        /// <param name="location">Asset 地址。</param>
        /// <returns>资源句柄，调用方负责 Release。</returns>
        public override IAssetHandle<T> LoadSync<T>(string location)
        {
            ResourcePackage pkg = GetPackage(m_DefaultPackageName);
            AssetHandle handle = pkg.LoadAssetSync<T>(location);
            YooAssetHandleAdapter<T> adapter = null;
            try
            {
                RepairLoadedAssetShadersForEditor(handle.AssetObject);
                adapter = ReferencePool.Get<YooAssetHandleAdapter<T>>();
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
        /// 异步加载主资源句柄（泛型），取消或异常时内部自动释放 handle，正常完成时由调用方自行 Release。
        /// </summary>
        /// <typeparam name="T">资源类型。</typeparam>
        /// <param name="location">Asset 地址。</param>
        /// <param name="ct">取消令牌。</param>
        /// <returns>资源句柄，调用方负责 Release。</returns>
        public override async UniTask<IAssetHandle<T>> LoadAsync<T>(string location, CancellationToken ct = default)
        {
            ResourcePackage pkg = GetPackage(m_DefaultPackageName);
            AssetHandle handle = pkg.LoadAssetAsync<T>(location);
            YooAssetHandleAdapter<T> adapter = null;
            try
            {
                await UniTask.WaitUntil(() => handle.IsDone, cancellationToken: ct);
                RepairLoadedAssetShadersForEditor(handle.AssetObject);
                adapter = ReferencePool.Get<YooAssetHandleAdapter<T>>();
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
