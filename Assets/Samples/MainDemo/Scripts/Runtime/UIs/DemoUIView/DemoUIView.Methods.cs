/***************************************************************
 * (c) copyright 2026 - 2030, Solotopia
 * All Rights Reserved.
 * -------------------------------------------------------------
 * filename:  DemoUIView.Methods.cs
 * author:    taoye
 * created:   2026/05/23
 * descrip:   Modules 2.8 UI 演示 View — 私有方法
 ***************************************************************/

using System;

namespace NovaFramework.Samples.Runtime
{
    /// <summary>
    /// Modules 2.8 UI 演示 View — 私有方法（打开/关闭子 View、列表刷新逻辑）。
    /// </summary>
    public sealed partial class DemoUIView
    {
        /// <summary>
        /// 打开 DemoToastView 按钮点击回调，调用 Nova.UI.OpenUIViewAsync 并记录 SerialID。
        /// </summary>
        private void OnOpenToastButtonClick()
        {
            if (m_IsClosed || !Available) return;
            if (m_OwnerUI == null)
            {
                AppendFeedback("Nova.UI.OpenUIViewAsync -> UIComponent 未初始化", FeedbackLevel.Error);
                return;
            }

            int serialID = m_OwnerUI.OpenUIViewAsync<DemoToastView>("Demo Toast from DemoUIView");
            TrackOpenedView(serialID, "DemoToastView");
        }

        /// <summary>
        /// 打开 DemoDialogView 按钮点击回调，调用 Nova.UI.OpenUIViewAsync 并记录 SerialID。
        /// </summary>
        private void OnOpenDialogButtonClick()
        {
            if (m_IsClosed || !Available) return;
            if (m_OwnerUI == null)
            {
                AppendFeedback("Nova.UI.OpenUIViewAsync -> UIComponent 未初始化", FeedbackLevel.Error);
                return;
            }

            int generation = m_ViewGeneration;
            DemoDialogView.DemoDialogData dialogData = new DemoDialogView.DemoDialogData
            {
                Title = "Demo 确认",
                Message = "这是一个 DemoDialogView 演示对话框。",
                ConfirmLabel = "确认",
                CancelLabel = "取消",
                OnConfirm = () => { if (!m_IsClosed && generation == m_ViewGeneration) OnDialogConfirm(); },
                OnCancel = () => { if (!m_IsClosed && generation == m_ViewGeneration) OnDialogCancel(); }
            };

            int serialID = m_OwnerUI.OpenUIViewAsync<DemoDialogView>(dialogData);
            TrackOpenedView(serialID, "DemoDialogView");
        }

        /// <summary>
        /// Close All 按钮点击回调，关闭所有本 View 打开的子 View 并输出活跃 SerialID 变化。
        /// </summary>
        private void OnCloseAllButtonClick()
        {
            if (!Available) return;
            int count = m_OpenedSerialIDs.Count;
            CloseAllOpenedViews();
            AppendFeedback("Nova.UI.CloseUIView -> 已处理 " + (count - m_OpenedSerialIDs.Count) +
                " 个子页面（已打开的关闭，加载中的取消打开），剩余=" + m_OpenedSerialIDs.Count,
                m_OpenedSerialIDs.Count == 0 ? FeedbackLevel.Info : FeedbackLevel.Error);
            AppendActiveSerialsToFeedback();
            if (m_IsClosed && m_OpenedSerialIDs.Count == 0) OnOpen(null);
        }

