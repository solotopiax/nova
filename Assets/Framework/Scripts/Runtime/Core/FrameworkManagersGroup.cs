/***************************************************************
 * (c) copyright 2026 - 2030, Solotopia
 * All Rights Reserved.
 * -------------------------------------------------------------
 * filename:  FrameworkManagersGroup.cs
 * author:    taoye
 * created:   2025/12/5
 * descrip:   框架管理器集合
 *            用于管理和获取所有注册的 FrameworkManager 实例
 ***************************************************************/

using System;
using System.Collections.Generic;
using UnityEngine;

namespace NovaFramework.Runtime
{
    /// <summary>
    /// 框架管理器集合。
    /// </summary>
    public static class FrameworkManagersGroup
    {
        /// <summary>
        /// 框架管理器注册链表。
        /// </summary>
        private static NovaLinkedList<FrameworkManager> s_FrameworkManagers = new NovaLinkedList<FrameworkManager>();
        /// <summary>
        /// 框架管理器接口类型缓存字典，以接口类型为键、管理器实例为值。
        /// </summary>
        private static Dictionary<Type, FrameworkManager> s_ManagerCache = new Dictionary<Type, FrameworkManager>();

        /// <summary>
        /// Domain Reload 时重置静态集合，防止关闭 Domain Reload 后产生脏数据。
        /// </summary>
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStatics()
        {
            s_FrameworkManagers = new NovaLinkedList<FrameworkManager>();
            s_ManagerCache = new Dictionary<Type, FrameworkManager>();
        }

        /// <summary>
        /// 所有框架管理器轮询。
        /// </summary>
        /// <remarks>按 Priority 升序同步执行；单个 Update 异常直接传播，并中断本帧后续 Manager。</remarks>
        public static void Update()
        {
            foreach (FrameworkManager manager in s_FrameworkManagers)
            {
                manager.Update();
            }
        }

        /// <summary>
        /// 关闭并清理所有框架模块。
        /// </summary>
        /// <remarks>按 Priority 降序执行。单个 Shutdown 异常中断后续关闭；finally 仍清全局默认 AES、注册表、查询缓存与引用池，不代表各模块清理均已完成。</remarks>
        public static void Shutdown()
        {
            try
            {
                for (LinkedListNode<FrameworkManager> current = s_FrameworkManagers.Last;
                     current != null;
                     current = current.Previous)
                {
                    current.Value.Shutdown();
                }
            }
            finally
            {
                Util.Encrypt.AES.ResetConfigInitialization();
                s_FrameworkManagers.Clear();
                s_ManagerCache.Clear();
                ReferencePool.ClearAll();
            }
        }

        /// <summary>
        /// 注册框架管理器。
        /// </summary>
        /// <param name="manager">管理器对象。</param>
        public static void RegisterManager(FrameworkManager manager)
        {
            if (manager == null)
            {
                throw new ArgumentNullException(nameof(manager), "管理器实例无效。");
            }

            Type managerType = manager.GetType();
            foreach (FrameworkManager existing in s_FrameworkManagers)
            {
                if (existing.GetType() == managerType)
                {
                    throw new InvalidOperationException(Txt.Format("管理器类型 '{0}' 已注册，不可重复注册。", managerType.FullName));
                }
            }

            LinkedListNode<FrameworkManager> current = s_FrameworkManagers.First;
            while (current != null)
            {
                if (manager.Priority < current.Value.Priority)
                {
                    break;
                }

                current = current.Next;
            }

            if (current != null)
            {
                s_FrameworkManagers.AddBefore(current, manager);
            }
            else
            {
                s_FrameworkManagers.AddLast(manager);
            }
        }
        
        /// <summary>
        /// 反注册框架管理器（仅用于构造异常时的回滚）。
        /// </summary>
        /// <param name="manager">管理器对象。</param>
        public static void UnregisterManager(FrameworkManager manager)
        {
            if (manager == null)
            {
                return;
            }

            s_FrameworkManagers.Remove(manager);
            s_ManagerCache.Clear();
        }

        /// <summary>
        /// 获取框架模块。
        /// </summary>
        /// <typeparam name="T">要获取的框架模块类型。</typeparam>
        /// <returns>要获取的框架模块，不存在时返回 null。</returns>
        public static T GetManager<T>() where T : class
        {
            Type interfaceType = typeof(T);
            if (!interfaceType.IsInterface)
            {
                throw new ArgumentException(Txt.Format("只能通过 interface 获取管理器，但 '{0}' 并不是 interface。", interfaceType.FullName));
            }
            
            return GetManager(interfaceType) as T;
        }

        /// <summary>
        /// 获取框架管理器。
        /// </summary>
        /// <param name="managerType">要获取的框架管理器类型。</param>
        /// <returns>要获取的框架管理器，不存在时返回 null。</returns>
        public static FrameworkManager GetManager(Type managerType)
        {
            if (managerType == null)
            {
                throw new ArgumentNullException(nameof(managerType), "管理器类型无效。");
            }

            if (s_ManagerCache.TryGetValue(managerType, out FrameworkManager cached))
            {
                return cached;
            }

            foreach (FrameworkManager manager in s_FrameworkManagers)
            {
                if (managerType.IsAssignableFrom(manager.GetType()))
                {
                    s_ManagerCache.Add(managerType, manager);
                    return manager;
                }
            }

            return null;
        }
        
    }
}
