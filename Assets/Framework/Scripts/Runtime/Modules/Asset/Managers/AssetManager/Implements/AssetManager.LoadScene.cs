/***************************************************************
 * (c) copyright 2026 - 2030, Solotopia
 * All Rights Reserved.
 * -------------------------------------------------------------
 * filename:  AssetManager.LoadScene.cs
 * author:    taoye
 * created:   2026/5/14
 * descrip:   AssetManager Scene 加载
 ***************************************************************/

using System.Threading;
using Cysharp.Threading.Tasks;
using UnityEngine.SceneManagement;
using YooAsset;

namespace NovaFramework.Runtime
{
    internal sealed partial class AssetManager : AssetManagerBase
    {
        /// <summary>
        /// 同步加载场景并返回场景句柄。
        /// 调用方负责在适当时机调用 UnloadAsync 卸载场景并归还引用计数。
        /// </summary>
        /// <param name="location">场景 Asset 地址。</param>
        /// <param name="mode">场景加载模式。</param>
        /// <returns>场景句柄，调用方负责 UnloadAsync。</returns>
        public override ISceneHandle LoadSceneSync(string location, LoadSceneMode mode = LoadSceneMode.Single)
        {
            return LoadSceneSyncCore(GetPackage(m_DefaultPackageName), location, mode);
        }

        /// <summary>
        /// 同步加载指定包的场景句柄，不自动注册包或加载清单。
        /// 调用方持有成功返回的句柄，并负责调用 UnloadAsync；不提供普通 Release。
        /// 沿用默认包入口的完成判定；完成不保证资源非空或底层加载成功。
        /// </summary>
        /// <param name="package">精确包名，必填且不得为 null、空串或空白；不会回退默认包。</param>
        /// <param name="location">该包内的资源地址。</param>
        /// <param name="mode">场景加载模式；Single 会替换已有场景。</param>
        /// <returns>由调用方持有并负责卸载的场景句柄。</returns>
        /// <exception cref="System.NotSupportedException">当前资源管理器不支持显式包加载。</exception>
        /// <exception cref="System.ArgumentException">内置实现收到空白包名。</exception>
        /// <exception cref="System.InvalidOperationException">内置实现中目标包未注册。</exception>
        public override ISceneHandle LoadSceneFromPackageSync(string package, string location, LoadSceneMode mode = LoadSceneMode.Single)
        {
            return LoadSceneSyncCore(GetExplicitPackage(package), location, mode);
        }

        /// <summary>
        /// 同步执行默认包与显式包共用的场景加载；保持原等待、失败与清理顺序。
        /// 调用方持有成功返回的句柄，并负责调用 UnloadAsync；不提供普通 Release。
        /// </summary>
        /// <param name="pkg">已经选定的资源包。</param>
        /// <param name="location">该包内的资源地址。</param>
        /// <param name="mode">场景加载模式；Single 会替换已有场景。</param>
        /// <returns>由调用方持有并负责卸载的场景句柄。</returns>
        private ISceneHandle LoadSceneSyncCore(ResourcePackage pkg, string location, LoadSceneMode mode)
        {
            YooAsset.SceneHandle inner = pkg.LoadSceneSync(location, mode);
            YooAssetSceneHandleAdapter adapter = null;
            try
            {
                adapter = ReferencePool.Get<YooAssetSceneHandleAdapter>();
                adapter.Bind(inner);
                return adapter;
            }
            catch
            {
                // inner 是 SceneHandle，无同步 Unload，仅归还 adapter 防止池泄漏
                if (adapter != null) ReferencePool.Put(adapter);
                throw;
            }
        }

        /// <summary>
        /// 异步加载场景并返回句柄；等待阶段取消或异常时尝试 UnloadAsync，卸载异常可继续传播。
        /// 取得适配器与 Bind 在等待 try 外；完成只检查 IsDone，不另检查底层成功状态。
        /// 调用方负责在适当时机调用 UnloadAsync 卸载场景并归还引用计数。
        /// </summary>
        /// <param name="location">场景 Asset 地址。</param>
        /// <param name="mode">场景加载模式。</param>
        /// <param name="ct">取消令牌。</param>
        /// <returns>场景句柄，调用方负责 UnloadAsync。</returns>
        public override async UniTask<ISceneHandle> LoadSceneAsync(string location, LoadSceneMode mode = LoadSceneMode.Single, CancellationToken ct = default)
        {
            return await LoadSceneAsyncCore(GetPackage(m_DefaultPackageName), location, mode, ct);
        }

        /// <summary>
        /// 异步加载指定包的场景句柄，不自动注册包或加载清单。
        /// 调用方持有成功返回的句柄，并负责调用 UnloadAsync；不提供普通 Release。
        /// 沿用默认包入口的完成判定；完成不保证资源非空或底层加载成功。
        /// </summary>
        /// <param name="package">精确包名，必填且不得为 null、空串或空白；不会回退默认包。</param>
        /// <param name="location">该包内的资源地址。</param>
        /// <param name="mode">场景加载模式；Single 会替换已有场景。</param>
        /// <param name="ct">取消令牌；显式包入口在调度前检查取消，加载中取消沿用原清理流程。</param>
        /// <returns>由调用方持有并负责卸载的场景句柄。</returns>
        /// <exception cref="System.NotSupportedException">当前资源管理器不支持显式包加载。</exception>
        /// <exception cref="System.ArgumentException">内置实现收到空白包名。</exception>
        /// <exception cref="System.InvalidOperationException">内置实现中目标包未注册。</exception>
        public override async UniTask<ISceneHandle> LoadSceneFromPackageAsync(string package, string location, LoadSceneMode mode = LoadSceneMode.Single, CancellationToken ct = default)
        {
            ct.ThrowIfCancellationRequested();
            return await LoadSceneAsyncCore(GetExplicitPackage(package), location, mode, ct);
        }

        /// <summary>
        /// 异步执行默认包与显式包共用的场景加载；保持原等待、失败与清理顺序。
        /// 调用方持有成功返回的句柄，并负责调用 UnloadAsync；不提供普通 Release。
        /// </summary>
        /// <param name="pkg">已经选定的资源包。</param>
        /// <param name="location">该包内的资源地址。</param>
        /// <param name="mode">场景加载模式；Single 会替换已有场景。</param>
        /// <param name="ct">等待加载完成时使用的取消令牌。</param>
        /// <returns>由调用方持有并负责卸载的场景句柄。</returns>
        private async UniTask<ISceneHandle> LoadSceneAsyncCore(ResourcePackage pkg, string location, LoadSceneMode mode, CancellationToken ct)
        {
            YooAsset.SceneHandle inner = pkg.LoadSceneAsync(location, mode);
            YooAssetSceneHandleAdapter adapter = ReferencePool.Get<YooAssetSceneHandleAdapter>();
            adapter.Bind(inner);
            try
            {
                await UniTask.WaitUntil(() => inner.IsDone, cancellationToken: ct);
                return adapter;
            }
            catch
            {
                await adapter.UnloadAsync();
                throw;
            }
        }
    }
}
