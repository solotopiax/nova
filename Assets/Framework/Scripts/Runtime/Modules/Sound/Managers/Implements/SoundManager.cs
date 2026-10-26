/***************************************************************
 * (c) copyright 2026 - 2030, Solotopia
 * All Rights Reserved.
 * -------------------------------------------------------------
 * filename:  SoundManager.cs
 * author:    taoye
 * created:   2026/4/9
 * descrip:   声音管理器
 ***************************************************************/

using System;
using System.Collections.Generic;
using System.Linq;
using Cysharp.Threading.Tasks;
using UnityEngine;
using UnityEngine.Audio;

namespace NovaFramework.Runtime
{
    /// <summary>
    /// 声音管理器。
    /// </summary>
    internal sealed partial class SoundManager : SoundManagerBase
    {
        /// <summary>
        /// 初始化声音管理器的新实例。
        /// </summary>
        public SoundManager()
        {
            m_SoundGroups = new Dictionary<string, SoundGroup>();
            m_SoundsLoading = new HashSet<int>();
            m_SoundsToReleaseOnLoad = new HashSet<int>();
            m_SoundTableDatas = new Dictionary<Type, ITable>();
            m_SoundRows = new Dictionary<string, ISoundRow>();
            m_Serial = 0;
        }

        /// <summary>
        /// 初始化。
        /// </summary>
        /// <param name="config">配置信息。</param>
        public override void Initialize(SoundManagerConfig config)
        {
            m_DataFormat = config.DataFormat;
            m_SoundUnitsSettings = config.SoundUnitsSettings;
            m_ParentTransform = config.ParentTransform;
            m_AudioMixer = config.AudioMixer;
            m_AssetManager = FrameworkManagersGroup.GetManager<IAssetManager>();
        }

        /// <summary>
        /// 管理器轮询。
        /// </summary>
        public override void Update()
        {
        }

        /// <summary>
        /// 关闭并清理管理器。
        /// </summary>
        public override void Shutdown()
        {
            StopAllLoadingSounds();
            StopAllLoadedSounds();
            foreach (KeyValuePair<string, SoundGroup> group in m_SoundGroups)
            {
                group.Value.ReleaseAllGroupSoundAsset();
            }

            m_SoundGroups.Clear();
            m_SoundsLoading.Clear();
            m_SoundsToReleaseOnLoad.Clear();
            m_SoundTableDatas.Clear();
            m_SoundRows.Clear();
        }

        /// <summary>
        /// 同步加载声音数据；任一有效单元读取失败即拒绝整轮，不构建部分索引或沿用旧配置。
        /// </summary>
        public override void LoadSync()
        {
            m_SoundTableDatas.Clear();
            m_SoundRows.Clear();
            if (!ValidateSoundUnitsSettings())
            {
                return;
            }

            BuildSoundDataSyncDelegates(out DataReceiver.ReleaseAssetAction releaseFunc, out DataReceiver.LoadAssetSyncFunc syncLoadFunc);

            LubanDataCache dataCache = new LubanDataCache();
            for (int i = 0; i < m_SoundUnitsSettings.Count; i++)
            {
                SoundUnitSetting unit = m_SoundUnitsSettings[i];
                if (string.IsNullOrEmpty(unit.AssetLocation))
                {
                    continue;
                }

                if (!LubanRuntimeData.CreateReceiver(m_DataFormat, dataCache, unit, syncLoadFunc, releaseFunc)
                    .ReadDataAssetSync(unit.AssetLocation))
                {
                    return;
                }
            }

            BuildSoundTablesFromCache(dataCache, m_SoundUnitsSettings.Count);
        }

