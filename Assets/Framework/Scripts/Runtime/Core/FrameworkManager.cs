/***************************************************************
 * (c) copyright 2026 - 2030, Solotopia
 * All Rights Reserved.
 * -------------------------------------------------------------
 * filename:  FrameworkManager.cs
 * author:    taoye
 * created:   2025/12/5
 * descrip:   框架管理器抽象类
 ***************************************************************/

using System;

namespace NovaFramework.Runtime
{
    /// <summary>
    /// 框架管理器抽象类。
    /// </summary>
    public abstract class FrameworkManager
    {
        /// <summary>
        /// 获取框架管理器优先级。
        /// </summary>
        /// <remarks>值越小优先级越高，越先 Update、越后 Shutdown。</remarks>
        public virtual int Priority => 0;

        /// <summary>
        /// 构造方法。
        /// </summary>
        public FrameworkManager()
        {
            try
            {
                FrameworkManagersGroup.RegisterManager(this);
            }
            catch (Exception)
            {
                FrameworkManagersGroup.UnregisterManager(this);
                throw;
            }
        }

        /// <summary>
        /// 框架管理器轮询。
        /// </summary>
        public abstract void Update();

        /// <summary>
        /// 关闭并清理管理器。
        /// </summary>
        public abstract void Shutdown();
    }
}
