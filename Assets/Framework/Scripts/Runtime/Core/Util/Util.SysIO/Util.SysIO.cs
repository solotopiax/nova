/***************************************************************
 * (c) copyright 2026 - 2030, Solotopia
 * All Rights Reserved.
 * -------------------------------------------------------------
 * filename:  Util.SysIO.cs
 * author:    taoye
 * created:   2024/9/24
 * descrip:   系统IO工具
 ***************************************************************/

namespace NovaFramework.Runtime
{
    public static partial class Util
    {
        public static partial class SysIO
        {
            /// <summary>
            /// 在 WebGL 中将缓存的文件写入 IndexedDB。
            /// https://forum.unity.com/threads/webgl-filesystem.294358/#post-1940712
            /// </summary>
            public static void WebGLSyncFs()
            {
#if UNITY_WEBGL && !UNITY_EDITOR
                SysIO.WebGLExtensions.SyncFs();
#endif
            }
        }   
    }
    
}


