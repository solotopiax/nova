/***************************************************************
 * (c) copyright 2026 - 2030, Solotopia
 * All Rights Reserved.
 * -------------------------------------------------------------
 * filename:  EditorUtil.Draw.Property.cs
 * author:    taoye
 * created:   2026/1/15
 * descrip:   编辑器绘制工具-属性
 ***************************************************************/

using System;
using System.Collections.Generic;
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
            /// EnumPopup 居中对齐样式（延迟初始化）。
            /// </summary>
            private static GUIStyle s_CenteredPopupStyle;

            /// <summary>
            /// TypesSelector 缓存的类型名称数组，避免每帧 ToArray。
            /// </summary>
            private static string[] s_TypesSelectorCachedArray;

            /// <summary>
            /// TypesSelector 缓存数组对应的源列表引用。
            /// </summary>
            private static List<string> s_TypesSelectorCachedSource;

            /// <summary>
            /// TypesSelector 缓存数组对应的源列表元素数量。
            /// </summary>
            private static int s_TypesSelectorCachedCount;

            /// <summary>
            /// 绘制文本内容。
            /// </summary>
            /// <param name="text">文本内容。</param>
            /// <param name="content">内容。</param>
            /// <param name="disableOnPlaying">是否在运行时禁用。</param>
            public static void Label(string text, string content, bool disableOnPlaying = true)
            {
                EditorGUI.BeginDisabledGroup(disableOnPlaying && EditorApplication.isPlaying);
                EditorGUILayout.LabelField(text, content);
                EditorGUI.EndDisabledGroup();
            }

            /// <summary>
            /// 绘制文本内容。
            /// </summary>
            /// <param name="text">文本内容。</param>
            /// <param name="disableOnPlaying">是否在运行时禁用。</param>
            /// <param name="options">布局选项。</param>
            public static void Label(string text, bool disableOnPlaying = true, params GUILayoutOption[] options)
            {
                EditorGUI.BeginDisabledGroup(disableOnPlaying && EditorApplication.isPlaying);
                EditorGUILayout.LabelField(text, options);
                EditorGUI.EndDisabledGroup();
            }

            /// <summary>
            /// 绘制文本内容（带样式）。
            /// </summary>
            /// <param name="text">文本内容。</param>
            /// <param name="style">GUI样式。</param>
            /// <param name="disableOnPlaying">是否在运行时禁用。</param>
            /// <param name="options">布局选项。</param>
            public static void Label(string text, GUIStyle style, bool disableOnPlaying = true, params GUILayoutOption[] options)
            {
                EditorGUI.BeginDisabledGroup(disableOnPlaying && EditorApplication.isPlaying);
                EditorGUILayout.LabelField(text, style, options);
                EditorGUI.EndDisabledGroup();
            }

            /// <summary>
            /// 内联紧凑文本标签（无样式版）：按内容宽自适应、无 EditorGUILayout 的 labelWidth 占位，
            /// 用于同行多元素紧贴排版（如标题行与多个 Toggle 并排）。
            /// </summary>
            /// <param name="text">文本内容。</param>
            /// <param name="disableOnPlaying">是否在运行时禁用。</param>
            /// <param name="options">布局选项（默认按内容宽自适应，无需传 ExpandWidth）。</param>
            public static void LabelInline(string text, bool disableOnPlaying = true, params GUILayoutOption[] options)
            {
                EditorGUI.BeginDisabledGroup(disableOnPlaying && EditorApplication.isPlaying);
                GUILayout.Label(text, options);
                EditorGUI.EndDisabledGroup();
            }

            /// <summary>
            /// 内联紧凑文本标签（带样式版）：按内容宽自适应、无 EditorGUILayout 的 labelWidth 占位，
            /// 用于同行多元素紧贴排版（如标题行与多个 Toggle 并排）。
            /// </summary>
            /// <param name="text">文本内容。</param>
            /// <param name="style">GUI 样式。</param>
            /// <param name="disableOnPlaying">是否在运行时禁用。</param>
            /// <param name="options">布局选项（默认按内容宽自适应，无需传 ExpandWidth）。</param>
            public static void LabelInline(string text, GUIStyle style, bool disableOnPlaying = true, params GUILayoutOption[] options)
            {
                EditorGUI.BeginDisabledGroup(disableOnPlaying && EditorApplication.isPlaying);
                GUILayout.Label(text, style, options);
                EditorGUI.EndDisabledGroup();
            }

            /// <summary>
            /// 绘制可编辑文本框（纯字符串，返回编辑后的值）。
            /// </summary>
            /// <param name="value">当前文本值。</param>
            /// <param name="disableOnPlaying">是否在运行时禁用。</param>
            /// <param name="options">布局选项。</param>
            /// <returns>编辑后的文本值。</returns>
            public static string TextField(string value, bool disableOnPlaying = true, params GUILayoutOption[] options)
            {
                EditorGUI.BeginDisabledGroup(disableOnPlaying && EditorApplication.isPlaying);
                string newValue = EditorGUILayout.TextField(value ?? string.Empty, options);
                EditorGUI.EndDisabledGroup();
                return newValue;
            }

            /// <summary>
            /// 绘制延迟提交文本框（仅在用户按 Enter 或控件失焦时返回新值，编辑过程中持续返回旧值）。
            /// 适用于"切换上下文坐标后须按坐标重读显示值"的场景，规避 IMGUI TextField 聚焦态内部缓冲与外部值冲突。
            /// </summary>
            /// <param name="value">当前文本值。</param>
            /// <param name="disableOnPlaying">是否在运行时禁用。</param>
            /// <param name="options">布局选项。</param>
            /// <returns>提交后的文本值；未提交时返回传入的 value。</returns>
            public static string DelayedTextField(string value, bool disableOnPlaying = true, params GUILayoutOption[] options)
            {
                EditorGUI.BeginDisabledGroup(disableOnPlaying && EditorApplication.isPlaying);
                string newValue = EditorGUILayout.DelayedTextField(value ?? string.Empty, options);
                EditorGUI.EndDisabledGroup();
                return newValue;
            }

            /// <summary>
            /// 绘制可编辑文本框（自定义样式，纯字符串，返回编辑后的值）。
            /// </summary>
            /// <param name="value">当前文本值。</param>
            /// <param name="style">自定义 GUIStyle。</param>
            /// <param name="disableOnPlaying">是否在运行时禁用。</param>
            /// <param name="options">布局选项。</param>
            /// <returns>编辑后的文本值。</returns>
            public static string TextField(string value, GUIStyle style, bool disableOnPlaying = true, params GUILayoutOption[] options)
            {
                EditorGUI.BeginDisabledGroup(disableOnPlaying && EditorApplication.isPlaying);
                string newValue = EditorGUILayout.TextField(value ?? string.Empty, style, options);
                EditorGUI.EndDisabledGroup();
                return newValue;
            }

            /// <summary>
            /// 绘制可编辑文本框（默认样式）。
            /// </summary>
            /// <param name="property">绑定的 SerializedProperty（字符串类型）。</param>
            /// <param name="disableOnPlaying">是否在运行时禁用。</param>
            /// <param name="onComplete">完成回调。</param>
            /// <param name="options">布局选项。</param>
            public static void TextField(SerializedProperty property, bool disableOnPlaying = true, Action onComplete = null, params GUILayoutOption[] options)
            {
                if (property == null)
                {
                    return;
                }

                EditorGUI.BeginDisabledGroup(disableOnPlaying && EditorApplication.isPlaying);
                string newValue = EditorGUILayout.TextField(property.stringValue, options);
                if (newValue != property.stringValue)
                {
                    property.stringValue = newValue;
                    property.serializedObject.ApplyModifiedProperties();
                    onComplete?.Invoke();
                }
                EditorGUI.EndDisabledGroup();
            }

            /// <summary>
            /// 绘制可编辑文本框（指定样式）。
            /// </summary>
            /// <param name="property">绑定的 SerializedProperty（字符串类型）。</param>
            /// <param name="style">输入框样式。</param>
            /// <param name="disableOnPlaying">是否在运行时禁用。</param>
            /// <param name="onComplete">完成回调。</param>
            /// <param name="options">布局选项。</param>
            public static void TextField(SerializedProperty property, GUIStyle style, bool disableOnPlaying = true, Action onComplete = null, params GUILayoutOption[] options)
            {
                if (property == null)
                {
                    return;
                }

                EditorGUI.BeginDisabledGroup(disableOnPlaying && EditorApplication.isPlaying);
                string newValue = EditorGUILayout.TextField(property.stringValue, style, options);
                if (newValue != property.stringValue)
                {
                    property.stringValue = newValue;
                    property.serializedObject.ApplyModifiedProperties();
                    onComplete?.Invoke();
                }
                EditorGUI.EndDisabledGroup();
            }

            /// <summary>
            /// 绘制属性。
            /// </summary>
            /// <param name="property">绑定的 SerializedProperty。</param>
            /// <param name="disableOnPlaying">是否在运行时禁用。</param>
            /// <param name="options">布局选项。</param>
            public static void Property(SerializedProperty property, bool disableOnPlaying = true, params GUILayoutOption[] options)
            {
                if (property == null)
                {
                    return;
                }

                EditorGUI.BeginDisabledGroup(disableOnPlaying && EditorApplication.isPlaying);
                EditorGUILayout.PropertyField(property, options);
                property.serializedObject.ApplyModifiedProperties();
                EditorGUI.EndDisabledGroup();
            }

            /// <summary>
            /// 绘制属性。
            /// </summary>
            /// <param name="label">标签名。</param>
            /// <param name="property">绑定的 SerializedProperty。</param>
            /// <param name="disableOnPlaying">是否在运行时禁用。</param>
            /// <param name="options">布局选项。</param>
            public static void Property(string label, SerializedProperty property, bool disableOnPlaying = true, params GUILayoutOption[] options)
            {
                if (property == null)
                {
                    return;
                }

                EditorGUILayout.BeginHorizontal();
                EditorGUI.BeginDisabledGroup(disableOnPlaying && EditorApplication.isPlaying);
                EditorGUILayout.LabelField(label, options);
                EditorGUILayout.PropertyField(property, GUIContent.none, true);
                EditorGUI.EndDisabledGroup();
                EditorGUILayout.EndHorizontal();
                
                property.serializedObject.ApplyModifiedProperties();
            }
            
            /// <summary>
            /// 绘制 Object 引用字段（带标签），返回选中对象（类型安全）。
            /// </summary>
            /// <param name="label">标签名。</param>
            /// <param name="value">当前引用值。</param>
            /// <param name="allowSceneObjects">是否允许选择场景对象。</param>
            /// <param name="disableOnPlaying">是否在运行时禁用。</param>
            /// <param name="options">布局选项。</param>
            /// <typeparam name="T">UnityEngine.Object 的子类型。</typeparam>
            /// <returns>用户选择的对象，未选择时返回 null。</returns>
            public static T ObjectField<T>(string label, T value, bool allowSceneObjects = false, bool disableOnPlaying = true, params GUILayoutOption[] options) where T : UnityEngine.Object
            {
                EditorGUI.BeginDisabledGroup(disableOnPlaying && EditorApplication.isPlaying);
                T result = (T)EditorGUILayout.ObjectField(label, value, typeof(T), allowSceneObjects, options);
                EditorGUI.EndDisabledGroup();
                return result;
            }

            /// <summary>
            /// 绘制布尔勾选（纯值版本，不绑定 SerializedProperty），返回用户操作后的值。
            /// </summary>
            /// <param name="label">标签名。</param>
            /// <param name="value">当前值。</param>
            /// <param name="disableOnPlaying">是否在运行时禁用。</param>
            /// <param name="options">布局选项。</param>
            /// <returns>用户操作后的布尔值。</returns>
            public static bool Toggle(string label, bool value, bool disableOnPlaying = true, params GUILayoutOption[] options)
            {
                EditorGUI.BeginDisabledGroup(disableOnPlaying && EditorApplication.isPlaying);
                bool newValue = EditorGUILayout.Toggle(label, value, options);
                EditorGUI.EndDisabledGroup();
                return newValue;
            }

            /// <summary>
            /// 绘制布尔勾选（无标签内联版本，不绑定 SerializedProperty），返回用户操作后的值。
            /// 适用于行内 Toggle（如树形列表行首的启用开关），调用方自行管理周围布局。
            /// </summary>
            /// <param name="value">当前值。</param>
            /// <param name="options">布局选项（如 GUILayout.Width(18f)）。</param>
            /// <returns>用户操作后的布尔值。</returns>
            public static bool Toggle(bool value, params GUILayoutOption[] options)
            {
                return EditorGUILayout.Toggle(value, options);
            }

            /// <summary>
            /// 绘制带标签的可编辑文本框（标签+值版本），返回编辑后的值。
            /// </summary>
            /// <param name="label">标签名。</param>
            /// <param name="value">当前文本值。</param>
            /// <param name="disableOnPlaying">是否在运行时禁用。</param>
            /// <param name="options">布局选项。</param>
            /// <returns>编辑后的文本值。</returns>
            public static string TextField(string label, string value, bool disableOnPlaying = true, params GUILayoutOption[] options)
            {
                EditorGUI.BeginDisabledGroup(disableOnPlaying && EditorApplication.isPlaying);
                string newValue = EditorGUILayout.TextField(label, value ?? string.Empty, options);
                EditorGUI.EndDisabledGroup();
                return newValue;
            }

            /// <summary>
            /// 绘制整数字段。
            /// </summary>
            /// <param name="label">标签名。</param>
            /// <param name="value">当前值。</param>
            /// <param name="disableOnPlaying">是否在运行时禁用。</param>
            /// <param name="options">布局选项。</param>
            /// <returns>用户输入的值。</returns>
            public static int IntField(string label, int value, bool disableOnPlaying = true, params GUILayoutOption[] options)
            {
                EditorGUI.BeginDisabledGroup(disableOnPlaying && EditorApplication.isPlaying);
                int newValue = EditorGUILayout.IntField(label, value, options);
                EditorGUI.EndDisabledGroup();
                return newValue;
            }

            /// <summary>
            /// 绘制整数字段（无标签，适用于内联布局）。
            /// </summary>
            /// <param name="value">当前值。</param>
            /// <param name="disableOnPlaying">是否在运行时禁用。</param>
            /// <param name="options">布局选项。</param>
            /// <returns>用户输入的值。</returns>
            public static int IntField(int value, bool disableOnPlaying = true, params GUILayoutOption[] options)
            {
                EditorGUI.BeginDisabledGroup(disableOnPlaying && EditorApplication.isPlaying);
                int newValue = EditorGUILayout.IntField(value, options);
                EditorGUI.EndDisabledGroup();
                return newValue;
            }

            /// <summary>
            /// 绘制浮点数字段（无标签，适用于内联布局）。
            /// </summary>
            /// <param name="value">当前值。</param>
            /// <param name="disableOnPlaying">是否在运行时禁用。</param>
            /// <param name="options">布局选项。</param>
            /// <returns>用户输入的值。</returns>
            public static float FloatField(float value, bool disableOnPlaying = true, params GUILayoutOption[] options)
            {
                EditorGUI.BeginDisabledGroup(disableOnPlaying && EditorApplication.isPlaying);
                float newValue = EditorGUILayout.FloatField(value, options);
                EditorGUI.EndDisabledGroup();
                return newValue;
            }

            /// <summary>
            /// 绘制整数滑动条。
            /// </summary>
            /// <param name="label">标签名。</param>
            /// <param name="property">绑定的 SerializedProperty。</param>
            /// <param name="min">最小值。</param>
            /// <param name="max">最大值。</param>
            /// <param name="disableOnPlaying">是否在运行时禁用。</param>
            /// <param name="runtimeSetter">运行时赋值回调，可选，为 null 时不修改对象。</param>
            /// <param name="onComplete">完成回调。</param>
            /// <param name="options">布局选项。</param>
            public static void IntSlider(string label, SerializedProperty property, int min, int max, bool disableOnPlaying = true, Action<int> runtimeSetter = null, Action onComplete = null, params GUILayoutOption[] options)
            {
                if (!ValidateProperty(property, SerializedPropertyType.Integer, label, disableOnPlaying))
                {
                    return;
                }

                int currentValue = property.intValue;

                EditorGUILayout.BeginHorizontal();
                EditorGUI.BeginDisabledGroup(disableOnPlaying && EditorApplication.isPlaying);
                if (!string.IsNullOrEmpty(label))
                {
                    EditorGUILayout.LabelField(label, options);
                }

                EditorGUI.BeginChangeCheck();
                int newValue = EditorGUILayout.IntSlider(currentValue, min, max);
                if (EditorGUI.EndChangeCheck())
                {
                    if (EditorApplication.isPlaying)
                    {
                        runtimeSetter?.Invoke(newValue);
                    }
                    else
                    {
                        property.intValue = newValue;
                        property.serializedObject.ApplyModifiedProperties();
                    }
                    onComplete?.Invoke();
                }
                EditorGUI.EndDisabledGroup();
                EditorGUILayout.EndHorizontal();
            }

            /// <summary>
            /// 绘制浮点数滑动条。
            /// </summary>
            /// <param name="label">标签名。</param>
            /// <param name="property">绑定的 SerializedProperty。</param>
            /// <param name="min">最小值。</param>
            /// <param name="max">最大值。</param>
            /// <param name="disableOnPlaying">是否在运行时禁用。</param>
            /// <param name="runtimeSetter">运行时赋值回调，可选，为 null 时不修改对象。</param>
            /// <param name="onComplete">完成回调。</param>
            /// <param name="options">布局选项。</param>
            public static void FloatSlider(string label, SerializedProperty property, float min, float max, bool disableOnPlaying = true, Action<float> runtimeSetter = null, Action onComplete = null, params GUILayoutOption[] options)
            {
                if (!ValidateProperty(property, SerializedPropertyType.Float, label, disableOnPlaying))
                {
                    return;
                }

                float currentValue = property.floatValue;

                EditorGUILayout.BeginHorizontal();
                EditorGUI.BeginDisabledGroup(disableOnPlaying && EditorApplication.isPlaying);
                if (!string.IsNullOrEmpty(label))
                {
                    EditorGUILayout.LabelField(label, options);
                }

                EditorGUI.BeginChangeCheck();
                float newValue = EditorGUILayout.Slider(currentValue, min, max);
                if (EditorGUI.EndChangeCheck())
                {
                    if (EditorApplication.isPlaying)
                    {
                        runtimeSetter?.Invoke(newValue);
                    }
                    else
                    {
                        property.floatValue = newValue;
                        property.serializedObject.ApplyModifiedProperties();
                    }
                    onComplete?.Invoke();
                }
                EditorGUI.EndDisabledGroup();
                EditorGUILayout.EndHorizontal();
            }

            /// <summary>
            /// 绘制勾选。
            /// </summary>
            /// <param name="label">标签名。</param>
            /// <param name="property">绑定的 SerializedProperty。</param>
            /// <param name="disableOnPlaying">是否在运行时禁用。</param>
            /// <param name="runtimeSetter">运行时赋值回调，可选，为 null 时不修改对象。</param>
            /// <param name="onComplete">完成回调。</param>
            /// <param name="options">布局选项。</param>
            public static void Toggle(string label, SerializedProperty property, bool disableOnPlaying = true, Action<bool> runtimeSetter = null, Action onComplete = null, params GUILayoutOption[] options)
            {
                if (!ValidateProperty(property, SerializedPropertyType.Boolean, label, disableOnPlaying)) 
                {
                    return;
                }

                EditorGUILayout.BeginHorizontal();
                EditorGUI.BeginDisabledGroup(disableOnPlaying && EditorApplication.isPlaying);
                if (!string.IsNullOrEmpty(label))
                {
                    EditorGUILayout.LabelField(label, options);
                }
                EditorGUI.BeginChangeCheck();
                bool newValue = EditorGUILayout.Toggle(property.boolValue);
                if (EditorGUI.EndChangeCheck())
                {
                    if (EditorApplication.isPlaying)
                    {
                        runtimeSetter?.Invoke(newValue);
                    }
                    else
                    {
                        property.boolValue = newValue;
                        property.serializedObject.ApplyModifiedProperties();
                    }
                    onComplete?.Invoke();
                }
                EditorGUI.EndDisabledGroup();
                EditorGUILayout.EndHorizontal();
            }

            /// <summary>
            /// 绘制左侧文本勾选（ToggleLeft）。
            /// </summary>
            /// <param name="label">标签文本。</param>
            /// <param name="value">当前值。</param>
            /// <param name="disableOnPlaying">是否在运行时禁用。</param>
            /// <param name="options">布局选项。</param>
            /// <returns>用户操作后的值。</returns>
            public static bool ToggleLeft(string label, bool value, bool disableOnPlaying = true, params GUILayoutOption[] options)
            {
                EditorGUI.BeginDisabledGroup(disableOnPlaying && EditorApplication.isPlaying);
                bool newValue = EditorGUILayout.ToggleLeft(label, value, options);
                EditorGUI.EndDisabledGroup();
                return newValue;
            }

            /// <summary>
            /// 绘制左侧文本勾选（ToggleLeft，带样式）。
            /// </summary>
            /// <param name="label">标签文本。</param>
            /// <param name="value">当前值。</param>
            /// <param name="style">GUI样式。</param>
            /// <param name="disableOnPlaying">是否在运行时禁用。</param>
            /// <param name="options">布局选项。</param>
            /// <returns>用户操作后的值。</returns>
            public static bool ToggleLeft(string label, bool value, GUIStyle style, bool disableOnPlaying = true, params GUILayoutOption[] options)
            {
                EditorGUI.BeginDisabledGroup(disableOnPlaying && EditorApplication.isPlaying);
                bool newValue = EditorGUILayout.ToggleLeft(label, value, style, options);
                EditorGUI.EndDisabledGroup();
                return newValue;
            }

            /// <summary>
            /// 内联紧凑勾选框（checkbox 在左、文字在右）：按内容宽自适应、无 EditorGUILayout 的 labelWidth 占位，
            /// 用于同行多元素紧贴排版（如标题行与多个 Toggle 并排）。
            /// </summary>
            /// <param name="label">标签文本（显示在 checkbox 右侧）。</param>
            /// <param name="value">当前值。</param>
            /// <param name="disableOnPlaying">是否在运行时禁用。</param>
            /// <param name="options">布局选项（默认按内容宽自适应，无需传 ExpandWidth）。</param>
            /// <returns>用户操作后的布尔值。</returns>
            public static bool ToggleInline(string label, bool value, bool disableOnPlaying = true, params GUILayoutOption[] options)
            {
                EditorGUI.BeginDisabledGroup(disableOnPlaying && EditorApplication.isPlaying);
                bool newValue = GUILayout.Toggle(value, label, options);
                EditorGUI.EndDisabledGroup();
                return newValue;
            }

            /// <summary>
            /// 绘制枚举选择器（带标签）。
            /// </summary>
            /// <param name="label">标签名。</param>
            /// <param name="property">绑定的 SerializedProperty。</param>
            /// <param name="disableOnPlaying">是否在运行时禁用。</param>
            /// <param name="onComplete">完成回调。</param>
            /// <param name="options">布局选项。</param>
            /// <typeparam name="TEnum">枚举类型。</typeparam>
            public static void EnumSelector<TEnum>(string label, SerializedProperty property, bool disableOnPlaying = true, Action onComplete = null, params GUILayoutOption[] options) where TEnum : Enum
            {
                if (!ValidateProperty(property, SerializedPropertyType.Enum, label, disableOnPlaying))
                {
                    return;
                }

                TEnum currentValue = (TEnum)Enum.GetValues(typeof(TEnum)).GetValue(property.enumValueIndex);
                
                EditorGUILayout.BeginHorizontal();
                EditorGUI.BeginDisabledGroup(disableOnPlaying && EditorApplication.isPlaying);
                if (!string.IsNullOrEmpty(label))
                {
                    EditorGUILayout.LabelField(label, options);
                }
                
                EditorGUI.BeginChangeCheck();
                TEnum newValue = (TEnum)EditorGUILayout.EnumPopup(currentValue);
                if (EditorGUI.EndChangeCheck())
                {
                    property.enumValueIndex = Array.IndexOf(Enum.GetValues(typeof(TEnum)), newValue);
                    property.serializedObject.ApplyModifiedProperties();
                    onComplete?.Invoke();
                }
                EditorGUI.EndDisabledGroup();
                EditorGUILayout.EndHorizontal();
            }

            /// <summary>
            /// 绘制标志位枚举选择器（支持 [Flags] 枚举多选）。
            /// </summary>
            /// <typeparam name="TEnum">枚举类型。</typeparam>
            /// <param name="label">标签。</param>
            /// <param name="property">序列化属性。</param>
            /// <param name="disableOnPlaying">是否在运行时禁用。</param>
            /// <param name="onComplete">完成后的回调。</param>
            /// <param name="options">布局选项。</param>
            public static void EnumFlagsSelector<TEnum>(string label, SerializedProperty property, bool disableOnPlaying = true, Action onComplete = null, params GUILayoutOption[] options) where TEnum : Enum
            {
                if (!ValidateProperty(property, SerializedPropertyType.Enum, label, disableOnPlaying))
                {
                    return;
                }

                TEnum currentValue = (TEnum)Enum.ToObject(typeof(TEnum), property.intValue);

                EditorGUILayout.BeginHorizontal();
                EditorGUI.BeginDisabledGroup(disableOnPlaying && EditorApplication.isPlaying);
                if (!string.IsNullOrEmpty(label))
                {
                    EditorGUILayout.LabelField(label, options);
                }

                EditorGUI.BeginChangeCheck();
                TEnum newValue = (TEnum)EditorGUILayout.EnumFlagsField(currentValue);
                if (EditorGUI.EndChangeCheck())
                {
                    property.intValue = Convert.ToInt32(newValue);
                    property.serializedObject.ApplyModifiedProperties();
                    onComplete?.Invoke();
                }
                EditorGUI.EndDisabledGroup();
                EditorGUILayout.EndHorizontal();
            }

            /// <summary>
            /// 绘制枚举选择器（无标签，内联使用，宽度精确控制）。
            /// </summary>
            /// <param name="property">绑定的 SerializedProperty。</param>
            /// <param name="width">控件宽度（像素）。</param>
            /// <param name="disableOnPlaying">是否在运行时禁用。</param>
            /// <param name="onComplete">完成回调。</param>
            /// <typeparam name="TEnum">枚举类型。</typeparam>
            public static void EnumPopup<TEnum>(SerializedProperty property, float width, bool disableOnPlaying = true, Action onComplete = null) where TEnum : Enum
            {
                if (property == null || property.propertyType != SerializedPropertyType.Enum)
                {
                    return;
                }

                TEnum currentValue = (TEnum)Enum.GetValues(typeof(TEnum)).GetValue(property.enumValueIndex);
                EditorGUI.BeginDisabledGroup(disableOnPlaying && EditorApplication.isPlaying);
                EditorGUI.BeginChangeCheck();
                if (s_CenteredPopupStyle == null)
                {
                    s_CenteredPopupStyle = new GUIStyle(EditorStyles.popup) { alignment = TextAnchor.MiddleCenter };
                }
                TEnum newValue = (TEnum)EditorGUILayout.EnumPopup(currentValue, s_CenteredPopupStyle, GUILayout.Width(width));
                if (EditorGUI.EndChangeCheck())
                {
                    property.enumValueIndex = Array.IndexOf(Enum.GetValues(typeof(TEnum)), newValue);
                    property.serializedObject.ApplyModifiedProperties();
                    onComplete?.Invoke();
                }
                EditorGUI.EndDisabledGroup();
            }

            /// <summary>
            /// 绘制枚举选择器（纯值版，无 SerializedProperty，返回新值）。
            /// </summary>
            /// <typeparam name="TEnum">枚举类型。</typeparam>
            /// <param name="label">标签名。</param>
            /// <param name="value">当前枚举值。</param>
            /// <param name="disableOnPlaying">是否在运行时禁用。</param>
            /// <param name="options">布局选项。</param>
            /// <returns>新选中的枚举值。</returns>
            public static TEnum EnumPopup<TEnum>(string label, TEnum value, bool disableOnPlaying = true, params GUILayoutOption[] options) where TEnum : Enum
            {
                EditorGUI.BeginDisabledGroup(disableOnPlaying && EditorApplication.isPlaying);
                TEnum newValue = (TEnum)EditorGUILayout.EnumPopup(label, value, options);
                EditorGUI.EndDisabledGroup();
                return newValue;
            }

            /// <summary>
            /// 绘制非序列化属性的 Popup 下拉选择（返回新选中索引）。
            /// </summary>
            /// <param name="label">标签。</param>
            /// <param name="selectedIndex">当前选中索引。</param>
            /// <param name="displayedOptions">选项名称数组。</param>
            /// <param name="disableOnPlaying">是否在运行时禁用。</param>
            /// <param name="options">布局选项。</param>
            /// <returns>新选中索引。</returns>
            public static int Popup(string label, int selectedIndex, string[] displayedOptions, bool disableOnPlaying = true, params GUILayoutOption[] options)
            {
                EditorGUI.BeginDisabledGroup(disableOnPlaying && EditorApplication.isPlaying);
                int newIndex = EditorGUILayout.Popup(label, selectedIndex, displayedOptions, options);
                EditorGUI.EndDisabledGroup();
                return newIndex;
            }

            /// <summary>
            /// 绘制无 label 版 Popup（仅渲染下拉控件本身，调用方需自行布局 label 列）。
            /// </summary>
            /// <param name="selectedIndex">当前选中索引。</param>
            /// <param name="displayedOptions">选项名称数组。</param>
            /// <param name="disableOnPlaying">是否在运行时禁用。</param>
            /// <param name="options">布局选项。</param>
            /// <returns>新选中索引。</returns>
            public static int Popup(int selectedIndex, string[] displayedOptions, bool disableOnPlaying = true, params GUILayoutOption[] options)
            {
                EditorGUI.BeginDisabledGroup(disableOnPlaying && EditorApplication.isPlaying);
                int newIndex = EditorGUILayout.Popup(selectedIndex, displayedOptions, options);
                EditorGUI.EndDisabledGroup();
                return newIndex;
            }

            /// <summary>
            /// 绘制限制候选值集合的 IntPopup（候选值与显示文本一一对应）；用于不希望走完整 enum Popup 的限制选项集场景。
            /// </summary>
            /// <param name="selectedValue">当前选中整数值。</param>
            /// <param name="displayedOptions">选项显示文本数组。</param>
            /// <param name="optionValues">与显示文本一一对应的候选整数值数组。</param>
            /// <param name="disableOnPlaying">是否在运行时禁用。</param>
            /// <param name="options">布局选项。</param>
            /// <returns>新选中整数值。</returns>
            public static int IntPopup(int selectedValue, string[] displayedOptions, int[] optionValues, bool disableOnPlaying = true, params GUILayoutOption[] options)
            {
                EditorGUI.BeginDisabledGroup(disableOnPlaying && EditorApplication.isPlaying);
                int newValue = EditorGUILayout.IntPopup(selectedValue, displayedOptions, optionValues, options);
                EditorGUI.EndDisabledGroup();
                return newValue;
            }

            /// <summary>
            /// 绘制 SerializedProperty 的默认 PropertyField（含子节点），用于数组 / 列表 / 复合属性等 Unity 默认控件足够用的场景。
            /// </summary>
            /// <param name="property">SerializedProperty。</param>
            /// <param name="label">显示标签。</param>
            /// <param name="includeChildren">是否包含子节点（List / 复合属性需 true）。</param>
            /// <param name="disableOnPlaying">是否在运行时禁用。</param>
            /// <param name="options">布局选项。</param>
            public static void PropertyField(SerializedProperty property, string label, bool includeChildren = true, bool disableOnPlaying = true, params GUILayoutOption[] options)
            {
                if (property == null) return;
                EditorGUI.BeginDisabledGroup(disableOnPlaying && EditorApplication.isPlaying);
                EditorGUILayout.PropertyField(property, new GUIContent(label), includeChildren, options);
                EditorGUI.EndDisabledGroup();
            }

            /// <summary>
            /// 绘制管理器选择器。
            /// </summary>
            /// <param name="label">标签名。</param>
            /// <param name="typeNames">可选类型名称列表。</param>
            /// <param name="typeNameProperty">绑定的类型名称 SerializedProperty。</param>
            /// <param name="disableOnPlaying">是否在运行时禁用。</param>
            /// <param name="onComplete">完成回调。</param>
            /// <param name="options">布局选项。</param>
            public static void TypesSelector(string label, List<string> typeNames, SerializedProperty typeNameProperty, bool disableOnPlaying = true, Action onComplete = null, params GUILayoutOption[] options)
            {
                if (!ValidateProperty(typeNameProperty, SerializedPropertyType.String, label, disableOnPlaying))
                {
                    return;
                }

                if (typeNames == null || typeNames.Count == 0)
                {
                    EditorGUI.BeginDisabledGroup(disableOnPlaying && EditorApplication.isPlaying);
                    EditorGUILayout.LabelField(label, "无可用类型", options);
                    EditorGUI.EndDisabledGroup();
                    return;
                }

                int curIndex = typeNames.IndexOf(typeNameProperty.stringValue);
                if (curIndex < 0)
                {
                    curIndex = 0;
                }

                EditorGUILayout.BeginHorizontal();
                EditorGUI.BeginDisabledGroup(disableOnPlaying && EditorApplication.isPlaying);
                if (!string.IsNullOrEmpty(label))
                {
                    EditorGUILayout.LabelField(label, options);
                }

                if (s_TypesSelectorCachedSource != typeNames || s_TypesSelectorCachedCount != typeNames.Count)
                {
                    s_TypesSelectorCachedArray = typeNames.ToArray();
                    s_TypesSelectorCachedSource = typeNames;
                    s_TypesSelectorCachedCount = typeNames.Count;
                }

                EditorGUI.BeginChangeCheck();
                int newIndex = EditorGUILayout.Popup(curIndex, s_TypesSelectorCachedArray);
                if (EditorGUI.EndChangeCheck() || string.IsNullOrEmpty(typeNameProperty.stringValue) || typeNameProperty.stringValue != typeNames[newIndex])
                {
                    typeNameProperty.stringValue = typeNames[newIndex];
                    typeNameProperty.serializedObject.ApplyModifiedProperties();
                    onComplete?.Invoke();
                }
                EditorGUI.EndDisabledGroup();
                EditorGUILayout.EndHorizontal();
            }
            
            /// <summary>
            /// 绘制浮点值的 SelectionGrid，并处理编辑器与运行时赋值。
            /// </summary>
            /// <param name="property">绑定的 SerializedProperty。</param>
            /// <param name="gridTexts">SelectionGrid 显示文本数组。</param>
            /// <param name="valueToIndex">将当前值映射为 SelectionGrid 索引。</param>
            /// <param name="indexToValue">将 SelectionGrid 索引映射为值。</param>
            /// <param name="disableOnPlaying">是否在运行时禁用。</param>
            /// <param name="runtimeSetter">运行时赋值回调，可选。</param>
            /// <param name="onComplete">完成回调。</param>
            /// <param name="options">布局选项。</param>
            public static void FloatSelectionGrid(SerializedProperty property, string[] gridTexts, Func<float, int> valueToIndex, Func<int, float> indexToValue, bool disableOnPlaying = true, Action<float> runtimeSetter = null, Action onComplete = null, params GUILayoutOption[] options)
            {
                if (!ValidateProperty(property, SerializedPropertyType.Float, "SelectionGrid", disableOnPlaying))
                {
                    return;
                }

                float currentValue = property.floatValue;

                // 获取当前选中的索引。
                int selectedIndex = valueToIndex(currentValue);

                // 绘制 SelectionGrid。
                EditorGUI.BeginDisabledGroup(disableOnPlaying && EditorApplication.isPlaying);
                int newSelectedIndex = GUILayout.SelectionGrid(selectedIndex, gridTexts, 5, options);
                EditorGUI.EndDisabledGroup();

                if (newSelectedIndex >= 0)
                {
                    float newValue = indexToValue(newSelectedIndex);

                    // 如果值发生变化则更新 SerializedProperty 或运行时对象。
                    if (newValue != currentValue)
                    {
                        if (EditorApplication.isPlaying)
                        {
                            runtimeSetter?.Invoke(newValue);
                        }
                        else
                        {
                            property.floatValue = newValue;
                            property.serializedObject.ApplyModifiedProperties();
                        }
                        onComplete?.Invoke();
                    }
                }
            }

            /// <summary>
            /// 绘制可选择文本标签。
            /// </summary>
            /// <param name="text">文本内容。</param>
            /// <param name="disableOnPlaying">是否在运行时禁用。</param>
            /// <param name="options">布局选项。</param>
            public static void SelectableLabel(string text, bool disableOnPlaying = true, params GUILayoutOption[] options)
            {
                EditorGUI.BeginDisabledGroup(disableOnPlaying && EditorApplication.isPlaying);
                EditorGUILayout.SelectableLabel(text, options);
                EditorGUI.EndDisabledGroup();
            }

            /// <summary>
            /// 绘制可选择文本标签（带样式）。
            /// </summary>
            /// <param name="text">文本内容。</param>
            /// <param name="style">GUI样式。</param>
            /// <param name="disableOnPlaying">是否在运行时禁用。</param>
            /// <param name="options">布局选项。</param>
            public static void SelectableLabel(string text, GUIStyle style, bool disableOnPlaying = true, params GUILayoutOption[] options)
            {
                EditorGUI.BeginDisabledGroup(disableOnPlaying && EditorApplication.isPlaying);
                EditorGUILayout.SelectableLabel(text, style, options);
                EditorGUI.EndDisabledGroup();
            }

            /// <summary>
            /// 绘制多行文本输入框，返回编辑后的值。
            /// </summary>
            /// <param name="value">当前文本值。</param>
            /// <param name="disableOnPlaying">是否在运行时禁用。</param>
            /// <param name="options">布局选项。</param>
            /// <returns>编辑后的文本值。</returns>
            public static string TextArea(string value, bool disableOnPlaying = false, params GUILayoutOption[] options)
            {
                EditorGUI.BeginDisabledGroup(disableOnPlaying && EditorApplication.isPlaying);
                string newValue = EditorGUILayout.TextArea(value, options);
                EditorGUI.EndDisabledGroup();
                return newValue;
            }

            /// <summary>
            /// 绘制表格行，Name 列文本与上方标准属性行标签列对齐，值列文本与属性值列对齐。
            /// 对齐原理：
            ///   - Name 列使用 EditorGUI.LabelField(rect)，其内部会对 rect 做 IndentedRect 偏移，
            ///     与 LabelField(label, value) 中标签文本的 indent 偏移完全一致。
            ///   - 值列使用 GUI.Label(rect, EditorStyles.label)，不做 indent 偏移，
            ///     与 LabelField(label, value) 中值文本的绘制方式完全一致。
            ///   - 分割点统一为 rowRect.x + EditorGUIUtility.labelWidth，
            ///     与 PrefixLabel 内 valueRect.x = totalPosition.x + labelWidth 完全吻合。
            /// </summary>
            /// <param name="name">名称列文本（对应标签区）。</param>
            /// <param name="columnTexts">各值列文本数组。</param>
            /// <param name="columnWidths">各值列宽度数组（像素），与 columnTexts 一一对应。</param>
            /// <param name="disableOnPlaying">是否在运行时禁用。</param>
            public static void TableRow(string name, string[] columnTexts, float[] columnWidths, bool disableOnPlaying = false)
            {
                Rect rowRect = EditorGUILayout.GetControlRect(false, EditorGUIUtility.singleLineHeight);

                EditorGUI.BeginDisabledGroup(disableOnPlaying && EditorApplication.isPlaying);

                // Name 列：EditorGUI.LabelField 内部做 IndentedRect，与属性标签文本 indent 一致
                EditorGUI.LabelField(new Rect(rowRect.x, rowRect.y, EditorGUIUtility.labelWidth, rowRect.height), name ?? string.Empty);

                // 值列：GUI.Label 不做 indent 偏移，与属性值文本位置完全一致
                float x = rowRect.x + EditorGUIUtility.labelWidth;
                int count = Mathf.Min(columnTexts?.Length ?? 0, columnWidths?.Length ?? 0);
                for (int i = 0; i < count; i++)
                {
                    GUI.Label(new Rect(x, rowRect.y, columnWidths[i], rowRect.height), columnTexts[i] ?? string.Empty, EditorStyles.label);
                    x += columnWidths[i];
                }

                EditorGUI.EndDisabledGroup();
            }

            /// <summary>
            /// 在指定 Rect 内绘制文本标签（Rect 版，适用于 ReorderableList drawHeaderCallback 等固定布局场景）。
            /// </summary>
            /// <param name="rect">绘制区域。</param>
            /// <param name="text">文本内容。</param>
            /// <param name="disableOnPlaying">是否在运行时禁用。</param>
            public static void Label(Rect rect, string text, bool disableOnPlaying = true)
            {
                EditorGUI.BeginDisabledGroup(disableOnPlaying && EditorApplication.isPlaying);
                EditorGUI.LabelField(rect, text);
                EditorGUI.EndDisabledGroup();
            }

            /// <summary>
            /// 在指定 Rect 内绘制文本标签（Rect 带样式版，适用于需要指定 GUIStyle 的固定布局场景，如粗体标题行）。
            /// </summary>
            /// <param name="rect">绘制区域。</param>
            /// <param name="text">文本内容。</param>
            /// <param name="style">文本样式。</param>
            /// <param name="disableOnPlaying">是否在运行时禁用。</param>
            public static void Label(Rect rect, string text, GUIStyle style, bool disableOnPlaying = true)
            {
                EditorGUI.BeginDisabledGroup(disableOnPlaying && EditorApplication.isPlaying);
                EditorGUI.LabelField(rect, text, style);
                EditorGUI.EndDisabledGroup();
            }

            /// <summary>
            /// 在指定 Rect 内绘制可编辑文本框（Rect 版，带变更检测与 ApplyModifiedProperties，适用于 ReorderableList drawElementCallback 等固定布局场景）。
            /// </summary>
            /// <param name="rect">绘制区域。</param>
            /// <param name="property">绑定的 SerializedProperty（字符串类型）。</param>
            /// <param name="disableOnPlaying">是否在运行时禁用。</param>
            /// <param name="onComplete">完成回调。</param>
            public static void TextField(Rect rect, SerializedProperty property, bool disableOnPlaying = true, Action onComplete = null)
            {
                if (property == null)
                {
                    return;
                }

                EditorGUI.BeginDisabledGroup(disableOnPlaying && EditorApplication.isPlaying);
                EditorGUI.BeginChangeCheck();
                string newValue = EditorGUI.TextField(rect, property.stringValue);
                if (EditorGUI.EndChangeCheck())
                {
                    property.stringValue = newValue;
                    property.serializedObject.ApplyModifiedProperties();
                    onComplete?.Invoke();
                }
                EditorGUI.EndDisabledGroup();
            }

            /// <summary>
            /// 暗黄小字说明标签的文本颜色（wordWrap 自动换行，hover/focused/active 同色避免变色）。
            /// </summary>
            private static readonly Color s_HintLabelColor = new Color(0.78f, 0.62f, 0.20f);

            /// <summary>
            /// 暗黄小字说明标签样式（延迟初始化）。
            /// </summary>
            private static GUIStyle s_HintLabelStyle;

            /// <summary>
            /// 绘制暗黄小字说明标签（miniLabel 字体，wordWrap 自动换行，颜色为暗黄色）。
            /// 用于在 Inspector 或 EditorWindow 中附加对上方控件的补充说明，视觉上次于正文。
            /// </summary>
            /// <param name="text">说明文本。</param>
            /// <param name="disableOnPlaying">是否在运行时禁用。</param>
            public static void HintLabel(string text, bool disableOnPlaying = true)
            {
                if (s_HintLabelStyle == null)
                {
                    s_HintLabelStyle = new GUIStyle(EditorStyles.miniLabel)
                    {
                        wordWrap = true,
                        richText = false
                    };
                    s_HintLabelStyle.normal.textColor = s_HintLabelColor;
                    s_HintLabelStyle.hover.textColor = s_HintLabelColor;
                    s_HintLabelStyle.focused.textColor = s_HintLabelColor;
                    s_HintLabelStyle.active.textColor = s_HintLabelColor;
                }

                EditorGUI.BeginDisabledGroup(disableOnPlaying && EditorApplication.isPlaying);
                EditorGUILayout.LabelField(text, s_HintLabelStyle);
                EditorGUI.EndDisabledGroup();
            }

            /// <summary>
            /// 在指定 Rect 内绘制 SerializedProperty 的默认 PropertyField（Rect 版，PropertyDrawer 专用）。
            /// 与 GUILayout 版等价，内部直接调用 EditorGUI.PropertyField，适用于 PropertyDrawer 的 OnGUI 回调。
            /// </summary>
            /// <param name="rect">绘制区域。</param>
            /// <param name="property">SerializedProperty。</param>
            /// <param name="label">显示标签文本。</param>
            /// <param name="includeChildren">是否包含子节点。</param>
            /// <param name="disableOnPlaying">是否在运行时禁用。</param>
            public static void PropertyField(Rect rect, SerializedProperty property, string label, bool includeChildren = false, bool disableOnPlaying = true)
            {
                if (property == null)
                {
                    return;
                }
                EditorGUI.BeginDisabledGroup(disableOnPlaying && EditorApplication.isPlaying);
                EditorGUI.PropertyField(rect, property, new GUIContent(label), includeChildren);
                EditorGUI.EndDisabledGroup();
            }

            /// <summary>
            /// 在指定 Rect 内绘制整数滑动条（Rect 版，PropertyDrawer 专用）。
            /// 通过变更检测写入 SerializedProperty 并 ApplyModifiedProperties。
            /// </summary>
            /// <param name="rect">绘制区域。</param>
            /// <param name="label">标签文本（传入 GUIContent 构造）。</param>
            /// <param name="property">绑定的 SerializedProperty（整数类型）。</param>
            /// <param name="min">最小值。</param>
            /// <param name="max">最大值。</param>
            /// <param name="disableOnPlaying">是否在运行时禁用。</param>
            public static void IntSlider(Rect rect, string label, SerializedProperty property, int min, int max, bool disableOnPlaying = true)
            {
                if (property == null || property.propertyType != SerializedPropertyType.Integer)
                {
                    return;
                }
                EditorGUI.BeginDisabledGroup(disableOnPlaying && EditorApplication.isPlaying);
                EditorGUI.BeginChangeCheck();
                int newValue = EditorGUI.IntSlider(rect, new GUIContent(label), property.intValue, min, max);
                if (EditorGUI.EndChangeCheck())
                {
                    property.intValue = newValue;
                    property.serializedObject.ApplyModifiedProperties();
                }
                EditorGUI.EndDisabledGroup();
            }

            /// <summary>
            /// 属性合法性验证。
            /// </summary>
            /// <param name="property">待验证的 SerializedProperty。</param>
            /// <param name="type">期望的属性类型。</param>
            /// <param name="label">显示标签。</param>
            /// <param name="disableOnPlaying">是否在运行时禁用。</param>
            /// <returns>属性是否有效。</returns>
            private static bool ValidateProperty(SerializedProperty property, SerializedPropertyType type, string label, bool disableOnPlaying = true)
            {
                if (property == null || property.propertyType != type)
                {
                    EditorGUI.BeginDisabledGroup(disableOnPlaying && EditorApplication.isPlaying);
                    EditorGUILayout.LabelField(label, "Property 无效");
                    EditorGUI.EndDisabledGroup();
                    return false;
                }
                return true;
            }

        }   
    }
}
