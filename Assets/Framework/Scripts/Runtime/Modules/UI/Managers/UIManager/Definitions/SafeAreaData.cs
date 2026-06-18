/***************************************************************
 * (c) copyright 2026 - 2030, Solotopia
 * All Rights Reserved.
 * -------------------------------------------------------------
 * filename:  SafeAreaData.cs
 * author:    taoye
 * created:   2026/04/24
 * descrip:   设备安全区域原始数据
 ***************************************************************/

namespace NovaFramework.Runtime
{
    /// <summary>
    /// 设备安全区域原始数据。
    /// </summary>
    public struct SafeAreaData
    {
        /// <summary>
        /// 安全区域左边界（逻辑坐标）。
        /// </summary>
        public float Left;

        /// <summary>
        /// 安全区域右边界（逻辑坐标）。
        /// </summary>
        public float Right;

        /// <summary>
        /// 安全区域上边界（逻辑坐标）。
        /// </summary>
        public float Top;

        /// <summary>
        /// 安全区域下边界（逻辑坐标）。
        /// </summary>
        public float Bottom;

        /// <summary>
        /// 设备像素比（物理像素 / 逻辑像素）。
        /// </summary>
        public float PixelRatio;

        /// <summary>
        /// 屏幕逻辑高度。
        /// </summary>
        public float ScreenHeight;
    }
}
