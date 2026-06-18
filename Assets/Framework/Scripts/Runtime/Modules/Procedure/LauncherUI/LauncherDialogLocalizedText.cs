/***************************************************************
 * (c) copyright 2026 - 2030, Solotopia
 * All Rights Reserved.
 * -------------------------------------------------------------
 * filename:  LauncherDialogLocalizedText.cs
 * author:    taoye
 * created:   2026/5/26
 * descrip:   启动期弹窗本地化文本条目
 *            在 LauncherLocalizedText 基础上增加 LauncherDialogType 维度，
 *            由 LauncherDialogPanel 按当前弹窗类型激活/隐藏对应行。
 ***************************************************************/

using System;
using TMPro;
using UnityEngine;

namespace NovaFramework.Runtime
{
    /// <summary>
    /// 启动期弹窗本地化文本条目。
    /// 关联一个 LauncherDialogType、一个 TMP_Text 和一个多语言 Key；
    /// 由 LauncherDialogPanel 遍历调 ApplyByType() 根据当前弹窗类型决定激活或隐藏。
    /// 文本通过 LauncherLocalization.GetText 获取，只走 Resources 通道，不依赖资源系统。
    /// </summary>
    [Serializable]
    public sealed class LauncherDialogLocalizedText
    {
        /// <summary>
        /// 该条目所属的弹窗类型。
        /// </summary>
        [SerializeField]
        private LauncherDialogType m_DialogType;

        /// <summary>
        /// 关联的 TMP_Text 组件。
        /// </summary>
        [SerializeField]
        private TMP_Text m_Text;

        /// <summary>
        /// 多语言 Key，对应 LocalizationTexts_{language}.json 中的 Name 字段。
        /// </summary>
        [SerializeField]
        private string m_LocalizationKey;

        /// <summary>
        /// 该条目所属的弹窗类型（只读）。
        /// </summary>
        public LauncherDialogType DialogType => m_DialogType;

        /// <summary>
        /// 关联的 TMP_Text 组件（只读）。
        /// </summary>
        public TMP_Text Text => m_Text;

        /// <summary>
        /// 多语言 Key（只读）。
        /// </summary>
        public string LocalizationKey => m_LocalizationKey;

        /// <summary>
        /// 根据当前激活的弹窗类型决定激活或隐藏本条目文本，并在激活时刷新文本内容。
        /// 取消按钮的显隐由 LauncherDialogPanel 自行按 onCancel 是否 null 决定，本方法不处理。
        /// </summary>
        /// <param name="activeType">当前显示的弹窗类型。</param>
        /// <param name="hasCancelAction">是否存在取消回调（当前未使用，预留扩展）。</param>
        public void ApplyByType(LauncherDialogType activeType, bool hasCancelAction)
        {
            _ = hasCancelAction;
            if (m_Text == null)
            {
                return;
            }

            if (m_DialogType == activeType)
            {
                m_Text.gameObject.SetActive(true);
                Refresh();
            }
            else
            {
                m_Text.gameObject.SetActive(false);
            }
        }

        /// <summary>
        /// 用当前语言刷新文本内容。
        /// 若 m_Text 或 m_LocalizationKey 为空则跳过；
        /// 未命中时 LauncherLocalization.GetText 返回 key 本身作为兜底。
        /// </summary>
        public void Refresh()
        {
            if (m_Text == null || string.IsNullOrEmpty(m_LocalizationKey))
            {
                return;
            }

            m_Text.text = LauncherLocalization.GetText(m_LocalizationKey);
        }
    }
}
