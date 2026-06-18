/***************************************************************
 * (c) copyright 2026 - 2030, Solotopia
 * All Rights Reserved.
 * -------------------------------------------------------------
 * filename:  DebugOptions.Attributes.cs
 * author:    yingzheng
 * created:   2026/6/12
 * descrip:   Nova Runtime Debugger 运行时代码。
 ***************************************************************/
using System;

namespace NovaFramework.Runtime
{
public partial class DebugOptions
{
    // For compatibility with older versions of RuntimeDebugger, this simply inherits from the component model version.

    [AttributeUsage(AttributeTargets.Property | AttributeTargets.Method)]
    public sealed class DisplayNameAttribute : System.ComponentModel.DisplayNameAttribute
    {
        public DisplayNameAttribute(string displayName) : base(displayName)
        {
        }
    }

    // These attributes are used when using DebugOptions. Options added via RuntimeDebugger.Instance.AddOptionsContainer can use the attribute defined in RuntimeDebugger namespace.

    [AttributeUsage(AttributeTargets.Property)]
    public sealed class IncrementAttribute :
#if DISABLE_RUNTIME_DEBUGGER
        Attribute
#else
        NovaFramework.Runtime.IncrementAttribute
#endif
    {
        public IncrementAttribute(double increment)
#if !DISABLE_RUNTIME_DEBUGGER
            : base(increment)
#endif
        {
        }
    }

    [AttributeUsage(AttributeTargets.Property)]
    public sealed class NumberRangeAttribute :
#if DISABLE_RUNTIME_DEBUGGER
        Attribute
#else
        NovaFramework.Runtime.NumberRangeAttribute
#endif
    {
        public NumberRangeAttribute(double min, double max)
#if !DISABLE_RUNTIME_DEBUGGER
            : base(min, max)
#endif
        {
        }
    }

    [AttributeUsage(AttributeTargets.Property | AttributeTargets.Method)]
    public sealed class SortAttribute :
#if DISABLE_RUNTIME_DEBUGGER
        Attribute
#else
        NovaFramework.Runtime.SortAttribute
#endif
    {
        public SortAttribute(int priority)
#if !DISABLE_RUNTIME_DEBUGGER
            : base(priority)
#endif
        {
        }
    }
}
}
