/***************************************************************
 * (c) copyright 2026 - 2030, Solotopia
 * All Rights Reserved.
 * -------------------------------------------------------------
 * filename:  AssetManager.Preload.cs
 * author:    taoye
 * created:   2026/5/14
 * descrip:   AssetManager 预加载
 ***************************************************************/

using System.Threading;
using Cysharp.Threading.Tasks;
using YooAsset;

namespace NovaFramework.Runtime
{
    internal sealed partial class AssetManager : AssetManagerBase
    {
        /// <summary>
        /// 预加载单个资源（仅触发 AB + Asset 进缓存，不返回对象），取消或异常时释放 handle。
        /// </summary>
        /// <param name="location">资源 location。</param>
        /// <param name="ct">取消令牌。</param>
        public override async UniTask PreloadAsync(string location, CancellationToken ct = default)
        {
            ResourcePackage pkg = GetPackage(m_DefaultPackageName);
            AssetHandle handle = pkg.LoadAssetAsync(location);
            try
            {
                await UniTask.WaitUntil(() => handle.IsDone, cancellationToken: ct);
            }
            catch
            {
                handle.Release();
                throw;
            }
        }

        /// <summary>
        /// 批量预加载资源，并发触发所有 location 的 AB + Asset 缓存。
        /// </summary>
        /// <param name="locations">资源 location 列表。</param>
        /// <param name="ct">取消令牌。</param>
        public override async UniTask PreloadAsync(string[] locations, CancellationToken ct = default)
        {
            if (locations == null || locations.Length == 0)
            {
                return;
            }
            ResourcePackage pkg = GetPackage(m_DefaultPackageName);
            UniTask[] tasks = new UniTask[locations.Length];
            for (int i = 0; i < locations.Length; i++)
            {
                tasks[i] = PreloadSingleAsync(pkg, locations[i], ct);
            }
            await UniTask.WhenAll(tasks);
        }

        /// <summary>
        /// 预加载单个资源并在取消或异常时释放 handle。
        /// </summary>
        /// <param name="pkg">目标包。</param>
        /// <param name="location">资源 location。</param>
        /// <param name="ct">取消令牌。</param>
        private static async UniTask PreloadSingleAsync(ResourcePackage pkg, string location, CancellationToken ct)
        {
            AssetHandle handle = pkg.LoadAssetAsync(location);
            try
            {
                await UniTask.WaitUntil(() => handle.IsDone, cancellationToken: ct);
            }
            catch
            {
                handle.Release();
                throw;
            }
        }
    }
}
