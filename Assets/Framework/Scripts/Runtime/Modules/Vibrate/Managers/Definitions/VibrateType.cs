/***************************************************************
 * (c) copyright 2026 - 2030, Solotopia
 * All Rights Reserved.
 * -------------------------------------------------------------
 * filename:  VibrateType.cs
 * author:    taoye
 * created:   2026/4/9
 * descrip:   振动类型枚举
 ***************************************************************/

namespace NovaFramework.Runtime
{
    /// <summary>
    /// 振动类型枚举。
    /// </summary>
    public enum VibrateType : byte
    {
        /// <summary>
        /// 无振动。
        /// </summary>
        None = 0,

        /// <summary>
        /// 选择反馈。
        /// </summary>
        Selection,

        /// <summary>
        /// 成功反馈。
        /// </summary>
        Success,

        /// <summary>
        /// 警告反馈。
        /// </summary>
        Warning,

        /// <summary>
        /// 失败反馈。
        /// </summary>
        Failure,

        /// <summary>
        /// 轻度撞击。
        /// </summary>
        LightImpact,

        /// <summary>
        /// 中度撞击。
        /// </summary>
        MediumImpact,

        /// <summary>
        /// 重度撞击。
        /// </summary>
        HeavyImpact,

        /// <summary>
        /// 刚性撞击。
        /// </summary>
        RigidImpact,

        /// <summary>
        /// 柔性撞击。
        /// </summary>
        SoftImpact,
    }
}
