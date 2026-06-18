/***************************************************************
 * (c) copyright 2026 - 2030, Solotopia
 * All Rights Reserved.
 * -------------------------------------------------------------
 * filename:  BaseDemoView.Methods.cs
 * author:    taoye
 * created:   2026/05/23
 * descrip:   Demo 基础 View — 私有/受保护方法
 ***************************************************************/

using NovaFramework.Runtime;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace NovaFramework.Sdk.Ad.Samples.Runtime
{
    /// <summary>
    /// Demo 基础 View 基类，三段式骨架（TitleBar / InteractionArea / FeedbackArea）。
    /// </summary>
    public partial class BaseDemoView
    {
        /// <summary>
        /// 设置顶部居中标题文本。
        /// </summary>
        /// <param name="text">标题字符串。</param>
        protected void SetTitle(string text)
        {
            if (m_TitleText != null)
            {
                m_TitleText.text = text;
            }
        }

        /// <summary>
        /// 在按钮内部查找名为 ApiHintText 的子 TMP_Text 并赋值 hint。
        /// hint 为空时将该子节点 SetActive(false)；不在运行时创建节点（创建由 Prefab 编辑期完成）。
        /// </summary>
        /// <param name="button">目标按钮。</param>
        /// <param name="hint">API 提示字符串；为空则隐藏提示节点。</param>
        protected void SetButtonApiHint(Button button, string hint)
        {
            if (button == null)
            {
                return;
            }

            Transform hintTransform = button.transform.Find("ApiHintText");
            if (hintTransform == null)
            {
                return;
            }

            if (string.IsNullOrEmpty(hint))
            {
                hintTransform.gameObject.SetActive(false);
                return;
            }

            hintTransform.gameObject.SetActive(true);
            TMP_Text hintTmp = hintTransform.GetComponent<TMP_Text>();
            if (hintTmp != null)
            {
                hintTmp.text = hint;
            }
        }

        /// <summary>
        /// 在 owner TMP 节点下查找名为 ApiHintText 的子 TMP_Text 并赋值 hint。
        /// hint 为空时将该子节点 SetActive(false)；不在运行时创建节点（创建由 Prefab 编辑期完成）。
        /// </summary>
        /// <param name="owner">字段主文本组件。</param>
        /// <param name="hint">API 提示字符串；为空则隐藏提示节点。</param>
        protected void SetFieldApiHint(TMP_Text owner, string hint)
        {
            if (owner == null)
            {
                return;
            }

            Transform hintTransform = owner.transform.Find("ApiHintText");
            if (hintTransform == null)
            {
                return;
            }

            if (string.IsNullOrEmpty(hint))
            {
                hintTransform.gameObject.SetActive(false);
                return;
            }

            hintTransform.gameObject.SetActive(true);
            TMP_Text hintTmp = hintTransform.GetComponent<TMP_Text>();
            if (hintTmp != null)
            {
                hintTmp.text = hint;
            }
        }

        /// <summary>
        /// 向反馈区追加一行日志。
        /// 强制带 "> " 前缀，按 FeedbackLevel 染色，超过 c_MaxFeedbackLines 后 FIFO 移除最旧行。
        /// </summary>
        /// <param name="line">日志内容（不需要手动加 "> " 前缀）。</param>
        /// <param name="level">日志等级，决定颜色显示。</param>
        protected void AppendFeedback(string line, FeedbackLevel level = FeedbackLevel.Info)
        {
            if (m_FeedbackLineTemplate == null || m_FeedbackContent == null)
            {
                return;
            }

            if (m_FeedbackLines.Count >= c_MaxFeedbackLines)
            {
                TextMeshProUGUI oldest = m_FeedbackLines[0];
                m_FeedbackLines.RemoveAt(0);
                Destroy(oldest.gameObject);
            }

            TextMeshProUGUI newLine = Instantiate(m_FeedbackLineTemplate, m_FeedbackContent);
            newLine.gameObject.SetActive(true);
            newLine.text = "> " + line;
            newLine.color = GetFeedbackColor(level);
            m_FeedbackLines.Add(newLine);

            if (m_FeedbackScrollRect != null)
            {
                Canvas.ForceUpdateCanvases();
                m_FeedbackScrollRect.verticalNormalizedPosition = 0f;
            }
        }

        /// <summary>
        /// 清空反馈区所有已追加行。
        /// </summary>
        protected void ClearFeedback()
        {
            for (int i = 0; i < m_FeedbackLines.Count; i++)
            {
                if (m_FeedbackLines[i] != null)
                {
                    Destroy(m_FeedbackLines[i].gameObject);
                }
            }

            m_FeedbackLines.Clear();
        }

        /// <summary>
        /// 关闭按钮点击回调，调用 Nova.UI.CloseUIView 关闭当前视图。
        /// </summary>
        private void OnCloseButtonClick()
        {
            Nova.UI.CloseUIView(this);
        }

        /// <summary>
        /// 清空反馈按钮点击回调。
        /// </summary>
        private void OnClearFeedbackButtonClick()
        {
            ClearFeedback();
        }

        /// <summary>
        /// 将 FeedbackLevel 映射为对应的 Color32 颜色值。
        /// Info=#CCCCCC / Success=#4CAF50 / Warn=#FFB300 / Error=#E53935。
        /// </summary>
        /// <param name="level">反馈等级。</param>
        /// <returns>对应颜色。</returns>
        private static Color GetFeedbackColor(FeedbackLevel level)
        {
            switch (level)
            {
                case FeedbackLevel.Success:
                    return new Color32(0x4C, 0xAF, 0x50, 0xFF);
                case FeedbackLevel.Warn:
                    return new Color32(0xFF, 0xB3, 0x00, 0xFF);
                case FeedbackLevel.Error:
                    return new Color32(0xE5, 0x39, 0x35, 0xFF);
                default:
                    return new Color32(0xCC, 0xCC, 0xCC, 0xFF);
            }
        }
    }
}
