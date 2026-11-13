/***************************************************************
 * (c) copyright 2026 - 2030, Solotopia
 * All Rights Reserved.
 * -------------------------------------------------------------
 * filename:  AssetManager.LoadRaw.cs
 * author:    taoye
 * created:   2026/5/14
 * descrip:   AssetManager Raw 字节加载 —— 通过 RawFileObject 维持 Nova 原始文件句柄契约
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
            return LoadRawSyncCore(GetPackage(m_DefaultPackageName), location);
        }

        /// <summary>
        /// 同步加载指定包的原始文件句柄，不自动注册包或加载清单。
        /// 调用方持有成功返回的句柄，并负责在使用结束后 Release。
        /// </summary>
        /// <param name="package">精确包名，必填且不得为 null、空串或空白；不会回退默认包。</param>
        /// <param name="location">该包内的资源地址。</param>
        /// <returns>由调用方持有并负责释放的资源句柄。</returns>
        /// <exception cref="System.NotSupportedException">当前资源管理器不支持显式包加载。</exception>
        /// <exception cref="System.ArgumentException">内置实现收到空白包名。</exception>
        /// <exception cref="System.InvalidOperationException">内置实现中目标包未注册。</exception>
        public override IRawFileHandle LoadRawFromPackageSync(string package, string location)
        {
            return LoadRawSyncCore(GetExplicitPackage(package), location);
        }

        /// <summary>
        /// 同步执行默认包与显式包共用的原始文件加载；保持原等待、失败与清理顺序。
        /// 调用方持有成功返回的句柄，并负责在使用结束后 Release。
        /// </summary>
        /// <param name="pkg">已经选定的资源包。</param>
        /// <param name="location">该包内的资源地址。</param>
        /// <returns>由调用方持有并负责释放的资源句柄。</returns>
        private IRawFileHandle LoadRawSyncCore(ResourcePackage pkg, string location)
        {
            AssetHandle inner = null;
            YooAssetRawFileHandleAdapter adapter = null;
            try
            {
                inner = pkg.LoadAssetSync<RawFileObject>(location);
                if (inner.Status != EOperationStatus.Succeeded)
                    throw new System.InvalidOperationException($"LoadAssetSync<RawFileObject> failed: {inner.Error}");

                RawFileObject rawFile = inner.GetAssetObject<RawFileObject>();
                if (rawFile == null)
                    throw new System.InvalidOperationException($"RawFileObject is null: {location}");

                adapter = ReferencePool.Get<YooAssetRawFileHandleAdapter>();
                // EnsureBundleFileOperation 不支持同步等待；同步加载仍可靠提供字节，但底层 bundle 路径为空。
                adapter.Bind(inner, rawFile, null);
                inner = null;
                return adapter;
            }
            catch
            {
                inner?.Release();
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
            return await LoadRawAsyncCore(GetPackage(m_DefaultPackageName), location, ct);
        }

        /// <summary>
        /// 异步加载指定包的原始文件句柄，不自动注册包或加载清单。
        /// 调用方持有成功返回的句柄，并负责在使用结束后 Release。
        /// </summary>
        /// <param name="package">精确包名，必填且不得为 null、空串或空白；不会回退默认包。</param>
        /// <param name="location">该包内的资源地址。</param>
        /// <param name="ct">取消令牌；显式包入口在调度前检查取消，加载中取消沿用原清理流程。</param>
        /// <returns>由调用方持有并负责释放的资源句柄。</returns>
        /// <exception cref="System.NotSupportedException">当前资源管理器不支持显式包加载。</exception>
        /// <exception cref="System.ArgumentException">内置实现收到空白包名。</exception>
        /// <exception cref="System.InvalidOperationException">内置实现中目标包未注册。</exception>
        public override async UniTask<IRawFileHandle> LoadRawFromPackageAsync(string package, string location, CancellationToken ct = default)
        {
            ct.ThrowIfCancellationRequested();
            return await LoadRawAsyncCore(GetExplicitPackage(package), location, ct);
        }

        /// <summary>
        /// 异步执行默认包与显式包共用的原始文件加载；保持原等待、失败与清理顺序。
        /// 调用方持有成功返回的句柄，并负责在使用结束后 Release。
        /// </summary>
        /// <param name="pkg">已经选定的资源包。</param>
        /// <param name="location">该包内的资源地址。</param>
        /// <param name="ct">等待加载完成时使用的取消令牌。</param>
        /// <returns>由调用方持有并负责释放的资源句柄。</returns>
        private async UniTask<IRawFileHandle> LoadRawAsyncCore(ResourcePackage pkg, string location, CancellationToken ct)
        {
            AssetHandle inner = null;
            YooAssetRawFileHandleAdapter adapter = null;
            try
            {
                inner = pkg.LoadAssetAsync<RawFileObject>(location);
                await UniTask.WaitUntil(() => inner.IsDone, cancellationToken: ct);
                if (inner.Status != EOperationStatus.Succeeded)
                    throw new System.InvalidOperationException($"LoadAssetAsync<RawFileObject> failed: {inner.Error}");

                RawFileObject rawFile = inner.GetAssetObject<RawFileObject>();
                if (rawFile == null)
                    throw new System.InvalidOperationException($"RawFileObject is null: {location}");

                // 路径是尽力补充信息：Web/内存文件系统不支持 Ensure，失败时不影响已成功加载的 RawFileObject 字节。
                EnsureBundleFileOperation ensureOperation = pkg.EnsureBundleFileAsync(new EnsureBundleFileOptions(inner.GetAssetInfo()));
                await UniTask.WaitUntil(() => ensureOperation.IsDone, cancellationToken: ct);
                string filePath = ensureOperation.Status == EOperationStatus.Succeeded
                    ? ensureOperation.Detail.BundleFilePath
                    : null;

                adapter = ReferencePool.Get<YooAssetRawFileHandleAdapter>();
                adapter.Bind(inner, rawFile, filePath);
                inner = null;
                return adapter;
            }
            catch
            {
                inner?.Release();
                if (adapter != null) ReferencePool.Put(adapter);
                throw;
            }
        }
    }
}
