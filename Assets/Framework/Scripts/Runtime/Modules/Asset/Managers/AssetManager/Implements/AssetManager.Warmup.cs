/***************************************************************
 * (c) copyright 2026 - 2030, Solotopia
 * All Rights Reserved.
 * -------------------------------------------------------------
 * filename:  AssetManager.Warmup.cs
 * author:    taoye
 * created:   2026/9/16
 * descrip:   AssetManager Bundle 预热
 ***************************************************************/

using System;
using System.Collections.Generic;
using System.Threading;
using Cysharp.Threading.Tasks;
using UnityEngine;
using YooAsset;

namespace NovaFramework.Runtime
{
    internal sealed partial class AssetManager : AssetManagerBase, IAssetStartupWarmupController
    {
        /// <inheritdoc />
        public override IAssetWarmupGroup CreateWarmupAll(string package = null)
        {
            string name = ResolvePackageName(package);
            ResourcePackage pkg = GetPackage(name);
            EnsureManifestReadyForWarmup(name);
            return new AssetWarmupGroup(pkg, pkg.GetAllAssetInfos(), "all");
        }

        /// <inheritdoc />
        public override IAssetWarmupGroup CreateWarmupByTags(string[] tags, string package = null)
        {
            string[] normalizedTags = NormalizeValues(tags);
            if (normalizedTags.Length == 0)
            {
                throw new ArgumentException("tags 清理空白与重复项后不能为空；全部资源请使用 CreateWarmupAll。", nameof(tags));
            }

            string name = ResolvePackageName(package);
            ResourcePackage pkg = GetPackage(name);
            EnsureManifestReadyForWarmup(name);
            AssetInfo[] assetInfos = pkg.GetAssetInfos(normalizedTags);
            if (assetInfos.Length == 0)
            {
                throw new InvalidOperationException(
                    $"没有资源命中指定 Warmup Tag。Package={name}, Tags={string.Join(',', normalizedTags)}");
            }
            return new AssetWarmupGroup(
                pkg,
                assetInfos,
                $"tags:{string.Join(',', normalizedTags)}");
        }

        /// <inheritdoc />
        public override IAssetWarmupGroup CreateWarmupByLocations(string[] locations, string package = null)
        {
            string[] normalizedLocations = NormalizeValues(locations);
            if (normalizedLocations.Length == 0)
            {
                throw new ArgumentException("locations 清理空白与重复项后不能为空。", nameof(locations));
            }

            string name = ResolvePackageName(package);
            ResourcePackage pkg = GetPackage(name);
            EnsureManifestReadyForWarmup(name);
            var assetInfos = new List<AssetInfo>(normalizedLocations.Length);
            for (int i = 0; i < normalizedLocations.Length; i++)
            {
                AssetInfo assetInfo = pkg.GetAssetInfo(normalizedLocations[i]);
                if (!assetInfo.IsValid)
                {
                    Log.Warning(LogTag.Asset,
                        "Warmup Asset 地址无效，已跳过：{0} — {1}",
                        normalizedLocations[i],
                        assetInfo.Error);
                    continue;
                }
                assetInfos.Add(assetInfo);
            }

            if (assetInfos.Count == 0)
            {
                throw new ArgumentException("locations 中没有可用于 Warmup 的有效 Asset 地址。", nameof(locations));
            }

            return new AssetWarmupGroup(
                pkg,
                assetInfos.ToArray(),
                $"locations:{assetInfos.Count}");
        }

        /// <inheritdoc />
        bool IAssetStartupWarmupController.RequiresLaunchWarmup
        {
            get
            {
                if (Application.platform != RuntimePlatform.WebGLPlayer || m_Config == null)
                {
                    return false;
                }

                return ResolveEffectiveWebGLStrategy() != WebGLAssetStrategy.OnDemand;
            }
        }

        /// <inheritdoc />
        IAssetWarmupGroup IAssetStartupWarmupController.CreateLaunchWarmupGroup()
        {
            WebGLAssetStrategy strategy = ResolveEffectiveWebGLStrategy();
            switch (strategy)
            {
                case WebGLAssetStrategy.TagsOnLaunch:
                    return CreateWarmupByTags(NormalizeValues(m_Config.LaunchHotfixTags?.ToArray()));
                case WebGLAssetStrategy.AllOnLaunch:
                    return CreateWarmupAll();
                default:
                    throw new InvalidOperationException("OnDemand 不创建启动 WarmupGroup。");
            }
        }

