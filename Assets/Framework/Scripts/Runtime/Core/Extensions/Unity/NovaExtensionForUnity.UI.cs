/***************************************************************
 * (c) copyright 2026 - 2030, Solotopia
 * All Rights Reserved.
 * -------------------------------------------------------------
 * filename:  NovaExtensionForUnity.UI.cs
 * author:    taoye
 * created:   2026/02/28
 * descrip:   框架对Unity的扩展方法-UI
 *            提供 CanvasGroup、Slider 等 UI 组件的动画协程扩展
 ***************************************************************/

using System.Collections;
using UnityEngine;
using UnityEngine.UI;

namespace NovaFramework.Runtime
{
    /// <summary>
    /// 框架对 Unity UI 组件的扩展方法。
    /// </summary>
    public static partial class NovaExtensionForUnity
    {
        /// <summary>
        /// 在指定时间内将 CanvasGroup 的 alpha 渐变到目标值。
        /// </summary>
        /// <param name="canvasGroup">目标 CanvasGroup。</param>
        /// <param name="alpha">目标 alpha 值（0~1）。</param>
        /// <param name="duration">渐变持续时间（秒）。</param>
        /// <returns>协程枚举器。</returns>
        public static IEnumerator FadeToAlpha(this CanvasGroup canvasGroup, float alpha, float duration)
        {
            float time = 0f;
            float originalAlpha = canvasGroup.alpha;
            while (time < duration)
            {
                time += Time.deltaTime;
                canvasGroup.alpha = Mathf.Lerp(originalAlpha, alpha, time / duration);
                yield return null;
            }

            canvasGroup.alpha = alpha;
        }

        /// <summary>
        /// 在指定时间内将 Slider 的值平滑过渡到目标值。
        /// </summary>
        /// <param name="slider">目标 Slider。</param>
        /// <param name="value">目标值。</param>
        /// <param name="duration">过渡持续时间（秒）。</param>
        /// <returns>协程枚举器。</returns>
        public static IEnumerator SmoothValue(this Slider slider, float value, float duration)
        {
            float time = 0f;
            float originalValue = slider.value;
            while (time < duration)
            {
                time += Time.deltaTime;
                slider.value = Mathf.Lerp(originalValue, value, time / duration);
                yield return null;
            }

            slider.value = value;
        }

    }
}
