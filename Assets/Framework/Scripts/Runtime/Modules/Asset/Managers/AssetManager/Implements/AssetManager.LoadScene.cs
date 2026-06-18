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
            ResourcePackage pkg = GetPackage(m_DefaultPackageName);
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
        /// 异步加载场景并返回场景句柄，取消或异常时自动调用 UnloadAsync。
        /// 调用方负责在适当时机调用 UnloadAsync 卸载场景并归还引用计数。
        /// </summary>
        /// <param name="location">场景 Asset 地址。</param>
        /// <param name="mode">场景加载模式。</param>
        /// <param name="ct">取消令牌。</param>
        /// <returns>场景句柄，调用方负责 UnloadAsync。</returns>
        public override async UniTask<ISceneHandle> LoadSceneAsync(string location, LoadSceneMode mode = LoadSceneMode.Single, CancellationToken ct = default)
        {
            ResourcePackage pkg = GetPackage(m_DefaultPackageName);
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
