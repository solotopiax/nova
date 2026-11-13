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
            return LoadSyncCore<T>(GetPackage(m_DefaultPackageName), location);
        }

        /// <summary>
        /// 同步加载指定包的主资源句柄，不自动注册包或加载清单。
        /// 调用方持有成功返回的句柄，并负责在使用结束后 Release。
        /// 沿用默认包入口的完成判定；完成不保证资源非空或底层加载成功。
        /// </summary>
        /// <typeparam name="T">资源类型。</typeparam>
        /// <param name="package">精确包名，必填且不得为 null、空串或空白；不会回退默认包。</param>
        /// <param name="location">该包内的资源地址。</param>
        /// <returns>由调用方持有并负责释放的资源句柄。</returns>
        /// <exception cref="System.NotSupportedException">当前资源管理器不支持显式包加载。</exception>
        /// <exception cref="System.ArgumentException">内置实现收到空白包名。</exception>
        /// <exception cref="System.InvalidOperationException">内置实现中目标包未注册。</exception>
        public override IAssetHandle<T> LoadFromPackageSync<T>(string package, string location)
        {
            return LoadSyncCore<T>(GetExplicitPackage(package), location);
        }

        /// <summary>
        /// 同步执行默认包与显式包共用的主资源加载；保持原等待、失败与清理顺序。
        /// 调用方持有成功返回的句柄，并负责在使用结束后 Release。
        /// </summary>
        /// <typeparam name="T">资源类型。</typeparam>
        /// <param name="pkg">已经选定的资源包。</param>
        /// <param name="location">该包内的资源地址。</param>
        /// <returns>由调用方持有并负责释放的资源句柄。</returns>
        private IAssetHandle<T> LoadSyncCore<T>(ResourcePackage pkg, string location) where T : UnityEngine.Object
        {
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
            return await LoadAsyncCore<T>(GetPackage(m_DefaultPackageName), location, ct);
        }

        /// <summary>
        /// 异步加载指定包的主资源句柄，不自动注册包或加载清单。
        /// 调用方持有成功返回的句柄，并负责在使用结束后 Release。
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
        public override async UniTask<IAssetHandle<T>> LoadFromPackageAsync<T>(string package, string location, CancellationToken ct = default)
        {
            ct.ThrowIfCancellationRequested();
            return await LoadAsyncCore<T>(GetExplicitPackage(package), location, ct);
        }

        /// <summary>
        /// 异步执行默认包与显式包共用的主资源加载；保持原等待、失败与清理顺序。
        /// 调用方持有成功返回的句柄，并负责在使用结束后 Release。
        /// </summary>
        /// <typeparam name="T">资源类型。</typeparam>
        /// <param name="pkg">已经选定的资源包。</param>
        /// <param name="location">该包内的资源地址。</param>
        /// <param name="ct">等待加载完成时使用的取消令牌。</param>
        /// <returns>由调用方持有并负责释放的资源句柄。</returns>
        private async UniTask<IAssetHandle<T>> LoadAsyncCore<T>(ResourcePackage pkg, string location, CancellationToken ct) where T : UnityEngine.Object
        {
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

        /// <summary>
        /// 校验显式包名并取得已注册的资源包，禁止空包名隐式落到默认包。
        /// </summary>
        /// <param name="package">必须精确匹配的非空白包名。</param>
        /// <returns>已注册的目标包，不执行初始化或清单加载。</returns>
        /// <exception cref="System.ArgumentException">包名为 null、空串或空白。</exception>
        /// <exception cref="System.InvalidOperationException">目标包尚未注册。</exception>
        private ResourcePackage GetExplicitPackage(string package)
        {
            if (string.IsNullOrWhiteSpace(package))
                throw new System.ArgumentException("显式包加载必须提供非空白包名。", nameof(package));
            return GetPackage(package);
        }
    }
}
