/***************************************************************
 * (c) copyright 2026 - 2030, Solotopia
 * All Rights Reserved.
 * -------------------------------------------------------------
 * filename:  SoundManager.SoundGroup.cs
 * author:    taoye
 * created:   2026/4/9
 * descrip:   声音管理器 -- 声音组
 ***************************************************************/

using System;
using System.Collections.Generic;

namespace NovaFramework.Runtime
{
    internal sealed partial class SoundManager : SoundManagerBase
    {
        /// <summary>
        /// 声音组。
        /// <para>管理一组 SoundAgent，提供播放/停止/暂停/恢复等操作。</para>
        /// </summary>
        internal sealed class SoundGroup
        {
            /// <summary>
            /// 声音组名称。
            /// </summary>
            private readonly string m_Name;
            public string Name => m_Name;

            /// <summary>
            /// 声音组辅助器。
            /// </summary>
            private readonly SoundGroupHelper m_SoundGroupHelper;
            public SoundGroupHelper SoundGroupHelper => m_SoundGroupHelper;

            /// <summary>
            /// 声音代理集合。
            /// </summary>
            private readonly List<SoundAgent> m_SoundAgents;

            /// <summary>
            /// 获取声音代理数量。
            /// </summary>
            public int SoundAgentCount => m_SoundAgents.Count;

            /// <summary>
            /// 是否避免被同优先级声音替换。
            /// </summary>
            private bool m_AvoidBeingReplacedBySamePriority;
            public bool AvoidBeingReplacedBySamePriority { get => m_AvoidBeingReplacedBySamePriority; set => m_AvoidBeingReplacedBySamePriority = value; }

            /// <summary>
            /// 声音组是否静音。
            /// </summary>
            private bool m_Mute;
            public bool Mute
            {
                get => m_Mute;
                set
                {
                    m_Mute = value;
                    foreach (SoundAgent soundAgent in m_SoundAgents)
                    {
                        soundAgent.RefreshMute();
                    }
                }
            }

            /// <summary>
            /// 当一个正在播放的声音代理被新请求替换时触发，参数为被替换声音的序列编号。
            /// </summary>
            public Action<int> OnSoundReplaced;

            /// <summary>
            /// 声音组音量。
            /// </summary>
            private float m_Volume;
            public float Volume
            {
                get => m_Volume;
                set
                {
                    m_Volume = value;
                    foreach (SoundAgent soundAgent in m_SoundAgents)
                    {
                        soundAgent.RefreshVolume();
                    }
                }
            }

            /// <summary>
            /// 初始化声音组的新实例。
            /// </summary>
            /// <param name="name">声音组名称。</param>
            /// <param name="soundGroupHelper">声音组辅助器。</param>
            public SoundGroup(string name, SoundGroupHelper soundGroupHelper)
            {
                m_Name = name;
                m_SoundGroupHelper = soundGroupHelper;
                m_SoundAgents = new List<SoundAgent>();
            }

            /// <summary>
            /// 增加声音代理。
            /// </summary>
            /// <param name="soundManager">声音管理器。</param>
            /// <param name="soundAgentHelper">声音代理辅助器。</param>
            public void AddSoundAgent(SoundManager soundManager, SoundAgentHelper soundAgentHelper)
            {
                m_SoundAgents.Add(new SoundAgent(this, soundManager, soundAgentHelper));
            }

            /// <summary>
            /// 播放声音。
            /// </summary>
            /// <param name="serialID">声音的序列编号。</param>
            /// <param name="soundAsset">声音资源。</param>
            /// <param name="handle">声音资源句柄，所有权转入 SoundAgent，停止时由 SoundAgent 自动 Release。</param>
            /// <param name="playSoundParams">播放声音参数。</param>
            /// <param name="errorCode">错误码。</param>
            /// <returns>用于播放的声音代理，失败时返回 null。</returns>
            public SoundAgent PlaySound(int serialID, UnityEngine.Object soundAsset, IAssetHandle handle, PlaySoundParams playSoundParams, out PlaySoundErrorCode? errorCode)
            {
                errorCode = null;
                SoundAgent candidateAgent = null;
                int busyCount = 0;

                foreach (SoundAgent soundAgent in m_SoundAgents)
                {
                    if (!soundAgent.IsPlaying)
                    {
                        candidateAgent = soundAgent;
                        break;
                    }

                    busyCount++;

                    if (soundAgent.Priority < playSoundParams.Priority)
                    {
                        if (candidateAgent == null || soundAgent.Priority < candidateAgent.Priority)
                        {
                            candidateAgent = soundAgent;
                        }
                    }
                    else if (!m_AvoidBeingReplacedBySamePriority && soundAgent.Priority == playSoundParams.Priority)
                    {
                        if (candidateAgent == null || soundAgent.SetSoundAssetTime < candidateAgent.SetSoundAssetTime)
                        {
                            candidateAgent = soundAgent;
                        }
                    }
                }

                if (candidateAgent == null)
                {
                    errorCode = busyCount == m_SoundAgents.Count ? PlaySoundErrorCode.SoundGroupHasNotEnoughAgent : PlaySoundErrorCode.IgnoredDueToLowPriority;
                    return null;
                }

                if (candidateAgent.IsPlaying)
                {
                    Log.Debug(LogTag.Sound, "声音代理被替换：被替换 serialID={0}，替换者 serialID={1}，声音组={2}", candidateAgent.SerialID, serialID, m_Name);
                    OnSoundReplaced?.Invoke(candidateAgent.SerialID);
                }

                if (!candidateAgent.SetSoundAsset(soundAsset, handle))
                {
                    errorCode = PlaySoundErrorCode.SetSoundAssetFailure;
                    return null;
                }

                candidateAgent.SerialID = serialID;
                candidateAgent.Time = playSoundParams.Time;
                candidateAgent.MuteInSoundGroup = playSoundParams.MuteInSoundGroup;
                candidateAgent.Loop = playSoundParams.Loop;
                candidateAgent.Priority = playSoundParams.Priority;
                candidateAgent.VolumeInSoundGroup = playSoundParams.VolumeInSoundGroup;
                candidateAgent.Pitch = playSoundParams.Pitch;
                candidateAgent.PanStereo = playSoundParams.PanStereo;
                candidateAgent.Play(playSoundParams.FadeInSeconds);
                return candidateAgent;
            }

