/***************************************************************
 * (c) copyright 2026 - 2030, Solotopia
 * All Rights Reserved.
 * -------------------------------------------------------------
 * filename:  Util.Convert.cs
 * author:    taoye
 * created:   2026/1/27
 * descrip:   类型转换工具 —— 根定义、ScreenDpi、单位转换
 ***************************************************************/

using System;

namespace NovaFramework.Runtime
{
    public static partial class Util
    {
        /// <summary>
        /// 类型转换工具。
        /// </summary>
        public static partial class Convert
        {
            /// <summary>
            /// 1 英寸等于 2.54 厘米。
            /// </summary>
            private const float c_InchesToCentimeters = 2.54f;
            /// <summary>
            /// 1 厘米等于 1/2.54 英寸。
            /// </summary>
            private const float c_CentimetersToInches = 1f / c_InchesToCentimeters;

            /// <summary>
            /// 获取数据在此计算机结构中存储时的字节顺序。
            /// </summary>
            public static bool IsLittleEndian => BitConverter.IsLittleEndian;

            /// <summary>
            /// 屏幕每英寸点数。
            /// </summary>
            private static float s_ScreenDpi;
            /// <summary>
            /// 获取屏幕每英寸点数。
            /// </summary>
            public static float ScreenDpi => s_ScreenDpi;

            /// <summary>
            /// 设置屏幕每英寸点数。
            /// </summary>
            /// <param name="dpi">DPI 值。</param>
            internal static void SetScreenDpi(float dpi)
            {
                s_ScreenDpi = dpi;
            }

            /// <summary>
            /// 将像素转换为厘米。
            /// </summary>
            /// <param name="pixels">像素。</param>
            /// <returns>厘米。</returns>
            public static float PixelsToCentimeters(float pixels)
            {
                if (s_ScreenDpi <= 0)
                {
                    throw new InvalidOperationException("必须先设置屏幕每英寸点数 ScreenDpi。");
                }

                return c_InchesToCentimeters * pixels / s_ScreenDpi;
            }

            /// <summary>
            /// 将厘米转换为像素。
            /// </summary>
            /// <param name="centimeters">厘米。</param>
            /// <returns>像素。</returns>
            public static float CentimetersToPixels(float centimeters)
            {
                if (s_ScreenDpi <= 0)
                {
                    throw new InvalidOperationException("必须先设置屏幕每英寸点数 ScreenDpi。");
                }

                return c_CentimetersToInches * centimeters * s_ScreenDpi;
            }

            /// <summary>
            /// 将像素转换为英寸。
            /// </summary>
            /// <param name="pixels">像素。</param>
            /// <returns>英寸。</returns>
            public static float PixelsToInches(float pixels)
            {
                if (s_ScreenDpi <= 0)
                {
                    throw new InvalidOperationException("必须先设置屏幕每英寸点数 ScreenDpi。");
                }

                return pixels / s_ScreenDpi;
            }

            /// <summary>
            /// 将英寸转换为像素。
            /// </summary>
            /// <param name="inches">英寸。</param>
            /// <returns>像素。</returns>
            public static float InchesToPixels(float inches)
            {
                if (s_ScreenDpi <= 0)
                {
                    throw new InvalidOperationException("必须先设置屏幕每英寸点数 ScreenDpi。");
                }

                return inches * s_ScreenDpi;
            }
        }
    }
}
