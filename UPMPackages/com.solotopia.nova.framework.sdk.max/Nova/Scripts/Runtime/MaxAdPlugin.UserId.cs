/***************************************************************
 * (c) copyright 2026 - 2030, Solotopia
 * All Rights Reserved.
 * -------------------------------------------------------------
 * filename:  MaxAdPlugin.UserId.cs
 * author:    yingzheng
 * created:   2026/5/18
 * descrip:   MaxAdPlugin 用户身份同步
 ***************************************************************/

using NovaFramework.SDK.AdPlugin.Runtime;

namespace NovaFramework.SDK.MaxAdPlugin.Runtime
{
    public sealed partial class MaxAdPlugin
    {
        /// <summary>
        /// 同步用户登录 userId 到 MAX SDK。
        /// </summary>
        /// <param name="userId">已登录用户唯一标识。</param>
        public override void SetUserId(string userId)
        {
            MaxSdk.SetUserId(userId);
        }
    }
}
