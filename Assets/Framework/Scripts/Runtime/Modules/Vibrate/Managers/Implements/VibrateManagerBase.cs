/***************************************************************
 * (c) copyright 2026 - 2030, Solotopia
 * All Rights Reserved.
 * -------------------------------------------------------------
 * filename:  VibrateManagerBase.cs
 * author:    taoye
 * created:   2026/4/9
 * descrip:   振动管理器基类
 ***************************************************************/

using Cysharp.Threading.Tasks;

namespace NovaFramework.Runtime
{
    /// <summary>
    /// 振动管理器基类。
    /// </summary>
    internal abstract class VibrateManagerBase : FrameworkManager, IVibrateManager
    {
        /// <summary>
        /// 管理器优先级（值越小越先 Update、越后 Shutdown）。
        /// </summary>
        public override int Priority => 18;

        /// <summary>
        /// 初始化。
        /// </summary>
        /// <param name="config">振动管理器配置。</param>
        public abstract void Initialize(VibrateManagerConfig config);

        /// <summary>
        /// 管理器轮询。
        /// </summary>
        public abstract override void Update();

        /// <summary>
        /// 关闭并清理管理器。
        /// </summary>
        public abstract override void Shutdown();

        /// <summary>
        /// 加载振动数据（fire-and-forget）。
        /// </summary>
        public abstract void LoadVibrateDataSync();

        /// <summary>
        /// 异步加载振动数据。
        /// </summary>
        /// <returns>是否加载并解析成功。</returns>
        public abstract UniTask<bool> LoadVibrateDataAsync();

        /// <summary>
        /// 播放默认振动。
        /// </summary>
        public abstract void Play();

        /// <summary>
        /// 播放指定类型的预设振动。
        /// </summary>
        /// <param name="type">振动类型。</param>
        public abstract void Play(VibrateType type);

        /// <summary>
        /// 按名称播放自定义振动组合，未命中时输出 Warning 并静默返回。
        /// </summary>
        /// <param name="name">振动分组名称（对应数据表 Name 字段）。</param>
        public abstract void PlayCustom(string name);

        /// <summary>
        /// 播放自定义持续振动。
        /// </summary>
        /// <param name="intensity">振动强度（0~1）。</param>
        /// <param name="sharpness">振动锐度（0~1）。</param>
        /// <param name="preDuration">前置等待时长（秒）。</param>
        /// <param name="duration">振动持续时长（秒）。</param>
        public abstract void PlayCustom(float intensity, float sharpness, float preDuration, float duration);

        /// <summary>
        /// 按名称播放强调振动组合，未命中时输出 Warning 并静默返回。
        /// </summary>
        /// <param name="name">振动分组名称（对应数据表 Name 字段）。</param>
        public abstract void PlayEmphasis(string name);

        /// <summary>
        /// 播放强调振动。
        /// </summary>
        /// <param name="amplitude">振幅（0~1）。</param>
        /// <param name="frequency">频率（0~1）。</param>
        /// <param name="preDuration">前置等待时长（秒）。</param>
        /// <param name="interval">振动间隔时长（秒）。</param>
        public abstract void PlayEmphasis(float amplitude, float frequency, float preDuration, float interval);

        /// <summary>
        /// 停止所有振动。
        /// </summary>
        public abstract void StopAll();

        /// <summary>
        /// 获取或设置振动是否启用。
        /// </summary>
        public abstract bool Enable { get; set; }

        /// <summary>
        /// 获取当前设备是否支持振动。
        /// </summary>
        public abstract bool IsSupported { get; }
    }
}
