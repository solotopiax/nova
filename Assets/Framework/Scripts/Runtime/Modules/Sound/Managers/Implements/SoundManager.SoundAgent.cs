/***************************************************************
 * (c) copyright 2026 - 2030, Solotopia
 * All Rights Reserved.
 * -------------------------------------------------------------
 * filename:  SoundManager.SoundAgent.cs
 * author:    taoye
 * created:   2026/4/9
 * descrip:   声音管理器 -- 声音代理
 ***************************************************************/

using UnityEngine;

namespace NovaFramework.Runtime
{
    internal sealed partial class SoundManager : SoundManagerBase
    {
        /// <summary>
        /// 声音代理。
        /// <para>封装单个声音播放实例的状态与控制逻辑。</para>
        /// </summary>
        internal sealed class SoundAgent
        {
            /// <summary>
            /// 声音管理器。
            /// </summary>
            private readonly SoundManager m_SoundManager;

            /// <summary>
            /// 所属声音组。
            /// </summary>
            private readonly SoundGroup m_SoundGroup;
            public SoundGroup SoundGroup => m_SoundGroup;

            /// <summary>
            /// 声音代理辅助器。
            /// </summary>
            private readonly SoundAgentHelper m_SoundAgentHelper;
            public SoundAgentHelper Helper => m_SoundAgentHelper;

            /// <summary>
            /// 声音序列编号。
            /// </summary>
            private int m_SerialID;
            public int SerialID { get => m_SerialID; set => m_SerialID = value; }

            /// <summary>
            /// 声音资源。
            /// </summary>
            private UnityEngine.Object m_SoundAsset;

            /// <summary>
            /// 声音资源句柄（加载时记录，停止时释放）。
            /// </summary>
            private IAssetHandle m_SoundAssetHandle;

            /// <summary>
            /// 声音资源设置时间（Time.realtimeSinceStartup 秒数）。
            /// </summary>
            private float m_SetSoundAssetTime;
            public float SetSoundAssetTime => m_SetSoundAssetTime;

            /// <summary>
            /// 声音组内是否静音。
            /// </summary>
            private bool m_MuteInSoundGroup;
            public bool MuteInSoundGroup
            {
                get => m_MuteInSoundGroup;
                set
                {
                    m_MuteInSoundGroup = value;
                    RefreshMute();
                }
            }

            /// <summary>
            /// 声音组内音量。
            /// </summary>
            private float m_VolumeInSoundGroup;
            public float VolumeInSoundGroup
            {
                get => m_VolumeInSoundGroup;
                set
                {
                    m_VolumeInSoundGroup = value;
                    RefreshVolume();
                }
            }

            /// <summary>
            /// 获取当前是否正在播放。
            /// </summary>
            public bool IsPlaying => m_SoundAgentHelper.IsPlaying;

            /// <summary>
            /// 获取声音长度。
            /// </summary>
            public float Length => m_SoundAgentHelper.Length;

            /// <summary>
            /// 获取或设置播放位置。
            /// </summary>
            public float Time { get => m_SoundAgentHelper.Time; set => m_SoundAgentHelper.Time = value; }

            /// <summary>
            /// 获取是否静音。
            /// </summary>
            public bool Mute => m_SoundAgentHelper.Mute;

            /// <summary>
            /// 获取或设置是否循环播放。
            /// </summary>
            public bool Loop { get => m_SoundAgentHelper.Loop; set => m_SoundAgentHelper.Loop = value; }

            /// <summary>
            /// 获取或设置声音优先级。
            /// </summary>
            public int Priority { get => m_SoundAgentHelper.Priority; set => m_SoundAgentHelper.Priority = value; }

            /// <summary>
            /// 获取音量大小。
            /// </summary>
            public float Volume => m_SoundAgentHelper.Volume;

            /// <summary>
            /// 获取或设置声音音调。
            /// </summary>
            public float Pitch { get => m_SoundAgentHelper.Pitch; set => m_SoundAgentHelper.Pitch = value; }

            /// <summary>
            /// 获取或设置声音立体声声相。
            /// </summary>
            public float PanStereo { get => m_SoundAgentHelper.PanStereo; set => m_SoundAgentHelper.PanStereo = value; }

