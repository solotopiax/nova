/***************************************************************
 * (c) copyright 2026 - 2030, Solotopia
 * All Rights Reserved.
 * -------------------------------------------------------------
 * filename:  DebugText.cs
 * author:    yingzheng
 * created:   2026/6/12
 * descrip:   Nova Runtime Debugger 运行时代码。
 ***************************************************************/
namespace NovaFramework.Runtime
{
    using System;
    using UnityEngine;
    using UnityEngine.UI;

    /// <summary>
    /// Adds a LayoutDirty callback to the default Text component.
    /// </summary>
    [AddComponentMenu(ComponentMenuPaths.DebugText)]
    public class DebugText : Text
    {
        public event Action<DebugText> LayoutDirty;

        public override void SetLayoutDirty()
        {
            base.SetLayoutDirty();

            if (LayoutDirty != null)
            {
                LayoutDirty(this);
            }
        }
    }
}
