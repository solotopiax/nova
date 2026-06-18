/***************************************************************
 * (c) copyright 2026 - 2030, Solotopia
 * All Rights Reserved.
 * -------------------------------------------------------------
 * filename:  Object.cs
 * author:    taoye
 * created:   2025/12/10
 * descrip:   对象
 ***************************************************************/

/*

引用计数包装（内部），为ObjectPool<T>提供包装实例，这个引用计数是池的内部机制，封在 Object<T> 里，对业务不可见，所以并没有放入 ObjectBase 。

Object<T> : IReference（internal sealed）
  |-- m_Object    -> T (ObjectBase 子类)
  |-- m_RefCount  -> 引用计数
  |-- Create(obj, got)    从 ReferencePool 取包装实例，初始化
  |-- Peek()              查看对象但不增加引用
  |-- Get()               引用计数 +1，更新 LastUseTime，回调 OnGet
  |-- Put()               引用计数 -1，回调 OnPut，负数则抛异常
  +-- Release(isShutdown) 调用 ObjectBase.Release，再把 ObjectBase 还回引用池
*/

using System;

namespace NovaFramework.Runtime
{
    /// <summary>
    /// 内部对象。
    /// 管理引用计数
    /// </summary>
    /// <typeparam name="T">对象类型。</typeparam>
    internal sealed partial class Object<T> : IReference where T : ObjectBase
    {
        /// <summary>
        /// 初始化内部对象的新实例。
        /// </summary>
        public Object()
        {
            m_Object = null;
            m_RefCount = 0;
        }

        /// <summary>
        /// 创建内部对象。
        /// </summary>
        /// <param name="obj">对象。</param>
        /// <param name="got">对象是否已被获取。</param>
        /// <returns>创建的内部对象。</returns>
        public static Object<T> Create(T obj, bool got)
        {
            if (obj == null)
            {
                throw new ArgumentNullException(nameof(obj), "Object 无效。");
            }

            Object<T> internalObject = ReferencePool.Get<Object<T>>();
            internalObject.m_Object = obj;
            internalObject.m_RefCount = got ? 1 : 0;
            if (got)
            {
                obj.OnGet();
            }

            return internalObject;
        }

        /// <summary>
        /// 清理内部对象。
        /// </summary>
        public void Clear()
        {
            m_Object = null;
            m_RefCount = 0;
        }

        /// <summary>
        /// 查看对象。
        /// </summary>
        /// <returns>对象。</returns>
        public T Peek() => m_Object;

        /// <summary>
        /// 获取对象。
        /// </summary>
        /// <returns>对象。</returns>
        public T Get()
        {
            m_RefCount++;
            m_Object.LastUseTime = DateTime.UtcNow;
            m_Object.OnGet();
            return m_Object;
        }

        /// <summary>
        /// 回收对象。
        /// </summary>
        public void Put()
        {
            m_Object.OnPut();
            m_Object.LastUseTime = DateTime.UtcNow;
            m_RefCount--;
            if (m_RefCount < 0)
            {
                throw new InvalidOperationException(Txt.Format("Object '{0}' 引用计数小于 0。", Name));
            }
        }

        /// <summary>
        /// 释放对象。
        /// </summary>
        /// <param name="isShutdown">是否是关闭对象池时触发。</param>
        public void Release(bool isShutdown)
        {
            m_Object.Release(isShutdown);
            ReferencePool.Put(m_Object);
            m_Object = null;
        }
    }
}
