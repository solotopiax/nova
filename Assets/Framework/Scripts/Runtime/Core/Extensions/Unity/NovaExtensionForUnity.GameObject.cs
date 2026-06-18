/***************************************************************
 * (c) copyright 2026 - 2030, Solotopia
 * All Rights Reserved.
 * -------------------------------------------------------------
 * filename:  NovaExtensionForUnity.GameObject.cs
 * author:    taoye
 * created:   2025/12/2
 * descrip:   框架对Unity的扩展方法-GameObject
 *            提供获取、增加组件及GameObject常用操作的扩展方法
 ***************************************************************/

using System;
using System.Collections.Generic;
using UnityEngine;

namespace NovaFramework.Runtime
{
    /// <summary>
    /// 框架对 Unity GameObject 的扩展方法。
    /// </summary>
    public static partial class NovaExtensionForUnity
    {
        /// <summary>
        /// 获取目标对象上的指定组件（方法内局部缓存，避免静态缓存重入覆盖风险）。
        /// </summary>
        /// <param name="gameObject">目标对象。</param>
        /// <param name="type">要获取的组件类型。</param>
        /// <returns>获取的组件，如果不存在返回 null。</returns>
        public static Component GetComponentNoAlloc(this GameObject gameObject, Type type)
        {
            return gameObject.GetComponent(type);
        }

        /// <summary>
        /// 获取目标对象上的指定组件（方法内局部缓存，避免静态缓存重入覆盖风险）。
        /// </summary>
        /// <typeparam name="T">要获取的组件类型。</typeparam>
        /// <param name="gameObject">目标对象。</param>
        /// <returns>获取的组件，如果不存在返回 null。</returns>
        public static T GetComponentNoAlloc<T>(this GameObject gameObject) where T : Component
        {
            gameObject.TryGetComponent<T>(out T component);
            return component;
        }

        /// <summary>
        /// 获取对象/子对象/父对象上的组件，如果未找到，则添加到对象。
        /// </summary>
        /// <typeparam name="T">要获取或添加的组件类型。</typeparam>
        /// <param name="gameObject">目标对象。</param>
        /// <returns>获取或添加的组件实例。</returns>
        public static T GetComponentAroundOrAdd<T>(this GameObject gameObject) where T : Component
        {
            T component = gameObject.GetComponentInChildren<T>(true);
            if (component == null)
            {
                component = gameObject.GetComponentInParent<T>();
            }
            if (component == null)
            {
                component = gameObject.AddComponent<T>();
            }
            return component;
        }

        /// <summary>
        /// 获取或增加组件。
        /// </summary>
        /// <typeparam name="T">要获取或增加的组件类型。</typeparam>
        /// <param name="gameObject">目标对象。</param>
        /// <returns>获取或添加的组件实例。</returns>
        public static T GetOrAddComponent<T>(this GameObject gameObject) where T : Component
        {
            T component = gameObject.GetComponent<T>();
            if (component == null)
            {
                component = gameObject.AddComponent<T>();
            }
            return component;
        }

        /// <summary>
        /// 获取或增加组件。
        /// </summary>
        /// <param name="gameObject">目标对象。</param>
        /// <param name="type">要获取或增加的组件类型。</param>
        /// <returns>获取或添加的组件实例。</returns>
        public static Component GetOrAddComponent(this GameObject gameObject, Type type)
        {
            Component component = gameObject.GetComponent(type);
            if (component == null)
            {
                component = gameObject.AddComponent(type);
            }
            return component;
        }

        /// <summary>
        /// 判断 GameObject 是否在场景中。
        /// </summary>
        /// <param name="gameObject">目标对象。</param>
        /// <returns>如果 GameObject 是场景中的实例返回 true，否则返回 false（Prefab）。</returns>
        public static bool InScene(this GameObject gameObject)
        {
            return gameObject.scene.name != null;
        }

        /// <summary>
        /// 递归设置游戏对象及其所有子对象的层次。
        /// </summary>
        /// <param name="gameObject">目标对象。</param>
        /// <param name="layer">目标层次编号。</param>
        public static void SetLayerRecursively(this GameObject gameObject, int layer)
        {
            List<Transform> transforms = new List<Transform>();
            gameObject.GetComponentsInChildren(true, transforms);
            foreach (var t in transforms)
            {
                t.gameObject.layer = layer;
            }
        }

        /// <summary>
        /// 获取 GameObject 的 RectTransform。
        /// </summary>
        /// <param name="gameObject">目标对象。</param>
        /// <returns>RectTransform 组件实例，如果不存在返回 null。</returns>
        public static RectTransform rectTransform(this GameObject gameObject)
        {
            return gameObject.transform as RectTransform;
        }

        /// <summary>
        /// 设置粒子特效的 SortingOrder。
        /// </summary>
        /// <param name="gameObject">目标对象。</param>
        /// <param name="sortOrder">目标排序层级。</param>
        /// <param name="isSortParticle">是否对粒子特效进行排序。</param>
        public static void SetParticleSortOrder(this GameObject gameObject, int sortOrder, bool isSortParticle)
        {
            if (gameObject == null) return;

            ParticleSystemRenderer[] psRenderers = gameObject.GetComponentsInChildren<ParticleSystemRenderer>();
            if (isSortParticle)
            {
                int lastSortingOrder = 0;
                int curSortingOrder = sortOrder;
                List<ParticleSystemRenderer> sortedList = new List<ParticleSystemRenderer>(psRenderers);
                sortedList.Sort((a, b) => a.sortingOrder.CompareTo(b.sortingOrder));

                foreach (var ps in sortedList)
                {
                    if (lastSortingOrder == 0)
                    {
                        lastSortingOrder = ps.sortingOrder;
                        ps.sortingOrder = curSortingOrder;
                        continue;
                    }

                    if (ps.sortingOrder == lastSortingOrder)
                    {
                        ps.sortingOrder = curSortingOrder;
                        continue;
                    }

                    lastSortingOrder = ps.sortingOrder;
                    ++curSortingOrder;
                    ps.sortingOrder = curSortingOrder;
                }
            }
            else
            {
                foreach (var ps in psRenderers)
                {
                    ps.sortingOrder = sortOrder;
                }
            }
        }
    }
}
