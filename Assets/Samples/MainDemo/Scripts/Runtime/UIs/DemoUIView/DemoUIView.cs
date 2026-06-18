/***************************************************************
 * (c) copyright 2026 - 2030, Solotopia
 * All Rights Reserved.
 * -------------------------------------------------------------
 * filename:  DemoUIView.cs
 * author:    taoye
 * created:   2026/05/23
 * descrip:   Modules 2.8 — UI 嵌套打开/关闭演示 View（交互型）
 *            职责：演示 Nova.UI.OpenUIViewAsync<DemoToastView> /
 *            OpenUIViewAsync<DemoDialogView> / CloseUIView，
 *            展示当前已打开的 View SerialID 列表，验证 UIGroup 叠层管理。
 ***************************************************************/

using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

namespace NovaFramework.Samples.Runtime
{
    /// <summary>
    /// Modules 2.8 UI 演示 View（交互型）。
    /// 演示 Nova.UI.OpenUIViewAsync 的泛型重载，支持打开 Toast / Dialog 子 View，
    /// 并提供 Close All 功能展示 CloseUIView 用法。
    /// 子 View 打开/关闭时通过 AppendFeedback 输出当前活跃 SerialID 集合，便于观察 View 生命周期。
    /// </summary>
    public sealed partial class DemoUIView : BaseDemoView
    {
        /// <summary>
        /// 打开 DemoToastView 按钮。
        /// </summary>

        [SerializeField] private Button m_OpenToastButton;

        /// <summary>
        /// 打开 DemoDialogView 按钮。
        /// </summary>

        [SerializeField] private Button m_OpenDialogButton;

        /// <summary>
        /// 关闭所有本 View 打开的子 View 按钮。
        /// </summary>

        [SerializeField] private Button m_CloseAllButton;

        /// <summary>
        /// 本 View 打开的子 View SerialID 列表，关闭时逐一 CloseUIView。
        /// </summary>
        private readonly List<int> m_OpenedSerialIDs = new List<int>();

        /// <summary>
        /// 视图初始化钩子，注册按钮事件，设置标题与各按钮 API 提示。
        /// </summary>
        /// <param name="userData">用户自定义数据，本 View 不使用。</param>
        protected override void OnInit(object userData)
        {
            base.OnInit(userData);

            SetTitle("UI");

            if (m_OpenToastButton != null)
            {
                m_OpenToastButton.onClick.AddListener(OnOpenToastButtonClick);
                SetButtonApiHint(m_OpenToastButton, "Nova.UI.OpenUIViewAsync<DemoToastView>()");
            }

            if (m_OpenDialogButton != null)
            {
                m_OpenDialogButton.onClick.AddListener(OnOpenDialogButtonClick);
                SetButtonApiHint(m_OpenDialogButton, "Nova.UI.OpenUIViewAsync<DemoDialogView>()");
            }

            if (m_CloseAllButton != null)
            {
                m_CloseAllButton.onClick.AddListener(OnCloseAllButtonClick);
                SetButtonApiHint(m_CloseAllButton, "Nova.UI.CloseUIView(serialID)");
            }
        }

        /// <summary>
        /// 视图打开钩子，清空 SerialID 列表并向反馈区输出初始状态。
        /// </summary>
        /// <param name="userData">用户自定义数据，本 View 不使用。</param>
        public override void OnOpen(object userData)
        {
            base.OnOpen(userData);
            m_OpenedSerialIDs.Clear();
            AppendFeedback("当前活跃 SerialIDs: []");
        }

        /// <summary>
        /// 视图关闭钩子，关闭所有本 View 打开的子 View。
        /// </summary>
        /// <param name="isShutdown">是否因视图管理器关闭而触发。</param>
        /// <param name="userData">用户自定义数据。</param>
        public override void OnClose(bool isShutdown, object userData)
        {
            CloseAllOpenedViews();
            base.OnClose(isShutdown, userData);
        }
    }
}
