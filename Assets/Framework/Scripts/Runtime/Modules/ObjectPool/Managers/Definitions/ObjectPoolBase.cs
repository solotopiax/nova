/***************************************************************
 * (c) copyright 2026 - 2030, Solotopia
 * All Rights Reserved.
 * -------------------------------------------------------------
 * filename:  ObjectPoolBase.cs
 * author:    taoye
 * created:   2025/12/10
 * descrip:   对象池基类
 ***************************************************************/

/*

为了统一遍历，给管理器提供非泛型入口，因此ObjectPoolBase需要是抽象类

ObjectPoolBase（抽象）
  |-- Name / FullName / ObjectType
  |-- Count / CanReleaseCount / AllowMultiGet
  |-- AutoReleaseInterval / Capacity / ExpireTime / Priority
  |-- Release() / Release(count) / ReleaseAllUnused()
  |-- GetAllObjectInfos()
  |-- Update() / Shutdown()
  +-- （全抽象，无任何实现细节）
*/

namespace NovaFramework.Runtime
{
    /// <summary>
    /// 对象池基类。
    /// </summary>
    public abstract partial class ObjectPoolBase
    {
        /// <summary>
        /// 初始化对象池基类的新实例。
        /// </summary>
        public ObjectPoolBase()
            : this(null)
        {
        }

        /// <summary>
        /// 初始化对象池基类的新实例。
        /// </summary>
        /// <param name="name">对象池名称。</param>
        public ObjectPoolBase(string name)
        {
            m_Name = name ?? string.Empty;
        }

        /// <summary>
        /// 释放对象池中的可释放对象。
        /// </summary>
        public abstract void Release();

        /// <summary>
        /// 释放对象池中的可释放对象。
        /// </summary>
        /// <param name="toReleaseCount">尝试释放对象数量。</param>
        public abstract void Release(int toReleaseCount);

        /// <summary>
        /// 释放对象池中的所有未使用对象。
        /// </summary>
        public abstract void ReleaseAllUnused();

        /// <summary>
        /// 获取所有对象信息。
        /// </summary>
        /// <returns>所有对象信息。</returns>
        public abstract ObjectInfo[] GetAllObjectInfos();

        /// <summary>
        /// 对象池轮询。
        /// </summary>
        public abstract void Update();

        /// <summary>
        /// 关闭并清理对象池。
        /// </summary>
        public abstract void Shutdown();
    }
}