        /// <summary>
        /// 异步加载声音数据。
        /// </summary>
        /// <returns>是否加载成功。</returns>
        public override async UniTask<bool> LoadAsync()
        {
            m_SoundTableDatas.Clear();
            m_SoundRows.Clear();
            if (!ValidateSoundUnitsSettings())
            {
                return true;
            }

            BuildSoundDataAsyncDelegates(out DataReceiver.LoadAssetAsyncFunc loadFunc, out DataReceiver.ReleaseAssetAction releaseFunc);

            LubanDataCache dataCache = new LubanDataCache();
            List<UniTask<bool>> tasks = new List<UniTask<bool>>(m_SoundUnitsSettings.Count);
            for (int i = 0; i < m_SoundUnitsSettings.Count; i++)
            {
                SoundUnitSetting unit = m_SoundUnitsSettings[i];
                if (string.IsNullOrEmpty(unit.AssetLocation))
                {
                    continue;
                }

                tasks.Add(LubanRuntimeData.CreateReceiver(m_DataFormat, dataCache, unit, loadFunc, releaseFunc)
                    .ReadDataAssetAsync(unit.AssetLocation));
            }

            if (tasks.Count > 0)
            {
                bool[] results = await UniTask.WhenAll(tasks);
                for (int i = 0; i < results.Length; i++)
                {
                    if (!results[i])
                    {
                        return false;
                    }
                }
            }

            return BuildSoundTablesFromCache(dataCache, m_SoundUnitsSettings.Count);
        }

        /// <summary>
        /// 按声音表 Name 播放，声音组、资源及全部表内播放配置均取自该行。
        /// </summary>
        /// <param name="name">ISoundRow.Name 主键。</param>
        /// <returns>请求序列编号；名称或配置无效时为 -1。</returns>
        public override int PlaySound(string name)
        {
            return PlaySound(name, null);
        }

        /// <summary>
        /// 按声音表 Name 播放，补充本次请求的表外运行时参数。
        /// </summary>
        /// <param name="name">ISoundRow.Name 主键。</param>
        /// <param name="playSoundParams">表外运行时参数；接受或拒绝时均由 Manager 接管并归还引用池。</param>
        /// <returns>请求序列编号；名称或配置无效时为 -1。</returns>
        public override int PlaySound(string name, PlaySoundParams playSoundParams)
        {
            if (string.IsNullOrWhiteSpace(name) || !m_SoundRows.TryGetValue(name, out ISoundRow row))
            {
                Log.Warning(LogTag.Sound, "未找到名称为 '{0}' 的声音数据行，请确认表数据已加载且 Name 主键正确。", name);
                return RejectSoundRequest(playSoundParams);
            }

            return PlaySoundRow(row, playSoundParams);
        }

        /// <summary>
        /// 按资源地址唯一反查声音表后播放；不存在或多行匹配时拒绝请求。
        /// </summary>
        /// <param name="assetLocation">ISoundRow.AssetLocation，精确区分大小写。</param>
        /// <param name="playSoundParams">表外运行时参数；调用后所有权转入 Manager。</param>
        /// <returns>请求序列编号；地址缺失、歧义或配置无效时为 -1。</returns>
        public override int PlaySoundByAsset(string assetLocation, PlaySoundParams playSoundParams = null)
        {
            ISoundRow matchedRow = null;
            if (!string.IsNullOrWhiteSpace(assetLocation))
            {
                foreach (ISoundRow row in m_SoundRows.Values)
                {
                    if (!string.Equals(row.AssetLocation, assetLocation, StringComparison.Ordinal))
                    {
                        continue;
                    }

                    if (matchedRow != null)
                    {
                        Log.Warning(LogTag.Sound, "声音资源地址 '{0}' 对应多个声音表行，请改用 PlaySound(Name) 明确选择。", assetLocation);
                        return RejectSoundRequest(playSoundParams);
                    }

                    matchedRow = row;
                }
            }

            if (matchedRow == null)
            {
                Log.Warning(LogTag.Sound, "未找到资源地址为 '{0}' 的声音数据行，请确认表数据已加载且 AssetLocation 正确。", assetLocation);
                return RejectSoundRequest(playSoundParams);
            }

            return PlaySoundRow(matchedRow, playSoundParams);
        }
        
        /// <summary>
        /// 停止播放声音。
        /// </summary>
        /// <param name="serialID">要停止播放声音的序列编号。</param>
        /// <returns>是否停止播放声音成功。</returns>
        public override bool StopSound(int serialID)
        {
            return StopSound(serialID, SoundConstant.c_DefaultFadeOutSeconds);
        }

