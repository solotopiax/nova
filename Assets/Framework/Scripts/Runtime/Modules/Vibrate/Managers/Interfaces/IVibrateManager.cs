/***************************************************************
 * (c) copyright 2026 - 2030, Solotopia
 * All Rights Reserved.
 * -------------------------------------------------------------
 * filename:  IVibrateManager.cs
 * author:    taoye
 * created:   2026/4/9
 * descrip:   振动管理器接口
 ***************************************************************/

using Cysharp.Threading.Tasks;

namespace NovaFramework.Runtime
{
    /// <summary>
    /// 振动管理器接口。
    /// </summary>
    public interface IVibrateManager
    {
        /// <summary>
        /// 初始化。
        /// </summary>
        /// <param name="config">振动管理器配置。</param>
        void Initialize(VibrateManagerConfig config);

        /// <summary>
        /// 加载振动数据（fire-and-forget）。
        /// </summary>
        void LoadVibrateDataSync();

        /// <summary>
        /// 异步加载振动数据。
        /// </summary>
        /// <returns>是否加载并解析成功。</returns>
        UniTask<bool> LoadVibrateDataAsync();

        /// <summary>
        /// 播放默认振动。
        /// </summary>
        void Play();

        /// <summary>
        /// 播放指定类型的预设振动。
        /// </summary>
        /// <param name="type">振动类型。</param>
        void Play(VibrateType type);

        /// <summary>
        /// 按名称播放自定义振动组合，未命中时输出 Warning 并静默返回。
        /// </summary>
        /// <param name="name">振动分组名称（对应数据表 Name 字段）。</param>
        void PlayCustom(string name);

        /// <summary>
        /// 播放自定义持续振动。
        /// </summary>
        /// <param name="intensity">振动强度（0~1）。</param>
        /// <param name="sharpness">振动锐度（0~1）。</param>
        /// <param name="preDuration">前置等待时长（秒）。</param>
        /// <param name="duration">振动持续时长（秒）。</param>
        void PlayCustom(float intensity, float sharpness, float preDuration, float duration);

        /// <summary>
        /// 按名称播放强调振动组合，未命中时输出 Warning 并静默返回。
        /// </summary>
        /// <param name="name">振动分组名称（对应数据表 Name 字段）。</param>
        void PlayEmphasis(string name);

        /// <summary>
        /// 播放强调振动。
        /// </summary>
        /// <param name="amplitude">振幅（0~1）。</param>
        /// <param name="frequency">频率（0~1）。</param>
        /// <param name="preDuration">前置等待时长（秒）。</param>
        /// <param name="interval">振动间隔时长（秒）。</param>
        void PlayEmphasis(float amplitude, float frequency, float preDuration, float interval);

        /// <summary>
        /// 停止所有振动。
        /// </summary>
        void StopAll();

        /// <summary>
        /// 获取或设置振动是否启用。
        /// </summary>
        bool Enable { get; set; }

        /// <summary>
        /// 获取当前设备是否支持振动。
        /// </summary>
        bool IsSupported { get; }
    }
}
