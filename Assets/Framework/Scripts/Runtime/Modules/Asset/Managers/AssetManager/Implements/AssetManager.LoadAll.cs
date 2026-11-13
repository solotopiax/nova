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
            return LoadAllSyncCore<T>(GetPackage(m_DefaultPackageName), location);
        }

        /// <summary>
        /// 同步加载指定包的全资源批量句柄，不自动注册包或加载清单。
        /// 调用方持有成功返回的句柄并负责 Release；批量句柄中的资源必须整批使用和释放。
        /// 沿用默认包入口的完成判定；完成不保证资源非空或底层加载成功。
        /// </summary>
        /// <typeparam name="T">资源类型。</typeparam>
        /// <param name="package">精确包名，必填且不得为 null、空串或空白；不会回退默认包。</param>
        /// <param name="location">该包内的资源地址。</param>
        /// <returns>由调用方持有并负责释放的资源句柄。</returns>
        /// <exception cref="System.NotSupportedException">当前资源管理器不支持显式包加载。</exception>
        /// <exception cref="System.ArgumentException">内置实现收到空白包名。</exception>
        /// <exception cref="System.InvalidOperationException">内置实现中目标包未注册。</exception>
        public override IAllAssetsHandle<T> LoadAllFromPackageSync<T>(string package, string location)
        {
            return LoadAllSyncCore<T>(GetExplicitPackage(package), location);
        }

        /// <summary>
        /// 同步执行默认包与显式包共用的全资源批量加载；保持原等待、失败与清理顺序。
        /// 调用方持有成功返回的句柄并负责 Release；批量句柄中的资源必须整批使用和释放。
        /// </summary>
        /// <typeparam name="T">资源类型。</typeparam>
        /// <param name="pkg">已经选定的资源包。</param>
        /// <param name="location">该包内的资源地址。</param>
        /// <returns>由调用方持有并负责释放的资源句柄。</returns>
        private IAllAssetsHandle<T> LoadAllSyncCore<T>(ResourcePackage pkg, string location) where T : UnityEngine.Object
        {
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
            return await LoadAllAsyncCore<T>(GetPackage(m_DefaultPackageName), location, ct);
        }

        /// <summary>
        /// 异步加载指定包的全资源批量句柄，不自动注册包或加载清单。
        /// 调用方持有成功返回的句柄并负责 Release；批量句柄中的资源必须整批使用和释放。
        /// 沿用默认包入口的完成判定；完成不保证资源非空或底层加载成功。
        /// </summary>
        /// <typeparam name="T">资源类型。</typeparam>
        /// <param name="package">精确包名，必填且不得为 null、空串或空白；不会回退默认包。</param>
        /// <param name="location">该包内的资源地址。</param>
        /// <param name="ct">取消令牌；显式包入口在调度前检查取消，加载中取消沿用原清理流程。</param>
        /// <returns>由调用方持有并负责释放的资源句柄。</returns>
        /// <exception cref="System.NotSupportedException">当前资源管理器不支持显式包加载。</exception>
        /// <exception cref="System.ArgumentException">内置实现收到空白包名。</exception>
        /// <exception cref="System.InvalidOperationException">内置实现中目标包未注册。</exception>
        public override async UniTask<IAllAssetsHandle<T>> LoadAllFromPackageAsync<T>(string package, string location, CancellationToken ct = default)
        {
            ct.ThrowIfCancellationRequested();
            return await LoadAllAsyncCore<T>(GetExplicitPackage(package), location, ct);
        }

        /// <summary>
        /// 异步执行默认包与显式包共用的全资源批量加载；保持原等待、失败与清理顺序。
        /// 调用方持有成功返回的句柄并负责 Release；批量句柄中的资源必须整批使用和释放。
        /// </summary>
        /// <typeparam name="T">资源类型。</typeparam>
        /// <param name="pkg">已经选定的资源包。</param>
        /// <param name="location">该包内的资源地址。</param>
        /// <param name="ct">等待加载完成时使用的取消令牌。</param>
        /// <returns>由调用方持有并负责释放的资源句柄。</returns>
        private async UniTask<IAllAssetsHandle<T>> LoadAllAsyncCore<T>(ResourcePackage pkg, string location, CancellationToken ct) where T : UnityEngine.Object
        {
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
