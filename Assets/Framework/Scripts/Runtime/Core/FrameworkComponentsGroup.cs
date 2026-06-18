/***************************************************************
 * (c) copyright 2026 - 2030, Solotopia
 * All Rights Reserved.
 * -------------------------------------------------------------
 * filename:  FrameworkComponentsGroup.cs
 * author:    taoye
 * created:   2025/12/2
 * descrip:   框架组件集合
 *            用于管理和获取所有注册的 FrameworkComponent 实例
 ***************************************************************/

using System;
using System.Collections.Generic;
using UnityEngine;

namespace NovaFramework.Runtime
{
    /// <summary>
    /// 框架组件集合管理类，负责注册、获取和清理所有 FrameworkComponent 实例。
    /// </summary>
    public static class FrameworkComponentsGroup
    {
        /// <summary>
        /// 框架组件注册链表。
        /// </summary>
        private static NovaLinkedList<FrameworkComponent> s_FrameworkComponents = new NovaLinkedList<FrameworkComponent>();

        /// <summary>
        /// 框架组件类型缓存字典，<组件类型, 组件实例>。
        /// </summary>
        private static Dictionary<Type, FrameworkComponent> s_ComponentCache = new Dictionary<Type, FrameworkComponent>();

        /// <summary>
        /// Domain Reload 时重置所有静态集合，防止关闭 Domain Reload 后产生脏数据。
        /// </summary>
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStatics()
        {
            s_FrameworkComponents = new NovaLinkedList<FrameworkComponent>();
            s_ComponentCache = new Dictionary<Type, FrameworkComponent>();
        }

        /// <summary>
        /// 获取框架组件。
        /// </summary>
        /// <typeparam name="T">FrameworkComponent 的派生类型。</typeparam>
        /// <returns>FrameworkComponent 的派生类型实例，如果未注册则返回 null。</returns>
        public static T GetComponent<T>() where T : FrameworkComponent
        {
            return (T)GetComponent(typeof(T));
        }

        /// <summary>
        /// 获取框架组件。
        /// </summary>
        /// <param name="type">FrameworkComponent 的派生类型。</param>
        /// <returns>FrameworkComponent 的派生类型实例，如果未注册则返回 null。</returns>
        public static FrameworkComponent GetComponent(Type type)
        {
            if (s_ComponentCache.TryGetValue(type, out FrameworkComponent component))
            {
                return component;
            }
            return null;
        }

        /// <summary>
        /// 注册框架组件（不会重复添加）。
        /// </summary>
        /// <param name="component">待注册的组件实例。</param>
        public static void RegisterComponent(FrameworkComponent component)
        {
            if (component == null)
            {
                throw new ArgumentNullException(nameof(component), "框架组件实例无效。");
            }

            Type type = component.GetType();

            if (s_ComponentCache.ContainsKey(type))
            {
                Log.Error(LogTag.Component, "框架组件类型 '{0}' 已经存在，不可重复注册。", type.FullName);
                return;
            }

            s_FrameworkComponents.AddLast(component);
            s_ComponentCache.Add(type, component);
        }

        /// <summary>
        /// 清空已注册的所有框架组件。
        /// </summary>
        public static void Clear()
        {
            s_FrameworkComponents.Clear();
            s_ComponentCache.Clear();
        }
    }
}
