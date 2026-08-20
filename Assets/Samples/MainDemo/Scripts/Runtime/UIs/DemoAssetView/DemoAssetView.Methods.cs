/***************************************************************
 * (c) copyright 2026 - 2030, Solotopia
 * All Rights Reserved.
 * -------------------------------------------------------------
 * filename:  DemoAssetView.Methods.cs
 * author:    taoye
 * created:   2026/05/23
 * descrip:   Modules 2.2 Asset 演示 View — 私有方法
 ***************************************************************/

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

    }
}
