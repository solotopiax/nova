/***************************************************************
 * (c) copyright 2026 - 2030, Solotopia
 * All Rights Reserved.
 * -------------------------------------------------------------
 * filename:  SoundManagerBase.cs
 * author:    taoye
 * created:   2026/4/9
 * descrip:   声音管理器基类
 ***************************************************************/

using System.Collections.Generic;
using Cysharp.Threading.Tasks;

namespace NovaFramework.Runtime
{
    /// <summary>
    /// 声音管理器基类。
    /// </summary>
    internal abstract class SoundManagerBase : FrameworkManager, ISoundManager
    {
        /// <summary>
        /// 管理器优先级。
        /// </summary>
        /// <remarks>值越小优先级越高，越先 Update、越后 Shutdown。</remarks>
        public override int Priority => 19;

        /// <summary>
        /// 获取声音组数量。
        /// </summary>
        public abstract int SoundGroupCount { get; }

        /// <summary>
        /// 初始化。
        /// </summary>
        /// <param name="config">配置信息。</param>
        public abstract void Initialize(SoundManagerConfig config);

        /// <summary>
        /// 管理器轮询。
        /// </summary>
        public abstract override void Update();

        /// <summary>
        /// 关闭并清理管理器。
        /// </summary>
        public abstract override void Shutdown();

        /// <summary>
        /// 同步加载全部声音数据并构建名称索引；有效单元读取失败时拒绝整轮，不保留部分索引或旧行。
        /// </summary>
        public abstract void LoadSync();

        /// <summary>
        /// 异步加载声音数据。
        /// </summary>
        /// <returns>是否加载成功。</returns>
        public abstract UniTask<bool> LoadAsync();

        /// <summary>
        /// 按声音表 Name 播放，所有表内播放配置均取自该行。
        /// </summary>
        /// <param name="name">ISoundRow.Name 主键。</param>
        /// <returns>请求序列编号；名称或配置无效时为 -1。</returns>
        public abstract int PlaySound(string name);

        /// <summary>
        /// 按声音表 Name 播放，补充本次请求的表外运行时参数。
        /// </summary>
        /// <param name="name">ISoundRow.Name 主键。</param>
        /// <param name="playSoundParams">表外运行时参数；接受或拒绝时均由 Manager 接管并归还引用池。</param>
        /// <returns>请求序列编号；名称或配置无效时为 -1。</returns>
        public abstract int PlaySound(string name, PlaySoundParams playSoundParams);

        /// <summary>
        /// 按资源地址唯一反查声音表后播放；无匹配或地址歧义时拒绝。
        /// </summary>
        /// <param name="assetLocation">ISoundRow.AssetLocation，精确区分大小写。</param>
        /// <param name="playSoundParams">表外运行时参数；调用后所有权转入 Manager。</param>
        /// <returns>请求序列编号；地址缺失、歧义或配置无效时为 -1。</returns>
        public abstract int PlaySoundByAsset(string assetLocation, PlaySoundParams playSoundParams = null);
        
        /// <summary>
        /// 停止播放声音。
        /// </summary>
        /// <param name="serialID">要停止播放声音的序列编号。</param>
        /// <returns>是否停止播放声音成功。</returns>
        public abstract bool StopSound(int serialID);

        /// <summary>
        /// 停止播放声音。
        /// </summary>
        /// <param name="serialID">要停止播放声音的序列编号。</param>
        /// <param name="fadeOutSeconds">声音淡出时间（秒）。</param>
        /// <returns>是否停止播放声音成功。</returns>
        public abstract bool StopSound(int serialID, float fadeOutSeconds);

        /// <summary>
        /// 停止声音组播放。
        /// </summary>
        /// <param name="groupName">要停止播放的声音组名称。</param>
        /// <returns>是否停止成功。</returns>
        public abstract bool StopGroupSound(string groupName);

        /// <summary>
        /// 停止声音组播放。
        /// </summary>
        /// <param name="groupName">要停止播放的声音组名称。</param>
        /// <param name="fadeOutSeconds">声音淡出时间（秒）。</param>
        /// <returns>是否停止成功。</returns>
        public abstract bool StopGroupSound(string groupName, float fadeOutSeconds);

        /// <summary>
        /// 停止所有已加载的声音。
        /// </summary>
        public abstract void StopAllLoadedSounds();

        /// <summary>
        /// 停止所有已加载的声音。
        /// </summary>
        /// <param name="fadeOutSeconds">声音淡出时间（秒）。</param>
        public abstract void StopAllLoadedSounds(float fadeOutSeconds);

