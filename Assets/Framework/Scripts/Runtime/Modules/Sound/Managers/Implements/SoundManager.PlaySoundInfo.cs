/***************************************************************
 * (c) copyright 2026 - 2030, Solotopia
 * All Rights Reserved.
 * -------------------------------------------------------------
 * filename:  SoundManager.PlaySoundInfo.cs
 * author:    taoye
 * created:   2026/4/9
 * descrip:   声音管理器 -- 播放声音信息
 ***************************************************************/

namespace NovaFramework.Runtime
{
    internal sealed partial class SoundManager : SoundManagerBase
    {
        /// <summary>
        /// 播放声音信息。
        /// 本类 Clear 时会级联 ReferencePool.Put 持有的 PlaySoundParams
        /// （所有权由外部 PlaySound 调用转入），外部禁止重复 Put。
        /// </summary>
        private sealed class PlaySoundInfo : IReference
        {
            /// <summary>
            /// 声音序列编号。
            /// </summary>
            private int m_SerialID;
            public int SerialID => m_SerialID;

            /// <summary>
            /// 所属声音组。
            /// </summary>
            private SoundGroup m_SoundGroup;
            public SoundGroup SoundGroup => m_SoundGroup;

            /// <summary>
            /// 播放声音参数。
            /// </summary>
            private PlaySoundParams m_PlaySoundParams;
            public PlaySoundParams PlaySoundParams => m_PlaySoundParams;

            /// <summary>
            /// 初始化播放声音信息的新实例。
            /// </summary>
            public PlaySoundInfo()
            {
                m_SerialID = 0;
                m_SoundGroup = null;
                m_PlaySoundParams = null;
            }

            /// <summary>
            /// 创建播放声音信息（从 ReferencePool 获取）。
            /// </summary>
            /// <param name="serialID">声音序列编号。</param>
            /// <param name="soundGroup">所属声音组。</param>
            /// <param name="playSoundParams">播放声音参数。</param>
            /// <returns>播放声音信息实例。</returns>
            public static PlaySoundInfo Create(int serialID, SoundGroup soundGroup, PlaySoundParams playSoundParams)
            {
                PlaySoundInfo playSoundInfo = ReferencePool.Get<PlaySoundInfo>();
                playSoundInfo.m_SerialID = serialID;
                playSoundInfo.m_SoundGroup = soundGroup;
                playSoundInfo.m_PlaySoundParams = playSoundParams;
                return playSoundInfo;
            }

            /// <summary>
            /// 清理引用。
            /// </summary>
            public void Clear()
            {
                m_SerialID = 0;
                m_SoundGroup = null;
                if (m_PlaySoundParams != null)
                {
                    ReferencePool.Put(m_PlaySoundParams);
                    m_PlaySoundParams = null;
                }
            }
        }
    }
}
