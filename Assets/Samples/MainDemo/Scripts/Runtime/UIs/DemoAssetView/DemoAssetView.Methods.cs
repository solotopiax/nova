/***************************************************************
 * (c) copyright 2026 - 2030, Solotopia
 * All Rights Reserved.
 * -------------------------------------------------------------
 * filename:  DemoAssetView.Methods.cs
 * author:    taoye
 * created:   2026/05/23
 * descrip:   Modules 2.2 Asset 演示 View — 私有方法
 ***************************************************************/

using System;
using System.Threading;
using Cysharp.Threading.Tasks;
using NovaFramework.Runtime;
using UnityEngine;

namespace NovaFramework.Samples.Runtime
{
    /// <summary>
    /// Modules 2.2 Asset 演示 View — 私有方法（加载/取消/释放逻辑）。
    /// </summary>
    public sealed partial class DemoAssetView
    {
        /// <summary>
        /// 异步加载按钮点击回调，启动 LoadAsync 流程。
        /// </summary>
        private void OnAsyncButtonClick()
        {
            LoadAssetAsync().Forget();
        }

        /// <summary>
        /// 取消加载按钮点击回调，取消当前进行中的异步加载任务。
        /// </summary>
        private void OnCancelButtonClick()
        {
            CancelLoad();
            AppendFeedback("Nova.Asset.LoadAsync -> 已取消", FeedbackLevel.Warn);
        }

        /// <summary>
        /// 释放资源按钮点击回调，释放当前已加载的 Sprite 并清空展示图。
        /// </summary>
        private void OnReleaseButtonClick()
        {
            ReleaseCurrentAsset();
        }

        /// <summary>
        /// 异步加载 Sprite 资源，成功后更新 Image 展示，失败时写入错误反馈。
        /// </summary>
        private async UniTaskVoid LoadAssetAsync()
        {
            if (Nova.Asset == null)
            {
                AppendFeedback("Nova.Asset.LoadAsync -> AssetComponent 未初始化", FeedbackLevel.Error);
                return;
            }

            string location = m_LocationInput != null ? m_LocationInput.text : "sprite_icon_tree";
            if (string.IsNullOrWhiteSpace(location))
            {
                AppendFeedback("Nova.Asset.LoadAsync -> Asset 地址不能为空", FeedbackLevel.Warn);
                return;
            }

            CancelLoad();
            ReleaseCurrentAsset();
            m_LoadCts = new CancellationTokenSource();

            AppendFeedback("Nova.Asset.LoadAsync<Sprite>(\"" + location + "\") -> 加载中...");

            NovaFramework.Runtime.IAssetHandle<Sprite> handle = null;
            bool cancelled = false;

            try
            {
                handle = await Nova.Asset.LoadAsync<Sprite>(location, m_LoadCts.Token);
            }
            catch (System.OperationCanceledException)
            {
                cancelled = true;
            }

            if (cancelled || m_LoadCts == null || m_LoadCts.IsCancellationRequested)
            {
                handle?.Release();
                AppendFeedback("Nova.Asset.LoadAsync<Sprite>(\"" + location + "\") -> 已取消", FeedbackLevel.Warn);
                return;
            }

            if (handle == null || handle.Asset == null)
            {
                handle?.Release();
                AppendFeedback("Nova.Asset.LoadAsync<Sprite>(\"" + location + "\") -> 加载失败，返回 null", FeedbackLevel.Error);
                return;
            }

            m_CurrentHandle = handle;
            Sprite sprite = handle.Asset;

            if (m_ResultImage != null)
            {
                m_ResultImage.sprite = sprite;
                m_ResultImage.gameObject.SetActive(true);
            }

            AppendFeedback("Nova.Asset.LoadAsync<Sprite>(\"" + location + "\") -> loaded " + sprite.texture.width + "x" + sprite.texture.height, FeedbackLevel.Success);
        }

        /// <summary>
        /// 释放当前已加载的资源，清空展示图并重置 Asset 地址记录。
        /// </summary>
        private void ReleaseCurrentAsset()
        {
            if (m_CurrentHandle == null)
            {
                return;
            }

            m_CurrentHandle.Release();
            AppendFeedback("IAssetHandle.Release() -> ok", FeedbackLevel.Success);
            m_CurrentHandle = null;

            if (m_ResultImage != null)
            {
                m_ResultImage.sprite = null;
                m_ResultImage.gameObject.SetActive(false);
            }
        }

