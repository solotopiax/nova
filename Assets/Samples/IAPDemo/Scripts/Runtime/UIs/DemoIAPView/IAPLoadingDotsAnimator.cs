/***************************************************************
 * (c) copyright 2026 - 2030, Solotopia
 * All Rights Reserved.
 * -------------------------------------------------------------
 * filename:  IAPLoadingDotsAnimator.cs
 * author:    Codex
 * created:   2026/06/09
 * descrip:   IAP loading 面板三点跳动动画
 ***************************************************************/

using UnityEngine;

namespace NovaFramework.Sdk.IAP.Samples.Runtime
{
    /// <summary>
    /// 驱动 IAP loading 面板中的三个点按固定节奏错峰跳动。
    /// </summary>
    public sealed class IAPLoadingDotsAnimator : MonoBehaviour
    {
        [SerializeField] private RectTransform[] m_Dots;
        [SerializeField] private float m_JumpHeight = 16f;
        [SerializeField] private float m_CycleDuration = 0.9f;

        private Vector2[] m_BasePositions;

        private void OnEnable()
        {
            CacheBasePositions();
        }

        private void OnDisable()
        {
            ResetDots();
        }

        private void Update()
        {
            if (m_Dots == null || m_Dots.Length == 0 || m_CycleDuration <= 0f)
            {
                return;
            }

            if (m_BasePositions == null || m_BasePositions.Length != m_Dots.Length)
            {
                CacheBasePositions();
            }

            float time = Time.unscaledTime;
            for (int i = 0; i < m_Dots.Length; i++)
            {
                RectTransform dot = m_Dots[i];
                if (dot == null)
                {
                    continue;
                }

                float phase = ((time / m_CycleDuration) - (i * 0.22f)) * Mathf.PI * 2f;
                float jump = Mathf.Max(0f, Mathf.Sin(phase)) * m_JumpHeight;
                dot.anchoredPosition = m_BasePositions[i] + new Vector2(0f, jump);
            }
        }

        private void CacheBasePositions()
        {
            if (m_Dots == null)
            {
                m_BasePositions = null;
                return;
            }

            m_BasePositions = new Vector2[m_Dots.Length];
            for (int i = 0; i < m_Dots.Length; i++)
            {
                m_BasePositions[i] = m_Dots[i] != null ? m_Dots[i].anchoredPosition : Vector2.zero;
            }
        }

        private void ResetDots()
        {
            if (m_Dots == null || m_BasePositions == null)
            {
                return;
            }

            int count = Mathf.Min(m_Dots.Length, m_BasePositions.Length);
            for (int i = 0; i < count; i++)
            {
                if (m_Dots[i] != null)
                {
                    m_Dots[i].anchoredPosition = m_BasePositions[i];
                }
            }
        }
    }
}
