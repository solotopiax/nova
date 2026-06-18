/***************************************************************
 * (c) copyright 2026 - 2030, Solotopia
 * All Rights Reserved.
 * -------------------------------------------------------------
 * filename:  RuntimeDebuggerInit.cs
 * author:    yingzheng
 * created:   2026/6/12
 * descrip:   Nova Runtime Debugger 运行时代码。
 ***************************************************************/
using System;

namespace NovaFramework.Runtime
{
    using UnityEngine;

    /// <summary>
    /// Add this component somewhere in your scene to automatically load RuntimeDebugger when the scene is loaded.
    /// By default, RuntimeDebugger will defer loading any UI except the corner-trigger until the user requests it.
    /// It is recommended to add this to the very first scene in your game. This will ensure the console log
    /// will hold useful information about your game initialization.
    /// </summary>
    [AddComponentMenu("")]
    [Obsolete("No longer required, use Automatic initialization mode or call RuntimeDebugger.Init() manually.")]
    public class RuntimeDebuggerInit : DebugMonoBehaviourEx
    {
    }
}
