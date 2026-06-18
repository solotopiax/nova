/***************************************************************
 * (c) copyright 2026 - 2030, Solotopia
 * All Rights Reserved.
 * -------------------------------------------------------------
 * filename:  DebugIListExtensions.cs
 * author:    yingzheng
 * created:   2026/6/12
 * descrip:   Nova Runtime Debugger 运行时代码。
 ***************************************************************/
namespace NovaFramework.Runtime
{
    using System;
    using System.Collections.Generic;

    public static class DebugIListExtensions
    {
        public static T Random<T>(this IList<T> list)
        {
            if (list.Count == 0)
            {
                throw new IndexOutOfRangeException("List needs at least one entry to call Random()");
            }

            if (list.Count == 1)
            {
                return list[0];
            }

            return list[UnityEngine.Random.Range(0, list.Count)];
        }

        public static T RandomOrDefault<T>(this IList<T> list)
        {
            if (list.Count == 0)
            {
                return default(T);
            }

            return list.Random();
        }

        public static T PopLast<T>(this IList<T> list)
        {
            if (list.Count == 0)
            {
                throw new InvalidOperationException();
            }

            var t = list[list.Count - 1];

            list.RemoveAt(list.Count - 1);

            return t;
        }
    }
}
