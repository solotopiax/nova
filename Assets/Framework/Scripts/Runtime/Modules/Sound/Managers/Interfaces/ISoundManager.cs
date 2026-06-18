/***************************************************************
 * (c) copyright 2026 - 2030, Solotopia
 * All Rights Reserved.
 * -------------------------------------------------------------
 * filename:  ISoundManager.cs
 * author:    taoye
 * created:   2026/4/9
 * descrip:   声音管理器接口
 ***************************************************************/

using System.Collections.Generic;
using Cysharp.Threading.Tasks;

namespace NovaFramework.Runtime
{
    /// <summary>
    /// 声音管理器接口。
    /// </summary>
    public interface ISoundManager
    {
        /// <summary>
        /// 获取声音组数量。
        /// </summary>
        int SoundGroupCount { get; }

        /// <summary>
        /// 初始化。
        /// </summary>
        /// <param name="config">配置信息。</param>
        void Initialize(SoundManagerConfig config);

        /// <summary>
        /// 加载声音数据（fire-and-forget）。
        /// </summary>
        void LoadSync();

        /// <summary>
        /// 异步加载声音数据。
        /// </summary>
        /// <returns>是否加载成功。</returns>
        UniTask<bool> LoadAsync();

        /// <summary>
        /// 按声音名称查表播放声音（使用表中配置的默认参数）。
        /// </summary>
        /// <param name="name">ISoundRow.Name 主键。</param>
        /// <returns>声音的序列编号。</returns>
        int PlaySound(string name);

        /// <summary>
        /// 按声音名称查表播放声音。
        /// </summary>
        /// <param name="name">ISoundRow.Name 主键。</param>
        /// <param name="playSoundParams">播放声音参数（IReference，所有权转入 SoundManager，调用方禁止再持有或 Put）。</param>
        /// <returns>声音的序列编号。</returns>
        int PlaySound(string name, PlaySoundParams playSoundParams);

        /// <summary>
        /// 播放声音。
        /// </summary>
        /// <param name="soundGroupName">声音组名称。</param>
        /// <param name="assetLocation">Asset 地址。</param>
        /// <param name="playSoundParams">播放声音参数（IReference，所有权转入 SoundManager，调用方禁止再持有或 Put）。</param>
        /// <returns>声音的序列编号。</returns>
        int PlaySound(string soundGroupName, string assetLocation, PlaySoundParams playSoundParams);
        
        /// <summary>
        /// 停止播放声音。
        /// </summary>
        /// <param name="serialID">要停止播放声音的序列编号。</param>
        /// <returns>是否停止播放声音成功。</returns>
        bool StopSound(int serialID);

        /// <summary>
        /// 停止播放声音。
        /// </summary>
        /// <param name="serialID">要停止播放声音的序列编号。</param>
        /// <param name="fadeOutSeconds">声音淡出时间（秒）。</param>
        /// <returns>是否停止播放声音成功。</returns>
        bool StopSound(int serialID, float fadeOutSeconds);

        /// <summary>
        /// 停止声音组播放。
        /// </summary>
        /// <param name="groupName">要停止播放的声音组名称。</param>
        /// <returns>是否停止成功。</returns>
        bool StopGroupSound(string groupName);

        /// <summary>
        /// 停止声音组播放。
        /// </summary>
        /// <param name="groupName">要停止播放的声音组名称。</param>
        /// <param name="fadeOutSeconds">声音淡出时间（秒）。</param>
        /// <returns>是否停止成功。</returns>
        bool StopGroupSound(string groupName, float fadeOutSeconds);

        /// <summary>
        /// 停止所有已加载的声音。
        /// </summary>
        void StopAllLoadedSounds();

        /// <summary>
        /// 停止所有已加载的声音。
        /// </summary>
        /// <param name="fadeOutSeconds">声音淡出时间（秒）。</param>
        void StopAllLoadedSounds(float fadeOutSeconds);

        /// <summary>
        /// 停止所有正在加载的声音。
        /// </summary>
        void StopAllLoadingSounds();

        /// <summary>
        /// 暂停播放声音。
        /// </summary>
        /// <param name="serialID">要暂停播放声音的序列编号。</param>
        void PauseSound(int serialID);