            /// <summary>
            /// 停止播放声音。
            /// </summary>
            /// <param name="serialID">要停止播放声音的序列编号。</param>
            /// <param name="fadeOutSeconds">声音淡出时间（秒）。</param>
            /// <returns>是否停止播放声音成功。</returns>
            public bool StopSound(int serialID, float fadeOutSeconds)
            {
                foreach (SoundAgent soundAgent in m_SoundAgents)
                {
                    if (soundAgent.SerialID != serialID)
                    {
                        continue;
                    }

                    soundAgent.Stop(fadeOutSeconds);
                    return true;
                }

                return false;
            }

            /// <summary>
            /// 暂停播放声音。
            /// </summary>
            /// <param name="serialID">要暂停播放声音的序列编号。</param>
            /// <param name="fadeOutSeconds">声音淡出时间（秒）。</param>
            /// <returns>是否暂停播放声音成功。</returns>
            public bool PauseSound(int serialID, float fadeOutSeconds)
            {
                foreach (SoundAgent soundAgent in m_SoundAgents)
                {
                    if (soundAgent.SerialID != serialID)
                    {
                        continue;
                    }

                    soundAgent.Pause(fadeOutSeconds);
                    return true;
                }

                return false;
            }

            /// <summary>
            /// 恢复播放声音。
            /// </summary>
            /// <param name="serialID">要恢复播放声音的序列编号。</param>
            /// <param name="fadeInSeconds">声音淡入时间（秒）。</param>
            /// <returns>是否恢复播放声音成功。</returns>
            public bool ResumeSound(int serialID, float fadeInSeconds)
            {
                foreach (SoundAgent soundAgent in m_SoundAgents)
                {
                    if (soundAgent.SerialID != serialID)
                    {
                        continue;
                    }

                    soundAgent.Resume(fadeInSeconds);
                    return true;
                }

                return false;
            }

            /// <summary>
            /// 停止所有已加载的声音。
            /// </summary>
            /// <param name="fadeOutSeconds">声音淡出时间（秒）。</param>
            public void StopAllLoadedSounds(float fadeOutSeconds)
            {
                foreach (SoundAgent soundAgent in m_SoundAgents)
                {
                    if (soundAgent.IsPlaying)
                    {
                        soundAgent.Stop(fadeOutSeconds);
                    }
                }
            }

            /// <summary>
            /// 暂停所有正在播放中的声音。
            /// </summary>
            /// <param name="fadeOutSeconds">声音淡出时间（秒）。</param>
            public void PauseAllLoadedSounds(float fadeOutSeconds)
            {
                foreach (SoundAgent soundAgent in m_SoundAgents)
                {
                    soundAgent.Pause(fadeOutSeconds);
                }
            }

            /// <summary>
            /// 恢复所有正在播放中的声音。
            /// </summary>
            /// <param name="fadeInSeconds">声音淡入时间（秒）。</param>
            public void ResumeAllLoadedSounds(float fadeInSeconds)
            {
                foreach (SoundAgent soundAgent in m_SoundAgents)
                {
                    soundAgent.Resume(fadeInSeconds);
                }
            }

            /// <summary>
            /// 释放组内所有声音资源。
            /// </summary>
            public void ReleaseAllGroupSoundAsset()
            {
                for (int i = 0; i < m_SoundAgents.Count; i++)
                {
                    m_SoundAgents[i].ReleaseSoundAsset();
                }
            }

            /// <summary>
            /// 通过序列编号释放声音资源。
            /// </summary>
            /// <param name="serialID">声音序列编号。</param>
            /// <returns>是否在此组中找到并释放。</returns>
            public bool ReleaseGroupSoundAssetBySerialID(int serialID)
            {
                for (int i = 0; i < m_SoundAgents.Count; i++)
                {
                    if (m_SoundAgents[i].SerialID == serialID)
                    {
                        m_SoundAgents[i].ReleaseSoundAsset();
                        return true;
                    }
                }

                return false;
            }
        }
    }
}
