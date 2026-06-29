/***************************************************************
 * (c) copyright 2026 - 2030, Solotopia
 * All Rights Reserved.
 * -------------------------------------------------------------
 * filename:  DebuggerUIColor.cs
 * author:    yingzheng
 * created:   2026/6/26
 ***************************************************************/
namespace NovaFramework.Runtime
{
    using UnityEngine;

    internal static class DebuggerUIColor
    {
        internal static class SideBarTab
        {
            public static readonly Color InactiveText = new Color(46 / 255f, 94 / 255f, 174 / 255f, 1f);
            public static readonly Color InactiveIcon = InactiveText;
            public static readonly Color InactiveBorder = new Color(0.25882354f, 0.25882354f, 0.25882354f, 1f);
            public static readonly Color InactiveBackground = new Color(1, 1, 1, 0);

            public static readonly Color HoverText = new Color(4 / 255f, 50 / 255f, 99 / 255f, 1f);
            public static readonly Color HoverIcon = HoverText;

            public static readonly Color ActiveText = Color.white;
            public static readonly Color ActiveBorder = Color.white;
            public static readonly Color ActiveBackground = new Color(0 / 255f, 73 / 255f, 141 / 255f, 1f);
            public static readonly Color ActiveIcon = Color.white;
        }
    }
}