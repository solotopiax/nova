/***************************************************************
 * (c) copyright 2026 - 2030, Solotopia
 * All Rights Reserved.
 * -------------------------------------------------------------
 * filename:  FirebaseEditorDestroyFix.cs
 * author:    yingzheng
 * created:   2026/6/5
 * descrip:   修复编辑器模式下如果firebase没有初始化会有报错问题
 ***************************************************************/

using System;
using System.Reflection;
using UnityEditor;
using UnityEngine;
using Object = UnityEngine.Object;

namespace NovaFramework.SDK.FirebasePlugin.Editor
{
    public static class FirebaseEditorDestroyFix
    {
        static FirebaseEditorDestroyFix()
        {
            // 劫持 Firebase 的销毁调用，自动替换为编辑模式安全方法
            var method = typeof(UnityEngine.Object)
                .GetMethod("Destroy", BindingFlags.Static | BindingFlags.Public, null, new[] { typeof(Object) }, null);

            UnityEditor.EditorApplication.playModeStateChanged += (state) =>
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
