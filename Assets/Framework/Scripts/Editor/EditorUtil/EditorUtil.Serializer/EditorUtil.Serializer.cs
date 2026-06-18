/***************************************************************
 * (c) copyright 2026 - 2030, Solotopia
 * All Rights Reserved.
 * -------------------------------------------------------------
 * filename:  EditorUtil.Serializer.cs
 * author:    taoye
 * created:   2026/2/3
 * descrip:   编辑器序列化数据工具
 ***************************************************************/

using System;
using NovaFramework.Runtime;
using UnityEditor;
using UnityEngine;

namespace NovaFramework.Editor
{
    public static partial class EditorUtil
    {
        /// <summary>
        /// 序列化数据工具。
        /// </summary>
        public static class Serializer
        {
            /// <summary>
            /// 获取序列化属性的值。
            /// </summary>
            /// <typeparam name="TTarget">目标对象类型。</typeparam>
            /// <typeparam name="TValue">属性值类型。</typeparam>
            /// <param name="target">目标对象。</param>
            /// <param name="propertyName">属性名称。</param>
            /// <returns>属性值。</returns>
            public static TValue GetProperty<TTarget, TValue>(TTarget target, string propertyName) where TTarget : UnityEngine.Object
            {
                if (target == null)
                {
                    throw new ArgumentNullException(nameof(target));
                }

                SerializedObject serializedObject = new SerializedObject(target);
                SerializedProperty property = serializedObject.FindProperty(propertyName);

                if (property == null)
                {
                    throw new ArgumentException($"属性 '{propertyName}' 在对象 '{target.name}' 上未找到。");
                }

                return GetValueFromProperty<TValue>(property);
            }

            /// <summary>
            /// 设置序列化属性的值。
            /// </summary>
            /// <typeparam name="TTarget">目标对象类型。</typeparam>
            /// <typeparam name="TValue">属性值类型。</typeparam>
            /// <param name="target">目标对象。</param>
            /// <param name="propertyName">属性名称。</param>
            /// <param name="value">要设置的值。</param>
            public static void SetProperty<TTarget, TValue>(TTarget target, string propertyName, TValue value) where TTarget : UnityEngine.Object
            {
                if (target == null)
                {
                    throw new ArgumentNullException(nameof(target));
                }

                SerializedObject serializedObject = new SerializedObject(target);
                SerializedProperty property = serializedObject.FindProperty(propertyName);

                if (property == null)
                {
                    throw new ArgumentException($"属性 '{propertyName}' 在对象 '{target.name}' 上未找到。");
                }

                SetValueToProperty(property, value);
                serializedObject.ApplyModifiedProperties();
            }

            /// <summary>
            /// 从 SerializedProperty 读取指定类型的值。
            /// </summary>
            /// <typeparam name="T">目标值类型。</typeparam>
            /// <param name="property">序列化属性。</param>
            /// <returns>属性值。</returns>
            private static T GetValueFromProperty<T>(SerializedProperty property)
            {
                Type type = typeof(T);

                object result = type switch
                {
                    _ when type == typeof(int) => property.intValue,
                    _ when type == typeof(long) => property.longValue,
                    _ when type == typeof(float) => property.floatValue,
                    _ when type == typeof(double) => property.doubleValue,
                    _ when type == typeof(bool) => property.boolValue,
                    _ when type == typeof(string) => property.stringValue,
                    _ when type == typeof(Color) => property.colorValue,
                    _ when type == typeof(Vector2) => property.vector2Value,
                    _ when type == typeof(Vector3) => property.vector3Value,
                    _ when type == typeof(Vector4) => property.vector4Value,
                    _ when type == typeof(Rect) => property.rectValue,
                    _ when type == typeof(AnimationCurve) => property.animationCurveValue,
                    _ when type == typeof(Bounds) => property.boundsValue,
                    _ when type == typeof(Quaternion) => property.quaternionValue,
                    // 使用 intValue 获取枚举的实际整数值，而非 enumValueIndex（下拉列表显示索引）。
                    // enumValueIndex 仅在枚举值连续从 0 开始时与实际值一致，非连续枚举（如 None=0, TypeA=10）会出错。
                    _ when type.IsEnum => Enum.ToObject(type, property.intValue),
                    _ when typeof(UnityEngine.Object).IsAssignableFrom(type) => property.objectReferenceValue,
                    // 特殊情况：T 是 int 但属性是 Enum。
                    _ when property.propertyType == SerializedPropertyType.Enum && type == typeof(int) => property.intValue,
                    _ => throw new NotSupportedException($"不支持类型 '{type.Name}' 。")
                };

                return (T)result;
            }

