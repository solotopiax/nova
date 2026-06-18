/***************************************************************
 * (c) copyright 2026 - 2030, Solotopia
 * All Rights Reserved.
 * -------------------------------------------------------------
 * filename:  DemoDebugView.cs
 * author:    taoye
 * created:   2026/05/23
 * descrip:   Modules 2.16 — Debug 模块演示视图（交互触发型）。
 *            演示 RuntimeDebugger 调试面板的开关操作及实时 FPS/RAM 展示。
 *            API：RuntimeDebugger.Instance.ShowDebugPanel() / HideDebugPanel() / IsDebugPanelVisible
 *            （DebugComponent 为 RuntimeDebugger 托管入口，无独立 Activate/Deactivate 门面）
 ***************************************************************/

using NovaFramework.Runtime;
using NovaFramework.Runtime;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace NovaFramework.Samples.Runtime
{
    /// <summary>
    /// Debug 模块演示视图，演示 RuntimeDebugger 调试面板开关与实时性能指标展示。
    /// 继承 BaseDemoView 三段式骨架，提供 Toggle 按钮与 FPS/RAM 实时卡片。
    /// 注意：RuntimeDebugger 仅在 DebuggerActiveType 满足条件时初始化，非初始化状态下按钮会提示未就绪。
    /// </summary>
    public sealed class DemoDebugView : BaseDemoView
    {
        /// <summary>
        /// 切换调试面板可见性按钮。
        /// </summary>

        [SerializeField] private Button m_ToggleDebugButton;

        /// <summary>
        /// 调试面板当前状态文本。
        /// </summary>

        [SerializeField] private TextMeshProUGUI m_DebugStateText;

        /// <summary>
        /// DebuggerActiveType 文本（来自 Nova.Debug.DebuggerActiveType）。
        /// </summary>

        [SerializeField] private TextMeshProUGUI m_ActiveTypeText;

        /// <summary>
        /// FPS 实时显示文本。
        /// </summary>

        [SerializeField] private TextMeshProUGUI m_FpsText;

        /// <summary>
        /// RAM 使用量实时显示文本。
        /// </summary>

        [SerializeField] private TextMeshProUGUI m_RamText;

        /// <summary>
        /// FPS 更新计时器。
        /// </summary>
        private float m_FpsDeltaAccum;

        /// <summary>
        /// FPS 更新帧计数器。
        /// </summary>
        private int m_FpsFrameCount;

        /// <summary>
        /// 每次 FPS 刷新的统计间隔（秒）。
        /// </summary>
        private const float c_FpsUpdateInterval = 0.5f;

        /// <summary>
        /// 视图初始化：注册按钮事件，设置标题与 API 副标题。
        /// </summary>
        /// <param name="userData">用户自定义数据。</param>
        protected override void OnInit(object userData)
        {
            base.OnInit(userData);

            SetTitle("Debug 演示");

            if (m_ToggleDebugButton != null)
            {
                m_ToggleDebugButton.onClick.AddListener(OnToggleDebugButtonClick);
                SetButtonApiHint(m_ToggleDebugButton, "RuntimeDebugger.Instance.ShowDebugPanel() / HideDebugPanel()");
            }
        }

        /// <summary>
        /// 视图打开：刷新调试状态卡片，重置 FPS 计时。
        /// </summary>
        /// <param name="userData">用户自定义数据。</param>
        public override void OnOpen(object userData)
        {
            base.OnOpen(userData);

            m_FpsDeltaAccum = 0f;
            m_FpsFrameCount = 0;

            SetFieldApiHint(m_DebugStateText, "RuntimeDebugger.Instance.IsDebugPanelVisible");
            RefreshDebugStateText();
            RefreshActiveTypeText();
        }

        /// <summary>
        /// 每帧更新：累积 FPS 统计并按间隔刷新显示。
        /// </summary>
        public override void OnUpdate()
        {
            m_FpsDeltaAccum += Time.unscaledDeltaTime;
            m_FpsFrameCount++;

            if (m_FpsDeltaAccum >= c_FpsUpdateInterval)
            {
                float fps = m_FpsFrameCount / m_FpsDeltaAccum;
                m_FpsDeltaAccum = 0f;
                m_FpsFrameCount = 0;

                if (m_FpsText != null)
                {
                    m_FpsText.text = $"FPS：{fps:F1}";
                }

                if (m_RamText != null)
                {
                    long ramMB = UnityEngine.Profiling.Profiler.GetTotalReservedMemoryLong() / (1024 * 1024);
                    m_RamText.text = $"RAM：{ramMB} MB";
                }
            }
        }

        /// <summary>
        /// 切换 Debug 面板按钮点击：显示或隐藏 RuntimeDebugger 调试面板。
        /// </summary>
        private void OnToggleDebugButtonClick()
        {
            if (!RuntimeDebugger.IsInitialized)
            {
                AppendFeedback("RuntimeDebugger 未初始化（当前 DebuggerActiveType 不满足激活条件）", FeedbackLevel.Warn);
                return;
            }

            if (RuntimeDebugger.Instance.IsDebugPanelVisible)
            {
                RuntimeDebugger.Instance.HideDebugPanel();
                AppendFeedback("RuntimeDebugger.Instance.HideDebugPanel() -> Panel=Hidden", FeedbackLevel.Success);
            }
            else
            {
                RuntimeDebugger.Instance.ShowDebugPanel(false);
                AppendFeedback("RuntimeDebugger.Instance.ShowDebugPanel() -> Panel=Visible", FeedbackLevel.Success);
            }

            RefreshDebugStateText();
        }

        /// <summary>
        /// 刷新调试面板当前状态文本。
        /// </summary>
        private void RefreshDebugStateText()
        {
            if (m_DebugStateText == null)
            {
                return;
            }

            if (!RuntimeDebugger.IsInitialized)
            {
                m_DebugStateText.text = "RuntimeDebugger：未初始化";
                m_DebugStateText.color = new Color32(0xFF, 0xB3, 0x00, 0xFF);
                return;
            }

            bool visible = RuntimeDebugger.Instance.IsDebugPanelVisible;
            m_DebugStateText.text = visible ? "Panel：Visible" : "Panel：Hidden";
            m_DebugStateText.color = visible ? new Color32(0x4C, 0xAF, 0x50, 0xFF) : new Color32(0xCC, 0xCC, 0xCC, 0xFF);
        }

        /// <summary>
        /// 刷新 DebuggerActiveType 文本。
        /// </summary>
        private void RefreshActiveTypeText()
        {
            if (m_ActiveTypeText == null)
            {
                return;
            }

            if (Nova.Debug == null)
            {
                m_ActiveTypeText.text = "Nova.Debug：不可用";
                return;
            }

            m_ActiveTypeText.text = $"ActiveType：{Nova.Debug.DebuggerActiveType}";
        }
    }
}
