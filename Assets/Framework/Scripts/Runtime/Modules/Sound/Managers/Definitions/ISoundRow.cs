/***************************************************************
 * (c) copyright 2026 - 2030, Solotopia
 * All Rights Reserved.
 * -------------------------------------------------------------
 * filename:  ISoundRow.cs
 * author:    taoye
 * created:   2026/4/26
 * descrip:   声音数据行接口
 ***************************************************************/

namespace NovaFramework.Runtime
{
    /// <summary>
    /// 声音数据行接口。
    /// Luban 生成的声音 bean 类须实现此接口，框架侧通过接口直接访问字段。
    /// </summary>
    public interface ISoundRow
    {
        /// <summary>
        /// 声音名称（主键）。
        /// </summary>
        string Name { get; }

        /// <summary>
        /// 声音描述。
        /// </summary>
        string Desc { get; }

        /// <summary>
        /// Asset 地址。
        /// </summary>
        string AssetLocation { get; }

        /// <summary>
        /// 声音组名称。
        /// </summary>
        string GroupName { get; }

        /// <summary>
        /// 播放优先级。
        /// </summary>
        int Priority { get; }

        /// <summary>
        /// 是否循环播放。
        /// </summary>
        bool Loop { get; }

        /// <summary>
        /// 音量。
        /// </summary>
        float Volume { get; }

        /// <summary>
        /// 空间混合度（0=2D，1=3D）。
        /// </summary>
        float SpatialBlend { get; }
    }
}
