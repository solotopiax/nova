/***************************************************************
 * (c) copyright 2026 - 2030, Solotopia
 * All Rights Reserved.
 * author: Codex
 * created: 2026/10/08
 * filename: IUserPropertyPlugin.cs
 * descrip: 可选的一次性用户属性能力，不强制依赖具体统计子包
 ***************************************************************/
using System.Collections.Generic;

namespace NovaFramework.Runtime
{
    /// <summary>只设置尚无值的用户属性；主线程调用，由实现方保留属性值类型。</summary>
    public interface IUserPropertyPlugin : ISDKPlugin
    {
        /// <summary>已存在的属性不得覆盖；调用成功代表提交 SDK，不保证服务端收数。</summary>
        void UserSetOnce(Dictionary<string, object> properties);
    }
}
