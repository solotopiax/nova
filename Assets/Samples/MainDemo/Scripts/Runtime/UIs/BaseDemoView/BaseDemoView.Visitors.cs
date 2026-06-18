/***************************************************************
 * (c) copyright 2026 - 2030, Solotopia
 * All Rights Reserved.
 * -------------------------------------------------------------
 * filename:  BaseDemoView.Visitors.cs
 * author:    taoye
 * created:   2026/05/23
 * descrip:   Demo 基础 View — 字段与属性
 ***************************************************************/

using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace NovaFramework.Samples.Runtime
{
    /// <summary>
    /// Demo 基础 View 基类，三段式骨架（TitleBar / InteractionArea / FeedbackArea）。
    /// </summary>
    public partial class BaseDemoView
    {
        /// <summary>
        /// 顶部居中标题文本组件。
        /// </summary>

        [SerializeField] protected TextMeshProUGUI m_TitleText;

        /// <summary>
        /// 右上角关闭按钮，点击后调 Nova.UI.CloseUIView。
        /// </summary>

        [SerializeField] protected Button m_CloseButton;

        /// <summary>
        /// 中部交互区根节点（ScrollRect content），子类在此挂载交互元素。
        /// </summary>

        [SerializeField] protected RectTransform m_InteractionRoot;

        /// <summary>
        /// 底部反馈区 content 节点（VerticalLayoutGroup 宿主），反馈行实例化到此节点下。
        /// </summary>

        [SerializeField] protected RectTransform m_FeedbackContent;

        /// <summary>
        /// 反馈单行模板（disable 状态），Instantiate 后启用作为新反馈行。
        /// </summary>

        [SerializeField] protected TextMeshProUGUI m_FeedbackLineTemplate;

        /// <summary>
        /// 反馈区清空按钮，点击后调 ClearFeedback。
        /// </summary>

        [SerializeField] protected Button m_ClearFeedbackButton;

        /// <summary>
        /// 反馈区滚动容器，AppendFeedback 后自动滚至底部最新行。
        /// </summary>

        [SerializeField] protected ScrollRect m_FeedbackScrollRect;

        /// <summary>
        /// 反馈区最大保留行数，超出后 FIFO 销毁最旧行，防止无限增长。
        /// </summary>
        private const int c_MaxFeedbackLines = 200;

        /// <summary>
        /// 已实例化的反馈行列表，用于 FIFO 剔除管理。
        /// </summary>
        private readonly List<TextMeshProUGUI> m_FeedbackLines = new List<TextMeshProUGUI>();

        /// <summary>
        /// 子类挂载交互元素的根节点（暴露 m_InteractionRoot）。
        /// </summary>
        public RectTransform InteractionRoot => m_InteractionRoot;
    }
}