        /// <summary>
        /// 停止所有正在加载的声音。
        /// </summary>
        public abstract void StopAllLoadingSounds();

        /// <summary>
        /// 暂停播放声音。
        /// </summary>
        /// <param name="serialID">要暂停播放声音的序列编号。</param>
        public abstract void PauseSound(int serialID);

        /// <summary>
        /// 暂停播放声音。
        /// </summary>
        /// <param name="serialID">要暂停播放声音的序列编号。</param>
        /// <param name="fadeOutSeconds">声音淡出时间（秒）。</param>
        public abstract void PauseSound(int serialID, float fadeOutSeconds);

        /// <summary>
        /// 暂停声音组播放。
        /// </summary>
        /// <param name="groupName">要暂停播放的声音组名称。</param>
        /// <returns>是否暂停成功。</returns>
        public abstract bool PauseGroupSound(string groupName);

        /// <summary>
        /// 暂停声音组播放。
        /// </summary>
        /// <param name="groupName">要暂停播放的声音组名称。</param>
        /// <param name="fadeOutSeconds">声音淡出时间（秒）。</param>
        /// <returns>是否暂停成功。</returns>
        public abstract bool PauseGroupSound(string groupName, float fadeOutSeconds);

        /// <summary>
        /// 恢复播放声音。
        /// </summary>
        /// <param name="serialID">要恢复播放声音的序列编号。</param>
        /// <returns>是否恢复成功。</returns>
        public abstract bool ResumeSound(int serialID);

        /// <summary>
        /// 恢复播放声音。
        /// </summary>
        /// <param name="serialID">要恢复播放声音的序列编号。</param>
        /// <param name="fadeInSeconds">声音淡入时间（秒）。</param>
        /// <returns>是否恢复成功。</returns>
        public abstract bool ResumeSound(int serialID, float fadeInSeconds);

        /// <summary>
        /// 恢复声音组播放。
        /// </summary>
        /// <param name="groupName">要恢复播放的声音组名称。</param>
        /// <returns>是否恢复成功。</returns>
        public abstract bool ResumeGroupSound(string groupName);

        /// <summary>
        /// 恢复声音组播放。
        /// </summary>
        /// <param name="groupName">要恢复播放的声音组名称。</param>
        /// <param name="fadeInSeconds">声音淡入时间（秒）。</param>
        /// <returns>是否恢复成功。</returns>
        public abstract bool ResumeGroupSound(string groupName, float fadeInSeconds);

        /// <summary>
        /// 是否存在指定声音组。
        /// </summary>
        /// <param name="soundGroupName">声音组名称。</param>
        /// <returns>指定声音组是否存在。</returns>
        public abstract bool HasSoundGroup(string soundGroupName);

        /// <summary>
        /// 增加声音组（包含创建 Helper 和声音代理）。
        /// </summary>
        /// <param name="soundGroupName">声音组名称。</param>
        /// <param name="avoidBeingReplacedBySamePriority">是否避免被同优先级声音替换。</param>
        /// <param name="mute">是否静音。</param>
        /// <param name="volume">音量。</param>
        /// <param name="agentCount">声音代理数量。</param>
        /// <returns>是否增加声音组成功。</returns>
        public abstract bool AddSoundGroup(string soundGroupName, bool avoidBeingReplacedBySamePriority, bool mute, float volume, int agentCount);

        /// <summary>
        /// 设置声音组音量。
        /// </summary>
        /// <param name="groupName">声音组名称。</param>
        /// <param name="volume">音量。</param>
        public abstract void SetSoundGroupVolume(string groupName, float volume);

        /// <summary>
        /// 是否正在加载声音。
        /// </summary>
        /// <param name="serialID">声音序列编号。</param>
        /// <returns>是否正在加载。</returns>
        public abstract bool IsLoadingSound(int serialID);

        /// <summary>
        /// 获取所有正在加载声音的序列编号。
        /// </summary>
        /// <returns>所有正在加载声音的序列编号数组。</returns>
        public abstract int[] GetAllLoadingSoundSerialIDs();

        /// <summary>
        /// 获取所有正在加载声音的序列编号。
        /// </summary>
        /// <param name="results">结果列表。</param>
        public abstract void GetAllLoadingSoundSerialIDs(List<int> results);

        /// <summary>
        /// 释放所有声音资源。
        /// </summary>
        public abstract void ReleaseAllAsset();

        /// <summary>
        /// 通过序列编号释放声音资源。
        /// </summary>
        /// <param name="serialID">声音序列编号。</param>
        public abstract void ReleaseAssetBySerialID(int serialID);
    }
}
