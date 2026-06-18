/***************************************************************
 * (c) copyright 2026 - 2030, Solotopia
 * All Rights Reserved.
 * -------------------------------------------------------------
 * filename:  LauncherProgressPanel.cs
 * author:    taoye
 * created:   2026/3/26
 * descrip:   启动阶段进度面板
 *            进度条 + 整数百分比 + 多语言文本数组，
 *            供热更和预加载共用。
 *            固化在 Resources/BuiltIn/Prefabs/ 中，不参与热更。
 ***************************************************************/

using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace NovaFramework.Runtime
{
    /// <summary>
    /// 启动阶段进度面板。进度条 + 整数百分比 + 多语言文本数组。
    /// 启动期不会切语言，多语言文本在 Awake 和 OnEnable 时一次性刷新即可，无需事件驱动。
    /// </summary>
    public sealed class LauncherProgressPanel : MonoBehaviour
    {
        /// <summary>
        /// 进度条。
        /// </summary>
        [SerializeField]
        private Slider m_ProgressBar;

        /// <summary>
        /// 进度百分比文本（整数，如"42%"）。
        /// </summary>
        [SerializeField]
        private TMP_Text m_ProgressText;

        /// <summary>
        /// 本地化文本条目数组，由业务侧在 Inspector 中拖拽并填写 Key。
        /// </summary>
        [SerializeField]
        private LauncherLocalizedText[] m_LocalizedTexts;

        /// <summary>
        /// 初始化时刷新本地化文本。
        /// </summary>
        private void Awake()
        {
            RefreshAllTexts();
        }

        /// <summary>
        /// 启用时立即刷新文本（应对面板重新激活的场景）。
        /// </summary>
        private void OnEnable()
        {
            RefreshAllTexts();
        }

        /// <summary>
        /// 设置进度值并更新进度条和整数百分比文本。
        /// </summary>
        /// <param name="progress">进度值（0 ~ 1）。</param>
        public void SetProgress(float progress)
        {
            if (m_ProgressBar != null)
            {
                m_ProgressBar.value = Mathf.Clamp01(progress);
            }

            if (m_ProgressText != null)
            {
                m_ProgressText.text = Txt.Format("{0}%", Mathf.RoundToInt(Mathf.Clamp01(progress) * 100f));
            }
        }

        /// <summary>
        /// 遍历本地化文本数组，逐一刷新文本内容。
        /// </summary>
        private void RefreshAllTexts()
        {
            if (m_LocalizedTexts == null)
            {
                return;
            }

            for (int i = 0; i < m_LocalizedTexts.Length; i++)
            {
                m_LocalizedTexts[i]?.Refresh();
            }
        }
    }
}
