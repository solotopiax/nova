/***************************************************************
 * (c) copyright 2026 - 2030, Solotopia
 * All Rights Reserved.
 * -------------------------------------------------------------
 * filename:  DemoToastView.cs
 * author:    taoye
 * created:   2026/05/23
 * descrip:   Demo 轻量提示 View — 自动倒计时关闭
 *            职责：展示单行提示文字，经过 m_AutoCloseDuration 秒后自动关闭。
 *            由 DemoUIView / DemoIntegrationUiLocalizationView 等内部 spawn。
 ***************************************************************/

using Cysharp.Threading.Tasks;
using System.Threading;
using TMPro;
using UnityEngine;
using NovaFramework.Runtime;

namespace NovaFramework.Samples.Runtime
{
    /// <summary>
    /// Demo 轻量提示 View，显示单行文字后自动倒计时关闭，不登记 DemoTreeData 叶子。
    /// 由调用方通过 Nova.UI.OpenUIViewAsync 打开，userData 传入提示字符串。
    /// </summary>
    public sealed class DemoToastView : UIView
    {
        /// <summary>
        /// 提示文字显示组件。
        /// </summary>

        [SerializeField] private TextMeshProUGUI m_MessageText;

        /// <summary>
        /// 自动关闭延迟秒数，Inspector 可调，默认 2 秒。
        /// </summary>

        [SerializeField] private float m_AutoCloseDuration = 2f;

        /// <summary>
        /// 当前自动关闭任务的取消令牌源，关闭视图时取消。
        /// </summary>
        private CancellationTokenSource m_AutoCloseCts;

        /// <summary>
        /// 视图打开钩子。
        /// userData 若为字符串则作为提示内容展示；随后启动自动关闭倒计时。
        /// </summary>
        /// <param name="userData">提示字符串，或 null 时显示默认文案。</param>
        public override void OnOpen(object userData)
        {
            base.OnOpen(userData);

            string message = userData as string;
            if (m_MessageText != null)
            {
                m_MessageText.text = string.IsNullOrEmpty(message) ? "Demo Toast" : message;
            }

            m_AutoCloseCts?.Cancel();
            m_AutoCloseCts?.Dispose();
            m_AutoCloseCts = new CancellationTokenSource();
            AutoCloseAsync(m_AutoCloseCts.Token).Forget();
        }

        /// <summary>
        /// 视图关闭钩子，取消自动关闭任务。
        /// </summary>
        /// <param name="isShutdown">是否因视图管理器关闭而触发。</param>
        /// <param name="userData">用户自定义数据，本 View 不使用。</param>
        public override void OnClose(bool isShutdown, object userData)
        {
            m_AutoCloseCts?.Cancel();
            m_AutoCloseCts?.Dispose();
            m_AutoCloseCts = null;
            base.OnClose(isShutdown, userData);
        }

        /// <summary>
        /// 等待 m_AutoCloseDuration 秒后自动关闭 View。
        /// 若期间 View 已被外部关闭则取消令牌生效，不重复关闭。
        /// </summary>
        /// <param name="ct">取消令牌，View 关闭时触发取消。</param>
        private async UniTaskVoid AutoCloseAsync(CancellationToken ct)
        {
            await UniTask.Delay((int)(m_AutoCloseDuration * 1000), cancellationToken: ct);

            if (ct.IsCancellationRequested)
            {
                return;
            }

            Nova.UI.CloseUIView(this);
        }
    }
}
