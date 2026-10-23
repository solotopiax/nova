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
        /// 释放按钮使未完成加载失效并释放当前 Sprite，防止稍后的任务重新显示资源。
        /// </summary>
        private void OnReleaseButtonClick()
        {
            CancelLoad();
            ReleaseCurrentAsset();
        }

        /// <summary>
        /// 每次加载持有自己的 CTS 和代际；成功只交付当前视图，旧任务、取消和异常均释放局部句柄。
        /// </summary>
        private async UniTaskVoid LoadAssetAsync()
        {
            if (this == null || m_IsClosed) return;
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
            var loadCts = new CancellationTokenSource();
            m_LoadCts = loadCts;
            int generation = m_LoadGeneration;
            CancellationToken token = loadCts.Token;
            IAssetHandle<Sprite> handle = null;
            try
            {
                AppendFeedback("Nova.Asset.LoadAsync<Sprite>(\"" + location + "\") -> 加载中...");
                handle = await Nova.Asset.LoadAsync<Sprite>(location, token);
                if (this == null || m_IsClosed || generation != m_LoadGeneration || token.IsCancellationRequested) return;
                Sprite sprite = handle?.Asset;
                if (sprite == null)
                {
                    AppendFeedback("Nova.Asset.LoadAsync<Sprite>(\"" + location + "\") -> 加载失败，返回 null", FeedbackLevel.Error);
                    return;
                }

                m_CurrentHandle = handle;
                handle = null; // 当前视图接管，之后由 ReleaseCurrentAsset 归还。
                if (m_ResultImage != null)
                {
                    m_ResultImage.sprite = sprite;
                    m_ResultImage.gameObject.SetActive(true);
                }
                AppendFeedback("Nova.Asset.LoadAsync<Sprite>(\"" + location + "\") -> loaded " + sprite.texture.width + "x" + sprite.texture.height, FeedbackLevel.Success);
            }
            catch (System.OperationCanceledException)
            {
                if (this != null && !m_IsClosed && generation == m_LoadGeneration)
                    AppendFeedback("Nova.Asset.LoadAsync -> 已取消", FeedbackLevel.Warn);
            }
            catch (System.Exception)
            {
                if (this != null && !m_IsClosed && generation == m_LoadGeneration)
                    AppendFeedback("Nova.Asset.LoadAsync -> 加载异常", FeedbackLevel.Error);
            }
            finally
            {
                try { handle?.Release(); }
                finally
                {
                    if (ReferenceEquals(m_LoadCts, loadCts)) m_LoadCts = null;
                    loadCts.Dispose();
                }
            }
        }

        /// <summary>
        /// 先清空当前句柄与 UI 引用再释放资源；关闭后的清理不再写入反馈区。
        /// </summary>
        private void ReleaseCurrentAsset()
        {
            if (m_CurrentHandle == null)
            {
                return;
            }

            IAssetHandle<Sprite> handle = m_CurrentHandle;
            m_CurrentHandle = null;

            if (m_ResultImage != null)
            {
                m_ResultImage.sprite = null;
                m_ResultImage.gameObject.SetActive(false);
            }
            handle.Release();
            if (!m_IsClosed) AppendFeedback("IAssetHandle.Release() -> ok", FeedbackLevel.Success);
        }

        /// <summary>
        /// 先使旧任务失效再取消；CTS 由创建它的异步任务 finally 释放，避免跨任务读取已释放的源。
        /// </summary>
        private void CancelLoad()
        {
            m_LoadGeneration++;
            CancellationTokenSource pending = m_LoadCts;
            m_LoadCts = null;
            pending?.Cancel();
        }

    }
}
