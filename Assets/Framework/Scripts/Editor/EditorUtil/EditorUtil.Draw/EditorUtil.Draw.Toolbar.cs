/***************************************************************
 * (c) copyright 2026 - 2030, Solotopia
 * All Rights Reserved.
 * -------------------------------------------------------------
 * filename:  EditorUtil.Draw.Toolbar.cs
 * author:    taoye
 * created:   2026/1/27
 * descrip:   编辑器绘制工具-工具栏相关
 ***************************************************************/

using System;
using UnityEditor;
using UnityEngine;

namespace NovaFramework.Editor
{
    public static partial class EditorUtil
    {
        /// <summary>
        /// 绘制工具。
        /// </summary>
        public static partial class Draw
        {
            /// <summary>
            /// 绘制工具栏。
            /// </summary>
            /// <param name="titles">标签页标题集合。</param>
            /// <param name="property">绑定的 SerializedProperty。</param>
            /// <param name="disableOnPlaying">是否在运行时禁用。</param>
            /// <param name="runtimeSetter">运行时赋值回调，可选，为 null 时不修改对象。</param>
            /// <param name="options">布局选项。</param>
            public static void Toolbar(string[] titles, SerializedProperty property, bool disableOnPlaying = true, Action<int> runtimeSetter = null, params GUILayoutOption[] options)
            {
                if (property == null)
                {
                    return;
                }

                if (titles == null || titles.Length == 0)
                {
                    return;
                }

                int currentValue = 0;
                if (property.propertyType == SerializedPropertyType.Integer)
                {
                    currentValue = property.intValue;
                }
                else if (property.propertyType == SerializedPropertyType.Enum)
                {
                    currentValue = property.enumValueIndex;
                }
                else
                {
                    Debug.LogError($"Toolbar 仅支持 Integer 或 Enum 类型的 SerializedProperty，当前类型为: {property.propertyType}");
                    return;
                }

                EditorGUI.BeginDisabledGroup(disableOnPlaying && EditorApplication.isPlaying);
                EditorGUI.BeginChangeCheck();
                int newValue = GUILayout.Toolbar(currentValue, titles, options);
                if (EditorGUI.EndChangeCheck())
                {
                    if (EditorApplication.isPlaying)
                    {
                        runtimeSetter?.Invoke(newValue);
                    }
                    else
                    {
                        if (property.propertyType == SerializedPropertyType.Integer)
                        {
                            property.intValue = newValue;
                        }
                        else
                        {
                            property.enumValueIndex = newValue;
                        }
                        property.serializedObject.ApplyModifiedProperties();
                    }
                }
                EditorGUI.EndDisabledGroup();
            }

            /// <summary>
            /// 绘制工具栏（带高度控制）。
            /// </summary>
            /// <param name="titles">标签页标题集合。</param>
            /// <param name="property">绑定的 SerializedProperty。</param>
            /// <param name="height">工具栏高度。</param>
            /// <param name="disableOnPlaying">是否在运行时禁用。</param>
            /// <param name="runtimeSetter">运行时赋值回调，可选，为 null 时不修改对象。</param>
            public static void Toolbar(string[] titles, SerializedProperty property, float height = 20f, bool disableOnPlaying = true, Action<int> runtimeSetter = null)
            {
                Toolbar(titles, property, disableOnPlaying, runtimeSetter, GUILayout.Height(height));
            }
        }   
    }
}
