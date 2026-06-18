/***************************************************************
 * (c) copyright 2026 - 2030, Solotopia
 * All Rights Reserved.
 * -------------------------------------------------------------
 * filename:  LauncherDialogPanel.cs
 * author:    taoye
 * created:   2026/3/26
 * descrip:   启动阶段通用弹窗面板
 *            确认/取消按钮 + 多语言文本数组（按 LauncherDialogType 维度激活），
 *            覆盖大版本更新提示、热更失败提示、网络异常提示等场景。
 *            固化在 Resources/BuiltIn/Prefabs/ 中，不参与热更。
 ***************************************************************/

using System;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace NovaFramework.Runtime
{
    /// <summary>
    /// 启动阶段通用弹窗面板。确认/取消按钮 + 多语言文本数组（按 LauncherDialogType 维度激活）。
    /// 文本内容由业务侧在 Inspector 中配置 LauncherDialogLocalizedText 数组和 Key；
    /// Show() 时根据传入的 dialogType 激活/隐藏对应行并一次性刷新语言。
    /// 启动期不会切语言，无需事件驱动，Show() 调用即刷新。
    /// </summary>
    public sealed class LauncherDialogPanel : MonoBehaviour
    {
        /// <summary>
        /// 确认按钮。
        /// </summary>
        [SerializeField]
        private Button m_ConfirmButton;

        /// <summary>
        /// 取消按钮。
        /// </summary>
        [SerializeField]
        private Button m_CancelButton;

        /// <summary>
        /// 弹窗本地化文本条目数组，由业务侧在 Inspector 中配置。
        /// 每条关联一个 LauncherDialogType、TMP_Text 和多语言 Key。
        /// </summary>
        [SerializeField]
        private LauncherDialogLocalizedText[] m_LocalizedTexts;

        /// <summary>
        /// 当前显示的弹窗类型。
        /// </summary>
        private LauncherDialogType m_CurrentType;

        /// <summary>
        /// 是否存在取消回调（当前弹窗是否显示取消按钮）。
        /// </summary>
        private bool m_HasCancelAction;

        /// <summary>
        /// 显示弹窗，根据 dialogType 激活对应文本行并绑定按钮回调。
        /// 取消按钮在 onCancel 为 null 时隐藏。
        /// </summary>
        /// <param name="type">弹窗类型，决定激活哪些文本行。</param>
        /// <param name="onConfirm">确认回调。</param>
        /// <param name="onCancel">取消回调（为 null 时隐藏取消按钮）。</param>
        public void Show(LauncherDialogType type, Action onConfirm, Action onCancel = null)
        {
            m_CurrentType = type;
            m_HasCancelAction = onCancel != null;

            RefreshAllTexts();

            if (m_ConfirmButton != null)
            {
                m_ConfirmButton.onClick.RemoveAllListeners();
                if (onConfirm != null)
                {
                    m_ConfirmButton.onClick.AddListener(() => onConfirm());
                }
            }

            if (m_CancelButton != null)
            {
                m_CancelButton.onClick.RemoveAllListeners();
                m_CancelButton.gameObject.SetActive(onCancel != null);
                if (onCancel != null)
                {
                    m_CancelButton.onClick.AddListener(() => onCancel());
                }
            }

            gameObject.SetActive(true);
        }

        /// <summary>
        /// 隐藏弹窗（不销毁）。
        /// </summary>
        public void Hide()
        {
            gameObject.SetActive(false);
        }

        /// <summary>
        /// 遍历本地化文本数组，逐一按当前弹窗类型激活/隐藏并刷新文本。
        /// </summary>
        private void RefreshAllTexts()
        {
            if (m_LocalizedTexts == null)
            {
                return;
            }

            HashSet<TMP_Text> uniqueTexts = new HashSet<TMP_Text>();
            for (int i = 0; i < m_LocalizedTexts.Length; i++)
            {
                TMP_Text text = m_LocalizedTexts[i]?.Text;
                if (text != null && uniqueTexts.Add(text))
                {
                    text.gameObject.SetActive(false);
                }
            }

            for (int i = 0; i < m_LocalizedTexts.Length; i++)
            {
                LauncherDialogLocalizedText entry = m_LocalizedTexts[i];
                if (entry == null || entry.Text == null || entry.DialogType != m_CurrentType)
                {
                    continue;
                }

                entry.Text.gameObject.SetActive(true);
                entry.Refresh();
            }
        }
    }
}
