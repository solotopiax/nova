/***************************************************************
 * (c) copyright 2026 - 2030, Solotopia
 * All Rights Reserved.
 * -------------------------------------------------------------
 * filename:  SoundComponent.cs
 * author:    taoye
 * created:   2026/4/9
 * descrip:   声音组件
 ***************************************************************/

using System;
using System.Collections.Generic;
using Cysharp.Threading.Tasks;
using UnityEngine;

namespace NovaFramework.Runtime
{
    /// <summary>
    /// 声音组件。
    /// </summary>
    [DisallowMultipleComponent]
    public sealed partial class SoundComponent : FrameworkComponent
    {
        /// <summary>
        /// 唤醒。
        /// </summary>
        protected override void Awake()
        {
            base.Awake();
            m_SoundManager = Util.TypeCreator.Create<ISoundManager>(m_CurManagerTypeName);
            if (m_SoundManager == null)
            {
                throw new InvalidOperationException("SoundManager 无效。");
            }
        }

        /// <summary>
        /// 开始。
        /// </summary>
        private void Start()
        {
            if (m_Settings == null)
            {
                throw new InvalidOperationException("SoundSettings 无效，请检查 SoundComponent 配置。");
            }

            m_SoundManager.Initialize(new SoundManagerConfig
            {
                SoundUnitsSettings = m_Settings.SoundUnitsSettings,
                ParentTransform = transform,
                AudioMixer = m_AudioMixer,
            });

            if (m_SoundGroupShells != null)
            {
                for (int i = 0; i < m_SoundGroupShells.Length; i++)
                {
                    SoundGroupShell shell = m_SoundGroupShells[i];
                    if (!m_SoundManager.AddSoundGroup(shell.Name, shell.AvoidBeingReplacedBySamePriority, shell.Mute, shell.Volume, shell.AgentCount))
                    {
                        Log.Error(LogTag.Sound, "添加声音组 '{0}' 失败。", m_SoundGroupShells[i].Name);
                    }
                }
            }
        }

        /// <summary>
        /// 加载声音数据（fire-and-forget）。
        /// </summary>
        public void LoadSync()
        {
            m_SoundManager.LoadSync();
        }

        /// <summary>
        /// 异步加载声音数据。
        /// </summary>
        /// <returns>是否加载成功。</returns>
        public async UniTask<bool> LoadAsync()
        {
            if (IsLoadOver)
            {
                return true;
            }

            if (m_LoadTcs != null)
            {
                return await m_LoadTcs.Task;
            }

            m_LoadTcs = new UniTaskCompletionSource<bool>();
            var tcs = m_LoadTcs;

            bool success;
            try
            {
                success = await m_SoundManager.LoadAsync();
            }
            catch (Exception e)
            {
                Log.Error(LogTag.Sound, "SoundComponent.LoadAsync 发生异常：{0}", e);
                success = false;
            }

            IsLoadOver = success;
            tcs.TrySetResult(success);
            m_LoadTcs = null;

            return success;
        }

        /// <summary>
        /// 按声音名称查表播放声音（使用表中配置的默认参数）。
        /// </summary>
        /// <param name="name">ISoundRow.Name 主键。</param>
        /// <returns>声音的序列编号。</returns>
        public int PlaySound(string name)
        {
            return m_SoundManager.PlaySound(name);
        }

        /// <summary>
        /// 按声音名称查表播放声音。
        /// </summary>
        /// <param name="name">ISoundRow.Name 主键。</param>
        /// <param name="playSoundParams">播放声音参数（为 null 时从行配置构造默认参数）。</param>
        /// <returns>声音的序列编号。</returns>
        public int PlaySound(string name, PlaySoundParams playSoundParams)
        {
            return m_SoundManager.PlaySound(name, playSoundParams);
        }

        /// <summary>
        /// 播放声音。
        /// </summary>
        /// <param name="soundGroupName">声音组名称。</param>
        /// <param name="assetLocation">Asset 地址。</param>
        /// <param name="playSoundParams">播放声音参数（为 null 时使用默认参数）。</param>
        /// <returns>声音的序列编号。</returns>
        public int PlaySound(string soundGroupName, string assetLocation, PlaySoundParams playSoundParams = null)
        {
            return m_SoundManager.PlaySound(soundGroupName, assetLocation, playSoundParams);
        }
        
        /// <summary>
        /// 停止播放声音。
        /// </summary>
        /// <param name="serialID">要停止播放声音的序列编号。</param>
        /// <returns>是否停止播放声音成功。</returns>
        public bool StopSound(int serialID)
        {
            return m_SoundManager.StopSound(serialID);
        }

        /// <summary>
        /// 停止播放声音。
        /// </summary>
        /// <param name="serialID">要停止播放声音的序列编号。</param>
        /// <param name="fadeOutSeconds">声音淡出时间（秒）。</param>
        /// <returns>是否停止播放声音成功。</returns>
        public bool StopSound(int serialID, float fadeOutSeconds)
        {
            return m_SoundManager.StopSound(serialID, fadeOutSeconds);
        }

        /// <summary>
        /// 停止声音组播放。
        /// </summary>
        /// <param name="groupName">要停止播放的声音组名称。</param>
        /// <returns>是否停止成功。</returns>
        public bool StopGroupSound(string groupName)
        {
            return m_SoundManager.StopGroupSound(groupName);
        }

        /// <summary>
        /// 停止声音组播放。
        /// </summary>
        /// <param name="groupName">要停止播放的声音组名称。</param>
        /// <param name="fadeOutSeconds">声音淡出时间（秒）。</param>
        /// <returns>是否停止成功。</returns>
        public bool StopGroupSound(string groupName, float fadeOutSeconds)
        {
            return m_SoundManager.StopGroupSound(groupName, fadeOutSeconds);
        }

