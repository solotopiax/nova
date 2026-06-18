/***************************************************************
 * (c) copyright 2026 - 2030, Solotopia
 * All Rights Reserved.
 * -------------------------------------------------------------
 * filename:  SoundConstant.cs
 * author:    taoye
 * created:   2026/4/9
 * descrip:   声音常量定义
 ***************************************************************/

namespace NovaFramework.Runtime
{
    /// <summary>
    /// 声音常量定义。
    /// </summary>
    internal static class SoundConstant
    {
        /// <summary>
        /// 默认播放位置（秒）。
        /// </summary>
        internal const float c_DefaultTime = 0f;

        /// <summary>
        /// 默认静音状态。
        /// </summary>
        internal const bool c_DefaultMute = false;

        /// <summary>
        /// 默认循环播放状态。
        /// </summary>
        internal const bool c_DefaultLoop = false;

        /// <summary>
        /// 默认声音优先级。
        /// </summary>
        internal const int c_DefaultPriority = 0;

        /// <summary>
        /// 默认音量。
        /// </summary>
        internal const float c_DefaultVolume = 1f;

        /// <summary>
        /// 默认淡入时间（秒）。
        /// </summary>
        internal const float c_DefaultFadeInSeconds = 0f;

        /// <summary>
        /// 默认淡出时间（秒）。
        /// </summary>
        internal const float c_DefaultFadeOutSeconds = 0f;

        /// <summary>
        /// 默认音调。
        /// </summary>
        internal const float c_DefaultPitch = 1f;

        /// <summary>
        /// 默认立体声声相。
        /// </summary>
        internal const float c_DefaultPanStereo = 0f;
    }
}
