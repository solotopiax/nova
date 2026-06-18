/***************************************************************
 * (c) copyright 2026 - 2030, Solotopia
 * All Rights Reserved.
 * -------------------------------------------------------------
 * filename:  PlaySoundParams.cs
 * author:    taoye
 * created:   2026/4/9
 * descrip:   播放声音参数
 ***************************************************************/

namespace NovaFramework.Runtime
{
    /// <summary>
    /// 播放声音参数。
    /// 🔴 红线：本类是 IReference。一经传入 ISoundManager.PlaySound，所有权转移到内部
    /// PlaySoundInfo，由其 Clear 时级联归还。调用方禁止再持有该实例或重复
    /// ReferencePool.Put，否则会触发引用池"重复回收"异常。
    /// </summary>
    public sealed class PlaySoundParams : IReference
    {
        /// <summary>
        /// 播放位置（秒）。
        /// </summary>
        private float m_Time;
        public float Time { get => m_Time; set => m_Time = value; }

        /// <summary>
        /// 在声音组内是否静音。
        /// </summary>
        private bool m_MuteInSoundGroup;
        public bool MuteInSoundGroup { get => m_MuteInSoundGroup; set => m_MuteInSoundGroup = value; }

        /// <summary>
        /// 是否循环播放。
        /// </summary>
        private bool m_Loop;
        public bool Loop { get => m_Loop; set => m_Loop = value; }

        /// <summary>
        /// 声音优先级。
        /// </summary>
        private int m_Priority;
        public int Priority { get => m_Priority; set => m_Priority = value; }

        /// <summary>
        /// 在声音组内音量大小。
        /// </summary>
        private float m_VolumeInSoundGroup;
        public float VolumeInSoundGroup { get => m_VolumeInSoundGroup; set => m_VolumeInSoundGroup = value; }

        /// <summary>
        /// 声音淡入时间（秒）。
        /// </summary>
        private float m_FadeInSeconds;
        public float FadeInSeconds { get => m_FadeInSeconds; set => m_FadeInSeconds = value; }

        /// <summary>
        /// 声音音调。
        /// </summary>
        private float m_Pitch;
        public float Pitch { get => m_Pitch; set => m_Pitch = value; }

        /// <summary>
        /// 声音立体声声相。
        /// </summary>
        private float m_PanStereo;
        public float PanStereo { get => m_PanStereo; set => m_PanStereo = value; }

        /// <summary>
        /// 初始化播放声音参数的新实例。
        /// </summary>
        public PlaySoundParams()
        {
            m_Time = SoundConstant.c_DefaultTime;
            m_MuteInSoundGroup = SoundConstant.c_DefaultMute;
            m_Loop = SoundConstant.c_DefaultLoop;
            m_Priority = SoundConstant.c_DefaultPriority;
            m_VolumeInSoundGroup = SoundConstant.c_DefaultVolume;
            m_FadeInSeconds = SoundConstant.c_DefaultFadeInSeconds;
            m_Pitch = SoundConstant.c_DefaultPitch;
            m_PanStereo = SoundConstant.c_DefaultPanStereo;
        }

        /// <summary>
        /// 创建播放声音参数（从 ReferencePool 获取）。
        /// </summary>
        /// <returns>播放声音参数实例。</returns>
        public static PlaySoundParams Create()
        {
            return ReferencePool.Get<PlaySoundParams>();
        }

        /// <summary>
        /// 清理引用。
        /// </summary>
        public void Clear()
        {
            m_Time = SoundConstant.c_DefaultTime;
            m_MuteInSoundGroup = SoundConstant.c_DefaultMute;
            m_Loop = SoundConstant.c_DefaultLoop;
            m_Priority = SoundConstant.c_DefaultPriority;
            m_VolumeInSoundGroup = SoundConstant.c_DefaultVolume;
            m_FadeInSeconds = SoundConstant.c_DefaultFadeInSeconds;
            m_Pitch = SoundConstant.c_DefaultPitch;
            m_PanStereo = SoundConstant.c_DefaultPanStereo;
        }
    }
}