        /// <inheritdoc />
        async UniTask IAssetStartupWarmupController.CompleteLaunchWarmupAsync(
            IAssetWarmupGroup group,
            bool succeeded,
            CancellationToken ct)
        {
            if (group == null)
            {
                return;
            }

            if (succeeded && ResolveEffectiveWebGLStrategy() == WebGLAssetStrategy.AllOnLaunch)
            {
                m_AllOnLaunchWarmupGroup?.Release();
                m_AllOnLaunchWarmupGroup = group;
                return;
            }

            group.Release();

            if (succeeded)
            {
                // TagsOnLaunch 先释放所有权，但延后到 ProcedureLoadDll 消费完启动 DLL 后再真正卸载。
                // 这样即使目标 Bundle 不具备可复用的 Web Cache，也不会在相邻启动阶段重复请求。
                m_LaunchWarmupCleanupPending = true;
                return;
            }

            await CleanupReleasedWarmupAsync(ct);
        }

        /// <inheritdoc />
        async UniTask IAssetStartupWarmupController.CleanupLaunchWarmupAfterDllAsync(CancellationToken ct)
        {
            if (!m_LaunchWarmupCleanupPending)
            {
                return;
            }

            await CleanupReleasedWarmupAsync(ct);
            m_LaunchWarmupCleanupPending = false;
        }

        /// <summary>
        /// 扫描并卸载已经释放且没有其他引用的启动预热资源；失败只影响本次内存回收。
        /// </summary>
        /// <param name="ct">取消令牌。</param>
        private async UniTask CleanupReleasedWarmupAsync(CancellationToken ct)
        {
            try
            {
                await CleanupAsync(null, ct);
            }
            catch (OperationCanceledException) when (ct.IsCancellationRequested)
            {
                throw;
            }
            catch (Exception e)
            {
                // Warmup Handle 已释放；内存清理失败只影响本次回收，不应触发重复预热。
                Log.Warning(LogTag.Asset, "Warmup Handle 已释放，但 CleanupAsync 执行失败：{0}", e.Message);
            }
        }

        /// <summary>
        /// 返回当前 WebGL 实际策略；TagsOnLaunch 没有有效 Tag 时降级为 OnDemand。
        /// </summary>
        private WebGLAssetStrategy ResolveEffectiveWebGLStrategy()
        {
            WebGLAssetStrategy strategy = m_Config?.WebGLAssetStrategy ?? WebGLAssetStrategy.TagsOnLaunch;
            if (strategy == WebGLAssetStrategy.TagsOnLaunch
                && NormalizeValues(m_Config?.LaunchHotfixTags?.ToArray()).Length == 0)
            {
                return WebGLAssetStrategy.OnDemand;
            }
            return strategy;
        }

        /// <summary>
        /// 清理字符串数组中的空白与重复项，并保留首次出现顺序。
        /// </summary>
        /// <param name="values">待清理字符串数组。</param>
        /// <returns>规范化后的数组。</returns>
        private static string[] NormalizeValues(string[] values)
        {
            if (values == null || values.Length == 0)
            {
                return Array.Empty<string>();
            }

            var seen = new HashSet<string>(StringComparer.Ordinal);
            var result = new List<string>(values.Length);
            for (int i = 0; i < values.Length; i++)
            {
                string value = values[i]?.Trim();
                if (!string.IsNullOrEmpty(value) && seen.Add(value))
                {
                    result.Add(value);
                }
            }
            return result.ToArray();
        }

        /// <summary>
        /// 确认目标包已加载 Manifest，避免把 YooAsset 初始化异常延后到预热执行阶段。
        /// </summary>
        /// <param name="package">目标包名。</param>
        private void EnsureManifestReadyForWarmup(string package)
        {
            if (!m_ManifestLoadedPackages.Contains(package))
            {
                throw new InvalidOperationException(
                    $"创建 WarmupGroup 前必须先完成 LoadManifestAsync。Package={package}");
            }
        }
    }
}
