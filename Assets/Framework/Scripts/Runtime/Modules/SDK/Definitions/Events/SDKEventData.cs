/***************************************************************
 * (c) copyright 2026 - 2030, Solotopia
 * All Rights Reserved.
 * -------------------------------------------------------------
 * filename:  SDKEventData.cs
 * author:    yingzheng
 * created:   2026/4/28
 * descrip:   SDK 事件数据定义
 ***************************************************************/

namespace NovaFramework.Runtime
{
    /// <summary>
    /// SDK 事件数据定义。包含所有 SDK 相关事件的强类型子类。
    /// </summary>
    public static class SDKEventData
    {
        /// <summary>
        /// 用户登录事件数据。由 SDKManager.Login 触发，携带已登录用户的唯一标识。
        /// </summary>
        public sealed class UserLogin : EventData
        {
            /// <summary>
            /// 已登录用户的唯一标识。
            /// </summary>
            public string UserId { get; private set; }

            /// <summary>
            /// 创建用户登录事件数据实例。
            /// </summary>
            /// <param name="userId">已登录用户的唯一标识。</param>
            /// <returns>从 ReferencePool 取出并完成字段赋值的事件数据实例。</returns>
            public static UserLogin Create(string userId)
            {
                UserLogin e = ReferencePool.Get<UserLogin>();
                e.UserId = userId;
                return e;
            }

            /// <summary>
            /// 清理引用，将字段重置为默认值，供 ReferencePool 回收复用。
            /// </summary>
            public override void Clear()
            {
                UserId = null;
            }
        }
    }
}
