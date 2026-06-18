/***************************************************************
 * (c) copyright 2026 - 2030, Solotopia
 * All Rights Reserved.
 * -------------------------------------------------------------
 * filename:  NovaExtensionForUnity.Component.cs
 * author:    taoye
 * created:   2025/12/2
 * descrip:   框架对Unity的扩展方法-Component
 *            提供获取、增加组件的快捷方法
 ***************************************************************/

using System;
using UnityEngine;

namespace NovaFramework.Runtime
{
    /// <summary>
    /// 框架对 Unity Component 的扩展方法。
    /// </summary>
    public static partial class NovaExtensionForUnity
    {
        /// <summary>
        /// 获取目标对象上的指定组件，如果不存在则添加该组件。
        /// </summary>
        /// <typeparam name="T">要获取或增加的组件类型。</typeparam>
        /// <param name="component">目标对象上的任意组件。</param>
        /// <returns>获取或添加的组件实例。</returns>
        public static T GetOrAddComponent<T>(this Component component) where T : Component
        {
            return GetOrAddComponent<T>(component.gameObject);
        }

        /// <summary>
        /// 获取目标对象上的指定组件，如果不存在则添加该组件。
        /// </summary>
        /// <param name="component">目标对象上的任意组件。</param>
        /// <param name="type">要获取或增加的组件类型。</param>
        /// <returns>获取或添加的组件实例。</returns>
        public static Component GetOrAddComponent(this Component component, Type type)
        {
            return GetOrAddComponent(component.gameObject, type);
        }

        /// <summary>
        /// 获取目标对象上的 RectTransform 组件。
        /// </summary>
        /// <param name="component">目标对象上的任意组件。</param>
        /// <returns>RectTransform 组件实例，如果组件不是 RectTransform 返回 null。</returns>
        public static RectTransform rectTransform(this Component component)
        {
            return component.transform as RectTransform;
        }
    }
}
