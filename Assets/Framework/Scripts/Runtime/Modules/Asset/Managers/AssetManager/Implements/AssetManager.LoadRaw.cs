/***************************************************************
 * (c) copyright 2026 - 2030, Solotopia
 * All Rights Reserved.
 * -------------------------------------------------------------
 * filename:  AssetManager.LoadRaw.cs
 * author:    taoye
 * created:   2026/5/14
 * descrip:   AssetManager Raw 字节加载 —— 直连 YooAsset RawFile 通道
 ***************************************************************/

using System.Threading;
using Cysharp.Threading.Tasks;
using YooAsset;

namespace NovaFramework.Runtime
{
    internal sealed partial class AssetManager : AssetManagerBase
    {
        /// <summary>
        /// 同步加载原始文件句柄（RawFile 通道）。
        /// 资源必须以 RawFile 模式打入 AB（PackRule = PackRawFile，且 BuildBundleType = RawBundle）。
        /// 调用方负责在使用完毕后调用 Release 归还引用计数。
        /// </summary>
        /// <param name="location">Asset 地址。</param>
        /// <returns>原始文件句柄，调用方负责 Release。</returns>
        public override IRawFileHandle LoadRawSync(string location)
        {
            ResourcePackage pkg = GetPackage(m_DefaultPackageName);
            RawFileHandle inner = pkg.LoadRawFileSync(location);
            YooAssetRawFileHandleAdapter adapter = null;
            try
            {
                adapter = ReferencePool.Get<YooAssetRawFileHandleAdapter>();
                adapter.Bind(inner);
                return adapter;
            }
            catch
            {
                inner.Release();
                if (adapter != null) ReferencePool.Put(adapter);
                throw;
            }
        }

        /// <summary>
        /// 异步加载原始文件句柄（RawFile 通道），取消或异常时自动释放 handle。
        /// 资源必须以 RawFile 模式打入 AB（PackRule = PackRawFile，且 BuildBundleType = RawBundle）。
        /// 调用方负责在使用完毕后调用 Release 归还引用计数。
        /// </summary>
        /// <param name="location">Asset 地址。</param>
        /// <param name="ct">取消令牌。</param>
        /// <returns>原始文件句柄，调用方负责 Release。</returns>
        public override async UniTask<IRawFileHandle> LoadRawAsync(string location, CancellationToken ct = default)
        {
            ResourcePackage pkg = GetPackage(m_DefaultPackageName);
            RawFileHandle inner = pkg.LoadRawFileAsync(location);
            YooAssetRawFileHandleAdapter adapter = ReferencePool.Get<YooAssetRawFileHandleAdapter>();
            adapter.Bind(inner);
            try
            {
                await UniTask.WaitUntil(() => inner.IsDone, cancellationToken: ct);
                return adapter;
            }
            catch
            {
                adapter.Release();
                throw;
            }
        }
    }
}
