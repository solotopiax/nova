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
            /// 在 WebGL 中请求将虚拟文件系统写入 IndexedDB。
            /// 请求异步执行，底层会串行合并重叠同步并在失败时记录错误。
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