            /// <summary>
            /// 初始化声音代理的新实例。
            /// </summary>
            /// <param name="soundGroup">所属声音组。</param>
            /// <param name="soundManager">声音管理器。</param>
            /// <param name="soundAgentHelper">声音代理辅助器。</param>
            public SoundAgent(SoundGroup soundGroup, SoundManager soundManager, SoundAgentHelper soundAgentHelper)
            {
                m_SoundGroup = soundGroup;
                m_SoundManager = soundManager;
                m_SoundAgentHelper = soundAgentHelper;
                m_SerialID = 0;
                m_SoundAsset = null;
                m_SoundAssetHandle = null;
                Reset();
            }

            /// <summary>
            /// 播放声音。
            /// </summary>
            /// <param name="fadeInSeconds">声音淡入时间（秒）。</param>
            public void Play(float fadeInSeconds)
            {
                m_SoundAgentHelper.Play(fadeInSeconds);
            }

            /// <summary>
            /// 停止播放声音。
            /// </summary>
            /// <param name="fadeOutSeconds">声音淡出时间（秒）。</param>
            public void Stop(float fadeOutSeconds)
            {
                m_SoundAgentHelper.Stop(fadeOutSeconds);
            }

            /// <summary>
            /// 暂停播放声音。
            /// </summary>
            /// <param name="fadeOutSeconds">声音淡出时间（秒）。</param>
            public void Pause(float fadeOutSeconds)
            {
                m_SoundAgentHelper.Pause(fadeOutSeconds);
            }

            /// <summary>
            /// 恢复播放声音。
            /// </summary>
            /// <param name="fadeInSeconds">声音淡入时间（秒）。</param>
            public void Resume(float fadeInSeconds)
            {
                m_SoundAgentHelper.Resume(fadeInSeconds);
            }

            /// <summary>
            /// 设置声音资源。
            /// </summary>
            /// <param name="soundAsset">声音资源。</param>
            /// <param name="location">资源 location，用于释放时传回 IAssetManager。</param>
            /// <returns>是否设置声音资源成功。</returns>
            public bool SetSoundAsset(UnityEngine.Object soundAsset, IAssetHandle handle)
            {
                Reset();
                m_SoundAsset = soundAsset;
                m_SoundAssetHandle = handle;
                m_SetSoundAssetTime = UnityEngine.Time.realtimeSinceStartup;
                return m_SoundAgentHelper.SetSoundAsset(soundAsset);
            }

            /// <summary>
            /// 重置声音代理。
            /// </summary>
            public void Reset()
            {
                if (m_SoundAsset != null)
                {
                    m_SoundAssetHandle?.Release();
                    m_SoundAssetHandle = null;
                    m_SoundAsset = null;
                }

                m_SetSoundAssetTime = 0f;
                Time = SoundConstant.c_DefaultTime;
                MuteInSoundGroup = SoundConstant.c_DefaultMute;
                Loop = SoundConstant.c_DefaultLoop;
                Priority = SoundConstant.c_DefaultPriority;
                VolumeInSoundGroup = SoundConstant.c_DefaultVolume;
                Pitch = SoundConstant.c_DefaultPitch;
                PanStereo = SoundConstant.c_DefaultPanStereo;
                m_SoundAgentHelper.ResetHelper();
            }

            /// <summary>
            /// 刷新静音状态。
            /// </summary>
            public void RefreshMute()
            {
                m_SoundAgentHelper.Mute = m_SoundGroup.Mute || m_MuteInSoundGroup;
            }

            /// <summary>
            /// 刷新音量。
            /// </summary>
            public void RefreshVolume()
            {
                m_SoundAgentHelper.Volume = m_SoundGroup.Volume * m_VolumeInSoundGroup;
            }

            /// <summary>
            /// 释放声音资源。
            /// </summary>
            public void ReleaseSoundAsset()
            {
                if (m_SoundAsset != null)
                {
                    m_SoundAssetHandle?.Release();
                    m_SoundAssetHandle = null;
                    m_SoundAsset = null;
                }
            }
        }
    }
}
