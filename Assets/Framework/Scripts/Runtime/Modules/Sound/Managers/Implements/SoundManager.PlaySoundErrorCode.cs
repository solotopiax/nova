/***************************************************************
 * (c) copyright 2026 - 2030, Solotopia
 * All Rights Reserved.
 * -------------------------------------------------------------
 * filename:  SoundManager.PlaySoundErrorCode.cs
 * author:    taoye
 * created:   2026/4/9
 * descrip:   声音管理器 -- 播放声音错误码
 ***************************************************************/

namespace NovaFramework.Runtime
{
    internal sealed partial class SoundManager : SoundManagerBase
    {
        /// <summary>
        /// 播放声音错误码。
        /// </summary>
        internal enum PlaySoundErrorCode : byte
        {
            /// <summary>
            /// 未知错误。
            /// </summary>
            Unknown = 0,

            /// <summary>
            /// 声音组不存在。
            /// </summary>
            SoundGroupNotExist,

            /// <summary>
            /// 声音组没有足够的声音代理。
            /// </summary>
            SoundGroupHasNotEnoughAgent,

            /// <summary>
            /// 加载资源失败。
            /// </summary>
            LoadAssetFailure,

            /// <summary>
            /// 播放声音因优先级低被忽略。
            /// </summary>
            IgnoredDueToLowPriority,

            /// <summary>
            /// 设置声音资源失败。
            /// </summary>
            SetSoundAssetFailure,
        }
    }
}