        /// <summary>
        /// 记录新打开的子 View SerialID，向反馈区输出当前活跃 SerialID 集合与操作日志。
        /// </summary>
        /// <param name="serialID">OpenUIViewAsync 返回的 SerialID。</param>
        /// <param name="viewName">View 类名，用于反馈日志显示。</param>
        private void TrackOpenedView(int serialID, string viewName)
        {
            if (serialID < 0)
            {
                AppendFeedback("Nova.UI.OpenUIViewAsync<" + viewName + ">() -> 请求被拒绝，SerialID=" + serialID, FeedbackLevel.Error);
                return;
            }
            if (m_OwnerUI == null ||
                (!m_OwnerUI.HasUIView(serialID) && !m_OwnerUI.IsLoadingUIView(serialID)))
            {
                AppendFeedback("Nova.UI.OpenUIViewAsync<" + viewName + ">() -> 请求已结束或页面已关闭，SerialID=" + serialID, FeedbackLevel.Warn);
                return;
            }
            if (!m_OpenedSerialIDs.Contains(serialID)) m_OpenedSerialIDs.Add(serialID);
            bool opened = m_OwnerUI.HasUIView(serialID);
            AppendFeedback("Nova.UI.OpenUIViewAsync<" + viewName + ">() -> SerialID=" + serialID +
                (opened ? "（已打开）" : "（已提交请求，正在加载）"),
                opened ? FeedbackLevel.Success : FeedbackLevel.Info);
            AppendActiveSerialsToFeedback();
        }

        /// <summary>
        /// 关闭所有已记录的子 View SerialID，清空列表并向反馈区输出操作结果。
        /// </summary>
        private void CloseAllOpenedViews()
        {
            if (m_OwnerUI == null)
            {
                if (m_OpenedSerialIDs.Count > 0)
                    AppendFeedback("Nova.UI.CloseUIView -> UIComponent 不可用，无法确认子页面已关闭", FeedbackLevel.Error);
                return;
            }

            int[] serialIDs = m_OpenedSerialIDs.ToArray();
            foreach (int serialID in serialIDs)
            {
                try
                {
                    if (m_OwnerUI.HasUIView(serialID) || m_OwnerUI.IsLoadingUIView(serialID))
                        m_OwnerUI.CloseUIView(serialID);
                    m_OpenedSerialIDs.Remove(serialID);
                }
                catch (Exception exception)
                {
                    AppendFeedback("Nova.UI.CloseUIView(" + serialID + ") -> " + exception.Message, FeedbackLevel.Error);
                }
            }
        }

        /// <summary>只处理本页面仍拥有的异步打开失败。</summary>
        private void OnOpenChildViewFail(int serialID, string assetLocation, string errorMessage)
        {
            if (m_IsClosed || !m_OpenedSerialIDs.Remove(serialID)) return;
            AppendFeedback("Nova.UI.OpenUIViewAsync -> SerialID=" + serialID +
                ", address=" + assetLocation + ", 加载失败：" + errorMessage, FeedbackLevel.Error);
            AppendActiveSerialsToFeedback();
        }

        /// <summary>取消订阅时保留门面引用，随后用于清理旧子页面。</summary>
        private void DetachOpenFailureHandler()
        {
            if (m_OwnerUI != null) m_OwnerUI.OnOpenUIViewFail -= OnOpenChildViewFail;
        }

        /// <summary>
        /// DemoDialogView 确认按钮回调，写入反馈日志。
        /// </summary>
        private void OnDialogConfirm()
        {
            AppendFeedback("DemoDialogView -> 用户点击确认", FeedbackLevel.Success);
        }

        /// <summary>
        /// DemoDialogView 取消按钮回调，写入反馈日志。
        /// </summary>
        private void OnDialogCancel()
        {
            AppendFeedback("DemoDialogView -> 用户点击取消", FeedbackLevel.Warn);
        }

        /// <summary>
        /// 将当前活跃 SerialID 集合以 "当前活跃 SerialIDs: [x, y, z]" 格式追加到反馈区。
        /// </summary>
        private void AppendActiveSerialsToFeedback()
        {
            if (m_OpenedSerialIDs.Count == 0)
            {
                AppendFeedback("当前活跃 SerialIDs: []");
                return;
            }

            System.Text.StringBuilder sb = new System.Text.StringBuilder("[");
            for (int i = 0; i < m_OpenedSerialIDs.Count; i++)
            {
                if (i > 0)
                {
                    sb.Append(", ");
                }
                sb.Append(m_OpenedSerialIDs[i]);
            }
            sb.Append("]");
            AppendFeedback("当前活跃 SerialIDs: " + sb);
        }
    }
}
