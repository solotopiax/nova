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

using System;
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

        /// <summary>每次打开或关闭递增，旧操作不能写入重新打开的页面。</summary>
        private int m_ViewGeneration;

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

            m_ViewGeneration++;
            CancelRequests();
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
            m_ViewGeneration++;
            try
            {
                CancelRequests();
            }
            finally
            {
                base.OnClose(isShutdown, userData);
            }
        }

        /// <summary>销毁时使旧操作失效，并释放本页的等待令牌。</summary>
        private void OnDestroy()
        {
            m_ViewGeneration++;
            CancelRequests();
        }

        /// <summary>先断开页面所有权；取消回调抛错时仍释放令牌源。</summary>
        private void CancelRequests()
        {
            CancellationTokenSource cancellation = m_Cts;
            m_Cts = null;
            if (cancellation == null) return;
            try
            {
                cancellation.Cancel();
            }
            finally
            {
                cancellation.Dispose();
            }
        }

        /// <summary>操作结果和异常反馈只属于发起时的那一次页面打开。</summary>
        private bool IsCurrentView(CancellationToken token, int generation)
        {
            return this != null && Available && m_Cts != null &&
                   !token.IsCancellationRequested && generation == m_ViewGeneration;
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
            if (!Available || m_Cts == null) return;
            CancellationToken token = m_Cts.Token;
            int generation = m_ViewGeneration;
            if (Nova.App == null)
            {
                AppendFeedback("Nova.App.CheckAsync() -> AppComponent 未初始化", FeedbackLevel.Error);
                return;
            }

            try
            {
                AppVersionResult result = await Nova.App.CheckAsync(token);
                if (!IsCurrentView(token, generation)) return;
                AppendFeedback("Nova.App.CheckAsync() -> Result=" + result, FeedbackLevel.Success);
            }
            catch (OperationCanceledException)
            {
                // 页面关闭只结束本次操作等待，不追加失败反馈。
            }
            catch (Exception exception)
            {
                if (IsCurrentView(token, generation))
                    AppendFeedback("Nova.App.CheckAsync() -> " + exception.Message, FeedbackLevel.Error);
            }
        }

        /// <summary>
        /// 执行 Nova.App.DownloadAsync 并追加反馈。
        /// </summary>
        private async UniTaskVoid DownloadAsync()
        {
            if (!Available || m_Cts == null) return;
            CancellationToken token = m_Cts.Token;
            int generation = m_ViewGeneration;
            if (Nova.App == null)
            {
                AppendFeedback("Nova.App.DownloadAsync() -> AppComponent 未初始化", FeedbackLevel.Error);
                return;
            }

            try
            {
                string downloadPath = await Nova.App.DownloadAsync(token);
                if (!IsCurrentView(token, generation)) return;
                bool success = !string.IsNullOrEmpty(downloadPath);
                FeedbackLevel level = success ? FeedbackLevel.Success : FeedbackLevel.Warn;
                AppendFeedback("Nova.App.DownloadAsync() -> path=" +
                    (success ? downloadPath + "（文件已下载，尚未安装）" : "null（无更新包）"), level);
            }
            catch (OperationCanceledException)
            {
                // 页面关闭只结束本次操作等待，不追加失败反馈。
            }
            catch (Exception exception)
            {
                if (IsCurrentView(token, generation))
                    AppendFeedback("Nova.App.DownloadAsync() -> " + exception.Message, FeedbackLevel.Error);
            }
        }

        /// <summary>
        /// 执行 Nova.App.OpenStoreAsync 并追加反馈。
        /// </summary>
        private async UniTaskVoid OpenStoreAsync()
        {
            if (!Available || m_Cts == null) return;
            CancellationToken token = m_Cts.Token;
            int generation = m_ViewGeneration;
            if (Nova.App == null)
            {
                AppendFeedback("Nova.App.OpenStoreAsync() -> AppComponent 未初始化", FeedbackLevel.Error);
                return;
            }

            try
            {
                bool opened = await Nova.App.OpenStoreAsync(token);
                if (!IsCurrentView(token, generation)) return;
                FeedbackLevel level = opened ? FeedbackLevel.Success : FeedbackLevel.Warn;
                AppendFeedback("Nova.App.OpenStoreAsync() -> opened=" + opened +
                    "（仅表示跳转请求结果，不表示已更新应用）", level);
            }
            catch (OperationCanceledException)
            {
                // 页面关闭只结束本次操作等待，不追加失败反馈。
            }
            catch (Exception exception)
            {
                if (IsCurrentView(token, generation))
                    AppendFeedback("Nova.App.OpenStoreAsync() -> " + exception.Message, FeedbackLevel.Error);
            }
        }
    }
}
