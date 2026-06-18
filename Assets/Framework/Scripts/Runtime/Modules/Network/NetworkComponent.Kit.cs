/***************************************************************
 * (c) copyright 2026 - 2030, Solotopia
 * All Rights Reserved.
 * -------------------------------------------------------------
 * filename:  NetworkComponent.Kit.cs
 * author:    taoye
 * created:   2026/5/26
 * descrip:   NetworkComponent Kit Service 惰性单例容器（partial）
 ***************************************************************/

using System;

namespace NovaFramework.Runtime
{
    /// <summary>
    /// NetworkComponent Kit 扩展分部。
    /// 提供 Kit Service 惰性单例容器，子包（Login/Account 等）的 Service 类通过此入口获取实例。
    /// </summary>
    public sealed partial class NetworkComponent : FrameworkComponent
    {
        /// <summary>
        /// 获取或惰性创建指定 Kit Service 的单例实例。
        /// 子包（Login/Account/...）的 Service 类必须满足 class + 无参构造约束。
        /// 不持有 Kit 层 asmdef 引用，不形成主框架 → Kit 的反向依赖。
        /// </summary>
        /// <typeparam name="T">Kit Service 类型，必须为 class 且具备无参构造器。</typeparam>
        /// <returns>对应 Service 的单例实例。</returns>
        public T Kit<T>() where T : class, new()
        {
            Type t = typeof(T);
            if (!m_KitInstances.TryGetValue(t, out object inst))
            {
                inst = new T();
                m_KitInstances[t] = inst;
            }
            return (T)inst;
        }
    }
}