        /// <summary>
        /// 取消当前进行中的异步加载任务并释放取消令牌源。
        /// </summary>
        private void CancelLoad()
        {
            m_LoadCts?.Cancel();
            m_LoadCts?.Dispose();
            m_LoadCts = null;
        }

        /// <summary>
        /// 检查增量下载按钮点击回调，启动三步完整下载流程。
        /// </summary>
        private void OnCheckPatchButtonClick()
        {
            CheckPatchAsync().Forget();
        }

        /// <summary>
        /// 演示无效 Asset 地址跳过按钮点击回调。
        /// </summary>
        private void OnInvalidLocationButtonClick()
        {
            DemoInvalidLocationAsync().Forget();
        }

        /// <summary>
        /// 运行时增量下载三步走演示：
        /// 1. Nova.Asset.RefreshManifestAsync() — 强刷清单；
        /// 2. Nova.Asset.CreateDownloaderByTags(tags) — 按 tag 创建切片下载器；
        /// 3. downloader.RunAsync(ct) — 运行下载。
        /// EditorSimulateMode 下 downloader.IsEmpty=true，反馈区标注需联机/真机才有实际下载。
        /// </summary>
        private async UniTaskVoid CheckPatchAsync()
        {
            if (Nova.Asset == null)
            {
                AppendFeedback("Nova.Asset == null，AssetComponent 未初始化", FeedbackLevel.Error);
                return;
            }

            CancelPatch();
            m_PatchCts = new CancellationTokenSource();
            CancellationToken ct = m_PatchCts.Token;

            AppendFeedback("Nova.Asset.RefreshManifestAsync() -> 刷新中...");
            try
            {
                await Nova.Asset.RefreshManifestAsync(ct: ct);
            }
            catch (OperationCanceledException)
            {
                AppendFeedback("Nova.Asset.RefreshManifestAsync() -> 已取消", FeedbackLevel.Warn);
                return;
            }
            catch (Exception ex)
            {
                AppendFeedback("Nova.Asset.RefreshManifestAsync() -> 失败: " + ex.Message, FeedbackLevel.Error);
                return;
            }

            AppendFeedback("Nova.Asset.RefreshManifestAsync() -> ok", FeedbackLevel.Success);

            IAssetDownloader downloader = Nova.Asset.CreateDownloaderByTags(new[] { "demo_runtime_patch" });
            AppendFeedback(string.Format("CreateDownloaderByTags([\"demo_runtime_patch\"]) -> Scope={0} / 文件数={1}", downloader.Scope, downloader.TotalCount));

            if (downloader.IsEmpty)
            {
                AppendFeedback("downloader.IsEmpty=true — EditorSimulate 下无补丁（联机/真机才有实际下载）", FeedbackLevel.Warn);
                return;
            }

            bool ok = false;
            try
            {
                ok = await downloader.RunAsync(ct);
            }
            catch (OperationCanceledException)
            {
                AppendFeedback("downloader.RunAsync() -> 已取消", FeedbackLevel.Warn);
                return;
            }

            AppendFeedback(string.Format("downloader.RunAsync() -> ok={0}", ok), ok ? FeedbackLevel.Success : FeedbackLevel.Error);
        }

        /// <summary>
        /// 演示 CreateDownloaderByLocations 传入无效 Asset 地址的行为：
        /// 资源系统会对无效 Asset 地址发出 Warning 并跳过，IsEmpty=true 是预期行为。
        /// </summary>
        private async UniTaskVoid DemoInvalidLocationAsync()
        {
            if (Nova.Asset == null)
            {
                AppendFeedback("Nova.Asset == null，AssetComponent 未初始化", FeedbackLevel.Error);
                return;
            }

            AppendFeedback("Nova.Asset.CreateDownloaderByLocations([\"invalid_xyz\"]) -> 创建中...");
            await UniTask.Yield();

            IAssetDownloader downloader = Nova.Asset.CreateDownloaderByLocations(new[] { "invalid_xyz" });
            AppendFeedback(string.Format("CreateDownloaderByLocations([\"invalid_xyz\"]) -> Scope={0} / IsEmpty={1}（无效 Asset 地址被资源系统 Warning 跳过）", downloader.Scope, downloader.IsEmpty), FeedbackLevel.Warn);
        }

        /// <summary>
        /// 取消当前增量下载任务并释放令牌源。
        /// </summary>
        private void CancelPatch()
        {
            m_PatchCts?.Cancel();
            m_PatchCts?.Dispose();
            m_PatchCts = null;
        }
    }
}
