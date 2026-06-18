/***************************************************************
 * (c) copyright 2026 - 2030, Solotopia
 * All Rights Reserved.
 * -------------------------------------------------------------
 * filename:  DemoFrameworkComponentView.cs
 * author:    taoye
 * created:   2026/05/23
 * descrip:   Demo 1.1 — FrameworkComponent 三段生命周期可视化
 *            展示 Awake/Start/OnDestroy 三段生命周期信息卡片，
 *            以及运行时通过 Nova.Self.GetComponentsInChildren 获取子组件清单。
 ***************************************************************/

using System.Text;
using NovaFramework.Runtime;
using UnityEngine;
using UnityEngine.UI;

namespace NovaFramework.Samples.Runtime
{
    /// <summary>
    /// Demo 1.1 FrameworkComponent 三段生命周期可视化 View。
    /// 展示 Awake / Start / OnDestroy 三段生命周期说明卡片，
    /// 以及运行时子组件清单（Nova.Self.GetComponentsInChildren）。
    /// </summary>
    public sealed class DemoFrameworkComponentView : BaseDemoView
    {
        /// <summary>
        /// 「刷新子组件清单」按钮，点击后调用 Nova.Self.GetComponentsInChildren 并输出到反馈区。
        /// </summary>

        [SerializeField] private Button m_RefreshChildrenButton;

        /// <summary>
        /// 初始化钩子，注册按钮事件，设置标题与 API 提示。
        /// </summary>
        /// <param name="userData">用户自定义数据。</param>
        protected override void OnInit(object userData)
        {
            base.OnInit(userData);

            if (m_RefreshChildrenButton != null)
            {
                m_RefreshChildrenButton.onClick.AddListener(OnRefreshChildrenClick);
            }
        }

        /// <summary>
        /// 打开钩子，设置标题、API 副标题，并追加三段生命周期说明。
        /// </summary>
        /// <param name="userData">用户自定义数据。</param>
        public override void OnOpen(object userData)
        {
            base.OnOpen(userData);
            SetTitle("FrameworkComponent 生命周期");
            SetButtonApiHint(m_RefreshChildrenButton, "FrameworkComponent.Awake() / Start() / OnDestroy()");
            AppendLifecycleInfo();
        }

        /// <summary>
        /// 追加三段生命周期说明到反馈区。
        /// </summary>
        private void AppendLifecycleInfo()
        {
            AppendFeedback("Awake: 初始化 Manager（Util.TypeCreator.Create），禁止跨组件获取", FeedbackLevel.Info);
            AppendFeedback("Start: 调用 Manager.Initialize(config)，获取跨组件依赖", FeedbackLevel.Info);
            AppendFeedback("OnDestroy: 调用 Manager.Shutdown()，释放托管/非托管资源", FeedbackLevel.Info);
        }

        /// <summary>
        /// 「刷新子组件清单」点击回调，通过 Nova.Self.GetComponentsInChildren 获取所有子 FrameworkComponent。
        /// </summary>
        private void OnRefreshChildrenClick()
        {
            if (Nova.Self == null)
            {
                AppendFeedback("Nova.Self 未就绪", FeedbackLevel.Error);
                return;
            }

            FrameworkComponent[] children = Nova.Self.GetComponentsInChildren<FrameworkComponent>(true);
            StringBuilder sb = new StringBuilder();
            for (int i = 0; i < children.Length; i++)
            {
                if (i > 0) sb.Append(", ");
                sb.Append(children[i].GetType().Name);
            }
            AppendFeedback($"Nova.Self -> {children.Length} children active: [{sb}]", FeedbackLevel.Success);
        }
    }
}
