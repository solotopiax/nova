/***************************************************************
 * (c) copyright 2026 - 2030, Solotopia
 * All Rights Reserved.
 * -------------------------------------------------------------
 * filename:  DemoCollectionsView.cs
 * author:    taoye
 * created:   2026/05/23
 * descrip:   Demo 1.7 — NovaLinkedSet 有序去重演示
 *            提供整数输入框 + Add / Remove / Clear 三个按钮，
 *            每次操作后打印当前序列到反馈区。
 ***************************************************************/

using System.Text;
using NovaFramework.Runtime;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace NovaFramework.Samples.Runtime
{
    /// <summary>
    /// Demo 1.7 NovaLinkedSet 有序去重演示 View。
    /// 演示 NovaLinkedSet.Add / Remove / Clear 及有序去重特性。
    /// </summary>
    public sealed class DemoCollectionsView : BaseDemoView
    {
        /// <summary>
        /// 整数输入框，输入要添加或移除的值。
        /// </summary>

        [SerializeField] private TMP_InputField m_ValueInput;

        /// <summary>
        /// 「Add」按钮，添加输入值到集合。
        /// </summary>

        [SerializeField] private Button m_AddButton;

        /// <summary>
        /// 「Remove」按钮，从集合中移除输入值。
        /// </summary>

        [SerializeField] private Button m_RemoveButton;

        /// <summary>
        /// 「Clear」按钮，清空集合。
        /// </summary>

        [SerializeField] private Button m_ClearButton;

        /// <summary>
        /// 演示用有序去重集合实例。
        /// </summary>
        private readonly NovaLinkedSet<int> m_Set = new NovaLinkedSet<int>();

        /// <summary>
        /// 初始化钩子，注册所有按钮事件。
        /// </summary>
        /// <param name="userData">用户自定义数据。</param>
        protected override void OnInit(object userData)
        {
            base.OnInit(userData);

            if (m_AddButton != null) m_AddButton.onClick.AddListener(OnAddClick);
            if (m_RemoveButton != null) m_RemoveButton.onClick.AddListener(OnRemoveClick);
            if (m_ClearButton != null) m_ClearButton.onClick.AddListener(OnClearClick);
        }

        /// <summary>
        /// 打开钩子，设置标题与 API 副标题，并打印初始状态。
        /// </summary>
        /// <param name="userData">用户自定义数据。</param>
        public override void OnOpen(object userData)
        {
            base.OnOpen(userData);
            SetTitle("NovaLinkedSet 有序去重");
            SetButtonApiHint(m_AddButton, "new NovaLinkedSet<int>() / .Add(x)");
            SetButtonApiHint(m_RemoveButton, "NovaLinkedSet.Contains(x) / .Remove(x)");
            SetButtonApiHint(m_ClearButton, "NovaLinkedSet.Clear()");
            m_Set.Clear();
            LogSetStatus("Init");
        }

        /// <summary>
        /// 「Add」点击回调，解析输入整数并添加到集合。
        /// </summary>
        private void OnAddClick()
        {
            if (!TryParseInput(out int value)) return;

            bool added = m_Set.Add(value);
            string status = added ? "added" : "already exists";
            LogSetStatus($"NovaLinkedSet.Add({value}) -> {status}");
        }

        /// <summary>
        /// 「Remove」点击回调，从集合中移除输入值。
        /// </summary>
        private void OnRemoveClick()
        {
            if (!TryParseInput(out int value)) return;

            bool removed = m_Set.Remove(value);
            string status = removed ? "removed" : "not found";
            LogSetStatus($"NovaLinkedSet.Remove({value}) -> {status}");
        }

        /// <summary>
        /// 「Clear」点击回调，清空集合。
        /// </summary>
        private void OnClearClick()
        {
            m_Set.Clear();
            LogSetStatus("NovaLinkedSet.Clear()");
        }

        /// <summary>
        /// 尝试将输入框文本解析为整数。
        /// </summary>
        /// <param name="value">解析结果。</param>
        /// <returns>解析成功返回 true。</returns>
        private bool TryParseInput(out int value)
        {
            string text = m_ValueInput != null ? m_ValueInput.text : string.Empty;
            if (!int.TryParse(text, out value))
            {
                AppendFeedback($"输入 \"{text}\" 不是有效整数", FeedbackLevel.Warn);
                return false;
            }
            return true;
        }

        /// <summary>
        /// 将集合当前状态打印到反馈区。
        /// </summary>
        /// <param name="action">触发动作描述。</param>
        private void LogSetStatus(string action)
        {
            StringBuilder sb = new StringBuilder("[");
            int idx = 0;
            foreach (int v in m_Set)
            {
                if (idx > 0) sb.Append(", ");
                sb.Append(v);
                idx++;
            }
            sb.Append("]");
            AppendFeedback($"{action} -> Count={m_Set.Count} {sb}", FeedbackLevel.Success);
        }
    }
}
