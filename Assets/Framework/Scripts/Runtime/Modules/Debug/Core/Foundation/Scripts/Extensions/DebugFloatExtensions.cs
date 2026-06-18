/***************************************************************
 * (c) copyright 2026 - 2030, Solotopia
 * All Rights Reserved.
 * -------------------------------------------------------------
 * filename:  DebugFloatExtensions.cs
 * author:    yingzheng
 * created:   2026/6/12
 * descrip:   Nova Runtime Debugger 运行时代码。
 ***************************************************************/
namespace NovaFramework.Runtime
{
    using UnityEngine;

    public static class DebugFloatExtensions
    {
        public static float Sqr(this float f)
        {
            return f*f;
        }

        public static float SqrRt(this float f)
        {
            return Mathf.Sqrt(f);
        }

        public static bool ApproxZero(this float f)
        {
            return Mathf.Approximately(0, f);
        }

        public static bool Approx(this float f, float f2)
        {
            return Mathf.Approximately(f, f2);
        }
    }
}