        /// <summary>
        /// 停止播放声音。
        /// </summary>
        /// <param name="serialID">要停止播放声音的序列编号。</param>
        /// <param name="fadeOutSeconds">声音淡出时间（秒）。</param>
        /// <returns>是否停止播放声音成功。</returns>
        public override bool StopSound(int serialID, float fadeOutSeconds)
        {
            if (IsLoadingSound(serialID))
            {
                m_SoundsToReleaseOnLoad.Add(serialID);
                m_SoundsLoading.Remove(serialID);
                return true;
            }

            foreach (KeyValuePair<string, SoundGroup> soundGroup in m_SoundGroups)
            {
                if (soundGroup.Value.StopSound(serialID, fadeOutSeconds))
                {
                    return true;
                }
            }

            return false;
        }

        /// <summary>
        /// 停止声音组播放。
        /// </summary>
        /// <param name="groupName">要停止播放的声音组名称。</param>
        /// <returns>是否停止成功。</returns>
        public override bool StopGroupSound(string groupName)
        {
            return StopGroupSound(groupName, SoundConstant.c_DefaultFadeOutSeconds);
        }

        /// <summary>
        /// 停止声音组播放。
        /// </summary>
        /// <param name="groupName">要停止播放的声音组名称。</param>
        /// <param name="fadeOutSeconds">声音淡出时间（秒）。</param>
        /// <returns>是否停止成功。</returns>
        public override bool StopGroupSound(string groupName, float fadeOutSeconds)
        {
            SoundGroup soundGroup = GetSoundGroup(groupName);
            if (soundGroup == null)
            {
                Log.Warning(LogTag.Sound, "未找到需要停止播放的声音组：{0}", groupName);
                return false;
            }

            Log.Debug(LogTag.Sound, "停止声音组播放：{0}", soundGroup.Name);
            soundGroup.StopAllLoadedSounds(fadeOutSeconds);
            return true;
        }

        /// <summary>
        /// 停止所有已加载的声音。
        /// </summary>
        public override void StopAllLoadedSounds()
        {
            StopAllLoadedSounds(SoundConstant.c_DefaultFadeOutSeconds);
        }

        /// <summary>
        /// 停止所有已加载的声音。
        /// </summary>
        /// <param name="fadeOutSeconds">声音淡出时间（秒）。</param>
        public override void StopAllLoadedSounds(float fadeOutSeconds)
        {
            foreach (KeyValuePair<string, SoundGroup> soundGroup in m_SoundGroups)
            {
                soundGroup.Value.StopAllLoadedSounds(fadeOutSeconds);
            }
        }

        /// <summary>
        /// 停止所有正在加载的声音。
        /// </summary>
        public override void StopAllLoadingSounds()
        {
            foreach (int serialID in m_SoundsLoading)
            {
                m_SoundsToReleaseOnLoad.Add(serialID);
            }

            m_SoundsLoading.Clear();
        }

        /// <summary>
        /// 暂停播放声音。
        /// </summary>
        /// <param name="serialID">要暂停播放声音的序列编号。</param>
        public override void PauseSound(int serialID)
        {
            PauseSound(serialID, SoundConstant.c_DefaultFadeOutSeconds);
        }

        /// <summary>
        /// 暂停播放声音。
        /// </summary>
        /// <param name="serialID">要暂停播放声音的序列编号。</param>
        /// <param name="fadeOutSeconds">声音淡出时间（秒）。</param>
        public override void PauseSound(int serialID, float fadeOutSeconds)
        {
            foreach (KeyValuePair<string, SoundGroup> soundGroup in m_SoundGroups)
            {
                if (soundGroup.Value.PauseSound(serialID, fadeOutSeconds))
                {
                    return;
                }
            }

            Log.Warning(LogTag.Sound, "无法找到需要暂停的声音：serialID={0}", serialID);
        }

        /// <summary>
        /// 暂停声音组播放。
        /// </summary>
        /// <param name="groupName">要暂停播放的声音组名称。</param>
        /// <returns>是否暂停成功。</returns>
        public override bool PauseGroupSound(string groupName)
        {
            return PauseGroupSound(groupName, SoundConstant.c_DefaultFadeOutSeconds);
        }

        /// <summary>
        /// 暂停声音组播放。
        /// </summary>
        /// <param name="groupName">要暂停播放的声音组名称。</param>
        /// <param name="fadeOutSeconds">声音淡出时间（秒）。</param>
        /// <returns>是否暂停成功。</returns>
        public override bool PauseGroupSound(string groupName, float fadeOutSeconds)
        {
            SoundGroup soundGroup = GetSoundGroup(groupName);
            if (soundGroup == null)
            {
                Log.Warning(LogTag.Sound, "未找到需要暂停播放的声音组：{0}", groupName);
                return false;
            }

            Log.Debug(LogTag.Sound, "暂停声音组播放：{0}", soundGroup.Name);
            soundGroup.PauseAllLoadedSounds(fadeOutSeconds);
            return true;
        }

