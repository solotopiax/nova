/***************************************************************
 * (c) copyright 2026 - 2030, Solotopia
 * All Rights Reserved.
 * -------------------------------------------------------------
 * filename:  DemoAppView.cs
 * author:    taoye
 * created:   2026/05/23
 * descrip:   Modules 2.1 — App 大版本检查链路演示 View（交互型）
 *            职责：演示 Nova.App.CheckAsync / DownloadAsync / OpenStoreAsync 三段链路，
 *            展示当前版本号，并将操作结果追加到反馈区。
 ***************************************************************/

using System.Threading;
using Cysharp.Threading.Tasks;
using NovaFramework.Runtime;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace NovaFramework.Samples.Runtime
{
    /// <summary>
    /// Modules 2.1 App 演示 View（交互型）。
    /// 演示 Nova.App.CheckAsync / DownloadAsync / OpenStoreAsync 三段式大版本检查链路。
    /// 三个按钮分别触发各阶段，结果追加到反馈区。
    /// </summary>
    public sealed class DemoAppView : BaseDemoView
    {
        /// <summary>
        /// 当前版本号展示文本。
        /// </summary>

        [SerializeField] private TextMeshProUGUI m_VersionText;

        /// <summary>
        /// 版本检查按钮，触发 Nova.App.CheckAsync。
        /// </summary>

        [SerializeField] private Button m_CheckButton;

        /// <summary>
        /// 下载更新按钮，触发 Nova.App.DownloadAsync。
        /// </summary>

        [SerializeField] private Button m_DownloadButton;

        /// <summary>
        /// 打开应用商店按钮，触发 Nova.App.OpenStoreAsync。
        /// </summary>

        [SerializeField] private Button m_OpenStoreButton;

        /// <summary>
        /// 当前按钮操作的取消令牌源，用于防止 View 关闭后回调访问已销毁对象。
        /// </summary>
        private CancellationTokenSource m_Cts;

        /// <summary>
        /// 视图初始化钩子，注册按钮事件，设置标题与 API 副标题。
        /// </summary>
        /// <param name="userData">用户自定义数据，本 View 不使用。</param>
        protected override void OnInit(object userData)
        {
            base.OnInit(userData);

            SetTitle("App");

            if (m_CheckButton != null)
            {
                m_CheckButton.onClick.AddListener(OnCheckButtonClick);
                SetButtonApiHint(m_CheckButton, "Nova.App.CheckAsync()");
            }

            if (m_DownloadButton != null)
            {
                m_DownloadButton.onClick.AddListener(OnDownloadButtonClick);
                SetButtonApiHint(m_DownloadButton, "Nova.App.DownloadAsync()");
            }

            if (m_OpenStoreButton != null)
            {
                m_OpenStoreButton.onClick.AddListener(OnOpenStoreButtonClick);
                SetButtonApiHint(m_OpenStoreButton, "Nova.App.OpenStoreAsync()");
            }
        }

        /// <summary>
        /// 视图打开钩子，刷新版本号文本。
        /// </summary>
        /// <param name="userData">用户自定义数据，本 View 不使用。</param>
        public override void OnOpen(object userData)
        {
            base.OnOpen(userData);

            m_Cts?.Cancel();
            m_Cts?.Dispose();
            m_Cts = new CancellationTokenSource();

            if (m_VersionText != null)
            {
                m_VersionText.text = "当前版本：" + Application.version;
            }
        }

        /// <summary>
        /// 视图关闭钩子，取消进行中的异步操作。
        /// </summary>
        /// <param name="isShutdown">是否因视图管理器关闭而触发。</param>
        /// <param name="userData">用户自定义数据。</param>
        public override void OnClose(bool isShutdown, object userData)
        {
            m_Cts?.Cancel();
            m_Cts?.Dispose();
            m_Cts = null;
            base.OnClose(isShutdown, userData);
        }

        /// <summary>
        /// 版本检查按钮点击回调，调用 Nova.App.CheckAsync 并将结果写入反馈区。
        /// </summary>
        private void OnCheckButtonClick()
        {
            CheckAsync().Forget();
        }

        /// <summary>
        /// 下载更新按钮点击回调，调用 Nova.App.DownloadAsync 并将结果写入反馈区。
        /// </summary>
        private void OnDownloadButtonClick()
        {
            DownloadAsync().Forget();
        }

        /// <summary>
        /// 打开应用商店按钮点击回调，调用 Nova.App.OpenStoreAsync 并将结果写入反馈区。
        /// </summary>
        private void OnOpenStoreButtonClick()
        {
            OpenStoreAsync().Forget();
        }

        /// <summary>
        /// 执行 Nova.App.CheckAsync 并追加反馈。
        /// </summary>
        private async UniTaskVoid CheckAsync()
        {
            if (Nova.App == null)
            {
                AppendFeedback("Nova.App.CheckAsync() -> AppComponent 未初始化", FeedbackLevel.Error);
                return;
            }

            AppVersionResult result = await Nova.App.CheckAsync(m_Cts.Token);

            if (m_Cts == null || m_Cts.IsCancellationRequested)
            {
                return;
            }

            AppendFeedback("Nova.App.CheckAsync() -> Result=" + result, FeedbackLevel.Success);
        }

        /// <summary>
        /// 执行 Nova.App.DownloadAsync 并追加反馈。
        /// </summary>
        private async UniTaskVoid DownloadAsync()
        {
            if (Nova.App == null)
            {
                AppendFeedback("Nova.App.DownloadAsync() -> AppComponent 未初始化", FeedbackLevel.Error);
                return;
            }

            string downloadPath = await Nova.App.DownloadAsync(m_Cts.Token);

            if (m_Cts == null || m_Cts.IsCancellationRequested)
            {
                return;
            }

            bool success = !string.IsNullOrEmpty(downloadPath);
            FeedbackLevel level = success ? FeedbackLevel.Success : FeedbackLevel.Warn;
            AppendFeedback("Nova.App.DownloadAsync() -> path=" + (success ? downloadPath : "null（无更新包）"), level);
        }

        /// <summary>
        /// 执行 Nova.App.OpenStoreAsync 并追加反馈。
        /// </summary>
        private async UniTaskVoid OpenStoreAsync()
        {
            if (Nova.App == null)
            {
                AppendFeedback("Nova.App.OpenStoreAsync() -> AppComponent 未初始化", FeedbackLevel.Error);
                return;
            }

            bool opened = await Nova.App.OpenStoreAsync(m_Cts.Token);

            if (m_Cts == null || m_Cts.IsCancellationRequested)
            {
                return;
            }

            FeedbackLevel level = opened ? FeedbackLevel.Success : FeedbackLevel.Warn;
            AppendFeedback("Nova.App.OpenStoreAsync() -> opened=" + opened, level);
        }
    }
}
