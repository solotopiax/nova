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
    /// Target 为 null（纯 C# 占位，无 GameObject 实体）。
    /// </summary>
    public sealed class DemoBullet : ObjectBase
    {
        /// <summary>
        /// 创建并初始化 DemoBullet 实例，向对象池注册可用。
        /// </summary>
        /// <returns>已初始化的 DemoBullet。</returns>
        public static DemoBullet Create()
        {
            DemoBullet bullet = ReferencePool.Get<DemoBullet>();
            bullet.Initialize("DemoBullet", null);
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