        /// <summary>
        /// 恢复播放声音。
        /// </summary>
        /// <param name="serialID">要恢复播放声音的序列编号。</param>
        /// <returns>是否恢复成功。</returns>
        public override bool ResumeSound(int serialID)
        {
            return ResumeSound(serialID, SoundConstant.c_DefaultFadeInSeconds);
        }

        /// <summary>
        /// 恢复播放声音。
        /// </summary>
        /// <param name="serialID">要恢复播放声音的序列编号。</param>
        /// <param name="fadeInSeconds">声音淡入时间（秒）。</param>
        /// <returns>是否恢复成功。</returns>
        public override bool ResumeSound(int serialID, float fadeInSeconds)
        {
            foreach (KeyValuePair<string, SoundGroup> soundGroup in m_SoundGroups)
            {
                if (soundGroup.Value.ResumeSound(serialID, fadeInSeconds))
                {
                    return true;
                }
            }

            Log.Warning(LogTag.Sound, "无法找到需要恢复播放的声音：serialID={0}", serialID);
            return false;
        }

        /// <summary>
        /// 恢复声音组播放。
        /// </summary>
        /// <param name="groupName">要恢复播放的声音组名称。</param>
        /// <returns>是否恢复成功。</returns>
        public override bool ResumeGroupSound(string groupName)
        {
            return ResumeGroupSound(groupName, SoundConstant.c_DefaultFadeInSeconds);
        }

        /// <summary>
        /// 恢复声音组播放。
        /// </summary>
        /// <param name="groupName">要恢复播放的声音组名称。</param>
        /// <param name="fadeInSeconds">声音淡入时间（秒）。</param>
        /// <returns>是否恢复成功。</returns>
        public override bool ResumeGroupSound(string groupName, float fadeInSeconds)
        {
            SoundGroup soundGroup = GetSoundGroup(groupName);
            if (soundGroup == null)
            {
                Log.Warning(LogTag.Sound, "未找到需要恢复播放的声音组：{0}", groupName);
                return false;
            }

            Log.Debug(LogTag.Sound, "恢复声音组播放：{0}", soundGroup.Name);
            soundGroup.ResumeAllLoadedSounds(fadeInSeconds);
            return true;
        }

        /// <summary>
        /// 是否存在指定声音组。
        /// </summary>
        /// <param name="soundGroupName">声音组名称。</param>
        /// <returns>指定声音组是否存在。</returns>
        public override bool HasSoundGroup(string soundGroupName)
        {
            if (string.IsNullOrEmpty(soundGroupName))
            {
                throw new ArgumentException("soundGroupName 无效。", nameof(soundGroupName));
            }

            return m_SoundGroups.ContainsKey(soundGroupName);
        }