        /// <summary>
        /// 停止所有已加载的声音。
        /// </summary>
        public void StopAllLoadedSounds()
        {
            m_SoundManager.StopAllLoadedSounds();
        }

        /// <summary>
        /// 停止所有已加载的声音。
        /// </summary>
        /// <param name="fadeOutSeconds">声音淡出时间（秒）。</param>
        public void StopAllLoadedSounds(float fadeOutSeconds)
        {
            m_SoundManager.StopAllLoadedSounds(fadeOutSeconds);
        }

        /// <summary>
        /// 停止所有正在加载的声音。
        /// </summary>
        public void StopAllLoadingSounds()
        {
            m_SoundManager.StopAllLoadingSounds();
        }

        /// <summary>
        /// 暂停播放声音。
        /// </summary>
        /// <param name="serialID">要暂停播放声音的序列编号。</param>
        public void PauseSound(int serialID)
        {
            m_SoundManager.PauseSound(serialID);
        }

        /// <summary>
        /// 暂停播放声音。
        /// </summary>
        /// <param name="serialID">要暂停播放声音的序列编号。</param>
        /// <param name="fadeOutSeconds">声音淡出时间（秒）。</param>
        public void PauseSound(int serialID, float fadeOutSeconds)
        {
            m_SoundManager.PauseSound(serialID, fadeOutSeconds);
        }

        /// <summary>
        /// 暂停声音组播放。
        /// </summary>
        /// <param name="groupName">要暂停播放的声音组名称。</param>
        /// <returns>是否暂停成功。</returns>
        public bool PauseGroupSound(string groupName)
        {
            return m_SoundManager.PauseGroupSound(groupName);
        }

        /// <summary>
        /// 暂停声音组播放。
        /// </summary>
        /// <param name="groupName">要暂停播放的声音组名称。</param>
        /// <param name="fadeOutSeconds">声音淡出时间（秒）。</param>
        /// <returns>是否暂停成功。</returns>
        public bool PauseGroupSound(string groupName, float fadeOutSeconds)
        {
            return m_SoundManager.PauseGroupSound(groupName, fadeOutSeconds);
        }

        /// <summary>
        /// 恢复播放声音。
        /// </summary>
        /// <param name="serialID">要恢复播放声音的序列编号。</param>
        /// <returns>是否恢复成功。</returns>
        public bool ResumeSound(int serialID)
        {
            return m_SoundManager.ResumeSound(serialID);
        }

        /// <summary>
        /// 恢复播放声音。
        /// </summary>
        /// <param name="serialID">要恢复播放声音的序列编号。</param>
        /// <param name="fadeInSeconds">声音淡入时间（秒）。</param>
        /// <returns>是否恢复成功。</returns>
        public bool ResumeSound(int serialID, float fadeInSeconds)
        {
            return m_SoundManager.ResumeSound(serialID, fadeInSeconds);
        }

        /// <summary>
        /// 恢复声音组播放。
        /// </summary>
        /// <param name="groupName">要恢复播放的声音组名称。</param>
        /// <returns>是否恢复成功。</returns>
        public bool ResumeGroupSound(string groupName)
        {
            return m_SoundManager.ResumeGroupSound(groupName);
        }

        /// <summary>
        /// 恢复声音组播放。
        /// </summary>
        /// <param name="groupName">要恢复播放的声音组名称。</param>
        /// <param name="fadeInSeconds">声音淡入时间（秒）。</param>
        /// <returns>是否恢复成功。</returns>
        public bool ResumeGroupSound(string groupName, float fadeInSeconds)
        {
            return m_SoundManager.ResumeGroupSound(groupName, fadeInSeconds);
        }

        /// <summary>
        /// 是否存在指定声音组。
        /// </summary>
        /// <param name="soundGroupName">声音组名称。</param>
        /// <returns>指定声音组是否存在。</returns>
        public bool HasSoundGroup(string soundGroupName)
        {
            return m_SoundManager.HasSoundGroup(soundGroupName);
        }

        /// <summary>
        /// 设置声音组音量。
        /// </summary>
        /// <param name="groupName">声音组名称。</param>
        /// <param name="volume">音量。</param>
        public void SetSoundGroupVolume(string groupName, float volume)
        {
            m_SoundManager.SetSoundGroupVolume(groupName, volume);
        }

        /// <summary>
        /// 是否正在加载声音。
        /// </summary>
        /// <param name="serialID">声音序列编号。</param>
        /// <returns>是否正在加载。</returns>
        public bool IsLoadingSound(int serialID)
        {
            return m_SoundManager.IsLoadingSound(serialID);
        }

        /// <summary>
        /// 获取所有正在加载声音的序列编号。
        /// </summary>
        /// <returns>所有正在加载声音的序列编号数组。</returns>
        public int[] GetAllLoadingSoundSerialIDs()
        {
            return m_SoundManager.GetAllLoadingSoundSerialIDs();
        }

        /// <summary>
        /// 获取所有正在加载声音的序列编号。
        /// </summary>
        /// <param name="results">结果列表。</param>
        public void GetAllLoadingSoundSerialIDs(List<int> results)
        {
            m_SoundManager.GetAllLoadingSoundSerialIDs(results);
        }

        /// <summary>
        /// 释放所有声音资源。
        /// </summary>
        public void ReleaseAllAsset()
        {
            m_SoundManager.ReleaseAllAsset();
        }

        /// <summary>
        /// 通过序列编号释放声音资源。
        /// </summary>
        /// <param name="serialID">声音序列编号。</param>
        public void ReleaseAssetBySerialID(int serialID)
        {
            m_SoundManager.ReleaseAssetBySerialID(serialID);
        }
    }
}
