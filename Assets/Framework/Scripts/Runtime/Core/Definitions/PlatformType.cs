/***************************************************************
 * (c) copyright 2026 - 2030, Solotopia
 * All Rights Reserved.
 * -------------------------------------------------------------
 * filename:  PlatformType.cs
 * author:    taoye
 * created:   2026/4/29
 * descrip:   运行平台类型
 ***************************************************************/

using System;

namespace NovaFramework.Runtime
{
    /// <summary>
    /// 运行平台类型。
    /// </summary>
    [Serializable]
    public enum PlatformType : byte
    {
        /// <summary>
        /// 无效平台。
        /// </summary>
        None = 0,

        /// <summary>
        /// Android 平台。
        /// </summary>
        Android = 1,

        /// <summary>
        /// iOS 平台。
        /// </summary>
        iOS = 2,

        /// <summary>
        /// WebGL 平台。
        /// </summary>
        WebGL = 3
    }
}