        /// <summary>
        /// 增加声音组（包含创建 Helper 和声音代理）。
        /// </summary>
        /// <param name="soundGroupName">声音组名称。</param>
        /// <param name="avoidBeingReplacedBySamePriority">是否避免被同优先级声音替换。</param>
        /// <param name="mute">是否静音。</param>
        /// <param name="volume">音量。</param>
        /// <param name="agentCount">声音代理数量。</param>
        /// <returns>是否增加声音组成功。</returns>
        public override bool AddSoundGroup(string soundGroupName, bool avoidBeingReplacedBySamePriority, bool mute, float volume, int agentCount)
        {
            if (string.IsNullOrEmpty(soundGroupName))
            {
                throw new ArgumentException("soundGroupName 无效。", nameof(soundGroupName));
            }

            if (HasSoundGroup(soundGroupName))
            {
                return false;
            }

            SoundGroupHelper soundGroupHelper = new GameObject(Txt.Format("SoundGroup - {0}", soundGroupName)).AddComponent<SoundGroupHelper>();
            soundGroupHelper.transform.SetParent(m_ParentTransform);

            if (m_AudioMixer != null)
            {
                AudioMixerGroup[] audioMixerGroups = m_AudioMixer.FindMatchingGroups(Txt.Format("Master/{0}", soundGroupName));
                if (audioMixerGroups.Length > 0)
                {
                    soundGroupHelper.AudioMixerGroup = audioMixerGroups[0];
                }
                else
                {
                    AudioMixerGroup[] masterGroups = m_AudioMixer.FindMatchingGroups("Master");
                    if (masterGroups.Length > 0)
                    {
                        soundGroupHelper.AudioMixerGroup = masterGroups[0];
                    }
                }
            }

            SoundGroup soundGroup = new SoundGroup(soundGroupName, soundGroupHelper)
            {
                AvoidBeingReplacedBySamePriority = avoidBeingReplacedBySamePriority,
                Mute = mute,
                Volume = volume,
            };

            m_SoundGroups.Add(soundGroupName, soundGroup);

            for (int i = 1; i <= agentCount; i++)
            {
                SoundAgentHelper soundAgentHelper = new GameObject(Txt.Format("SoundAgent - {0} - {1}", soundGroupName, i)).AddComponent<SoundAgentHelper>();
                soundAgentHelper.transform.SetParent(soundGroupHelper.transform);

                if (m_AudioMixer != null)
                {
                    AudioMixerGroup[] agentMixerGroups = m_AudioMixer.FindMatchingGroups(Txt.Format("Master/{0}/{0}_{1}", soundGroupName, i));
                    if (agentMixerGroups.Length > 0)
                    {
                        soundAgentHelper.AudioMixerGroup = agentMixerGroups[0];
                    }
                    else
                    {
                        soundAgentHelper.AudioMixerGroup = soundGroupHelper.AudioMixerGroup;
                    }
                }

                soundGroup.AddSoundAgent(this, soundAgentHelper);
            }

            return true;
        }

        /// <summary>
        /// 设置声音组音量。
        /// </summary>
        /// <param name="groupName">声音组名称。</param>
        /// <param name="volume">音量。</param>
        public override void SetSoundGroupVolume(string groupName, float volume)
        {
            SoundGroup soundGroup = GetSoundGroup(groupName);
            if (soundGroup == null)
            {
                Log.Warning(LogTag.Sound, "未找到需要设置音量的声音组：{0}", groupName);
                return;
            }

            Log.Debug(LogTag.Sound, "设置声音组音量：{0}，volume={1}", soundGroup.Name, volume);
            soundGroup.Volume = volume;
        }

        /// <summary>
        /// 是否正在加载声音。
        /// </summary>
        /// <param name="serialID">声音序列编号。</param>
        /// <returns>是否正在加载。</returns>
        public override bool IsLoadingSound(int serialID)
        {
            return m_SoundsLoading.Contains(serialID);
        }

        /// <summary>
        /// 获取所有正在加载声音的序列编号。
        /// </summary>
        /// <returns>所有正在加载声音的序列编号数组。</returns>
        public override int[] GetAllLoadingSoundSerialIDs()
        {
            return m_SoundsLoading.ToArray();
        }

        /// <summary>
        /// 获取所有正在加载声音的序列编号。
        /// </summary>
        /// <param name="results">结果列表。</param>
        public override void GetAllLoadingSoundSerialIDs(List<int> results)
        {
            if (results == null)
            {
                throw new ArgumentNullException(nameof(results), "results 无效。");
            }

            results.Clear();
            foreach (int serialID in m_SoundsLoading)
            {
                results.Add(serialID);
            }
        }

        /// <summary>
        /// 释放所有声音资源。
        /// </summary>
        public override void ReleaseAllAsset()
        {
            StopAllLoadingSounds();
            StopAllLoadedSounds();
            foreach (KeyValuePair<string, SoundGroup> group in m_SoundGroups)
            {
                group.Value.ReleaseAllGroupSoundAsset();
            }
        }

        /// <summary>
        /// 通过序列编号释放声音资源。
        /// </summary>
        /// <param name="serialID">声音序列编号。</param>
        public override void ReleaseAssetBySerialID(int serialID)
        {
            StopSound(serialID);
            foreach (KeyValuePair<string, SoundGroup> group in m_SoundGroups)
            {
                if (group.Value.ReleaseGroupSoundAssetBySerialID(serialID))
                {
                    return;
                }
            }
        }
    }
}
