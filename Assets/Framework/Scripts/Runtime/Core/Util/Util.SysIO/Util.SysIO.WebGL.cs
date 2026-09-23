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
using AOT;
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
                /// WebGL 文件系统同步完成回调。
                /// </summary>
                /// <param name="errorCode">0 表示成功，非 0 表示同步到 IndexedDB 失败。</param>
                private delegate void SyncFsCompletedCallback(int errorCode);

                private static readonly SyncFsCompletedCallback s_SyncFsCompletedCallback = OnSyncFsCompleted;

                /// <summary>
                /// 请求将 WebGL 虚拟文件系统同步到 IndexedDB。
                /// JS 层会串行执行并合并重叠请求，避免同时发起多个 FS.syncfs。
                /// </summary>
                [DllImport("__Internal", EntryPoint = "SyncFs")]
                private static extern void SyncFsNative(SyncFsCompletedCallback callback);

                /// <summary>
                /// 发起 WebGL 文件系统同步，并保持既有公共调用方式不变。
                /// </summary>
                public static void SyncFs()
                {
                    SyncFsNative(s_SyncFsCompletedCallback);
                }

                /// <summary>
                /// 接收 JS 层 IndexedDB 同步结果；失败时写入 Nova 错误日志。
                /// </summary>
                /// <param name="errorCode">0 表示成功，非 0 表示同步失败。</param>
                [MonoPInvokeCallback(typeof(SyncFsCompletedCallback))]
                private static void OnSyncFsCompleted(int errorCode)
                {
                    if (errorCode != 0)
                    {
                        Log.Error(LogTag.SysIO, "WebGL 文件系统同步到 IndexedDB 失败，请检查浏览器存储权限或配额。");
                    }
                }

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
