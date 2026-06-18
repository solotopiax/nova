/***************************************************************
 * (c) copyright 2026 - 2030, Solotopia
 * All Rights Reserved.
 * -------------------------------------------------------------
 * filename:  DemoLogView.cs
 * author:    taoye
 * created:   2026/05/23
 * descrip:   Demo 1.5 — Log 日志级别与 LogTag 演示
 *            提供 Debug / Warning / Error 三个按钮，
 *            并附带 LogTag 选择（Base / UI / Event / Component）。
 ***************************************************************/

using NovaFramework.Runtime;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace NovaFramework.Samples.Runtime
{
    /// <summary>
    /// Demo 1.5 Log 日志级别与 LogTag 演示 View。
    /// 演示 Log.Debug / Log.Warning / Log.Error 三个级别，并允许切换 LogTag。
    /// </summary>
    public sealed class DemoLogView : BaseDemoView
    {
        /// <summary>
        /// 「Debug」按钮，触发 Log.Debug。
        /// </summary>

        [SerializeField] private Button m_DebugButton;

        /// <summary>
        /// 「Warning」按钮，触发 Log.Warning。
        /// </summary>

        [SerializeField] private Button m_WarningButton;

        /// <summary>
        /// 「Error」按钮，触发 Log.Error。
        /// </summary>

        [SerializeField] private Button m_ErrorButton;

        /// <summary>
        /// LogTag 下拉选择（Base / UI / Event / Component）。
        /// </summary>

        [SerializeField] private TMP_Dropdown m_TagDropdown;

        /// <summary>
        /// 当前选中的 LogTag 字符串。
        /// </summary>
        private string m_SelectedTag = LogTag.Base;

        /// <summary>
        /// 可选 LogTag 列表（顺序对应下拉 index）。
        /// </summary>
        private static readonly string[] c_Tags = { LogTag.Base, LogTag.UI, LogTag.Event, LogTag.Component };

        /// <summary>
        /// 初始化钩子，注册所有按钮与下拉事件。
        /// </summary>
        /// <param name="userData">用户自定义数据。</param>
        protected override void OnInit(object userData)
        {
            base.OnInit(userData);

            if (m_DebugButton != null)
            {
                m_DebugButton.onClick.AddListener(OnDebugClick);
                SetButtonApiHint(m_DebugButton, "Log.Debug(tag, message)");
            }

            if (m_WarningButton != null)
            {
                m_WarningButton.onClick.AddListener(OnWarningClick);
                SetButtonApiHint(m_WarningButton, "Log.Warning(tag, message)");
            }

            if (m_ErrorButton != null)
            {
                m_ErrorButton.onClick.AddListener(OnErrorClick);
                SetButtonApiHint(m_ErrorButton, "Log.Error(tag, message)");
            }

            if (m_TagDropdown != null)
            {
                m_TagDropdown.onValueChanged.AddListener(OnTagChanged);
            }
        }

        /// <summary>
        /// 打开钩子，设置标题和 API 副标题，重置选中 Tag。
        /// </summary>
        /// <param name="userData">用户自定义数据。</param>
        public override void OnOpen(object userData)
        {
            base.OnOpen(userData);
            SetTitle("Log 日志级别");
            m_SelectedTag = LogTag.Base;
            if (m_TagDropdown != null) m_TagDropdown.value = 0;
            AppendFeedback("Log.Info 在框架层全局禁用，请使用 Debug / Warning / Error", FeedbackLevel.Warn);
        }

        /// <summary>
        /// LogTag 下拉值变更回调。
        /// </summary>
        /// <param name="index">下拉选项 index。</param>
        private void OnTagChanged(int index)
        {
            if (index >= 0 && index < c_Tags.Length)
            {
                m_SelectedTag = c_Tags[index];
            }
        }

        /// <summary>
        /// 「Debug」点击回调，触发 Log.Debug 并输出到反馈区。
        /// </summary>
        private void OnDebugClick()
        {
            Log.Debug(m_SelectedTag, "Demo Log.Debug 测试消息。");
            AppendFeedback($"Log.Debug({m_SelectedTag}, \"Demo Log.Debug 测试消息。\") -> Console", FeedbackLevel.Info);
        }

        /// <summary>
        /// 「Warning」点击回调，触发 Log.Warning 并输出到反馈区。
        /// </summary>
        private void OnWarningClick()
        {
            Log.Warning(m_SelectedTag, "Demo Log.Warning 测试消息。");
            AppendFeedback($"Log.Warning({m_SelectedTag}, \"Demo Log.Warning 测试消息。\") -> Console", FeedbackLevel.Warn);
        }

        /// <summary>
        /// 「Error」点击回调，触发 Log.Error 并输出到反馈区。
        /// </summary>
        private void OnErrorClick()
        {
            Log.Error(m_SelectedTag, "Demo Log.Error 测试消息。");
            AppendFeedback($"Log.Error({m_SelectedTag}, \"Demo Log.Error 测试消息。\") -> Console", FeedbackLevel.Error);
        }
    }
}
