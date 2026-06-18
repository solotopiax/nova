/***************************************************************
 * (c) copyright 2026 - 2030, Solotopia
 * All Rights Reserved.
 * -------------------------------------------------------------
 * filename:  EventData.cs
 * author:    taoye
 * created:   2026/1/16
 * descrip:   框架事件基类
 ***************************************************************/

using System;

namespace NovaFramework.Runtime
{
    /// <summary>
    /// 事件基类。
    /// </summary>
    public abstract class EventData : EventArgs, IReference
    {
        /// <summary>
        /// 类型编号。
        /// </summary>
        public int ID => EventTypeID.Get(GetType());

        /// <summary>
        /// 清理引用。
        /// </summary>
        public abstract void Clear();

        /// <summary>
        /// 克隆当前事件数据（深拷贝），用于异步场景下安全持有事件数据。
        /// 默认抛出 NotSupportedException，需要异步安全的子类应自行 override。
        /// </summary>
        /// <returns>克隆后的事件数据实例（不受引用池管理，由 GC 回收）。</returns>
        public virtual EventData Clone()
        {
            throw new NotSupportedException(Txt.Format("事件 '{0}' 未实现 Clone 方法。如需在异步 handler 中安全持有事件数据，请在子类中 override Clone()。", GetType().Name));
        }
    }
}
