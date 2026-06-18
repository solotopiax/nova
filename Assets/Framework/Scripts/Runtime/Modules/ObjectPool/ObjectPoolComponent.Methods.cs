/***************************************************************
 * (c) copyright 2026 - 2030, Solotopia
 * All Rights Reserved.
 * -------------------------------------------------------------
 * filename:  ObjectPoolComponent.Methods.cs
 * author:    taoye
 * created:   2026/4/7
 * descrip:   对象池组件-方法
 ***************************************************************/

namespace NovaFramework.Runtime
{
    /// <summary>
    /// 对象池组件。
    /// </summary>
    public sealed partial class ObjectPoolComponent : FrameworkComponent
    {
        /// <summary>
        /// 释放对象池中的可释放对象。
        /// </summary>
        public void Release()
        {
            Log.Debug(LogTag.ObjectPool, "正在释放对象池中的可释放对象。");
            m_ObjectPoolManager.Release();
            Log.Debug(LogTag.ObjectPool, "释放对象池中的可释放对象完毕。");
        }

        /// <summary>
        /// 释放对象池中的所有未使用对象。
        /// </summary>
        public void ReleaseAllUnused()
        {
            Log.Debug(LogTag.ObjectPool, "正在释放对象池中的所有未使用对象。");
            m_ObjectPoolManager.ReleaseAllUnused();
            Log.Debug(LogTag.ObjectPool, "释放对象池中的所有未使用对象完毕。");
        }
    }
}
