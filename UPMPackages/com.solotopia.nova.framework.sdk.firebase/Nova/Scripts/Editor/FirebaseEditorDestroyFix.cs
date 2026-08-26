/***************************************************************
 * (c) copyright 2026 - 2030, Solotopia
 * All Rights Reserved.
 * -------------------------------------------------------------
 * filename:  FirebaseEditorDestroyFix.cs
 * author:    yingzheng
 * created:   2026/6/5
 * descrip:   修复编辑器模式下如果firebase没有初始化会有报错问题
 ***************************************************************/

using UnityEditor;
using UnityEngine;
using Object = UnityEngine.Object;

namespace NovaFramework.SDK.FirebasePlugin.Editor
{
    /// <summary>
    /// Firebase Editor 播放态残留对象清理。
    /// 通过 InitializeOnLoad 在域重载后注册退出 Play Mode 回调，避免静态构造器无人引用时不生效。
    /// </summary>
    [InitializeOnLoad]
    public static class FirebaseEditorDestroyFix
    {
        static FirebaseEditorDestroyFix()
        {
            EditorApplication.playModeStateChanged += (state) =>
            {
                if (state == PlayModeStateChange.ExitingPlayMode)
                {
                    // 延迟一帧执行，避免时序问题
                    EditorApplication.delayCall += () =>
                    {
                        var handler = GameObject.Find("FirebaseHandler");
                        if (handler != null)
                        {
                            Object.DestroyImmediate(handler);
                        }
                    };
                }
            };
        }
    }
}
