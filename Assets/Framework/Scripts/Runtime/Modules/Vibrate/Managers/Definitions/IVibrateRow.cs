/***************************************************************
 * (c) copyright 2026 - 2030, Solotopia
 * All Rights Reserved.
 * -------------------------------------------------------------
 * filename:  IVibrateRow.cs
 * author:    taoye
 * created:   2026/4/24
 * descrip:   振动数据行接口定义
 ***************************************************************/

namespace NovaFramework.Runtime
{
    /// <summary>
    /// 振动数据行的基接口，定义所有振动类型共有的字段契约。
    /// Luban 生成的 Emphasis 和 Custom bean 类须实现对应子接口，
    /// 框架侧通过接口直接访问字段，彻底消除反射和 cast 开销。
    /// </summary>
    public interface IVibrateRow
    {
        /// <summary>
        /// 振动分组名称，用于按组检索数据行。
        /// </summary>
        string Name { get; }

        /// <summary>
        /// 组内排序序号，升序播放。
        /// </summary>
        int Order { get; }

        /// <summary>
        /// 前置等待时长（秒），在本步振动触发前先等待该时长。
        /// </summary>
        float PreDuration { get; }
    }

    /// <summary>
    /// Emphasis 强调振动数据行接口，继承 IVibrateRow 并扩展强调振动特有字段。
    /// Luban 生成的 VibrateEmphasis bean 类须实现此接口。
    /// </summary>
    public interface IVibrateEmphasisRow : IVibrateRow
    {
        /// <summary>
        /// 振幅（0~1），控制强调振动的力度。
        /// </summary>
        float Amplitude { get; }

        /// <summary>
        /// 频率（0~1），控制强调振动的频率感。
        /// </summary>
        float Frequency { get; }

        /// <summary>
        /// 振动间隔时长（秒），本步强调振动触发后等待该时长再进入下一步。
        /// </summary>
        float Interval { get; }
    }

    /// <summary>
    /// Custom 自定义持续振动数据行接口，继承 IVibrateRow 并扩展自定义振动特有字段。
    /// Luban 生成的 VibrateCustom bean 类须实现此接口。
    /// </summary>
    public interface IVibrateCustomRow : IVibrateRow
    {
        /// <summary>
        /// 振动强度（0~1），控制持续振动的力度。
        /// </summary>
        float Intensity { get; }

        /// <summary>
        /// 振动锐度（0~1），控制持续振动的质感。
        /// </summary>
        float Sharpness { get; }

        /// <summary>
        /// 振动持续时长（秒），本步持续振动的时长。
        /// </summary>
        float Duration { get; }
    }
}
