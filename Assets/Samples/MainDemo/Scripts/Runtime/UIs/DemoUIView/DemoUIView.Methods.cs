/***************************************************************
 * (c) copyright 2026 - 2030, Solotopia
 * All Rights Reserved.
 * -------------------------------------------------------------
 * filename:  DemoUIView.Methods.cs
 * author:    taoye
 * created:   2026/05/23
 * descrip:   Modules 2.8 UI 演示 View — 私有方法
 ***************************************************************/

using NovaFramework.Runtime;

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
            if (Nova.UI == null)
            {
                AppendFeedback("Nova.UI.OpenUIViewAsync -> UIComponent 未初始化", FeedbackLevel.Error);
                return;
            }

            int serialID = Nova.UI.OpenUIViewAsync<DemoToastView>("Demo Toast from DemoUIView");
            TrackOpenedView(serialID, "DemoToastView");
        }

        /// <summary>
        /// 打开 DemoDialogView 按钮点击回调，调用 Nova.UI.OpenUIViewAsync 并记录 SerialID。
        /// </summary>
        private void OnOpenDialogButtonClick()
        {
            if (Nova.UI == null)
            {
                AppendFeedback("Nova.UI.OpenUIViewAsync -> UIComponent 未初始化", FeedbackLevel.Error);
                return;
            }

            DemoDialogView.DemoDialogData dialogData = new DemoDialogView.DemoDialogData
            {
                Title = "Demo 确认",
                Message = "这是一个 DemoDialogView 演示对话框。",
                ConfirmLabel = "确认",
                CancelLabel = "取消",
                OnConfirm = OnDialogConfirm,
                OnCancel = OnDialogCancel
            };

            int serialID = Nova.UI.OpenUIViewAsync<DemoDialogView>(dialogData);
            TrackOpenedView(serialID, "DemoDialogView");
        }

        /// <summary>
        /// Close All 按钮点击回调，关闭所有本 View 打开的子 View 并输出活跃 SerialID 变化。
        /// </summary>
        private void OnCloseAllButtonClick()
        {
            int count = m_OpenedSerialIDs.Count;
            CloseAllOpenedViews();
            AppendFeedback("Nova.UI.CloseUIView × " + count + " -> 全部关闭", FeedbackLevel.Success);
            AppendActiveSerialsToFeedback();
        }

        /// <summary>
        /// 记录新打开的子 View SerialID，向反馈区输出当前活跃 SerialID 集合与操作日志。
        /// </summary>
        /// <param name="serialID">OpenUIViewAsync 返回的 SerialID。</param>
        /// <param name="viewName">View 类名，用于反馈日志显示。</param>
        private void TrackOpenedView(int serialID, string viewName)
        {
            m_OpenedSerialIDs.Add(serialID);
            AppendFeedback("Nova.UI.OpenUIViewAsync<" + viewName + ">() -> SerialID=" + serialID, FeedbackLevel.Success);
            AppendActiveSerialsToFeedback();
        }

        /// <summary>
        /// 关闭所有已记录的子 View SerialID，清空列表并向反馈区输出操作结果。
        /// </summary>
        private void CloseAllOpenedViews()
        {
            if (Nova.UI == null)
            {
                m_OpenedSerialIDs.Clear();
                return;
            }

            for (int i = 0; i < m_OpenedSerialIDs.Count; i++)
            {
                int serialID = m_OpenedSerialIDs[i];
                if (Nova.UI.HasUIView(serialID))
                {
                    Nova.UI.CloseUIView(serialID);
                }
            }

            m_OpenedSerialIDs.Clear();
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
