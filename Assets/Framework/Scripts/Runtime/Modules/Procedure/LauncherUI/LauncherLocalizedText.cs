/***************************************************************
 * (c) copyright 2026 - 2030, Solotopia
 * All Rights Reserved.
 * -------------------------------------------------------------
 * filename:  LauncherLocalizedText.cs
 * author:    taoye
 * created:   2026/5/26
 * descrip:   启动期本地化文本条目
 *            持有 TMP_Text 引用与多语言 Key，
 *            由面板统一遍历调 Refresh() 刷新显示。
 ***************************************************************/

using System;
using TMPro;
using UnityEngine;

namespace NovaFramework.Runtime
{
    /// <summary>
    /// 启动期本地化文本条目。
    /// 将一个 TMP_Text 与一个多语言 Key 绑定，
    /// 调用 Refresh() 时通过 LauncherLocalization.GetText 获取文本并写入。
    /// LauncherLocalization 只走 Resources 通道，不依赖资源系统，启动全程均可安全调用。
    /// </summary>
    [Serializable]
    public sealed class LauncherLocalizedText
    {
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
        /// 关联的 TMP_Text 组件（只读）。
        /// </summary>
        public TMP_Text Text => m_Text;

        /// <summary>
        /// 多语言 Key（只读）。
        /// </summary>
        public string LocalizationKey => m_LocalizationKey;

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
