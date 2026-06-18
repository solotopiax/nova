/***************************************************************
 * (c) copyright 2026 - 2030, Solotopia
 * All Rights Reserved.
 * -------------------------------------------------------------
 * filename:  DemoNavTreeNodeView.cs
 * author:    taoye
 * created:   2026/05/22
 * descrip:   演示树导航单节点视图。
 *            职责：将单个 DemoNode 渲染为一行（缩进 + 折叠箭头 + 标题），
 *            通过 Action 委托回调通知父 View，不依赖 Event 模块。
 ***************************************************************/

using System;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace NovaFramework.Samples.Runtime
{
    /// <summary>
    /// 演示树导航单节点视图组件。
    /// 挂载在 DemoNodeItemView.prefab 上，通过 Bind 一次性绑定数据并注册点击回调。
    /// </summary>
    public sealed class DemoNavTreeNodeView : MonoBehaviour
    {
        /// <summary>
        /// 每层缩进的像素宽度，Inspector 可调；默认 36 像素，与正文字号一致以保证错开一个汉字宽度。
        /// </summary>

        [SerializeField] private float m_IndentPerDepth = 36f;

        /// <summary>
        /// 节点收缩时折叠图标的旋转角度（度），展开时固定为 0°，Inspector 可调。
        /// </summary>

        [SerializeField] private float m_FoldedAngle = -90f;

        /// <summary>
        /// 控制左侧缩进宽度的 LayoutElement，按节点深度乘系数设置 preferredWidth。
        /// </summary>

        [SerializeField] private LayoutElement m_Indent;

        /// <summary>
        /// 折叠箭头图标，叶子节点隐藏，分支节点根据 IsExpanded 旋转。
        /// </summary>

        [SerializeField] private Image m_FoldIcon;

        /// <summary>
        /// 节点标题文本（TMP）。
        /// </summary>

        [SerializeField] private TMP_Text m_Title;

        /// <summary>
        /// 整行点击响应按钮。
        /// </summary>

        [SerializeField] private Button m_Button;

        /// <summary>
        /// 当前绑定的数据节点。
        /// </summary>
        private DemoNode m_Node;

        /// <summary>
        /// 绑定数据、注册点击回调，并刷新视觉显示。
        /// </summary>
        /// <param name="node">要绑定的数据节点。</param>
        /// <param name="depth">节点在树中的深度（根节点深度为 0）。</param>
        /// <param name="onClicked">节点被点击时的回调，参数为被点击的节点。</param>
        public void Bind(DemoNode node, int depth, Action<DemoNode> onClicked)
        {
            m_Node = node;

            m_Indent.preferredWidth = depth * m_IndentPerDepth;
            m_Title.text = node.Title;

            if (node.IsLeaf)
            {
                m_FoldIcon.gameObject.SetActive(false);
            }
            else
            {
                m_FoldIcon.gameObject.SetActive(true);
                RefreshFoldIcon();
            }

            m_Button.onClick.RemoveAllListeners();
            m_Button.onClick.AddListener(() => onClicked(m_Node));
        }

        /// <summary>
        /// 获取当前绑定的数据节点，供父 View 在 RefreshVisibility 时读取。
        /// </summary>
        public DemoNode Node => m_Node;

        /// <summary>
        /// 根据 m_Node.IsExpanded 旋转折叠箭头图标（展开 0°，收缩 -90°）。
        /// </summary>
        public void RefreshFoldIcon()
        {
            if (m_FoldIcon == null)
            {
                return;
            }

            float angle = m_Node.IsExpanded ? 0f : m_FoldedAngle;
            m_FoldIcon.rectTransform.localRotation = Quaternion.Euler(0f, 0f, angle);
        }
    }
}