            /// <summary>
            /// 将指定类型的值写入 SerializedProperty。
            /// </summary>
            /// <typeparam name="T">值类型。</typeparam>
            /// <param name="property">序列化属性。</param>
            /// <param name="value">要写入的值。</param>
            private static void SetValueToProperty<T>(SerializedProperty property, T value)
            {
                Type type = typeof(T);

                if (type == typeof(int)) { property.intValue = (int)(object)value; }
                else if (type == typeof(long)) { property.longValue = (long)(object)value; }
                else if (type == typeof(float)) { property.floatValue = (float)(object)value; }
                else if (type == typeof(double)) { property.doubleValue = (double)(object)value; }
                else if (type == typeof(bool)) { property.boolValue = (bool)(object)value; }
                else if (type == typeof(string)) { property.stringValue = (string)(object)value; }
                else if (type == typeof(Color)) { property.colorValue = (Color)(object)value; }
                else if (type == typeof(Vector2)) { property.vector2Value = (Vector2)(object)value; }
                else if (type == typeof(Vector3)) { property.vector3Value = (Vector3)(object)value; }
                else if (type == typeof(Vector4)) { property.vector4Value = (Vector4)(object)value; }
                else if (type == typeof(Rect)) { property.rectValue = (Rect)(object)value; }
                else if (type == typeof(AnimationCurve)) { property.animationCurveValue = (AnimationCurve)(object)value; }
                else if (type == typeof(Bounds)) { property.boundsValue = (Bounds)(object)value; }
                else if (type == typeof(Quaternion)) { property.quaternionValue = (Quaternion)(object)value; }
                else if (type.IsEnum) { property.intValue = Convert.ToInt32(value); }
                else if (typeof(UnityEngine.Object).IsAssignableFrom(type)) { property.objectReferenceValue = (UnityEngine.Object)(object)value; }
                else { throw new NotSupportedException($"不支持类型 '{type.Name}' 。"); }
            }

            /// <summary>
            /// 在当前场景中按 GameObject 路径查找指定组件，并返回其 SerializedObject。
            /// </summary>
            /// <typeparam name="T">组件类型。</typeparam>
            /// <param name="goPath">GameObject 路径（如 "Nova" 或 "Nova/Config"），使用 GameObject.Find 语法。</param>
            /// <returns>找到时返回 SerializedObject，未找到时返回 null。</returns>
            public static SerializedObject FindInScene<T>(string goPath) where T : Component
            {
                if (string.IsNullOrEmpty(goPath))
                {
                    Log.Warning(LogTag.Editor, "FindInScene: goPath 无效。");
                    return null;
                }

                var go = GameObject.Find(goPath);
                if (go == null)
                {
                    Log.Warning(LogTag.Editor, "FindInScene: 未在场景中找到 GameObject：{0}", goPath);
                    return null;
                }

                var component = go.GetComponent<T>();
                if (component == null)
                {
                    Log.Warning(LogTag.Editor, "FindInScene: 在 {0} 上未找到组件 {1}。", goPath, typeof(T).Name);
                    return null;
                }

                return new SerializedObject(component);
            }

            /// <summary>
            /// 在当前场景中按 GameObject 路径查找指定组件的指定序列化属性。
            /// </summary>
            /// <typeparam name="T">组件类型。</typeparam>
            /// <param name="goPath">GameObject 路径。</param>
            /// <param name="propertyName">序列化属性名称。</param>
            /// <returns>找到时返回 SerializedProperty，未找到时返回 null。</returns>
            public static SerializedProperty FindInSceneProperty<T>(string goPath, string propertyName) where T : Component
            {
                var serializedObject = FindInScene<T>(goPath);
                if (serializedObject == null) return null;

                var property = serializedObject.FindProperty(propertyName);
                if (property == null)
                {
                    Log.Warning(LogTag.Editor, "FindInSceneProperty: 在 {0}/{1} 上未找到属性：{2}", goPath, typeof(T).Name, propertyName);
                }

                return property;
            }
        }
    }
}
