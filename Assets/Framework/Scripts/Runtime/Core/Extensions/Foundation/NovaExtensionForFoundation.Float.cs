/***************************************************************
 * (c) copyright 2026 - 2030, Solotopia
 * All Rights Reserved.
 * -------------------------------------------------------------
 * filename:  NovaExtensionForFoundation.Float.cs
 * author:    taoye
 * created:   2025/12/2
 * descrip:   框架对C#的扩展方法-Float浮点数
 ***************************************************************/
using UnityEngine;

namespace NovaFramework.Runtime
{
    /// <summary>
    /// 框架对 C#的扩展方法-Float 浮点数。
    /// </summary>
    public static partial class NovaExtensionForFoundation
    {
        /// <summary>
        /// 将浮点数（以秒表示）转换为字符串，可选择显示小时、分钟、秒和毫秒。
        /// </summary>
        /// <param name="t">浮点数时间（单位秒）。</param>
        /// <param name="displayHours">是否显示小时。</param>
        /// <param name="displayMinutes">是否显示分钟。</param>
        /// <param name="displaySeconds">是否显示秒。</param>
        /// <param name="displayMilliseconds">是否显示毫秒。</param>
        /// <returns>格式化后的时间字符串。</returns>
        public static string FloatToTimeString(this float t, bool displayHours = false, bool displayMinutes = true, bool displaySeconds = true, bool displayMilliseconds = false)
        {
            int intTime = (int)t;
            int hours = intTime / 3600;
            int minutes = (intTime % 3600) / 60;
            int seconds = intTime % 60;
            int milliseconds = Mathf.FloorToInt((t * 1000) % 1000);

            if (displayHours && displayMinutes && displaySeconds && displayMilliseconds)
            {
                return string.Format("{0:00}:{1:00}:{2:00}.{3:D3}", hours, minutes, seconds, milliseconds);
            }
            if (!displayHours && displayMinutes && displaySeconds && displayMilliseconds)
            {
                return string.Format("{0:00}:{1:00}.{2:D3}", minutes, seconds, milliseconds);
            }
            if (!displayHours && !displayMinutes && displaySeconds && displayMilliseconds)
            {
                return string.Format("{0:D2}.{1:D3}", seconds, milliseconds);
            }
            if (!displayHours && !displayMinutes && displaySeconds && !displayMilliseconds)
            {
                return string.Format("{0:00}", seconds);
            }
            if (displayHours && displayMinutes && displaySeconds && !displayMilliseconds)
            {
                return string.Format("{0:00}:{1:00}:{2:00}", hours, minutes, seconds);
            }
            if (!displayHours && displayMinutes && displaySeconds && !displayMilliseconds)
            {
                return string.Format("{0:00}:{1:00}", minutes, seconds);
            }

            return string.Empty;
        }
    }
}
