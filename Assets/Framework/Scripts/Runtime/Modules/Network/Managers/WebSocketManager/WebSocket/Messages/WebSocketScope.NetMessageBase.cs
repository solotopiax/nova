/***************************************************************
 * (c) copyright 2026 - 2030, Solotopia
 * All Rights Reserved.
 * -------------------------------------------------------------
 * filename:  WebSocketScope.NetMessageBase.cs
 * author:    taoye
 * created:   2026/3/9
 * descrip:   网络消息基类
 ***************************************************************/

namespace NovaFramework.Runtime
{
    public partial class WebSocketScope
    {
        /// <summary>
        /// 网络消息基类，定义消息结构的通用外壳与可复用接口。
        /// 实现 IReference 以接入框架 ReferencePool 统一管理生命周期。
        /// </summary>
        public abstract class NetMessageBase : IReference
        {
            /// <summary>
            /// 通用消息外壳长度（字节）：协议 ID 4 字节 + 序列号 4 字节。
            /// </summary>
            public const int c_OuterShellLength = 8;

            /// <summary>
            /// 获取消息体字节长度。
            /// </summary>
            /// <returns>消息体字节数。</returns>
            public abstract int GetBodyLength();

            /// <summary>
            /// 复位消息对象到初始状态（供对象池复用）。
            /// </summary>
            public abstract void Reset();

            /// <summary>
            /// 清理引用，将消息对象复位到初始状态。
            /// 由 ReferencePool 在归还时自动调用。
            /// </summary>
            public virtual void Clear()
            {
                Reset();
            }
        }
    }
}
