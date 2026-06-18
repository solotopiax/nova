/***************************************************************
 * (c) copyright 2026 - 2030, Solotopia
 * All Rights Reserved.
 * -------------------------------------------------------------
 * filename:  DebugAssert.cs
 * author:    yingzheng
 * created:   2026/6/12
 * descrip:   Nova Runtime Debugger 运行时代码。
 ***************************************************************/
namespace NovaFramework.Runtime
{
using System;
using System.Collections.Generic;
using System.Diagnostics;
using UnityEngine;
using Debug = UnityEngine.Debug;

public static class DebugAssert
{
    public const int LineBufferCount = 512;
    public static bool IsFixedUpdate { get; set; }

    [DebuggerNonUserCode]
    [DebuggerStepThrough]
    public static void AssertNotNull(object value, string message = null, MonoBehaviour instance = null)
    {
        if (!EqualityComparer<object>.Default.Equals(value, null))
        {
            return;
        }

        message = message != null ? "NotNullAssert Failed: {0}".Fmt(message) : "Assert Failed";

        Debug.LogError(message, instance);

        if (instance != null)
        {
            instance.enabled = false;
        }

        throw new NullReferenceException(message);
    }

    [DebuggerNonUserCode]
    [DebuggerStepThrough]
    public static void Assert(bool condition, string message = null, MonoBehaviour instance = null)
    {
        if (condition)
        {
            return;
        }

        message = message != null ? "Assert Failed: {0}".Fmt(message) : "Assert Failed";

        Debug.LogError(message, instance);
        throw new Exception(message);
    }

    [Conditional("UNITY_EDITOR")]
    [DebuggerNonUserCode]
    [DebuggerStepThrough]
    public static void EditorAssertNotNull(object value, string message = null, MonoBehaviour instance = null)
    {
        AssertNotNull(value, message, instance);
    }

    [Conditional("UNITY_EDITOR")]
    [DebuggerNonUserCode]
    [DebuggerStepThrough]
    public static void EditorAssert(bool condition, string message = null, MonoBehaviour instance = null)
    {
        Assert(condition, message, instance);
    }
}
}
