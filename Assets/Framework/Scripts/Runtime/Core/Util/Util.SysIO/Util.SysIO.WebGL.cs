/***************************************************************
 * (c) copyright 2026 - 2030, Solotopia
 * All Rights Reserved.
 * -------------------------------------------------------------
 * filename:  Util.SysIO.WebGL.cs
 * author:    taoye
 * created:   2024/9/24
 * descrip:   系统IO工具-WebGL相关
 ***************************************************************/

#if UNITY_WEBGL && !UNITY_EDITOR
using System.Runtime.InteropServices;

namespace NovaFramework.Runtime
{
    public static partial class Util
    {
        public static partial class SysIO
        {
            public static class WebGLExtensions
            {
                /// <summary>
                /// Calls FS.syncfs in native js.
                /// </summary>
                [DllImport("__Internal")]
                public static extern void SyncFs();

                /// <summary>
                /// Invokes window.open() with the specified parameters.
                /// https://developer.mozilla.org/en-US/docs/Web/API/Window/open
                /// </summary>
                [DllImport("__Internal")]
                public static extern void OpenURL(string url, string target);
            }
        }   
    }
}

#endif
