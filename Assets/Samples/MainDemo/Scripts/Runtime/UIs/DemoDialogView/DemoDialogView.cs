/***************************************************************
 * (c) copyright 2026 - 2030, Solotopia
 * All Rights Reserved.
 * -------------------------------------------------------------
 * filename:  DemoDialogView.cs
 * author:    taoye
 * created:   2026/05/23
 * descrip:   Demo 确认对话框 View
 *            职责：展示标题 + 消息 + 确认/取消两按钮，
 *            通过 userData 传入 DemoDialogData 配置回调。
 ***************************************************************/

using System;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using NovaFramework.Runtime;

namespace NovaFramework.Samples.Runtime
{
    /// <summary>
    /// Demo 确认对话框 View，不登记 DemoTreeData 叶子，由 DemoUIView 等内部 spawn。
    /// userData 传入 DemoDialogData 以配置标题、消息文本及确认/取消回调。
    /// </summary>
    public sealed class DemoDialogView : UIView
    {
        /// <summary>
        /// 对话框标题文本组件。
        /// </summary>

        [SerializeField] private TextMeshProUGUI m_TitleText;

        /// <summary>
        /// 对话框消息正文文本组件。
        /// </summary>

        [SerializeField] private TextMeshProUGUI m_MessageText;

        /// <summary>
        /// 确认按钮。
        /// </summary>

        [SerializeField] private Button m_ConfirmButton;

        /// <summary>
        /// 取消按钮。
        /// </summary>

        [SerializeField] private Button m_CancelButton;

        /// <summary>
        /// 确认按钮文本组件。
        /// </summary>

        [SerializeField] private TextMeshProUGUI m_ConfirmButtonText;

        /// <summary>
        /// 取消按钮文本组件。
        /// </summary>

        [SerializeField] private TextMeshProUGUI m_CancelButtonText;

        /// <summary>
        /// 当前打开时传入的对话框配置数据，关闭时清空。
        /// </summary>
        private DemoDialogData m_CurrentData;

        /// <summary>
        /// 视图初始化钩子，绑定按钮事件。
        /// </summary>
        /// <param name="userData">用户自定义数据。</param>
        protected override void OnInit(object userData)
        {
            base.OnInit(userData);

            if (m_ConfirmButton != null)
            {
                m_ConfirmButton.onClick.AddListener(OnConfirmButtonClick);
            }

            if (m_CancelButton != null)
            {
                m_CancelButton.onClick.AddListener(OnCancelButtonClick);
            }
        }

        /// <summary>
        /// 视图打开钩子，从 userData 读取 DemoDialogData 配置界面文本与回调。
        /// </summary>
        /// <param name="userData">DemoDialogData 实例，或 null 时使用默认文案。</param>
        public override void OnOpen(object userData)
        {
            base.OnOpen(userData);

            m_CurrentData = userData as DemoDialogData;

            string title = m_CurrentData?.Title ?? "Confirm";
            string message = m_CurrentData?.Message ?? "Are you sure?";
            string confirmLabel = m_CurrentData?.ConfirmLabel ?? "OK";
            string cancelLabel = m_CurrentData?.CancelLabel ?? "Cancel";

            if (m_TitleText != null)
            {
                m_TitleText.text = title;
            }

            if (m_MessageText != null)
            {
                m_MessageText.text = message;
            }

            if (m_ConfirmButtonText != null)
            {
                m_ConfirmButtonText.text = confirmLabel;
            }

            if (m_CancelButtonText != null)
            {
                m_CancelButtonText.text = cancelLabel;
            }
        }

        /// <summary>
        /// 视图关闭钩子，清空当前配置数据引用。
        /// </summary>
        /// <param name="isShutdown">是否因视图管理器关闭而触发。</param>
        /// <param name="userData">用户自定义数据，本 View 不使用。</param>
        public override void OnClose(bool isShutdown, object userData)
        {
            m_CurrentData = null;
            base.OnClose(isShutdown, userData);
        }

        /// <summary>
        /// 确认按钮点击回调，触发 OnConfirm 后关闭视图。
        /// </summary>
        private void OnConfirmButtonClick()
        {
            m_CurrentData?.OnConfirm?.Invoke();
            Nova.UI.CloseUIView(this);
        }

        /// <summary>
        /// 取消按钮点击回调，触发 OnCancel 后关闭视图。
        /// </summary>
        private void OnCancelButtonClick()
        {
            m_CurrentData?.OnCancel?.Invoke();
            Nova.UI.CloseUIView(this);
        }

        /// <summary>
        /// DemoDialogView 打开时传入的配置数据，携带标题、消息及按钮回调。
        /// </summary>
        public sealed class DemoDialogData
        {
            /// <summary>
            /// 对话框标题文字。
            /// </summary>
            public string Title;

            /// <summary>
            /// 对话框消息正文文字。
            /// </summary>
            public string Message;

            /// <summary>
            /// 确认按钮标签，默认 "OK"。
            /// </summary>
            public string ConfirmLabel;

            /// <summary>
            /// 取消按钮标签，默认 "Cancel"。
            /// </summary>
            public string CancelLabel;

            /// <summary>
            /// 用户点击确认按钮时的回调，可为 null。
            /// </summary>
            public Action OnConfirm;

            /// <summary>
            /// 用户点击取消按钮时的回调，可为 null。
            /// </summary>
            public Action OnCancel;
        }
    }
}