        /// <summary>
        /// 暂停播放声音。
        /// </summary>
        /// <param name="serialID">要暂停播放声音的序列编号。</param>
        /// <param name="fadeOutSeconds">声音淡出时间（秒）。</param>
        void PauseSound(int serialID, float fadeOutSeconds);

        /// <summary>
        /// 暂停声音组播放。
        /// </summary>
        /// <param name="groupName">要暂停播放的声音组名称。</param>
        /// <returns>是否暂停成功。</returns>
        bool PauseGroupSound(string groupName);

        /// <summary>
        /// 暂停声音组播放。
        /// </summary>
        /// <param name="groupName">要暂停播放的声音组名称。</param>
        /// <param name="fadeOutSeconds">声音淡出时间（秒）。</param>
        /// <returns>是否暂停成功。</returns>
        bool PauseGroupSound(string groupName, float fadeOutSeconds);

        /// <summary>
        /// 恢复播放声音。
        /// </summary>
        /// <param name="serialID">要恢复播放声音的序列编号。</param>
        /// <returns>是否恢复成功。</returns>
        bool ResumeSound(int serialID);

        /// <summary>
        /// 恢复播放声音。
        /// </summary>
        /// <param name="serialID">要恢复播放声音的序列编号。</param>
        /// <param name="fadeInSeconds">声音淡入时间（秒）。</param>
        /// <returns>是否恢复成功。</returns>
        bool ResumeSound(int serialID, float fadeInSeconds);

        /// <summary>
        /// 恢复声音组播放。
        /// </summary>
        /// <param name="groupName">要恢复播放的声音组名称。</param>
        /// <returns>是否恢复成功。</returns>
        bool ResumeGroupSound(string groupName);

        /// <summary>
        /// 恢复声音组播放。
        /// </summary>
        /// <param name="groupName">要恢复播放的声音组名称。</param>
        /// <param name="fadeInSeconds">声音淡入时间（秒）。</param>
        /// <returns>是否恢复成功。</returns>
        bool ResumeGroupSound(string groupName, float fadeInSeconds);

        /// <summary>
        /// 是否存在指定声音组。
        /// </summary>
        /// <param name="soundGroupName">声音组名称。</param>
        /// <returns>指定声音组是否存在。</returns>
        bool HasSoundGroup(string soundGroupName);

        /// <summary>
        /// 增加声音组（包含创建 Helper 和声音代理）。
        /// </summary>
        /// <param name="soundGroupName">声音组名称。</param>
        /// <param name="avoidBeingReplacedBySamePriority">是否避免被同优先级声音替换。</param>
        /// <param name="mute">是否静音。</param>
        /// <param name="volume">音量。</param>
        /// <param name="agentCount">声音代理数量。</param>
        /// <returns>是否增加声音组成功。</returns>
        bool AddSoundGroup(string soundGroupName, bool avoidBeingReplacedBySamePriority, bool mute, float volume, int agentCount);

        /// <summary>
        /// 设置声音组音量。
        /// </summary>
        /// <param name="groupName">声音组名称。</param>
        /// <param name="volume">音量。</param>
        void SetSoundGroupVolume(string groupName, float volume);

        /// <summary>
        /// 是否正在加载声音。
        /// </summary>
        /// <param name="serialID">声音序列编号。</param>
        /// <returns>是否正在加载。</returns>
        bool IsLoadingSound(int serialID);

        /// <summary>
        /// 获取所有正在加载声音的序列编号。
        /// </summary>
        /// <returns>所有正在加载声音的序列编号数组。</returns>
        int[] GetAllLoadingSoundSerialIDs();

        /// <summary>
        /// 获取所有正在加载声音的序列编号。
        /// </summary>
        /// <param name="results">结果列表。</param>
        void GetAllLoadingSoundSerialIDs(List<int> results);

        /// <summary>
        /// 释放所有声音资源。
        /// </summary>
        void ReleaseAllAsset();

        /// <summary>
        /// 通过序列编号释放声音资源。
        /// </summary>
        /// <param name="serialID">声音序列编号。</param>
        void ReleaseAssetBySerialID(int serialID);
    }
}
