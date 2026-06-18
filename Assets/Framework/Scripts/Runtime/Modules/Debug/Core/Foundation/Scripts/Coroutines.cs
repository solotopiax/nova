/***************************************************************
 * (c) copyright 2026 - 2030, Solotopia
 * All Rights Reserved.
 * -------------------------------------------------------------
 * filename:  Coroutines.cs
 * author:    yingzheng
 * created:   2026/6/12
 * descrip:   Nova Runtime Debugger 运行时代码。
 ***************************************************************/
namespace NovaFramework.Runtime
{
    using System.Collections;
    using UnityEngine;

    public static class Coroutines
    {
        public static IEnumerator WaitForSecondsRealTime(float time)
        {
            var endTime = Time.realtimeSinceStartup + time;

            while (Time.realtimeSinceStartup < endTime)
            {
                yield return null;
            }
        }
    }
}
