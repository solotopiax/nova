/***************************************************************
 * (c) copyright 2026 - 2030, Solotopia
 * All Rights Reserved.
 * -------------------------------------------------------------
 * filename:  DemoBullet.cs
 * author:    taoye
 * created:   2026/05/23
 * descrip:   ObjectPool 演示用占位对象，继承 ObjectBase 接入对象池管理。
 ***************************************************************/

using NovaFramework.Runtime;

namespace NovaFramework.Samples.Runtime
{
    /// <summary>
    /// ObjectPool 模块演示用占位子弹对象，继承 ObjectBase，由池管理生命周期。
    /// 纯 C# 占位，无 GameObject 实体；以自身作为唯一 Target，满足池按 Target 归还的身份契约。
    /// </summary>
    public sealed class DemoBullet : ObjectBase
    {
        /// <summary>
        /// 从引用池创建占位对象，以自身提供非空唯一 Target，随后交给对象池注册和回收。
        /// </summary>
        /// <returns>已初始化的 DemoBullet。</returns>
        public static DemoBullet Create()
        {
            DemoBullet bullet = ReferencePool.Get<DemoBullet>();
            bullet.Initialize("DemoBullet", bullet);
            return bullet;
        }

        /// <summary>
        /// 取出对象时回调，可在此重置业务状态。
        /// </summary>
        protected override void OnGet()
        {
        }

        /// <summary>
        /// 归还对象时回调，可在此清理业务状态。
        /// </summary>
        protected override void OnPut()
        {
        }

        /// <summary>
        /// 释放对象时回调，无 GameObject 实体无需额外清理。
        /// </summary>
        /// <param name="isShutdown">是否因对象池关闭触发。</param>
        protected override void Release(bool isShutdown)
        {
        }

    }
}
