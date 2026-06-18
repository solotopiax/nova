/***************************************************************
 * (c) copyright 2026 - 2030, Solotopia
 * All Rights Reserved.
 * -------------------------------------------------------------
 * filename:  DebugOptionAttributes.cs
 * author:    yingzheng
 * created:   2026/6/12
 * descrip:   Nova Runtime Debugger 运行时代码。
 ***************************************************************/
using System;

namespace NovaFramework.Runtime
{
    [AttributeUsage(AttributeTargets.Property)]
    public class NumberRangeAttribute : Attribute
    {
        public readonly double Max;
        public readonly double Min;

        public NumberRangeAttribute(double min, double max)
        {
            Min = min;
            Max = max;
        }
    }

    [AttributeUsage(AttributeTargets.Property)]
    public class IncrementAttribute : Attribute
    {
        public readonly double Increment;

        public IncrementAttribute(double increment)
        {
            Increment = increment;
        }
    }

    [AttributeUsage(AttributeTargets.Property | AttributeTargets.Method)]
    public class SortAttribute : Attribute
    {
        public readonly int SortPriority;

        public SortAttribute(int priority)
        {
            SortPriority = priority;
        }
    }
}