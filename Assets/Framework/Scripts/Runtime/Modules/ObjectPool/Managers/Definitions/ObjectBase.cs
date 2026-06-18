/***************************************************************
 * (c) copyright 2026 - 2030, Solotopia
 * All Rights Reserved.
 * -------------------------------------------------------------
 * filename:  ObjectBase.cs
 * author:    taoye
 * created:   2025/12/10
 * descrip:   对象基类
 ***************************************************************/

/*

分离"管理信息"与"真实资源"，ObjectBase只管理信息，真实资源Target由子类实现

ObjectBase : IReference
  |-- m_Name                对象在池内的检索名
  |-- m_Target              真实的"被池化物体"（如 GameObject、Texture 等）
  |-- m_Locked              锁定保护，防止被自动回收
  |-- m_Priority            驱逐优先级（越小越优先保留）
  |-- m_LastUseTime         最后使用时刻，用于过期判断
  |-- CustomCanReleaseFlag  业务自定义"此时我能否被释放"
  |-- OnGet() / OnPut()     取用/归还时的业务回调
  +-- Release(isShutdown)   真正的资源清理（抽象，子类实现）
*/

using System;

namespace NovaFramework.Runtime
{
    /// <summary>
    /// 对象基类。
    /// 管理真实资源的清理
    /// </summary>
    public abstract partial class ObjectBase : IReference
    {
        /// <summary>
        /// 初始化对象基类的新实例。
        /// </summary>
        public ObjectBase()
        {
            m_Name = null;
            m_Target = null;
            m_Locked = false;
            m_Priority = 0;
            m_LastUseTime = default(DateTime);
        }

        /// <summary>
        /// 清理对象基类。
        /// </summary>
        public virtual void Clear()
        {
            m_Name = null;
            m_Target = null;
            m_Locked = false;
            m_Priority = 0;
            m_LastUseTime = default(DateTime);
        }

        /// <summary>
        /// 获取对象时的事件。
        /// </summary>
        protected internal virtual void OnGet()
        {
        }

        /// <summary>
        /// 回收对象时的事件。
        /// </summary>
        protected internal virtual void OnPut()
        {
        }

        /// <summary>
        /// 释放对象。
        /// </summary>
        /// <param name="isShutdown">是否是关闭对象池时触发。</param>
        protected internal abstract void Release(bool isShutdown);
    }
}
